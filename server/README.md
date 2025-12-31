# KITRANSFERT Signaling Server

Simple Node.js server for peer discovery via share codes.

## Deploy to Render.com

### Option 1: One-Click Deploy

[![Deploy to Render](https://render.com/images/deploy-to-render-button.svg)](https://render.com/deploy)

### Option 2: Manual Deploy

1. Create a [Render.com](https://render.com) account (free)
2. Click "New" → "Web Service"  
3. Connect your GitHub repo or use "Public Git repository"
4. Enter: `https://github.com/YOUR_USERNAME/lantransfer` (or upload manually)
5. Configure:
   - **Name**: `lantransfer-signaling`
   - **Root Directory**: `server`
   - **Runtime**: `Node`
   - **Build Command**: `npm install` (leave default)
   - **Start Command**: `npm start`
   - **Instance Type**: `Free`
6. Click "Create Web Service"

Your server will be available at: `https://lantransfer-signaling.onrender.com`

## Local Development

```bash
cd server
node index.js
```

Server runs on `http://localhost:3000`

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/register?port=N&name=X` | POST | Register and get a code |
| `/lookup?code=ABC123` | GET | Lookup peer by code |
| `/refresh?code=ABC123` | POST | Refresh code expiry |
| `/unregister?code=ABC123` | POST | Unregister code |
