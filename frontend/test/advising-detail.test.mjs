import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';

test('group progress requests fail and retry independently; group history is not read through topic tracking', async () => {
  const queries = [];
  const calls = [];
  const ok = async (name) => {
    calls.push(name);
    return null;
  };
  const dependencies = {
    react: { ...React, useState: (value) => [value, () => {}] },
    'react/jsx-runtime': jsxRuntime,
    '@tanstack/react-query': {
      useQuery(options) {
        queries.push(options);
        return {
          data:
            options.queryKey[0] === 'topic-advising-group-detail'
              ? {
                  groupId: 'old-group',
                  topicTitleVi: 'Đề tài',
                  groupStatus: 'Stopped',
                  members: [],
                }
              : undefined,
          isError: false,
          isPending: true,
          refetch() {},
        };
      },
      useQueries({ queries: items }) {
        assert.deepEqual(items, []);
        return [];
      },
    },
    '@/shared/auth': { useAuth: () => ({ user: { id: 'me' } }) },
    '@/components/ui/button': { Button: 'Button' },
    '@/components/ui/dialog': { Dialog: 'Dialog' },
    '@/components/ui/status-badge': { StatusBadge: 'StatusBadge' },
    '../services/advisingApi': {
      advisingApi: {
        getGroupDetail: async (id) => {
          calls.push(['group', id]);
          return { groupId: id };
        },
      },
    },
    '../services/topicApi': { getTopicErrorMessage: String },
    '../types/advising.types': { GROUP_STATUS_LABELS: { Stopped: 'Đã dừng' } },
    '../advising': {
      confirmationLabel: () => '',
      formatAdvisingDate: () => '',
      groupLabel: (id) => id,
    },
    '@/features/thesis/services/syllabusApi': {
      syllabusApi: {
        getSyllabusHistory: async () => {
          calls.push('syllabus');
          throw new Error('Đề cương lỗi');
        },
      },
    },
    '@/features/thesis/services/midtermApi': {
      midtermApi: { getMidtermHistory: () => ok('midterm') },
    },
    '@/features/thesis/services/finalReportApi': {
      finalReportApi: { getFinalReportHistory: () => ok('finalReport') },
    },
    '@/features/thesis/services/milestoneApi': {
      milestoneApi: { getMilestones: () => ok('milestones') },
    },
    '@/features/thesis/types/syllabus.types': { SYLLABUS_STATUS_LABELS: {} },
    '@/features/thesis/types/midterm.types': { MIDTERM_STATUS_LABELS: {} },
    '@/features/thesis/types/finalReport.types': { FINAL_REPORT_STATUS_LABELS: {} },
    '@/features/thesis/types/milestone.types': { PROGRESS_REPORT_STATUS_LABELS: {} },
    '@/features/thesis/components/SimilarityReportPanel': {
      SimilarityReportPanel: 'SimilarityReportPanel',
    },
  };
  const source = readFileSync(
    new URL('../src/features/topics/components/AdvisingGroupDetail.tsx', import.meta.url),
    'utf8',
  );
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, name);
    return { __esModule: true, ...dependencies[name] };
  }, exports);
  exports.AdvisingGroupDetail({ groupId: 'old-group', onClose() {} });
  const signal = new AbortController().signal;
  assert.equal(queries.length, 5);
  assert.ok(
    queries.every((query) => query.queryKey[1] === 'me' && query.queryKey[2] === 'old-group'),
  );
  await queries[0].queryFn({ signal });
  await assert.rejects(queries[1].queryFn({ signal }), /Đề cương lỗi/);
  await queries[2].queryFn({ signal });
  await queries[3].queryFn({ signal });
  await queries[4].queryFn({ signal });
  assert.deepEqual(calls, [
    ['group', 'old-group'],
    'syllabus',
    'midterm',
    'finalReport',
    'milestones',
  ]);
});
