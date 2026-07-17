param(
  [string]$Region = "sa-bogota-1",
  [string]$BucketName = "academic-registration-spa-pilot",
  [string]$ApiBaseUrl = "https://api.157.137.225.220.sslip.io/api",
  [string]$GatewayBaseUrl = "",
  [string]$CompartmentId = "",
  [string]$Auth = "security_token",
  [string]$OciCliPath = "C:\o\Scripts\oci.exe",
  [string]$K8sNamespace = "academic-registration",
  [int]$CorsIndex = 3,
  [string]$CorsOrigin = "",
  [switch]$SkipBuild,
  [switch]$SkipBucketCreate,
  [switch]$UpdateK8sCors
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$FrontendDir = Join-Path $RepoRoot "frontend"
$DistDir = Join-Path $FrontendDir "dist\academic-registration-web\browser"
$OciArgs = @("--auth", $Auth, "--region", $Region)

function Invoke-CheckedNativeCommand {
  param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath,
    [Parameter(Mandatory = $true)]
    [string[]]$Arguments
  )

  & $FilePath @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
  }
}

function Get-DefaultCompartmentId {
  $configPath = Join-Path $env:USERPROFILE ".oci\config"
  if (-not (Test-Path -LiteralPath $configPath)) {
    throw "CompartmentId was not provided and OCI config was not found at $configPath."
  }

  $tenancyLine = Select-String -Path $configPath -Pattern '^tenancy=' | Select-Object -First 1
  if (-not $tenancyLine) {
    throw "CompartmentId was not provided and tenancy= was not found in $configPath."
  }

  return $tenancyLine.Line.Split('=')[1].Trim()
}

function Get-ContentType {
  param([string]$Path)

  switch ([IO.Path]::GetExtension($Path).ToLowerInvariant()) {
    ".html" { return "text/html; charset=utf-8" }
    ".css" { return "text/css; charset=utf-8" }
    ".js" { return "text/javascript; charset=utf-8" }
    ".mjs" { return "text/javascript; charset=utf-8" }
    ".json" { return "application/json; charset=utf-8" }
    ".map" { return "application/json; charset=utf-8" }
    ".svg" { return "image/svg+xml" }
    ".ico" { return "image/x-icon" }
    ".png" { return "image/png" }
    ".jpg" { return "image/jpeg" }
    ".jpeg" { return "image/jpeg" }
    ".webp" { return "image/webp" }
    ".gif" { return "image/gif" }
    ".txt" { return "text/plain; charset=utf-8" }
    ".woff" { return "font/woff" }
    ".woff2" { return "font/woff2" }
    ".ttf" { return "font/ttf" }
    default { return "application/octet-stream" }
  }
}

function Get-CacheControl {
  param([string]$ObjectName)

  if ($ObjectName -in @("index.html", "app-config.json")) {
    return "no-cache"
  }

  return "public, max-age=31536000, immutable"
}

if (-not (Test-Path -LiteralPath $OciCliPath)) {
  throw "OCI CLI was not found at $OciCliPath. Run k8s\oci\scripts\install-oci-cli.ps1 or pass -OciCliPath."
}

if (-not $CompartmentId) {
  $CompartmentId = Get-DefaultCompartmentId
}

if ($GatewayBaseUrl) {
  Write-Warning "-GatewayBaseUrl is deprecated. Use -ApiBaseUrl instead."
  $ApiBaseUrl = $GatewayBaseUrl
}

if (-not $SkipBuild) {
  Push-Location $FrontendDir
  try {
    npm run build -- --base-href ./
    if ($LASTEXITCODE -ne 0) {
      throw "Angular build failed with exit code $LASTEXITCODE."
    }
  }
  finally {
    Pop-Location
  }
}

if (-not (Test-Path -LiteralPath $DistDir)) {
  throw "Build output not found at $DistDir. Run without -SkipBuild first."
}

$appConfigPath = Join-Path $DistDir "app-config.json"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$appConfigJson = @{
  apiBaseUrl = $ApiBaseUrl.TrimEnd("/")
} | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($appConfigPath, $appConfigJson + [Environment]::NewLine, $utf8NoBom)

$objectNamespace = (& $OciCliPath os ns get @OciArgs --query data --raw-output).Trim()
if ($LASTEXITCODE -ne 0) {
  throw "Could not resolve Object Storage namespace."
}

if (-not $SkipBucketCreate) {
  $previousErrorActionPreference = $ErrorActionPreference
  $ErrorActionPreference = "SilentlyContinue"
  try {
    & $OciCliPath os bucket get @OciArgs --namespace-name $objectNamespace --bucket-name $BucketName *> $null
    $bucketExists = $LASTEXITCODE -eq 0
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  if (-not $bucketExists) {
    Invoke-CheckedNativeCommand $OciCliPath (@("os", "bucket", "create") + $OciArgs + @(
      "--namespace-name", $objectNamespace,
      "--compartment-id", $CompartmentId,
      "--name", $BucketName,
      "--public-access-type", "ObjectReadWithoutList"
    ))
  }
  else {
    Invoke-CheckedNativeCommand $OciCliPath (@("os", "bucket", "update") + $OciArgs + @(
      "--namespace-name", $objectNamespace,
      "--bucket-name", $BucketName,
      "--public-access-type", "ObjectReadWithoutList"
    ))
  }
}

$rootPath = (Resolve-Path $DistDir).Path.TrimEnd("\") + "\"
$files = Get-ChildItem -LiteralPath $DistDir -Recurse -File
foreach ($file in $files) {
  $objectName = $file.FullName.Substring($rootPath.Length).Replace("\", "/")
  $contentType = Get-ContentType $file.FullName
  $cacheControl = Get-CacheControl $objectName

  Write-Host "Uploading $objectName ($contentType)"
  Invoke-CheckedNativeCommand $OciCliPath (@("os", "object", "put") + $OciArgs + @(
    "--namespace-name", $objectNamespace,
    "--bucket-name", $BucketName,
    "--name", $objectName,
    "--file", $file.FullName,
    "--content-type", $contentType,
    "--cache-control", $cacheControl,
    "--force"
  ))
}

$objectStorageOrigin = "https://objectstorage.$Region.oraclecloud.com"
if (-not $CorsOrigin) {
  $CorsOrigin = $objectStorageOrigin
}

if ($UpdateK8sCors) {
  $corsKey = "Cors__AllowedOrigins__$CorsIndex"
  $patch = @{
    data = @{
      $corsKey = $CorsOrigin
    }
  } | ConvertTo-Json -Depth 5 -Compress

  $patchFile = Join-Path ([IO.Path]::GetTempPath()) "academic-registration-cors-patch.json"
  [System.IO.File]::WriteAllText($patchFile, $patch, $utf8NoBom)
  kubectl patch configmap academic-registration-config -n $K8sNamespace --type merge --patch-file $patchFile
  if ($LASTEXITCODE -ne 0) { throw "Failed to update ConfigMap CORS origin." }

  kubectl rollout restart deployment/gateway -n $K8sNamespace
  if ($LASTEXITCODE -ne 0) { Write-Warning "Gateway deployment was not restarted. It may be disabled in the simplified PoC." }

  kubectl rollout restart deployment/api -n $K8sNamespace
  if ($LASTEXITCODE -ne 0) { throw "Failed to restart api deployment." }
}

$indexUrl = "$objectStorageOrigin/n/$objectNamespace/b/$BucketName/o/index.html"
Write-Host ""
Write-Host "SPA published to:"
Write-Host $indexUrl
Write-Host ""
Write-Host "Runtime API config:"
Write-Host "  apiBaseUrl = $($ApiBaseUrl.TrimEnd('/'))"
Write-Host ""
Write-Host "CORS origin to allow in API:"
Write-Host "  $CorsOrigin"
