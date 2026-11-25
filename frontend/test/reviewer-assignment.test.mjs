import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import ts from 'typescript';

function load(path, dependencies) {
  const source = readFileSync(new URL(`../src/features/councils/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
    return dependencies[name];
  }, exports);
  return exports;
}
const { ReviewerAssignmentFormCard } = load('components/reviewer-assignment/ReviewerAssignmentFormCard.tsx', {
  'react/jsx-runtime': jsxRuntime,
  '@/components/ui/button': { Button: 'Button' },
  '@/components/ui/inline-alert': { InlineAlert: 'InlineAlert' },
  '@/components/ui/select': { Select: 'Select' },
  '@/components/ui/avatar': { Avatar: 'Avatar' },
  'lucide-react': { UserCheck: 'UserCheck' },
});
function nodes(tree) {
  if (Array.isArray(tree)) return tree.flatMap(nodes);
  return React.isValidElement(tree) ? [tree, ...nodes(tree.props.children)] : [];
}
const find = (tree, type) => nodes(tree).find((node) => node.type === type);
const reviewerId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const advisorId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const groupId = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';
const evaluationId = 'dddddddd-dddd-4ddd-8ddd-dddddddddddd';
const candidate = { userId: reviewerId, fullName: 'Giảng viên phản biện', userCode: 'GV02' };
const selectedItem = { groupId, advisorId, advisorEvaluationId: evaluationId, reportVersionNo: 2, hasActiveAssignment: false, canAssign: true };
const base = { selectedItem, candidates: [candidate], selectedReviewerId: '', onSelectReviewerId() {}, onSubmitAssignment() {}, isSubmitting: false, canManage: true };

test('server userId flows from candidate API through Select into assignment reviewerId', async () => {
  const calls = [];
  const api = load('services/reviewerAssignmentApi.ts', { '@/shared/api': { httpClient: {
    get: async (url, config) => {
      assert.equal(url, '/api/thesis/reviewer-candidates');
      assert.equal(config.params.groupId, groupId);
      return { data: { success: true, data: { items: [candidate], totalCount: 1, pageNumber: 1, pageSize: 100 } } };
    },
    post: async (...args) => {
      calls.push(args);
      return { data: { success: true, data: { id: evaluationId, reviewerId, isActive: true } } };
    },
  } } }).reviewerAssignmentApi;
  const response = await api.getCandidates(groupId);
  let selection = '';
  const tree = ReviewerAssignmentFormCard({ ...base, candidates: response.items, onSelectReviewerId: (value) => { selection = value; } });
  const select = find(tree, 'Select');
  assert.equal(select.props.options[0].value, reviewerId);
  assert.equal(find(tree, 'Button').props.disabled, true);
  select.props.onChange(select.props.options[0].value);
  const selected = ReviewerAssignmentFormCard({ ...base, candidates: response.items, selectedReviewerId: selection });
  assert.equal(find(selected, 'Select').props.value, reviewerId);
  assert.equal(find(selected, 'Button').props.disabled, false);
  assert.ok(nodes(selected).some((node) => node.type === 'strong' && node.props.children === candidate.fullName));
  const result = await api.createAssignment({ groupId, reviewerId: selection, expectedAdvisorEvaluationId: evaluationId, expectedReportVersionNo: 2 }, evaluationId);
  assert.equal(result.reviewerId, reviewerId);
  assert.deepEqual(calls, [['/api/thesis/reviewer-assignments', { groupId, reviewerId, expectedAdvisorEvaluationId: evaluationId, expectedReportVersionNo: 2 }, { headers: { 'Idempotency-Key': evaluationId } }]]);
});

test('undefined IDs never enable assignment or show a falsely selected reviewer', () => {
  const tree = ReviewerAssignmentFormCard({ ...base, candidates: [{ id: reviewerId, fullName: 'Malformed candidate' }], selectedReviewerId: undefined });
  assert.deepEqual(find(tree, 'Select').props.options, []);
  assert.equal(find(tree, 'Button').props.disabled, true);
  assert.ok(!nodes(tree).some((node) => node.type === 'strong'));
});

test('advisor is excluded using userId and unknown selection cannot be submitted', () => {
  const tree = ReviewerAssignmentFormCard({ ...base, candidates: [candidate, { userId: advisorId, fullName: 'GVHD' }], selectedReviewerId: advisorId });
  assert.deepEqual(find(tree, 'Select').props.options.map((option) => option.value), [reviewerId]);
  assert.equal(find(tree, 'Button').props.disabled, true);
});

test('loading, pending and ineligible group keep assignment disabled', () => {
  for (const extra of [{ isLoadingCandidates: true }, { isSubmitting: true }, { selectedItem: { ...selectedItem, canAssign: false } }]) {
    const tree = ReviewerAssignmentFormCard({ ...base, selectedReviewerId: reviewerId, ...extra });
    assert.equal(find(tree, 'Button').props.disabled, true);
  }
});

test('candidate loading failure shows retry and keeps the selection form unavailable', () => {
  const tree = ReviewerAssignmentFormCard({ ...base, selectedReviewerId: reviewerId, candidatesError: 'API unavailable' });
  assert.equal(find(tree, 'Select'), undefined);
  assert.equal(find(tree, 'InlineAlert').props.title, 'Không tải được giảng viên');
});
