# ============================================
# Kafka Topic Creation Script for Windows PowerShell
# Works with apache/kafka:latest Docker container
# ============================================

$container = "kafka_broker"       # Kafka container name from docker-compose
$broker    = "localhost:9092"     # Kafka bootstrap server

# ---- TOPICS TO CREATE ----
# Format: "topicName:partitions"
$topics = @(
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

Write-Host "Starting Kafka topic creation..." -ForegroundColor Cyan

foreach ($tp in $topics) {
    $parts = $tp.Split(":")
    $topic = $parts[0]
    $partitions = $parts[1]

    Write-Host "Creating topic '$topic' with $partitions partition(s)..." -ForegroundColor Yellow

    docker exec -it $container /opt/kafka/bin/kafka-topics.sh `
        --create `
        --if-not-exists `
        --topic $topic `
        --partitions $partitions `
        --replication-factor 1 `
        --bootstrap-server $broker
}

Write-Host "All topics created successfully!" -ForegroundColor Green

# Optional: list all topics
Write-Host "`nCurrent Kafka topics:" -ForegroundColor Cyan
docker exec -it $container /opt/kafka/bin/kafka-topics.sh --list --bootstrap-server $broker
