import React, { useEffect, useMemo, useState } from 'react';
import { Link, useBlocker } from 'react-router-dom';
import {
  AlertCircle,
  BookOpen,
  Check,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Clock,
  Download,
  ExternalLink,
  Eye,
  Lock,
  RefreshCw,
  RotateCcw,
  Send,
  ShieldAlert,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PageHeader } from '@/components/ui/page-header';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { replaceInterpunctWithHyphen } from '@/features/councils/utils/studentResults';
import { cn } from '@/lib/utils';
import {
  ArchivalDossierSidebar,
  ArchivalFileDropSlot,
  ArchivalHistoryDialog,
  ArchivalPipelineStrip,
} from '../components/archival';
import { PdfPreviewDialog } from '../components/shared/PdfPreviewDialog';
import { ThesisFileTypeIcon } from '../components/shared/ThesisFileTypeIcon';
import { useStudentArchival } from '../hooks/useStudentArchival';
import {
  ARCHIVAL_ABSTRACT_MAX_LENGTH,
  ARCHIVAL_KEYWORDS_MAX_LENGTH,
  formatArchivalBlockingReason,
  formatArchivalFileSize,
  getArchivalStatusDisplay,
  parseArchivalKeywords,
} from '../utils/archival';
import { classifyThesisError, getThesisErrorMessage } from '../utils/thesisErrors';

function countWords(text: string): number {
  const trimmed = text.trim();
  if (!trimmed) return 0;
  return trimmed.split(/\s+/).length;
}

export const StudentArchivalPage: React.FC = () => {
  const [confirmSubmitOpen, setConfirmSubmitOpen] = useState(false);
  const [isReadonlyMetadataExpanded, setIsReadonlyMetadataExpanded] = useState(false);

  const {
    statusQuery,
    groupDetailQuery,
    eligibilityQuery,
    submissionQuery,
    feedbackQuery,
    packagesQuery,
    isConfirmedMember,
    submissionStatus,
    isLockedStatus,
    canEdit,
    canSubmit,
    hasUnsavedChanges,
    latestRevisionReview,
    pdfFile,
    docxFile,
    fileError,
    metadata,
    metadataValidation,
    isHistoryOpen,
    setIsHistoryOpen,
    downloadingKey,
    previewingKey,
    pdfPreviewState,
    closePdfPreview,
    selectPdfFile,
    selectDocxFile,
    removePdfFile,
    removeDocxFile,
    updateMetadataField,
    resetDraft,
    submitArchival,
    isSubmitting,
    handleDownloadFile,
    handlePreviewLocalPdf,
    handlePreviewPackagePdf,
    refresh,
  } = useStudentArchival();

  // Protect unsaved draft changes against accidental route transitions or tab closes
  const shouldGuardNavigation = canEdit && hasUnsavedChanges && !isSubmitting;
  const navigationBlocker = useBlocker(shouldGuardNavigation);

  useEffect(() => {
    if (!shouldGuardNavigation) return;
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [shouldGuardNavigation]);

  const eligibility = eligibilityQuery.data;
  const submission = submissionQuery.data;
  const groupDetail = groupDetailQuery.data;
  const statusDisplay = getArchivalStatusDisplay(submissionStatus);

  const isLoadingPage =
    statusQuery.isLoading ||
    (isConfirmedMember && (eligibilityQuery.isLoading || submissionQuery.isLoading) && !eligibility);

  const isFetching =
    statusQuery.isFetching ||
    groupDetailQuery.isFetching ||
    eligibilityQuery.isFetching ||
    submissionQuery.isFetching;

  const existingPdf = submission?.files.find((f) => f.fileRole === 'ThesisPdf') ?? null;
  const existingDocx = submission?.files.find((f) => f.fileRole === 'ThesisDocx') ?? null;
  const hasSubmission = Boolean(submission && submission.currentVersionNo > 0);
  const nextVersionNo = (submission?.currentVersionNo ?? 0) + 1;

  const keywordsViList = useMemo(
    () => parseArchivalKeywords(metadata.keywordsVi),
    [metadata.keywordsVi],
  );
  const keywordsEnList = useMemo(
    () => parseArchivalKeywords(metadata.keywordsEn),
    [metadata.keywordsEn],
  );

  const readinessItems = useMemo(
    () => [
      {
        key: 'pdf',
        label: 'Báo cáo hoàn thiện (.pdf)',
        ready: Boolean(pdfFile) || (isLockedStatus && Boolean(existingPdf)),
        hint: pdfFile
          ? formatArchivalFileSize(pdfFile.size)
          : isLockedStatus && existingPdf
            ? formatArchivalFileSize(existingPdf.fileSize)
            : undefined,
      },
      {
        key: 'docx',
        label: 'Bản có thể chỉnh sửa (.docx)',
        ready: Boolean(docxFile) || (isLockedStatus && Boolean(existingDocx)),
        hint: docxFile
          ? formatArchivalFileSize(docxFile.size)
          : isLockedStatus && existingDocx
            ? formatArchivalFileSize(existingDocx.fileSize)
            : undefined,
      },
      {
        key: 'abstracts',
        label: 'Tóm tắt Tiếng Việt & Tiếng Anh',
        ready: metadataValidation.abstractsReady,
      },
      {
        key: 'keywords',
        label: 'Từ khóa Tiếng Việt & Tiếng Anh',
        ready: metadataValidation.keywordsReady,
        hint:
          keywordsViList.length + keywordsEnList.length > 0
            ? `${keywordsViList.length + keywordsEnList.length} thẻ`
            : undefined,
      },
    ],
    [
      docxFile,
      existingDocx,
      existingPdf,
      isLockedStatus,
      keywordsEnList.length,
      keywordsViList.length,
      metadataValidation.abstractsReady,
      metadataValidation.keywordsReady,
      pdfFile,
    ],
  );

  const readyCount = useMemo(
    () => readinessItems.filter((item) => item.ready).length,
    [readinessItems],
  );

  const blockingAlerts = useMemo(
    () =>
      (eligibility?.blockingReasons ?? [])
        .filter((code) => !(isLockedStatus && code === 'FinalSubmission.Locked'))
        .map(formatArchivalBlockingReason),
    [eligibility?.blockingReasons, isLockedStatus],
  );

  const missingStepsSummary = useMemo(() => {
    const missing: string[] = [];
    if (!pdfFile) missing.push('tệp PDF');
    if (!docxFile) missing.push('tệp DOCX');
    if (!metadataValidation.abstractsReady) missing.push('tóm tắt VI/EN');
    if (!metadataValidation.keywordsReady) missing.push('từ khóa VI/EN');
    return missing;
  }, [docxFile, metadataValidation.abstractsReady, metadataValidation.keywordsReady, pdfFile]);

  if (isLoadingPage) {
    return (
      <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
        <Skeleton className="h-24 w-full rounded-2xl" />
        <Skeleton className="h-20 w-full rounded-2xl" />
        <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_340px] gap-6 items-start">
          <div className="space-y-5">
            <Skeleton className="h-96 w-full rounded-2xl" />
          </div>
          <Skeleton className="h-80 w-full rounded-2xl" />
        </div>
      </div>
    );
  }

  if (statusQuery.isError) {
    const errorKind = classifyThesisError(statusQuery.error);
    const message = getThesisErrorMessage(
      statusQuery.error,
      'Không thể tải trạng thái đăng ký đề tài của bạn.',
    );

    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <Card className="p-8 text-center space-y-4">
          <div className="size-12 mx-auto rounded-full bg-tone-danger-bg text-tone-danger-icon flex items-center justify-center">
            {errorKind === 'forbidden' ? (
              <ShieldAlert className="size-6 text-tone-warning-icon" />
            ) : (
              <AlertCircle className="size-6" />
            )}
          </div>
          <h2 className="text-lg font-bold text-ink-primary">
            {errorKind === 'forbidden'
              ? 'Không có quyền truy cập hồ sơ lưu chiểu'
              : 'Không tải được thông tin đề tài KLTN'}
          </h2>
          <p className="text-sm text-ink-muted max-w-md mx-auto">{message}</p>
          <Button type="button" onClick={() => void statusQuery.refetch()} className="mx-auto">
            <RefreshCw className="size-4" />
            Thử lại
          </Button>
        </Card>
      </div>
    );
  }

  if (!isConfirmedMember) {
    const isPendingConfirmation =
      Boolean(statusQuery.data?.hasRegistered) &&
      statusQuery.data?.confirmationStatus !== 'Confirmed';

    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <Card className="p-8 text-center space-y-4">
          <div className="size-12 mx-auto rounded-full bg-tone-info-bg text-tone-info-icon flex items-center justify-center">
            <AlertCircle className="size-6" />
          </div>
          <h2 className="text-lg font-bold text-ink-primary">
            {isPendingConfirmation
              ? 'Bạn chưa xác nhận tham gia nhóm đề tài'
              : 'Chưa có thông tin nhóm đề tài KLTN'}
          </h2>
          <p className="text-sm text-ink-muted max-w-md mx-auto">
            {isPendingConfirmation
              ? 'Vui lòng hoàn tất xác nhận tham gia nhóm đề tài trước khi nộp hồ sơ lưu chiểu.'
              : 'Bạn chưa đăng ký đề tài nào trong đợt KLTN này. Vui lòng tra cứu và đăng ký đề tài trước khi thực hiện lưu chiểu.'}
          </p>
          <Button asChild>
            <Link to="/topics/catalog">
              <BookOpen className="size-4" />
              Xem danh mục đề tài
            </Link>
          </Button>
        </Card>
      </div>
    );
  }

  const groupCode =
    groupDetail?.groupCode ||
    (statusQuery.data?.groupId
      ? `Nhóm ${statusQuery.data.groupId.slice(0, 4).toUpperCase()}`
      : 'Nhóm KLTN');
  const topicCode = groupDetail?.topicCode || 'KLTN';
  const topicTitleVi =
    groupDetail?.topicTitleVi || statusQuery.data?.topicTitleVi || 'Đề tài khóa luận tốt nghiệp';
  const topicTitleEn = groupDetail?.topicTitleEn || '';
  const advisorName = groupDetail?.advisor?.fullName || 'Chưa phân công';
  const confirmedMembers = (groupDetail?.members ?? [])
    .filter((m) => m.confirmationStatus === 'Confirmed')
    .map((m) => ({
      id: m.studentId,
      name: m.fullName,
      studentCode: m.userCode,
    }));

  const advisorStatusText =
    submissionStatus === 'AdvisorConfirmed' || submissionStatus === 'ArchiveConfirmed'
      ? submission?.advisorConfirmedAt
        ? `Đã xác nhận (${formatDateTimeVi(submission.advisorConfirmedAt)})`
        : 'Đã xác nhận'
      : submissionStatus === 'Submitted'
        ? 'Đang chờ xác nhận'
        : submissionStatus === 'NeedRevision'
          ? 'Yêu cầu chỉnh sửa'
          : 'Chờ nộp hồ sơ';

  const feedbackComment = feedbackQuery.data?.comment?.trim() || '';
  const feedbackContextLabel = null;

  const revisionNote =
    latestRevisionReview?.reason?.trim() || submission?.facultyFeedback?.trim() || '';
  const revisionActorNote = latestRevisionReview?.decidedAtUtc
    ? `${latestRevisionReview.stage === 'Faculty' ? 'Khoa' : 'GVHD'} phản hồi lúc ${formatDateTimeVi(latestRevisionReview.decidedAtUtc)}`
    : null;

  const isBlockedWithoutSubmission = !canEdit && !isLockedStatus && !hasSubmission;
  const activeVersion = submission?.currentVersionNo ?? 1;

  const handleConfirmSubmit = async () => {
    try {
      await submitArchival();
      setConfirmSubmitOpen(false);
    } catch {
      // Error toast is already surfaced by submitMutation.onError
    }
  };

  const readonlyAbstractsLong =
    (submission?.abstractVi?.length ?? 0) > 320 || (submission?.abstractEn?.length ?? 0) > 320;
  const readonlyKeywordsVi = parseArchivalKeywords(submission?.keywordsVi);
  const readonlyKeywordsEn = parseArchivalKeywords(submission?.keywordsEn);

  return (
    <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
      <PageHeader
        accent
        title="Hồ sơ lưu chiểu"
        description={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
            <span className="font-medium text-ink-secondary">{groupCode}</span>
            <span aria-hidden="true" className="text-ink-faint">
              ·
            </span>
            <span className="tabular-nums">{topicCode}</span>
            <span aria-hidden="true" className="text-ink-faint">
              ·
            </span>
            <span>Bản chỉnh sửa sau bảo vệ</span>
          </span>
        }
      >
        <StatusBadge tone={statusDisplay.tone}>{statusDisplay.label}</StatusBadge>
        {hasSubmission && (
          <Badge variant="outline" className="tabular-nums bg-surface-subtle">
            v{submission!.currentVersionNo}
          </Badge>
        )}
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="bg-surface-card"
          isLoading={isFetching}
          leftIcon={!isFetching ? <RefreshCw className="size-3.5" /> : undefined}
          onClick={() => void refresh()}
        >
          Làm mới
        </Button>
      </PageHeader>

      {eligibilityQuery.isError && (
        <InlineAlert
          tone="error"
          title="Không kiểm tra được điều kiện nộp lưu chiểu"
          action={
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => void eligibilityQuery.refetch()}
            >
              Thử lại
            </Button>
          }
        >
          {getThesisErrorMessage(
            eligibilityQuery.error,
            'Vui lòng thử làm mới trang để kiểm tra lại điều kiện.',
          )}
        </InlineAlert>
      )}

      {/* Compact 3-milestone hairline progress strip */}
      <ArchivalPipelineStrip
        submissionStatus={submissionStatus}
        currentVersionNo={submission?.currentVersionNo ?? 0}
        submittedAt={submission?.submittedAt ?? null}
        advisorConfirmedAt={submission?.advisorConfirmedAt ?? null}
        archivedConfirmedAt={submission?.archivedConfirmedAt ?? null}
      />

      <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_340px] gap-6 items-start">
        {/* Left Main State-Driven Dossier */}
        <div className="min-w-0 space-y-5">
          {/* STATE 1: Blocked without prior submission */}
          {isBlockedWithoutSubmission ? (
            <Card className="overflow-hidden border-line/80 shadow-[0_1px_2px_rgba(15,23,42,0.03)] divide-y divide-line-subtle">
              <div className="p-5 sm:p-6 space-y-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    <h2 className="text-base font-semibold text-ink-primary">
                      Điều kiện mở cổng nộp lưu chiểu
                    </h2>
                    <p className="text-xs text-ink-muted mt-0.5 leading-relaxed">
                      Cổng tải lên PDF, DOCX và metadata sẽ tự động mở khi nhóm đáp ứng đủ điều kiện bên dưới.
                    </p>
                  </div>
                  <StatusBadge tone="warning" className="shrink-0">
                    Chưa mở cổng nộp
                  </StatusBadge>
                </div>

                {blockingAlerts.length > 0 && (
                  <InlineAlert tone="warning" title="Các điều kiện cần hoàn tất trước khi nộp">
                    <ul className="list-disc pl-4 space-y-1">
                      {blockingAlerts.map((reason) => (
                        <li key={reason}>{reason}</li>
                      ))}
                    </ul>
                  </InlineAlert>
                )}
              </div>

              {/* Clean key-value summary table of conditions + topic info */}
              <div className="p-5 sm:p-6 space-y-4">
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 text-sm">
                  <div className="space-y-1">
                    <p className="text-xs text-ink-muted">Đợt khóa luận</p>
                    <p className="font-semibold text-ink-primary">
                      {eligibility?.isBatchOpen ? 'Đang mở' : 'Chưa mở / Đã đóng'}
                    </p>
                  </div>
                  <div className="space-y-1">
                    <p className="text-xs text-ink-muted">Cổng lưu chiểu (GĐ 7)</p>
                    <p className="font-semibold text-ink-primary flex items-center gap-1.5">
                      <Clock className="size-3.5 text-ink-subtle" aria-hidden="true" />
                      <span>
                        {eligibility?.isWithinSubmissionWindow ? 'Đang trong hạn' : 'Ngoài hạn nộp'}
                      </span>
                    </p>
                  </div>
                  <div className="space-y-1">
                    <p className="text-xs text-ink-muted">Biên bản Hội đồng</p>
                    <p className="font-semibold text-ink-primary flex items-center gap-1.5">
                      {eligibility?.isCouncilDefenseCompleted ? (
                        <CheckCircle2 className="size-3.5 text-tone-success-icon" aria-hidden="true" />
                      ) : (
                        <Lock className="size-3.5 text-ink-subtle" aria-hidden="true" />
                      )}
                      <span>
                        {eligibility?.isCouncilDefenseCompleted
                          ? 'Đã khóa (Locked)'
                          : eligibility?.councilMinuteLockStatus || 'Chưa khóa'}
                      </span>
                    </p>
                  </div>
                </div>

                <div className="pt-4 border-t border-line-subtle space-y-1">
                  <p className="text-xs text-ink-muted">Đề tài áp dụng lưu chiểu</p>
                  <p className="text-sm font-semibold text-ink-primary leading-snug">
                    {topicTitleVi}
                  </p>
                  {topicTitleEn && (
                    <p className="text-xs text-ink-muted leading-snug">{topicTitleEn}</p>
                  )}
                </div>
              </div>

              <div className="bg-surface-subtle/50 px-5 sm:px-6 py-3.5 flex flex-wrap items-center justify-between gap-3">
                <p className="text-xs text-ink-muted">
                  Bạn có thể theo dõi góp ý báo cáo cuối kỳ hoặc kiểm tra biên bản hội đồng trong lúc chờ mở cổng.
                </p>
                <div className="flex flex-wrap items-center gap-2">
                  <Button asChild variant="outline" size="sm" className="bg-surface-card">
                    <Link to="/thesis/final-report">
                      Báo cáo & Tiến độ
                      <ExternalLink className="size-3.5" />
                    </Link>
                  </Button>
                  <Button asChild variant="outline" size="sm" className="bg-surface-card">
                    <Link to="/councils/results">
                      Kết quả bảo vệ
                      <ExternalLink className="size-3.5" />
                    </Link>
                  </Button>
                </div>
              </div>
            </Card>
          ) : canEdit ? (
            /* STATE 2: Editable Composer (NotSubmitted or NeedRevision) */
            <>
              {submissionStatus === 'NeedRevision' && (
                <InlineAlert
                  tone="error"
                  title="Yêu cầu chỉnh sửa và nộp lại hồ sơ lưu chiểu"
                >
                  <div className="space-y-1">
                    {revisionNote ? (
                      <p className="whitespace-pre-wrap leading-relaxed">{revisionNote}</p>
                    ) : (
                      <p>
                        GVHD hoặc Khoa đã yêu cầu cập nhật lại bản chỉnh sửa. Vui lòng kiểm tra lại tệp PDF/DOCX và thông tin song ngữ rồi nộp phiên bản mới.
                      </p>
                    )}
                    {revisionActorNote && (
                      <p className="text-xs opacity-85 tabular-nums">{revisionActorNote}</p>
                    )}
                  </div>
                </InlineAlert>
              )}

              {submissionStatus === 'Submitted' && submission && (
                <InlineAlert
                  tone="info"
                  title={`Hồ sơ v${submission.currentVersionNo} đang chờ GVHD xác nhận`}
                >
                  {submission.submittedAt
                    ? `Đã nộp lúc ${formatDateTimeVi(submission.submittedAt)}. Nếu phát hiện sai sót trước khi ${advisorName} xác nhận, bạn có thể chọn lại tệp và nộp phiên bản v${nextVersionNo}.`
                    : `Hồ sơ đang chờ ${advisorName} kiểm tra. Bạn vẫn có thể cập nhật và nộp phiên bản v${nextVersionNo} trước khi được xác nhận.`}
                </InlineAlert>
              )}

              {submissionStatus !== 'NeedRevision' &&
                submissionStatus !== 'Submitted' &&
                submission?.facultyFeedback && (
                <InlineAlert tone="info" title="Ghi chú từ đơn vị thẩm định">
                  <p className="whitespace-pre-wrap leading-relaxed">
                    {submission.facultyFeedback}
                  </p>
                </InlineAlert>
              )}

              <Card className="overflow-hidden border-line/80 shadow-[0_1px_2px_rgba(15,23,42,0.03)] divide-y divide-line-subtle">
                {/* Section A: Files Upload (PDF + DOCX) */}
                <section className="p-5 sm:p-6 space-y-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <h2 className="text-base font-semibold text-ink-primary">
                        Tài liệu hoàn thiện
                      </h2>
                      <p className="text-xs text-ink-muted mt-0.5 leading-relaxed">
                        Nộp đồng thời bản báo cáo PDF và tệp nguồn DOCX đã tiếp thu góp ý hội đồng (tối đa 20 MiB mỗi tệp).
                      </p>
                    </div>
                    <Badge variant="outline" className="tabular-nums bg-surface-subtle shrink-0">
                      {hasSubmission ? `Phiên bản mới: v${nextVersionNo}` : 'Phiên bản v1'}
                    </Badge>
                  </div>

                  {fileError && (
                    <InlineAlert tone="error" title="Tệp chưa hợp lệ">
                      {fileError}
                    </InlineAlert>
                  )}

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <ArchivalFileDropSlot
                      id="archival-pdf-input"
                      extension=".pdf"
                      title="Báo cáo hoàn thiện (.pdf)"
                      subtitle="Định dạng PDF · tối đa 20 MiB"
                      selectedFile={pdfFile}
                      existingFile={existingPdf}
                      disabled={isSubmitting}
                      onSelectFile={selectPdfFile}
                      onRemoveFile={removePdfFile}
                      onPreviewLocalPdf={handlePreviewLocalPdf}
                      onPreviewExistingPdf={(file) =>
                        void handlePreviewPackagePdf(activeVersion, file.fileName, file.fileSize)
                      }
                      onDownloadExistingFile={(file) =>
                        void handleDownloadFile(activeVersion, 'ThesisPdf', file.fileName)
                      }
                      isPreviewingExisting={previewingKey === `${activeVersion}-ThesisPdf`}
                      isDownloadingExisting={downloadingKey === `${activeVersion}-ThesisPdf`}
                    />

                    <ArchivalFileDropSlot
                      id="archival-docx-input"
                      extension=".docx"
                      title="Bản có thể chỉnh sửa (.docx)"
                      subtitle="Định dạng DOCX · tối đa 20 MiB"
                      selectedFile={docxFile}
                      existingFile={existingDocx}
                      disabled={isSubmitting}
                      onSelectFile={selectDocxFile}
                      onRemoveFile={removeDocxFile}
                      onDownloadExistingFile={(file) =>
                        void handleDownloadFile(activeVersion, 'ThesisDocx', file.fileName)
                      }
                      isDownloadingExisting={downloadingKey === `${activeVersion}-ThesisDocx`}
                    />
                  </div>
                </section>

                {/* Section B: Bilingual Abstracts & Keywords */}
                <section className="p-5 sm:p-6 space-y-5">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <h2 className="text-base font-semibold text-ink-primary">
                        Tóm tắt & Từ khóa song ngữ
                      </h2>
                      <p className="text-xs text-ink-muted mt-0.5 leading-relaxed">
                        Thông tin phục vụ lưu trữ thư viện và tra cứu học thuật (từ khóa phân tách bằng dấu phẩy hoặc chấm phẩy).
                      </p>
                    </div>
                    <StatusBadge tone={metadataValidation.valid ? 'success' : 'neutral'} className="shrink-0">
                      {metadataValidation.valid ? 'Đủ 4 trường' : 'Chưa đủ 4 trường'}
                    </StatusBadge>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                    {/* Vietnamese Column */}
                    <div className="space-y-4">
                      <div className="space-y-1.5">
                        <div className="flex items-center justify-between gap-2">
                          <label
                            htmlFor="archival-abstract-vi"
                            className="text-xs font-semibold text-ink-body"
                          >
                            Tóm tắt tiếng Việt <span className="text-tone-danger-text">*</span>
                          </label>
                          <span className="text-[11px] text-ink-muted tabular-nums">
                            {countWords(metadata.abstractVi)} từ · {metadata.abstractVi.length}/
                            {ARCHIVAL_ABSTRACT_MAX_LENGTH}
                          </span>
                        </div>
                        <textarea
                          id="archival-abstract-vi"
                          rows={5}
                          maxLength={ARCHIVAL_ABSTRACT_MAX_LENGTH}
                          disabled={isSubmitting}
                          value={metadata.abstractVi}
                          onChange={(e) => updateMetadataField('abstractVi', e.target.value)}
                          placeholder="Tóm tắt mục tiêu, phương pháp nghiên cứu và kết quả chính của khóa luận bằng tiếng Việt…"
                          className="w-full rounded-xl border border-line-input bg-surface-card px-3.5 py-2.5 text-sm text-ink-primary placeholder:text-ink-subtle leading-relaxed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-60"
                        />
                      </div>

                      <div className="space-y-2">
                        <div className="flex items-center justify-between gap-2">
                          <label
                            htmlFor="archival-keywords-vi"
                            className="text-xs font-semibold text-ink-body"
                          >
                            Từ khóa tiếng Việt <span className="text-tone-danger-text">*</span>
                          </label>
                          <span className="text-[11px] text-ink-muted tabular-nums">
                            {keywordsViList.length} thẻ · {metadata.keywordsVi.length}/
                            {ARCHIVAL_KEYWORDS_MAX_LENGTH}
                          </span>
                        </div>
                        <Input
                          id="archival-keywords-vi"
                          maxLength={ARCHIVAL_KEYWORDS_MAX_LENGTH}
                          disabled={isSubmitting}
                          value={metadata.keywordsVi}
                          onChange={(e) => updateMetadataField('keywordsVi', e.target.value)}
                          placeholder="VD: khóa luận tốt nghiệp, học sâu, thị giác máy tính"
                        />
                        {keywordsViList.length > 0 && (
                          <div
                            className="flex flex-wrap gap-1.5 pt-0.5"
                            aria-label="Xem trước thẻ từ khóa tiếng Việt"
                          >
                            {keywordsViList.map((kw) => (
                              <span
                                key={kw}
                                className="inline-flex items-center rounded-md border border-line-input/80 bg-surface-elevated px-2 py-0.5 text-xs font-medium text-ink-secondary"
                              >
                                {kw}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                    </div>

                    {/* English Column */}
                    <div className="space-y-4">
                      <div className="space-y-1.5">
                        <div className="flex items-center justify-between gap-2">
                          <label
                            htmlFor="archival-abstract-en"
                            className="text-xs font-semibold text-ink-body"
                          >
                            Abstract tiếng Anh <span className="text-tone-danger-text">*</span>
                          </label>
                          <span className="text-[11px] text-ink-muted tabular-nums">
                            {countWords(metadata.abstractEn)} words · {metadata.abstractEn.length}/
                            {ARCHIVAL_ABSTRACT_MAX_LENGTH}
                          </span>
                        </div>
                        <textarea
                          id="archival-abstract-en"
                          rows={5}
                          maxLength={ARCHIVAL_ABSTRACT_MAX_LENGTH}
                          disabled={isSubmitting}
                          value={metadata.abstractEn}
                          onChange={(e) => updateMetadataField('abstractEn', e.target.value)}
                          placeholder="Summarize objectives, methodology, and main findings of the thesis in English…"
                          className="w-full rounded-xl border border-line-input bg-surface-card px-3.5 py-2.5 text-sm text-ink-primary placeholder:text-ink-subtle leading-relaxed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-60"
                        />
                      </div>

                      <div className="space-y-2">
                        <div className="flex items-center justify-between gap-2">
                          <label
                            htmlFor="archival-keywords-en"
                            className="text-xs font-semibold text-ink-body"
                          >
                            Keywords tiếng Anh <span className="text-tone-danger-text">*</span>
                          </label>
                          <span className="text-[11px] text-ink-muted tabular-nums">
                            {keywordsEnList.length} tags · {metadata.keywordsEn.length}/
                            {ARCHIVAL_KEYWORDS_MAX_LENGTH}
                          </span>
                        </div>
                        <Input
                          id="archival-keywords-en"
                          maxLength={ARCHIVAL_KEYWORDS_MAX_LENGTH}
                          disabled={isSubmitting}
                          value={metadata.keywordsEn}
                          onChange={(e) => updateMetadataField('keywordsEn', e.target.value)}
                          placeholder="Ex: thesis, deep learning, computer vision"
                        />
                        {keywordsEnList.length > 0 && (
                          <div
                            className="flex flex-wrap gap-1.5 pt-0.5"
                            aria-label="Preview English keyword tags"
                          >
                            {keywordsEnList.map((kw) => (
                              <span
                                key={kw}
                                className="inline-flex items-center rounded-md border border-tone-info-border bg-tone-info-bg px-2 py-0.5 text-xs font-medium text-tone-info-strong"
                              >
                                {kw}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                </section>

                {/* Integrated Footer Action Bar */}
                <div className="bg-surface-subtle/50 px-5 sm:px-6 py-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                  <div className="flex items-center gap-2 text-xs text-ink-muted">
                    {canSubmit ? (
                      <>
                        <Check className="size-4 text-tone-success-icon shrink-0" aria-hidden="true" />
                        <span className="text-ink-primary font-medium">
                          Sẵn sàng nộp hồ sơ lưu chiểu phiên bản v{nextVersionNo}.
                        </span>
                      </>
                    ) : (
                      <span>Còn thiếu: {missingStepsSummary.join(', ')}.</span>
                    )}
                  </div>

                  <div className="flex flex-wrap items-center gap-2 sm:justify-end">
                    {hasUnsavedChanges && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        disabled={isSubmitting}
                        leftIcon={<RotateCcw className="size-3.5" />}
                        onClick={resetDraft}
                      >
                        Hoàn tác bản nháp
                      </Button>
                    )}

                    <Button
                      type="button"
                      disabled={!canSubmit || isSubmitting}
                      isLoading={isSubmitting}
                      leftIcon={!isSubmitting ? <Send className="size-4" /> : undefined}
                      onClick={() => setConfirmSubmitOpen(true)}
                    >
                      {submissionStatus === 'NeedRevision'
                        ? `Nộp lại phiên bản v${nextVersionNo}`
                        : 'Nộp hồ sơ lưu chiểu'}
                    </Button>
                  </div>
                </div>
              </Card>
            </>
          ) : (
            /* STATE 3: Locked / Confirmed / Read-only Submitted Package */
            <>
              {submissionStatus === 'ArchiveConfirmed' ? (
                <InlineAlert
                  tone="success"
                  title="Khoa đã nghiệm thu hồ sơ lưu chiểu"
                >
                  {submission?.archivedConfirmedAt
                    ? `Hồ sơ phiên bản v${activeVersion} đã được nghiệm thu hoàn tất lúc ${formatDateTimeVi(submission.archivedConfirmedAt)}.`
                    : `Hồ sơ phiên bản v${activeVersion} đã hoàn tất quy trình lưu chiểu.`}
                </InlineAlert>
              ) : submissionStatus === 'AdvisorConfirmed' ? (
                <InlineAlert
                  tone="info"
                  title="GVHD đã xác nhận bản chỉnh sửa"
                >
                  {submission?.advisorConfirmedAt
                    ? `GVHD xác nhận phiên bản v${activeVersion} lúc ${formatDateTimeVi(submission.advisorConfirmedAt)}. Hồ sơ đang chờ Khoa kiểm duyệt nghiệm thu cuối.`
                    : `GVHD đã xác nhận phiên bản v${activeVersion}. Hồ sơ đang chờ Khoa kiểm duyệt nghiệm thu cuối.`}
                </InlineAlert>
              ) : submissionStatus === 'Submitted' ? (
                <InlineAlert
                  tone="warning"
                  title={`Hồ sơ v${activeVersion} đang chờ GVHD xác nhận`}
                >
                  {submission?.submittedAt
                    ? `Đã nộp lúc ${formatDateTimeVi(submission.submittedAt)}. Bạn có thể xem trước bản PDF, tải lại tệp hoặc đối chiếu lịch sử phiên bản trong lúc chờ GVHD duyệt.`
                    : 'Hồ sơ đã nộp thành công và đang chờ GVHD kiểm tra trước khi chuyển lên Khoa.'}
                </InlineAlert>
              ) : (
                blockingAlerts.length > 0 && (
                  <InlineAlert tone="warning" title="Hiện không thể chỉnh sửa hồ sơ">
                    <ul className="list-disc pl-4 space-y-1">
                      {blockingAlerts.map((reason) => (
                        <li key={reason}>{reason}</li>
                      ))}
                    </ul>
                  </InlineAlert>
                )
              )}

              <Card className="overflow-hidden border-line/80 shadow-[0_1px_2px_rgba(15,23,42,0.03)] divide-y divide-line-subtle">
                {/* Verified Files Section */}
                <section className="p-5 sm:p-6 space-y-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <h2 className="text-base font-semibold text-ink-primary">
                        Tài liệu đã nộp (Phiên bản v{activeVersion})
                      </h2>
                      <p className="text-xs text-ink-muted mt-0.5">
                        {submission?.submittedAt
                          ? `Nộp lúc ${formatDateTimeVi(submission.submittedAt)}`
                          : 'Gói tài liệu lưu chiểu hiện hành'}
                      </p>
                    </div>
                    <StatusBadge tone={statusDisplay.tone}>{statusDisplay.label}</StatusBadge>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {[
                      {
                        role: 'ThesisPdf',
                        label: 'PDF',
                        fallbackName: 'bao-cao-hoan-thien.pdf',
                        file: existingPdf,
                      },
                      {
                        role: 'ThesisDocx',
                        label: 'DOCX',
                        fallbackName: 'ban-chinh-sua.docx',
                        file: existingDocx,
                      },
                    ].map(({ role, label, fallbackName, file }) => {
                      const actionKey = `${activeVersion}-${role}`;
                      const isPreviewing = role === 'ThesisPdf' && previewingKey === actionKey;
                      const isDownloading = downloadingKey === actionKey;
                      const isBusy = previewingKey !== null || downloadingKey !== null;

                      return (
                        <div
                          key={role}
                          className="flex flex-col justify-between gap-3 rounded-xl border border-line/80 bg-surface-subtle/60 px-4 py-3.5"
                        >
                          <div className="min-w-0 flex items-center gap-3">
                            <ThesisFileTypeIcon fileName={file?.fileName ?? fallbackName} />
                            <div className="min-w-0 flex-1">
                              <p
                                className="text-xs font-semibold text-ink-primary truncate"
                                title={file?.fileName ?? 'Chưa có tệp'}
                              >
                                {file?.fileName ?? 'Chưa có tệp'}
                              </p>
                              <p className="mt-0.5 text-[11px] text-ink-muted tabular-nums truncate">
                                {file
                                  ? `${formatArchivalFileSize(file.fileSize)} · ${formatDateTimeVi(file.uploadedAt)}`
                                  : 'Không có tệp'}
                              </p>
                            </div>
                          </div>

                          <div className="flex items-center justify-end gap-1.5 pt-2.5 border-t border-line/60">
                            {role === 'ThesisPdf' && (
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                className="h-8 rounded-lg bg-surface-card text-xs"
                                disabled={!file || isBusy}
                                isLoading={isPreviewing}
                                leftIcon={!isPreviewing ? <Eye className="size-3.5 text-ink-body" /> : undefined}
                                onClick={() =>
                                  file &&
                                  void handlePreviewPackagePdf(
                                    activeVersion,
                                    file.fileName,
                                    file.fileSize,
                                  )
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
                              className="h-8 rounded-lg bg-surface-card text-xs"
                              disabled={!file || isBusy}
                              isLoading={isDownloading}
                              leftIcon={!isDownloading ? <Download className="size-3.5" /> : undefined}
                              onClick={() =>
                                file && void handleDownloadFile(activeVersion, role, file.fileName)
                              }
                            >
                              Tải xuống
                            </Button>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </section>

                {/* Read-only Bilingual Metadata Section */}
                <section className="p-5 sm:p-6 space-y-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h2 className="text-base font-semibold text-ink-primary">
                      Tóm tắt & Từ khóa đã lưu
                    </h2>
                    {readonlyAbstractsLong && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="h-7 px-2 text-xs text-primary"
                        rightIcon={
                          isReadonlyMetadataExpanded ? (
                            <ChevronUp className="size-3.5" />
                          ) : (
                            <ChevronDown className="size-3.5" />
                          )
                        }
                        onClick={() => setIsReadonlyMetadataExpanded((prev) => !prev)}
                      >
                        {isReadonlyMetadataExpanded ? 'Thu gọn tóm tắt' : 'Xem đầy đủ tóm tắt'}
                      </Button>
                    )}
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="rounded-xl border border-line/75 bg-surface-subtle/55 p-4 space-y-3">
                      <div className="space-y-1.5">
                        <p className="text-xs font-semibold text-ink-body">Tóm tắt tiếng Việt</p>
                        <p
                          className={cn(
                            'text-sm text-ink-heading whitespace-pre-wrap leading-relaxed',
                            !isReadonlyMetadataExpanded && 'line-clamp-5',
                          )}
                        >
                          {submission?.abstractVi || 'Chưa cập nhật'}
                        </p>
                      </div>
                      <div className="space-y-1.5 pt-2.5 border-t border-line/60">
                        <p className="text-xs font-semibold text-ink-body">Từ khóa tiếng Việt</p>
                        {readonlyKeywordsVi.length > 0 ? (
                          <div className="flex flex-wrap gap-1.5">
                            {readonlyKeywordsVi.map((kw) => (
                              <span
                                key={kw}
                                className="inline-flex items-center rounded-md border border-line-input/80 bg-surface-elevated px-2 py-0.5 text-xs font-medium text-ink-secondary"
                              >
                                {kw}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <p className="text-xs text-ink-muted">Chưa cập nhật</p>
                        )}
                      </div>
                    </div>

                    <div className="rounded-xl border border-line/75 bg-surface-subtle/55 p-4 space-y-3">
                      <div className="space-y-1.5">
                        <p className="text-xs font-semibold text-ink-body">Abstract tiếng Anh</p>
                        <p
                          className={cn(
                            'text-sm text-ink-heading whitespace-pre-wrap leading-relaxed',
                            !isReadonlyMetadataExpanded && 'line-clamp-5',
                          )}
                        >
                          {submission?.abstractEn || 'Chưa cập nhật'}
                        </p>
                      </div>
                      <div className="space-y-1.5 pt-2.5 border-t border-line/60">
                        <p className="text-xs font-semibold text-ink-body">Keywords tiếng Anh</p>
                        {readonlyKeywordsEn.length > 0 ? (
                          <div className="flex flex-wrap gap-1.5">
                            {readonlyKeywordsEn.map((kw) => (
                              <span
                                key={kw}
                                className="inline-flex items-center rounded-md border border-tone-info-border bg-tone-info-bg px-2 py-0.5 text-xs font-medium text-tone-info-strong"
                              >
                                {kw}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <p className="text-xs text-ink-muted">Chưa cập nhật</p>
                        )}
                      </div>
                    </div>
                  </div>
                </section>
              </Card>
            </>
          )}
        </div>

        {/* Sticky 340px Right Readiness & Context Dock */}
        <ArchivalDossierSidebar
          submissionStatus={submissionStatus}
          isLockedStatus={isLockedStatus}
          canEdit={canEdit}
          hasSubmission={hasSubmission}
          currentVersionNo={submission?.currentVersionNo ?? 0}
          submissionDeadline={eligibility?.submissionDeadline ?? null}
          readinessItems={readinessItems}
          readyCount={readyCount}
          feedbackText={feedbackComment ? replaceInterpunctWithHyphen(feedbackComment) : null}
          feedbackContextLabel={feedbackContextLabel}
          feedbackLoading={feedbackQuery.isLoading}
          advisorName={advisorName}
          advisorStatusText={advisorStatusText}
          groupMembers={confirmedMembers}
          onOpenHistory={() => setIsHistoryOpen(true)}
        />
      </div>

      {/* Confirm Submission Dialog */}
      <ConfirmDialog
        open={confirmSubmitOpen}
        onClose={() => !isSubmitting && setConfirmSubmitOpen(false)}
        onConfirm={() => void handleConfirmSubmit()}
        type="brand"
        title={`Xác nhận nộp hồ sơ lưu chiểu v${nextVersionNo}`}
        description={
          <div className="space-y-2.5 text-left text-xs text-ink-body">
            <p>
              Sau khi nộp, hồ sơ sẽ chuyển sang trạng thái chờ{' '}
              <span className="font-semibold text-ink-primary">{advisorName}</span> kiểm tra và xác
              nhận. Bạn không thể tự sửa gói tài liệu trừ khi được yêu cầu bổ sung.
            </p>
            <div className="rounded-lg border border-line-input/80 bg-surface-subtle/60 p-3 space-y-1.5 tabular-nums">
              <div className="flex items-center justify-between gap-2">
                <span className="text-ink-muted">Báo cáo PDF:</span>
                <span className="font-medium text-ink-primary truncate max-w-[200px]">
                  {pdfFile?.name} ({formatArchivalFileSize(pdfFile?.size)})
                </span>
              </div>
              <div className="flex items-center justify-between gap-2">
                <span className="text-ink-muted">Tệp nguồn DOCX:</span>
                <span className="font-medium text-ink-primary truncate max-w-[200px]">
                  {docxFile?.name} ({formatArchivalFileSize(docxFile?.size)})
                </span>
              </div>
              <div className="flex items-center justify-between gap-2">
                <span className="text-ink-muted">Từ khóa (VI / EN):</span>
                <span className="font-medium text-ink-primary">
                  {keywordsViList.length} VI · {keywordsEnList.length} EN
                </span>
              </div>
            </div>
          </div>
        }
        confirmText={isSubmitting ? 'Đang nộp hồ sơ…' : `Xác nhận nộp v${nextVersionNo}`}
        cancelText="Kiểm tra lại"
        isConfirming={isSubmitting}
      />

      {/* Unsaved Draft Navigation Guard */}
      <ConfirmDialog
        open={navigationBlocker.state === 'blocked'}
        onClose={() => navigationBlocker.reset?.()}
        onConfirm={() => navigationBlocker.proceed?.()}
        type="warning"
        title="Rời khỏi trang Hồ sơ lưu chiểu?"
        description="Bạn đang có tệp hoặc nội dung Tóm tắt/Từ khóa chưa nộp. Nếu rời trang lúc này, bản nháp hiện tại sẽ không được giữ lại."
        confirmText="Rời trang"
        cancelText="Ở lại tiếp tục"
      />

      {/* Resizable PDF Preview Dialog (supports both local draft File & server presigned URL) */}
      <PdfPreviewDialog
        open={pdfPreviewState.open}
        onClose={closePdfPreview}
        previewUrl={pdfPreviewState.url}
        fileName={pdfPreviewState.fileName}
        fileSize={pdfPreviewState.fileSize}
      />

      {/* Version History Dialog with integrated PDF preview */}
      <ArchivalHistoryDialog
        open={isHistoryOpen}
        onClose={() => setIsHistoryOpen(false)}
        packages={packagesQuery.data ?? []}
        isLoading={packagesQuery.isLoading}
        error={
          packagesQuery.isError
            ? getThesisErrorMessage(
                packagesQuery.error,
                'Không tải được lịch sử các phiên bản hồ sơ lưu chiểu.',
              )
            : null
        }
        onRetry={() => void packagesQuery.refetch()}
        onDownloadFile={(packageVersion, fileRole, fileName) =>
          void handleDownloadFile(packageVersion, fileRole, fileName)
        }
        downloadingKey={downloadingKey}
        onPreviewPdf={(packageVersion, fileName, fileSize) =>
          void handlePreviewPackagePdf(packageVersion, fileName, fileSize)
        }
        previewingKey={previewingKey}
      />
    </div>
  );
};
