import { Link } from 'react-router-dom';
import { FileUp, BookOpen, AlertCircle, CheckCircle2, Clock } from 'lucide-react';
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

export function FinalReportSubmissionPage() {
  const {
    statusQuery,
    groupDetailQuery,
    feedbackQuery,
    selectedFile,
    productNote,
    setProductNote,
    isFeedbackOpen,
    setIsFeedbackOpen,
    actionError,
    actionSuccess,
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

      {actionError && (
        <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 flex items-start gap-3 shadow-xs">
          <AlertCircle className="size-5 text-rose-600 shrink-0 mt-0.5" />
          <div className="text-sm font-medium leading-relaxed">{actionError}</div>
        </div>
      )}

      {actionSuccess && (
        <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 flex items-start gap-3 shadow-xs">
          <CheckCircle2 className="size-5 text-emerald-600 shrink-0 mt-0.5" />
          <div className="text-sm font-medium leading-relaxed">{actionSuccess}</div>
        </div>
      )}

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