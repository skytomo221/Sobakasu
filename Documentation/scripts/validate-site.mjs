import { readdir, readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const site = 'https://skytomo221.com';
const base = '/Sobakasu';
const locales = ['ja', 'en', 'ko', 'zh-cn'];
const categoryTargets = [
  'guide/getting-started/',
  'language/arrays/',
  'reference/standard-library/',
  'samples/arrays/',
];
const publishedVpmRoutes = new Set(['/Sobakasu/index.json']);
const dist = new URL('../dist/', import.meta.url);
const distPath = fileURLToPath(dist);

async function files(directory, relative = '') {
  const entries = await readdir(directory, { withFileTypes: true });
  const result = [];
  for (const entry of entries) {
    const relativePath = path.join(relative, entry.name);
    if (entry.isDirectory()) {
      result.push(...await files(path.join(directory, entry.name), relativePath));
    } else if (entry.isFile()) {
      result.push(relativePath);
    }
  }
  return result;
}

function toRoute(file) {
  const normalized = file.split(path.sep).join('/');
  if (normalized.endsWith('/index.html')) {
    return `${base}/${normalized.slice(0, -'index.html'.length)}`;
  }
  if (normalized === 'index.html') return `${base}/`;
  if (normalized.endsWith('.html')) return `${base}/${normalized.slice(0, -'.html'.length)}/`;
  return `${base}/${normalized}`;
}

function routeKey(pathname) {
  if (pathname === base || pathname.endsWith('/')) return `${pathname}/`.replace(/\/+/g, '/');
  if (path.extname(pathname)) return pathname;
  return `${pathname}/`;
}

function localeFor(pathname) {
  const segments = pathname.slice(`${base}/`.length).split('/');
  return locales.includes(segments[0]) ? segments[0] : undefined;
}

const outputFiles = await files(distPath);
const routes = new Set(outputFiles.map(toRoute).map(routeKey));
const htmlFiles = outputFiles.filter((file) => file.endsWith('.html'));
const errors = [];
const anchorsByRoute = new Map();

for (const file of htmlFiles) {
  const route = toRoute(file);
  const sourceLocale = localeFor(route);
  const html = await readFile(new URL(file.split(path.sep).join('/'), dist), 'utf8');
  const anchors = [];
  for (const match of html.matchAll(/<a\b[^>]*\bhref=(["'])(.*?)\1/gi)) {
    const href = match[2];
    const target = new URL(href, `${site}${route}`);
    if (target.origin !== site || href.startsWith('#')) continue;
    const pathname = routeKey(target.pathname);
    anchors.push(pathname);
    if (!pathname.startsWith(`${base}/`) && pathname !== `${base}/`) {
      errors.push(`${route}: internal link escapes ${base}: ${href}`);
      continue;
    }
    if (!routes.has(pathname) && !publishedVpmRoutes.has(pathname)) {
      errors.push(`${route}: internal link does not target generated output: ${href}`);
      continue;
    }
    const targetLocale = localeFor(pathname);
    const relativeToBase = pathname.slice(`${base}/`.length);
    if (relativeToBase && !targetLocale && !publishedVpmRoutes.has(pathname)) {
      errors.push(`${route}: internal link is missing a locale prefix: ${href}`);
    }
    if (sourceLocale && targetLocale && sourceLocale !== targetLocale && /^(guide|language|reference|samples)\//.test(relativeToBase.slice(targetLocale.length + 1))) {
      errors.push(`${route}: category link changed locale from ${sourceLocale} to ${targetLocale}: ${href}`);
    }
  }
  anchorsByRoute.set(routeKey(route), anchors);
}

for (const locale of locales) {
  const home = `${base}/${locale}/`;
  const anchors = anchorsByRoute.get(home) ?? [];
  for (const target of categoryTargets) {
    const expected = `${base}/${locale}/${target}`;
    if (!anchors.includes(expected)) {
      errors.push(`${home}: missing locale-aware link to ${expected}`);
    }
  }
}

const root = await readFile(new URL('index.html', dist), 'utf8');
if (!root.includes('url=/Sobakasu/ja/')) {
  errors.push('root redirect does not target /Sobakasu/ja/.');
}

for (const file of htmlFiles) {
  const html = await readFile(new URL(file.split(path.sep).join('/'), dist), 'utf8');
  if (html.includes('https://skytomo221.github.io')) {
    errors.push(`${file}: contains the retired github.io host.`);
  }
  if (!html.includes('https://skytomo221.com/Sobakasu/')) {
    errors.push(`${file}: does not contain the canonical site URL.`);
  }
}

try {
  await stat(new URL('favicon.svg', dist));
} catch {
  errors.push('favicon.svg is missing from the production output.');
}

if (errors.length > 0) {
  throw new Error(`Documentation validation failed:\n${errors.join('\n')}`);
}

console.log(`Validated ${htmlFiles.length} HTML files and ${routes.size} generated routes.`);
