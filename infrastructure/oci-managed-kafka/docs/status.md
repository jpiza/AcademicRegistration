# Estado de la PoC OCI Kafka

Fecha: 2026-07-16

## Recursos OCI creados

- Kafka cluster: `academic-registration-kafka-poc`
- Estado: `ACTIVE`
- OCID: `ocid1.kafkacluster.oc1.sa-bogota-1.amaaaaaath7pecqahpg333hlx2bb2eky6g43hsi5iupiw2nko2fkawuyajmq`
- Kafka version: `3.9.1`
- Coordination: `ZOOKEEPER`
- Cluster type: `DEVELOPMENT`
- Broker shape: `VM.Standard.E5.Flex`
- Broker count: `1`
- Storage: `150 GB`
- SASL endpoint: `bootstrap-clstr-ufx2qi2chn08ohq4.kafka.sa-bogota-1.oci.oraclecloud.com:9092`
- TLS endpoint: `bootstrap-clstr-ufx2qi2chn08ohq4.kafka.sa-bogota-1.oci.oraclecloud.com:9093`

## Recursos de seguridad

- Vault: `academic-registration-kafka-poc-vault`
- Vault OCID: `ocid1.vault.oc1.sa-bogota-1.i5vfszcmaaf6y.abrgcljrvyt5es6zmnjyckwewpt2i57bfz2bonobuejf5nyalqbaztwoxz6q`
- Key OCID: `ocid1.key.oc1.sa-bogota-1.i5vfszcmaaf6y.abrgcljrwgbltdpbtel32q4wppn5lchgvacmk3uvr7y467pzc2rjq6uv2mjq`
- Secret OCID: `ocid1.vaultsecret.oc1.sa-bogota-1.amaaaaaath7pecqazbs3stzc5szc6iakufr7iqp6rshsrjbhvyn5jn6cqyjq`
- Kubernetes Secret: `oci-managed-kafka-client` en namespace `academic-registration`

## Validacion realizada

Smoke test desde OKE:

- Job: `oci-managed-kafka-smoke-1626`
- Namespace: `academic-registration`
- Resultado: `Completed`
- Topic creado: `poc.oke.heartbeat`
- Mensaje producido y consumido correctamente desde OCI Kafka.

## Estado de la app

Se implemento soporte SASL/SCRAM en `api` y `notifications-worker` y se desplegaron imagenes versionadas:

- API: `ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/api:oci-kafka-sasl-20260716-1957`
- Worker: `ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/notifications:oci-kafka-sasl-20260716-1957`

Configuracion activa:

- `Kafka__BootstrapServers=bootstrap-clstr-ufx2qi2chn08ohq4.kafka.sa-bogota-1.oci.oraclecloud.com:9092`
- `Kafka__SecurityProtocol=SaslSsl`
- `Kafka__SaslMechanism=ScramSha512`
- `Kafka__SaslUsername` y `Kafka__SaslPassword` desde el Secret `oci-managed-kafka-client`

Topic de aplicacion creado:

- `academic-registration-events`
- Particiones: `3`
- Replication factor: `1` porque la PoC usa un solo broker.

Validacion final:

- `api` quedo `1/1 Running`, sin restarts.
- `notifications-worker` quedo `1/1 Running`, sin restarts.
- El worker registro: `Consumidor de notificaciones suscrito al topico academic-registration-events`.
- No se observaron errores recientes de `brokers are down`, `Disconnected`, `Exception` o autenticacion SASL.
- `deployment/kafka` in-cluster quedo desactivado con `0` replicas usando `k8s/oci/manifests/99-disable-incluster-kafka.yaml`.
- `service/kafka` conserva el nombre para rollback, pero no tiene endpoints porque su selector apunta a `kafka-disabled`.

Ver tambien:

- `app-sasl-code-change.md`
