# Create a test user and place sample data under users/{sub}/ in the data bucket.
# Usage: ./scripts/seed-user.ps1 -Env dev -Email user1@example.com
#
# Self sign-up is disabled (admin-issued users only), so issuing real users follows
# the same procedure as this script.

param(
    [Parameter(Mandatory = $true)]
    [string] $Email,

    [ValidateSet('dev', 'prod')]
    [string] $Env = 'dev',

    # When omitted, a random password satisfying the pool policy is generated.
    [string] $Password
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$root = Split-Path -Parent $PSScriptRoot
$outputs = Get-StackOutputs -Root $root -EnvName $Env
$userPoolId = $outputs.UserPoolId
$bucket = $outputs.DataBucketName

if (-not $Password) {
    $Password = 'Aa1!' + [guid]::NewGuid().ToString('N').Substring(0, 12)
}

# 1. Create the user (no invitation mail; the password is set directly).
#    Attribute specs are quoted so PowerShell does not split them at the comma.
aws cognito-idp admin-create-user --user-pool-id $userPoolId --username $Email `
    --user-attributes "Name=email,Value=$Email" "Name=email_verified,Value=true" `
    --message-action SUPPRESS | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Warning 'admin-create-user failed (continuing in case the user already exists).'
}

aws cognito-idp admin-set-user-password --user-pool-id $userPoolId --username $Email --password $Password --permanent
if ($LASTEXITCODE -ne 0) { throw 'Setting the password failed.' }

# 2. Get the sub (S3 prefixes and the IAM principal tag are keyed by the user pool sub).
$user = aws cognito-idp admin-get-user --user-pool-id $userPoolId --username $Email | ConvertFrom-Json
$sub = ($user.UserAttributes | Where-Object Name -eq 'sub').Value
if (-not $sub) { throw 'Could not get the sub attribute.' }

# 3. Place sample data.
#    The CSV layout is "date,value,note", which the app parses into a chart and a table.
$stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$work = Join-Path ([System.IO.Path]::GetTempPath()) "seed-$sub"
New-Item -ItemType Directory -Force (Join-Path $work 'reports') | Out-Null

@{ email = $Email; sub = $sub; seededAt = $stamp } | ConvertTo-Json | Set-Content (Join-Path $work 'profile.json') -Encoding utf8

# Deterministic per-user pseudo-random series so two seeded users get different shapes.
$seed = [Math]::Abs($sub.GetHashCode())
$rng = [System.Random]::new($seed)

foreach ($month in @('2026-05', '2026-06', '2026-07')) {
    $first = [datetime]::ParseExact("$month-01", 'yyyy-MM-dd', $null)
    $days = [datetime]::DaysInMonth($first.Year, $first.Month)
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('date,value,note')

    for ($i = 0; $i -lt $days; $i++) {
        $date = $first.AddDays($i)
        # Weekday baseline with a weekend dip, a slow upward trend and a little noise.
        $weekend = ($date.DayOfWeek -eq 'Saturday') -or ($date.DayOfWeek -eq 'Sunday')
        $base = if ($weekend) { 60 } else { 130 }
        $value = [Math]::Round($base + ($i * 1.5) + $rng.Next(-18, 19), 1)
        $note = if ($weekend) { 'weekend' } else { '' }
        $lines.Add("$($date.ToString('yyyy-MM-dd')),$value,$note")
    }

    $lines -join "`n" | Set-Content (Join-Path $work "reports/$month.csv") -Encoding utf8
}

"This file was placed by seed-user.ps1. ($stamp)" | Set-Content (Join-Path $work 'readme.txt') -Encoding utf8

aws s3 cp $work "s3://$bucket/users/$sub/" --recursive
if ($LASTEXITCODE -ne 0) { throw 'Placing sample data failed.' }

Remove-Item -Recurse -Force $work

# 4. Show the result.
Write-Host ''
Write-Host '===== Test user ====='
Write-Host "URL      : https://$($outputs.CloudFrontDomain)/"
Write-Host "Email    : $Email"
Write-Host "Password : $Password"
Write-Host "sub      : $sub"
Write-Host "Data     : s3://$bucket/users/$sub/"
