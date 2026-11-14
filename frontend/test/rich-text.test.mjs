import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';

function loadModule(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
  );
  return exports;
}

function walk(node, match) {
  if (!node || typeof node !== 'object') return [];
  if (Array.isArray(node)) return node.flatMap((child) => walk(child, match));
  const self = React.isValidElement(node) && match(node) ? [node] : [];
  return [...self, ...walk(node.props?.children, match)];
}

function collectText(node) {
  if (node == null || typeof node === 'boolean') return '';
  if (typeof node === 'string' || typeof node === 'number') return String(node);
  if (Array.isArray(node)) return node.map(collectText).join('');
  if (React.isValidElement(node)) return collectText(node.props?.children);
  return '';
}

const { RichTextContent, stripRichText } = loadModule('components/ui/rich-text-content.tsx', {
  react: React,
  'react/jsx-runtime': jsxRuntime,
  '@/lib/utils': { cn: (...parts) => parts.flat().filter(Boolean).join(' ') },
});

const { normalizeRichTextMarkdown, normalizeSafeLinkUrl } = loadModule(
  'components/ui/rich-text-editor.tsx',
  {
    react: React,
    'react/jsx-runtime': jsxRuntime,
    '@tiptap/react': { EditorContent: 'EditorContent', useEditor: () => null, useEditorState: () => ({}) },
    '@tiptap/starter-kit': { default: { configure: () => ({}) } },
    '@tiptap/extensions': {
      CharacterCount: { configure: () => ({}) },
      Placeholder: { configure: () => ({}) },
    },
    '@tiptap/markdown': { Markdown: {} },
    'lucide-react': new Proxy({}, { get: (_, name) => String(name) }),
    '@/lib/utils': { cn: (...parts) => parts.flat().filter(Boolean).join(' ') },
    './button': { Button: 'Button' },
    './dialog': { Dialog: 'Dialog' },
  },
);

test('RichTextContent handles empty values, preserves line breaks, and never renders raw HTML scripts', () => {
  assert.equal(collectText(RichTextContent({ content: null })), '');
  assert.equal(collectText(RichTextContent({ content: '   ' })), '');

  const note = 'Dòng một\n<script>Ví dụ nội dung</script>';
  const tree = RichTextContent({ content: note });
  const paragraph = walk(tree, (node) => node.type === 'p')[0];
  assert.ok(paragraph);
  assert.match(String(paragraph.props.className), /whitespace-pre-wrap/);
  assert.equal(collectText(paragraph), note);
  assert.equal(walk(tree, (node) => node.type === 'script').length, 0);
});

test('RichTextContent renders bold, italic, underline, lists, quotes, and safe links as React elements', () => {
  const markdown = [
    '**Nhận xét** *chi tiết* và <u>quan trọng</u>',
    '',
    '- Mục 1 với [tài liệu](https://example.com/doc)',
    '- Không cho [xss](javascript:alert(1))',
    '',
    '1. Bước một',
    '2. Bước hai',
    '',
    '> Lưu ý chuẩn đầu ra',
  ].join('\n');

  const tree = RichTextContent({ content: markdown });
  assert.equal(collectText(walk(tree, (node) => node.type === 'strong')[0]), 'Nhận xét');
  assert.equal(collectText(walk(tree, (node) => node.type === 'em')[0]), 'chi tiết');
  assert.equal(collectText(walk(tree, (node) => node.type === 'u')[0]), 'quan trọng');
  assert.equal(walk(tree, (node) => node.type === 'ul').length, 1);
  assert.equal(walk(tree, (node) => node.type === 'ol').length, 1);
  assert.equal(walk(tree, (node) => node.type === 'blockquote').length, 1);

  const links = walk(tree, (node) => node.type === 'a');
  assert.equal(links.length, 1);
  assert.equal(links[0].props.href, 'https://example.com/doc');
  assert.match(collectText(tree), /\[xss\]\(javascript:alert\(1\)\)/);
});

test('stripRichText strips markdown delimiters cleanly for table tooltips and leaves identifiers intact', () => {
  const sample = '**Cần làm rõ** `topic_id` theo [mẫu](https://example.com)\n- Mục 1\n- <u>Mục 2</u>';
  const stripped = stripRichText(sample);
  assert.equal(stripped, 'Cần làm rõ topic_id theo mẫu\n• Mục 1\n• Mục 2');
});

test('RichTextEditor helper functions normalize markdown and validate safe link URLs', () => {
  assert.equal(normalizeRichTextMarkdown('\u00a0', true), '');
  assert.equal(normalizeRichTextMarkdown('**Góp ý**\n\n', false), '**Góp ý**');
  assert.equal(normalizeSafeLinkUrl('example.com/guide'), 'https://example.com/guide');
  assert.equal(normalizeSafeLinkUrl('mailto:khoa@uit.edu.vn'), 'mailto:khoa@uit.edu.vn');
  assert.equal(normalizeSafeLinkUrl('javascript:alert(1)'), null);
  assert.equal(normalizeSafeLinkUrl('   '), null);
});
