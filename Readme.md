# TeollinenInternetProjekti

This repository contains components for a small industrial IoT project:

- Backend Node.js server (MQTT + MongoDB) in `backend/`.
- A WPF client application in `LukuOhjelma/`.
- An ESP firmware project in `TeollinenInternetESP/`.

**Quick Start**

Prerequisites:
- Node.js (v18+ recommended)
- Docker (optional, for containerized backend or MongoDB)
- MongoDB (or run via Docker)
- For the ESP: PlatformIO
- For the WPF app: .NET SDK / Visual Studio

**Backend — run locally**

1. Open a shell and change to the backend folder:

	`cd backend`

2. Install dependencies:

	`npm install`

3. Create an `.env` file in the `backend/` directory (see the **.env files** section below for required variables).

4. Start the server:

	`node index.js`

The backend listens on port `3001` by default (see `backend/index.js`).

**Docker (build & run backend)**

Build the Docker image from the `backend/` directory:

```
cd backend
docker build -t your-image-name .
```

Stop and remove an existing container (if present):

```
docker stop your-container
docker rm your-container
```

Run the container (example using an env-file):

```
docker run -d --name your-container --env-file .env -p 3001:3001 your-image-name
```

There is also a helper script in `backend/run_docker`. On Unix systems run `./run_docker`. On Windows you can run it with Git Bash or WSL: `bash run_docker`.

**MongoDB — install locally (Windows)**

Option A: Install MongoDB Community Server

1. Download the installer from the MongoDB website and follow the Windows installation instructions.
2. Start the MongoDB service (or use the included MongoDB Compass GUI to verify).
3. Typical connection string for local MongoDB:

	`mongodb://localhost:27017/teollinen`

Option B: Run MongoDB in Docker (quick and reproducible):

```
docker run -d --name mongodb -p 27017:27017 -v mongodata:/data/db mongo:6.0
```

Use the connection string above and point the backend `MONGO_URI` to it.

**.env files and required variables**

Place your `.env` file into the `backend/` directory (the backend loads environment variables via `dotenv`). Example variables used by `backend/index.js`:

```
MONGO_URI=mongodb://localhost:27017/teollinen
MQTT_HOST=your-mqtt-broker-host
MQTT_PORT=1883
MQTT_USERNAME=optional
MQTT_PASSWORD=optional
MQTT_PROTOCOL=mqtt # or mqtts/ws/wss depending on broker
```

Notes:
- If you run the backend in Docker, pass the same `.env` file via `--env-file` or use Docker secrets/environment variables.
- If your broker requires TLS or websockets, set `MQTT_PROTOCOL` accordingly and adjust `MQTT_PORT`.

**Debugging tips**

- To get verbose MQTT.js logs on Windows (PowerShell):

  `$env:DEBUG="mqttjs*"; node index.js`

- On cmd.exe:

  `set DEBUG=mqttjs* && node index.js`

- If you see repeated reconnects, verify the MQTT host/port/protocol and check broker logs.

**Relevant files**

- Backend server: [backend/index.js](backend/index.js)
- Docker helper: [backend/run_docker](backend/run_docker)

If you want, I can add a `backend/.env.example` file and a basic `docker-compose.yml` to simplify local development. Would you like me to add those?
