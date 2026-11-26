import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import axios from 'axios';
import ts from 'typescript';

function loadApi(client) {
  const source = readFileSync(new URL('../src/features/thesis/services/finalReportApi.ts', import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  });
  const exports = {};
  const dependencies = {
    '@/shared/api': { httpClient: client },
    '../utils/archival': {},
    './submissionUploadHelper': {},
  };
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
    return dependencies[name];
  }, exports);
  return exports.finalReportApi;
}

const groupId = '11111111-1111-4111-8111-111111111111';
const file = new File(['%PDF-1.7\nrevision report'], 'ban-sua.pdf', { type: 'application/pdf' });

test('revised report upload preserves file bytes through real Axios transforms with JSON defaults', async () => {
  const expected = { finalReportId: 'report', versionType: 'RevisedAfterReviewer', versionNo: 1, status: 'Submitted' };
  let uploads = 0;
  const client = axios.create({
    headers: { 'Content-Type': 'application/json' },
    adapter: async (config) => {
      uploads++;
      assert.equal(config.method, 'post');
      assert.equal(config.url, '/api/thesis/revised-final-reports');
      assert.match(String(config.headers.get('Content-Type')), /^multipart\/form-data/);
      assert.ok(config.data instanceof FormData, 'request must retain FormData rather than JSON serialization');
      assert.equal(config.data.get('groupId'), groupId);
      const uploaded = config.data.get('file');
      assert.equal(uploaded.name, file.name);
      assert.equal(uploaded.type, file.type);
      assert.equal(await uploaded.text(), await file.text());
      return { status: 200, statusText: 'OK', headers: {}, config, data: { success: true, data: expected } };
    },
  });
  assert.deepEqual(await loadApi(client).submitRevisedFinalReport(groupId, file), expected);
  assert.equal(uploads, 1);
});

test('revised report API preserves backend rejection for the submission UI', async () => {
  const failure = new axios.AxiosError('Rejected', 'ERR_BAD_REQUEST', undefined, undefined, {
    status: 409, data: { error: { code: 'FinalReport.OutsideAllowedWindow', message: 'Ngoài thời gian nộp.' } },
  });
  const api = loadApi({ post: async () => { throw failure; } });
  await assert.rejects(api.submitRevisedFinalReport(groupId, file), (error) => error === failure);
});
