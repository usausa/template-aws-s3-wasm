# Reflect cdk outputs into appsettings.Development.json for local development.
# Usage: ./scripts/update-appsettings.ps1 -Env dev

param(
    [ValidateSet('dev', 'prod')]
    [string] $Env = 'dev'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$root = Split-Path -Parent $PSScriptRoot
$outputs = Get-StackOutputs -Root $root -EnvName $Env

Write-AppSettings -Outputs $outputs -Path (Join-Path $root 'Frontend/wwwroot/appsettings.Development.json')

Write-Host "Run 'dotnet run --project Frontend' (http://localhost:5250) to connect to the $Env stack."
