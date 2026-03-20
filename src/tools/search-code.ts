import type { KVCache } from '../lib/index-cache.js';
import type { CodeIndex, FrontIndex, FrontExport } from '../lib/code-index.js';
import { getCodeIndex, getFrontIndex } from '../lib/code-index.js';

export interface SearchCodeInput {
  query: string;
  repo?: 'dotnet' | 'front';
  kind?: string;
  limit: number;
  branch?: string;
}

interface ScoredResult {
  name: string;
  fqn: string;
  kind: string;
  project: string;
  file?: string;
  signature?: string;
  repo: string;
  score: number;
}

export async function handleSearchCode(
  input: SearchCodeInput,
  codeIndexUrl: string,
  frontIndexUrl: string,
  cache: KVCache,
): Promise<string> {
  const results: ScoredResult[] = [];

  if (input.repo !== 'front') {
    const codeIndex = await getCodeIndex(codeIndexUrl, cache, input.branch);
    if (codeIndex) {
      results.push(...searchDotnet(codeIndex, input));
    }
  }

  if (input.repo !== 'dotnet') {
    const frontIndex = await getFrontIndex(frontIndexUrl, cache, input.branch);
    if (frontIndex) {
      results.push(...searchFront(frontIndex, input));
    }
  }

  if (results.length === 0) {
    const hint = input.repo
      ? ` in repo "${input.repo}"`
      : '';
    return `No code results found for "${input.query}"${hint}.`;
  }

  results.sort((a, b) => b.score - a.score);
  const top = results.slice(0, input.limit);

  const formatted = top
    .map((r, i) => {
      const lines = [
        `### ${i + 1}. ${r.name}`,
        `**Kind:** ${r.kind} · **Repo:** ${r.repo} · **Project:** ${r.project}`,
      ];
      if (r.fqn) lines.push(`**FQN:** ${r.fqn}`);
      if (r.file) lines.push(`**File:** ${r.file}`);
      if (r.signature) lines.push(`**Signature:** \`${r.signature}\``);
      return lines.join('\n');
    })
    .join('\n\n---\n\n');

  return `## Code search for "${input.query}" (${top.length} found)\n\n${formatted}`;
}

// ─── .NET search ──────────────────────────────────────────────────────────────

function searchDotnetMembers(
  sym: CodeIndex['symbols'][number],
  terms: string[],
  kindFilter?: string,
): ScoredResult[] {
  const results: ScoredResult[] = [];
  for (const member of sym.members) {
    if (kindFilter && member.kind !== kindFilter) continue;

    const memberScore = scoreMember(member.name, sym.name, member.signature, terms);
    if (memberScore > 0) {
      results.push({
        name: `${sym.name}.${member.name}`,
        fqn: `${sym.fqn}.${member.name}`,
        kind: member.kind,
        project: sym.project,
        file: sym.file,
        signature: member.signature,
        repo: 'dotnet',
        score: memberScore,
      });
    }
  }
  return results;
}

function searchDotnet(index: CodeIndex, input: SearchCodeInput): ScoredResult[] {
  const terms = tokenize(input.query);
  if (terms.length === 0) return [];

  const results: ScoredResult[] = [];

  for (const sym of index.symbols) {
    if (input.kind && sym.kind !== input.kind) continue;

    const score = scoreSymbol(sym.name, sym.fqn, sym.members.map((m) => m.name), terms);
    if (score > 0) {
      results.push({
        name: sym.name,
        fqn: sym.fqn,
        kind: sym.kind,
        project: sym.project,
        file: sym.file,
        repo: 'dotnet',
        score,
      });
    }

    results.push(...searchDotnetMembers(sym, terms, input.kind));
  }

  return results;
}

// ─── Front-end search ─────────────────────────────────────────────────────────

function searchFront(index: FrontIndex, input: SearchCodeInput): ScoredResult[] {
  const terms = tokenize(input.query);
  if (terms.length === 0) return [];

  const results: ScoredResult[] = [];

  for (const pkg of index.packages) {
    for (const exp of pkg.exports) {
      if (input.kind && exp.kind !== input.kind) continue;

      const score = scoreExport(exp, pkg.name, terms);
      if (score > 0) {
        results.push({
          name: exp.name,
          fqn: `${pkg.name}/${exp.name}`,
          kind: exp.kind,
          project: pkg.name,
          signature: exp.signature,
          repo: 'front',
          score,
        });
      }
    }
  }

  return results;
}

// ─── Scoring ──────────────────────────────────────────────────────────────────

function tokenize(query: string): string[] {
  return query
    .toLowerCase()
    .split(/\s+/)
    .filter((t) => t.length >= 2);
}

function countHits(text: string, terms: string[]): number {
  const lower = text.toLowerCase();
  let count = 0;
  for (const term of terms) {
    let idx = 0;
    while ((idx = lower.indexOf(term, idx)) !== -1) {
      count++;
      idx += term.length;
    }
  }
  return count;
}

function scoreSymbol(
  name: string,
  fqn: string,
  memberNames: string[],
  terms: string[],
): number {
  const nameHits = countHits(name, terms) * 5;
  const fqnHits = countHits(fqn, terms) * 3;
  const memberText = memberNames.join(' ');
  const memberHits = countHits(memberText, terms);
  return nameHits + fqnHits + memberHits;
}

function scoreMember(
  name: string,
  parentName: string,
  signature: string,
  terms: string[],
): number {
  const nameHits = countHits(name, terms) * 5;
  const parentHits = countHits(parentName, terms) * 2;
  const sigHits = countHits(signature, terms);
  return nameHits + parentHits + sigHits;
}

function scoreExport(
  exp: FrontExport,
  packageName: string,
  terms: string[],
): number {
  const nameHits = countHits(exp.name, terms) * 5;
  const pkgHits = countHits(packageName, terms) * 2;
  const sigHits = countHits(exp.signature, terms);
  return nameHits + pkgHits + sigHits;
}
