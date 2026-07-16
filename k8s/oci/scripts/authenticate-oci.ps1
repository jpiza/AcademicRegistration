param(
  [Parameter(Mandatory = $true)]
  [string]$Region,
  [string]$ProfileName = "DEFAULT",
  [string]$OciExe = "C:\o\Scripts\oci.exe"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
if ([System.IO.Path]::IsPathRooted($OciExe)) {
  $OciPath = $OciExe
}
else {
  $OciPath = Join-Path $RepoRoot $OciExe
}

if (-not (Test-Path $OciPath)) {
  throw "OCI CLI was not found at $OciPath. Run k8s\oci\scripts\install-oci-cli.ps1 first."
}

Write-Host "Starting OCI browser authentication. Do not share your password or OTP."
& $OciPath session authenticate --region $Region --profile-name $ProfileName
if ($LASTEXITCODE -ne 0) {
  throw "OCI authentication failed."
}

Write-Host "OCI session is ready for profile '$ProfileName'."
