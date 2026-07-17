# OCI Managed Kafka: Terraform and Deployment Runbook

This runbook documents how the Academic Registration PoC provisions OCI Streaming with Apache Kafka and how the OKE pods are configured to use it.

## What Terraform Creates

Terraform under `infrastructure/oci-managed-kafka/terraform` creates:

- OCI Managed Kafka cluster.
- Kafka cluster config.
- Vault, key, and secret for the generated SASL/SCRAM superuser password.
- IAM policy that lets the OCI Kafka service use the selected subnet and update the Vault secret.

Do not commit:

- `terraform.tfvars`
- `*.tfstate`
- `*.tfplan`
- `.terraform/`

Those files are ignored by `.gitignore`.

## Prerequisites

1. OCI CLI authenticated with a security token:

```powershell
oci session authenticate --profile-name DEFAULT --region sa-bogota-1
oci iam region list --profile DEFAULT --auth security_token
```

2. Terraform installed and available in `PATH`.

For this workstation, Terraform was installed portably at:

```text
C:\Users\julio\Documents\Codex\2026-07-16\teng\work\tools\terraform\terraform.exe
```

3. `kubectl` configured for the OKE cluster.

4. OCI IAM approval for the `rawfka` service policy.

## Provision Kafka

From the repo root:

```powershell
cd infrastructure\oci-managed-kafka\terraform
copy terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars`:

```hcl
region                  = "sa-bogota-1"
oci_auth                = "SecurityToken"
oci_config_file_profile = "DEFAULT"
compartment_id          = "<compartment-or-tenancy-ocid>"

subnet_ocids = [
  "<oke-node-or-reachable-private-subnet-ocid>"
]

display_name      = "academic-registration-kafka-poc"
cluster_type      = "DEVELOPMENT"
kafka_version     = "3.9.1"
coordination_type = "ZOOKEEPER"
broker_node_count = 1
```

Then run:

```powershell
terraform init
terraform validate
terraform plan -out kafka-poc.tfplan
terraform apply kafka-poc.tfplan
terraform output
```

Expected output includes:

```text
bootstrap_servers = "...:9092,...:9093"
sasl_scram_secret_id = "ocid1.vaultsecret..."
kafka_cluster_state = "ACTIVE"
```

Use `:9092` for SASL/SCRAM clients.

## Create the Kubernetes Secret

The OCI Kafka superuser is stored in Vault as JSON:

```json
{
  "username": "...",
  "password": "..."
}
```

Create/update the Kubernetes Secret without printing the password:

```powershell
$secretId = "<terraform-output-sasl_scram_secret_id>"
$bootstrap = "<terraform-output-sasl-bootstrap-url-9092>"
$raw = oci secrets secret-bundle get `
  --auth security_token `
  --region sa-bogota-1 `
  --secret-id $secretId `
  --stage CURRENT `
  --output json

$content = (($raw | ConvertFrom-Json).data.'secret-bundle-content'.content)
$decoded = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($content)) | ConvertFrom-Json

kubectl create secret generic oci-managed-kafka-client `
  -n academic-registration `
  --from-literal=KAFKA_BOOTSTRAP_SERVERS=$bootstrap `
  --from-literal=KAFKA_SECURITY_PROTOCOL=SASL_SSL `
  --from-literal=KAFKA_SASL_MECHANISM=SCRAM-SHA-512 `
  --from-literal=KAFKA_USERNAME=$($decoded.username) `
  --from-literal=KAFKA_PASSWORD=$($decoded.password) `
  --from-literal=Kafka__SaslUsername=$($decoded.username) `
  --from-literal=Kafka__SaslPassword=$($decoded.password) `
  --dry-run=client -o yaml | kubectl apply -f -
```

## Validate From OKE

Run the smoke test manifest:

```powershell
kubectl apply -f infrastructure\oci-managed-kafka\k8s\smoke-test-job.yaml
kubectl wait --for=condition=complete job/oci-managed-kafka-smoke-test -n kafka-poc --timeout=300s
kubectl logs job/oci-managed-kafka-smoke-test -n kafka-poc
```

For this project, the smoke test was also run in namespace `academic-registration` and succeeded:

```text
Created topic poc.oke.heartbeat.
hello-from-oke ...
Processed a total of 1 messages
```

The smoke test manifest uses `docker.io/apache/kafka:3.9.1` and absolute Kafka CLI paths because OKE nodes can reject short image names such as `apache/kafka:3.9.1`.

## Create the Application Topic

For this PoC, the application topic was created explicitly:

```text
academic-registration-events
```

Topic settings used:

```text
partitions=3
replication-factor=1
```

Replication factor is `1` because the PoC Kafka cluster has one broker. For a production cluster with three or more brokers, use a higher replication factor and revisit `min.insync.replicas`.

The temporary OKE Job used the same `oci-managed-kafka-client` Secret and ran:

```bash
/opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server "${KAFKA_BOOTSTRAP_SERVERS}" \
  --command-config /tmp/client.properties \
  --create \
  --if-not-exists \
  --topic academic-registration-events \
  --partitions 3 \
  --replication-factor 1
```

Validation output:

```text
Created topic academic-registration-events.
Topic: academic-registration-events  PartitionCount: 3  ReplicationFactor: 1
```

## Configure API and Worker

The OCI manifests configure:

```text
Kafka__BootstrapServers=<oci-bootstrap>:9092
Kafka__SecurityProtocol=SaslSsl
Kafka__SaslMechanism=ScramSha512
Kafka__SaslUsername=<from oci-managed-kafka-client>
Kafka__SaslPassword=<from oci-managed-kafka-client>
```

Apply the changed manifests:

```powershell
kubectl apply `
  -f k8s\oci\manifests\01-configmap.yaml `
  -f k8s\oci\manifests\05-api.yaml `
  -f k8s\oci\manifests\06-notifications-worker.yaml `
  -f k8s\oci\manifests\99-disable-incluster-kafka.yaml
```

The API and worker include backwards-compatible SASL support:

- If `Kafka__SecurityProtocol` is empty, they behave like before and use local/plain Kafka.
- If `Kafka__SecurityProtocol=SaslSsl`, they require `Kafka__SaslMechanism`, `Kafka__SaslUsername`, and `Kafka__SaslPassword`.

## Build and Push Updated Images

Example tag:

```powershell
$tag = "oci-kafka-sasl-20260716-1957"
.\k8s\oci\scripts\build-push-images.ps1 `
  -RegionKey sa-bogota-1 `
  -RegistryHost ocir.sa-bogota-1.oci.oraclecloud.com `
  -TenancyNamespace ax8nej0w8huf `
  -Tag $tag `
  -Platform linux/amd64 `
  -Output push
```

If you only changed API and worker, it is also fine to build/push only:

```powershell
docker buildx build --platform linux/amd64 `
  -t ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/api:$tag `
  -f backend/Dockerfile.api backend --push

docker buildx build --platform linux/amd64 `
  -t ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/notifications:$tag `
  -f backend/Dockerfile.notifications backend --push
```

Deploy:

```powershell
kubectl apply `
  -f k8s\oci\manifests\01-configmap.yaml `
  -f k8s\oci\manifests\05-api.yaml `
  -f k8s\oci\manifests\06-notifications-worker.yaml

kubectl rollout status deployment/api -n academic-registration --timeout=300s
kubectl rollout status deployment/notifications-worker -n academic-registration --timeout=300s
```

Currently deployed images:

```text
ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/api:oci-kafka-sasl-20260716-1957
ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/notifications:oci-kafka-sasl-20260716-1957
```

## Disable In-Cluster Kafka

Once API and worker point to OCI Managed Kafka, apply:

```powershell
kubectl apply -f k8s\oci\manifests\99-disable-incluster-kafka.yaml
```

This manifest:

- Scales `deployment/kafka` to `0`.
- Changes `service/kafka` to a selector with no matching pods.
- Leaves the PVC intact so rollback remains possible.

To temporarily restore the containerized Kafka service:

```powershell
kubectl apply -f k8s\oci\manifests\04-datastores.yaml
```

## Rollback

To return to in-cluster Kafka:

```powershell
kubectl patch configmap academic-registration-config `
  -n academic-registration `
  --type merge `
  --patch '{"data":{"Kafka__BootstrapServers":"kafka:29092","Kafka__SecurityProtocol":"","Kafka__SaslMechanism":""}}'

kubectl rollout restart deployment/api deployment/notifications-worker -n academic-registration
```

Keep the OCI Kafka cluster running only while the PoC is needed; it creates billable resources.
