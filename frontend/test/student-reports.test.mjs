import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import { renderToStaticMarkup } from 'react-dom/server';
import * as icons from 'lucide-react';

function loadModule(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name} in ${path}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
  );
  return exports;
}

const commonUiDeps = {
  react: React,
  'react/jsx-runtime': jsxRuntime,
  'lucide-react': icons,
  'react-router-dom': {
    Link: ({ to, children, ...rest }) => React.createElement('a', { href: to, ...rest }, children),
    useLocation: () => ({ pathname: '/thesis/proposal' }),
  },
  '@/lib/utils': {
    cn: (...classes) => classes.filter(Boolean).join(' '),
  },
  '@/components/ui/button': {
    Button: ({ children, type = 'button', onClick, disabled, isLoading, loadingText, leftIcon, asChild, className }) => {
      if (asChild && React.isValidElement(children)) {
        return children;
      }
      return React.createElement(
        'button',
        { type, onClick, disabled, className },
        leftIcon,
        isLoading ? loadingText : children,
      );
    },
  },
  '@/components/ui/card': {
    Card: ({ children, className }) => React.createElement('section', { className }, children),
  },
  '@/components/ui/dialog': {
    Dialog: ({ open, children }) => (open ? React.createElement('dialog', { open: true }, children) : null),
  },
  '@/components/ui/input': {
    Input: (props) => React.createElement('input', props),
  },
  '@/components/ui/skeleton': {
    Skeleton: ({ className }) => React.createElement('span', { 'data-skeleton': 'true', className }),
  },
  '@/components/ui/status-badge': {
    StatusBadge: ({ tone, children, className }) =>
      React.createElement('span', { 'data-status-tone': tone, className }, children),
  },
  '@/components/ui/avatar': {
    Avatar: ({ name, className }) =>
      React.createElement('span', { 'data-avatar': name, className }, name.slice(0, 2)),
  },
  '@/components/ui/rich-text-content': {
    RichTextContent: ({ content, emptyFallback }) =>
      React.createElement('div', { 'data-rich-content': 'true' }, content || emptyFallback),
  },
  '@/components/ui/inline-alert': {
    InlineAlert: ({ tone, title, action, children }) =>
      React.createElement(
        'div',
        { role: 'alert', 'data-tone': tone },
        title && React.createElement('p', null, title),
        children,
        action,
      ),
  },
  '@/components/ui/selection-indicator': {
    SelectionIndicator: ({ continuityKey }) =>
      React.createElement('span', { 'data-selection-indicator': continuityKey }),
  },
  '@/components/ui/navigation-tabs': {
    NavigationTabs: ({ tabs, activeId, continuityKey }) =>
      React.createElement(
        'nav',
        { 'data-continuity-key': continuityKey, 'data-active-id': activeId },
        tabs.map((tab) =>
          React.createElement(
            'a',
            {
              key: tab.id,
              href: tab.href,
              'aria-current': tab.id === activeId ? 'page' : undefined,
            },
            tab.label,
            tab.badge,
          ),
        ),
      ),
  },
  '@/components/ui/page-header': {
    PageHeader: ({ title, description, navigation, children, accent }) =>
      React.createElement(
        'header',
        { 'data-accent': String(Boolean(accent)) },
        React.createElement('h1', null, title),
        description && React.createElement('div', null, description),
        children,
        navigation,
      ),
  },
};

const sampleGroupDetail = {
  groupId: 'grp-1',
  groupCode: 'Nhóm G01',
  topicId: 'top-1',
  topicCode: 'KLTN-GRAPH01',
  topicTitleVi: 'Hệ thống GraphRAG tư vấn quy chế UIT',
  batchName: 'KLTN HK2 2025-2026',
  advisor: { advisorId: 'adv-1', fullName: 'TS. Phạm Minh Khang' },
  members: [
    { studentId: 's1', userCode: '22520101', fullName: 'Nguyễn Minh Khôi', email: 'k@uit', confirmationStatus: 'Confirmed' },
    { studentId: 's2', userCode: '22520102', fullName: 'Trần Ngọc Thảo', email: 't@uit', confirmationStatus: 'Confirmed' },
  ],
};

test('ThesisSubmissionNavigation uses NavigationTabs with thesis-submission-tabs continuityKey and inline badges', () => {
  const { ThesisSubmissionNavigation } = loadModule(
    'features/thesis/components/navigation/ThesisSubmissionNavigation.tsx',
    commonUiDeps,
  );
  const gates = [
    { id: 'milestones', stepLabel: 'Thường kỳ', title: 'Báo cáo tiến độ', href: '/thesis/milestones', statusLabel: '2/4 mốc đạt', shortLabel: '2/4', state: 'revision', isLoading: false },
    { id: 'proposal', stepLabel: 'Cổng 1', title: 'Đề cương chi tiết', href: '/thesis/proposal', statusLabel: 'Đã đạt', shortLabel: 'Đạt', state: 'completed', isLoading: false },
    { id: 'midterm', stepLabel: 'Cổng 2', title: 'Báo cáo giữa kỳ', href: '/thesis/midterm', statusLabel: 'Chờ thẩm định', shortLabel: 'Chờ duyệt', state: 'pending', isLoading: false },
    { id: 'final-report', stepLabel: 'Cổng 3', title: 'Báo cáo cuối kỳ', href: '/thesis/final-report', statusLabel: 'Chờ Cổng 2', shortLabel: 'Chờ Cổng 2', state: 'locked', isLoading: false },
  ];
  const html = renderToStaticMarkup(React.createElement(ThesisSubmissionNavigation, { gates }));
  assert.match(html, /data-continuity-key="thesis-submission-tabs"/);
  assert.match(html, /data-active-id="proposal"/);
  assert.match(html, /Đạt/);
  assert.doesNotMatch(html, /2\/4/);
  assert.doesNotMatch(html, /Chờ duyệt/);
  assert.doesNotMatch(html, /Chờ Cổng 2/);
});

test('ThesisGroupDossierCard and FinalReportSummaryCard render Avatar member rows without fake mock fallbacks', () => {
  const sharedDossier = loadModule(
    'features/thesis/components/shared/ThesisGroupDossierCard.tsx',
    commonUiDeps,
  );
  const { FinalReportSummaryCard } = loadModule(
    'features/thesis/components/finalReport/FinalReportSummaryCard.tsx',
    {
      ...commonUiDeps,
      '../shared': sharedDossier,
    },
  );

  const emptyHtml = renderToStaticMarkup(
    React.createElement(FinalReportSummaryCard, { groupDetail: null, isLoading: true }),
  );
  assert.doesNotMatch(emptyHtml, /KLTN-2026-084|Trần Hoàng Long|Nguyễn Minh Đức|Trần Mai Anh/);
  assert.match(emptyHtml, /data-skeleton="true"/);

  const errorHtml = renderToStaticMarkup(
    React.createElement(FinalReportSummaryCard, { groupDetail: null, isError: true }),
  );
  assert.match(errorHtml, /Chưa tải được hồ sơ nhóm/);
  assert.doesNotMatch(errorHtml, /Trần Hoàng Long/);

  const populatedHtml = renderToStaticMarkup(
    React.createElement(FinalReportSummaryCard, { groupDetail: sampleGroupDetail }),
  );
  assert.match(populatedHtml, /Hồ sơ cuối kỳ/);
  assert.match(populatedHtml, /data-avatar="Nguyễn Minh Khôi"/);
  assert.match(populatedHtml, /data-avatar="Trần Ngọc Thảo"/);
  assert.match(populatedHtml, /2 sinh viên/);
  assert.match(populatedHtml, /TS\. Phạm Minh Khang/);
});

test('StudentMilestoneBriefCard renders RichTextContent and accurate Accepted message without feedback', () => {
  const formatters = loadModule('features/thesis/utils/milestoneFormatters.ts');
  const statusBadge = loadModule('features/thesis/components/milestone/MilestoneStatusBadge.tsx', commonUiDeps);
  const { StudentMilestoneBriefCard } = loadModule(
    'features/thesis/components/milestone/StudentMilestoneBriefCard.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
      './MilestoneStatusBadge': statusBadge,
    },
  );

  const acceptedHtml = renderToStaticMarkup(
    React.createElement(StudentMilestoneBriefCard, {
      milestoneIndex: 0,
      currentStatus: 'Accepted',
      currentVersionNo: 1,
      milestone: {
        id: 'm1',
        groupId: 'grp-1',
        title: 'Mốc 1 — Khởi tạo',
        description: '**Hoàn thiện** schema `Neo4j`',
        deadlineUtc: '2026-11-20T16:59:00Z',
        createdBy: 'adv-1',
        createdAtUtc: '2026-11-01T00:00:00Z',
      },
      versions: [
        {
          versionNo: 1,
          gitUrl: 'https://github.com/uit/repo',
          submittedBy: 's1',
          submittedAtUtc: '2026-11-04T08:00:00Z',
          isLate: false,
          status: 'Accepted',
          review: null,
        },
      ],
    }),
  );

  assert.match(acceptedHtml, /data-rich-content="true"/);
  assert.match(acceptedHtml, /GVHD đã xác nhận.*Đạt/);
  assert.doesNotMatch(acceptedHtml, /Chưa có nhận xét từ GVHD.*Vui lòng nộp báo cáo/);
});

test('FinalReportVersionCard renders full multi-version history timeline without truncating older versions', () => {
  const formatters = loadModule('features/thesis/utils/milestoneFormatters.ts');
  const gallery = loadModule(
    'features/thesis/components/shared/SubmissionAttachmentGallery.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
    },
  );
  const timeline = loadModule(
    'features/thesis/components/shared/VersionHistoryTimeline.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
      './SubmissionAttachmentGallery': gallery,
    },
  );
  const { FinalReportVersionCard } = loadModule(
    'features/thesis/components/finalReport/FinalReportVersionCard.tsx',
    {
      ...commonUiDeps,
      '../../services/finalReportApi': { finalReportApi: { getAttachmentUrl: async () => ({ downloadUrl: '' }) } },
      '../shared': timeline,
    },
  );

  const html = renderToStaticMarkup(
    React.createElement(FinalReportVersionCard, {
      currentStatus: 'Submitted',
      currentVersionNo: 2,
      onDownload() {},
      versions: [
        {
          versionNo: 1,
          file: {
            id: 'f1',
            fileName: 'Bao-cao-cuoi-ky-v1.docx',
            contentType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
            fileSize: 1024000,
            fileExtension: '.docx',
            checksumSha256: 'abc',
            uploadedBy: 's1',
            uploadedAtUtc: '2026-11-10T08:00:00Z',
          },
        },
        {
          versionNo: 2,
          file: {
            id: 'f2',
            fileName: 'Bao-cao-cuoi-ky-v2-final.pdf',
            contentType: 'application/pdf',
            fileSize: 2048000,
            fileExtension: '.pdf',
            checksumSha256: 'def',
            uploadedBy: 's1',
            uploadedAtUtc: '2026-11-12T08:00:00Z',
          },
        },
      ],
    }),
  );

  assert.match(html, /2 phiên bản/);
  assert.match(html, /Bao-cao-cuoi-ky-v1\.docx/);
  assert.match(html, /Bao-cao-cuoi-ky-v2-final\.pdf/);
  assert.doesNotMatch(html, /Xem góp ý GVPB/);
});

test('MidtermSubmissionPage renders Hồ sơ giữa kỳ instead of Hồ sơ đề cương copy-paste bug', () => {
  const midtermSource = readFileSync(
    new URL('../src/features/thesis/pages/MidtermSubmissionPage.tsx', import.meta.url),
    'utf8',
  );
  assert.match(midtermSource, /title="Hồ sơ giữa kỳ"/);
  assert.doesNotMatch(midtermSource, /title="Hồ sơ đề cương"/);
});

test('milestoneFormatters formats Git source links cleanly and detects self-portal URLs + size deltas', () => {
  const { formatGitSourceLink, formatFileSizeDelta } = loadModule(
    'features/thesis/utils/milestoneFormatters.ts',
  );

  const githubMeta = formatGitSourceLink('https://github.com/karasuma-2401/ugtm-thesis/tree/main');
  assert.equal(githubMeta.hostLabel, 'GitHub');
  assert.equal(githubMeta.displayText, 'github.com/karasuma-2401/ugtm-thesis/tree/main');
  assert.equal(githubMeta.isLocalPortalUrl, false);

  const localMeta = formatGitSourceLink('http://localhost:5173/thesis/milestones');
  assert.equal(localMeta.isLocalPortalUrl, true);
  assert.equal(localMeta.displayText, 'localhost:5173/thesis/milestones');

  assert.equal(formatFileSizeDelta(2048 * 1024, 1024 * 1024), '+1.00 MB so với bản trước');
  assert.equal(formatFileSizeDelta(512 * 1024, 1024 * 1024), '−512 KB so với bản trước');
  assert.equal(formatFileSizeDelta(1024, null), null);
});

test('VersionHistoryTimeline omits duplicated latest review when omitLatestReview is enabled', () => {
  const formatters = loadModule('features/thesis/utils/milestoneFormatters.ts');
  const gallery = loadModule(
    'features/thesis/components/shared/SubmissionAttachmentGallery.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
    },
  );
  const { VersionHistoryTimeline } = loadModule(
    'features/thesis/components/shared/VersionHistoryTimeline.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
      './SubmissionAttachmentGallery': gallery,
    },
  );

  const html = renderToStaticMarkup(
    React.createElement(VersionHistoryTimeline, {
      title: 'Lịch sử nộp sản phẩm',
      omitLatestReview: true,
      onDownload() {},
      items: [
        {
          key: 2,
          versionNo: 2,
          fileId: 'f2',
          fileName: 'Bao-cao-v2.pdf',
          fileSize: 2048000,
          timestampUtc: '2026-11-12T08:00:00Z',
          statusBadge: React.createElement('span', null, 'Đạt'),
          review: {
            decisionLabel: 'GVHD xác nhận Đạt',
            tone: 'success',
            feedback: 'Latest review already shown above',
            reviewedAtUtc: '2026-11-12T09:00:00Z',
          },
        },
        {
          key: 1,
          versionNo: 1,
          fileId: 'f1',
          fileName: 'Bao-cao-v1.docx',
          fileSize: 1024000,
          timestampUtc: '2026-11-10T08:00:00Z',
          statusBadge: React.createElement('span', null, 'Cần chỉnh sửa'),
          review: {
            decisionLabel: 'GVHD yêu cầu chỉnh sửa',
            tone: 'warning',
            feedback: 'Historical v1 feedback remains visible',
            reviewedAtUtc: '2026-11-10T09:00:00Z',
          },
        },
      ],
    }),
  );

  assert.doesNotMatch(html, /Latest review already shown above/);
  assert.match(html, /Historical v1 feedback remains visible/);
  assert.match(html, /Bản hiện hành/);
  assert.match(html, /\+1000 KB so với bản trước/);
});

test('multi-link and evidence image upload helpers enforce limits and render in timeline', () => {
  const uploadHelper = loadModule('features/thesis/services/submissionUploadHelper.ts');
  const formatters = loadModule('features/thesis/utils/milestoneFormatters.ts');
  const gallery = loadModule(
    'features/thesis/components/shared/SubmissionAttachmentGallery.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
    },
  );
  const { VersionHistoryTimeline } = loadModule(
    'features/thesis/components/shared/VersionHistoryTimeline.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
      './SubmissionAttachmentGallery': gallery,
    },
  );

  assert.equal(uploadHelper.IMAGE_MAX_COUNT, 5);
  assert.equal(uploadHelper.PRODUCT_LINK_MAX_COUNT, 5);
  assert.equal(
    uploadHelper.validateReportFile({ name: 'hack.exe', size: 1024 }),
    'Chỉ chấp nhận tệp định dạng .pdf, .doc hoặc .docx.',
  );
  assert.equal(
    uploadHelper.validateImageAttachmentFile({
      name: 'huge.png',
      size: 6 * 1024 * 1024,
      type: 'image/png',
    }),
    '"huge.png" vượt quá giới hạn 5 MB mỗi ảnh.',
  );
  assert.equal(
    uploadHelper.validateImageAttachmentFile({
      name: 'screen.webp',
      size: 256 * 1024,
      type: 'image/webp',
    }),
    null,
  );

  assert.equal(formatters.detectLinkCategory('https://github.com/org/repo'), 'SourceCode');
  assert.equal(formatters.detectLinkCategory('https://youtu.be/demo123'), 'Demo');
  assert.equal(formatters.detectLinkCategory('https://www.figma.com/file/abc'), 'Documentation');

  const html = renderToStaticMarkup(
    React.createElement(VersionHistoryTimeline, {
      title: 'Lịch sử nộp sản phẩm',
      onDownload() {},
      items: [
        {
          key: 1,
          versionNo: 1,
          fileId: 'f1',
          fileName: 'Bao-cao-v1.pdf',
          fileSize: 1024000,
          timestampUtc: '2026-11-12T08:00:00Z',
          statusBadge: React.createElement('span', null, 'Đã nộp'),
          productLinks: [
            {
              id: 'l1',
              category: 'SourceCode',
              label: 'Backend Repo',
              url: 'https://github.com/team/ugtm-api',
              displayOrder: 0,
            },
            {
              id: 'l2',
              category: 'Demo',
              label: 'Video Demo',
              url: 'https://youtu.be/demo123',
              displayOrder: 1,
            },
          ],
          attachments: [
            {
              id: 'att-1',
              fileName: 'screenshot-ui.png',
              contentType: 'image/png',
              fileSize: 245760,
              displayOrder: 0,
              uploadedAtUtc: '2026-11-12T08:00:00Z',
            },
          ],
        },
      ],
    }),
  );

  assert.match(html, /Backend Repo/);
  assert.match(html, /Video Demo/);
  assert.match(html, /screenshot-ui\.png/);
  assert.match(html, /Ảnh minh chứng \(1\)/);
});

test('reusable BlockComposer and SubmissionBlockComposer render link and image blocks without slash input, while milestone note uses RichTextEditor', () => {
  const formatters = loadModule('features/thesis/utils/milestoneFormatters.ts');
  const uploadHelper = loadModule('features/thesis/services/submissionUploadHelper.ts');

  const blockComposerModule = loadModule('components/ui/block-composer.tsx', {
    ...commonUiDeps,
    'react-dom': {
      createPortal: (node) => node,
    },
  });

  const linksEditorModule = loadModule(
    'features/thesis/components/shared/SubmissionProductLinksEditor.tsx',
    {
      ...commonUiDeps,
      '@/components/ui/select': {
        Select: ({ value, options }) =>
          React.createElement(
            'select',
            { value },
            (options || []).map((opt) =>
              React.createElement('option', { key: opt.value, value: opt.value }, opt.label),
            ),
          ),
      },
      '../../utils/milestoneFormatters': formatters,
      '../../services/submissionUploadHelper': uploadHelper,
    },
  );

  const composerModule = loadModule(
    'features/thesis/components/shared/SubmissionBlockComposer.tsx',
    {
      ...commonUiDeps,
      'react-dom': { createPortal: (n) => n },
      '@/components/ui/block-composer': blockComposerModule,
      '@/components/ui/select': {
        Select: ({ value, options }) =>
          React.createElement(
            'select',
            { value },
            (options || []).map((opt) =>
              React.createElement('option', { key: opt.value, value: opt.value }, opt.label),
            ),
          ),
      },
      '../../utils/milestoneFormatters': formatters,
      '../../services/submissionUploadHelper': uploadHelper,
      './SubmissionProductLinksEditor': linksEditorModule,
    },
  );

  const pdfPreviewModule = loadModule(
    'features/thesis/components/shared/PdfPreviewDialog.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
    },
  );

  const dropzoneModule = loadModule(
    'features/thesis/components/shared/SubmissionDropzoneCard.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
      '../../services/submissionUploadHelper': uploadHelper,
      './PdfPreviewDialog': pdfPreviewModule,
      './ThesisFileTypeIcon': loadModule(
        'features/thesis/components/shared/ThesisFileTypeIcon.tsx',
        commonUiDeps,
      ),
    },
  );

  const { StudentMilestoneUploadCard } = loadModule(
    'features/thesis/components/milestone/StudentMilestoneUploadCard.tsx',
    {
      ...commonUiDeps,
      '@/components/ui/rich-text-editor': {
        RichTextEditor: ({ id, value, placeholder }) =>
          React.createElement('div', { 'data-richtext-editor': id, 'data-placeholder': placeholder }, value),
      },
      '../shared': {
        ...dropzoneModule,
        ...linksEditorModule,
        ...composerModule,
      },
    },
  );

  const milestoneHtml = renderToStaticMarkup(
    React.createElement(StudentMilestoneUploadCard, {
      versionNo: 1,
      selectedFile: null,
      productLinks: [
        {
          id: 'link-1',
          category: 'SourceCode',
          label: 'Frontend',
          url: 'https://github.com/team/ugtm-ui',
        },
      ],
      selectedImages: [
        new File(['img'], 'dashboard-preview.png', {
          type: 'image/png',
          lastModified: 1730000000000,
        }),
      ],
      studentNote: '**Đã xong UI**',
      onFileSelect() {},
      onProductLinksChange() {},
      onImagesChange() {},
      onNoteChange() {},
      onSubmit() {},
    }),
  );

  assert.match(milestoneHtml, /1\/5 liên kết/);
  assert.match(milestoneHtml, /1\/5 ảnh/);
  assert.match(milestoneHtml, /data-block-kind="link"/);
  assert.match(milestoneHtml, /data-block-kind="image"/);
  assert.match(milestoneHtml, /dashboard-preview\.png/);
  assert.match(milestoneHtml, /Phóng to/);
  assert.doesNotMatch(milestoneHtml, /Xem lớn/);
  assert.match(milestoneHtml, /Thêm liên kết \/ ảnh/);
  assert.match(milestoneHtml, /data-richtext-editor="student-note"/);
  assert.match(milestoneHtml, /\*\*Đã xong UI\*\*/);
  assert.doesNotMatch(milestoneHtml, /Gõ \/ để chèn khối mới/);

  const finalReportEmptyHtml = renderToStaticMarkup(
    React.createElement(composerModule.SubmissionBlockComposer, {
      productLinks: [],
      onProductLinksChange() {},
      images: [],
      onImagesChange() {},
      linksRequired: false,
    }),
  );

  assert.match(finalReportEmptyHtml, /Chưa có liên kết hoặc ảnh minh chứng/);
  assert.match(finalReportEmptyHtml, /Thêm liên kết \/ ảnh/);

  const pdfPreviewHtml = renderToStaticMarkup(
    React.createElement(pdfPreviewModule.PdfPreviewDialog, {
      open: true,
      onClose() {},
      previewUrl: 'blob:http://localhost:5173/demo.pdf',
      fileName: 'BaoCao.pdf',
      fileSize: 2 * 1024 * 1024,
    }),
  );

  assert.match(pdfPreviewHtml, /BaoCao\.pdf/);
  assert.match(pdfPreviewHtml, /Nhỏ gọn/);
  assert.match(pdfPreviewHtml, /Tiêu chuẩn/);
  assert.match(pdfPreviewHtml, /Mở rộng/);
  assert.match(pdfPreviewHtml, /Toàn màn hình/);
  assert.match(pdfPreviewHtml, /Giữ chuột để kéo di chuyển cửa sổ/);
  assert.match(pdfPreviewHtml, /Kéo góc dưới-phải để thay đổi kích thước khung xem trước/);
  for (const dir of ['n', 's', 'e', 'w', 'nw', 'ne', 'sw']) {
    assert.match(pdfPreviewHtml, new RegExp(`data-resize-handle="${dir}"`));
  }
});

test('evaluateSubmissionStageWindow gates Syllabus (Stage 2), Midterm (Stage 4), and Final Report (Stage 5) windows', () => {
  const { evaluateSubmissionStageWindow } = loadModule(
    'features/thesis/utils/submissionStageWindow.ts',
  );

  const baseTimeline = {
    batchId: 'b-1',
    batchName: 'Đợt 1 2026',
    status: 'Open',
    currentLocalDate: '2026-10-15',
    timeZoneId: 'Asia/Ho_Chi_Minh',
    stages: [
      {
        id: 'st-2',
        orderIndex: 2,
        stageCode: 'REGISTRATION_AND_SYLLABUS',
        stageName: 'Đăng ký & Nộp đề cương',
        startDate: '2026-10-10',
        endDate: '2026-10-20',
        deadlineAtUtc: '2026-10-20T16:59:59Z',
      },
      {
        id: 'st-4',
        orderIndex: 4,
        stageCode: 'MIDTERM_GATE',
        stageName: 'Báo cáo giữa kỳ',
        startDate: '2026-11-10',
        endDate: '2026-11-20',
        deadlineAtUtc: '2026-11-20T16:59:59Z',
      },
      {
        id: 'st-5',
        orderIndex: 5,
        stageCode: 'FINAL_EVALUATION',
        stageName: 'Đánh giá cuối kỳ',
        startDate: '2026-12-01',
        endDate: '2026-12-15',
        deadlineAtUtc: '2026-12-15T16:59:59Z',
      },
    ],
  };

  const nowInsideStage2 = Date.parse('2026-10-15T08:00:00Z');
  assert.equal(evaluateSubmissionStageWindow(undefined, 'syllabus', nowInsideStage2).state, 'unknown');

  const syllabusOpen = evaluateSubmissionStageWindow(baseTimeline, 'syllabus', nowInsideStage2);
  assert.equal(syllabusOpen.state, 'open');
  assert.equal(syllabusOpen.message, null);

  const midtermBefore = evaluateSubmissionStageWindow(baseTimeline, 'midterm', nowInsideStage2);
  assert.equal(midtermBefore.state, 'closed');
  assert.equal(midtermBefore.reason, 'before_start');
  assert.match(midtermBefore.message, /10\/11\/2026/);

  const finalBefore = evaluateSubmissionStageWindow(baseTimeline, 'final-report', nowInsideStage2);
  assert.equal(finalBefore.state, 'closed');
  assert.equal(finalBefore.reason, 'before_start');
  assert.match(finalBefore.message, /01\/12\/2026/);

  const nowAfterStage2 = Date.parse('2026-10-21T00:00:00Z');
  const syllabusExpired = evaluateSubmissionStageWindow(
    { ...baseTimeline, currentLocalDate: '2026-10-21' },
    'syllabus',
    nowAfterStage2,
  );
  assert.equal(syllabusExpired.state, 'closed');
  assert.equal(syllabusExpired.reason, 'after_deadline');
  assert.match(syllabusExpired.message, /Đã hết thời gian nộp Đề cương chi tiết/);

  const closedBatch = evaluateSubmissionStageWindow(
    { ...baseTimeline, status: 'Closed' },
    'syllabus',
    nowInsideStage2,
  );
  assert.equal(closedBatch.state, 'closed');
  assert.equal(closedBatch.reason, 'batch_closed');
});

test('useSyllabusSubmission blocks submission outside Stage 2 unless allowSyllabusResubmission is true', () => {
  const stageHelper = loadModule('features/thesis/utils/submissionStageWindow.ts');
  const archival = loadModule('features/thesis/utils/archival.ts');
  const thesisErrors = loadModule('features/thesis/utils/thesisErrors.ts');

  const closedStage2Timeline = {
    batchId: 'b-1',
    batchName: 'Đợt 1',
    status: 'Open',
    currentLocalDate: '2026-11-05',
    timeZoneId: 'Asia/Ho_Chi_Minh',
    stages: [
      {
        id: 'st-2',
        orderIndex: 2,
        stageCode: 'REGISTRATION_AND_SYLLABUS',
        stageName: 'Đăng ký & Đề cương',
        startDate: '2026-10-01',
        endDate: '2026-10-15',
        deadlineAtUtc: '2026-10-15T16:59:59Z',
      },
    ],
  };

  function evaluateSyllabusHook({ allowSyllabusResubmission, syllabusStatus }) {
    const mod = loadModule('features/thesis/hooks/useSyllabusSubmission.ts', {
      react: { useState: (v) => [v, () => {}] },
      '@tanstack/react-query': {
        useQueryClient: () => ({ invalidateQueries: () => {} }),
        useQuery(opts) {
          if (opts.queryKey[0] === 'student-registration-status') {
            return { data: { groupId: 'grp-1', batchId: 'b-1' }, isLoading: false, isError: false };
          }
          if (opts.queryKey[0] === 'student-group-detail') {
            return {
              data: { groupId: 'grp-1', batchId: 'b-1', allowSyllabusResubmission },
              isLoading: false,
              isError: false,
            };
          }
          if (opts.queryKey[0] === 'syllabus-history') {
            return {
              data: syllabusStatus ? { currentStatus: syllabusStatus, versions: [] } : null,
              isLoading: false,
              isError: false,
            };
          }
          if (opts.queryKey[0] === 'academic-batch-timeline') {
            return { data: closedStage2Timeline, isLoading: false, isError: false };
          }
          return { data: null, isLoading: false, isError: false };
        },
        useMutation: () => ({ mutate: () => {}, isPending: false }),
      },
      '@/shared/auth': { useAuth: () => ({ user: { id: 'stu-1' } }) },
      '@/shared/academic-periods/academicBatchApi': {
        academicBatchApi: { getTimeline: async () => closedStage2Timeline },
      },
      '@/shared/academic-periods/useAcademicBatches': {
        useAcademicBatches: () => ({ data: [{ id: 'b-1', batchName: 'Đợt 1', status: 'Open' }] }),
      },
      '../services/syllabusApi': { syllabusApi: {} },
      '../services/submissionUploadHelper': { validateReportFile: () => null },
      '../utils/archival': archival,
      '../utils/submissionStageWindow': stageHelper,
      '../utils/thesisErrors': thesisErrors,
    });
    return mod.useSyllabusSubmission();
  }

  const normalClosed = evaluateSyllabusHook({ allowSyllabusResubmission: false, syllabusStatus: null });
  assert.equal(normalClosed.canSubmit, false);
  assert.match(normalClosed.stageBlockedReason, /Đã hết thời gian nộp Đề cương chi tiết/);

  const renameAllowed = evaluateSyllabusHook({ allowSyllabusResubmission: true, syllabusStatus: 'Approved' });
  assert.equal(renameAllowed.canSubmit, true);
  assert.equal(renameAllowed.stageBlockedReason, null);
  assert.equal(renameAllowed.isRenameResubmission, true);
});

test('getThesisErrorMessage extracts backend error.message from 409 Axios errors instead of raw status text', () => {
  const { getThesisErrorMessage } = loadModule('features/thesis/utils/thesisErrors.ts');

  const axios409 = Object.assign(new Error('Request failed with status code 409'), {
    isAxiosError: true,
    response: {
      status: 409,
      data: {
        success: false,
        data: null,
        error: {
          code: 'Syllabus.InitialSubmissionDeadlinePassed',
          message: 'Đã qua hạn nộp đề cương lần đầu.',
          errors: null,
        },
      },
    },
  });

  assert.equal(
    getThesisErrorMessage(axios409, 'Nộp đề cương thất bại.'),
    'Đã qua hạn nộp đề cương lần đầu.',
  );

  const empty409 = Object.assign(new Error('Request failed with status code 409'), {
    isAxiosError: true,
    response: { status: 409, data: null },
  });
  assert.equal(getThesisErrorMessage(empty409, 'Nộp đề cương thất bại.'), 'Nộp đề cương thất bại.');
});
