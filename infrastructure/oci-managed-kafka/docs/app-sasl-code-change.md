# Cambio implementado en la app para OCI Kafka SASL/SCRAM

El cluster OCI Kafka quedo activo y el smoke test desde OKE funciono con `SASL_SSL` + `SCRAM-SHA-512`.

Se agrego soporte SASL/SCRAM al productor del API y al consumidor del worker. La configuracion sigue siendo compatible con el Kafka local anterior: si `Kafka__SecurityProtocol` esta vacio, la app no configura SASL.

## Archivos modificados

- `backend/src/AcademicRegistration.Infrastructure/Messaging/KafkaSettings.cs`
- `backend/src/AcademicRegistration.Infrastructure/DependencyInjection.cs`
- `backend/src/AcademicRegistration.Notifications.Worker/Configuration/KafkaConsumerSettings.cs`
- `backend/src/AcademicRegistration.Notifications.Worker/Program.cs`
- `k8s/oci/manifests/01-configmap.yaml`
- `k8s/oci/manifests/05-api.yaml`
- `k8s/oci/manifests/06-notifications-worker.yaml`

## Propiedades agregadas

`KafkaSettings` y `KafkaConsumerSettings` ahora aceptan:

```csharp
public string? SecurityProtocol { get; set; }
public string? SaslMechanism { get; set; }
public string? SaslUsername { get; set; }
public string? SaslPassword { get; set; }
```

## Configuracion aplicada

Los productores y consumidores parsean los valores de configuracion hacia los enums de `Confluent.Kafka`, aceptando formatos como `SaslSsl`, `SASL_SSL`, `ScramSha512` y `SCRAM-SHA-512`.

## Variables Kubernetes listas

Cuando la nueva imagen soporte esas propiedades, usar:

```text
Kafka__BootstrapServers=bootstrap-clstr-ufx2qi2chn08ohq4.kafka.sa-bogota-1.oci.oraclecloud.com:9092
Kafka__SecurityProtocol=SaslSsl
Kafka__SaslMechanism=ScramSha512
Kafka__SaslUsername=<desde Secret oci-managed-kafka-client>
Kafka__SaslPassword=<desde Secret oci-managed-kafka-client>
```

El Secret `oci-managed-kafka-client` existe en el namespace `academic-registration` y contiene las llaves requeridas por la app.

## Imagenes desplegadas

- API: `ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/api:oci-kafka-sasl-20260716-1957`
- Worker: `ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/notifications:oci-kafka-sasl-20260716-1957`

Validacion realizada:

```powershell
dotnet build backend\AcademicRegistration.slnx --configuration Release
kubectl rollout status deployment/api -n academic-registration --timeout=300s
kubectl rollout status deployment/notifications-worker -n academic-registration --timeout=300s
```
