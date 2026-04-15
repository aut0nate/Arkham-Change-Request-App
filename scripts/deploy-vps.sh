#!/bin/bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

ENV_FILE=".env.production"
NETWORK_NAME="edge-net"
COMPOSE_FILE="docker-compose.prod.yml"

if ! command -v docker >/dev/null 2>&1; then
    echo "Docker is required but not installed."
    exit 1
fi

if [ ! -f "$ENV_FILE" ]; then
    echo "Missing $ENV_FILE. Create it from .env.production.example before deploying."
    exit 1
fi

if ! docker network inspect "$NETWORK_NAME" >/dev/null 2>&1; then
    echo "Creating external Docker network: $NETWORK_NAME"
    docker network create "$NETWORK_NAME" >/dev/null
fi

echo "Pulling latest repository changes..."
git pull --ff-only

echo "Starting production stack..."
docker compose -f "$COMPOSE_FILE" up --build -d

echo
echo "Deployment complete."
echo "Useful follow-up commands:"
echo "  docker compose -f $COMPOSE_FILE ps"
echo "  docker compose -f $COMPOSE_FILE logs --tail=100"
echo "  curl -I http://127.0.0.1:8080/health"
