import { useMemo, useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Clock,
  Download,
  Eye,
  FileCheck2,
  History,
  Lock,
  RefreshCw,
  RotateCcw,
} from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import type { AcademicResultDetailDto } from '@/features/councils/types/results.types';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { cn } from '@/lib/utils';
import type {
  ArchivalReviewRow,
  FinalSubmissionDto,
  FinalSubmissionReviewAction,
} from '../../types/archival.types';
import {
  ARCHIVAL_REASON_MAX_LENGTH,
  formatArchivalFileSize,
  parseArchivalKeywords,
} from '../../utils/archival';
import { ThesisFileTypeIcon } from '../shared/ThesisFileTypeIcon';

interface ArchivalReviewInspectorProps {
  selectedRow: ArchivalReviewRow | null;
  submissionDetail: FinalSubmissionDto | null;
  isLoadingDetail: boolean;
  detailError: string | null;
  academicDetail: AcademicResultDetailDto | null;
  selectedBatchLabel: string;
  canManage: boolean;
  facultyComment: string;
  commentError: string | null;
  isConfirming: boolean;
  downloadingKey: string | null;
  previewingKey: string | null;
  onChangeComment: (value: string) => void;
  onOpenDecisionConfirm: (action: FinalSubmissionReviewAction) => void;
  onOpenHistory: () => void;
  onRefetchDetail: () => void;
  onDownloadFile: (packageVersion: number, fileRole: string, fileName: string) => void;
  onPreviewPdf: (packageVersion: number, fileName: string, fileSize?: number) => void;
}

function countWords(text?: string | null): number {
  const trimmed = text?.trim();
  if (!trimmed) return 0;
  return trimmed.split(/\s+/).length;
}

export function ArchivalReviewInspector({
  selectedRow,
  submissionDetail,
  isLoadingDetail,
  detailError,
  academicDetail,
  selectedBatchLabel,
  canManage,
  facultyComment,
  commentError,
  isConfirming,
  downloadingKey,
  previewingKey,
  onChangeComment,
  onOpenDecisionConfirm,
  onOpenHistory,
  onRefetchDetail,
  onDownloadFile,
  onPreviewPdf,
}: ArchivalReviewInspectorProps) {
  const [expandedForId, setExpandedForId] = useState<string | null>(null);

  const keywordsVi = useMemo(
    () => parseArchivalKeywords(submissionDetail?.keywordsVi),
    [submissionDetail?.keywordsVi],
  );
  const keywordsEn = useMemo(
    () => parseArchivalKeywords(submissionDetail?.keywordsEn),
    [submissionDetail?.keywordsEn],
  );

  if (!selectedRow) {
    return (
      <div className="p-6 text-center space-y-2">
        <div className="mx-auto flex size-10 items-center justify-center rounded-full bg-surface-muted text-ink-subtle">
          <FileCheck2 className="size-5" />
        </div>
        <p className="text-sm font-medium text-ink-heading">Chưa chọn hồ sơ lưu chiểu</p>
        <p className="text-xs text-ink-muted">
          Chọn một hồ sơ ở danh sách bên trái để xem tệp toàn văn, đối chiếu metadata và nghiệm thu.
        </p>
      </div>
    );
  }

  const activeVersion = submissionDetail?.currentVersionNo ?? selectedRow.currentVersionNo;
  const pdfFile = submissionDetail?.files.find((f) => f.fileRole === 'ThesisPdf') ?? null;
  const docxFile = submissionDetail?.files.find((f) => f.fileRole === 'ThesisDocx') ?? null;

  const hasFullMetadata = Boolean(
    submissionDetail?.abstractVi?.trim() &&
      submissionDetail?.abstractEn?.trim() &&
      keywordsVi.length > 0 &&
      keywordsEn.length > 0,
  );
  const viWords = countWords(submissionDetail?.abstractVi);
  const enWords = countWords(submissionDetail?.abstractEn);
  const isLongAbstract =
    (submissionDetail?.abstractVi?.length ?? 0) > 200 ||
    (submissionDetail?.abstractEn?.length ?? 0) > 200;
  const isMetadataExpanded = expandedForId === selectedRow.submissionId;

  const detailStudents =
    academicDetail?.students.map((student) => ({
      id: student.studentId,
      name: student.fullName,
      code: student.studentCode,
      userCode: student.studentCode,
    })) ??
    selectedRow.studentNames.map((name, index) => ({
      id: `${selectedRow.groupId}-${index}`,
      name,
      code: selectedRow.studentCodes[index] ?? '',
      userCode: selectedRow.studentCodes[index] ?? '',
    }));

  const detailAdvisorName = academicDetail?.advisorName || selectedRow.advisorName || '—';
  const detailAdvisorItems =
    detailAdvisorName !== '—'
      ? [{ id: academicDetail?.advisorId || detailAdvisorName, name: detailAdvisorName }]
      : [];

  const advisorConfirmedAt =
    submissionDetail?.advisorConfirmedAt ?? selectedRow.advisorConfirmedAt;
  const archivedConfirmedAt =
    submissionDetail?.archivedConfirmedAt ?? selectedRow.archivedConfirmedAt;
  const isAdvisorConfirmed =
    selectedRow.status === 'AdvisorConfirmed' || selectedRow.status === 'ArchiveConfirmed';
  const canDecideSelected =
    canManage &&
    selectedRow.status === 'AdvisorConfirmed' &&
    Boolean(submissionDetail) &&
    !isLoadingDetail;

  const checklistItems = [
    {
      key: 'advisor',
      label: 'GVHD xác nhận',
      ok: isAdvisorConfirmed,
      detail: advisorConfirmedAt
        ? formatDateTimeVi(advisorConfirmedAt)
        : 'Chờ GVHD xác nhận bản sửa',
    },
    {
      key: 'files',
      label: 'Cặp tệp PDF & DOCX',
      ok: Boolean(pdfFile && docxFile),
      detail: pdfFile && docxFile ? 'Đủ PDF + DOCX hợp lệ' : 'Chưa đủ 2 tệp chuẩn',
    },
    {
      key: 'metadata',
      label: 'Metadata Việt / Anh',
      ok: hasFullMetadata,
      detail: hasFullMetadata ? 'Đủ 4 trường (Việt & Anh)' : 'Thiếu tóm tắt hoặc từ khóa',
    },
    {
      key: 'lock',
      label: 'Trạng thái khóa',
      ok: selectedRow.status === 'ArchiveConfirmed' || selectedRow.status === 'AdvisorConfirmed',
      detail:
        selectedRow.status === 'ArchiveConfirmed'
          ? 'Đã khóa lưu chiểu'
          : selectedRow.status === 'AdvisorConfirmed'
            ? 'Sẵn sàng nghiệm thu'
            : 'Chưa thể nghiệm thu',
    },
  ];

  const isBusyFileAction = downloadingKey !== null || previewingKey !== null;

  return (
    <div className="divide-y divide-line-subtle">
      {/* 1. Header & People */}
      <div className="p-5 space-y-3.5">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0">
            <span className="text-sm font-bold text-primary tabular-nums whitespace-nowrap">
              {academicDetail?.topicCode || 'KLTN'} · {selectedRow.groupCode}
            </span>
            {activeVersion > 1 ? (
              <Badge variant="info" className="text-[11px] tabular-nums whitespace-nowrap">
                v{activeVersion} · Nộp lại
              </Badge>
            ) : (
              <span className="rounded-md border border-line-input bg-surface-muted px-2 py-0.5 text-xs font-semibold text-ink-secondary tabular-nums whitespace-nowrap">
                v{activeVersion}
              </span>
            )}
          </div>
          <StatusBadge tone={selectedRow.statusTone} className="shrink-0">
            {selectedRow.statusLabel}
          </StatusBadge>
        </div>

        <div className="space-y-1">
          <h2 className="text-base font-bold text-ink-heading leading-snug">
            {selectedRow.topicTitleVi}
          </h2>
          {selectedRow.topicTitleEn && (
            <p className="text-xs italic text-ink-muted leading-relaxed">
              {selectedRow.topicTitleEn}
            </p>
          )}
        </div>

        <div className="rounded-xl border border-line/75 bg-surface-subtle/60 p-3 space-y-2.5 text-xs">
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-2.5 min-w-0">
              <AvatarGroup items={detailStudents} max={3} size="sm" ariaLabel="Sinh viên thực hiện" />
              <div className="min-w-0">
                <p className="text-[11px] text-ink-subtle">Sinh viên thực hiện</p>
                <p className="font-medium text-ink-heading truncate">
                  {detailStudents.length > 0
                    ? detailStudents
                        .map((s) => (s.code ? `${s.name} (${s.code})` : s.name))
                        .join(' · ')
                    : 'Đang cập nhật'}
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center justify-between gap-3 pt-2 border-t border-line/60">
            <div className="flex items-center gap-2 min-w-0">
              <AvatarGroup
                items={detailAdvisorItems}
                max={1}
                size="sm"
                ariaLabel="Giảng viên hướng dẫn"
              />
              <div className="min-w-0">
                <p className="text-[11px] text-ink-subtle">GVHD</p>
                <p className="font-medium text-ink-heading truncate">{detailAdvisorName}</p>
              </div>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-7 px-2.5 rounded-lg text-xs shrink-0 bg-surface-card"
              leftIcon={<History className="size-3.5" />}
              onClick={onOpenHistory}
              title={
                activeVersion > 1
                  ? `Xem lịch sử các phiên bản (v1–v${activeVersion})`
                  : `Xem lịch sử nộp (${selectedBatchLabel})`
              }
            >
              Lịch sử {activeVersion > 1 ? `(v1–v${activeVersion})` : ''}
            </Button>
          </div>
        </div>
      </div>

      {/* 2. Body: Checklist + Files + Bilingual Metadata */}
      <div className="p-5 space-y-5">
        {detailError ? (
          <InlineAlert
            tone="error"
            title="Không tải được chi tiết hồ sơ"
            action={
              <Button
                type="button"
                variant="outline"
                size="sm"
                leftIcon={<RefreshCw className="size-3.5" />}
                onClick={onRefetchDetail}
              >
                Thử lại
              </Button>
            }
          >
            {detailError}
          </InlineAlert>
        ) : isLoadingDetail ? (
          <div className="space-y-3">
            <Skeleton className="h-24 w-full rounded-xl" />
            <Skeleton className="h-20 w-full rounded-xl" />
            <Skeleton className="h-32 w-full rounded-xl" />
          </div>
        ) : (
          submissionDetail && (
            <>
              {/* 4-criterion Pre-check Checklist */}
              <div className="space-y-2">
                <div className="flex items-center justify-between gap-2">
                  <h3 className="text-xs font-semibold text-ink-secondary">
                    Checklist nghiệm thu
                  </h3>
                  <span className="text-[11px] font-semibold text-ink-muted tabular-nums">
                    {checklistItems.filter((c) => c.ok).length}/{checklistItems.length} đạt
                  </span>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  {checklistItems.map((item) => (
                    <div
                      key={item.key}
                      className="rounded-xl border border-line/70 bg-surface-subtle/40 px-3 py-2 flex items-start gap-2"
                    >
                      {item.ok ? (
                        <CheckCircle2 className="size-3.5 text-tone-success-icon shrink-0 mt-0.5" />
                      ) : selectedRow.status === 'NeedRevision' ? (
                        <AlertTriangle className="size-3.5 text-tone-danger-icon shrink-0 mt-0.5" />
                      ) : (
                        <Clock className="size-3.5 text-tone-warning-icon shrink-0 mt-0.5" />
                      )}
                      <div className="min-w-0">
                        <p className="text-xs font-medium text-ink-heading leading-tight">
                          {item.label}
                        </p>
                        <p className="text-[11px] text-ink-muted truncate mt-0.5 tabular-nums">
                          {item.detail}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Files + Inline PDF Preview trigger */}
              <div className="space-y-2">
                <div className="flex items-center justify-between gap-2">
                  <h3 className="text-xs font-semibold text-ink-secondary">
                    Tệp toàn văn (v{activeVersion})
                  </h3>
                  <span className="text-[11px] text-ink-muted">
                    {pdfFile && docxFile ? 'Đủ cặp PDF + DOCX' : 'Thiếu tệp bắt buộc'}
                  </span>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
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
                    return (
                      <div
                        key={role}
                        className="rounded-xl border border-line/80 bg-surface-subtle/60 p-3 flex flex-col justify-between gap-2.5"
                      >
                        <div className="flex items-center gap-2.5 min-w-0">
                          <ThesisFileTypeIcon fileName={file?.fileName ?? fallbackName} />
                          <div className="min-w-0 flex-1">
                            <p
                              className="text-xs font-semibold text-ink-heading truncate"
                              title={file?.fileName ?? 'Chưa có tệp'}
                            >
                              {file?.fileName ?? 'Chưa có tệp'}
                            </p>
                            <p className="text-[11px] text-ink-muted tabular-nums truncate">
                              {file ? formatArchivalFileSize(file.fileSize) : 'Chưa tải lên'}
                            </p>
                          </div>
                        </div>

                        <div className="flex items-center justify-end gap-1.5 pt-2 border-t border-line/60">
                          {role === 'ThesisPdf' && (
                            <Button
                              type="button"
                              variant="outline"
                              size="sm"
                              aria-label="Xem trước bản PDF"
                              className="h-7 rounded-lg bg-surface-card px-2.5 text-[11px]"
                              disabled={!file || isBusyFileAction}
                              isLoading={isPreviewing}
                              leftIcon={
                                !isPreviewing ? <Eye className="size-3 text-ink-body" /> : undefined
                              }
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
                            className="h-7 rounded-lg bg-surface-card px-2.5 text-[11px]"
                            disabled={!file || isBusyFileAction}
                            isLoading={isDownloading}
                            leftIcon={!isDownloading ? <Download className="size-3" /> : undefined}
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

              {/* Bilingual Metadata with word counts & keyword chips */}
              <div className="space-y-2.5">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <h3 className="text-xs font-semibold text-ink-secondary">
                      Đối chiếu Metadata Việt / Anh
                    </h3>
                    <Badge variant={hasFullMetadata ? 'success' : 'warning'} className="text-[11px]">
                      {hasFullMetadata ? 'Đủ 4 trường' : 'Chưa đủ trường'}
                    </Badge>
                  </div>
                  {isLongAbstract && (
                    <button
                      type="button"
                      onClick={() =>
                        setExpandedForId((prev) =>
                          prev === selectedRow.submissionId ? null : selectedRow.submissionId,
                        )
                      }
                      className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline cursor-pointer"
                    >
                      {isMetadataExpanded ? (
                        <>
                          Thu gọn <ChevronUp className="size-3.5" />
                        </>
                      ) : (
                        <>
                          Xem đầy đủ <ChevronDown className="size-3.5" />
                        </>
                      )}
                    </button>
                  )}
                </div>

                <div className="space-y-2.5">
                  <div className="rounded-xl border border-line/75 bg-surface-subtle/50 p-3 space-y-2">
                    <div className="flex items-center justify-between gap-2 text-xs">
                      <span className="font-semibold text-ink-body">Tóm tắt tiếng Việt</span>
                      <span className="text-[11px] text-ink-muted tabular-nums">{viWords} từ</span>
                    </div>
                    <p
                      className={cn(
                        'text-xs text-ink-heading whitespace-pre-wrap leading-relaxed',
                        !isMetadataExpanded && 'line-clamp-3',
                      )}
                    >
                      {submissionDetail.abstractVi || 'Chưa cập nhật'}
                    </p>
                    <div className="pt-1.5 border-t border-line/60 flex flex-wrap gap-1.5">
                      {keywordsVi.length > 0 ? (
                        keywordsVi.map((kw) => (
                          <span
                            key={kw}
                            className="inline-flex items-center rounded-md border border-line-input/80 bg-surface-card px-2 py-0.5 text-[11px] font-medium text-ink-secondary"
                          >
                            {kw}
                          </span>
                        ))
                      ) : (
                        <span className="text-[11px] text-ink-muted">Chưa có từ khóa tiếng Việt</span>
                      )}
                    </div>
                  </div>

                  <div className="rounded-xl border border-line/75 bg-surface-subtle/50 p-3 space-y-2">
                    <div className="flex items-center justify-between gap-2 text-xs">
                      <span className="font-semibold text-ink-body">Abstract tiếng Anh</span>
                      <span className="text-[11px] text-ink-muted tabular-nums">{enWords} words</span>
                    </div>
                    <p
                      className={cn(
                        'text-xs text-ink-heading whitespace-pre-wrap leading-relaxed',
                        !isMetadataExpanded && 'line-clamp-3',
                      )}
                    >
                      {submissionDetail.abstractEn || 'Chưa cập nhật'}
                    </p>
                    <div className="pt-1.5 border-t border-line/60 flex flex-wrap gap-1.5">
                      {keywordsEn.length > 0 ? (
                        keywordsEn.map((kw) => (
                          <span
                            key={kw}
                            className="inline-flex items-center rounded-md border border-tone-info-border bg-tone-info-bg px-2 py-0.5 text-[11px] font-medium text-tone-info-strong"
                          >
                            {kw}
                          </span>
                        ))
                      ) : (
                        <span className="text-[11px] text-ink-muted">Chưa có từ khóa tiếng Anh</span>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            </>
          )
        )}
      </div>

      {/* 3. Decision / Status Section */}
      <div className="p-5 bg-surface-subtle/30 space-y-3.5">
        {submissionDetail?.facultyFeedback && (
          <InlineAlert tone="warning" title="Phản hồi chỉnh sửa gần nhất">
            {submissionDetail.facultyFeedback}
          </InlineAlert>
        )}

        {selectedRow.status === 'AdvisorConfirmed' && canManage ? (
          <div className="space-y-3">
            <div className="flex items-center justify-between text-xs">
              <label
                htmlFor="faculty-archival-comment"
                className="font-semibold text-ink-heading"
              >
                Nhận xét Khoa{' '}
                <span className="font-normal text-ink-muted">(bắt buộc khi yêu cầu sửa)</span>
              </label>
              <span className="text-ink-subtle tabular-nums">
                {facultyComment.length}/{ARCHIVAL_REASON_MAX_LENGTH}
              </span>
            </div>

            <textarea
              id="faculty-archival-comment"
              rows={3}
              maxLength={ARCHIVAL_REASON_MAX_LENGTH}
              disabled={!canDecideSelected || isConfirming}
              value={facultyComment}
              onChange={(e) => onChangeComment(e.target.value)}
              placeholder="Ghi rõ nội dung cần sinh viên bổ sung (nếu yêu cầu chỉnh sửa)..."
              className={cn(
                'w-full rounded-xl border bg-surface-card px-3 py-2 text-xs text-ink-heading placeholder:text-ink-subtle focus:outline-none focus:ring-2 disabled:bg-surface-subtle disabled:text-ink-muted',
                commentError
                  ? 'border-rose-400 focus:border-rose-500 focus:ring-rose-500/20'
                  : 'border-line-input focus:border-primary focus:ring-primary/20',
              )}
            />

            {commentError && (
              <p role="alert" className="text-xs font-medium text-tone-danger-icon">
                {commentError}
              </p>
            )}

            <div className="flex flex-wrap items-center justify-end gap-2.5 pt-1">
              <Button
                type="button"
                variant="outline"
                disabled={!canDecideSelected || isConfirming}
                leftIcon={<RotateCcw className="size-3.5" />}
                onClick={() => onOpenDecisionConfirm('RequestRevision')}
                className="h-9 rounded-xl border-tone-danger-border text-tone-danger-text hover:bg-tone-danger-bg text-xs font-semibold"
              >
                Yêu cầu sửa
              </Button>
              <Button
                type="button"
                disabled={!canDecideSelected || isConfirming}
                leftIcon={<CheckCircle2 className="size-3.5" />}
                onClick={() => onOpenDecisionConfirm('FacultyConfirm')}
                className="h-9 rounded-xl text-xs font-semibold"
              >
                Hoàn thành lưu chiểu
              </Button>
            </div>
          </div>
        ) : (
          <div className="rounded-xl border border-line/80 bg-surface-card p-3.5 space-y-1.5 text-xs">
            <div className="flex items-start gap-2 font-semibold text-ink-heading">
              <Lock className="size-4 text-ink-muted shrink-0 mt-0.5" />
              <span>
                {selectedRow.status === 'ArchiveConfirmed'
                  ? 'Hồ sơ đã hoàn thành nghiệm thu và khóa vĩnh viễn'
                  : selectedRow.status === 'Submitted'
                    ? 'Đang chờ GVHD xác nhận bản chỉnh sửa'
                    : selectedRow.status === 'NeedRevision'
                      ? 'Đang chờ sinh viên chỉnh sửa và nộp lại phiên bản mới'
                      : 'Tài khoản hiện tại chỉ có quyền xem hồ sơ lưu chiểu'}
              </span>
            </div>
            {archivedConfirmedAt && (
              <p className="text-ink-muted tabular-nums pl-6">
                Nghiệm thu lúc: {formatDateTimeVi(archivedConfirmedAt)}
              </p>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
