/**
 * Generic index fetcher with KV caching.
 *
 * Supports multiple indexes (docs, code, front) via prefixed cache keys
 * and configurable TTLs. Each index is fetched from a static URL and
 * cached in Cloudflare KV.
 */

// ─── TTLs per index type ──────────────────────────────────────────────────────

const TTL = {
  docs: 86_400,   // 24 h — docs change infrequently
  code: 43_200,   // 12 h — code changes on develop merges
  front: 43_200,  // 12 h
  nuget: 43_200,  // 12 h — package list
} as const;

export type IndexKind = keyof typeof TTL;

// ─── Docs index entry (search-index.json) ─────────────────────────────────────

export interface IndexEntry {
  title: string;
  description: string;
  url: string;
  category: string;
  platform: string;
  content: string;
}

// ─── KV abstraction ───────────────────────────────────────────────────────────

export interface KVCache {
  get(key: string): Promise<string | null>;
  put(key: string, value: string, options?: { expirationTtl?: number }): Promise<void>;
}

// ─── Generic index fetcher ────────────────────────────────────────────────────

/**
 * Fetches a JSON index from a URL, caching it in KV with a prefixed key.
 *
 * @param kind   - Index type (determines cache key prefix and TTL)
 * @param url    - URL to fetch the index from on cache miss
 * @param cache  - KV namespace
 */
export async function getIndex<T>(kind: IndexKind, url: string, cache: KVCache): Promise<T> {
  const cacheKey = `${kind}:index`;

  // 1. Try KV cache first
  const cached = await cache.get(cacheKey);
  if (cached) return JSON.parse(cached) as T;

  // 2. Fetch from origin
  let text: string;
  try {
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`);
    }
    text = await response.text();
  } catch (err) {
    // Fail-open: if fetch fails and we have a stale cache entry, use it.
    // KV .get() above only returns non-expired entries, so check with a
    // separate stale key that has a much longer TTL.
    const stale = await cache.get(`${cacheKey}:stale`);
    if (stale) {
      console.warn(`[granit-mcp] ${kind} index fetch failed, serving stale cache: ${err}`);
      return JSON.parse(stale) as T;
    }
    throw new Error(`Failed to fetch ${kind} index from ${url}: ${err}`);
  }

  // 3. Cache fresh copy + long-lived stale fallback (7 days)
  await Promise.all([
    cache.put(cacheKey, text, { expirationTtl: TTL[kind] }),
    cache.put(`${cacheKey}:stale`, text, { expirationTtl: 604_800 }),
  ]);

  return JSON.parse(text) as T;
}

// ─── Convenience: docs search index ───────────────────────────────────────────

export async function getSearchIndex(indexUrl: string, cache: KVCache): Promise<IndexEntry[]> {
  return getIndex<IndexEntry[]>('docs', indexUrl, cache);
}
