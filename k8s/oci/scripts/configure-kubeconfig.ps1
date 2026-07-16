param(
  [Parameter(Mandatory = $true)]
  [string]$ClusterId,
  [Parameter(Mandatory = $true)]
  [string]$Region,
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

$KubeConfigPath = Join-Path $HOME ".kube\config"

& $OciPath ce cluster create-kubeconfig `
  --cluster-id $ClusterId `
  --file $KubeConfigPath `
  --region $Region `
  --token-version 2.0.0 `
  --kube-endpoint PUBLIC_ENDPOINT `
  --auth security_token

if ($LASTEXITCODE -ne 0) {
  throw "Could not configure kubeconfig."
}

$kubeConfig = Get-Content -LiteralPath $KubeConfigPath -Raw
$kubeConfig = $kubeConfig -replace "(?m)^(\s*)command:\s+oci\s*$", "`$1command: $OciPath"
if ($kubeConfig -notmatch "(?m)^\s*-\s+--auth\s*$") {
  $regionPattern = [regex]::Escape($Region)
  $kubeConfig = $kubeConfig -replace "(?m)^(\s*-\s+$regionPattern\s*)$", "`$1`r`n      - --auth`r`n      - security_token"
}
Set-Content -LiteralPath $KubeConfigPath -Value $kubeConfig -Encoding UTF8

kubectl get nodes
