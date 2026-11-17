import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import ts from 'typescript';

const compiled = ts.transpileModule(readFileSync(new URL('../src/shared/notifications/notify.ts', import.meta.url), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
function fixture() {
  const items = new Map();
  const toast = { dismiss(id) { if (id === undefined) items.clear(); else items.delete(id); } };
  for (const tone of ['success', 'info', 'warning', 'error', 'loading']) {
    toast[tone] = (title, options) => { items.set(options.id, { title, tone, ...options }); return options.id; };
  }
  const exports = {};
  new Function('exports', 'require', compiled)(exports, (id) => {
    assert.equal(id, 'sonner'); return { toast };
  });
  return { notify: exports.notify, items };
}
const flush = () => new Promise((resolve) => setImmediate(resolve));
const click = (action) => action.onClick({ preventDefault() {} });

test('policy delegates durations and semantic tones to Sonner', () => {
  const { notify, items } = fixture();
  assert.equal(items.get(notify.success('Saved')).duration, 5000);
  assert.equal(items.get(notify.info('Updated')).duration, 5000);
  for (const tone of ['warning', 'error', 'loading']) assert.equal(items.get(notify[tone](tone)).duration, Infinity);
  assert.equal(items.get(notify.info('Read', { duration: 0 })).duration, Infinity);
  assert.equal(items.get(notify.success('Custom', { duration: 9000 })).duration, 9000);
});
test('duplicates update the same Sonner id and dismiss releases deduplication', () => {
  const { notify, items } = fixture();
  const first = notify.success('Saved');
  assert.equal(notify.success('Saved'), first);
  assert.equal(items.size, 1);
  notify.dismiss(first);
  assert.notEqual(notify.success('Saved'), first);
  items.values().next().value.onAutoClose();
  assert.equal(notify.info('  '), '');
});
test('loading can be replaced in place without leaking the old dedupe key', () => {
  const { notify, items } = fixture();
  const id = notify.loading('Importing');
  assert.equal(notify.success('Imported', { id }), id);
  assert.equal(items.size, 1);
  assert.notEqual(notify.loading('Importing'), id);
});
test('actions stay visible and run only once while pending', async () => {
  const { notify, items } = fixture();
  let finish, calls = 0;
  const task = new Promise((resolve) => { finish = resolve; });
  const id = notify.error('Failed', { action: { label: 'Retry', onClick: () => { calls++; return task; } } });
  const action = items.get(id).action;
  assert.equal(items.get(id).duration, Infinity);
  click(action); click(action); await flush();
  assert.equal(calls, 1);
  assert.equal(items.get(id).dismissible, false);
  finish(); await flush();
  assert.equal(items.size, 0);
});
test('rejected actions preserve retry and show safe public copy', async () => {
  const { notify, items } = fixture();
  const id = notify.error('Failed', { action: { label: 'Retry', onClick: () => Promise.reject(new Error('Private server payload')) } });
  click(items.get(id).action); await flush();
  assert.equal(items.get(id).tone, 'error');
  assert.equal(items.get(id).action.label, 'Retry');
  assert.ok(!items.get(id).description.includes('Private'));
  assert.equal(items.get(id).dismissible, true);
});
test('clearing a session prevents late actions from creating new notifications', async () => {
  const { notify, items } = fixture();
  let reject;
  const task = new Promise((_, r) => { reject = r; });
  const id = notify.error('Old session', { action: { label: 'Retry', onClick: () => task } });
  click(items.get(id).action); await flush();
  notify.dismissAll();
  reject(new Error('Late failure')); await flush();
  assert.equal(items.size, 0);
});
