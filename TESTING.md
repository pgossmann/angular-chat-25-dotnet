# Testing Instructions

## Prerequisites
- .NET 8.0 SDK installed
- Node.js 20+ installed

## Testing the Backend

1. **Navigate to backend directory:**
   ```bash
   cd backend
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Run the backend:**
   ```bash
   dotnet run
   ```

4. **Verify backend is running:**
   - The backend should start on `http://localhost:5000` and `https://localhost:5001`
   - You should see output like:
     ```
     info: Microsoft.Hosting.Lifetime[14]
           Now listening on: http://localhost:5000
     info: Microsoft.Hosting.Lifetime[14]
           Now listening on: https://localhost:5001
     ```

5. **Test the HelloWorld endpoint:**
   ```bash
   curl http://localhost:5000/api/chat/hello
   ```
   
   Expected response:
   ```json
   {
     "message": "Hello World from ASP.NET Core!",
     "timestamp": "2025-01-09T..."
   }
   ```

6. **Test Swagger UI:**
   - Open browser to: `http://localhost:5000/swagger`
   - You should see the Swagger documentation with the chat endpoints

## Testing the Frontend

1. **Open a new terminal and navigate to frontend directory:**
   ```bash
   cd frontend
   ```

2. **Install dependencies:**
   ```bash
   npm install
   ```

3. **Start the development server:**
   ```bash
   npm start
   ```

4. **Verify frontend is running:**
   - The frontend should start on `http://localhost:4200`
   - Your browser should automatically open to the application

## Testing Frontend-Backend Communication

1. **Ensure both backend and frontend are running** (steps above)

2. **Open the application in your browser:**
   - Go to `http://localhost:4200`

3. **Test the connection:**
   - Click the "Test Backend Connection" button
   - If successful, you should see: ✅ Success! "Hello World from ASP.NET Core!"
   - If failed, you should see an error message

## Troubleshooting

### Backend Issues
- **Port already in use:** Stop any existing processes on ports 5000/5001
- **CORS errors:** The backend is configured to allow requests from `http://localhost:4200`
- **SSL certificate issues:** Use `http://localhost:5000` instead of `https://localhost:5001` for testing

### Frontend Issues
- **Angular CLI not found:** Install globally with `npm install -g @angular/cli`
- **Build errors:** Make sure you're using Node.js 20+
- **HTTP requests failing:** Ensure backend is running on `http://localhost:5000`

### Connection Issues
- **CORS errors:** Check browser console - backend should allow requests from frontend
- **Network errors:** Verify both services are running on correct ports
- **Firewall issues:** Make sure Windows Defender or other firewalls aren't blocking the connections

## API Endpoints Available

- `GET /api/chat/hello` - Hello World test endpoint
- `POST /api/chat/send` - Send a chat message
- `POST /api/chat/stream` - Stream chat responses (Server-Sent Events)
- `GET /api/chat/health` - Health check endpoint

## Next Steps

Once the basic connection test works, you're ready to:
1. Implement full chat functionality
2. Add LLM integration to the streaming endpoint
3. Deploy to production environments