param(
  [Parameter(Mandatory = $true)]
  [string]$RegionKey,
  [Parameter(Mandatory = $true)]
  [string]$TenancyNamespace,
  [Parameter(Mandatory = $true)]
  [string]$DockerUsername,
  [Parameter(Mandatory = $true)]
  [securestring]$DockerAuthToken,
  [string]$RepositoryPrefix = "academic-registration",
  [string]$Tag = "dev",
  [string]$Namespace = "academic-registration",
  [string]$MysqlRootPassword = "",
  [string]$EmailUsername = "",
  [string]$EmailPassword = "",
  [string]$RegistryHost = "",
  [switch]$SkipIngressController
)

$ErrorActionPreference = "Stop"

$ManifestDir = Resolve-Path (Join-Path $PSScriptRoot "..\manifests")
if ($RegistryHost) {
  $Registry = $RegistryHost
}
elseif ($RegionKey -like "*-*-*") {
  $Registry = "ocir.$RegionKey.oci.oraclecloud.com"
}
else {
  $Registry = "$RegionKey.ocir.io"
}
$ImagePrefix = "$Registry/$TenancyNamespace/$RepositoryPrefix"

function Invoke-CheckedCommand {
  param(
    [Parameter(Mandatory = $true)]
    [scriptblock]$Command
  )

  & $Command
  if ($LASTEXITCODE -ne 0) {
    throw "Command failed with exit code $LASTEXITCODE"
  }
}

function Convert-SecureStringToPlainText {
  param([securestring]$SecureValue)

  $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
  try {
    return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
  }
  finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
  }
}

function New-Password {
  $bytes = New-Object byte[] 24
  [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
  return [Convert]::ToBase64String($bytes).TrimEnd("=")
}

if (-not $MysqlRootPassword) {
  $MysqlRootPassword = New-Password
}

$plainDockerToken = Convert-SecureStringToPlainText $DockerAuthToken

Invoke-CheckedCommand { kubectl apply -f (Join-Path $ManifestDir "00-namespace.yaml") }

$dockerServer = $Registry
Invoke-CheckedCommand {
  kubectl create secret docker-registry ocir-secret `
    --namespace $Namespace `
    --docker-server $dockerServer `
    --docker-username $DockerUsername `
    --docker-password $plainDockerToken `
    --dry-run=client `
    -o yaml | kubectl apply -f -
}

Invoke-CheckedCommand {
  kubectl create secret generic academic-registration-secrets `
    --namespace $Namespace `
    --from-literal "mysql-root-password=$MysqlRootPassword" `
    --from-literal "email-username=$EmailUsername" `
    --from-literal "email-password=$EmailPassword" `
    --dry-run=client `
    -o yaml | kubectl apply -f -
}

Invoke-CheckedCommand { kubectl apply -f $ManifestDir }

Invoke-CheckedCommand {
  kubectl set image deployment/api api="$ImagePrefix/api:$Tag" -n $Namespace
}
Invoke-CheckedCommand {
  kubectl set image deployment/gateway gateway="$ImagePrefix/gateway:$Tag" -n $Namespace
}
Invoke-CheckedCommand {
  kubectl set image deployment/notifications-worker notifications-worker="$ImagePrefix/notifications:$Tag" -n $Namespace
}
Invoke-CheckedCommand {
  kubectl set image deployment/frontend frontend="$ImagePrefix/frontend:$Tag" -n $Namespace
}

if (-not $SkipIngressController) {
  Write-Host "Make sure ingress-nginx is installed in the cluster."
  Write-Host "If it is not installed, use k8s\oci\scripts\install-ingress-nginx.ps1."
}

Invoke-CheckedCommand { kubectl rollout status deployment/mysql -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/kafka -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/api -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/notifications-worker -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/gateway -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/frontend -n $Namespace --timeout=5m }

kubectl get pods,svc,ingress,rs -n $Namespace
