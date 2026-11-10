import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import axios from 'axios';
import ts from 'typescript';

function loadModule(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
    return dependencies[name];
  }, exports);
  return exports;
}

function createApiFixture(httpClient) {
  return loadModule('features/councils/services/councilApprovalApi.ts', {
    axios: { __esModule: true, default: axios },
    '@/shared/api': { __esModule: true, httpClient },
  }).councilApprovalApi;
}

test('getCouncilDrafts maps filters, paginates, and groups rows by councilId', async () => {
  const calls = [];
  const signal = new AbortController().signal;
  const page1 = Array.from({ length: 100 }, (_, index) => ({
    id: index < 2 ? 'c-multi' : `c-${index}`,
    batchId: 'batch-1',
    councilCode: index < 2 ? 'HD01' : `HD${index}`,
    facultyName: 'Khoa CNTT',
    groupCode: index === 0 ? 'N01' : index === 1 ? 'N02' : 'N03',
    topicTitle: index === 0 ? 'De tai A' : index === 1 ? 'De tai B' : `De tai ${index}`,
    room: 'B1.401',
    timeSlot: 'Ca sang',
    defenseDate: '2026-12-20',
    status: 'Draft',
    statusLabel: 'Cho phe duyet',
    rosterModel: 'ReviewerInCouncil',
    rosterModelLabel: 'CT · TK · GVPB kiem UV',
    profileStatus: 'Pending',
    profileStatusLabel: 'Chua co phieu',
    minuteLockStatus: 'Open',
    minuteLockStatusLabel: 'Chua khoa',
    assignmentId: `a-${index}`,
    chairman: { userId: 'u1', fullName: 'TS. Chu Tich', role: 'Chairman', isReviewer: false, isAdvisorConflict: false },
    secretary: { userId: 'u2', fullName: 'ThS. Thu Ky', role: 'Secretary', isReviewer: false, isAdvisorConflict: false },
    members: [{ userId: 'u3', fullName: 'TS. Uy Vien', role: 'Member', isReviewer: index < 2, isAdvisorConflict: false }],
  }));
  const page2 = [
    {
      id: 'c-nogroup',
      batchId: 'batch-1',
      councilCode: 'HD-EMPTY',
      facultyName: 'Khoa CNTT',
      groupCode: null,
      topicTitle: null,
      room: 'B1.402',
      timeSlot: 'Ca chieu',
      defenseDate: '2026-12-21',
      status: 'Active',
      statusLabel: 'Da phe duyet',
      rosterModel: 'IndependentMember',
      rosterModelLabel: 'CT · TK · UV doc lap',
      profileStatus: 'Pending',
      profileStatusLabel: 'Chua co phieu',
      minuteLockStatus: 'Open',
      minuteLockStatusLabel: 'Chua khoa',
      assignmentId: null,
      chairman: null,
      secretary: null,
      members: [],
    },
  ];

  const api = createApiFixture({
    get: async (url, config) => {
      calls.push({ url, config });
      return {
        data: {
          success: true,
          data: config.params.pageNumber === 1 ? page1 : page2,
          error: null,
        },
      };
    },
  });

  const result = await api.getCouncilDrafts(
    { batchId: 'all', keyword: '  HD01 ', status: 'draft' },
    signal,
  );

  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, '/api/councils');
  assert.equal(calls[0].config.signal, signal);
  assert.deepEqual(calls[0].config.params, {
    batchId: undefined,
    keyword: 'HD01',
    status: 'Draft',
    pageNumber: 1,
    pageSize: 100,
  });

  const grouped = result.find((x) => x.id === 'c-multi');
  assert.ok(grouped);
  assert.equal(grouped.groupCode, 'N01, N02');
  assert.deepEqual(grouped.topicTitles, ['De tai A', 'De tai B']);
  assert.equal(grouped.chairmanName, 'TS. Chu Tich');
  assert.equal(grouped.secretaryName, 'ThS. Thu Ky');
  assert.deepEqual(grouped.memberNames, ['TS. Uy Vien (GVPB kiêm UV)']);
  assert.equal(grouped.groupConditionLabel, 'Đủ điều kiện bảo vệ');

  const noGroup = result.find((x) => x.id === 'c-nogroup');
  assert.ok(noGroup);
  assert.equal(noGroup.groupCode, 'Chưa gán nhóm');
  assert.equal(noGroup.groupConditionLabel, 'Chưa gán nhóm');
  assert.equal(noGroup.membersSummary, 'Chưa có thành viên');
});

test('approveCouncil sends payload and surfaces backend business errors', async () => {
  const putCalls = [];
  const apiOk = createApiFixture({
    put: async (url, body) => {
      putCalls.push({ url, body });
      return {
        data: {
          success: true,
          data: {
            id: 'c1',
            batchId: 'b1',
            councilCode: 'HD01',
            defenseDate: '2026-12-20',
            timeSlot: 'Ca sang',
            room: 'B1.401',
            status: 'Active',
            decisionNumber: body.decisionNumber,
            signedDate: body.signedDate,
            decisionFileUrl: body.decisionFileUrl,
          },
          error: null,
        },
      };
    },
  });

  const approved = await apiOk.approveCouncil('c1', {
    decisionNumber: 'QD-HD01',
    signedDate: '2026-11-10',
    decisionFileUrl: 'QD-HD01.pdf',
  });
  assert.equal(putCalls[0].url, '/api/councils/c1/approve');
  assert.equal(approved.status, 'Active');

  const apiErr = createApiFixture({
    put: async () => {
      const error = new axios.AxiosError(
        'Request failed with status code 409',
        'ERR_BAD_REQUEST',
        undefined,
        undefined,
        {
          status: 409,
          statusText: 'Conflict',
          headers: {},
          config: {},
          data: {
            success: false,
            data: null,
            error: {
              code: 'Council.NotSubmittedToAcademicAffairs',
              message: 'Hội đồng chưa được Văn phòng Khoa nộp hồ sơ thẩm định lên Phòng Đào tạo.',
            },
          },
        },
      );
      throw error;
    },
  });

  await assert.rejects(
    () =>
      apiErr.approveCouncil('c1', {
        decisionNumber: 'QD-HD01',
        signedDate: '2026-11-10',
        decisionFileUrl: null,
      }),
    (err) =>
      err instanceof Error &&
      err.message === 'Hội đồng chưa được Văn phòng Khoa nộp hồ sơ thẩm định lên Phòng Đào tạo.',
  );
});
