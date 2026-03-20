import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { z } from 'zod';
import { handleSearch } from '../tools/search.js';
import { handleModuleRef } from '../tools/module-ref.js';
import { handleListPatterns } from '../tools/patterns.js';
import { handleListPackages } from '../tools/list-packages.js';
import { handlePackageInfo } from '../tools/package-info.js';
import { handleSearchCode } from '../tools/search-code.js';
import { handlePublicApi } from '../tools/public-api.js';
import { handleProjectGraph } from '../tools/project-graph.js';
import { handleListBranches } from '../tools/list-branches.js';
import type { Env } from '../index.js';

export function createMcpServer(env: Env): McpServer {
  const server = new McpServer({
    name: 'granit-mcp',
    version: '2.0.0',
  });

  const {
    CACHE: cache,
    SEARCH_INDEX_URL: indexUrl,
    CODE_INDEX_URL: codeIndexUrl,
    FRONT_INDEX_URL: frontIndexUrl,
  } = env;

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

  // ─── search_code ─────────────────────────────────────────────────────────────
  server.registerTool(
    'search_code',
    {
      description:
        'Search across Granit source code symbols (types, methods, interfaces, enums). ' +
        'Returns ranked matches with name, kind, project, file path, and signature. ' +
        'Searches pre-built code indexes for both .NET (granit-dotnet) and TypeScript (granit-front).',
      inputSchema: {
        query: z.string().min(1).describe('Search query — type name, method name, or keywords'),
        repo: z
          .enum(['dotnet', 'front'])
          .optional()
          .describe('Restrict search to a specific repo. Omit to search both.'),
        kind: z
          .string()
          .optional()
          .describe('Filter by symbol kind: "class", "interface", "method", "enum", "record", "function", "type"'),
        limit: z.number().int().min(1).max(20).default(10).describe('Maximum results (default 10, max 20)'),
        branch: z.string().optional().describe('Git branch for the code index. Defaults to "develop". Use "main" for stable API.'),
      },
    },
    async ({ query, repo, kind, limit, branch }) => {
      const text = await handleSearchCode(
        { query, repo, kind, limit: limit ?? 10, branch },
        codeIndexUrl,
        frontIndexUrl,
        cache,
      );
      return { content: [{ type: 'text', text }] };
    },
  );

  // ─── get_public_api ─────────────────────────────────────────────────────────
  server.registerTool(
    'get_public_api',
    {
      description:
        'Retrieves the full public API surface of a Granit type — all public methods, properties, and events with their signatures. ' +
        'Works for both .NET types (from granit-dotnet) and TypeScript exports (from granit-front). ' +
        'Use when you need detailed member information for a specific type like IBlobStorage, ICurrentTenant, etc.',
      inputSchema: {
        type: z
          .string()
          .min(1)
          .describe('Type name, e.g. "IBlobStorage", "GranitModule", "createApiClient". Case-insensitive.'),
        repo: z
          .enum(['dotnet', 'front'])
          .optional()
          .describe('Restrict to a specific repo. Omit to search both.'),
        branch: z.string().optional().describe('Git branch for the code index. Defaults to "develop". Use "main" for stable API.'),
      },
    },
    async ({ type, repo, branch }) => {
      const text = await handlePublicApi({ type, repo, branch }, codeIndexUrl, frontIndexUrl, cache);
      return { content: [{ type: 'text', text }] };
    },
  );

  // ─── get_project_graph ──────────────────────────────────────────────────────
  server.registerTool(
    'get_project_graph',
    {
      description:
        'Shows the project/package dependency graph for the Granit framework. ' +
        'Lists all .NET projects (with NuGet dependencies) and/or TypeScript packages. ' +
        'Use to understand how modules relate to each other.',
      inputSchema: {
        repo: z
          .enum(['dotnet', 'front'])
          .optional()
          .describe('Restrict to a specific repo. Omit to show both.'),
        branch: z.string().optional().describe('Git branch for the code index. Defaults to "develop". Use "main" for stable API.'),
      },
    },
    async ({ repo, branch }) => {
      const text = await handleProjectGraph({ repo, branch }, codeIndexUrl, frontIndexUrl, cache);
      return { content: [{ type: 'text', text }] };
    },
  );

  // ─── list_branches ─────────────────────────────────────────────────────────
  server.registerTool(
    'list_branches',
    {
      description:
        'Lists Git branches that have a committed code index, so you know which values ' +
        'are valid for the "branch" parameter of search_code, get_public_api, and get_project_graph. ' +
        'Queries the public GitHub API (no auth required).',
      inputSchema: {
        repo: z
          .enum(['dotnet', 'front'])
          .optional()
          .describe('Restrict to a specific repo. Omit to check both.'),
      },
    },
    async ({ repo }) => {
      const text = await handleListBranches({ repo });
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
