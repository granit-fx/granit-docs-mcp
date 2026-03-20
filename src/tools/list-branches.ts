/**
 * Lists branches that have a committed code/front index file.
 * Uses the public GitHub API (no auth required for public repos).
 */

const REPOS = {
  dotnet: { owner: 'granit-fx', repo: 'granit-dotnet', indexFile: 'code-index.json' },
  front: { owner: 'granit-fx', repo: 'granit-front', indexFile: 'front-index.json' },
} as const;

type RepoKey = keyof typeof REPOS;

export interface ListBranchesInput {
  repo?: 'dotnet' | 'front';
}

interface BranchInfo {
  name: string;
  hasIndex: boolean;
}

interface RepoBranches {
  repo: string;
  indexFile: string;
  branches: BranchInfo[];
}

export async function handleListBranches(input: ListBranchesInput): Promise<string> {
  const keys: RepoKey[] = input.repo ? [input.repo] : ['dotnet', 'front'];

  const results = await Promise.all(keys.map((key) => listRepoBranches(key)));

  const sections = results.map((r) => {
    const available = r.branches.filter((b) => b.hasIndex);
    if (available.length === 0) {
      return `### ${r.repo}\nNo branches have \`${r.indexFile}\` committed yet.`;
    }
    const list = available.map((b) => `- \`${b.name}\``).join('\n');
    return `### ${r.repo}\n${list}`;
  });

  return `## Available index branches\n\n${sections.join('\n\n')}`;
}

async function listRepoBranches(key: RepoKey): Promise<RepoBranches> {
  const { owner, repo, indexFile } = REPOS[key];

  // Fetch branch list from GitHub API (public, no auth needed)
  const branchesUrl = `https://api.github.com/repos/${owner}/${repo}/branches?per_page=100`;
  const res = await fetch(branchesUrl, {
    headers: { 'User-Agent': 'granit-mcp', Accept: 'application/vnd.github+json' },
  });

  if (!res.ok) {
    return { repo, indexFile, branches: [] };
  }

  const branches = (await res.json()) as { name: string }[];

  // Check which branches have the index file (parallel HEAD requests)
  const checks = await Promise.all(
    branches.map(async (b): Promise<BranchInfo> => {
      const fileUrl = `https://api.github.com/repos/${owner}/${repo}/contents/${indexFile}?ref=${b.name}`;
      const fileRes = await fetch(fileUrl, {
        method: 'HEAD',
        headers: { 'User-Agent': 'granit-mcp', Accept: 'application/vnd.github+json' },
      });
      return { name: b.name, hasIndex: fileRes.ok };
    }),
  );

  return { repo, indexFile, branches: checks };
}
