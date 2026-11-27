import express, { type Request, type Response } from 'express';
import cors from 'cors';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StreamableHTTPServerTransport } from '@modelcontextprotocol/sdk/server/streamableHttp.js';

const PORT = Number(process.env.PORT ?? 3333);

/**
 * Builds a fresh MCP server instance. The mock runs in stateless mode
 * (one server + transport per request); shared state will live in a
 * separate in-memory store so it survives across requests.
 */
function createServer(): McpServer {
  const server = new McpServer({
    name: 'unisk-mock-router',
    version: '0.1.0',
  });

  // Tools are registered in later steps.

  return server;
}

const app = express();
app.use(express.json());
// The Svelte frontend calls this from the browser, so allow cross-origin requests.
app.use(
  cors({
    origin: '*',
    exposedHeaders: ['mcp-session-id'],
    allowedHeaders: ['Content-Type', 'Authorization', 'mcp-session-id', 'mcp-protocol-version'],
  }),
);

app.post('/mcp', async (req: Request, res: Response) => {
  const server = createServer();
  const transport = new StreamableHTTPServerTransport({ sessionIdGenerator: undefined });

  res.on('close', () => {
    transport.close();
    server.close();
  });

  try {
    await server.connect(transport);
    await transport.handleRequest(req, res, req.body);
  } catch (err) {
    console.error('MCP request failed:', err);
    if (!res.headersSent) {
      res.status(500).json({
        jsonrpc: '2.0',
        error: { code: -32603, message: 'Internal server error' },
        id: null,
      });
    }
  }
});

// Stateless mode: no SSE stream on GET and no session to DELETE.
const methodNotAllowed = (_req: Request, res: Response) => {
  res.status(405).json({
    jsonrpc: '2.0',
    error: { code: -32000, message: 'Method not allowed.' },
    id: null,
  });
};
app.get('/mcp', methodNotAllowed);
app.delete('/mcp', methodNotAllowed);

app.get('/health', (_req: Request, res: Response) => {
  res.json({ status: 'ok' });
});

app.listen(PORT, () => {
  console.log(`Mock MCP server listening on http://localhost:${PORT}/mcp`);
});
