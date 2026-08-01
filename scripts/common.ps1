# Shared helpers dot-sourced by deploy-app.ps1 / seed-user.ps1 / update-appsettings.ps1.

$Script:Region = 'ap-northeast-1'

function Get-StackOutputs {
    param(
        [Parameter(Mandatory = $true)] [string] $Root,
        [Parameter(Mandatory = $true)] [string] $EnvName
    )

    $path = Join-Path $Root "cdk-outputs.$EnvName.json"
    if (-not (Test-Path $path)) {
        throw "cdk outputs not found: $path`nRun this in the IaC directory: npx --yes aws-cdk@latest deploy -c env=$EnvName --outputs-file ../cdk-outputs.$EnvName.json"
    }

    $stack = "template-aws-s3-wasm-$EnvName"
    $outputs = (Get-Content $path -Raw | ConvertFrom-Json).$stack
    if ($null -eq $outputs) {
        throw "Stack $stack not found in outputs: $path"
    }

    return $outputs
}

function Write-AppSettings {
    param(
        [Parameter(Mandatory = $true)] $Outputs,
        [Parameter(Mandatory = $true)] [string] $Path
    )

    # All values here are public by design (authorization is enforced by IAM; see docs/DESIGN.md).
    $settings = [ordered]@{
        Oidc = [ordered]@{
            Authority    = "https://cognito-idp.$Script:Region.amazonaws.com/$($Outputs.UserPoolId)"
            ClientId     = $Outputs.UserPoolClientId
            ResponseType = 'code'
        }
        App = [ordered]@{
            Region         = $Script:Region
            UserPoolId     = $Outputs.UserPoolId
            IdentityPoolId = $Outputs.IdentityPoolId
            CognitoDomain  = $Outputs.CognitoDomain
            DataBucket     = $Outputs.DataBucketName
            ApiEndpoint    = $Outputs.ApiEndpoint
        }
    }

    $settings | ConvertTo-Json -Depth 5 | Set-Content -Path $Path -Encoding utf8
    Write-Host "Updated: $Path"
}
