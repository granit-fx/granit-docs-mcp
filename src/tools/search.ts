import type { KVCache } from '../lib/index-cache.js';
import { getSearchIndex } from '../lib/index-cache.js';
import { searchIndex } from '../lib/search.js';

export interface SearchInput {
  query: string;
  limit: number;
}

export async function handleSearch(input: SearchInput, indexUrl: string, cache: KVCache): Promise<string> {
  const entries = await getSearchIndex(indexUrl, cache);
  const results = searchIndex(entries, input.query, input.limit);

  if (results.length === 0) {
    return `No results found for "${input.query}".`;
  }

  const formatted = results
    .map((r, i) =>
      `### ${i + 1}. ${r.title}\n` +
      `**URL:** ${r.url}\n` +
      `**Category:** ${r.category} · **Score:** ${r.score.toFixed(3)}\n\n` +
      (r.description ? `> ${r.description}\n\n` : '') +
      r.excerpt,
    )
    .join('\n\n---\n\n');

  return `## Search results for "${input.query}" (${results.length} found)\n\n${formatted}`;
}
