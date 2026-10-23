import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';

function load(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/features/topics/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
    return { __esModule: true, ...dependencies[name] };
  }, exports);
  return exports;
}

const types = load('types/advising.types.ts');
const helpers = load('advising.ts');
const adapter = load('services/advisingDemo.ts');
const data = await adapter.loadAdvisingDemo();

function fixture(queryOverrides = {}, pageProps = {}) {
  const state = [];
  let cursor = 0;
  const refetchCalls = [];
  const useState = (initial) => {
    const index = cursor++;
    if (!(index in state)) state[index] = initial;
    return [
      state[index],
      (value) => {
        state[index] = typeof value === 'function' ? value(state[index]) : value;
      },
    ];
  };
  const dependencies = {
    react: { ...React, useState },
    'react/jsx-runtime': jsxRuntime,
    '@tanstack/react-query': {
      useQuery: () => ({
        data,
        isPending: false,
        isError: false,
        isFetching: false,
        refetch: () => refetchCalls.push(true),
        ...queryOverrides,
      }),
    },
    'react-router-dom': { Link: 'Link' },
    'lucide-react': icons,
    '@/shared/auth': {
      useAuth: () => ({ user: { id: 'lecturer', fullName: 'Giảng viên thử nghiệm' } }),
    },
    '../components/TopicStatusBadge': { TopicStatusBadge: 'TopicStatusBadge' },
    '../types/topic.types': {
      TOPIC_STATUS_LABELS: {
        Published: 'Đã công bố',
        Approved: 'Đã duyệt',
        Submitted: 'Chờ duyệt',
        Closed: 'Đã đóng',
      },
    },
    '../types/advising.types': types,
    '../services/advisingDemo': adapter,
    '../advising': helpers,
  };
  for (const [file, names] of Object.entries({
    button: ['Button'],
    input: ['Input'],
    select: ['Select'],
    badge: ['Badge'],
    'status-badge': ['StatusBadge'],
    card: ['Card', 'CardHeader', 'CardTitle'],
    dialog: ['Dialog'],
    'statistics-bar': ['StatisticsBar'],
    'list-card-header': ['ListCardHeader'],
    'table-pagination': ['TablePagination'],
    'table-skeleton': ['TableSkeleton'],
    'info-cell': ['InfoCell'],
  })) {
    dependencies[`@/components/ui/${file}`] = Object.fromEntries(names.map((name) => [name, name]));
  }
  const { TopicAdvisingPage } = load('pages/TopicAdvisingPage.tsx', dependencies);
  return {
    render: () => {
      cursor = 0;
      return TopicAdvisingPage(pageProps);
    },
    refetchCalls,
  };
}

function nodes(tree) {
  if (Array.isArray(tree)) return tree.flatMap(nodes);
  if (!React.isValidElement(tree)) return [];
  return [tree, ...nodes(tree.props.children)];
}
function text(tree) {
  if (Array.isArray(tree)) return tree.map(text).join(' ');
  if (React.isValidElement(tree)) return text(tree.props.children);
  return typeof tree === 'string' || typeof tree === 'number' ? String(tree) : '';
}
const find = (tree, predicate) => {
  const node = nodes(tree).find(predicate);
  assert.ok(node, 'Expected UI element was not rendered');
  return node;
};
const clickTab = (tree, label) =>
  find(
    tree,
    (n) => n.type === 'Button' && n.props.type === 'button' && text(n) === label,
  ).props.onClick();
const tableText = (tree) => text(find(tree, (n) => n.type === 'tbody'));

test('demo adapter returns isolated data and respects aborted requests', async () => {
  const first = await adapter.loadAdvisingDemo();
  first.topics.length = 0;
  assert.equal((await adapter.loadAdvisingDemo()).topics.length, 8);
  const controller = new AbortController();
  controller.abort();
  await assert.rejects(adapter.loadAdvisingDemo(controller.signal), { name: 'AbortError' });
});

test('individual registration is immediately effective; only the second student receives an invitation', () => {
  for (const group of data.registrations) {
    assert.equal(
      group.students.filter((student) => student.registrationRole === 'Registrant').length,
      1,
    );
    assert.equal(
      group.students.find((student) => student.registrationRole === 'Registrant')
        .participationConfirmedAt,
      null,
    );
    if (group.students.length === 1) {
      assert.equal(group.confirmationDeadline, null);
      assert.equal(group.confirmedAt, group.registeredAt);
      assert.notEqual(group.status, 'PendingEmail');
      assert.notEqual(group.status, 'Expired');
    } else {
      assert.equal(
        group.students.filter((student) => student.registrationRole === 'Invitee').length,
        1,
      );
    }
  }
  const ui = fixture();
  let tree = ui.render();
  clickTab(tree, 'Nhóm đăng ký');
  tree = ui.render();
  find(tree, (n) => n.props['aria-label'] === 'Xem nhóm N03').props.onClick();
  tree = ui.render();
  const dialog = find(tree, (n) => n.type === 'Dialog');
  assert.match(text(dialog), /Người đăng ký/);
  assert.doesNotMatch(text(dialog), /Chờ xác nhận tham gia/);
});

test('default batch statistics distinguish pending email from the advising roster', () => {
  const tree = fixture().render();
  const items = find(tree, (n) => n.type === 'StatisticsBar').props.items;
  assert.deepEqual(
    items.map((item) => item.value),
    [7, 2, 2, 3],
  );
  assert.match(text(tree), /Dữ liệu mẫu/);
  const buttons = nodes(tree)
    .filter((n) => n.type === 'Button' || n.type === 'button')
    .map(text)
    .join(' ');
  assert.doesNotMatch(buttons, /Xác nhận tiếp nhận|Từ chối|Tải phiếu|Duyệt nhóm/);
});

test('embedded advising shows only its selected group pane without a second title or navigation', () => {
  const tree = fixture({}, { embedded: true, initialTab: 'advising' }).render();
  assert.equal(nodes(tree).filter((node) => node.type === 'h1' || node.type === 'nav').length, 0);
  assert.match(tableText(tree), /N01/);
  assert.match(tableText(tree), /N03/);
  assert.doesNotMatch(tableText(tree), /N02|N04|N05|N06|N07/);
  assert.equal(
    nodes(tree).some((node) => node.props['aria-label'] === 'Tải lại danh sách nhóm'),
    false,
  );
});

test('advising tab only shows confirmed, unfinished groups, including after switching batches', () => {
  const ui = fixture();
  let tree = ui.render();
  clickTab(tree, 'Đang hướng dẫn');
  tree = ui.render();
  assert.match(tableText(tree), /N01/);
  assert.match(tableText(tree), /N03/);
  assert.doesNotMatch(tableText(tree), /N02|N04|N05|N06|N07/);
  find(
    tree,
    (n) => n.type === 'Select' && n.props['aria-label'] === 'Lọc theo đợt KLTN',
  ).props.onChange('2025-2');
  tree = ui.render();
  find(tree, (n) => n.type === 'form').props.onSubmit({ preventDefault() {} });
  tree = ui.render();
  assert.match(text(tree), /Không có\s+nhóm\s+phù hợp/);
});

test('explicit status filter opens the email queue and changing tabs clears incompatible status', () => {
  const ui = fixture();
  let tree = ui.render();
  assert.ok(
    find(tree, (n) => n.type === 'StatisticsBar').props.items.every(
      (item) => typeof item.onClick === 'function',
    ),
  );
  clickTab(tree, 'Nhóm đăng ký');
  tree = ui.render();
  find(
    tree,
    (n) => n.type === 'Select' && n.props['aria-label'] === 'Lọc trạng thái',
  ).props.onChange('PendingEmail');
  tree = ui.render();
  find(tree, (n) => n.type === 'form').props.onSubmit({ preventDefault() {} });
  tree = ui.render();
  assert.match(tableText(tree), /N02/);
  assert.match(tableText(tree), /N04/);
  assert.doesNotMatch(tableText(tree), /N01|N03|N05|N06/);
  clickTab(tree, 'Danh sách đề tài');
  tree = ui.render();
  assert.equal(
    find(tree, (n) => n.type === 'Select' && n.props['aria-label'] === 'Lọc trạng thái').props
      .value,
    '',
  );
});

test('advising statistics switch panes and preserve batch when embedded in the unified route', () => {
  const transitions = [];
  const ui = fixture(
    {},
    {
      embedded: true,
      initialTab: 'advising',
      initialBatchId: '2025-2',
      onQuickFilter: (...args) => transitions.push(args),
    },
  );
  const tree = ui.render();
  const items = find(tree, (node) => node.type === 'StatisticsBar').props.items;
  for (const key of ['topics', 'pending', 'advising', 'students'])
    items.find((item) => item.key === key).onClick();
  assert.deepEqual(transitions, [
    ['topics', '', '2025-2'],
    ['registrations', 'PendingEmail', '2025-2'],
    ['advising', '', '2025-2'],
    ['advising', '', '2025-2'],
  ]);
  const pending = fixture(
    {},
    { embedded: true, initialTab: 'registrations', initialStatus: 'PendingEmail' },
  ).render();
  assert.equal(find(pending, (node) => node.type === 'TablePagination').props.totalCount, 2);
});

test('search applies on submit, finds unaccented names and student IDs, and reset restores results', () => {
  const ui = fixture();
  let tree = ui.render();
  clickTab(tree, 'Nhóm đăng ký');
  tree = ui.render();
  find(tree, (n) => n.type === 'Input').props.onChange({ target: { value: 'nguyen minh duc' } });
  tree = ui.render();
  assert.equal(find(tree, (n) => n.type === 'TablePagination').props.totalCount, 6);
  find(tree, (n) => n.type === 'form').props.onSubmit({ preventDefault() {} });
  tree = ui.render();
  assert.match(tableText(tree), /N01/);
  assert.equal(find(tree, (n) => n.type === 'TablePagination').props.totalCount, 1);
  find(tree, (n) => n.type === 'Input').props.onChange({ target: { value: '24521606' } });
  tree = ui.render();
  find(tree, (n) => n.type === 'form').props.onSubmit({ preventDefault() {} });
  tree = ui.render();
  assert.match(tableText(tree), /N02/);
  find(tree, (n) => n.type === 'Button' && text(n) === 'Bỏ lọc').props.onClick();
  tree = ui.render();
  assert.equal(find(tree, (n) => n.type === 'TablePagination').props.totalCount, 7);
});

test('pagination and topic details preserve registrations including cancelled history', () => {
  const ui = fixture({
    data: {
      ...data,
      topics: [
        ...data.topics,
        ...data.topics.map((topic) => ({ ...topic, id: `${topic.id}-copy` })),
      ],
    },
  });
  let tree = ui.render();
  find(tree, (n) => n.type === 'TablePagination').props.onPageChange(2);
  tree = ui.render();
  assert.match(tableText(tree), /DT-006/);
  assert.doesNotMatch(tableText(tree), /DT-001/);
  find(tree, (n) => n.type === 'TablePagination').props.onPageChange(1);
  tree = ui.render();
  find(tree, (n) => n.props['aria-label'] === 'Xem đề tài DT-004').props.onClick();
  tree = ui.render();
  const dialog = find(tree, (n) => n.type === 'Dialog');
  assert.match(text(dialog), /N04/);
  assert.match(text(dialog), /N06/);
  assert.match(text(dialog), /Khoa hủy theo đề nghị/);
  dialog.props.onClose();
  assert.equal(nodes(ui.render()).filter((n) => n.type === 'Dialog').length, 0);
});

test('page size selector, visible rows and range agree for six groups and after changing size', () => {
  const ui = fixture({}, { embedded: true, initialTab: 'registrations' });
  let tree = ui.render();
  const selector = find(tree, (node) => node.props.id === 'advising-page-size');
  assert.equal(selector.props.value, 10);
  assert.ok(
    nodes(selector).some(
      (node) => node.type === 'option' && node.props.value === selector.props.value,
    ),
  );
  const pagination = find(tree, (node) => node.type === 'TablePagination').props;
  assert.equal(pagination.pageSize, 10);
  assert.equal(pagination.totalCount, 6);
  assert.equal(
    nodes(find(tree, (node) => node.type === 'tbody')).filter((node) => node.type === 'tr').length,
    6,
  );
  const { TablePagination } = load('../../components/ui/table-pagination.tsx', {
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    './button': { Button: 'Button' },
    '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
  });
  const footer = TablePagination(pagination);
  assert.match(text(footer).replace(/\s+/g, ''), /Hiểnthị1–6\/6nhóm/);
  assert.equal(
    find(footer, (node) => node.type === 'Button' && text(node) === 'Sau').props.disabled,
    true,
  );

  const largerData = {
    ...data,
    registrations: [
      ...data.registrations,
      ...data.registrations.map((group) => ({ ...group, id: `${group.id}-copy` })),
    ],
  };
  const larger = fixture({ data: largerData }, { embedded: true, initialTab: 'registrations' });
  tree = larger.render();
  find(tree, (node) => node.type === 'TablePagination').props.onPageChange(2);
  tree = larger.render();
  assert.equal(
    nodes(find(tree, (node) => node.type === 'tbody')).filter((node) => node.type === 'tr').length,
    2,
  );
  find(tree, (node) => node.props.id === 'advising-page-size').props.onChange({
    target: { value: '20' },
  });
  tree = larger.render();
  assert.equal(find(tree, (node) => node.type === 'TablePagination').props.pageNumber, 1);
  assert.equal(find(tree, (node) => node.type === 'TablePagination').props.pageSize, 20);
  assert.equal(
    nodes(find(tree, (node) => node.type === 'tbody')).filter((node) => node.type === 'tr').length,
    12,
  );
});

test('group details show email confirmation per student and faculty contact never submits a cancellation', () => {
  const ui = fixture();
  let tree = ui.render();
  clickTab(tree, 'Nhóm đăng ký');
  tree = ui.render();
  find(tree, (n) => n.props['aria-label'] === 'Xem nhóm N02').props.onClick();
  tree = ui.render();
  let dialog = find(tree, (n) => n.type === 'Dialog');
  assert.match(text(dialog), /24521606@gm.uit.edu.vn/);
  assert.match(text(dialog), /Chờ xác nhận tham gia/);
  assert.match(text(dialog), /Người đăng ký/);
  dialog.props.onClose();
  tree = ui.render();
  find(tree, (n) => n.props['aria-label'] === 'Xem nhóm N01').props.onClick();
  tree = ui.render();
  find(tree, (n) => n.type === 'Button' && text(n) === 'Liên hệ Khoa').props.onClick();
  tree = ui.render();
  dialog = find(tree, (n) => n.type === 'Dialog' && n.props.title === 'Liên hệ Văn phòng Khoa');
  assert.match(text(dialog), /chưa gửi yêu cầu/);
  const textarea = find(dialog, (n) => n.type === 'textarea');
  assert.equal(textarea.props.readOnly, true);
  assert.match(textarea.props.value, /KLTN-2026-084/);
  assert.doesNotMatch(
    nodes(dialog)
      .filter((n) => n.type === 'Button')
      .map(text)
      .join(' '),
    /Gửi yêu cầu|Hủy nhóm/,
  );
});

test('loading and failed queries expose readable status and retry', () => {
  assert.ok(
    nodes(fixture({ data: undefined, isPending: true }).render()).some(
      (n) => n.type === 'TableSkeleton',
    ),
  );
  const ui = fixture({ data: undefined, isError: true });
  const tree = ui.render();
  assert.match(text(find(tree, (n) => n.props.role === 'alert')), /Không thể tải/);
  find(tree, (n) => n.type === 'Button' && text(n) === 'Thử lại').props.onClick();
  assert.equal(ui.refetchCalls.length, 1);
});
