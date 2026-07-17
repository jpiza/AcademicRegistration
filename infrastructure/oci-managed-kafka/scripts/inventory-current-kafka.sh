#!/usr/bin/env bash
set -euo pipefail

# Run this from a shell that can reach the current Kafka running in OKE.
# Example:
#   KAFKA_BOOTSTRAP_SERVERS="kafka.kafka.svc.cluster.local:9092" ./inventory-current-kafka.sh

: "${KAFKA_BOOTSTRAP_SERVERS:?Set KAFKA_BOOTSTRAP_SERVERS}"

OUT_DIR="${OUT_DIR:-./kafka-inventory-$(date +%Y%m%d-%H%M%S)}"
mkdir -p "${OUT_DIR}"

kafka-topics.sh --bootstrap-server "${KAFKA_BOOTSTRAP_SERVERS}" --list \
  | sort > "${OUT_DIR}/topics.txt"

while read -r topic; do
  [ -z "${topic}" ] && continue
  kafka-topics.sh --bootstrap-server "${KAFKA_BOOTSTRAP_SERVERS}" --describe --topic "${topic}" \
    > "${OUT_DIR}/topic-${topic//\//_}.txt"
done < "${OUT_DIR}/topics.txt"

kafka-consumer-groups.sh --bootstrap-server "${KAFKA_BOOTSTRAP_SERVERS}" --list \
  | sort > "${OUT_DIR}/consumer-groups.txt"

while read -r group; do
  [ -z "${group}" ] && continue
  kafka-consumer-groups.sh --bootstrap-server "${KAFKA_BOOTSTRAP_SERVERS}" --describe --group "${group}" \
    > "${OUT_DIR}/group-${group//\//_}.txt" || true
done < "${OUT_DIR}/consumer-groups.txt"

echo "Inventory written to ${OUT_DIR}"
