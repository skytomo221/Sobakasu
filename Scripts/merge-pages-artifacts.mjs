import { cp, mkdir, readdir, readFile, stat } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';

const [documentationOutput, vpmListingOutput, pagesOutput] = process.argv.slice(2);

if (!documentationOutput || !vpmListingOutput || !pagesOutput || process.argv.length !== 5) {
  console.error('Usage: node Scripts/merge-pages-artifacts.mjs <documentation-output> <vpm-listing-output> <pages-output>');
  process.exit(64);
}

async function requireDirectory(directory, label) {
  try {
    if (!(await stat(directory)).isDirectory()) {
      throw new Error();
    }
  } catch {
    throw new Error(`${label} does not exist: ${directory}`);
  }
}

async function requireFile(file, label) {
  try {
    if (!(await stat(file)).isFile()) {
      throw new Error();
    }
  } catch {
    throw new Error(`${label} is missing: ${file}`);
  }
}

async function filePaths(directory, relativeDirectory = '') {
  const entries = await readdir(directory, { withFileTypes: true });
  const paths = [];
  for (const entry of entries) {
    const relativePath = path.join(relativeDirectory, entry.name);
    if (entry.isDirectory()) {
      paths.push(...await filePaths(path.join(directory, entry.name), relativePath));
    } else if (entry.isFile()) {
      paths.push(relativePath);
    }
  }
  return paths;
}

function sha256(contents) {
  return createHash('sha256').update(contents).digest('hex');
}

await requireDirectory(documentationOutput, 'Documentation output');
await requireDirectory(vpmListingOutput, 'VPM listing output');
await requireFile(path.join(documentationOutput, 'index.html'), 'Documentation root index.html');
await requireFile(path.join(vpmListingOutput, 'index.json'), 'VPM listing index.json');

try {
  await stat(pagesOutput);
  throw new Error(`Pages output must not already exist: ${pagesOutput}`);
} catch (error) {
  if (error.code !== 'ENOENT') {
    throw error;
  }
}

// package-list-action renders a browser UI as index.html/app.js in addition to
// the machine-readable listing. The VPM client only consumes index.json; never
// allow its UI files to replace Astro's root page.
const allowedVpmFiles = new Set(['index.json', 'index.html', 'app.js']);
const unexpectedVpmFiles = (await filePaths(vpmListingOutput))
  .map((file) => file.split(path.sep).join('/'))
  .filter((file) => !allowedVpmFiles.has(file));
if (unexpectedVpmFiles.length > 0) {
  throw new Error(`Unexpected VPM listing output; update this merge contract intentionally:\n${unexpectedVpmFiles.join('\n')}`);
}

const listing = JSON.parse(await readFile(path.join(vpmListingOutput, 'index.json'), 'utf8'));
if (listing.url !== 'https://skytomo221.com/Sobakasu/index.json' || typeof listing.packages !== 'object') {
  throw new Error('VPM index.json does not have the expected repository-listing shape.');
}

const documentationIndex = await readFile(path.join(documentationOutput, 'index.html'));
await mkdir(pagesOutput);
await cp(documentationOutput, pagesOutput, { recursive: true, errorOnExist: true, force: false });
await cp(path.join(vpmListingOutput, 'index.json'), path.join(pagesOutput, 'index.json'), { errorOnExist: true, force: false });

const mergedIndex = await readFile(path.join(pagesOutput, 'index.html'));
if (sha256(documentationIndex) !== sha256(mergedIndex)) {
  throw new Error("VPM output replaced Astro's root index.html.");
}

const mergedListing = await readFile(path.join(pagesOutput, 'index.json'));
if (sha256(await readFile(path.join(vpmListingOutput, 'index.json'))) !== sha256(mergedListing)) {
  throw new Error('VPM index.json was not copied unchanged.');
}
