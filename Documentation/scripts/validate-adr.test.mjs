import assert from 'node:assert/strict';
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { validateAdrs, validateFrontmatter } from './validate-adr.mjs';

for (const [name, source, expected] of [
  ['missing frontmatter', '# ADR\n', /missing YAML frontmatter/],
  ['unclosed frontmatter', '---\ntitle: ADR', /closing ---/],
  ['missing title', '---\nstatus: Proposed\n---', /missing required title/],
  ['empty title', "---\ntitle: ''\n---", /empty or whitespace-only/],
  ['whitespace title', "---\ntitle: '   '\n---", /empty or whitespace-only/],
  ['null title', '---\ntitle:\n---', /must be a string/],
  ['numeric title', '---\ntitle: 59\n---', /must be a string/],
  ['boolean title', '---\ntitle: true\n---', /must be a string/],
  ['array title', '---\ntitle: [ADR]\n---', /must be a string/],
  ['mapping title', '---\ntitle: {name: ADR}\n---', /must be a string/],
  ['non-mapping document', '---\n- ADR\n---', /must be a mapping/],
  ['invalid YAML', '---\ntitle: [broken\n---', /invalid YAML/],
  ['duplicate title', '---\ntitle: One\ntitle: Two\n---', /invalid YAML/],
]) {
  test(name, () => assert.match(validateFrontmatter(source), expected));
}

for (const title of ["'ADR: quoted title'", 'Plain title # comment', '|\n  Multiline\n  title', '>\n  Folded\n  title', '"Escaped \\"title\\""']) {
  test(`valid YAML title: ${title}`, () => {
    assert.equal(validateFrontmatter(`---\ntitle: ${title}\n---\n## Status\nProposed`), undefined);
  });
}

test('valid BOM and CRLF frontmatter', () => {
  assert.equal(validateFrontmatter('\uFEFF---\r\ntitle: ADR\r\n---\r\n'), undefined);
});

test('all repository ADRs, including ADR-0059 and ADR-0060, pass', async () => {
  assert.ok(await validateAdrs() >= 60);
});

test('recursive validation reports each file and its specific problem', async (t) => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'sobakasu-adr-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  await mkdir(path.join(directory, 'nested'));
  await writeFile(path.join(directory, 'valid.md'), '---\ntitle: Valid\n---');
  await writeFile(path.join(directory, 'missing.md'), '---\nstatus: Proposed\n---');
  await writeFile(path.join(directory, 'nested', 'absent.md'), '# ADR');
  await assert.rejects(validateAdrs(directory), (error) => {
    assert.ok(error.message.includes(`${path.join(directory, 'missing.md')}: missing required title.`));
    assert.ok(error.message.includes(`${path.join(directory, 'nested', 'absent.md')}: missing YAML frontmatter`));
    assert.ok(!error.message.includes('valid.md:'));
    return true;
  });
});
