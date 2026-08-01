# Publish the Template.Backend Lambda so the CDK stack can pick it up as an asset.
# Usage: ./scripts/deploy-api.ps1
#
# Run this before 'cdk deploy'. The stack reads publish-api/ from the repository root, so the
# CDK app itself performs no build and needs no Docker.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'publish-api'

# 'dotnet publish -o' does not clean the output directory; stale assemblies would ship otherwise.
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

dotnet publish (Join-Path $root 'Template.Backend/Template.Backend.csproj') -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Write-Host ''
Write-Host "Lambda artifact ready: $publishDir"
Write-Host "Next: cd Template.IaC; npx --yes aws-cdk@latest deploy -c env=dev --outputs-file ../cdk-outputs.dev.json"
