import type { KVCache } from '../lib/index-cache.js';
import { listGranitPackages } from '../lib/nuget.js';

export async function handleListPackages(cache: KVCache): Promise<string> {
  const packages = await listGranitPackages(cache);

  if (packages.length === 0) {
    return 'No Granit packages found on NuGet.';
  }

  const sorted = [...packages].sort((a, b) => a.id.localeCompare(b.id));
  const rows = sorted
    .map((pkg) => {
      const dl = pkg.downloads >= 1000 ? `${(pkg.downloads / 1000).toFixed(1)}k` : String(pkg.downloads);
      return `- **${pkg.id}** v${pkg.version} — ${pkg.description || 'No description'} (${dl} downloads)`;
    })
    .join('\n');

  return `## Granit NuGet packages (${packages.length})\n\n${rows}`;
}
