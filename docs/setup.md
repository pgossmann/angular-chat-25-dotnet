# Setup Guide

## Prerequisites

- Node.js 20+
- .NET 8.0 SDK
- Docker (optional)

## Local Development

### Frontend (Angular 20)
```bash
cd frontend
npm install
npm start
```
The frontend will be available at http://localhost:4200

### Backend (ASP.NET Core)
```bash
cd backend
dotnet restore
dotnet run
```
The backend API will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: https://localhost:5001/swagger

### Using Docker Compose
```bash
docker-compose up --build
```

## Deployment

### Frontend (GitHub Pages)
The frontend is configured for GitHub Pages deployment. Push to main branch to trigger deployment.

### Backend (Azure/Railway)
Configure your cloud provider with the included Dockerfile and environment variables.

## API Endpoints

- `POST /api/chat/send` - Send a chat message
- `POST /api/chat/stream` - Stream chat responses
- `GET /api/chat/health` - Health check