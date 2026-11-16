import React, { useMemo, useState } from 'react';
import {
  CalendarClock,
  Check,
  CheckCircle2,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  Clock,
  Download,
  Eye,
  History,
  Lock,
  RotateCcw,
  X,
} from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { cn } from '@/lib/utils';
import { ThesisFileTypeIcon } from '../shared/ThesisFileTypeIcon';
import type { AdvisorArchivalDecision } from '../../hooks/useAdvisorArchival';
import type { ArchivalReviewRow, FinalSubmissionDto } from '../../types/archival.types';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';
import {
  ARCHIVAL_REASON_MAX_LENGTH,
  formatAdvisorDisplayName,
  formatArchivalFileSize,
  parseArchivalKeywords,
} from '../../utils/archival';

interface AdvisorArchivalReviewDialogProps {
  open: boolean;
  inspectedRow: ArchivalReviewRow | null;
  inspectedIndex: number;
  filteredTotalCount: number;
  submissionDetail: FinalSubmissionDto | null;
  groupDetail: StudentGroupDetailDto | null;
  selectedBatchLabel: string;
  isLoadingDetail: boolean;
  detailError: string | null;
  decision: AdvisorArchivalDecision;
  revisionReason: string;
  reasonError: string | null;
  isConfirming: boolean;
  downloadingKey: string | null;
  previewingKey: string | null;
  onClose: () => void;
  onNavigate: (delta: -1 | 1) => void;
  onRefetchDetail: () => void;
  onSelectDecision: (decision: AdvisorArchivalDecision) => void;
  onChangeRevisionReason: (value: string) => void;
  onOpenDecisionConfirm: () => void;
  onDownloadFile: (packageVersion: number, fileRole: string, fileName: string) => void;
  onPreviewPdf: (packageVersion: number, fileName: string, fileSize?: number) => void;
  onOpenHistory: () => void;
}

export const AdvisorArchivalReviewDialog: React.FC<AdvisorArchivalReviewDialogProps> = ({
  open,
  inspectedRow,
  inspectedIndex,
  filteredTotalCount,
  submissionDetail,
  groupDetail,
  selectedBatchLabel,
  isLoadingDetail,
  detailError,
  decision,
  revisionReason,
  reasonError,
  isConfirming,
  downloadingKey,
  previewingKey,
  onClose,
  onNavigate,
  onRefetchDetail,
  onSelectDecision,
  onChangeRevisionReason,
  onOpenDecisionConfirm,
  onDownloadFile,
  onPreviewPdf,
  onOpenHistory,
}) => {
  const [expandedForId, setExpandedForId] = useState<string | null>(null);

  const activeVersion =
    submissionDetail?.currentVersionNo ?? inspectedRow?.currentVersionNo ?? 1;
  const pdfFile = submissionDetail?.files.find((f) => f.fileRole === 'ThesisPdf') ?? null;
  const docxFile = submissionDetail?.files.find((f) => f.fileRole === 'ThesisDocx') ?? null;

  const keywordsVi = useMemo(
    () => parseArchivalKeywords(submissionDetail?.keywordsVi),
    [submissionDetail?.keywordsVi],
  );
  const keywordsEn = useMemo(
    () => parseArchivalKeywords(submissionDetail?.keywordsEn),
    [submissionDetail?.keywordsEn],
  );

  const hasFullMetadata = Boolean(
    submissionDetail?.abstractVi?.trim() &&
      submissionDetail?.abstractEn?.trim() &&
      keywordsVi.length > 0 &&
      keywordsEn.length > 0,
  );

  const isLongAbstract =
    (submissionDetail?.abstractVi?.length ?? 0) > 220 ||
    (submissionDetail?.abstractEn?.length ?? 0) > 220;
  const isMetadataExpanded =
    Boolean(inspectedRow) && expandedForId === inspectedRow?.submissionId;

  const canConfirmRow = inspectedRow?.status === 'Submitted';

  const detailStudents = useMemo(() => {
    if (!inspectedRow) return [];
    if (groupDetail && groupDetail.members.length > 0) {
      return groupDetail.members.map((m) => ({
        id: m.studentId,
        name: m.fullName,
        code: m.userCode,
        userCode: m.userCode,
      }));
    }
    return inspectedRow.studentNames.map((name, idx) => ({
      id: `${inspectedRow.groupId}-${idx}`,
      name,
      code: inspectedRow.studentCodes[idx] ?? '',
      userCode: inspectedRow.studentCodes[idx] ?? '',
    }));
  }, [groupDetail, inspectedRow]);

  const advisorDisplayName = useMemo(
    () =>
      formatAdvisorDisplayName(
        groupDetail?.advisor?.fullName ?? inspectedRow?.advisorName,
        groupDetail?.advisor?.academicTitle,
      ),
    [
      groupDetail?.advisor?.fullName,
      groupDetail?.advisor?.academicTitle,
      inspectedRow?.advisorName,
    ],
  );

  const detailAdvisorItems =
    advisorDisplayName !== 'Giảng viên hướng dẫn' || groupDetail?.advisor?.advisorId
      ? [{ id: groupDetail?.advisor?.advisorId || advisorDisplayName, name: advisorDisplayName }]
      : [];

  if (!open || !inspectedRow) {
    return null;
  }

  const topicCode = groupDetail?.topicCode || 'KLTN';
  const topicTitleEn = groupDetail?.topicTitleEn ?? inspectedRow.topicTitleEn;
  const submittedAtText = submissionDetail?.submittedAt ?? inspectedRow.submittedAt;
  const advisorConfirmedAtText =
    submissionDetail?.advisorConfirmedAt ?? inspectedRow.advisorConfirmedAt;
  const archivedConfirmedAtText =
    submissionDetail?.archivedConfirmedAt ?? inspectedRow.archivedConfirmedAt;

  const timelineSteps = [
    {
      key: 'submitted',
      label: `Sinh viên nộp bản v${activeVersion}`,
      state: 'done' as const,
      meta: submittedAtText ? formatDateTimeVi(submittedAtText) : 'Đã ghi nhận hồ sơ',
    },
    {
      key: 'advisor',
      label: 'GVHD kiểm tra và xác nhận',
      state:
        inspectedRow.status === 'AdvisorConfirmed' || inspectedRow.status === 'ArchiveConfirmed'
          ? ('done' as const)
          : inspectedRow.status === 'NeedRevision'
            ? ('warning' as const)
            : ('current' as const),
      meta: advisorConfirmedAtText
        ? formatDateTimeVi(advisorConfirmedAtText)
        : inspectedRow.status === 'NeedRevision'
          ? 'Đã trả về Cần bổ sung'
          : 'Đang chờ bạn xác nhận',
    },
    {
      key: 'faculty',
      label: 'Văn phòng Khoa nghiệm thu',
      state:
        inspectedRow.status === 'ArchiveConfirmed'
          ? ('done' as const)
          : inspectedRow.status === 'AdvisorConfirmed'
            ? ('current' as const)
            : ('pending' as const),
      meta: archivedConfirmedAtText
        ? formatDateTimeVi(archivedConfirmedAtText)
        : inspectedRow.status === 'AdvisorConfirmed'
          ? 'Đang chờ Khoa nghiệm thu'
          : 'Chờ hoàn thành bước GVHD',
    },
  ];

  const hasPrev = inspectedIndex > 0;
  const hasNext = inspectedIndex >= 0 && inspectedIndex < filteredTotalCount - 1;

  return (
    <Dialog
      open={open}
      hideHeader
      closeOnBackdropClick
      onClose={onClose}
      busy={isConfirming}
      title={`Kiểm tra hồ sơ lưu chiểu · ${inspectedRow.groupCode}`}
      className="flex max-h-[90dvh] w-[calc(100%-2rem)] max-w-6xl flex-col overflow-hidden rounded-2xl border border-slate-200/90 p-0 shadow-[0_24px_60px_-15px_rgba(15,23,42,0.18)]"
    >
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3 border-b border-slate-100 bg-white px-5 py-4 sm:px-6">
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-base font-semibold tracking-tight text-slate-900">
              Kiểm tra hồ sơ lưu chiểu
            </h2>
            <Badge
              variant="info"
              className="tabular-nums"
              title={`UUID nhóm: ${inspectedRow.groupId}`}
            >
              {inspectedRow.groupCode}
            </Badge>
            <span className="rounded-md border border-slate-200 bg-slate-50 px-2 py-0.5 text-xs font-semibold text-slate-700 tabular-nums">
              v{activeVersion}
            </span>
            <StatusBadge tone={inspectedRow.statusTone}>{inspectedRow.statusLabel}</StatusBadge>
          </div>
          <p className="flex flex-wrap items-center gap-1.5 text-xs text-slate-500">
            <CalendarClock className="size-3.5 text-slate-400 shrink-0" aria-hidden="true" />
            <span className="tabular-nums">
              {submittedAtText ? `Nộp lúc ${formatDateTimeVi(submittedAtText)}` : 'Đã nộp hồ sơ'}
            </span>
            <span aria-hidden="true">·</span>
            <span className="truncate font-medium text-slate-600">{selectedBatchLabel}</span>
          </p>
        </div>

        <div className="flex items-center gap-2">
          {filteredTotalCount > 1 && (
            <div className="flex items-center rounded-lg border border-slate-200 bg-slate-50/70 p-0.5">
              <button
                type="button"
                aria-label="Hồ sơ trước"
                title="Hồ sơ trước"
                disabled={!hasPrev || isConfirming}
                onClick={() => onNavigate(-1)}
                className="flex size-7 items-center justify-center rounded-md text-slate-600 hover:bg-white hover:text-slate-900 disabled:opacity-40 disabled:pointer-events-none cursor-pointer"
              >
                <ChevronLeft className="size-4" />
              </button>
              <span className="px-2 text-xs font-medium text-slate-600 tabular-nums">
                {inspectedIndex + 1}/{filteredTotalCount}
              </span>
              <button
                type="button"
                aria-label="Hồ sơ tiếp theo"
                title="Hồ sơ tiếp theo"
                disabled={!hasNext || isConfirming}
                onClick={() => onNavigate(1)}
                className="flex size-7 items-center justify-center rounded-md text-slate-600 hover:bg-white hover:text-slate-900 disabled:opacity-40 disabled:pointer-events-none cursor-pointer"
              >
                <ChevronRight className="size-4" />
              </button>
            </div>
          )}

          <Button
            type="button"
            variant="outline"
            size="sm"
            className="h-8 rounded-lg bg-white text-xs"
            leftIcon={<History className="size-3.5" />}
            onClick={onOpenHistory}
          >
            Lịch sử {activeVersion > 1 ? `(v1–v${activeVersion})` : ''}
          </Button>

          <Button
            type="button"
            variant="ghost"
            size="sm"
            aria-label="Đóng hộp thoại"
            disabled={isConfirming}
            onClick={onClose}
            className="size-8 shrink-0 rounded-lg p-0 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            <X className="size-4" />
          </Button>
        </div>
      </div>

      <div className="grid min-h-0 flex-1 grid-cols-1 overflow-y-auto lg:grid-cols-[minmax(0,1.25fr)_minmax(380px,0.9fr)] lg:grid-rows-[minmax(0,1fr)] lg:divide-x lg:divide-slate-100 lg:overflow-hidden">
        <div className="min-h-0 bg-white p-5 sm:p-6 space-y-5 lg:overflow-y-auto">
          <div className="space-y-3">
            <div className="space-y-1">
              <div className="flex flex-wrap items-center gap-2 text-xs font-semibold text-primary">
                <span>{topicCode}</span>
                <span aria-hidden="true" className="text-slate-300">
                  ·
                </span>
                <span className="text-slate-600">{inspectedRow.groupCode}</span>
              </div>
              <h3 className="text-base sm:text-lg font-bold tracking-tight text-slate-900 leading-snug">
                {inspectedRow.topicTitleVi}
              </h3>
              {topicTitleEn && (
                <p className="text-xs sm:text-sm italic text-slate-500 leading-relaxed">
                  {topicTitleEn}
                </p>
              )}
            </div>

            <div className="flex flex-wrap items-center gap-x-6 gap-y-2.5 rounded-xl border border-slate-200/75 bg-slate-50/60 px-4 py-3">
              <div className="flex items-center gap-2.5 min-w-0">
                <AvatarGroup items={detailStudents} max={3} size="sm" ariaLabel="Sinh viên" />
                <div className="min-w-0 text-xs">
                  <p className="text-[11px] font-medium text-slate-500">Sinh viên thực hiện</p>
                  <p className="font-medium text-slate-800 truncate">
                    {detailStudents.length > 0
                      ? detailStudents
                          .map((s) => (s.code ? `${s.name} (${s.code})` : s.name))
                          .join(' · ')
                      : 'Đang cập nhật'}
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2.5 min-w-0">
                <AvatarGroup
                  items={detailAdvisorItems}
                  max={1}
                  size="sm"
                  ariaLabel="Giảng viên hướng dẫn"
                />
                <div className="min-w-0 text-xs">
                  <p className="text-[11px] font-medium text-slate-500">GVHD</p>
                  <p className="font-medium text-slate-800 truncate">{advisorDisplayName}</p>
                </div>
              </div>
            </div>
          </div>

          {detailError && (
            <InlineAlert
              tone="error"
              title="Không tải được chi tiết hồ sơ"
              action={
                <Button type="button" variant="outline" size="sm" onClick={onRefetchDetail}>
                  Thử lại
                </Button>
              }
            >
              {detailError}
            </InlineAlert>
          )}

          {isLoadingDetail ? (
            <div className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <Skeleton className="h-20 w-full rounded-xl" />
                <Skeleton className="h-20 w-full rounded-xl" />
              </div>
              <Skeleton className="h-44 w-full rounded-xl" />
            </div>
          ) : (
            <>
              <div className="space-y-2.5">
                <div className="flex items-center justify-between gap-2">
                  <h4 className="text-xs font-semibold text-slate-700">
                    Tệp toàn văn đã nộp (v{activeVersion})
                  </h4>
                  <span className="text-xs text-slate-500">
                    {pdfFile && docxFile ? 'Đủ cặp PDF + DOCX' : 'Thiếu tệp bắt buộc'}
                  </span>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  {[
                    {
                      role: 'ThesisPdf',
                      label: 'PDF',
                      fallbackName: 'thesis.pdf',
                      file: pdfFile,
                    },
                    {
                      role: 'ThesisDocx',
                      label: 'DOCX',
                      fallbackName: 'thesis.docx',
                      file: docxFile,
                    },
                  ].map(({ role, label, fallbackName, file }) => {
                    const dlKey = `${activeVersion}-${role}`;
                    const isDownloading = downloadingKey === dlKey;
                    const isPreviewing = role === 'ThesisPdf' && previewingKey === dlKey;
                    const isBusyFileAction = downloadingKey !== null || previewingKey !== null;
                    return (
                      <div
                        key={role}
                        className="flex flex-col justify-between gap-2.5 rounded-xl border border-slate-200/80 bg-slate-50/70 px-3.5 py-3"
                      >
                        <div className="min-w-0 flex items-center gap-3">
                          <ThesisFileTypeIcon fileName={file?.fileName ?? fallbackName} />
                          <div className="min-w-0 flex-1">
                            <p
                              className="text-xs font-semibold text-slate-900 truncate"
                              title={file?.fileName ?? 'Chưa có tệp'}
                            >
                              {file?.fileName ?? 'Chưa có tệp'}
                            </p>
                            <p className="mt-0.5 text-[11px] text-slate-500 tabular-nums truncate">
                              {file
                                ? `${formatArchivalFileSize(file.fileSize)} · ${formatDateTimeVi(file.uploadedAt)}`
                                : 'Chưa được tải lên'}
                            </p>
                          </div>
                        </div>

                        <div className="flex items-center justify-end gap-1.5 border-t border-slate-200/60 pt-2">
                          {role === 'ThesisPdf' && (
                            <Button
                              type="button"
                              variant="outline"
                              size="sm"
                              aria-label="Xem trước bản PDF"
                              className="h-8 rounded-lg bg-white px-2.5 text-xs"
                              disabled={!file || isBusyFileAction}
                              isLoading={isPreviewing}
                              leftIcon={!isPreviewing ? <Eye className="size-3.5 text-slate-600" /> : undefined}
                              onClick={() =>
                                file && onPreviewPdf(activeVersion, file.fileName, file.fileSize)
                              }
                            >
                              Xem trước
                            </Button>
                          )}
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            aria-label={`Tải bản ${label}`}
                            className="h-8 rounded-lg bg-white px-2.5 text-xs"
                            disabled={!file || isBusyFileAction}
                            isLoading={isDownloading}
                            leftIcon={!isDownloading ? <Download className="size-3.5" /> : undefined}
                            onClick={() =>
                              file && onDownloadFile(activeVersion, role, file.fileName)
                            }
                          >
                            Tải
                          </Button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>

              <div className="space-y-3 pt-4 border-t border-slate-100">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <h4 className="text-xs font-semibold text-slate-700">
                      Đối chiếu Metadata Việt / Anh
                    </h4>
                    <Badge variant={hasFullMetadata ? 'success' : 'warning'} className="text-[11px]">
                      {hasFullMetadata ? 'Đủ 4 trường' : 'Chưa đủ trường'}
                    </Badge>
                  </div>
                  {isLongAbstract && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="h-7 px-2 text-xs text-primary"
                      rightIcon={
                        isMetadataExpanded ? (
                          <ChevronUp className="size-3.5" />
                        ) : (
                          <ChevronDown className="size-3.5" />
                        )
                      }
                      onClick={() =>
                        setExpandedForId((prev) =>
                          prev === inspectedRow.submissionId ? null : inspectedRow.submissionId,
                        )
                      }
                    >
                      {isMetadataExpanded ? 'Thu gọn tóm tắt' : 'Xem đầy đủ tóm tắt'}
                    </Button>
                  )}
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="rounded-xl border border-slate-200/75 bg-slate-50/60 p-4 space-y-3">
                    <div className="space-y-1.5">
                      <p className="text-xs font-semibold text-slate-600">Tóm tắt tiếng Việt</p>
                      <p
                        className={cn(
                          'text-sm text-slate-800 whitespace-pre-wrap leading-relaxed',
                          !isMetadataExpanded && 'line-clamp-4',
                        )}
                      >
                        {submissionDetail?.abstractVi || 'Chưa cập nhật'}
                      </p>
                    </div>
                    <div className="space-y-1.5 pt-2 border-t border-slate-200/60">
                      <p className="text-xs font-semibold text-slate-600">Từ khóa tiếng Việt</p>
                      {keywordsVi.length > 0 ? (
                        <div className="flex flex-wrap gap-1.5">
                          {keywordsVi.map((kw) => (
                            <span
                              key={kw}
                              className="inline-flex items-center rounded-md border border-slate-200/80 bg-white px-2 py-0.5 text-xs font-medium text-slate-700"
                            >
                              {kw}
                            </span>
                          ))}
                        </div>
                      ) : (
                        <p className="text-xs text-slate-500">Chưa cập nhật</p>
                      )}
                    </div>
                  </div>

                  <div className="rounded-xl border border-slate-200/75 bg-slate-50/60 p-4 space-y-3">
                    <div className="space-y-1.5">
                      <p className="text-xs font-semibold text-slate-600">Abstract tiếng Anh</p>
                      <p
                        className={cn(
                          'text-sm text-slate-800 whitespace-pre-wrap leading-relaxed',
                          !isMetadataExpanded && 'line-clamp-4',
                        )}
                      >
                        {submissionDetail?.abstractEn || 'Chưa cập nhật'}
                      </p>
                    </div>
                    <div className="space-y-1.5 pt-2 border-t border-slate-200/60">
                      <p className="text-xs font-semibold text-slate-600">Keywords tiếng Anh</p>
                      {keywordsEn.length > 0 ? (
                        <div className="flex flex-wrap gap-1.5">
                          {keywordsEn.map((kw) => (
                            <span
                              key={kw}
                              className="inline-flex items-center rounded-md border border-blue-200/70 bg-blue-50/70 px-2 py-0.5 text-xs font-medium text-blue-800"
                            >
                              {kw}
                            </span>
                          ))}
                        </div>
                      ) : (
                        <p className="text-xs text-slate-500">Chưa cập nhật</p>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            </>
          )}
        </div>

        <aside className="flex min-h-0 flex-col border-t border-slate-100 bg-slate-50/60 lg:border-t-0">
          <div className="flex min-h-0 flex-1 flex-col gap-5 p-5 sm:p-6 lg:overflow-y-auto">
            <div className="rounded-xl border border-slate-200/80 bg-white p-4 space-y-3">
              <p className="text-xs font-semibold text-slate-700">Tiến trình lưu chiểu</p>
              <ol className="space-y-2.5">
                {timelineSteps.map((step) => (
                  <li key={step.key} className="flex items-start gap-2.5">
                    <span
                      className={cn(
                        'mt-0.5 flex size-5 shrink-0 items-center justify-center rounded-full border text-[11px] font-bold',
                        step.state === 'done'
                          ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
                          : step.state === 'current'
                            ? 'border-blue-200 bg-blue-50 text-primary'
                            : step.state === 'warning'
                              ? 'border-rose-200 bg-rose-50 text-rose-700'
                              : 'border-slate-200 bg-slate-50 text-slate-400',
                      )}
                    >
                      {step.state === 'done' ? (
                        <Check className="size-3" />
                      ) : step.state === 'warning' ? (
                        <RotateCcw className="size-3" />
                      ) : (
                        <Clock className="size-3" />
                      )}
                    </span>
                    <div className="min-w-0">
                      <p className="text-xs font-semibold text-slate-800">{step.label}</p>
                      <p className="text-[11px] text-slate-500 tabular-nums">{step.meta}</p>
                    </div>
                  </li>
                ))}
              </ol>
            </div>

            {canConfirmRow ? (
              <div className="space-y-3">
                <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                  Quyết định của GVHD
                </p>

                <div
                  className="space-y-2"
                  role="radiogroup"
                  aria-label="Chọn kết quả xác nhận"
                >
                  <button
                    type="button"
                    role="radio"
                    aria-checked={decision === 'AdvisorConfirm'}
                    disabled={isConfirming}
                    onClick={() => onSelectDecision('AdvisorConfirm')}
                    className={cn(
                      'flex w-full items-start gap-3 rounded-xl border px-3.5 py-3 text-left transition-all cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 disabled:opacity-60',
                      decision === 'AdvisorConfirm'
                        ? 'border-primary bg-blue-50/70 shadow-2xs ring-1 ring-blue-200'
                        : 'border-slate-200/90 bg-white hover:border-slate-300 hover:bg-slate-50/70',
                    )}
                  >
                    <span
                      className={cn(
                        'mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-lg',
                        decision === 'AdvisorConfirm'
                          ? 'bg-primary text-white'
                          : 'bg-slate-100 text-slate-500',
                      )}
                    >
                      <CheckCircle2 className="size-4" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block text-xs font-semibold text-slate-900">
                        Đồng ý bản chỉnh sửa
                      </span>
                      <span className="mt-0.5 block text-[11px] leading-snug text-slate-600">
                        Hồ sơ đạt yêu cầu, chuyển Văn phòng Khoa nghiệm thu lưu chiểu.
                      </span>
                    </span>
                  </button>

                  <button
                    type="button"
                    role="radio"
                    aria-checked={decision === 'RequestRevision'}
                    disabled={isConfirming}
                    onClick={() => onSelectDecision('RequestRevision')}
                    className={cn(
                      'flex w-full items-start gap-3 rounded-xl border px-3.5 py-3 text-left transition-all cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-400/30 disabled:opacity-60',
                      decision === 'RequestRevision'
                        ? 'border-rose-400 bg-rose-50/70 shadow-2xs ring-1 ring-rose-200'
                        : 'border-slate-200/90 bg-white hover:border-slate-300 hover:bg-slate-50/70',
                    )}
                  >
                    <span
                      className={cn(
                        'mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-lg',
                        decision === 'RequestRevision'
                          ? 'bg-rose-600 text-white'
                          : 'bg-slate-100 text-slate-500',
                      )}
                    >
                      <RotateCcw className="size-4" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block text-xs font-semibold text-slate-900">
                        Yêu cầu chỉnh sửa
                      </span>
                      <span className="mt-0.5 block text-[11px] leading-snug text-slate-600">
                        Trả hồ sơ về Cần bổ sung để sinh viên cập nhật và nộp lại.
                      </span>
                    </span>
                  </button>
                </div>

                {decision === 'AdvisorConfirm' ? (
                  <div className="rounded-xl border border-blue-200/70 bg-blue-50/60 px-3.5 py-3 text-xs text-slate-700 leading-relaxed">
                    Xác nhận cặp tệp PDF + DOCX và Metadata Việt/Anh phiên bản{' '}
                    <strong>v{activeVersion}</strong> hợp lệ để chuyển Khoa nghiệm thu.
                  </div>
                ) : (
                  <div className="space-y-2">
                    <div className="flex items-center justify-between gap-2">
                      <label
                        htmlFor="advisor-archival-reason"
                        className="text-xs font-semibold text-slate-700"
                      >
                        Lý do yêu cầu chỉnh sửa <span className="text-rose-600">*</span>
                      </label>
                      <span className="text-xs text-slate-500 tabular-nums">
                        {revisionReason.length}/{ARCHIVAL_REASON_MAX_LENGTH}
                      </span>
                    </div>
                    <textarea
                      id="advisor-archival-reason"
                      rows={5}
                      value={revisionReason}
                      onChange={(event) => onChangeRevisionReason(event.target.value)}
                      disabled={isConfirming || isLoadingDetail}
                      placeholder="Nhập cụ thể nội dung sinh viên cần bổ sung hoặc chỉnh sửa trước khi chuyển Khoa..."
                      className={cn(
                        'w-full rounded-xl border px-3.5 py-2.5 text-sm text-slate-900 placeholder:text-slate-400 bg-white focus:outline-none focus:ring-2 transition-colors',
                        reasonError
                          ? 'border-rose-400 focus:border-rose-500 focus:ring-rose-500/20'
                          : 'border-slate-200 focus:border-primary focus:ring-primary/20',
                      )}
                    />
                    {reasonError && (
                      <p role="alert" className="text-xs font-medium text-rose-600">
                        {reasonError}
                      </p>
                    )}
                  </div>
                )}
              </div>
            ) : (
              <div className="rounded-xl border border-slate-200/80 bg-white p-4 space-y-2.5">
                <div className="flex items-start gap-2 text-xs font-semibold text-slate-900 leading-relaxed">
                  <Lock className="size-4 text-slate-500 shrink-0 mt-0.5" />
                  <span>
                    {inspectedRow.status === 'AdvisorConfirmed'
                      ? 'Bạn đã xác nhận bản chỉnh sửa này và chuyển sang Văn phòng Khoa.'
                      : inspectedRow.status === 'ArchiveConfirmed'
                        ? 'Văn phòng Khoa đã hoàn thành nghiệm thu lưu chiểu.'
                        : 'Hồ sơ đang ở trạng thái Cần bổ sung — chờ sinh viên nộp lại phiên bản mới.'}
                  </span>
                </div>
                {advisorConfirmedAtText && (
                  <p className="text-xs text-slate-600 tabular-nums">
                    Thời điểm GVHD xác nhận: {formatDateTimeVi(advisorConfirmedAtText)}
                  </p>
                )}
                {archivedConfirmedAtText && (
                  <p className="text-xs text-slate-600 tabular-nums">
                    Thời điểm Khoa nghiệm thu: {formatDateTimeVi(archivedConfirmedAtText)}
                  </p>
                )}
                {submissionDetail?.facultyFeedback && (
                  <div className="rounded-lg border border-rose-200/70 bg-rose-50/60 px-3 py-2 text-xs text-rose-900 whitespace-pre-wrap leading-relaxed">
                    <strong>Nội dung yêu cầu chỉnh sửa:</strong> {submissionDetail.facultyFeedback}
                  </div>
                )}
              </div>
            )}
          </div>

          <div className="flex shrink-0 items-center justify-between gap-3 border-t border-slate-200/80 bg-white px-5 py-3.5 sm:px-6">
            <Button type="button" variant="outline" onClick={onClose} disabled={isConfirming}>
              Đóng
            </Button>
            {canConfirmRow && (
              <Button
                type="button"
                variant={decision === 'AdvisorConfirm' ? 'default' : 'destructive'}
                onClick={onOpenDecisionConfirm}
                disabled={isLoadingDetail || !submissionDetail || isConfirming}
                isLoading={isConfirming}
              >
                {decision === 'AdvisorConfirm'
                  ? 'Xác nhận chuyển Khoa'
                  : 'Gửi yêu cầu chỉnh sửa'}
              </Button>
            )}
          </div>
        </aside>
      </div>
    </Dialog>
  );
};
