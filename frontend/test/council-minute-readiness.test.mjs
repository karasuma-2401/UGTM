import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import axios from 'axios';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import { renderToStaticMarkup } from 'react-dom/server';
import * as icons from 'lucide-react';
import ts from 'typescript';

function load(file, dependencies = {}) {
  const source = readFileSync(new URL(`../src/features/councils/${file}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX } });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency ${name}`);
    return dependencies[name];
  }, exports);
  return exports;
}
const scores = load('utils/scoreSummaryFormatting.ts');
const formatting = load('utils/councilMinuteFormatting.ts', { axios: { default: axios }, './scoreSummaryFormatting': scores });
const { CouncilMinuteAuditDock } = load('components/minutes/CouncilMinuteAuditDock.tsx', {
  'react/jsx-runtime': jsxRuntime, 'lucide-react': icons,
  '@/lib/utils': { cn: (...values) => values.filter(Boolean).join(' ') },
  '../../utils/councilMinuteFormatting': formatting, '../../utils/scoreSummaryFormatting': scores,
  '@/components/ui/button': { Button: ({ children, onClick, disabled }) => React.createElement('button', { onClick, disabled }, children) },
  '@/components/ui/inline-alert': { InlineAlert: ({ title, children }) => React.createElement('div', { role: 'alert' }, title, children) },
});
const blocked = { rawTotal: 0, rawDivisor: 0, rawAverage: 0, adjustedTotal: 0, adjustedDivisor: 0, adjustedAverage: 0, finalRoundedScore: null, formulaExplanation: '', isReadyToLock: false, calculationErrorCode: 'ScoreSummary.MissingRequiredSource', missingSources: ['CouncilMember'] };

test('minute header reflects backend calculation readiness instead of the council score-entry status', () => {
  assert.equal(formatting.getMinuteReadinessMeta('AllScoresEntered', blocked).label, 'Chưa đủ điều kiện tính điểm');
  assert.equal(formatting.getMinuteReadinessMeta('AllScoresEntered', { ...blocked, calculationErrorCode: null, isReadyToLock: true, adjustedDivisor: 6 }).label, 'Sẵn sàng khóa biên bản');
  assert.equal(formatting.getMinuteReadinessMeta('Locked', blocked).label, 'Đã khóa');
  assert.equal(formatting.getMinuteReadinessMeta('AllScoresEntered').label, 'Đủ phiếu Hội đồng');
});

test('missing independent member is named and zero denominators are shown as undetermined', () => {
  const markup = renderToStaticMarkup(React.createElement(CouncilMinuteAuditDock, {
    scoreItems: [], calculationPreview: blocked, discrepancySummary: { hasDiscrepancy: false },
    isDirty: false, isLocked: false, isCouncilActive: true, canEditMinute: true, canLockMinute: true,
    isSaving: false, isPreviewing: false,
  }));
  assert.match(markup, /Thiếu đầu điểm: Ủy viên Hội đồng/);
  assert.equal((markup.match(/Mẫu số: Chưa xác định/g) || []).length, 2);
  assert.doesNotMatch(markup, /Mẫu số: 0/);
  assert.match(markup, /disabled=""[^>]*>[\s\S]*Xác thực và Khóa biên bản/);
});
