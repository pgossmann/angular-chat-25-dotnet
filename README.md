# Angular Chat 25 - Full Stack Chatbot

A modern full-stack chatbot application built with Angular 20 frontend and C# ASP.NET Core backend, featuring real-time streaming responses for LLM integration.

## 🚀 Features

- **Angular 20** with standalone components, signals, and modern architecture
- **ASP.NET Core 8.0** Web API with streaming support
- **Real-time streaming** chat responses using Server-Sent Events (SSE)
- **CORS configuration** for seamless frontend-backend communication
- **Docker support** for containerized local development
- **CI/CD pipeline** with GitHub Actions
- **Deployment ready** for GitHub Pages (frontend) and Azure/Railway (backend)

## 📁 Project Structure

```
├── frontend/           # Angular 20 application
├── backend/           # ASP.NET Core Web API
├── docs/             # Documentation
├── .github/workflows/ # CI/CD pipelines
├── docker-compose.yml # Local development setup
└── README.md         # This file
```

## 🛠️ Quick Start

### Prerequisites

- Node.js 20+
- .NET 8.0 SDK
- Docker (optional)

### Local Development

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd angular-chat-25-dotnet
   ```

2. **Start the backend**
   ```bash
   cd backend
   dotnet restore
   dotnet run
   ```

3. **Start the frontend**
   ```bash
   cd frontend
   npm install
   npm start
   ```

4. **Or use Docker Compose**
   ```bash
   docker-compose up --build
   ```

### Access the Application

- **Frontend**: http://localhost:4200
- **Backend API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger

## 🔧 API Endpoints

- `POST /api/chat/send` - Send a chat message
- `POST /api/chat/stream` - Stream chat responses (SSE)
- `GET /api/chat/health` - Health check endpoint

## 🚀 Deployment

### Frontend (GitHub Pages)
The frontend is configured for automatic deployment to GitHub Pages on push to main branch.

### Backend (Cloud Deployment)
The backend includes Dockerfile and can be deployed to:
- Azure App Service
- Railway
- Any container-compatible hosting service

## 🧪 Testing

- **Frontend**: `npm test` (in frontend directory)
- **Backend**: `dotnet test` (in backend directory)

## 📚 Documentation

Detailed setup and deployment instructions are available in the [docs/setup.md](docs/setup.md) file.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
