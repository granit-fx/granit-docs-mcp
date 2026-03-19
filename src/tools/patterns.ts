import type { KVCache } from '../lib/index-cache.js';
import { getSearchIndex } from '../lib/index-cache.js';

const DOCS_BASE = 'https://granit-fx.dev';

export async function handleListPatterns(indexUrl: string, cache: KVCache): Promise<string> {
  const entries = await getSearchIndex(indexUrl, cache);
  const patterns = entries.filter((e) => e.category === 'pattern');

  if (patterns.length === 0) {
    return 'No architecture patterns found in the documentation.';
  }

  // Group by platform
  const dotnet = patterns.filter((p) => p.platform === 'dotnet');
  const frontend = patterns.filter((p) => p.platform === 'frontend');
  const other = patterns.filter((p) => p.platform !== 'dotnet' && p.platform !== 'frontend');

  const sections: string[] = [];

  if (dotnet.length > 0) {
    sections.push(
      `### Backend (.NET) — ${dotnet.length} patterns\n\n` +
      formatPatternList(dotnet),
    );
  }

  if (frontend.length > 0) {
    sections.push(
      `### Frontend (TypeScript) — ${frontend.length} patterns\n\n` +
      formatPatternList(frontend),
    );
  }

  if (other.length > 0) {
    sections.push(
      `### Other — ${other.length} patterns\n\n` +
      formatPatternList(other),
    );
  }

  return `## Granit architecture patterns (${patterns.length} total)\n\n${sections.join('\n\n')}`;
}

function formatPatternList(patterns: { title: string; url: string; description: string }[]): string {
  return patterns
    .map((p) => {
      const desc = p.description ? ` — ${p.description}` : '';
      return `- [${p.title}](${DOCS_BASE}${p.url})${desc}`;
    })
    .join('\n');
}
