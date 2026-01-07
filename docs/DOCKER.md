# Docker Guide for TLDRkseid

A technical and educational guide to understanding Docker and how it's used with this Discord bot.

---

## Table of Contents

1. [What is Docker?](#what-is-docker)
2. [Why Use Docker for a Discord Bot?](#why-use-docker-for-a-discord-bot)
3. [Understanding the Dockerfile](#understanding-the-dockerfile)
4. [Building and Running Locally](#building-and-running-locally)
5. [Docker Compose](#docker-compose)
6. [Environment Variables](#environment-variables)
7. [Data Persistence](#data-persistence)
8. [Deployment with Docker](#deployment-with-docker)
9. [Common Commands](#common-commands)
10. [Troubleshooting](#troubleshooting)

---

## What is Docker?

Docker is a platform that packages applications into **containers** - lightweight, standalone units that include everything needed to run the software:

- Application code
- Runtime (like .NET 8.0)
- System libraries
- Dependencies

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Image** | A read-only template/blueprint for creating containers. Like a class in OOP. |
| **Container** | A running instance of an image. Like an object instantiated from a class. |
| **Dockerfile** | A text file with instructions to build an image. Like a recipe. |
| **Registry** | A storage for images (Docker Hub, GitHub Container Registry). Like npm for Docker. |
| **Volume** | Persistent storage that survives container restarts. |

### The Container vs VM Difference

```
Traditional VM:                    Docker Container:
┌─────────────────────┐           ┌─────────────────────┐
│      Your App       │           │      Your App       │
├─────────────────────┤           ├─────────────────────┤
│   Guest OS (Linux)  │           │  Container Runtime  │
├─────────────────────┤           ├─────────────────────┤
│     Hypervisor      │           │     Host OS         │
├─────────────────────┤           ├─────────────────────┤
│      Host OS        │           │     Hardware        │
├─────────────────────┤           └─────────────────────┘
│     Hardware        │
└─────────────────────┘

VMs: Heavy, slow to start, full OS    Containers: Light, instant start, shared OS
```

Containers share the host OS kernel, making them much lighter than VMs.

---

## Why Use Docker for a Discord Bot?

### Benefits

1. **Consistency** - "Works on my machine" becomes "works everywhere"
2. **Isolation** - Bot runs in its own environment, won't conflict with other software
3. **Easy Deployment** - Same container runs locally and in production
4. **Scalability** - Easy to run multiple instances if needed
5. **Reproducibility** - Anyone can build the exact same environment

### For TLDRkseid Specifically

- **No .NET installation required** - Docker image includes .NET 8.0 runtime
- **Platform agnostic** - Runs on Windows, Mac, Linux servers
- **Cloud-ready** - Railway, Fly.io, Render all support Docker
- **Easy updates** - Rebuild image, redeploy container

---

## Understanding the Dockerfile

Let's break down the TLDRkseid Dockerfile line by line:

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY . ./
RUN dotnet publish DiscordPA.csproj -c Release -o out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "DiscordPA.dll"]
```

### Line-by-Line Explanation

#### Stage 1: Build Stage

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
```
- `FROM` - Start from a base image (like inheriting from a class)
- `mcr.microsoft.com/dotnet/sdk:8.0` - Microsoft's official .NET 8 SDK image
- `AS build` - Name this stage "build" (for multi-stage builds)
- **Why SDK?** - We need the full SDK (compiler, tools) to build the project

```dockerfile
WORKDIR /app
```
- `WORKDIR` - Set the working directory inside the container
- Like `cd /app` but creates it if it doesn't exist
- All subsequent commands run from this directory

```dockerfile
COPY . ./
```
- `COPY` - Copy files from host machine into the container
- `.` (first) - Source: everything in current directory on host
- `./` (second) - Destination: current directory in container (`/app`)
- This copies your entire project into the container

```dockerfile
RUN dotnet publish DiscordPA.csproj -c Release -o out
```
- `RUN` - Execute a command during image build
- `dotnet publish` - Compile and package the application
- `-c Release` - Use Release configuration (optimized)
- `-o out` - Output to the `out` directory

#### Stage 2: Runtime Stage

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
```
- Start fresh from a **runtime-only** image
- **Why not SDK?** - Runtime image is much smaller (~200MB vs ~900MB)
- We don't need the compiler anymore, just the runtime

```dockerfile
WORKDIR /app
```
- Set working directory in the new stage

```dockerfile
COPY --from=build /app/out .
```
- `--from=build` - Copy from the previous "build" stage
- `/app/out` - The compiled output directory
- `.` - Copy to current directory (`/app`)
- **This is the magic** - We only copy the compiled output, not source code or SDK

```dockerfile
ENTRYPOINT ["dotnet", "DiscordPA.dll"]
```
- `ENTRYPOINT` - The command to run when the container starts
- Runs: `dotnet DiscordPA.dll`
- Container will stay alive as long as this process runs

### Multi-Stage Build Benefits

```
┌─────────────────────────────────────────────────────┐
│ Stage 1: Build (~900MB)                             │
│ ┌─────────────────────────────────────────────────┐ │
│ │ .NET SDK + Source Code + Dependencies           │ │
│ │                    ↓                            │ │
│ │            dotnet publish                       │ │
│ │                    ↓                            │ │
│ │         Compiled Output (out/)                  │ │
│ └─────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
                        │
                        │ COPY --from=build
                        ↓
┌─────────────────────────────────────────────────────┐
│ Stage 2: Runtime (~200MB)                           │
│ ┌─────────────────────────────────────────────────┐ │
│ │ .NET Runtime + Compiled Output ONLY             │ │
│ └─────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
                        │
                        ↓
              Final Image: ~200MB
              (No source code, no SDK)
```

---

## Building and Running Locally

### Prerequisites

1. **Install Docker Desktop**
   - Windows/Mac: [Docker Desktop](https://www.docker.com/products/docker-desktop/)
   - Linux: `sudo apt install docker.io` (or equivalent)

2. **Verify installation**
   ```bash
   docker --version
   # Docker version 24.x.x
   ```

### Build the Image

```bash
# Navigate to project directory
cd Discord-TLDRkseid

# Build the image
docker build -t tldrkseid .
```

- `-t tldrkseid` - Tag (name) the image as "tldrkseid"
- `.` - Use current directory (where Dockerfile is)

**What happens:**
1. Docker reads the Dockerfile
2. Downloads base images (if not cached)
3. Executes each instruction
4. Creates a new image

### Run the Container

```bash
# Run with environment variables
docker run -d \
  --name tldrkseid-bot \
  -e DISCORD_BOT_TOKEN=your-token-here \
  -e OPENAI_API_KEY=your-key-here \
  tldrkseid
```

- `-d` - Detached mode (run in background)
- `--name tldrkseid-bot` - Name the container
- `-e` - Set environment variables
- `tldrkseid` - Image name to run

### Using an Environment File

```bash
# Create .env file with your secrets (never commit this!)
# Then run:
docker run -d \
  --name tldrkseid-bot \
  --env-file .env \
  tldrkseid
```

---

## Docker Compose

Docker Compose simplifies running containers with a YAML configuration file.

### Create docker-compose.yml

```yaml
version: '3.8'

services:
  bot:
    build: .
    container_name: tldrkseid-bot
    restart: unless-stopped
    env_file:
      - .env
    volumes:
      - ./data:/app/data
```

### Explanation

| Key | Description |
|-----|-------------|
| `version` | Compose file format version |
| `services` | Define containers to run |
| `bot` | Service name (arbitrary) |
| `build: .` | Build from Dockerfile in current directory |
| `container_name` | Name for the container |
| `restart: unless-stopped` | Auto-restart unless manually stopped |
| `env_file` | Load environment variables from file |
| `volumes` | Mount host directory into container |

### Docker Compose Commands

```bash
# Build and start
docker compose up -d

# View logs
docker compose logs -f

# Stop
docker compose down

# Rebuild after code changes
docker compose up -d --build
```

---

## Environment Variables

### How They Work in Docker

Environment variables are passed to the container at runtime, not baked into the image. This is important for security - your tokens never get stored in the image.

### Methods to Pass Environment Variables

#### 1. Command Line (-e flag)
```bash
docker run -e DISCORD_BOT_TOKEN=xxx -e OPENAI_API_KEY=yyy tldrkseid
```

#### 2. Environment File (--env-file)
```bash
docker run --env-file .env tldrkseid
```

#### 3. Docker Compose (env_file)
```yaml
services:
  bot:
    env_file:
      - .env
```

#### 4. Docker Compose (environment)
```yaml
services:
  bot:
    environment:
      - DISCORD_BOT_TOKEN=${DISCORD_BOT_TOKEN}
      - OPENAI_API_KEY=${OPENAI_API_KEY}
```

### Best Practice

**Never** put secrets in:
- Dockerfile
- Docker image
- Git repository

**Always** use:
- Environment files (`.env`) with `.gitignore`
- Secret management (Docker secrets, cloud provider secrets)

---

## Data Persistence

### The Problem

Containers are **ephemeral** - when they stop, all data inside is lost.

TLDRkseid stores data in:
- `tldr.sqlite` - Database (admin roles, settings, logs)
- `total_cost.json` - API cost tracking

### The Solution: Volumes

Volumes mount a host directory into the container, persisting data outside the container.

```bash
# Run with volume
docker run -d \
  --name tldrkseid-bot \
  --env-file .env \
  -v $(pwd)/data:/app \
  tldrkseid
```

- `-v $(pwd)/data:/app` - Mount `./data` on host to `/app` in container
- Database files will be saved to `./data/` on your machine

### Docker Compose with Volumes

```yaml
services:
  bot:
    build: .
    env_file:
      - .env
    volumes:
      - bot-data:/app

volumes:
  bot-data:
```

This creates a named volume managed by Docker.

---

## Deployment with Docker

### How Cloud Platforms Use Docker

Most cloud platforms that support Docker follow this pattern:

```
┌─────────────────────────────────────────────────────────┐
│                    Your Computer                        │
│  ┌─────────────┐      git push       ┌──────────────┐  │
│  │   Code +    │ ──────────────────► │    GitHub    │  │
│  │ Dockerfile  │                     └──────────────┘  │
│  └─────────────┘                            │          │
└─────────────────────────────────────────────│──────────┘
                                              │
                                    webhook / detect push
                                              │
                                              ▼
┌─────────────────────────────────────────────────────────┐
│                  Cloud Platform                         │
│                                                         │
│  1. Clone repository                                    │
│  2. docker build -t app .                               │
│  3. docker run (with your env vars from dashboard)      │
│  4. Route traffic to container                          │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### Platform-Specific Notes

#### Railway

Railway auto-detects Dockerfiles and builds automatically.

```bash
# No special config needed
# Just push to GitHub, Railway builds from Dockerfile
```

Set environment variables in Railway dashboard → Variables.

#### Fly.io

Requires a `fly.toml` configuration file.

```toml
# fly.toml
app = "tldrkseid"
primary_region = "ord"

[build]
  dockerfile = "Dockerfile"

[env]
  # Non-secret env vars here

# Secrets set via CLI:
# fly secrets set DISCORD_BOT_TOKEN=xxx OPENAI_API_KEY=yyy
```

Deploy with:
```bash
fly launch
fly deploy
```

#### Render

Uses `render.yaml` or dashboard configuration.

```yaml
# render.yaml
services:
  - type: worker  # Not "web" since bot doesn't serve HTTP
    name: tldrkseid
    env: docker
    dockerfilePath: ./Dockerfile
    envVars:
      - key: DISCORD_BOT_TOKEN
        sync: false  # Set manually in dashboard
      - key: OPENAI_API_KEY
        sync: false
```

### Important: Worker vs Web Service

Discord bots are **not** web servers. They don't listen on HTTP ports.

- **Web Service** - Listens on a port, handles HTTP requests
- **Worker Service** - Background process, no HTTP

TLDRkseid is a **worker**. On platforms like Render or Fly.io, configure it as a worker/background service, not a web service.

---

## Common Commands

### Image Commands

```bash
# List images
docker images

# Remove image
docker rmi tldrkseid

# Remove all unused images
docker image prune

# Build with no cache (fresh build)
docker build --no-cache -t tldrkseid .
```

### Container Commands

```bash
# List running containers
docker ps

# List all containers (including stopped)
docker ps -a

# Stop container
docker stop tldrkseid-bot

# Start stopped container
docker start tldrkseid-bot

# Remove container
docker rm tldrkseid-bot

# View logs
docker logs tldrkseid-bot

# Follow logs (live)
docker logs -f tldrkseid-bot

# Execute command in running container
docker exec -it tldrkseid-bot /bin/bash
```

### Cleanup Commands

```bash
# Remove all stopped containers
docker container prune

# Remove all unused data (containers, images, volumes)
docker system prune

# Nuclear option: remove everything
docker system prune -a --volumes
```

---

## Troubleshooting

### Container Exits Immediately

**Symptom:** Container starts and immediately stops.

**Check logs:**
```bash
docker logs tldrkseid-bot
```

**Common causes:**
1. Missing environment variables
2. Invalid Discord token
3. Application crash on startup

### "Permission Denied" Errors

**On Linux:**
```bash
# Add user to docker group
sudo usermod -aG docker $USER
# Log out and back in
```

### Port Already in Use

Discord bots don't use ports, but if you added a health check endpoint:
```bash
# Find what's using the port
lsof -i :8080

# Or use a different port
docker run -p 8081:8080 tldrkseid
```

### Out of Disk Space

```bash
# Check Docker disk usage
docker system df

# Clean up
docker system prune -a
```

### Database Not Persisting

Make sure you're using volumes:
```bash
docker run -v $(pwd)/data:/app --env-file .env tldrkseid
```

### Build Fails

```bash
# Try building with verbose output
docker build --progress=plain -t tldrkseid .

# Build with no cache
docker build --no-cache -t tldrkseid .
```

### Container Can't Reach Internet

```bash
# Check if container has network access
docker run --rm tldrkseid ping -c 1 google.com

# Try with host network (debugging only)
docker run --network host --env-file .env tldrkseid
```

---

## Quick Reference

### Development Workflow

```bash
# 1. Make code changes
# 2. Rebuild image
docker build -t tldrkseid .

# 3. Stop old container
docker stop tldrkseid-bot
docker rm tldrkseid-bot

# 4. Start new container
docker run -d --name tldrkseid-bot --env-file .env tldrkseid

# 5. Check logs
docker logs -f tldrkseid-bot
```

### Production Workflow

```bash
# With Docker Compose (recommended)
docker compose up -d --build

# View status
docker compose ps

# View logs
docker compose logs -f

# Restart
docker compose restart

# Stop
docker compose down
```

---

## Summary

| What | Why | How |
|------|-----|-----|
| **Docker** | Consistent environments | Install Docker Desktop |
| **Dockerfile** | Build instructions | Multi-stage for small images |
| **Image** | Immutable template | `docker build` |
| **Container** | Running instance | `docker run` |
| **Volume** | Persistent data | `-v host:container` |
| **Compose** | Multi-container apps | `docker-compose.yml` |
| **Deployment** | Cloud hosting | Push to Git, platform builds |

---

*Last updated: January 2026*
