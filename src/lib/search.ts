/**
 * TF-IDF-style search over the structured search index.
 *
 * Scores entries by term frequency in title (5x weight), description (3x),
 * and content (1x), normalized by document length. Much better than naive
 * string matching because the index has proper metadata and categories.
 */

import type { IndexEntry } from './index-cache.js';

const DOCS_BASE = 'https://granit-fx.dev';

export interface SearchResult {
  title: string;
  url: string;
  description: string;
  excerpt: string;
  category: string;
  score: number;
}

/**
 * Tokenizes a query into lowercase terms (2+ chars).
 */
function tokenize(query: string): string[] {
  return query
    .toLowerCase()
    .split(/\s+/)
    .filter((t) => t.length >= 2);
}

/**
 * Counts occurrences of a term in text (case-insensitive).
 */
function countOccurrences(text: string, term: string): number {
  const lower = text.toLowerCase();
  let count = 0;
  let idx = 0;
  while ((idx = lower.indexOf(term, idx)) !== -1) {
    count++;
    idx += term.length;
  }
  return count;
}

/**
 * Extracts a short excerpt around the first match.
 */
function extractExcerpt(content: string, terms: string[], maxLength = 250): string {
  const lower = content.toLowerCase();
  let bestPos = -1;

  for (const term of terms) {
    const pos = lower.indexOf(term);
    if (pos !== -1 && (bestPos === -1 || pos < bestPos)) {
      bestPos = pos;
    }
  }

  if (bestPos === -1) bestPos = 0;

  const start = Math.max(0, content.lastIndexOf('\n', Math.max(0, bestPos - 80)) + 1);
  const end = Math.min(content.length, start + maxLength);
  let excerpt = content.slice(start, end).trim();

  // Clean up
  excerpt = excerpt
    .replaceAll(/#{1,6}\s+/g, '')
    .replaceAll(/\n{2,}/g, ' ')
    .replaceAll(/\s{2,}/g, ' ');

  if (end < content.length) excerpt += '…';
  return excerpt;
}

/**
 * Scores a single index entry against the query terms.
 */
function scoreEntry(entry: IndexEntry, terms: string[]): number {
  let score = 0;

  for (const term of terms) {
    const titleHits = countOccurrences(entry.title, term);
    const descHits = countOccurrences(entry.description, term);
    const contentHits = countOccurrences(entry.content, term);

    // Weighted: title 5x, description 3x, content 1x
    const raw = titleHits * 5 + descHits * 3 + contentHits;

    // Normalize by sqrt of content length to reduce long-doc bias
    score += raw / Math.sqrt(1 + entry.content.length / 500);
  }

  return score;
}

/**
 * Full-text search over the structured index.
 */
export function searchIndex(
  entries: IndexEntry[],
  query: string,
  limit = 5,
  categoryFilter?: string,
): SearchResult[] {
  const terms = tokenize(query);
  if (terms.length === 0) return [];

  const candidates = categoryFilter
    ? entries.filter((e) => e.category === categoryFilter)
    : entries;

  return candidates
    .map((entry) => ({
      title: entry.title,
      url: `${DOCS_BASE}${entry.url}`,
      description: entry.description,
      excerpt: extractExcerpt(entry.content, terms),
      category: entry.category,
      score: scoreEntry(entry, terms),
    }))
    .filter((r) => r.score > 0)
    .sort((a, b) => b.score - a.score)
    .slice(0, limit);
}
