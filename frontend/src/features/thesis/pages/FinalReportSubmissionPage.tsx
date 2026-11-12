import { InlineAlert } from '@/components/ui/inline-alert';
import { OperationToast } from '@/shared/notifications';
import { Link } from 'react-router-dom';
import { FileUp, BookOpen, AlertCircle, Clock, ShieldAlert, RotateCcw } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { ThesisSubmissionNavigation } from '../components/navigation';
import {
  FinalReportUploadCard,
  FinalReportVersionCard,
  FinalReportSummaryCard,
  ReviewerFeedbackDialog,
} from '../components/finalReport';
import { useFinalReportSubmission } from '../hooks/useFinalReportSubmission';
import { SimilarityReportPanel } from '../components/SimilarityReportPanel';
import { classifyThesisError, getThesisErrorMessage } from '../utils/thesisErrors';

export function FinalReportSubmissionPage() {
  const {
    statusQuery,
    groupDetailQuery,
    midtermHistoryQuery,
    feedbackQuery,
    historyQuery,
    selectedFile,
    productNote,
    setProductNote,
    isFeedbackOpen,
    setIsFeedbackOpen,
    actionError,
    actionSuccess,
    setActionSuccess,
    currentStatus,
    latestVersion,
    canSubmit,
    notEligibleReason,
    submitMutation,
    handleDownload,
    handleFileSelect,
    handleRemoveFile,
  } = useFinalReportSubmission();

  const groupDetail = groupDetailQuery.data;

  const handleSubmit = () => {
    if (selectedFile) {
      submitMutation.mutate(selectedFile);
    }
  };

  if (statusQuery.isError) {
    const errorKind = classifyThesisError(statusQuery.error);
    const message = getThesisErrorMessage(
      statusQuery.error,
      'Không thể tải trạng thái đăng ký đề tài của bạn.',
    );

    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <div className="p-8 text-center bg-white rounded-xl border border-slate-200 shadow-xs space-y-4">
          <div className="size-12 mx-auto rounded-full bg-rose-50 text-rose-600 flex items-center justify-center">
            {errorKind === 'forbidden' ? (
              <ShieldAlert className="size-6 text-amber-600" />
            ) : (
              <AlertCircle className="size-6" />
            )}
          </div>
          <h2 className="text-lg font-bold text-slate-800">
            {errorKind === 'forbidden'
              ? 'Không có quyền truy cập báo cáo cuối kỳ'
              : 'Không tải được thông tin đề tài KLTN'}
          </h2>
          <p className="text-sm text-slate-500 max-w-md mx-auto">{message}</p>
          {errorKind !== 'forbidden' && (
            <Button
              type="button"
              variant="outline"
              onClick={() => void statusQuery.refetch()}
              className="border-slate-300"
            >
              <RotateCcw className="size-4 mr-2" />
              Thử lại
            </Button>
          )}
        </div>
      </div>
    );
  }

  if (!statusQuery.isLoading && !statusQuery.data?.hasRegistered) {
    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <div className="p-8 text-center bg-white rounded-xl border border-slate-200 shadow-xs space-y-4">
          <div className="size-12 mx-auto rounded-full bg-amber-50 text-amber-600 flex items-center justify-center">
            <AlertCircle className="size-6" />
          </div>
          <h2 className="text-lg font-bold text-slate-800">
            Chưa có thông tin đề tài KLTN
          </h2>
          <p className="text-sm text-slate-500 max-w-md mx-auto">
            Bạn chưa đăng ký đề tài nào trong đợt KLTN này hoặc đăng ký chưa được xác nhận. Vui lòng tra cứu và đăng ký đề tài trước khi nộp báo cáo cuối kỳ.
          </p>
          <Button asChild className="bg-primary text-white">
            <Link to="/topics/catalog">
              <BookOpen className="size-4 mr-2" />
              Xem danh mục đề tài
            </Link>
          </Button>
        </div>
      </div>
    );
  }

  const badgeText =
    currentStatus === 'Approved'
      ? 'Đạt chuyển phản biện'
      : currentStatus === 'UnderEvaluation'
        ? 'Đang chấm điểm'
        : currentStatus === 'Submitted'
          ? 'Đã nộp v' + (latestVersion?.versionNo ?? 1)
          : canSubmit
            ? 'Sẵn sàng nộp'
            : 'Chưa đủ điều kiện';

  const badgeVariant =
    currentStatus === 'Approved'
      ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
      : currentStatus === 'UnderEvaluation'
        ? 'bg-amber-50 text-amber-700 border-amber-200'
        : canSubmit
          ? 'bg-blue-50 text-blue-700 border-blue-200/80'
          : 'bg-slate-100 text-slate-600 border-slate-200';

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-6">
      <PageHeader
        title="Nộp báo cáo cuối kỳ"
        description="Tuần 11–18 · Sau khi được GVHD cho phép tiếp tục ở Cổng 2."
        icon={<FileUp />}
        navigation={<ThesisSubmissionNavigation />}
      >
        <span
          className={`inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-semibold border ${badgeVariant}`}
        >
          {badgeText}
        </span>
      </PageHeader>

      {notEligibleReason && (
        <div className="p-4 rounded-xl bg-amber-50/90 border border-amber-200 text-amber-900 flex items-start gap-3.5 shadow-xs">
          <div className="p-2 rounded-lg bg-amber-100/80 text-amber-700 shrink-0 mt-0.5">
            <Clock className="size-5" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="font-semibold text-sm text-amber-900">
              Lưu ý điều kiện nộp Báo cáo cuối kỳ
            </p>
            <p className="text-xs mt-1 text-amber-800/90 leading-relaxed">
              {notEligibleReason}
            </p>
          </div>
        </div>
      )}

      { (historyQuery.isError || midtermHistoryQuery.isError) && (() => {
        const err = historyQuery.error || midtermHistoryQuery.error;
        const kind = classifyThesisError(err);
        const msg = getThesisErrorMessage(err, 'Không thể tải dữ liệu báo cáo cuối kỳ.');
        return (
          <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 flex flex-wrap items-center justify-between gap-3 shadow-xs">
            <div className="flex items-start gap-3">
              {kind === 'forbidden' ? (
                <ShieldAlert className="size-5 text-amber-600 shrink-0 mt-0.5" />
              ) : (
                <AlertCircle className="size-5 text-rose-600 shrink-0 mt-0.5" />
              )}
              <div>
                <p className="text-sm font-semibold">
                  {kind === 'forbidden'
                    ? 'Không có quyền truy cập dữ liệu báo cáo cuối kỳ'
                    : 'Không tải được dữ liệu báo cáo cuối kỳ'}
                </p>
                <p className="text-xs mt-0.5 text-rose-700">{msg}</p>
              </div>
            </div>
            {kind !== 'forbidden' && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  void midtermHistoryQuery.refetch();
                  void historyQuery.refetch();
                }}
                className="h-8 text-xs border-rose-300 bg-white text-rose-700 hover:bg-rose-100"
              >
                <RotateCcw className="size-3.5 mr-1.5" />
                Thử lại
              </Button>
            )}
          </div>
        );
      })()}

      {actionError && <InlineAlert title="Không thể hoàn tất">{actionError}</InlineAlert>}

      <OperationToast message={actionSuccess} onConsumed={() => setActionSuccess(null)} />

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8 space-y-6">
          <FinalReportUploadCard
            selectedFile={selectedFile}
            productNote={productNote}
            onProductNoteChange={setProductNote}
            onFileSelect={handleFileSelect}
            onRemoveFile={handleRemoveFile}
            disabled={!canSubmit || submitMutation.isPending}
            latestFileName={latestVersion?.file.fileName}
          />

          <FinalReportVersionCard
            latestVersion={latestVersion}
            currentStatus={currentStatus}
            hasSelectedFile={Boolean(selectedFile)}
            isSubmitting={submitMutation.isPending}
            canSubmit={canSubmit}
            onSubmit={handleSubmit}
            onOpenFeedback={() => setIsFeedbackOpen(true)}
            onDownloadFile={handleDownload}
          />
          {historyQuery.data && (
            <SimilarityReportPanel reportId={historyQuery.data.finalReportId} versionNo={historyQuery.data.currentVersionNo} />
          )}
        </div>

        <div className="lg:col-span-4">
          <FinalReportSummaryCard groupDetail={groupDetail} />
        </div>
      </div>

      <ReviewerFeedbackDialog
        open={isFeedbackOpen}
        onClose={() => setIsFeedbackOpen(false)}
        comment={feedbackQuery.data?.comment}
        isLoading={feedbackQuery.isLoading}
      />
    </div>
  );
}
