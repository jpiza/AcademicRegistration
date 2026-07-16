param(
  [Parameter(Mandatory = $true)]
  [string]$RegionKey,
  [Parameter(Mandatory = $true)]
  [string]$TenancyNamespace,
  [string]$RepositoryPrefix = "academic-registration",
  [string]$Tag = "dev",
  [string]$Platform = "linux/amd64",
  [ValidateSet("push", "load")]
  [string]$Output = "push",
  [string]$RegistryHost = ""
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
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

function Build-And-Push {
  param(
    [string]$Name,
    [string]$Dockerfile,
    [string]$Context
  )

  $image = "$ImagePrefix/$Name`:$Tag"
  Write-Host "Building $image"
  Push-Location $RepoRoot
  try {
    if ($Output -eq "push") {
      Invoke-CheckedCommand {
        docker buildx build --platform $Platform -t $image -f $Dockerfile $Context --push
      }
    }
    else {
      Invoke-CheckedCommand {
        docker buildx build --platform $Platform -t $image -f $Dockerfile $Context --load
      }
    }
  }
  finally {
    Pop-Location
  }
}

Write-Host "Registry: $Registry"
Write-Host "Image prefix: $ImagePrefix"
Write-Host ""
if ($Output -eq "push") {
  Write-Host "Make sure you already ran docker login $Registry in your own terminal."
  Write-Host "Use an OCI auth token, not your account password."
}
else {
  Write-Host "Local build mode: images will be loaded into Docker and not pushed."
}
Write-Host ""

Build-And-Push -Name "api" -Dockerfile "backend/Dockerfile.api" -Context "backend"
Build-And-Push -Name "gateway" -Dockerfile "backend/Dockerfile.gateway" -Context "backend"
Build-And-Push -Name "notifications" -Dockerfile "backend/Dockerfile.notifications" -Context "backend"
Build-And-Push -Name "frontend" -Dockerfile "frontend/Dockerfile" -Context "frontend"

Write-Host ""
if ($Output -eq "push") {
  Write-Host "Images pushed:"
}
else {
  Write-Host "Images built locally:"
}
Write-Host "  $ImagePrefix/api:$Tag"
Write-Host "  $ImagePrefix/gateway:$Tag"
Write-Host "  $ImagePrefix/notifications:$Tag"
Write-Host "  $ImagePrefix/frontend:$Tag"
