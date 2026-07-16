param(
  [string]$PythonPath = "",
  [string]$InstallPath = "C:\o"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
if ([System.IO.Path]::IsPathRooted($InstallPath)) {
  $TargetPath = $InstallPath
}
else {
  $TargetPath = Join-Path $RepoRoot $InstallPath
}

function Resolve-Python {
  if ($PythonPath) {
    return $PythonPath
  }

  $candidates = @(
    "python",
    "py",
    "C:\Users\julio\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
  )

  foreach ($candidate in $candidates) {
    try {
      $version = & $candidate --version 2>$null
      if ($LASTEXITCODE -eq 0 -and $version) {
        return $candidate
      }
    }
    catch {
    }
  }

  throw "Python was not found. Install Python 3.10+ or pass -PythonPath."
}

$python = Resolve-Python
Write-Host "Using Python: $python"

if (-not (Test-Path $TargetPath)) {
  & $python -m venv $TargetPath
  if ($LASTEXITCODE -ne 0) {
    throw "Could not create virtual environment."
  }
}

$venvPython = Join-Path $TargetPath "Scripts\python.exe"
$ociExe = Join-Path $TargetPath "Scripts\oci.exe"

& $venvPython -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) {
  throw "Could not upgrade pip."
}

& $venvPython -m pip install --upgrade oci-cli
if ($LASTEXITCODE -ne 0) {
  throw "Could not install oci-cli."
}

& $ociExe --version
Write-Host ""
Write-Host "OCI CLI installed at:"
Write-Host "  $ociExe"
