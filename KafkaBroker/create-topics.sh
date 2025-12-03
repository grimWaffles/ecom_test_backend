#!/bin/bash

# ============================================
# Kafka Topic Creation Script for Ubuntu
# Works with apache/kafka:latest Docker container
# ============================================

CONTAINER="kafka_broker"       # Kafka container name
BROKER="localhost:9092"         # Kafka bootstrap server

# ---- TOPICS TO CREATE ----
# Format: "topicName:partitions"
TOPICS=(
  # Main topics
  "order-create:3"
  "order-update:3"
  "order-delete:3"

  # DLQ topics
  "order-create-dlq:1"
  "order-update-dlq:1"
  "order-delete-dlq:1"

  #Catch All DLQ
  "catch-all-dlq:1"
)
# -------------------------

echo "Starting Kafka topic creation..."

for TP in "${TOPICS[@]}"; do
  TOPIC=$(echo $TP | cut -d ":" -f1)
  PARTITIONS=$(echo $TP | cut -d ":" -f2)

  echo "Creating topic '$TOPIC' with $PARTITIONS partition(s)..."

  docker exec -i $CONTAINER /opt/kafka/bin/kafka-topics.sh \
    --create \
    --if-not-exists \
    --topic $TOPIC \
    --partitions $PARTITIONS \
    --replication-factor 1 \
    --bootstrap-server $BROKER
done

echo ""
echo "All topics created successfully!"
echo ""
echo "Current Kafka topics:"
docker exec -i $CONTAINER /opt/kafka/bin/kafka-topics.sh --list --bootstrap-server $BROKER
