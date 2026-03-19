import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { z } from 'zod';
import { handleSearch } from '../tools/search.js';
import { handleModuleRef } from '../tools/module-ref.js';
import { handleListPatterns } from '../tools/patterns.js';
import { handleListPackages } from '../tools/list-packages.js';
import { handlePackageInfo } from '../tools/package-info.js';
import type { Env } from '../index.js';

export function createMcpServer(env: Env): McpServer {
  const server = new McpServer({
    name: 'granit-mcp',
    version: '2.0.0',
  });

  const { CACHE: cache, SEARCH_INDEX_URL: indexUrl } = env;

  // ─── search_granit_docs ───────────────────────────────────────────────────
  server.registerTool(
    'search_granit_docs',
    {
      description:
        'Full-text search across the entire Granit framework documentation. ' +
        'Returns the most relevant pages with title, URL, category, and excerpt. ' +
        'Use this first when looking for information about any Granit feature, module, or concept.',
      inputSchema: {
        query: z.string().min(1).describe('Search query in plain English or keywords'),
        limit: z.number().int().min(1).max(20).default(5).describe('Maximum number of results (default 5, max 20)'),
      },
    },
    async ({ query, limit }) => {
      const text = await handleSearch({ query, limit: limit ?? 5 }, indexUrl, cache);
      return { content: [{ type: 'text', text }] };
    },
  );

  // ─── get_module_reference ─────────────────────────────────────────────────
  server.registerTool(
    'get_module_reference',
    {
      description:
        'Retrieves the complete reference documentation for a specific Granit module. ' +
        'Returns the full module content including API, configuration options, and code examples. ' +
        'Use when you need detailed information about a specific module like BlobStorage, Observability, Identity, etc.',
      inputSchema: {
        module: z
          .string()
          .min(1)
          .describe(
            'Module name, e.g. "BlobStorage", "Observability", "MultiTenancy", "Identity". ' +
              'Case-insensitive. May include or omit the "Granit." prefix.',
          ),
      },
    },
    async ({ module }) => {
      const text = await handleModuleRef({ module }, indexUrl, cache);
      return { content: [{ type: 'text', text }] };
    },
  );

  // ─── list_patterns ────────────────────────────────────────────────────────
  server.registerTool(
    'list_patterns',
    {
      description:
        'Lists all architecture patterns documented in the Granit framework, grouped by platform (Backend .NET / Frontend TypeScript), with a short description and URL for each. ' +
        'Use this to discover available patterns or to get an overview of recommended practices.',
      inputSchema: {},
    },
    async () => {
      const text = await handleListPatterns(indexUrl, cache);
      return { content: [{ type: 'text', text }] };
    },
  );

  // ─── list_packages ──────────────────────────────────────────────────────────
  server.registerTool(
    'list_packages',
    {
      description:
        'Lists all published Granit NuGet packages with their latest version, description, and download count. ' +
        'Use this to discover which Granit.* packages exist on NuGet.',
      inputSchema: {},
    },
    async () => {
      const text = await handleListPackages(cache);
      return { content: [{ type: 'text', text }] };
    },
  );

  // ─── get_package_info ──────────────────────────────────────────────────────
  server.registerTool(
    'get_package_info',
    {
      description:
        'Retrieves detailed information about a specific Granit NuGet package: ' +
        'all published versions, dependency groups per target framework, license, and tags. ' +
        'Use when you need to check package versions, dependencies, or framework compatibility.',
      inputSchema: {
        package: z.string().min(1).describe('NuGet package ID, e.g. "Granit.Core", "Granit.BlobStorage"'),
        version: z
          .string()
          .optional()
          .describe('Specific version to inspect. If omitted, shows the latest version.'),
      },
    },
    async ({ package: packageId, version }) => {
      const text = await handlePackageInfo({ package: packageId, version }, cache);
      return { content: [{ type: 'text', text }] };
    },
  );

  return server;
}
