# ============================================
# Kafka Topic Creation Script for Windows PowerShell
# Works with apache/kafka:latest Docker container
# ============================================

$container = "kafka_broker"       # Kafka container name from docker-compose
$broker    = "localhost:9092"     # Kafka bootstrap server

# ---- TOPICS TO CREATE ----
# Format: "topicName:partitions"
$topics = @(
    # Main topics
    "order-create:6",
    "order-update:6",
    "order-delete:6",

    # DLQ topics
    "order-create-dlq:1",
    "order-update-dlq:1",
    "order-delete-dlq:1"

    #Catch All DLQ
    "catch-all-dlq"
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
