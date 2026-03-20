import type { KVCache } from '../lib/index-cache.js';
import { getCodeIndex, getFrontIndex } from '../lib/code-index.js';

export interface ProjectGraphInput {
  repo?: 'dotnet' | 'front';
  branch?: string;
}

export async function handleProjectGraph(
  input: ProjectGraphInput,
  codeIndexUrl: string,
  frontIndexUrl: string,
  cache: KVCache,
): Promise<string> {
  const sections: string[] = [];

  if (input.repo !== 'front') {
    const codeIndex = await getCodeIndex(codeIndexUrl, cache, input.branch);
    if (codeIndex && codeIndex.projectGraph.length > 0) {
      const projects = codeIndex.projectGraph;
      const sorted = [...projects].sort((a, b) => a.name.localeCompare(b.name));
      const lines = sorted.map((p) => {
          const deps = p.deps.length > 0
            ? `→ ${p.deps.join(', ')}`
            : '*(no dependencies)*';
          return `- **${p.name}** (${p.framework}) ${deps}`;
        });

      sections.push(
        `### .NET — ${projects.length} projects\n\n` +
        lines.join('\n'),
      );
    }
  }

  if (input.repo !== 'dotnet') {
    const frontIndex = await getFrontIndex(frontIndexUrl, cache, input.branch);
    if (frontIndex && frontIndex.packages.length > 0) {
      const pkgs = frontIndex.packages;
      const sorted = [...pkgs].sort((a, b) => a.name.localeCompare(b.name));
      const lines = sorted.map((p) => {
          const desc = p.description ? ` — ${p.description}` : '';
          return `- **${p.name}**${desc}`;
        });

      sections.push(
        `### TypeScript — ${pkgs.length} packages\n\n` +
        lines.join('\n'),
      );
    }
  }

  if (sections.length === 0) {
    return 'No project graph data available. Code indexes may not be published yet.';
  }

  return `## Granit project graph\n\n${sections.join('\n\n')}`;
}
