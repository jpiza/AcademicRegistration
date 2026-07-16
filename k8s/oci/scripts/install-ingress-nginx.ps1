param(
  [string]$Namespace = "ingress-nginx"
)

$ErrorActionPreference = "Stop"

$ManifestUrl = "https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.15.1/deploy/static/provider/cloud/deploy.yaml"

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

Invoke-CheckedCommand { kubectl apply -f $ManifestUrl }

Invoke-CheckedCommand {
  kubectl annotate service ingress-nginx-controller `
    -n $Namespace `
    service.beta.kubernetes.io/oci-load-balancer-shape=flexible `
    service.beta.kubernetes.io/oci-load-balancer-shape-flex-min=10 `
    service.beta.kubernetes.io/oci-load-balancer-shape-flex-max=10 `
    --overwrite
}

Invoke-CheckedCommand {
  kubectl wait --namespace $Namespace `
    --for=condition=ready pod `
    --selector=app.kubernetes.io/component=controller `
    --timeout=180s
}

kubectl get service ingress-nginx-controller -n $Namespace
