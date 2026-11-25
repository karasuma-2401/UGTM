import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

function load(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/features/thesis/${path}.ts`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency ${name}`);
    return dependencies[name];
  }, exports);
  return exports;
}

const { prepareAdvisorReport } = load('utils/prepareAdvisorReport');
const report = {
  finalReportId: '11111111-1111-4111-8111-111111111111',
  reportVersionNo: 3, reportVersionType: 'InitialFinal', reportStatus: 'Submitted',
};
const started = { finalReportId: report.finalReportId, versionNo: 3, versionType: 'InitialFinal', status: 'UnderEvaluation' };

test('the viewed Submitted report is locked before the caller can save scores', async () => {
  const calls = [];
  const result = await prepareAdvisorReport(report, async (id, version) => {
    calls.push([id, version]);
    return started;
  });
  assert.deepEqual(calls, [[report.finalReportId, 3]]);
  assert.deepEqual(result, { finalReportId: report.finalReportId, reportVersionNo: 3, reportStatus: 'UnderEvaluation' });
});

test('an already UnderEvaluation report can be saved without starting it again', async () => {
  const prepared = await prepareAdvisorReport({ ...report, reportStatus: 'UnderEvaluation' }, () => {
    assert.fail('must not begin an already locked report');
  });
  assert.equal(prepared.reportVersionNo, 3);
});

test('missing report metadata never falls back to report version 1', async () => {
  for (const patch of [{ finalReportId: null }, { reportVersionNo: null }, { reportVersionNo: 0 }, { reportVersionNo: 1.5 }, { reportVersionType: 'RevisedAfterReviewer' }]) {
    await assert.rejects(prepareAdvisorReport({ ...report, ...patch }, () => assert.fail('unexpected mutation')), /Chưa tải được/);
  }
});

test('an invalid report state does not trigger beginning or saving', async () => {
  await assert.rejects(prepareAdvisorReport({ ...report, reportStatus: 'Rejected' }, () => assert.fail('unexpected mutation')), /không ở trạng thái/);
});

test('a version conflict or denied begin must stop the save chain', async () => {
  const conflict = new Error('FinalReport.ConcurrencyConflict');
  await assert.rejects(prepareAdvisorReport(report, async () => { throw conflict; }), (error) => error === conflict);
});

test('a begin response cannot silently switch to a newer report version or another report', async () => {
  for (const patch of [{ versionNo: 4 }, { finalReportId: 'other' }, { versionType: 'RevisedAfterReviewer' }, { status: 'Submitted' }]) {
    await assert.rejects(prepareAdvisorReport(report, async () => ({ ...started, ...patch })), /đã thay đổi/);
  }
});

test('retry after a successful begin and failed save does not begin twice', async () => {
  let begins = 0;
  const begin = async () => { begins++; return started; };
  const prepared = await prepareAdvisorReport(report, begin);
  await prepareAdvisorReport({ ...report, ...prepared }, begin);
  assert.equal(begins, 1);
});

test('the begin API sends the exact viewed version to the report endpoint', async () => {
  const calls = [];
  const { finalReportApi } = load('services/finalReportApi', {
    '@/shared/api': { httpClient: { post: async (...args) => {
      calls.push(args);
      return { data: { success: true, data: started } };
    } } },
    '../utils/archival': {},
    './submissionUploadHelper': {},
  });
  assert.deepEqual(await finalReportApi.beginEvaluation(report.finalReportId, 3), started);
  assert.deepEqual(calls, [[`/api/thesis/final-reports/${report.finalReportId}/begin-evaluation`, { expectedVersionNo: 3 }]]);
});
