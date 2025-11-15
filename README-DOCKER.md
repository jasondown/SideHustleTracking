# Running Side Hustle Tracker with Docker

## Prerequisites

- Docker Desktop installed and running on Linux
- This repository cloned locally

## Quick Start

### 1. Build and Run

From the repository root:

```bash
# Build and start the container
docker compose up -d

# View logs
docker compose logs -f

# Stop the container
docker compose down
```

## Standalone Deployment

To run the app without needing the source repository:

### Initial Setup

```bash
# In your repository, build and export the image
docker build -t sidehustle-tracker:latest .
docker save sidehustle-tracker:latest -o sidehustle-tracker-image.tar

# Create standalone directory
mkdir -p ~/SideHustleTracking/data/fx

# Copy required files
cp sidehustle-tracker-image.tar ~/SideHustleTracking/
cp data/entries.csv ~/SideHustleTracking/data/
cp data/fx/* ~/SideHustleTracking/data/fx/

# Create docker-compose.yml in ~/SideHustleTracking/
cat > ~/SideHustleTracking/docker-compose.yml << 'EOF'
services:
  sidehustle-tracker:
    image: sidehustle-tracker:latest
    container_name: sidehustle-tracker
    ports:
      - "5152:8080"
    volumes:
      - ./data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DataPaths__EntriesCsv=/app/data/entries.csv
      - DataPaths__FxSnapshotDir=/app/data/fx
      - FxApiUrl=https://api.frankfurter.app
    restart: unless-stopped
EOF

# Load the image and start
cd ~/SideHustleTracking
docker load -i sidehustle-tracker-image.tar
docker compose up -d
```

## Updating After Code Changes

```bash
# In your repository
docker build -t sidehustle-tracker:latest .
docker save sidehustle-tracker:latest -o sidehustle-tracker-image.tar

# Copy new image to standalone directory
cp sidehustle-tracker-image.tar ~/SideHustleTracking/

# In standalone directory, reload and restart
cd ~/SideHustleTracking
docker load -i sidehustle-tracker-image.tar
docker compose up -d --force-recreate
```
