import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

function loadModule(file, dependencies = {}) {
  const source = readFileSync(new URL(`../src/features/thesis/utils/${file}.ts`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    if (name in dependencies) return dependencies[name];
    throw new Error(`Unexpected dependency: ${name}`);
  }, exports);
  return exports;
}

const validation = loadModule('gradingRubricValidation');
const { requireGradingRubric } = validation;
const { DEFAULT_UIT_SECTIONS, buildRubricSections, buildStudentScoresPayload } = loadModule('gradingRubricHelper', {
  './gradingRubricValidation': validation,
});
const batchId = '11111111-1111-4111-8111-111111111111';
const criterionId = '22222222-2222-4222-8222-222222222222';
const studentId = '33333333-3333-4333-8333-333333333333';
const standard = DEFAULT_UIT_SECTIONS.flatMap((section) => section.criteria);
const criterion = { id: criterionId, code: '1.1', name: 'Criterion', maxScore: 1.5, order: 1 };
const rubric = { batchId, locked: false, criteria: standard.map((item, index) => ({
  id: index === 0 ? criterionId : `22222222-2222-4222-8222-${String(index).padStart(12, '0')}`,
  code: item.code, name: item.name, maxScore: item.max, order: index + 1,
})) };

test('missing or empty rubric cannot be used to grade', () => {
  for (const value of [null, { ...rubric, criteria: [] }]) {
    assert.throws(() => requireGradingRubric(value, batchId), /Chưa tải được/);
  }
});

test('fixed UIT form is preserved while database IDs load, but cannot be submitted', () => {
  for (const value of [null, undefined, []]) {
    const sections = buildRubricSections(value);
    assert.deepEqual(sections, DEFAULT_UIT_SECTIONS);
    assert.equal(sections.flatMap((section) => section.criteria).length, 10);
    assert.equal(sections.reduce((sum, section) => sum + section.max, 0), 10);
    assert.throws(() => buildStudentScoresPayload([studentId], sections.flatMap((section) => section.criteria), {}), /Chưa tải xong/);
  }
});

test('criterion codes from the old sample rubric are rejected as IDs', () => {
  for (const id of ['1.1', '2.3', '', '00000000-0000-0000-0000-000000000000']) {
    assert.throws(() => requireGradingRubric({ ...rubric, criteria: [{ ...criterion, id }] }, batchId), /không hợp lệ/);
  }
});

test('a rubric from the previous batch cannot be used for the current batch', () => {
  assert.throws(() => requireGradingRubric(rubric, '44444444-4444-4444-8444-444444444444'), /không thuộc đợt/);
});

test('duplicate criterion IDs are rejected even with different casing', () => {
  const id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
  assert.throws(() => requireGradingRubric({ ...rubric, criteria: [
    { ...criterion, id }, { ...criterion, id: id.toUpperCase(), code: '1.2' },
  ] }, batchId), /không hợp lệ/);
});

test('grading payload preserves server GUIDs and student scores', () => {
  const sections = buildRubricSections(requireGradingRubric(rubric, batchId));
  const criteria = sections.flatMap((section) => section.criteria);
  const payload = buildStudentScoresPayload([studentId], criteria, {
    [studentId]: { [criterionId]: '1.25' },
  });
  assert.deepEqual(payload, [{ studentId, details: [{ criteriaId: criterionId, score: 1.25, studentId }] }]);
  assert.deepEqual(sections.flatMap((section) => section.criteria).map(({ code, max, name }) => ({ code, max, name })),
    standard.map(({ code, max, name }) => ({ code, max, name })));
});

test('custom criteria or changed maximums cannot replace the fixed UIT form', () => {
  assert.throws(() => requireGradingRubric({ ...rubric, criteria: [criterion] }, batchId), /không khớp barem/);
  assert.throws(() => requireGradingRubric({ ...rubric, criteria: rubric.criteria.map((c, i) => i === 0 ? { ...c, maxScore: 2 } : c) }, batchId), /không khớp barem/);
});

test('rubric can be accepted after a missing-rubric failure without synthesizing IDs', () => {
  assert.throws(() => requireGradingRubric(null, batchId));
  assert.equal(requireGradingRubric(rubric, batchId), rubric.criteria);
});
