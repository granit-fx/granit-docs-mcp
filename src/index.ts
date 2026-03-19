import { Hono } from 'hono';
import { WebStandardStreamableHTTPServerTransport } from '@modelcontextprotocol/sdk/server/webStandardStreamableHttp.js';
import { createMcpServer } from './mcp/server.js';

export interface Env {
  CACHE: KVNamespace;
  SEARCH_INDEX_URL: string;
  CODE_INDEX_URL: string;
  FRONT_INDEX_URL: string;
}

const app = new Hono<{ Bindings: Env }>();

// Global error handler — fail-open: return 503 instead of crashing the Worker.
// A crash still counts as a billed request on Cloudflare; a clean 503 does too,
// but it avoids retry storms from clients that interpret a TCP-level failure as
// "try again immediately".
app.onError((err, c) => {
  console.error('[granit-mcp] Unhandled error:', err.message);
  return c.json(
    { error: 'internal_error', message: 'The MCP server encountered an error. Please retry later.' },
    503,
  );
});

// Health check
app.get('/', (c) => c.json({ name: 'granit-mcp', version: '2.0.0', status: 'ok' }));

// MCP endpoint — Streamable HTTP transport (stateless: one transport per request)
app.all('/mcp', async (c) => {
  const server = createMcpServer(c.env);

  const transport = new WebStandardStreamableHTTPServerTransport({
    sessionIdGenerator: undefined,
    enableJsonResponse: true,
  });

  await server.connect(transport);
  return transport.handleRequest(c.req.raw);
});

// Catch-all: reject unknown paths early to avoid unnecessary processing
app.all('*', (c) => c.json({ error: 'not_found', endpoints: ['/', '/mcp'] }, 404));

export default app;
