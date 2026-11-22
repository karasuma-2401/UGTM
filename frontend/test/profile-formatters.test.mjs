import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

function loadProfileFormatters() {
  const source = readFileSync(
    new URL('../src/features/profile/utils/profileFormatters.ts', import.meta.url),
    'utf8',
  );
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  });
  const exports = {};
  new Function('require', 'exports', outputText)(
    (name) => {
      if (name === 'axios') {
        return { __esModule: true, default: { isAxiosError: (err) => Boolean(err?.isAxiosError) } };
      }
      if (name === '@/features/identity/utils/permissionLabels') {
        return {
          __esModule: true,
          permissionModuleLabel: (moduleName) =>
            moduleName === 'TopicCatalog' ? 'Danh mục đề tài' : moduleName,
        };
      }
      throw new Error(`Unexpected dependency: ${name}`);
    },
    exports,
  );
  return exports;
}

test('parseUserAgent detects desktop Chrome on Windows and mobile Safari on iPhone', () => {
  const { parseUserAgent } = loadProfileFormatters();
  const win = parseUserAgent(
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36',
  );
  assert.equal(win.deviceType, 'desktop');
  assert.equal(win.os, 'Windows');
  assert.equal(win.browser, 'Google Chrome 131');

  const iphone = parseUserAgent(
    'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile/15E148 Safari/604.1',
  );
  assert.equal(iphone.deviceType, 'mobile');
  assert.equal(iphone.os, 'iOS (iPhone)');
  assert.ok(iphone.browser.startsWith('Safari'));
});

test('isValidVietnamPhone accepts empty, 10-digit 0-prefix, and +84 prefix', () => {
  const { isValidVietnamPhone } = loadProfileFormatters();
  assert.equal(isValidVietnamPhone(''), true);
  assert.equal(isValidVietnamPhone('0901234567'), true);
  assert.equal(isValidVietnamPhone('+84901234567'), true);
  assert.equal(isValidVietnamPhone('12345'), false);
  assert.equal(isValidVietnamPhone('090123456789'), false);
});

test('computeSecurityHealth flags mustChangePassword and missing phoneNumber accurately', () => {
  const { computeSecurityHealth } = loadProfileFormatters();
  const warning = computeSecurityHealth(
    {
      id: '1',
      email: 'sv@gm.uit.edu.vn',
      fullName: 'Sinh viên A',
      roles: ['Student'],
      permissions: [],
      mustChangePassword: true,
      phoneNumber: null,
      status: 'Active',
    },
    1,
  );
  assert.equal(warning.tone, 'error');
  assert.equal(warning.passedCount, 2);

  const healthy = computeSecurityHealth(
    {
      id: '1',
      email: 'sv@gm.uit.edu.vn',
      fullName: 'Sinh viên A',
      roles: ['Student'],
      permissions: [],
      mustChangePassword: false,
      phoneNumber: '0901234567',
      status: 'Active',
      passwordChangedAt: '2026-10-15T00:00:00Z',
    },
    2,
  );
  assert.equal(healthy.tone, 'success');
  assert.equal(healthy.passedCount, 4);
});

test('groupUserPermissions groups permissionDetails by module label', () => {
  const { groupUserPermissions } = loadProfileFormatters();
  const grouped = groupUserPermissions({
    id: '1',
    email: 'a@uit.edu.vn',
    fullName: 'Test',
    roles: ['Lecturer'],
    permissions: ['topics.read'],
    permissionDetails: [
      { permissionCode: 'topics.read', permissionName: 'Xem đề tài', module: 'TopicCatalog' },
    ],
  });
  assert.equal(grouped.length, 1);
  assert.equal(grouped[0].moduleLabel, 'Danh mục đề tài');
  assert.equal(grouped[0].items[0].permissionName, 'Xem đề tài');
});

test('buildContributionWeeks generates 16+ Monday-Sunday columns and maps counts/alerts', () => {
  const { buildContributionWeeks } = loadProfileFormatters();
  const ref = new Date('2026-11-22T07:30:00Z');
  const weeks = buildContributionWeeks(
    [
      { date: '2026-11-22', totalCount: 5, successCount: 4, alertCount: 1 },
      { date: '2026-11-21', totalCount: 1, successCount: 1, alertCount: 0 },
    ],
    16,
    ref,
  );
  assert.ok(weeks.length >= 16);
  for (const w of weeks) {
    assert.equal(w.cells.length, 7);
  }
  const allCells = weeks.flatMap((w) => w.cells);
  const todayCell = allCells.find((c) => c.date === '2026-11-22');
  assert.ok(todayCell);
  assert.equal(todayCell.inRange, true);
  assert.equal(todayCell.level, 3);
  assert.equal(todayCell.alertCount, 1);
  const yestCell = allCells.find((c) => c.date === '2026-11-21');
  assert.ok(yestCell);
  assert.equal(yestCell.level, 1);
});

test('groupActivityByMonthAndAction groups logs by Vietnam month and action cluster', () => {
  const { groupActivityByMonthAndAction } = loadProfileFormatters();
  const grouped = groupActivityByMonthAndAction([
    {
      id: 1,
      action: 'Identity.Authentication.Login',
      outcome: 'Success',
      timestamp: '2026-11-22T06:00:00Z',
    },
    {
      id: 2,
      action: 'Identity.Authentication.Login',
      outcome: 'Failed',
      timestamp: '2026-11-21T18:00:00Z',
    },
    {
      id: 3,
      action: 'Identity.Account.PasswordChanged',
      outcome: 'Success',
      timestamp: '2026-10-15T10:00:00Z',
    },
  ]);

  assert.equal(grouped.length, 2);
  assert.equal(grouped[0].monthKey, '2026-11');
  assert.equal(grouped[0].monthTitle, 'Tháng 11');
  assert.equal(grouped[0].clusters.length, 1);
  assert.equal(grouped[0].clusters[0].items.length, 2);
  assert.equal(grouped[0].clusters[0].alertCount, 1);
  assert.equal(grouped[1].monthKey, '2026-10');
});
