const http = require('http');
const url = require('url');

// In-memory store for registered peers
// Format: { code: { ip, port, name, timestamp } }
const peers = new Map();

// Cleanup interval (remove stale peers after 5 minutes)
const PEER_TIMEOUT_MS = 5 * 60 * 1000;

setInterval(() => {
    const now = Date.now();
    for (const [code, peer] of peers) {
        if (now - peer.timestamp > PEER_TIMEOUT_MS) {
            peers.delete(code);
            console.log(`[Cleanup] Removed stale peer: ${code}`);
        }
    }
}, 60000); // Check every minute

// Generate a random 6-character code
function generateCode() {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // Easy to read chars
    let code = '';
    for (let i = 0; i < 6; i++) {
        code += chars[Math.floor(Math.random() * chars.length)];
    }
    return code;
}

// Get client IP from request
function getClientIP(req) {
    return req.headers['x-forwarded-for']?.split(',')[0]?.trim()
        || req.socket.remoteAddress
        || 'unknown';
}

// Handle HTTP requests
function handleRequest(req, res) {
    const parsedUrl = url.parse(req.url, true);
    const path = parsedUrl.pathname;
    const query = parsedUrl.query;

    // CORS headers
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Content-Type', 'application/json');

    if (req.method === 'OPTIONS') {
        res.writeHead(200);
        res.end();
        return;
    }

    // Health check
    if (path === '/health') {
        res.writeHead(200);
        res.end(JSON.stringify({ status: 'ok', peers: peers.size }));
        return;
    }

    // Register: POST /register?port=12345&name=MyPC
    if (path === '/register' && req.method === 'POST') {
        const port = parseInt(query.port);
        const name = query.name || 'Unknown';

        if (!port || port < 1 || port > 65535) {
            res.writeHead(400);
            res.end(JSON.stringify({ error: 'Invalid port' }));
            return;
        }

        const ip = getClientIP(req);
        const code = generateCode();

        // Ensure unique code
        while (peers.has(code)) {
            code = generateCode();
        }

        peers.set(code, {
            ip,
            port,
            name,
            timestamp: Date.now()
        });

        console.log(`[Register] ${code} -> ${ip}:${port} (${name})`);

        res.writeHead(200);
        res.end(JSON.stringify({
            code,
            expiresIn: PEER_TIMEOUT_MS / 1000
        }));
        return;
    }

    // Refresh: POST /refresh?code=ABC123
    if (path === '/refresh' && req.method === 'POST') {
        const code = query.code?.toUpperCase();

        if (!code || !peers.has(code)) {
            res.writeHead(404);
            res.end(JSON.stringify({ error: 'Code not found' }));
            return;
        }

        const peer = peers.get(code);
        const ip = getClientIP(req);

        // Update timestamp and IP (in case it changed)
        peer.timestamp = Date.now();
        peer.ip = ip;

        console.log(`[Refresh] ${code} -> ${ip}:${peer.port}`);

        res.writeHead(200);
        res.end(JSON.stringify({ success: true }));
        return;
    }

    // Lookup: GET /lookup?code=ABC123
    if (path === '/lookup' && req.method === 'GET') {
        const code = query.code?.toUpperCase();

        if (!code) {
            res.writeHead(400);
            res.end(JSON.stringify({ error: 'Missing code' }));
            return;
        }

        const peer = peers.get(code);

        if (!peer) {
            res.writeHead(404);
            res.end(JSON.stringify({ error: 'Code not found' }));
            return;
        }

        console.log(`[Lookup] ${code} -> ${peer.ip}:${peer.port}`);

        res.writeHead(200);
        res.end(JSON.stringify({
            ip: peer.ip,
            port: peer.port,
            name: peer.name
        }));
        return;
    }

    // Unregister: POST /unregister?code=ABC123
    if (path === '/unregister' && req.method === 'POST') {
        const code = query.code?.toUpperCase();

        if (code && peers.has(code)) {
            peers.delete(code);
            console.log(`[Unregister] ${code}`);
        }

        res.writeHead(200);
        res.end(JSON.stringify({ success: true }));
        return;
    }

    // Default: 404
    res.writeHead(404);
    res.end(JSON.stringify({ error: 'Not found' }));
}

// Start server
const PORT = process.env.PORT || 3000;
const server = http.createServer(handleRequest);

server.listen(PORT, () => {
    console.log(`🚀 LAN Transfer Signaling Server running on port ${PORT}`);
    console.log(`   Health: http://localhost:${PORT}/health`);
});
