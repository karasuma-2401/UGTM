import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

const source = readFileSync(new URL('../src/features/councils/services/councilPlanningApi.ts', import.meta.url), 'utf8');

function load(client) {
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    if (name === '@/shared/api') return { httpClient: client };
    throw new Error(`Unexpected dependency: ${name}`);
  }, exports);
  return exports;
}

test('planning API writes the server contract and reads the saved schedule', async () => {
  const calls = [];
  const client = {
    post: async (url, body) => {
      calls.push(['post', url, body]);
      return { data: { success: true, data: url === '/api/councils' ? { id: 'c1' } : [] } };
    },
    put: async (url, body) => {
      calls.push(['put', url, body]);
      return { data: { success: true, data: { id: 'c1' } } };
    },
    get: async (url) => {
      calls.push(['get', url]);
      return { data: { success: true, data: { groups: [] } } };
    },
    delete: async (url) => {
      calls.push(['delete', url]);
      return { data: { success: true, data: true } };
    },
  };
  const api = load(client).councilPlanningApi;
  const schedule = { defenseDate: '2026-12-20', timeSlot: '08:00 - 11:30', room: 'B1.401' };
  await api.create({ batchId: 'b1', councilCode: 'HD01', ...schedule });
  await api.updateSchedule('c1', schedule);
  await api.addGroup('c1', 'g1', 2);
  await api.reorderGroups('c1', [{ groupId: 'g1', orderNumber: 1 }]);
  await api.removeGroup('c1', 'a1');
  await api.schedule('c1');
  await api.defenseEligibility('g1');
  assert.deepEqual(calls, [
    ['post', '/api/councils', { batchId: 'b1', councilCode: 'HD01', ...schedule }],
    ['put', '/api/councils/c1', schedule],
    ['post', '/api/councils/c1/assignments', { assignments: [{ groupId: 'g1', orderNumber: 2 }] }],
    ['put', '/api/councils/c1/assignments/order', { assignments: [{ groupId: 'g1', orderNumber: 1 }] }],
    ['delete', '/api/councils/c1/assignments/a1'],
    ['get', '/api/councils/schedule/c1'],
    ['get', '/api/councils/group-candidates/g1/eligibility'],
  ]);
});

test('group candidate list includes later pages and eligibility reasons are readable', async () => {
  const pages = [];
  const client = {
    get: async (url, config) => {
      assert.equal(url, '/api/councils/group-candidates');
      pages.push(config.params);
      return { data: { success: true, data: {
        items: [{ groupId: `g${config.params.pageNumber}`, topicTitleVi: `Đề tài ${config.params.pageNumber}` }],
        hasNextPage: config.params.pageNumber === 1,
      } } };
    },
  };
  const planning = load(client);
  assert.deepEqual((await planning.councilPlanningApi.groupCandidates('b1')).map((row) => row.groupId), ['g1', 'g2']);
  assert.deepEqual(pages.map((page) => page.pageNumber), [1, 2]);
  assert.ok(pages.every((page) => page.batchId === 'b1' && page.pageSize === 100));
  assert.match(planning.eligibilityMessage('EvaluationNotSubmitted'), /chưa nộp/);
  assert.match(planning.eligibilityMessage('Unknown'), /chưa đủ điều kiện/);
});

test('council dates follow the selected batch defense stage without inventing a missing schedule', () => {
  const utilitySource = readFileSync(new URL('../src/features/councils/utils/councilDefenseWindow.ts', import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(utilitySource, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    throw new Error(`Unexpected dependency: ${name}`);
  }, exports);

  const timeline = { currentLocalDate: '2026-11-21', stages: [
    { stageCode: 'FINAL_EVALUATION', startDate: '2026-11-01', endDate: '2026-11-30' },
    { stageCode: 'DEFENSE', startDate: '2026-12-01', endDate: '2026-12-20' },
  ] };
  const stage = exports.defenseStageOf(timeline);
  assert.equal(stage.startDate, '2026-12-01');
  const window = exports.councilDateWindow(timeline);
  assert.equal(window.source, 'stage');
  assert.equal(exports.isOutsideCouncilWindow('2026-11-30', window), true);
  assert.equal(exports.isOutsideCouncilWindow('2026-12-01', window), false);
  assert.equal(exports.isOutsideCouncilWindow('2026-12-20', window), false);
  assert.equal(exports.isOutsideCouncilWindow('2026-12-21', window), true);
  assert.equal(exports.defenseStageOf({ stages: [] }), undefined);
  assert.equal(exports.isOutsideCouncilWindow('2026-12-21', undefined), false);
  assert.equal(exports.suggestedDefenseDate(window, '2026-11-21'), '2026-12-01');
  assert.equal(exports.suggestedDefenseDate(window, '2026-12-10'), '2026-12-10');
  assert.equal(exports.suggestedDefenseDate(window, '2026-12-21'), '');

  const batch = { startDate: '2026-11-13', endDate: '2026-12-18', timeZoneId: 'Asia/Ho_Chi_Minh' };
  const fallback = exports.councilDateWindow({ stages: [] }, batch);
  assert.deepEqual(fallback, { startDate: '2026-11-13', endDate: '2026-12-18', source: 'batch' });
  assert.equal(exports.suggestedDefenseDate(fallback, '2026-11-21'), '2026-11-21');
  assert.equal(exports.isOutsideCouncilWindow('2026-12-19', fallback), true);
  assert.equal(exports.batchLocalDate(batch, new Date('2026-11-20T18:00:00Z')), '2026-11-21');
});
