const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test, before } = require('node:test');
const tm = require('vscode-textmate');
const onig = require('vscode-oniguruma');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
let grammar, uasm;
before(async () => {
  await onig.loadWASM(fs.readFileSync(require.resolve('vscode-oniguruma/release/onig.wasm')));
  const registry = new tm.Registry({
    onigLib: Promise.resolve({ createOnigScanner: patterns => new onig.OnigScanner(patterns), createOnigString: text => new onig.OnigString(text) }),
    loadGrammar: async scope => tm.parseRawGrammar(read(`syntaxes/${scope === 'source.uasm' ? 'uasm' : 'sobakasu'}.tmLanguage.json`), 'grammar.json')
  });
  grammar = await registry.loadGrammar('source.sobakasu');
  uasm = await registry.loadGrammar('source.uasm');
});

function tokenize(source, language = grammar) {
  let stack = tm.INITIAL;
  return source.split('\n').flatMap(line => {
    const result = language.tokenizeLine(line, stack);
    stack = result.ruleStack;
    return result.tokens.map(t => ({ text: line.slice(t.startIndex, t.endIndex), scopes: t.scopes }));
  });
}
function has(source, text, scope, language) {
  const tokens = tokenize(source, language);
  assert.ok(tokens.some(t => t.text === text && t.scopes.some(s => s.startsWith(scope))), JSON.stringify({ text, scope, tokens }));
}
function noKeyword(source, text) {
  const tokens = tokenize(source).filter(t => t.text === text);
  assert.ok(tokens.length, `Missing whole identifier ${text}`);
  assert.ok(tokens.every(t => !t.scopes.some(s => /^(keyword|storage|invalid)\./.test(s))), JSON.stringify(tokens));
}

test('current declarations, state, behavior roots and separators', () => {
  const source = 'type Foo = extern Runtime.Foo;\nbehavior {\n on interact(state) { behavior::event(); state.target; }\n}\nstate { public target: Foo = field; }';
  for (const word of ['type', 'behavior', 'field']) has(source, word, 'keyword.declaration');
  has(source, 'Foo', 'entity.name.type');
  has(source, '::', 'punctuation.separator.path');
  has('Maybe::Just(value)', '::', 'punctuation.separator.path');
  has(source, ':', 'punctuation.separator.type');
  has('module 日本語;', '日本語', 'entity.name.namespace');
  has('implementation<T> Foo<T> { function get(self) {} }', 'implementation', 'keyword.declaration');
});

test('maybe is contextual only in extern bindings and nullable out parameters', () => {
  has('function foo() -> Maybe<Foo>\n = maybe extern Runtime.Foo();', 'maybe', 'keyword.declaration.contextual');
  has('function foo() -> (bool, Maybe<Foo>)\n = extern Runtime.Foo(maybe out Foo value);', 'maybe', 'keyword.declaration.contextual');
  has('function foo() -> Maybe<Foo>\n =\n maybe /* comment */\n extern Runtime.Foo();', 'maybe', 'keyword.declaration.contextual');
  noKeyword('function foo() { let maybe = 1; maybe(); }', 'maybe');
  noKeyword('function foo(maybe: i32) { }', 'maybe');
  noKeyword('function foo() = extern Runtime.Foo(maybe);', 'maybe');
  noKeyword('function foo() = extern Runtime.Foo(maybe ref Foo value);', 'maybe');
  noKeyword('function foo() { maybe out Foo value; }', 'maybe');
});

test('Unicode and quoted names stay whole in every identifier pattern', () => {
  for (const name of ['日本語', 'café', 'cafe\u0301', '_名前', '\u{105C0}name', '\u{31350}name', 'a\u{E0100}', 'a\u200C', 'a\u200D', 'カ・ナ', 'ｶ･ﾅ']) {
    has(`function ${name}() {}`, name, 'entity.name.function');
    has(`struct ${name} { x: f32, }`, name, 'entity.name.type');
    has(`on ${name}(state) {}`, name, 'entity.name.function.event');
    has(`receive ${name}(state) {}`, name, 'entity.name.function.event');
    has(`${name}()`, name, 'support.function');
    has(`'${name}: loop { break '${name}; }`, `'${name}`, 'entity.name.label');
    noKeyword(`let ${name} = 1;`, name);
  }
  has('function `名前に空白を含めてもよい例` {}', '`名前に空白を含めてもよい例`', 'entity.name.function');
  has('let x = `任意の名前`;', '任意の名前', 'entity.name.quoted');
  has("let x = 'あ';", "'あ'", 'string.quoted.single');
  for (const name of ['function日本語', 'if\u0301', 'true日本語', 'sync日本語', 'i32日本語']) noKeyword(`let ${name} = 1;`, name);
});

test('all compiler Unicode 16 XID characters fit the grammar identifier classes', () => {
  const facts = read('../../Editor/Compiler/Syntax/SobakasuIdentifierFacts.cs');
  const regex = JSON.parse(read('syntaxes/sobakasu.tmLanguage.json')).repository.identifiers.patterns[0].match;
  const scanner = new onig.OnigScanner(['\\A' + regex + '\\z']);
  const missing = [];
  for (const kind of ['Start', 'Continue']) {
    const encoded = facts.match(new RegExp(`Xid${kind}Ranges = DecodeRanges\\(\\s*"([^"]+)"`))[1];
    const data = Buffer.from(encoded, 'base64');
    for (let i = 0; i < data.length; i += 6) {
      for (let cp = data.readUIntBE(i, 3); cp <= data.readUIntBE(i + 3, 3); cp++) {
        const value = (kind === 'Continue' ? 'a' : '') + String.fromCodePoint(cp);
        if (!scanner.findNextMatchSync(value, 0)) missing.push(`${kind} U+${cp.toString(16)}`);
      }
    }
  }
  assert.deepEqual(missing, []);
});

test('hard keyword vocabulary is covered and contextual/legacy words stay identifiers', () => {
  const lexer = read('../../Editor/Compiler/Lexer/SobakasuLexer.cs');
  const words = [...lexer.matchAll(/"(\w+)"\s*=>\s*new SyntaxToken\(SyntaxKind\.(?:\w*Keyword|On),/g)].map(m => m[1]);
  assert.ok(words.length >= 40, 'Lexer keyword extraction must not silently stop working');
  for (const word of words) {
    const tokens = tokenize(word);
    assert.ok(tokens.some(t => t.text === word && t.scopes.some(s => /^(keyword|storage|variable\.language|constant\.language|invalid\.deprecated)\./.test(s))), word);
  }
  for (const word of ['maybe', 'none', 'linear', 'smooth', 'fn', 'impl', 'mut', 'pub', 'mod', 'lang']) {
    assert.ok(!words.includes(word), `${word} became a hard keyword: review contextual/legacy handling`);
    noKeyword(`let ${word} = 1;`, word);
  }
  has('static function foo() {}', 'static', 'invalid.deprecated');
  for (const mode of ['none', 'linear', 'smooth']) has(`state { sync(${mode}) value = 0; }`, mode, 'storage.modifier');
});

test('comments and strings shield keyword-like contents; operator declarations still work', () => {
  has('// type maybe field', '// type maybe field', 'comment.line');
  const nested = tokenize('/* outer /* inner */ type Foo */\ntype Bar = extern Runtime.Bar;');
  assert.ok(nested.some(t => t.text.includes('type Foo') && t.scopes.includes('comment.block.sobakasu')));
  has('/* outer /* inner */ type Foo */\ntype Bar = extern Runtime.Bar;', 'Bar', 'entity.name.type');
  has('"behavior::event()"', 'behavior::event()', 'string.quoted.double');
  has('function @+(self, other: Self) -> Self {}', '@+', 'entity.name.function');
  has('function try_get?() {}', 'try_get?', 'entity.name.function');
});

test('representative current language constructs', () => {
  const source = [
    'public module 日本語;',
    'use core::{string, self as core_alias};',
    'language item "example" struct 座標 { x: f32, }',
    'implementation<T> 座標 { function set(mutable self) {} }',
    'const LIMIT: i32 = 10;',
    'behavior {',
    ' public receive changed(state, value: i32) {}',
    ' on interact(state) {',
    '  let mutable value = Maybe::Just(1);',
    "  '繰り返し: loop { continue '繰り返し; }",
    '  match value { Maybe::Just(x) => x, Maybe::Nothing => 0, };',
    '  send behavior::changed(state, state.count) to others;',
    ' }',
    '}'
  ].join('\n');
  for (const word of ['public', 'module', 'use', 'as', 'language', 'item', 'implementation', 'const', 'mutable', 'state']) has(source, word, 'keyword.declaration');
  for (const word of ['loop', 'continue', 'match']) has(source, word, 'keyword.control');
  for (const word of ['send', 'to']) has(source, word, 'keyword.other');
  has(source, 'self', 'variable.language');
  has('function copy(self) -> Self {}', 'Self', 'variable.language');
  has(source, "'繰り返し", 'entity.name.label');
});

test('manifest JSON, contributed paths, editor pairs and README example', () => {
  const pkg = JSON.parse(read('package.json'));
  for (const language of pkg.contributes.languages) JSON.parse(read(language.configuration));
  for (const language of pkg.contributes.grammars) assert.equal(JSON.parse(read(language.path)).scopeName, language.scopeName);
  const config = JSON.parse(read('language-configuration.json'));
  for (const key of ['autoClosingPairs', 'surroundingPairs']) {
    for (const pair of [['`', '`'], ['"', '"'], ["'", "'"], ['(', ')'], ['[', ']'], ['{', '}']]) assert.ok(config[key].some(p => p[0] === pair[0] && p[1] === pair[1]));
  }
  const sample = read('README.md').match(/```sobakasu\r?\n([\s\S]*?)```/)[1];
  has(sample, 'state', 'keyword.declaration');
  has(sample, 'behavior', 'keyword.declaration');
  has(sample, 'interact', 'entity.name.function.event');
});

test('assembler output vocabulary remains recognized by UASM grammar', () => {
  const assembler = read('../../Editor/Compiler/UasmAssembler/SobakasuUasmAssembler.cs');
  const directives = [...new Set([...assembler.matchAll(/\.(data_start|data_end|code_start|code_end|export|sync)\b/g)].map(m => '.' + m[1]))];
  for (const directive of directives) has(directive, directive, 'keyword', uasm);
  const opcodes = [...new Set([...assembler.matchAll(/(?:WriteIndentedLine\(\$?"|\$"|^\s*")([A-Z_]+)(?:,|"(?=, builder))/gm)].map(m => m[1]))];
  assert.ok(opcodes.includes('JUMP_IF_FALSE'));
  for (const opcode of opcodes) has(opcode, opcode, 'keyword.control.instruction', uasm);
  has('.data_start\n    __sbk_q_E5908D: %SystemInt32, null\n.data_end', '__sbk_q_E5908D', 'variable.other.data-slot', uasm);
  has('.code_start\n    entry_0:\n    JUMP, entry_0\n.code_end', 'entry_0', 'entity.name.label', uasm);
});
