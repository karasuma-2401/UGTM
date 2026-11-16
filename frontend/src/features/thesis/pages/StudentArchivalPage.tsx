import React, { useMemo, useRef } from 'react';
import { Link } from 'react-router-dom';
import {
  AlertCircle,
  Archive,
  BookOpen,
  Check,
  CheckCircle2,
  Circle,
  Download,
  ExternalLink,
  FileText,
  History,
  Lock,
  RefreshCw,
  ShieldAlert,
  Upload,
} from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PageHeader } from '@/components/ui/page-header';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { replaceInterpunctWithHyphen } from '@/features/councils/utils/studentResults';
import { ArchivalHistoryDialog } from '../components/archival';
import { useStudentArchival } from '../hooks/useStudentArchival';
import {
  ARCHIVAL_ABSTRACT_MAX_LENGTH,
  ARCHIVAL_KEYWORDS_MAX_LENGTH,
  formatArchivalBlockingReason,
  formatArchivalFileSize,
  getArchivalStatusDisplay,
} from '../utils/archival';
import { classifyThesisError, getThesisErrorMessage } from '../utils/thesisErrors';

export const StudentArchivalPage: React.FC = () => {
  const pdfInputRef = useRef<HTMLInputElement>(null);
  const docxInputRef = useRef<HTMLInputElement>(null);

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
    pdfFile,
    docxFile,
    fileError,
    metadata,
    metadataValidation,
    isHistoryOpen,
    setIsHistoryOpen,
    downloadingKey,
    selectPdfFile,
    selectDocxFile,
    removePdfFile,
    removeDocxFile,
    updateMetadataField,
    submitArchival,
    isSubmitting,
    handleDownloadFile,
    refresh,
  } = useStudentArchival();

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

  const isPdfReady = Boolean(pdfFile) || (isLockedStatus && Boolean(existingPdf));
  const isDocxReady = Boolean(docxFile) || (isLockedStatus && Boolean(existingDocx));
  const isAllChecklistReady =
    isPdfReady &&
    isDocxReady &&
    metadataValidation.abstractsReady &&
    metadataValidation.keywordsReady;

  const blockingAlerts = useMemo(
    () =>
      (eligibility?.blockingReasons ?? [])
        .filter((code) => !(isLockedStatus && code === 'FinalSubmission.Locked'))
        .map(formatArchivalBlockingReason),
    [eligibility?.blockingReasons, isLockedStatus],
  );

  if (isLoadingPage) {
    return (
      <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
        <Skeleton className="h-28 w-full rounded-2xl" />
        <Skeleton className="h-20 w-full rounded-2xl" />
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
          <div className="lg:col-span-8 space-y-5">
            <Skeleton className="h-60 w-full rounded-2xl" />
            <Skeleton className="h-64 w-full rounded-2xl" />
          </div>
          <div className="lg:col-span-4 space-y-5">
            <Skeleton className="h-52 w-full rounded-2xl" />
            <Skeleton className="h-64 w-full rounded-2xl" />
          </div>
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
          <div className="size-12 mx-auto rounded-full bg-rose-50 text-rose-600 flex items-center justify-center">
            {errorKind === 'forbidden' ? (
              <ShieldAlert className="size-6 text-amber-600" />
            ) : (
              <AlertCircle className="size-6" />
            )}
          </div>
          <h2 className="text-lg font-bold text-slate-900">
            {errorKind === 'forbidden'
              ? 'Không có quyền truy cập hồ sơ lưu chiểu'
              : 'Không tải được thông tin đề tài KLTN'}
          </h2>
          <p className="text-sm text-slate-500 max-w-md mx-auto">{message}</p>
          {errorKind !== 'forbidden' && (
            <Button
              type="button"
              variant="outline"
              leftIcon={<RefreshCw className="size-4" />}
              onClick={() => void statusQuery.refetch()}
            >
              Thử lại
            </Button>
          )}
        </Card>
      </div>
    );
  }

  if (!isConfirmedMember) {
    const isPendingConfirmation =
      Boolean(statusQuery.data?.hasRegistered) &&
      statusQuery.data?.confirmationStatus === 'Pending';

    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <Card className="p-8 text-center space-y-4">
          <div className="size-12 mx-auto rounded-full bg-amber-50 text-amber-600 flex items-center justify-center">
            <AlertCircle className="size-6" />
          </div>
          <h2 className="text-lg font-bold text-slate-900">
            {isPendingConfirmation
              ? 'Bạn chưa xác nhận tham gia nhóm đề tài'
              : 'Chưa có thông tin nhóm đề tài KLTN'}
          </h2>
          <p className="text-sm text-slate-500 max-w-md mx-auto">
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
  const advisorName = groupDetail?.advisor?.fullName || 'Chưa phân công';
  const advisorItems = groupDetail?.advisor?.fullName
    ? [{ id: groupDetail.advisor.advisorId || advisorName, name: advisorName }]
    : [];
  const feedbackComment = feedbackQuery.data?.comment?.trim() || '';

  const step1Done =
    submissionStatus === 'Submitted' ||
    submissionStatus === 'AdvisorConfirmed' ||
    submissionStatus === 'ArchiveConfirmed';
  const step2Done =
    submissionStatus === 'AdvisorConfirmed' || submissionStatus === 'ArchiveConfirmed';
  const step3Done = submissionStatus === 'ArchiveConfirmed';

  const steps = [
    {
      num: '01',
      title: 'Sinh viên nộp hồ sơ',
      subtitle: step1Done
        ? `Đã nộp v${submission?.currentVersionNo ?? 1}`
        : submissionStatus === 'NeedRevision'
          ? 'Cần chỉnh sửa và nộp lại'
          : 'Bạn đang thực hiện',
      active: !step1Done,
      done: step1Done,
    },
    {
      num: '02',
      title: 'GVHD xác nhận',
      subtitle: step2Done
        ? submission?.advisorConfirmedAt
          ? `Đã xác nhận ${formatDateTimeVi(submission.advisorConfirmedAt)}`
          : 'Đã xác nhận'
        : submissionStatus === 'Submitted'
          ? 'Đang chờ GVHD kiểm tra'
          : 'Kiểm tra bản chỉnh sửa',
      active: submissionStatus === 'Submitted',
      done: step2Done,
    },
    {
      num: '03',
      title: 'Khoa nghiệm thu',
      subtitle: step3Done
        ? submission?.archivedConfirmedAt
          ? `Đã nghiệm thu ${formatDateTimeVi(submission.archivedConfirmedAt)}`
          : 'Hoàn thành lưu chiểu'
        : submissionStatus === 'AdvisorConfirmed'
          ? 'Đang chờ Khoa nghiệm thu'
          : 'Hoàn thành lưu chiểu',
      active: submissionStatus === 'AdvisorConfirmed',
      done: step3Done,
    },
  ];

  const handleFileInput = (
    event: React.ChangeEvent<HTMLInputElement>,
    handler: (file: File) => void,
  ) => {
    const file = event.target.files?.[0];
    if (file) {
      handler(file);
    }
    event.target.value = '';
  };

  return (
    <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
      <PageHeader
        title="Hồ sơ lưu chiểu"
        description={`${groupCode} - ${topicCode} - Bản chỉnh sửa sau bảo vệ`}
        icon={<Archive />}
      >
        <StatusBadge tone={statusDisplay.tone}>{statusDisplay.label}</StatusBadge>
        <Button
          type="button"
          variant="outline"
          size="icon"
          aria-label="Làm mới hồ sơ lưu chiểu"
          title="Làm mới"
          disabled={isFetching}
          onClick={() => void refresh()}
          className="size-9 rounded-xl border-slate-200 bg-white text-slate-600 hover:text-slate-900"
        >
          <RefreshCw className={`size-4 ${isFetching ? 'animate-spin' : ''}`} />
        </Button>
      </PageHeader>

      <Card className="px-5 py-4 sm:px-6">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 md:gap-6">
          {steps.map((step) => (
            <div
              key={step.num}
              className={`flex items-start gap-3 rounded-xl px-3.5 py-2.5 border transition-colors ${
                step.done
                  ? 'border-emerald-200/80 bg-emerald-50/60'
                  : step.active
                    ? 'border-blue-200/80 bg-blue-50/60'
                    : 'border-slate-200/70 bg-slate-50/50'
              }`}
            >
              <span
                className={`text-sm font-bold tabular-nums mt-0.5 ${
                  step.done
                    ? 'text-emerald-700'
                    : step.active
                      ? 'text-primary'
                      : 'text-slate-400'
                }`}
              >
                {step.num}
              </span>
              <div className="min-w-0">
                <p
                  className={`text-sm font-semibold ${
                    step.done || step.active ? 'text-slate-900' : 'text-slate-500'
                  }`}
                >
                  {step.title}
                </p>
                <p className="text-xs text-slate-500 mt-0.5">{step.subtitle}</p>
              </div>
            </div>
          ))}
        </div>
      </Card>

      {submissionStatus === 'NeedRevision' && submission?.facultyFeedback && (
        <InlineAlert tone="error" title="Hồ sơ cần chỉnh sửa theo phản hồi">
          {submission.facultyFeedback}
        </InlineAlert>
      )}

      {blockingAlerts.length > 0 && (
        <InlineAlert tone="warning" title="Điều kiện nộp lưu chiểu">
          <ul className="list-disc pl-4 space-y-1">
            {blockingAlerts.map((reason) => (
              <li key={reason}>{reason}</li>
            ))}
          </ul>
        </InlineAlert>
      )}

      {isLockedStatus && (
        <InlineAlert
          tone={submissionStatus === 'ArchiveConfirmed' ? 'success' : 'info'}
          title={
            submissionStatus === 'ArchiveConfirmed'
              ? 'Hồ sơ lưu chiểu đã được Khoa nghiệm thu'
              : 'Hồ sơ đang được khóa để Khoa nghiệm thu'
          }
        >
          {submissionStatus === 'ArchiveConfirmed'
            ? 'Nhóm đã hoàn thành bước lưu chiểu và đủ điều kiện mở tra cứu điểm khi Khoa công bố.'
            : 'GVHD đã xác nhận bản chỉnh sửa của nhóm. Hiện không thể thay đổi tệp hoặc metadata trừ khi được yêu cầu chỉnh sửa.'}
        </InlineAlert>
      )}

      {fileError && (
        <InlineAlert tone="error" title="Tệp không hợp lệ">
          {fileError}
        </InlineAlert>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
        <div className="lg:col-span-8 space-y-5">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base font-bold text-slate-900">
                Tài liệu hoàn thiện
              </CardTitle>
              <p className="text-xs text-slate-500">
                Nộp bản đã tiếp thu góp ý của hội đồng (gồm đủ cả PDF và DOCX, tối đa 20 MiB mỗi
                tệp). Các phiên bản trước được giữ trong lịch sử.
              </p>
            </CardHeader>
            <CardContent className="space-y-3">
              <input
                ref={pdfInputRef}
                type="file"
                accept=".pdf,application/pdf"
                className="hidden"
                onChange={(e) => handleFileInput(e, selectPdfFile)}
              />
              <input
                ref={docxInputRef}
                type="file"
                accept=".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                className="hidden"
                onChange={(e) => handleFileInput(e, selectDocxFile)}
              />

              <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200/80 bg-slate-50/70 px-4 py-3">
                <div className="min-w-0 flex items-center gap-3">
                  <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-white border border-slate-200/80 text-slate-600">
                    <FileText className="size-4" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-slate-900 truncate">
                      {pdfFile
                        ? pdfFile.name
                        : existingPdf
                          ? existingPdf.fileName
                          : 'Báo cáo hoàn thiện (.pdf)'}
                    </p>
                    <p className="text-xs text-slate-500 tabular-nums">
                      {pdfFile
                        ? `PDF - ${formatArchivalFileSize(pdfFile.size)} - Tệp mới sẵn sàng nộp`
                        : existingPdf
                          ? `PDF - ${formatArchivalFileSize(existingPdf.fileSize)} - Phiên bản v${submission?.currentVersionNo ?? 1}${canEdit ? ' (chọn tệp mới để nộp lại)' : ''}`
                          : 'PDF - Tối đa 20 MiB - Bắt buộc'}
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {existingPdf && submission && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={downloadingKey === `${submission.currentVersionNo}-ThesisPdf`}
                      leftIcon={<Download className="size-3.5" />}
                      onClick={() =>
                        void handleDownloadFile(
                          submission.currentVersionNo,
                          'ThesisPdf',
                          existingPdf.fileName,
                        )
                      }
                    >
                      Tải
                    </Button>
                  )}
                  {canEdit && (
                    <>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        leftIcon={<Upload className="size-3.5" />}
                        onClick={() => pdfInputRef.current?.click()}
                      >
                        {pdfFile ? 'Thay tệp' : 'Chọn PDF'}
                      </Button>
                      {pdfFile && (
                        <Button type="button" variant="outline" size="sm" onClick={removePdfFile}>
                          Xóa
                        </Button>
                      )}
                    </>
                  )}
                </div>
              </div>

              <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200/80 bg-slate-50/70 px-4 py-3">
                <div className="min-w-0 flex items-center gap-3">
                  <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-white border border-slate-200/80 text-slate-600">
                    <FileText className="size-4" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-slate-900 truncate">
                      {docxFile
                        ? docxFile.name
                        : existingDocx
                          ? existingDocx.fileName
                          : 'Bản có thể chỉnh sửa (.docx)'}
                    </p>
                    <p className="text-xs text-slate-500 tabular-nums">
                      {docxFile
                        ? `DOCX - ${formatArchivalFileSize(docxFile.size)} - Tệp mới sẵn sàng nộp`
                        : existingDocx
                          ? `DOCX - ${formatArchivalFileSize(existingDocx.fileSize)} - Phiên bản v${submission?.currentVersionNo ?? 1}${canEdit ? ' (chọn tệp mới để nộp lại)' : ''}`
                          : 'DOCX - Tối đa 20 MiB - Bắt buộc'}
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {existingDocx && submission && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={downloadingKey === `${submission.currentVersionNo}-ThesisDocx`}
                      leftIcon={<Download className="size-3.5" />}
                      onClick={() =>
                        void handleDownloadFile(
                          submission.currentVersionNo,
                          'ThesisDocx',
                          existingDocx.fileName,
                        )
                      }
                    >
                      Tải
                    </Button>
                  )}
                  {canEdit && (
                    <>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        leftIcon={<Upload className="size-3.5" />}
                        onClick={() => docxInputRef.current?.click()}
                      >
                        {docxFile ? 'Thay tệp' : 'Chọn DOCX'}
                      </Button>
                      {docxFile && (
                        <Button type="button" variant="outline" size="sm" onClick={removeDocxFile}>
                          Xóa
                        </Button>
                      )}
                    </>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base font-bold text-slate-900">
                Tóm tắt và từ khóa
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <div className="flex items-center justify-between text-xs">
                    <label htmlFor="archival-abstract-vi" className="font-semibold text-slate-800">
                      Tóm tắt tiếng Việt <span className="text-rose-600">*</span>
                    </label>
                    <span className="text-slate-400 tabular-nums">
                      {metadata.abstractVi.length}/{ARCHIVAL_ABSTRACT_MAX_LENGTH}
                    </span>
                  </div>
                  <textarea
                    id="archival-abstract-vi"
                    rows={4}
                    maxLength={ARCHIVAL_ABSTRACT_MAX_LENGTH}
                    disabled={!canEdit || isSubmitting}
                    value={metadata.abstractVi}
                    onChange={(e) => updateMetadataField('abstractVi', e.target.value)}
                    placeholder="Nhập tóm tắt khóa luận bằng tiếng Việt..."
                    className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-800 placeholder:text-slate-400 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 disabled:bg-slate-50 disabled:text-slate-600"
                  />
                </div>

                <div className="space-y-1.5">
                  <div className="flex items-center justify-between text-xs">
                    <label htmlFor="archival-abstract-en" className="font-semibold text-slate-800">
                      Abstract tiếng Anh <span className="text-rose-600">*</span>
                    </label>
                    <span className="text-slate-400 tabular-nums">
                      {metadata.abstractEn.length}/{ARCHIVAL_ABSTRACT_MAX_LENGTH}
                    </span>
                  </div>
                  <textarea
                    id="archival-abstract-en"
                    rows={4}
                    maxLength={ARCHIVAL_ABSTRACT_MAX_LENGTH}
                    disabled={!canEdit || isSubmitting}
                    value={metadata.abstractEn}
                    onChange={(e) => updateMetadataField('abstractEn', e.target.value)}
                    placeholder="Enter thesis abstract in English..."
                    className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-800 placeholder:text-slate-400 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 disabled:bg-slate-50 disabled:text-slate-600"
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <div className="flex items-center justify-between text-xs">
                    <label htmlFor="archival-keywords-vi" className="font-semibold text-slate-800">
                      Từ khóa tiếng Việt <span className="text-rose-600">*</span>
                    </label>
                    <span className="text-slate-400 tabular-nums">
                      {metadata.keywordsVi.length}/{ARCHIVAL_KEYWORDS_MAX_LENGTH}
                    </span>
                  </div>
                  <Input
                    id="archival-keywords-vi"
                    maxLength={ARCHIVAL_KEYWORDS_MAX_LENGTH}
                    disabled={!canEdit || isSubmitting}
                    value={metadata.keywordsVi}
                    onChange={(e) => updateMetadataField('keywordsVi', e.target.value)}
                    placeholder="Ví dụ: học sâu; ảnh y tế; phân loại"
                  />
                </div>

                <div className="space-y-1.5">
                  <div className="flex items-center justify-between text-xs">
                    <label htmlFor="archival-keywords-en" className="font-semibold text-slate-800">
                      Keywords tiếng Anh <span className="text-rose-600">*</span>
                    </label>
                    <span className="text-slate-400 tabular-nums">
                      {metadata.keywordsEn.length}/{ARCHIVAL_KEYWORDS_MAX_LENGTH}
                    </span>
                  </div>
                  <Input
                    id="archival-keywords-en"
                    maxLength={ARCHIVAL_KEYWORDS_MAX_LENGTH}
                    disabled={!canEdit || isSubmitting}
                    value={metadata.keywordsEn}
                    onChange={(e) => updateMetadataField('keywordsEn', e.target.value)}
                    placeholder="e.g. deep learning; medical images; classification"
                  />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4 sm:p-5 flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-2 text-xs text-slate-600">
                {isLockedStatus ? (
                  <>
                    <Lock className="size-4 text-slate-500 shrink-0" />
                    <span>
                      Hồ sơ đã được {submissionStatus === 'ArchiveConfirmed' ? 'Khoa nghiệm thu' : 'GVHD xác nhận'} và đang khóa.
                    </span>
                  </>
                ) : (
                  <span>
                    {canSubmit
                      ? 'Hồ sơ đầy đủ 6 thành phần bắt buộc. Sau khi nộp, chờ GVHD xác nhận.'
                      : 'Cần chọn đủ 2 tệp (PDF + DOCX) và điền đầy đủ Tóm tắt/Từ khóa Việt - Anh.'}
                  </span>
                )}
              </div>
              <Button
                type="button"
                disabled={!canSubmit || isSubmitting}
                isLoading={isSubmitting}
                loadingText="Đang nộp..."
                onClick={submitArchival}
              >
                {submission ? 'Nộp lại lưu chiểu' : 'Nộp lưu chiểu'}
              </Button>
            </CardContent>
          </Card>
        </div>

        <div className="lg:col-span-4 space-y-5">
          <Card>
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between gap-2">
                <CardTitle className="text-base font-bold text-slate-900">
                  Kiểm tra trước khi nộp
                </CardTitle>
                <StatusBadge tone={isAllChecklistReady ? 'success' : 'neutral'}>
                  {isAllChecklistReady ? 'Đã chuẩn bị đủ' : 'Chưa đủ'}
                </StatusBadge>
              </div>
            </CardHeader>
            <CardContent className="space-y-3 text-xs">
              <ul className="space-y-2.5 text-slate-700">
                <li className="flex items-center gap-2.5">
                  {isPdfReady ? (
                    <Check className="size-4 text-emerald-600 shrink-0" />
                  ) : (
                    <Circle className="size-4 text-slate-300 shrink-0" />
                  )}
                  <span>Báo cáo hoàn thiện - PDF</span>
                </li>
                <li className="flex items-center gap-2.5">
                  {isDocxReady ? (
                    <Check className="size-4 text-emerald-600 shrink-0" />
                  ) : (
                    <Circle className="size-4 text-slate-300 shrink-0" />
                  )}
                  <span>Bản có thể chỉnh sửa - DOCX</span>
                </li>
                <li className="flex items-center gap-2.5">
                  {metadataValidation.abstractsReady ? (
                    <Check className="size-4 text-emerald-600 shrink-0" />
                  ) : (
                    <Circle className="size-4 text-slate-300 shrink-0" />
                  )}
                  <span>Tóm tắt - Tiếng Việt / Anh</span>
                </li>
                <li className="flex items-center gap-2.5">
                  {metadataValidation.keywordsReady ? (
                    <Check className="size-4 text-emerald-600 shrink-0" />
                  ) : (
                    <Circle className="size-4 text-slate-300 shrink-0" />
                  )}
                  <span>Từ khóa - Tiếng Việt / Anh</span>
                </li>
              </ul>

              <div className="pt-2 border-t border-slate-100 text-slate-500 space-y-1">
                <p>GVHD xác nhận bản sửa trước khi Khoa nghiệm thu.</p>
                {eligibility?.submissionDeadline && (
                  <p className="tabular-nums">
                    Hạn nộp lưu chiểu:{' '}
                    <strong className="text-slate-700">
                      {formatDateTimeVi(eligibility.submissionDeadline)}
                    </strong>
                  </p>
                )}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base font-bold text-slate-900">
                Góp ý sau bảo vệ
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 text-sm">
              {feedbackComment ? (
                <p className="text-xs text-slate-700 leading-relaxed whitespace-pre-line">
                  {feedbackComment}
                </p>
              ) : (
                <p className="text-xs text-slate-500 leading-relaxed">
                  Theo dõi đầy đủ nhận xét và góp ý chỉnh sửa báo cáo cuối kỳ của nhóm.
                </p>
              )}

              <Button asChild variant="outline" className="w-full">
                <Link to="/thesis/final-report">
                  <ExternalLink className="size-3.5" />
                  Xem toàn bộ góp ý
                </Link>
              </Button>

              <div className="space-y-1.5 pt-3 border-t border-slate-100">
                <p className="text-xs font-semibold text-slate-800">Tra cứu điểm chính thức</p>
                <p className="text-xs text-slate-500 leading-relaxed">
                  Điểm được mở khi Khoa đã nghiệm thu lưu chiểu và công bố kết quả.
                </p>
              </div>

              <div className="space-y-1.5 pt-3 border-t border-slate-100">
                <p className="text-xs font-medium text-slate-500">GVHD xác nhận</p>
                {advisorItems.length > 0 ? (
                  <div className="flex items-center gap-2.5">
                    <AvatarGroup
                      items={advisorItems}
                      max={1}
                      size="sm"
                      ariaLabel="Giảng viên hướng dẫn"
                    />
                    <div className="min-w-0">
                      <p className="text-xs font-semibold text-slate-800 truncate">
                        {advisorName}
                      </p>
                      <p className="text-[11px] text-slate-500">
                        {step2Done && submission?.advisorConfirmedAt
                          ? `Đã xác nhận (${formatDateTimeVi(submission.advisorConfirmedAt)})`
                          : submissionStatus === 'Submitted'
                            ? 'Chờ GVHD xác nhận bản sửa'
                            : replaceInterpunctWithHyphen(groupDetail?.batchName) || 'Học kỳ hiện tại'}
                      </p>
                    </div>
                  </div>
                ) : (
                  <p className="text-xs text-slate-500">{advisorName}</p>
                )}
                {step3Done && (
                  <div className="flex items-center gap-1.5 pt-1 text-xs text-emerald-700 font-medium">
                    <CheckCircle2 className="size-3.5 shrink-0" />
                    <span>Khoa đã nghiệm thu lưu chiểu</span>
                  </div>
                )}
              </div>

              <Button
                type="button"
                variant="outline"
                className="w-full"
                disabled={!submission}
                leftIcon={<History className="size-4" />}
                onClick={() => setIsHistoryOpen(true)}
              >
                Xem lịch sử hồ sơ
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>

      <ArchivalHistoryDialog
        open={isHistoryOpen}
        onClose={() => setIsHistoryOpen(false)}
        packages={packagesQuery.data ?? []}
        isLoading={packagesQuery.isLoading}
        error={
          packagesQuery.isError
            ? getThesisErrorMessage(packagesQuery.error, 'Không tải được lịch sử hồ sơ.')
            : null
        }
        onRetry={() => void packagesQuery.refetch()}
        onDownloadFile={(version, role, fileName) => void handleDownloadFile(version, role, fileName)}
        downloadingKey={downloadingKey}
      />
    </div>
  );
};
