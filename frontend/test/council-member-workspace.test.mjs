import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import axios from 'axios';
import ts from 'typescript';

function load(path, deps = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in deps, `Unexpected dependency: ${name}`);
    return deps[name];
  }, exports);
  return exports;
}

const ws = load('features/councils/utils/councilMemberWorkspace.ts');
const api = load('features/councils/services/councilMembersApi.ts', {
  axios: { __esModule: true, default: axios },
  '@/shared/api': { httpClient: {} },
});

const person = (userId, fullName, role, extra = {}) => ({
  userId,
  fullName,
  role,
  isReviewer: false,
  isAdvisorConflict: false,
  note: null,
  ...extra,
});
function council(overrides = {}) {
  return {
    id: 'c1',
    councilCode: 'HD01',
    room: 'B1.401',
    status: 'Draft',
    groupCodes: ['N01'],
    chairman: person('u1', 'An', 'Chairman'),
    secretary: person('u2', 'Bình', 'Secretary'),
    members: [person('u3', 'Chi', 'Member')],
    hasRoomConflict: false,
    hasAdvisorConflict: false,
    hasIneligibleGroups: false,
    hasReviewerRoleConflict: false,
    conflictingMemberNames: [],
    isSubmittedToAcademicAffairs: false,
    submittedAt: null,
    submissionNotes: null,
    ...overrides,
  };
}
const candidate = (userId, overrides = {}) => ({
  userId,
  fullName: `GV ${userId}`,
  email: `${userId}@uit.edu.vn`,
  userCode: `C${userId}`,
  isAlreadyInCouncil: false,
  isEligible: true,
  allowedRoles: ['Chairman', 'Secretary', 'Member'],
  conflictReason: null,
  ...overrides,
});

test('roster is derived from the council overview, so no schedule request is needed', () => {
  const roster = ws.rosterFromCouncil(council());
  assert.deepEqual(
    roster.map((m) => [m.userId, m.role]),
    [['u1', 'Chairman'], ['u2', 'Secretary'], ['u3', 'Member']],
  );
});

test('role availability explains taken leadership slots, reviewers and conflicts', () => {
  const roster = ws.rosterFromCouncil(council());
  const taken = ws.getRoleAvailability(roster, candidate('u9'), 'Chairman', 'u9');
  assert.equal(taken.allowed, false);
  assert.match(taken.reason, /Đã có Chủ tịch: An/);
  assert.equal(ws.getRoleAvailability(roster, candidate('u9'), 'Member', 'u9').allowed, true);
  // The current holder may keep their own role.
  assert.equal(ws.getRoleAvailability(roster, candidate('u1'), 'Chairman', 'u1').allowed, true);
  const reviewer = candidate('u8', { allowedRoles: ['Member'], isReviewerForCouncilGroups: true, conflictReason: 'Là GVPB' });
  assert.match(ws.getRoleAvailability([], reviewer, 'Secretary', 'u8').reason, /chỉ có thể làm Ủy viên/);
  const blocked = candidate('u7', { isEligible: false, allowedRoles: [], isAdvisorConflict: true, conflictReason: 'Là GVHD' });
  assert.match(ws.getRoleAvailability([], blocked, 'Member', 'u7').reason, /đang hướng dẫn một nhóm/);
});

test('candidate list searches by keyword and keeps blocked people visible with addable people first', () => {
  const list = [
    candidate('b', { fullName: 'Bùi Hòa', isEligible: false, allowedRoles: [], conflictReason: 'Trùng lịch' }),
    candidate('a', { fullName: 'An Nguyễn', isAlreadyInCouncil: true, currentRoleInCouncil: 'Member' }),
    candidate('c', { fullName: 'Cao Minh' }),
    candidate('d', { fullName: 'Đinh Lan', allowedRoles: ['Member'], conflictReason: 'GVPB' }),
  ];
  assert.deepEqual(ws.filterCandidates(list, '').map((x) => x.userId), ['c', 'd', 'b', 'a']);
  assert.deepEqual(ws.filterCandidates(list, 'cao').map((x) => x.userId), ['c']);
  assert.deepEqual(ws.filterCandidates(list, 'ĐINH').map((x) => x.userId), ['d']);
  assert.deepEqual(ws.filterCandidates(list, 'khong co'), []);
});

test('member picker suggests group reviewers first, still searches other lecturers, and never shows group advisors', () => {
  const list = [
    candidate('other', { fullName: 'An Giảng viên' }),
    candidate('advisor', { fullName: 'An GVHD', isAdvisorConflict: true, isEligible: false, allowedRoles: [] }),
    candidate('reviewer', { fullName: 'Bình GVPB', isReviewerForCouncilGroups: true, allowedRoles: ['Member'] }),
    candidate('busyReviewer', { fullName: 'Chi GVPB', isReviewerForCouncilGroups: true, isScheduleConflict: true, isEligible: false, allowedRoles: [] }),
    candidate('assignedReviewer', { fullName: 'Dũng GVPB', isReviewerForCouncilGroups: true, allowedRoles: ['Member'], isAlreadyInCouncil: true }),
  ];
  const roster = [{ userId: 'assignedReviewer', fullName: 'Dũng GVPB', role: 'Member', isReviewer: true }];

  assert.deepEqual(ws.pickerCandidates(list, '', 'Member', roster).map((x) => x.userId), ['reviewer', 'busyReviewer']);
  assert.deepEqual(ws.pickerCandidates(list, 'g', 'Member', roster).map((x) => x.userId), ['reviewer', 'busyReviewer', 'other', 'assignedReviewer']);
  assert.deepEqual(ws.pickerCandidates(list, 'gvhd', 'Member', roster), []);
  assert.deepEqual(ws.pickerCandidates(list, '', 'Chairman', roster), []);
  assert.equal(ws.pickerCandidates(list, 'gvpb', 'Chairman', roster).some((x) => x.userId === 'advisor'), false);
});

test('submit readiness lists everything missing and never allows resubmitting or approved councils', () => {
  const full = council();
  assert.equal(ws.evaluateSubmitReadiness(full, ws.rosterFromCouncil(full)).canSubmit, true);
  const bare = council({ chairman: null, secretary: null, members: [], groupCodes: [], hasRoomConflict: true });
  const result = ws.evaluateSubmitReadiness(bare, []);
  assert.equal(result.canSubmit, false);
  assert.equal(result.hasChairman, false);
  assert.equal(result.conflicts.length, 1);
  assert.match(result.missing.join(' '), /Chủ tịch.*Thư ký.*nhóm bảo vệ.*Phòng B1\.401/);
  const ineligible = council({ hasIneligibleGroups: true });
  const groupResult = ws.evaluateSubmitReadiness(ineligible, ws.rosterFromCouncil(ineligible));
  assert.equal(groupResult.canSubmit, false);
  assert.match(groupResult.groupEligibilityIssue, /kiểm tra kết quả phản biện/);
  assert.deepEqual(groupResult.conflicts, []);
  const submitted = council({ isSubmittedToAcademicAffairs: true });
  assert.equal(ws.evaluateSubmitReadiness(submitted, ws.rosterFromCouncil(submitted)).canSubmit, false);
  const active = council({ status: 'Active' });
  assert.equal(ws.evaluateSubmitReadiness(active, ws.rosterFromCouncil(active)).canSubmit, false);
});

test('applying a server roster withdraws the submission and recomputes conflict flags', () => {
  const conflicted = council({
    isSubmittedToAcademicAffairs: true,
    submittedAt: '2026-11-01T00:00:00Z',
    hasAdvisorConflict: true,
    conflictingMemberNames: ['Chi (C3)'],
    members: [person('u3', 'Chi', 'Member', { isAdvisorConflict: true })],
  });
  const withoutChi = ws.rosterFromCouncil(conflicted).filter((m) => m.userId !== 'u3');
  const next = ws.applyRosterToCouncil(conflicted, withoutChi);
  assert.equal(next.isSubmittedToAcademicAffairs, false);
  assert.equal(next.submittedAt, null);
  assert.equal(next.hasAdvisorConflict, false);
  assert.deepEqual(next.conflictingMemberNames, []);
  assert.equal(next.members.length, 0);
  assert.equal(next.membersSummary, 'Chủ tịch · Thư ký');
  // The React Query cache entry must not be mutated in place.
  assert.equal(conflicted.isSubmittedToAcademicAffairs, true);
  assert.equal(conflicted.members.length, 1);
});

test('an add response contains only the new member and is merged into the roster and candidates', () => {
  const roster = ws.rosterFromCouncil(council());
  const merged = ws.mergeAddedMembers(roster, [
    { id: 'm', councilId: 'c1', userId: 'u9', role: 'Member', fullName: 'Dũng' },
  ]);
  assert.deepEqual(merged.map((m) => m.userId), ['u1', 'u2', 'u3', 'u9']);
  const candidates = ws.applyRosterToCandidates([candidate('u9'), candidate('u5')], merged);
  assert.equal(candidates[0].isAlreadyInCouncil, true);
  assert.equal(candidates[0].currentRoleInCouncil, 'Member');
  assert.equal(candidates[1].isAlreadyInCouncil, false);
});

test('memberError gives a specific message and recovery step for each failure type', () => {
  const fail = (status, message) =>
    api.memberError(
      new axios.AxiosError(
        'x',
        'ERR',
        undefined,
        undefined,
        status ? { status, data: message ? { error: { message } } : {} } : undefined,
      ),
    );
  assert.equal(fail(409, 'Hội đồng chỉ được có tối đa 01 Chủ tịch.'), 'Hội đồng chỉ được có tối đa 01 Chủ tịch.');
  assert.match(fail(403), /không có quyền/);
  assert.match(fail(401), /đăng nhập lại/);
  assert.match(fail(404), /không còn tồn tại/);
  assert.match(fail(409), /Làm mới/);
  assert.match(fail(500), /thử lại sau/);
  assert.match(fail(undefined), /Không kết nối được máy chủ/);
});

test('member mutations return the roster payload so the UI can update without refetching', async () => {
  const members = [{ id: 'm1', councilId: 'c1', userId: 'u1', role: 'Chairman', fullName: 'An' }];
  const client = Object.fromEntries(
    ['post', 'put', 'delete'].map((method) => [method, async () => ({ data: { success: true, data: members } })]),
  );
  const real = load('features/councils/services/councilMembersApi.ts', {
    axios: { __esModule: true, default: axios },
    '@/shared/api': { httpClient: client },
  }).councilMembersApi;
  assert.deepEqual(await real.add('c1', { userId: 'u1', role: 'Chairman' }), members);
  assert.deepEqual(await real.replace('c1', [{ userId: 'u1', role: 'Chairman' }]), members);
  assert.deepEqual(await real.remove('c1', 'u1'), members);
});
