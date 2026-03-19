/**
 * NuGet API client with KV caching.
 *
 * Uses the public NuGet v3 API — no authentication required.
 * - Search API: discover Granit.* packages
 * - Registration API: package metadata (versions, deps, frameworks)
 */

import type { KVCache } from './index-cache.js';

const NUGET_SEARCH_URL = 'https://azuresearch-usnc.nuget.org/query';
const NUGET_REGISTRATION_URL = 'https://api.nuget.org/v3/registration5-gz-semver2';

const PACKAGE_LIST_KEY = 'nuget:package-list';
const PACKAGE_LIST_TTL = 43_200; // 12 h

const PACKAGE_INFO_TTL = 21_600; // 6 h

// ─── Search API types ─────────────────────────────────────────────────────────

interface NuGetSearchResponse {
  totalHits: number;
  data: NuGetSearchPackage[];
}

interface NuGetSearchPackage {
  id: string;
  version: string;
  description: string;
  totalDownloads: number;
  iconUrl?: string;
  projectUrl?: string;
  tags: string[];
  authors: string[];
  owners: string[];
  versions: { version: string; downloads: number }[];
}

export interface PackageSummary {
  id: string;
  version: string;
  description: string;
  downloads: number;
  authors: string[];
  tags: string[];
}

// ─── Registration API types ───────────────────────────────────────────────────

interface RegistrationIndex {
  count: number;
  items: RegistrationPage[];
}

interface RegistrationPage {
  items?: RegistrationLeaf[];
  '@id': string;
  lower: string;
  upper: string;
}

interface RegistrationLeaf {
  catalogEntry: CatalogEntry;
}

interface CatalogEntry {
  id: string;
  version: string;
  description: string;
  authors: string;
  licenseExpression?: string;
  licenseUrl?: string;
  projectUrl?: string;
  tags?: string[];
  dependencyGroups?: DependencyGroup[];
  listed?: boolean;
  published?: string;
}

interface DependencyGroup {
  targetFramework: string;
  dependencies?: { id: string; range: string }[];
}

export interface PackageVersion {
  version: string;
  published?: string;
  listed: boolean;
}

export interface PackageInfo {
  id: string;
  latestVersion: string;
  description: string;
  authors: string;
  license?: string;
  projectUrl?: string;
  tags: string[];
  versions: PackageVersion[];
  dependencyGroups: {
    framework: string;
    dependencies: { id: string; range: string }[];
  }[];
}

// ─── list_packages ────────────────────────────────────────────────────────────

export async function listGranitPackages(cache: KVCache): Promise<PackageSummary[]> {
  const cached = await cache.get(PACKAGE_LIST_KEY);
  if (cached) return JSON.parse(cached) as PackageSummary[];

  const url = `${NUGET_SEARCH_URL}?q=owner:granit-fx&take=50&prerelease=false`;
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`NuGet search failed: ${response.status} ${response.statusText}`);
  }

  const data = (await response.json()) as NuGetSearchResponse;

  const packages: PackageSummary[] = data.data.map((pkg) => ({
    id: pkg.id,
    version: pkg.version,
    description: pkg.description,
    downloads: pkg.totalDownloads,
    authors: pkg.authors,
    tags: pkg.tags,
  }));

  await cache.put(PACKAGE_LIST_KEY, JSON.stringify(packages), { expirationTtl: PACKAGE_LIST_TTL });
  return packages;
}

// ─── get_package_info ─────────────────────────────────────────────────────────

export async function getPackageInfo(packageId: string, cache: KVCache): Promise<PackageInfo | null> {
  const cacheKey = `nuget:pkg:${packageId.toLowerCase()}`;

  const cached = await cache.get(cacheKey);
  if (cached) return JSON.parse(cached) as PackageInfo;

  const url = `${NUGET_REGISTRATION_URL}/${packageId.toLowerCase()}/index.json`;
  const response = await fetch(url, {
    headers: { Accept: 'application/json' },
  });

  if (response.status === 404) return null;
  if (!response.ok) {
    throw new Error(`NuGet registration failed for ${packageId}: ${response.status} ${response.statusText}`);
  }

  const index = (await response.json()) as RegistrationIndex;

  // Collect all catalog entries across pages
  const entries: CatalogEntry[] = [];
  for (const page of index.items) {
    if (page.items) {
      // Inlined items
      entries.push(...page.items.map((leaf) => leaf.catalogEntry));
    } else {
      // Need to fetch the page
      const pageResponse = await fetch(page['@id'], {
        headers: { Accept: 'application/json' },
      });
      if (pageResponse.ok) {
        const pageData = (await pageResponse.json()) as RegistrationPage;
        if (pageData.items) {
          entries.push(...pageData.items.map((leaf) => leaf.catalogEntry));
        }
      }
    }
  }

  if (entries.length === 0) return null;

  const latest = entries.at(-1)!;

  const info: PackageInfo = {
    id: latest.id,
    latestVersion: latest.version,
    description: latest.description,
    authors: latest.authors,
    license: latest.licenseExpression ?? latest.licenseUrl,
    projectUrl: latest.projectUrl,
    tags: latest.tags ?? [],
    versions: entries.map((e) => ({
      version: e.version,
      published: e.published,
      listed: e.listed !== false,
    })),
    dependencyGroups: (latest.dependencyGroups ?? []).map((g) => ({
      framework: g.targetFramework,
      dependencies: (g.dependencies ?? []).map((d) => ({ id: d.id, range: d.range })),
    })),
  };

  await cache.put(cacheKey, JSON.stringify(info), { expirationTtl: PACKAGE_INFO_TTL });
  return info;
}
