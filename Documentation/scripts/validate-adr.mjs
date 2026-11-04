import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import yaml from 'js-yaml';

export const adrDirectory = fileURLToPath(new URL('../src/content/docs/ja/adr/', import.meta.url));

export function validateFrontmatter(source) {
  const lines = source.replace(/^\uFEFF/, '').split(/\r?\n/);
  if (lines[0] !== '---') return 'missing YAML frontmatter (expected opening ---).';
  const end = lines.findIndex((line, index) => index > 0 && line === '---');
  if (end < 0) return 'YAML frontmatter is missing its closing ---.';

  let data;
  try {
    data = yaml.load(lines.slice(1, end).join('\n'));
  } catch (error) {
    return `invalid YAML frontmatter: ${error.message}`;
  }
  if (data === null || typeof data !== 'object' || Array.isArray(data)) {
    return 'YAML frontmatter must be a mapping containing title.';
  }
  if (!Object.hasOwn(data, 'title')) return 'missing required title.';
  if (typeof data.title !== 'string') return 'title must be a string.';
  if (data.title.trim().length === 0) return 'title must not be empty or whitespace-only.';
  return undefined;
}

export async function validateAdrs(directory = adrDirectory) {
  const errors = [];
  let count = 0;
  async function visit(current) {
    const entries = await readdir(current, { withFileTypes: true });
    for (const entry of entries.sort((a, b) => a.name.localeCompare(b.name))) {
      const file = path.join(current, entry.name);
      if (entry.isDirectory()) {
        await visit(file);
      } else if (entry.isFile() && /\.md$/i.test(entry.name)) {
        count++;
        const problem = validateFrontmatter(await readFile(file, 'utf8'));
        if (problem) errors.push(`${file}: ${problem}`);
      }
    }
  }
  await visit(directory);
  if (errors.length > 0) throw new Error(`ADR validation failed:\n${errors.join('\n')}`);
  return count;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  console.log(`Validated ${await validateAdrs()} ADR Markdown files.`);
}
