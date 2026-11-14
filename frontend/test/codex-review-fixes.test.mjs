import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

function loadTsModule(relativePath, dependencies = {}) {
  const source = readFileSync(new URL(`../src/${relativePath}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  const module = { exports };
  new Function('require', 'exports', 'module', outputText)(
    (specifier) => {
      assert.ok(specifier in dependencies, `Missing mock dependency "${specifier}" for ${relativePath}`);
      return { __esModule: true, ...dependencies[specifier] };
    },
    exports,
    module,
  );
  return module.exports;
}

function createHookRunner() {
  const states = [];
  const refs = [];
  const callbacks = [];
  const memos = [];
  const effectDeps = [];
  const cleanups = [];
  let stateCursor = 0;
  let refCursor = 0;
  let callbackCursor = 0;
  let memoCursor = 0;
  let effectCursor = 0;
  let pendingEffects = [];

  function depsChanged(prevDeps, nextDeps) {
    return (
      !prevDeps ||
      !nextDeps ||
      nextDeps.length !== prevDeps.length ||
      nextDeps.some((val, i) => !Object.is(val, prevDeps[i]))
    );
  }

  const reactMock = {
    useState(initial) {
      const idx = stateCursor++;
      if (!(idx in states)) {
        states[idx] = typeof initial === 'function' ? initial() : initial;
      }
      const setState = (next) => {
        states[idx] = typeof next === 'function' ? next(states[idx]) : next;
      };
      return [states[idx], setState];
    },
    useRef(initial) {
      const idx = refCursor++;
      if (!(idx in refs)) {
        refs[idx] = { current: initial };
      }
      return refs[idx];
    },
    useCallback(fn, deps) {
      const idx = callbackCursor++;
      const prev = callbacks[idx];
      if (!prev || depsChanged(prev.deps, deps)) {
        callbacks[idx] = { fn, deps };
      }
      return callbacks[idx].fn;
    },
    useMemo(fn, deps) {
      const idx = memoCursor++;
      const prev = memos[idx];
      if (!prev || depsChanged(prev.deps, deps)) {
        memos[idx] = { value: fn(), deps };
      }
      return memos[idx].value;
    },
    useEffect(effect, deps) {
      const idx = effectCursor++;
      const prevDeps = effectDeps[idx];
      if (depsChanged(prevDeps, deps)) {
        effectDeps[idx] = deps;
        pendingEffects.push({ idx, effect });
      }
    },
  };

  function render(hookFn) {
    stateCursor = 0;
    refCursor = 0;
    callbackCursor = 0;
    memoCursor = 0;
    effectCursor = 0;
    pendingEffects = [];
    const result = hookFn();
    const toRun = [...pendingEffects];
    pendingEffects = [];
    for (const { idx, effect } of toRun) {
      if (typeof cleanups[idx] === 'function') {
        cleanups[idx]();
      }
      cleanups[idx] = effect();
    }
    return result;
  }

  return { reactMock, render };
}

test('[P1 #1] useRosterData does not loop when onError callback identity changes across renders and exposes loadError', async () => {
  const { reactMock, render } = createHookRunner();
  let batchCalls = 0;
  let userCalls = 0;

  const { useRosterData } = loadTsModule('features/academic/hooks/roster/useRosterData.ts', {
    react: reactMock,
    '../../services/periodApi': {
      periodApi: {
        getBatches: async () => {
          batchCalls++;
          return { items: [{ id: 'batch-1', batchName: 'Đợt 1', status: 'Open' }] };
        },
      },
      getPeriodErrorMessage: (err) => err?.message ?? 'Error',
    },
    '@/features/identity/services/userApi': {
      userApi: {
        getUsers: async () => {
          userCalls++;
          return {
            items: [{ id: 'u-1', userCode: 'SV01', fullName: 'Nguyen Van A', email: 'a@uni.edu' }],
          };
        },
      },
      getUserErrorMessage: (err) => err?.message ?? 'Error',
    },
  });

  // Render 1 with inline callback
  render(() => useRosterData((msg) => msg));
  await new Promise((r) => setTimeout(r, 10));

  // Re-render multiple times with brand-new () => {} functions (simulating parent re-renders)
  let latest;
  for (let i = 0; i < 5; i++) {
    latest = render(() => useRosterData(() => {}));
  }
  await new Promise((r) => setTimeout(r, 10));

  assert.equal(batchCalls, 1, 'getBatches should only be called once on mount');
  assert.equal(userCalls, 1, 'getUsers should only be called once on mount');
  assert.equal(latest.loadError, null);
});

test('[P1 #2] useSyllabusSubmission scopes query keys by user.id and disables queries when unauthenticated', () => {
  const queries = [];
  const { useSyllabusSubmission } = loadTsModule('features/thesis/hooks/useSyllabusSubmission.ts', {
    react: { useState: (v) => [v, () => {}] },
    '@tanstack/react-query': {
      useQueryClient: () => ({}),
      useQuery(opts) {
        queries.push(opts);
        return {
          data:
            opts.queryKey[0] === 'student-registration-status'
              ? { groupId: 'grp-1', batchId: 'b-1' }
              : null,
          isLoading: false,
          isError: false,
          error: null,
          refetch: () => {},
        };
      },
      useMutation: () => ({ mutateAsync: async () => {}, isPending: false }),
    },
    '@/shared/auth': {
      useAuth: () => ({ user: { id: 'student-42' } }),
    },
    '@/shared/academic-periods/useAcademicBatches': {
      useAcademicBatches: () => ({
        data: [{ id: 'b-1', batchName: 'Đợt 1', status: 'Open' }],
      }),
    },
    '../services/syllabusApi': {
      syllabusApi: {},
    },
    '../services/submissionUploadHelper': {
      validateReportFile: () => null,
    },
  });

  useSyllabusSubmission();
  assert.deepEqual(queries[0].queryKey, ['student-registration-status', 'student-42']);
  assert.equal(queries[0].enabled, true);
  assert.deepEqual(queries[1].queryKey, ['student-group-detail', 'student-42', 'grp-1', 'b-1']);
  assert.equal(queries[1].enabled, true);
  assert.deepEqual(queries[2].queryKey, ['syllabus-history', 'student-42', 'grp-1']);
  assert.equal(queries[2].enabled, true);
});

test('[P1 #3] usePeriodData ignores stale batch detail response when switching batches quickly', async () => {
  const { reactMock, render } = createHookRunner();
  const resolvers = {};

  const { usePeriodData } = loadTsModule('features/academic/hooks/periods/usePeriodData.ts', {
    react: reactMock,
    '../../services/periodApi': {
      periodApi: {
        getBatches: async () => ({
          items: [
            { id: 'batch-a', batchName: 'Batch A', status: 'Upcoming' },
            { id: 'batch-b', batchName: 'Batch B', status: 'Open' },
          ],
        }),
        getBatchById: (id) =>
          new Promise((resolve) => {
            resolvers[id] = resolve;
          }),
        getReviewGateConfigurations: async () => [],
      },
      getPeriodErrorMessage: (err) => err?.message ?? 'Error',
    },
  });

  // Mount & load batch list -> selects batch-a
  render(() => usePeriodData(() => {}));
  await new Promise((r) => setTimeout(r, 10));
  let state = render(() => usePeriodData(() => {}));
  assert.equal(state.selectedBatchId, 'batch-a');

  // Start loading batch-a detail
  const loadAPromise = state.loadBatchDetail('batch-a');

  // Switch quickly to batch-b before batch-a resolves
  state.setSelectedBatchId('batch-b');
  state = render(() => usePeriodData(() => {}));
  assert.equal(state.selectedBatchId, 'batch-b');

  // Start loading batch-b detail and resolve batch-b first
  const loadBPromise = state.loadBatchDetail('batch-b');
  resolvers['batch-b']({
    id: 'batch-b',
    batchName: 'Batch B Detail',
    academicYear: '2026-2027',
    semester: 2,
    status: 'Open',
    startDate: '2026-09-01',
    endDate: '2026-12-31',
    stages: [],
  });
  await loadBPromise;
  state = render(() => usePeriodData(() => {}));
  assert.equal(state.currentBatch?.id, 'batch-b');
  assert.equal(state.isSelectionSynced, true);

  // Late response for batch-a arrives afterwards — must be ignored!
  resolvers['batch-a']({
    id: 'batch-a',
    batchName: 'Batch A Stale Detail',
    academicYear: '2025-2026',
    semester: 1,
    status: 'Upcoming',
    startDate: '2025-09-01',
    endDate: '2025-12-31',
    stages: [],
  });
  await loadAPromise;
  state = render(() => usePeriodData(() => {}));
  assert.equal(state.currentBatch?.id, 'batch-b');
  assert.equal(state.currentBatch?.batchName, 'Batch B Detail');
});

test('[P1 #3 & #5] usePeriodActions blocks mismatched batch save and reports partial gate update failures with retry', async () => {
  const { reactMock, render } = createHookRunner();
  const updatedScheduleFor = [];
  let midtermShouldFail = true;
  const errors = [];
  const successes = [];
  const partials = [];

  const { usePeriodActions } = loadTsModule('features/academic/hooks/periods/usePeriodActions.ts', {
    react: reactMock,
    '../../services/periodApi': {
      periodApi: {
        createBatch: async () => ({ id: 'new-id' }),
        updateSchedule: async (batchId) => {
          updatedScheduleFor.push(batchId);
        },
        openBatch: async () => ({}),
        upsertReviewGateConfiguration: async (_batchId, gateType) => {
          if (gateType === 'Midterm' && midtermShouldFail) {
            throw new Error('Midterm gate error');
          }
        },
        getReviewGateConfigurations: async () => [],
      },
      getPeriodErrorMessage: (err) => err?.message ?? 'Error',
    },
    '../../utils/periodDates': loadTsModule('features/academic/utils/periodDates.ts', {
      '../types/period.types': loadTsModule('features/academic/types/period.types.ts'),
    }),
  });

  const baseParams = {
    startDate: '2026-09-01',
    batchName: 'Batch B',
    academicYear: '2026-2027',
    semester: 1,
    computedStages: [
      {
        orderIndex: 2,
        stageCode: 'STAGE_2',
        stageName: 'Stage 2',
        startDate: '2026-09-05',
        endDate: '2026-09-25',
      },
    ],
    syllabusDeadline: '2026-09-20',
    midtermDeadline: '2026-10-20',
    finalEvaluationDeadline: '',
    defenseDeadline: '',
    onSuccess: async (_id, msg) => {
      successes.push(msg);
    },
    onPartialSuccess: async (_id, _isNew, failedGates) => {
      partials.push(failedGates);
    },
    onError: (msg) => {
      errors.push(msg);
    },
  };

  // 1. Mismatched batch ID must be blocked
  let actions = render(() => usePeriodActions());
  await actions.executeSave({
    ...baseParams,
    selectedBatchId: 'batch-b',
    currentBatch: { id: 'batch-a', status: 'Upcoming' },
    isLoadingDetail: false,
  });
  assert.equal(updatedScheduleFor.length, 0, 'Should not save when currentBatch.id !== selectedBatchId');
  assert.equal(errors.length, 1);

  // 2. Synced batch ID where Midterm gate fails -> partial save reported, not full success
  errors.length = 0;
  actions = render(() => usePeriodActions());
  await actions.executeSave({
    ...baseParams,
    selectedBatchId: 'batch-b',
    currentBatch: { id: 'batch-b', status: 'Upcoming' },
    isLoadingDetail: false,
  });
  actions = render(() => usePeriodActions());

  assert.deepEqual(updatedScheduleFor, ['batch-b']);
  assert.equal(successes.length, 0, 'Full onSuccess must not be called when a gate fails');
  assert.equal(partials.length, 1, 'onPartialSuccess should be called');
  assert.equal(errors.length, 1, 'onError should report failed gate');
  assert.equal(actions.failedGateUpdates.length, 1);
  assert.equal(actions.failedGateUpdates[0].gateType, 'Midterm');

  // 3. Retry failed gates succeeds once Midterm gate recovers
  midtermShouldFail = false;
  await actions.retryFailedGates({
    selectedBatchId: 'batch-b',
    onSuccess: (_id, msg) => successes.push(msg),
    onError: (msg) => errors.push(msg),
  });
  assert.equal(successes.length, 1, 'Retrying failed gate should call onSuccess');
});

test('[P1 #4] useScoreConfigActions saves unsaved draft changes before publishing and aborts publish if save fails', async () => {
  const { reactMock, render } = createHookRunner();
  const callOrder = [];
  let failUpdateDraft = false;
  const errors = [];

  const scoreWeights = loadTsModule('features/academic/utils/scoreWeights.ts', {
    '../types/scoreConfig.types': loadTsModule('features/academic/types/scoreConfig.types.ts'),
  });

  const { useScoreConfigActions } = loadTsModule(
    'features/academic/hooks/score-config/useScoreConfigActions.ts',
    {
      react: reactMock,
      '../../services/scoreConfigApi': {
        scoreConfigApi: {
          updateDraft: async (batchId, draftId, payload) => {
            callOrder.push(['updateDraft', batchId, draftId, payload.calculationMethod]);
            if (failUpdateDraft) {
              throw new Error('Lưu nháp thất bại');
            }
            return { id: 'draft-updated-id', status: 'Draft', batchId };
          },
          publishConfiguration: async (batchId, draftId) => {
            callOrder.push(['publish', batchId, draftId]);
            return { id: draftId, status: 'Published', batchId };
          },
        },
        getScoreConfigErrorMessage: (err) => err?.message ?? 'Error',
      },
      '../../utils/scoreWeights': scoreWeights,
    },
  );

  const runPublish = async () => {
    const hook = render(() => useScoreConfigActions());
    await hook.handlePublish({
      selectedBatchId: 'batch-1',
      currentConfig: {
        id: 'draft-1',
        batchId: 'batch-1',
        status: 'Draft',
        calculationMethod: 'Percentage',
        components: [],
      },
      isLoadingConfig: false,
      calculationMethod: 'Percentage',
      components: [
        { componentType: 'Advisor', displayName: 'HD', weightOrCoefficient: 50, orderIndex: 1 },
        { componentType: 'Council', displayName: 'HĐ', weightOrCoefficient: 50, orderIndex: 2 },
      ],
      totalWeight: 100,
      isDirty: true,
      isValid: true,
      onSuccess: () => {},
      onError: (msg) => errors.push(msg),
    });
  };

  await runPublish();
  assert.deepEqual(callOrder, [
    ['updateDraft', 'batch-1', 'draft-1', 'Percentage'],
    ['publish', 'batch-1', 'draft-updated-id'],
  ]);

  // If updateDraft fails, publishConfiguration must NOT be called
  callOrder.length = 0;
  failUpdateDraft = true;
  await runPublish();
  assert.deepEqual(callOrder, [['updateDraft', 'batch-1', 'draft-1', 'Percentage']]);
  assert.ok(errors.some((e) => e.includes('Lưu nháp thất bại')));
});

test('[P1 #6 & P2 #7] thesis APIs preserve HTTP errors without fake fallback data and forward pagination/filter parameters', async () => {
  const { classifyThesisError } = loadTsModule('features/thesis/utils/thesisErrors.ts', {
    axios: {
      default: {
        isAxiosError: (err) => Boolean(err?.isAxiosError),
      },
    },
  });

  assert.equal(classifyThesisError({ isAxiosError: true, response: { status: 403 } }), 'forbidden');
  assert.equal(classifyThesisError({ isAxiosError: true, response: { status: 404 } }), 'not_found');
  assert.equal(classifyThesisError(new Error('Network error')), 'load_error');

  // syllabusApi.getGroupDetail must rethrow 403 instead of fabricating Confirmed group
  const { syllabusApi } = loadTsModule('features/thesis/services/syllabusApi.ts', {
    './submissionUploadHelper': {
      buildUploadProgressHandler: () => undefined,
    },
    '@/shared/api': {
      httpClient: {
        get: async () => {
          const err = new Error('Forbidden');
          err.isAxiosError = true;
          err.response = { status: 403 };
          throw err;
        },
      },
    },
  });

  await assert.rejects(
    () => syllabusApi.getGroupDetail('grp-1'),
    (err) => err.response?.status === 403,
  );

  // syllabusReviewApi.getReviewQueue forwards server params and preserves PagedResult metadata
  const capturedParams = [];
  const { syllabusReviewApi } = loadTsModule('features/thesis/services/syllabusReviewApi.ts', {
    '@/shared/api': {
      httpClient: {
        get: async (url, config) => {
          capturedParams.push({ url, params: config?.params });
          return {
            data: {
              success: true,
              data: {
                items: [],
                totalCount: 45,
                pageNumber: 2,
                pageSize: 20,
                totalPages: 3,
                hasPreviousPage: true,
                hasNextPage: true,
              },
            },
          };
        },
      },
    },
  });

  const queueResult = await syllabusReviewApi.getReviewQueue({
    batchId: 'batch-1',
    status: 'Submitted',
    keyword: ' AI ',
    pageNumber: 2,
    pageSize: 20,
  });

  assert.equal(capturedParams[0].url, '/api/thesis/syllabus/submissions');
  assert.deepEqual(capturedParams[0].params, {
    batchId: 'batch-1',
    status: 'Submitted',
    keyword: 'AI',
    pageNumber: 2,
    pageSize: 20,
  });
  assert.equal(queueResult.totalCount, 45);
  assert.equal(queueResult.pageNumber, 2);
  assert.equal(queueResult.totalPages, 3);
  assert.equal(queueResult.hasNextPage, true);
});
