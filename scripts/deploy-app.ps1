# Publish Frontend and deploy it to S3 + CloudFront.
# Usage: ./scripts/deploy-app.ps1 -Env dev
#
# Prerequisite: the IaC stack is deployed and cdk-outputs.{env}.json exists at the repository root
# (see common.ps1 for the exact deploy command).

param(
    [ValidateSet('dev', 'prod')]
    [string] $Env = 'dev'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$root = Split-Path -Parent $PSScriptRoot
$outputs = Get-StackOutputs -Root $root -EnvName $Env
$bucket = $outputs.AppBucketName

# 1. Write the target environment settings as the Production overlay before publishing
#    (a published WASM app always runs with the Production environment).
Write-AppSettings -Outputs $outputs -Path (Join-Path $root 'Frontend/wwwroot/appsettings.Production.json')

# 2. Publish. The output directory is wiped first: 'dotnet publish -o' does not clean it, so
#    fingerprinted assets from earlier builds would otherwise pile up and get uploaded forever.
$publishDir = Join-Path $root 'publish'
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

dotnet publish (Join-Path $root 'Frontend/Frontend.csproj') -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$wwwroot = Join-Path $publishDir 'wwwroot'

# 3. Sync to S3, uploading everything as no-cache first; step 4 promotes the immutable assets.
#    Skipped uploads:
#      *.br / *.gz  - S3 as a CloudFront origin does no content negotiation, so these
#                     pre-compressed copies are never requested. CloudFront compresses on the fly.
#      appsettings.Development.json - only used by 'dotnet run' locally.
$skip = @('--exclude', '*.br', '--exclude', '*.gz', '--exclude', 'appsettings.Development.json')

aws s3 sync $wwwroot "s3://$bucket" --delete @skip --cache-control 'no-cache'
if ($LASTEXITCODE -ne 0) { throw 'aws s3 sync failed.' }

# Excluded files are also exempt from --delete, so purge leftovers from earlier deployments.
aws s3 rm "s3://$bucket" --recursive --exclude '*' --include '*.br' --include '*.gz' `
    --include 'appsettings.Development.json' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Removing unused objects failed.' }

# 4. Promote the fingerprinted assets under _framework/ to long-lived immutable caching, and
#    pin their Content-Type at the same time (some environments guess application/octet-stream
#    for .wasm, which drops it from CloudFront compression).
#
#    Every _framework asset except dotnet.js and blazor.webassembly.js carries a content hash in
#    its name, so a new build produces new URLs and a stale copy can never be paired with a new
#    manifest. The entry points that resolve those names stay no-cache from step 3, which is what
#    keeps the set consistent across deployments.
$immutable = 'public, max-age=31536000, immutable'

aws s3 cp "s3://$bucket/_framework/" "s3://$bucket/_framework/" --recursive `
    --exclude '*' --include '*.wasm' `
    --content-type 'application/wasm' --cache-control $immutable --metadata-directive REPLACE | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Setting cache headers on .wasm failed.' }

aws s3 cp "s3://$bucket/_framework/" "s3://$bucket/_framework/" --recursive `
    --exclude '*' --include '*.js' --exclude 'dotnet.js' --exclude 'blazor.webassembly.js' `
    --content-type 'text/javascript' --cache-control $immutable --metadata-directive REPLACE | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Setting cache headers on .js failed.' }

# 5. CloudFront only compresses objects up to 10 MB, so any bigger asset (the AOT native
#    runtime is ~19 MB) would ship uncompressed on every first visit and can exceed browser
#    per-entry cache limits. Replace such objects with their publish-generated Brotli bodies
#    at the same key. Browsers universally send Accept-Encoding: br over HTTPS, and fetch
#    integrity checks run over the decoded bytes, so nothing else changes.
$bigFiles = Get-ChildItem (Join-Path $wwwroot '_framework') -File |
    Where-Object { ($_.Length -gt 9MB) -and ($_.Extension -in '.wasm', '.js') }
foreach ($file in $bigFiles) {
    $brotli = "$($file.FullName).br"
    if (Test-Path $brotli) {
        $type = if ($file.Extension -eq '.wasm') { 'application/wasm' } else { 'text/javascript' }
        aws s3 cp $brotli "s3://$bucket/_framework/$($file.Name)" `
            --content-encoding br --content-type $type --cache-control $immutable | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Uploading compressed large asset failed.' }
        Write-Host "Compressed in place: $($file.Name) ($([Math]::Round($file.Length/1MB,1)) MB -> $([Math]::Round((Get-Item $brotli).Length/1MB,1)) MB)"
    }
}

# 6. Invalidate the CloudFront cache ('/*' counts as a single path, so this stays within the free tier).
aws cloudfront create-invalidation --distribution-id $outputs.DistributionId --paths '/*' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'CloudFront invalidation failed.' }

Write-Host ''
Write-Host "Deployed: https://$($outputs.CloudFrontDomain)/"
