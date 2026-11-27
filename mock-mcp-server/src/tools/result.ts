import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

/** Successful tool result: JSON as text (for LLM clients) + structuredContent (for code). */
export function ok(data: Record<string, unknown>): CallToolResult {
  return {
    content: [{ type: 'text', text: JSON.stringify(data, null, 2) }],
    structuredContent: data,
  };
}

/** Tool-level error (isError), as opposed to a JSON-RPC protocol error. */
export function fail(code: string, message: string): CallToolResult {
  return {
    isError: true,
    content: [{ type: 'text', text: JSON.stringify({ error: { code, message } }) }],
  };
}
