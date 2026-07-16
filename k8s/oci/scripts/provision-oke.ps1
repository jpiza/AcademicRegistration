param(
  [string]$Region = "sa-bogota-1",
  [string]$CompartmentId = "",
  [string]$OciExe = "C:\o\Scripts\oci.exe",
  [string]$ClusterName = "academic-registration-oke",
  [string]$VcnName = "academic-registration-vcn",
  [string]$KubernetesVersion = "v1.36.1",
  [string]$CidrBlock = "10.42.0.0/16",
  [string]$SubnetCidrBlock = "10.42.10.0/24",
  [string]$NodeSubnetCidrBlock = "10.42.20.0/24",
  [string]$NodeShape = "VM.Standard.A1.Flex",
  [decimal]$NodeOcpus = 4,
  [decimal]$NodeMemoryGb = 24,
  [int]$NodeCount = 1,
  [int]$NodeBootVolumeGb = 50
)

$ErrorActionPreference = "Stop"

$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "academic-registration-oci"
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null

if (-not (Test-Path $OciExe)) {
  throw "OCI CLI was not found at $OciExe. Run k8s\oci\scripts\install-oci-cli.ps1 first."
}

function Get-TenancyId {
  $configPath = Join-Path $env:USERPROFILE ".oci\config"
  if (-not (Test-Path $configPath)) {
    throw "OCI config was not found. Run authenticate-oci.ps1 first."
  }

  $line = Select-String -Path $configPath -Pattern "^tenancy=" | Select-Object -First 1
  if (-not $line) {
    throw "Could not find tenancy in OCI config."
  }

  return $line.Line.Split("=")[1].Trim()
}

function Invoke-OciJson {
  param([string[]]$Arguments)

  $previousErrorActionPreference = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    $output = & $OciExe @Arguments --auth security_token --region $Region 2>&1
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  $text = ($output | ForEach-Object { "$_" } | Out-String).Trim()
  if ($exitCode -ne 0) {
    throw "OCI command failed: $text"
  }

  if (-not $text) {
    return $null
  }

  $jsonStart = $text.IndexOf("{")
  if ($jsonStart -gt 0) {
    $text = $text.Substring($jsonStart)
  }

  return $text | ConvertFrom-Json
}

function ConvertTo-JsonArgument {
  param(
    [object]$Value
  )

  return (ConvertTo-Json -InputObject $Value -Depth 20 -Compress)
}

function Write-JsonFileUri {
  param(
    [string]$Name,
    [object]$Value
  )

  $path = Join-Path $TempDir $Name
  ConvertTo-Json -InputObject $Value -Depth 20 -Compress | Set-Content -LiteralPath $path -Encoding ASCII
  $resolved = (Resolve-Path -LiteralPath $path).Path
  return "file://$resolved"
}

function Get-FirstByDisplayName {
  param(
    [string[]]$ListArguments,
    [string]$DisplayName
  )

  $result = Invoke-OciJson $ListArguments
  return @($result.data | Where-Object { $_.'display-name' -eq $DisplayName }) | Select-Object -First 1
}

function Get-ClusterByName {
  param(
    [string]$Name,
    [string]$CompartmentId
  )

  $result = Invoke-OciJson @("ce", "cluster", "list", "--compartment-id", $CompartmentId)
  return @($result.data | Where-Object { $_.name -eq $Name }) | Select-Object -First 1
}

function Wait-ClusterExists {
  param(
    [string]$Name,
    [string]$CompartmentId
  )

  for ($i = 0; $i -lt 40; $i++) {
    $cluster = Get-ClusterByName -Name $Name -CompartmentId $CompartmentId
    if ($cluster -and $cluster.id) {
      return $cluster
    }

    Start-Sleep -Seconds 15
  }

  throw "Timed out waiting for cluster '$Name' to appear."
}

function Wait-ClusterActive {
  param([string]$ClusterId)

  Write-Host "Waiting for cluster to become ACTIVE..."
  for ($i = 0; $i -lt 80; $i++) {
    $cluster = Invoke-OciJson @("ce", "cluster", "get", "--cluster-id", $ClusterId)
    $state = $cluster.data.'lifecycle-state'
    Write-Host "  cluster state: $state"

    if ($state -eq "ACTIVE") {
      return
    }
    if ($state -in @("FAILED", "DELETED")) {
      throw "Cluster reached terminal state: $state"
    }

    Start-Sleep -Seconds 30
  }

  throw "Timed out waiting for cluster to become ACTIVE."
}

function Wait-NodePoolActive {
  param([string]$NodePoolId)

  Write-Host "Waiting for node pool to become ACTIVE..."
  for ($i = 0; $i -lt 80; $i++) {
    $nodePool = Invoke-OciJson @("ce", "node-pool", "get", "--node-pool-id", $NodePoolId)
    $state = $nodePool.data.'lifecycle-state'
    Write-Host "  node pool state: $state"

    if ($state -eq "ACTIVE") {
      return
    }
    if ($state -in @("FAILED", "DELETED")) {
      throw "Node pool reached terminal state: $state"
    }

    Start-Sleep -Seconds 30
  }

  throw "Timed out waiting for node pool to become ACTIVE."
}

function Get-AvailabilityDomainName {
  param([string]$CompartmentId)

  $ads = Invoke-OciJson @("iam", "availability-domain", "list", "--compartment-id", $CompartmentId)
  $ad = @($ads.data) | Select-Object -First 1
  if (-not $ad -or -not $ad.name) {
    throw "No availability domain was found in compartment $CompartmentId."
  }

  return $ad.name
}

function Get-NodeImageId {
  param([string]$KubernetesVersion)

  $options = Invoke-OciJson @("ce", "node-pool-options", "get", "--node-pool-option-id", "all")
  $source = @($options.data.sources | Where-Object {
      $_.'source-type' -eq "IMAGE" -and
      $_.'source-name' -like "*aarch64*" -and
      $_.'source-name' -like "*OKE-$($KubernetesVersion.TrimStart('v'))-*"
    }) | Select-Object -First 1

  if (-not $source -or -not $source.'image-id') {
    throw "No aarch64 OKE image was found for Kubernetes $KubernetesVersion."
  }

  Write-Host "Using node image: $($source.'source-name')"
  return $source.'image-id'
}

if (-not $CompartmentId) {
  $CompartmentId = Get-TenancyId
}

Write-Host "Region: $Region"
Write-Host "Compartment: $CompartmentId"
Write-Host "Cluster: $ClusterName"

$vcn = Get-FirstByDisplayName `
  -DisplayName $VcnName `
  -ListArguments @("network", "vcn", "list", "--compartment-id", $CompartmentId, "--lifecycle-state", "AVAILABLE")

if (-not $vcn) {
  Write-Host "Creating VCN..."
  $vcn = (Invoke-OciJson @(
      "network", "vcn", "create",
      "--compartment-id", $CompartmentId,
      "--display-name", $VcnName,
      "--dns-label", "acadreg",
      "--cidr-block", $CidrBlock,
      "--wait-for-state", "AVAILABLE"
    )).data
}
else {
  Write-Host "Using existing VCN: $($vcn.id)"
}

$igwName = "$ClusterName-igw"
$igw = Get-FirstByDisplayName `
  -DisplayName $igwName `
  -ListArguments @("network", "internet-gateway", "list", "--compartment-id", $CompartmentId, "--vcn-id", $vcn.id, "--lifecycle-state", "AVAILABLE")

if (-not $igw) {
  Write-Host "Creating internet gateway..."
  $igw = (Invoke-OciJson @(
      "network", "internet-gateway", "create",
      "--compartment-id", $CompartmentId,
      "--vcn-id", $vcn.id,
      "--display-name", $igwName,
      "--is-enabled", "true",
      "--wait-for-state", "AVAILABLE"
    )).data
}
else {
  Write-Host "Using existing internet gateway: $($igw.id)"
}

$routeTableName = "$ClusterName-public-rt"
$routeTable = Get-FirstByDisplayName `
  -DisplayName $routeTableName `
  -ListArguments @("network", "route-table", "list", "--compartment-id", $CompartmentId, "--vcn-id", $vcn.id, "--lifecycle-state", "AVAILABLE")

if (-not $routeTable) {
  Write-Host "Creating route table..."
  $routeRulesFile = Write-JsonFileUri "route-rules.json" @(
    @{
      destination     = "0.0.0.0/0"
      destinationType = "CIDR_BLOCK"
      networkEntityId = $igw.id
      description     = "Internet access"
    }
  )

  $routeTable = (Invoke-OciJson @(
      "network", "route-table", "create",
      "--compartment-id", $CompartmentId,
      "--vcn-id", $vcn.id,
      "--display-name", $routeTableName,
      "--route-rules", $routeRulesFile,
      "--wait-for-state", "AVAILABLE"
    )).data
}
else {
  Write-Host "Using existing route table: $($routeTable.id)"
}

$securityListName = "$ClusterName-public-sl"
$securityList = Get-FirstByDisplayName `
  -DisplayName $securityListName `
  -ListArguments @("network", "security-list", "list", "--compartment-id", $CompartmentId, "--vcn-id", $vcn.id, "--lifecycle-state", "AVAILABLE")

if (-not $securityList) {
  Write-Host "Creating security list..."
  $egressFile = Write-JsonFileUri "egress-rules.json" @(
    @{
      destination = "0.0.0.0/0"
      protocol    = "all"
      description = "Allow all egress"
    }
  )

  $ingressRules = @(
    @{
      source      = $CidrBlock
      protocol    = "all"
      description = "Allow VCN internal traffic"
    },
    @{
      source      = "0.0.0.0/0"
      protocol    = "6"
      description = "Allow HTTP"
      tcpOptions  = @{ destinationPortRange = @{ min = 80; max = 80 } }
    },
    @{
      source      = "0.0.0.0/0"
      protocol    = "6"
      description = "Allow HTTPS"
      tcpOptions  = @{ destinationPortRange = @{ min = 443; max = 443 } }
    },
    @{
      source      = "0.0.0.0/0"
      protocol    = "6"
      description = "Allow Kubernetes API endpoint"
      tcpOptions  = @{ destinationPortRange = @{ min = 6443; max = 6443 } }
    },
    @{
      source      = "0.0.0.0/0"
      protocol    = "1"
      description = "Allow path MTU discovery"
      icmpOptions = @{ type = 3; code = 4 }
    }
  )

  $ingressFile = Write-JsonFileUri "ingress-rules.json" $ingressRules

  $securityList = (Invoke-OciJson @(
      "network", "security-list", "create",
      "--compartment-id", $CompartmentId,
      "--vcn-id", $vcn.id,
      "--display-name", $securityListName,
      "--egress-security-rules", $egressFile,
      "--ingress-security-rules", $ingressFile,
      "--wait-for-state", "AVAILABLE"
    )).data
}
else {
  Write-Host "Using existing security list: $($securityList.id)"
}

$subnetName = "$ClusterName-public-subnet"
$subnet = Get-FirstByDisplayName `
  -DisplayName $subnetName `
  -ListArguments @("network", "subnet", "list", "--compartment-id", $CompartmentId, "--vcn-id", $vcn.id, "--lifecycle-state", "AVAILABLE")

if (-not $subnet) {
  Write-Host "Creating public subnet..."
  $securityListIdsFile = Write-JsonFileUri "security-list-ids.json" @($securityList.id)

  $subnet = (Invoke-OciJson @(
      "network", "subnet", "create",
      "--compartment-id", $CompartmentId,
      "--vcn-id", $vcn.id,
      "--display-name", $subnetName,
      "--dns-label", "oke",
      "--cidr-block", $SubnetCidrBlock,
      "--route-table-id", $routeTable.id,
      "--security-list-ids", $securityListIdsFile,
      "--prohibit-public-ip-on-vnic", "false",
      "--wait-for-state", "AVAILABLE"
    )).data
}
else {
  Write-Host "Using existing subnet: $($subnet.id)"
}

$nodeSubnetName = "$ClusterName-node-subnet"
$nodeSubnet = Get-FirstByDisplayName `
  -DisplayName $nodeSubnetName `
  -ListArguments @("network", "subnet", "list", "--compartment-id", $CompartmentId, "--vcn-id", $vcn.id, "--lifecycle-state", "AVAILABLE")

if (-not $nodeSubnet) {
  Write-Host "Creating node subnet..."
  $securityListIdsFile = Write-JsonFileUri "node-security-list-ids.json" @($securityList.id)

  $nodeSubnet = (Invoke-OciJson @(
      "network", "subnet", "create",
      "--compartment-id", $CompartmentId,
      "--vcn-id", $vcn.id,
      "--display-name", $nodeSubnetName,
      "--dns-label", "nodes",
      "--cidr-block", $NodeSubnetCidrBlock,
      "--route-table-id", $routeTable.id,
      "--security-list-ids", $securityListIdsFile,
      "--prohibit-public-ip-on-vnic", "false",
      "--wait-for-state", "AVAILABLE"
    )).data
}
else {
  Write-Host "Using existing node subnet: $($nodeSubnet.id)"
}

$cluster = Get-ClusterByName -Name $ClusterName -CompartmentId $CompartmentId

if (-not $cluster) {
  Write-Host "Creating OKE Basic cluster..."
  $podNetworkFile = Write-JsonFileUri "cluster-pod-network-options.json" @(
    @{ cniType = "FLANNEL_OVERLAY" }
  )
  $serviceLbSubnetsFile = Write-JsonFileUri "service-lb-subnets.json" @($subnet.id)

  $cluster = (Invoke-OciJson @(
      "ce", "cluster", "create",
      "--compartment-id", $CompartmentId,
      "--name", $ClusterName,
      "--kubernetes-version", $KubernetesVersion,
      "--vcn-id", $vcn.id,
      "--endpoint-subnet-id", $subnet.id,
      "--endpoint-public-ip-enabled", "true",
      "--service-lb-subnet-ids", $serviceLbSubnetsFile,
      "--cluster-pod-network-options", $podNetworkFile,
      "--type", "BASIC_CLUSTER"
    )) | Out-Null

  $cluster = Wait-ClusterExists -Name $ClusterName -CompartmentId $CompartmentId
}
else {
  Write-Host "Using existing cluster: $($cluster.id)"
}

Wait-ClusterActive $cluster.id

$nodePoolName = "acadreg-a1-pool"
$nodePools = Invoke-OciJson @("ce", "node-pool", "list", "--compartment-id", $CompartmentId, "--cluster-id", $cluster.id)
$nodePool = @($nodePools.data | Where-Object { $_.name -eq $nodePoolName }) | Select-Object -First 1

if (-not $nodePool) {
  Write-Host "Creating A1 node pool..."
  $availabilityDomain = Get-AvailabilityDomainName -CompartmentId $CompartmentId
  $placementConfigsFile = Write-JsonFileUri "placement-configs.json" @(
    @{
      availabilityDomain = $availabilityDomain
      subnetId            = $nodeSubnet.id
    }
  )
  $shapeConfigFile = Write-JsonFileUri "node-shape-config.json" @{
    ocpus       = $NodeOcpus
    memoryInGBs = $NodeMemoryGb
  }
  $nodeImageId = Get-NodeImageId -KubernetesVersion $KubernetesVersion
  $nodeSourceDetailsFile = Write-JsonFileUri "node-source-details.json" @{
    sourceType           = "IMAGE"
    imageId              = $nodeImageId
    bootVolumeSizeInGBs  = $NodeBootVolumeGb
  }

  $nodePool = (Invoke-OciJson @(
      "ce", "node-pool", "create",
      "--compartment-id", $CompartmentId,
      "--cluster-id", $cluster.id,
      "--name", $nodePoolName,
      "--kubernetes-version", $KubernetesVersion,
      "--node-shape", $NodeShape,
      "--node-shape-config", $shapeConfigFile,
      "--node-source-details", $nodeSourceDetailsFile,
      "--placement-configs", $placementConfigsFile,
      "--size", "$NodeCount"
    )) | Out-Null

  for ($i = 0; $i -lt 40; $i++) {
    $nodePools = Invoke-OciJson @("ce", "node-pool", "list", "--compartment-id", $CompartmentId, "--cluster-id", $cluster.id)
    $nodePool = @($nodePools.data | Where-Object { $_.name -eq $nodePoolName }) | Select-Object -First 1
    if ($nodePool -and $nodePool.id) {
      break
    }

    Start-Sleep -Seconds 15
  }

  if (-not $nodePool -or -not $nodePool.id) {
    throw "Timed out waiting for node pool '$nodePoolName' to appear."
  }
}
else {
  Write-Host "Using existing node pool: $($nodePool.id)"
}

Wait-NodePoolActive $nodePool.id

Write-Host "Configuring kubeconfig..."
& $OciExe ce cluster create-kubeconfig `
  --cluster-id $cluster.id `
  --file "$HOME\.kube\config" `
  --region $Region `
  --token-version 2.0.0 `
  --kube-endpoint PUBLIC_ENDPOINT `
  --auth security_token

if ($LASTEXITCODE -ne 0) {
  throw "Could not configure kubeconfig."
}

kubectl get nodes

Write-Host ""
Write-Host "OKE is ready."
Write-Host "Cluster ID: $($cluster.id)"
Write-Host "Service subnet ID: $($subnet.id)"
Write-Host "Node subnet ID:    $($nodeSubnet.id)"
