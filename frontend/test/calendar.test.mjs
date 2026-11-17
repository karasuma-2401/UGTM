import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

const source = readFileSync(new URL('../src/components/ui/calendar-utils.ts', import.meta.url), 'utf8');
const { outputText } = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
});
const exports = {};
new Function('exports', outputText)(exports);
const { calendarDate, calendarISO, calendarDisplay, calendarInputValue } = exports;

test('calendar validates real dates, including leap days and year boundaries', () => {
  for (const value of ['2024-02-29', '2026-12-31', '2027-01-01', '0099-01-01']) {
    assert.equal(calendarISO(calendarDate(value)), value);
  }
  for (const value of ['2026-02-29', '2026-04-31', '2026-13-01', '2026-00-01', '0000-01-01', 'garbage']) {
    assert.equal(calendarDate(value), null);
  }
});

test('Vietnamese display round trips without changing local dates or times', () => {
  assert.equal(calendarDisplay('2026-11-11'), '11/11/2026');
  assert.equal(calendarInputValue('11/11/2026'), '2026-11-11');
  assert.equal(calendarDisplay('2026-11-11T23:59', true), '11/11/2026 23:59');
  assert.equal(calendarInputValue('11/11/2026 23:59', true), '2026-11-11T23:59');
  assert.equal(calendarInputValue('11/11/2026 24:00', true), null);
  assert.equal(calendarInputValue('11/11/2026 12:60', true), null);
  assert.equal(calendarInputValue('31/02/2026'), null);
  assert.equal(calendarInputValue('11/11/'), null);
  assert.equal(calendarInputValue(''), null);
  assert.equal(calendarDisplay(''), '');
});
