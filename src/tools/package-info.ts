import type { KVCache } from '../lib/index-cache.js';
import { getPackageInfo } from '../lib/nuget.js';

export interface PackageInfoInput {
  package: string;
  version?: string;
}

export async function handlePackageInfo(input: PackageInfoInput, cache: KVCache): Promise<string> {
  const info = await getPackageInfo(input.package, cache);

  if (!info) {
    return (
      `Package "${input.package}" not found on NuGet.\n\n` +
      'Tip: use `list_packages` to see all available Granit packages.'
    );
  }

  // If a specific version was requested, check it exists
  if (input.version) {
    const match = info.versions.find((v) => v.version === input.version);
    if (!match) {
      const available = info.versions
        .filter((v) => v.listed)
        .slice(-10)
        .map((v) => v.version)
        .join(', ');
      return `Version "${input.version}" not found for ${info.id}.\n\n**Recent versions:** ${available}`;
    }
  }

  const displayVersion = input.version ?? info.latestVersion;

  const lines: string[] = [
    `## ${info.id} v${displayVersion}`,
    '',
    info.description ? `> ${info.description}` : '',
    '',
  ];

  // Metadata
  lines.push(`**Authors:** ${info.authors}`);
  if (info.license) lines.push(`**License:** ${info.license}`);
  if (info.projectUrl) lines.push(`**Project:** ${info.projectUrl}`);
  if (info.tags.length > 0) lines.push(`**Tags:** ${info.tags.join(', ')}`);
  lines.push('');

  // Dependency groups (for the latest version)
  if (info.dependencyGroups.length > 0) {
    lines.push('### Dependencies');
    lines.push('');
    for (const group of info.dependencyGroups) {
      lines.push(`**${group.framework}**`);
      if (group.dependencies.length === 0) {
        lines.push('- *(none)*');
      } else {
        for (const dep of group.dependencies) {
          lines.push(`- ${dep.id} ${dep.range}`);
        }
      }
      lines.push('');
    }
  }

  // Version history (last 10 listed)
  const listedVersions = info.versions.filter((v) => v.listed);
  const recentVersions = listedVersions.slice(-10).reverse();
  if (recentVersions.length > 0) {
    lines.push('### Recent versions');
    lines.push('');
    for (const v of recentVersions) {
      const date = v.published ? ` (${v.published.split('T')[0]})` : '';
      lines.push(`- v${v.version}${date}`);
    }
    if (listedVersions.length > 10) {
      lines.push(`- *… and ${listedVersions.length - 10} earlier versions*`);
    }
  }

  return lines.filter((l) => l !== undefined).join('\n');
}
