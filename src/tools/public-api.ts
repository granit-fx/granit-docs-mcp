import type { KVCache } from '../lib/index-cache.js';
import type { CodeSymbol, FrontExport } from '../lib/code-index.js';
import { getCodeIndex, getFrontIndex } from '../lib/code-index.js';

export interface PublicApiInput {
  type: string;
  repo?: 'dotnet' | 'front';
}

export async function handlePublicApi(
  input: PublicApiInput,
  codeIndexUrl: string,
  frontIndexUrl: string,
  cache: KVCache,
): Promise<string> {
  const query = input.type.toLowerCase().replace(/^granit\.?/, '');

  // Search .NET
  if (input.repo !== 'front') {
    const codeIndex = await getCodeIndex(codeIndexUrl, cache);
    if (codeIndex) {
      const match = findDotnetType(codeIndex.symbols, query);
      if (match) return formatDotnetApi(match);
    }
  }

  // Search Front
  if (input.repo !== 'dotnet') {
    const frontIndex = await getFrontIndex(frontIndexUrl, cache);
    if (frontIndex) {
      const match = findFrontExport(frontIndex.packages, query);
      if (match) return formatFrontApi(match.pkg, match.exp);
    }
  }

  return (
    `Type "${input.type}" not found in the code index.\n\n` +
    'Tip: use `search_code` to find the correct type name.'
  );
}

// ─── .NET matching ────────────────────────────────────────────────────────────

function findDotnetType(
  symbols: CodeSymbol[],
  query: string,
): CodeSymbol | undefined {
  const alpha = query.replaceAll(/[^a-z0-9]/g, '');

  // 1. Exact name match
  const exact = symbols.find(
    (s) => s.name.toLowerCase() === query || s.name.toLowerCase() === alpha,
  );
  if (exact) return exact;

  // 2. FQN ends with query
  const byFqn = symbols.find(
    (s) => s.fqn.toLowerCase().endsWith(`.${query}`) ||
           s.fqn.toLowerCase().endsWith(`.${alpha}`),
  );
  if (byFqn) return byFqn;

  // 3. Partial match — shortest name wins
  const partials = symbols
    .filter((s) => s.name.toLowerCase().includes(alpha))
    .sort((a, b) => a.name.length - b.name.length);
  return partials[0];
}

function formatDotnetApi(sym: CodeSymbol): string {
  const namespace = sym.fqn.replace(`.${sym.name}`, '');
  const lines = [
    `## ${sym.name}`,
    `**Kind:** ${sym.kind} · **Namespace:** ${namespace}`,
    `**Project:** ${sym.project} · **File:** ${sym.file}`,
    '',
  ];

  if (sym.members.length === 0) {
    lines.push('*No public members.*');
  } else {
    const grouped = groupBy(sym.members, (m) => m.kind);
    for (const [kind, members] of Object.entries(grouped)) {
      const heading = `### ${capitalize(kind)}s (${members.length})`;
      const memberLines = members.map((m) => {
        const ret = m.returnType ? ` → ${m.returnType}` : '';
        return `- \`${m.signature}\`${ret}`;
      });
      lines.push(heading, '', ...memberLines, '');
    }
  }

  return lines.join('\n');
}

// ─── Front-end matching ───────────────────────────────────────────────────────

function findFrontExport(
  packages: { name: string; description: string; exports: FrontExport[] }[],
  query: string,
): { pkg: string; exp: FrontExport } | undefined {
  const alpha = query.replaceAll(/[^a-z0-9]/g, '');

  for (const pkg of packages) {
    const match = pkg.exports.find(
      (e) => e.name.toLowerCase() === query ||
             e.name.toLowerCase() === alpha,
    );
    if (match) return { pkg: pkg.name, exp: match };
  }

  // Partial
  for (const pkg of packages) {
    const match = pkg.exports.find(
      (e) => e.name.toLowerCase().includes(alpha),
    );
    if (match) return { pkg: pkg.name, exp: match };
  }

  return undefined;
}

function formatFrontApi(packageName: string, exp: FrontExport): string {
  const lines = [
    `## ${exp.name}`,
    `**Kind:** ${exp.kind} · **Package:** ${packageName}`,
    `**Signature:** \`${exp.signature}\``,
    '',
  ];

  if (exp.members && exp.members.length > 0) {
    const memberLines = exp.members.map((m) => `- \`${m.signature}\``);
    lines.push('### Members', '', ...memberLines);
  }

  return lines.join('\n');
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function groupBy<T>(arr: T[], key: (item: T) => string): Record<string, T[]> {
  const result: Record<string, T[]> = {};
  for (const item of arr) {
    const k = key(item);
    if (!result[k]) {
      result[k] = [];
    }
    result[k].push(item);
  }
  return result;
}

function capitalize(s: string): string {
  return s.charAt(0).toUpperCase() + s.slice(1);
}
