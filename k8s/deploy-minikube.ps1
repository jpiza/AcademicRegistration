param(
  [switch]$SkipBuild,
  [switch]$SkipIngressAddon
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$Namespace = "academic-registration"

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

function Build-MinikubeImage {
  param(
    [string]$Tag,
    [string]$Dockerfile,
    [string]$Context
  )

  Write-Host "Building $Tag"
  Push-Location $RepoRoot
  try {
    $output = & minikube image build -t $Tag -f $Dockerfile $Context 2>&1
    $output | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0 -or ($output -match "^ERROR:")) {
      throw "Image build failed for $Tag."
    }
  }
  finally {
    Pop-Location
  }
}

if (-not $SkipBuild) {
  Build-MinikubeImage `
    -Tag "academic-registration-api:dev" `
    -Dockerfile "Dockerfile.api" `
    -Context "backend"

  Build-MinikubeImage `
    -Tag "academic-registration-gateway:dev" `
    -Dockerfile "Dockerfile.gateway" `
    -Context "backend"

  Build-MinikubeImage `
    -Tag "academic-registration-notifications:dev" `
    -Dockerfile "Dockerfile.notifications" `
    -Context "backend"

  Build-MinikubeImage `
    -Tag "academic-registration-frontend:dev" `
    -Dockerfile "Dockerfile" `
    -Context "frontend"
}

if (-not $SkipIngressAddon) {
  Invoke-CheckedCommand { minikube addons enable ingress }
}

Invoke-CheckedCommand { kubectl apply -f $ScriptDir }

Invoke-CheckedCommand { kubectl rollout status deployment/sqlserver -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/kafka -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/api -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/notifications-worker -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/gateway -n $Namespace --timeout=5m }
Invoke-CheckedCommand { kubectl rollout status deployment/frontend -n $Namespace --timeout=5m }

Write-Host ""
Write-Host "Resources are ready in namespace '$Namespace'."
$minikubeIp = minikube ip
if ($LASTEXITCODE -ne 0) {
  throw "Could not get Minikube IP."
}
Write-Host "Minikube IP: $minikubeIp"
Write-Host ""
Write-Host "For Minikube with the Docker driver on Windows, keep this running in another terminal:"
Write-Host "  minikube tunnel"
Write-Host "Then map these hosts to 127.0.0.1 in your Windows hosts file:"
Write-Host "  academic-registration.local"
Write-Host "  gateway.academic-registration.local"
Write-Host "  api.academic-registration.local"
Write-Host ""
Write-Host "If your Minikube driver exposes the node IP directly, map those hosts to $minikubeIp instead."
