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
  # 0) Gateway to Microservice-1
  "order-create:2"
  "order-update:2"
  "order-delete:2" 

  # 0.1) Gateway to MS-1 DLQ Topics
  "order-create-dlq:1"
  "order-update-dlq:1"
  "order-delete-dlq:1"

  # 1) MS-1 to MS-2
  "order-create-success:2"
  "order-update-success:2"
  "order-delete-success:2"

  # 2) MS-2 to MS-1
  "transaction-create-failed:2"
  "transaction-update-failed:2"
  "transaction-delete-failed:2"

  # 3) MS-2 to Notification-Service
  "transaction-create-success:2"
  "transaction-update-success:2"
  "transaction-delete-success:2"

  #Catch All DLQ
  "catch-all-dlq:1"
)
# -------------------------

# Deleting existing topics
echo deleting existing topics and messages

docker exec -it $CONTAINER bash -c "kafka-topics.sh --bootstrap-server localhost:9092 --list | \ xargs -I{} kafka-topics.sh --bootstrap-server $BROKER --delete --topic {}"

# -------------------------------------

# Creating the topics

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

# -------------------------------------

echo ""
echo "Current Kafka topics:"
docker exec -i $CONTAINER /opt/kafka/bin/kafka-topics.sh --list --bootstrap-server $BROKER
