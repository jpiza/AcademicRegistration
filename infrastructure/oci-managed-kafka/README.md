# PoC: OCI Streaming with Apache Kafka

Este paquete crea una prueba de concepto para mover integraciones asincronas desde Kafka en OKE hacia **OCI Streaming with Apache Kafka**, el servicio gestionado de Kafka en OCI.

Runbook especifico del proyecto:

- `docs/terraform-and-deployment.md`

## Alcance

- Crea un cluster Kafka gestionado en OCI.
- Usa una subnet privada para que OKE acceda por red interna.
- Deja SASL/SCRAM preparado para credenciales en OCI Vault.
- Incluye ejemplos de configuracion para pods en OKE.
- Incluye smoke test para crear un topic, producir y consumir un mensaje.

## Requisitos previos

1. Un compartment donde crear el cluster.
2. Una VCN/subnet alcanzable desde el cluster de OKE.
3. Reglas de red desde los nodos/pods de OKE hacia la subnet Kafka:
   - TCP `9092` para `SASL_SCRAM`.
   - TCP `9093` solo si decides usar mTLS.
4. Terraform `>= 1.5`.
5. OCI Terraform provider `>= 7.18`.
6. Opcional pero recomendado: un secret manual en OCI Vault para el superusuario SASL/SCRAM.

Documentacion base:

- OCI Kafka managed service: https://docs.oracle.com/en-us/iaas/Content/kafka/overview.htm
- Crear cluster: https://docs.oracle.com/en-us/iaas/Content/kafka/gs.htm
- SASL/SCRAM: https://docs.oracle.com/en-us/iaas/Content/kafka/security-sasl.htm
- ACLs: https://docs.oracle.com/en-us/iaas/Content/kafka/security-acl.htm
- Terraform resource: https://docs.oracle.com/en-us/iaas/tools/terraform-provider-oci/latest/docs/r/managed_kafka_kafka_cluster.html

## Archivos

- `terraform/`: IaC para cluster, config y superusuario opcional.
- `iam-policies.txt`: politicas IAM base para el admin y el servicio `rawfka`.
- `k8s/`: ejemplos de Secret, client properties y Job de smoke test.
- `scripts/`: comandos auxiliares para inventario, topics y ACLs.

## Flujo recomendado

1. Copia `terraform/terraform.tfvars.example` a `terraform/terraform.tfvars`.
2. Completa `region`, `compartment_id`, `subnet_ocids` y, si aplica, `sasl_scram_secret_id`.
3. Aplica las politicas de `iam-policies.txt`.
4. Verifica que la subnet permita trafico desde OKE hacia `9092`.
5. Ejecuta Terraform:

```bash
cd terraform
terraform init
terraform plan
terraform apply
terraform output
```

6. Toma el output `bootstrap_servers`.
7. Crea/actualiza el Secret de Kubernetes basado en `k8s/kafka-client-secret.example.yaml`.
8. Ejecuta `k8s/smoke-test-job.yaml` desde el namespace de prueba.
9. Cambia un microservicio no critico para apuntar al nuevo `bootstrap_servers`.

## Valores exactos usados en esta PoC

Estos fueron los valores usados para ejecutar el plan de Terraform que creo el cluster `academic-registration-kafka-poc`.

> Nota: no se documenta el password del superusuario SASL/SCRAM ni el contenido del Secret. Los OCID y nombres de recursos se dejan para trazabilidad de la PoC.

Directorio de ejecucion:

```text
C:\Users\julio\Documents\Codex\2026-07-16\teng\outputs\oci-managed-kafka-poc\terraform
```

Terraform portable usado:

```text
C:\Users\julio\Documents\Codex\2026-07-16\teng\work\tools\terraform\terraform.exe
```

Provider resuelto por `terraform init`:

```text
registry.terraform.io/oracle/oci = 7.32.0
```

Contenido de `terraform.tfvars` usado:

```hcl
region                  = "sa-bogota-1"
oci_auth                = "SecurityToken"
oci_config_file_profile = "DEFAULT"
compartment_id          = "ocid1.tenancy.oc1..aaaaaaaapq67indghpkmh4kyxhihkkt42jpibg66tzceniext3wjzwitiftq"

subnet_ocids = [
  "ocid1.subnet.oc1.sa-bogota-1.aaaaaaaaje3gw7e3urx4jxudp6327c57a2qfwky62cbkvje3sdtxjck3uswa"
]

display_name      = "academic-registration-kafka-poc"
cluster_type      = "DEVELOPMENT"
kafka_version     = "3.9.1"
coordination_type = "ZOOKEEPER"

broker_node_count              = 1
broker_ocpu_count              = 1
broker_node_shape              = "VM.Standard.E5.Flex"
broker_storage_size_in_gbs     = 150
auto_create_topics_enable      = false
allow_everyone_if_no_acl_found = true

enable_sasl_scram_superuser        = true
create_sasl_scram_secret           = true
sasl_scram_secret_id               = ""
sasl_scram_secret_compartment_id   = "ocid1.tenancy.oc1..aaaaaaaapq67indghpkmh4kyxhihkkt42jpibg66tzceniext3wjzwitiftq"
sasl_scram_vault_name              = "academic-registration-kafka-poc-vault"
sasl_scram_key_name                = "academic-registration-kafka-poc-key"
sasl_scram_secret_name             = "academic-registration-kafka-poc-superuser"

freeform_tags = {
  environment = "poc"
  owner       = "platform"
  workload    = "academic-registration"
}
```

Plan files generados durante la ejecucion:

```text
rawfka-policy.tfplan
kafka-poc.tfplan
```

Outputs relevantes despues de `terraform apply`:

```text
kafka_cluster_state = "ACTIVE"
kafka_cluster_id    = "ocid1.kafkacluster.oc1.sa-bogota-1.amaaaaaath7pecqahpg333hlx2bb2eky6g43hsi5iupiw2nko2fkawuyajmq"
cluster_config_id   = "ocid1.kafkaclusterconfig.oc1.sa-bogota-1.amaaaaaath7pecqaoyagg3bsuwykjsgnlahnomcskrbt3llnxf22adaqakoa"

sasl_scram_vault_id  = "ocid1.vault.oc1.sa-bogota-1.i5vfszcmaaf6y.abrgcljrvyt5es6zmnjyckwewpt2i57bfz2bonobuejf5nyalqbaztwoxz6q"
sasl_scram_key_id    = "ocid1.key.oc1.sa-bogota-1.i5vfszcmaaf6y.abrgcljrwgbltdpbtel32q4wppn5lchgvacmk3uvr7y467pzc2rjq6uv2mjq"
sasl_scram_secret_id = "ocid1.vaultsecret.oc1.sa-bogota-1.amaaaaaath7pecqazbs3stzc5szc6iakufr7iqp6rshsrjbhvyn5jn6cqyjq"

bootstrap SASL/SCRAM = "bootstrap-clstr-ufx2qi2chn08ohq4.kafka.sa-bogota-1.oci.oraclecloud.com:9092"
bootstrap TLS/mTLS   = "bootstrap-clstr-ufx2qi2chn08ohq4.kafka.sa-bogota-1.oci.oraclecloud.com:9093"
```

## Sizing inicial sugerido

Para PoC cercana a produccion:

- `cluster_type = "DEVELOPMENT"`
- `broker_node_count = 3`
- `broker_ocpu_count = 1`
- `broker_storage_size_in_gbs = 150`
- `kafka_version = "3.9.1"`
- `coordination_type = "KRAFT"`

Si quieres minimizar costo para una prueba de conectividad, usa `broker_node_count = 1`; el Terraform ajusta replication factor e ISR a `1`.

## Seguridad de la PoC

El Terraform deja `allow.everyone.if.no.acl.found = true` por defecto para facilitar el primer smoke test. Antes de probar flujos reales con datos sensibles, cambia:

```hcl
allow_everyone_if_no_acl_found = false
```

Luego crea ACLs explicitas con `scripts/create-topics-and-acls.sh`.

## Criterios de exito

- Los pods en OKE resuelven y alcanzan el bootstrap server.
- Un Job en OKE crea un topic, produce y consume un mensaje.
- Un microservicio de bajo riesgo consume y produce usando el cluster gestionado.
- Consumer lag estable durante una prueba de carga corta.
- ACLs bloquean accesos no autorizados cuando `allow.everyone.if.no.acl.found=false`.

## Rollback

Mantén la configuracion de Kafka actual en OKE como Secret/ConfigMap separado. Para volver:

1. Reapunta el Secret/ConfigMap del microservicio al Kafka actual.
2. Reinicia el deployment.
3. Valida consumo desde el consumer group original.

No destruyas el cluster de PoC hasta confirmar que no quedan producers ni consumers apuntando al bootstrap gestionado.
