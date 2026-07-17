#!/usr/bin/env bash
set -euo pipefail

# Run from a host/pod with Kafka CLI and network access to OCI Managed Kafka.
#
# Required env:
#   KAFKA_BOOTSTRAP_SERVERS
#   KAFKA_USERNAME
#   KAFKA_PASSWORD
#
# Optional env:
#   TOPICS="orders.created payments.completed integrations.dlq"
#   PARTITIONS=3
#   REPLICATION_FACTOR=3
#   APP_PRINCIPAL="app-user"
#   CONSUMER_GROUP_PREFIX="my-service-"

: "${KAFKA_BOOTSTRAP_SERVERS:?Set KAFKA_BOOTSTRAP_SERVERS}"
: "${KAFKA_USERNAME:?Set KAFKA_USERNAME}"
: "${KAFKA_PASSWORD:?Set KAFKA_PASSWORD}"

TOPICS="${TOPICS:-poc.oke.heartbeat}"
PARTITIONS="${PARTITIONS:-3}"
REPLICATION_FACTOR="${REPLICATION_FACTOR:-3}"
APP_PRINCIPAL="${APP_PRINCIPAL:-${KAFKA_USERNAME}}"
CONSUMER_GROUP_PREFIX="${CONSUMER_GROUP_PREFIX:-poc-}"
CLIENT_CONFIG="$(mktemp)"

trap 'rm -f "${CLIENT_CONFIG}"' EXIT

cat > "${CLIENT_CONFIG}" <<EOF
security.protocol=SASL_SSL
sasl.mechanism=SCRAM-SHA-512
sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required username="${KAFKA_USERNAME}" password="${KAFKA_PASSWORD}";
EOF

for topic in ${TOPICS}; do
  kafka-topics.sh \
    --bootstrap-server "${KAFKA_BOOTSTRAP_SERVERS}" \
    --command-config "${CLIENT_CONFIG}" \
    --create \
    --if-not-exists \
    --topic "${topic}" \
    --partitions "${PARTITIONS}" \
    --replication-factor "${REPLICATION_FACTOR}"

  kafka-acls.sh \
    --bootstrap-server "${KAFKA_BOOTSTRAP_SERVERS}" \
    --command-config "${CLIENT_CONFIG}" \
    --add \
    --allow-principal "User:${APP_PRINCIPAL}" \
    --operation Read \
    --operation Write \
    --operation Describe \
    --topic "${topic}"
done

kafka-acls.sh \
  --bootstrap-server "${KAFKA_BOOTSTRAP_SERVERS}" \
  --command-config "${CLIENT_CONFIG}" \
  --add \
  --allow-principal "User:${APP_PRINCIPAL}" \
  --operation Read \
  --group "${CONSUMER_GROUP_PREFIX}" \
  --resource-pattern-type prefixed

echo "Topics and ACLs applied for principal User:${APP_PRINCIPAL}"
