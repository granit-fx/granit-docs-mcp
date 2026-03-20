/**
 * Code index types and cache for .NET (granit-dotnet) and
 * TypeScript (granit-front) source code.
 *
 * Indexes are generated at CI time — by RoslynLens for .NET,
 * by ts-morph for TypeScript — and published as GitHub Release Assets.
 */

import type { KVCache } from './index-cache.js';
import { getIndex } from './index-cache.js';

// ─── .NET code index (code-index.json) ────────────────────────────────────────

export interface CodeMember {
  name: string;
  kind: string;       // "method" | "property" | "event" | "field"
  signature: string;
  returnType?: string;
}

export interface CodeSymbol {
  name: string;
  fqn: string;        // Fully-qualified name
  kind: string;       // "class" | "interface" | "enum" | "record" | "struct"
  project: string;
  file: string;
  line?: number;
  members: CodeMember[];
}

export interface ProjectNode {
  name: string;
  deps: string[];
  framework: string;
}

export interface CodeIndex {
  generatedAt: string;
  repo: string;
  projectGraph: ProjectNode[];
  symbols: CodeSymbol[];
}

// ─── TypeScript front index (front-index.json) ───────────────────────────────

export interface FrontExport {
  name: string;
  kind: string;       // "function" | "interface" | "type" | "class" | "enum"
  signature: string;
  members?: CodeMember[];
}

export interface FrontPackage {
  name: string;
  description: string;
  exports: FrontExport[];
}

export interface FrontIndex {
  generatedAt: string;
  repo: string;
  packages: FrontPackage[];
}

// ─── Cache helpers ────────────────────────────────────────────────────────────

export async function getCodeIndex(
  url: string,
  cache: KVCache,
  branch?: string,
): Promise<CodeIndex | null> {
  try {
    return await getIndex<CodeIndex>('code', url, cache, branch);
  } catch {
    return null;
  }
}

export async function getFrontIndex(
  url: string,
  cache: KVCache,
  branch?: string,
): Promise<FrontIndex | null> {
  try {
    return await getIndex<FrontIndex>('front', url, cache, branch);
  } catch {
    return null;
  }
}
