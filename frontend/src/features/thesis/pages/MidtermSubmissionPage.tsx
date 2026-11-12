import { InlineAlert } from '@/components/ui/inline-alert';
import { OperationToast } from '@/shared/notifications';
import { useMidtermSubmission } from '../hooks/useMidtermSubmission';
import {
  MidtermStatusBadge,
  MidtermUploadCard,
  MidtermHistoryTable,
} from '../components/midterm';
import { SyllabusTopicInfoCard } from '../components/submission';
import { ThesisSubmissionNavigation } from '../components/navigation';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/ui/page-header';
import { AlertCircle, BookOpen, FileUp, Clock, ShieldAlert, RotateCcw } from 'lucide-react';
import { Link } from 'react-router-dom';
import { classifyThesisError, getThesisErrorMessage } from '../utils/thesisErrors';

export function MidtermSubmissionPage() {
  const {
    statusQuery,
    groupDetailQuery,
    syllabusHistoryQuery,
    historyQuery,
    selectedFile,
    setSelectedFile,
    actionError,
    actionSuccess,
    setActionSuccess,
    submitMutation,
    handleDownload,
    currentStatus,
    canSubmit,
    notEligibleReason,
  } = useMidtermSubmission();

  const groupDetail = groupDetailQuery.data;
  const versions = historyQuery.data?.versions || [];

  const groupCode =
    groupDetail?.groupCode ||
    (statusQuery.data?.groupId
      ? `Nhóm ${statusQuery.data.groupId.slice(0, 4).toUpperCase()}`
      : 'Nhóm KLTN');
  const topicCode =
    groupDetail?.topicCode ||
    (statusQuery.data?.topicId
      ? `KLTN-${statusQuery.data.topicId.slice(0, 8).toUpperCase()}`
      : 'KLTN');
  const batchName = groupDetail?.batchName || 'Đợt KLTN';

  const advisorName = groupDetail?.advisor?.fullName
    ? `${groupDetail.advisor.academicTitle || 'GV.'} ${groupDetail.advisor.fullName}`.trim()
    : 'Chưa cập nhật';

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
              ? 'Không có quyền truy cập hồ sơ giữa kỳ'
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
            Bạn chưa đăng ký đề tài nào trong đợt KLTN này hoặc đăng ký chưa được xác nhận. Vui lòng tra cứu và đăng ký đề tài trước khi nộp báo cáo giữa kỳ.
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

  const loadFailureError = historyQuery.error || syllabusHistoryQuery.error;
  const loadFailureKind = loadFailureError ? classifyThesisError(loadFailureError) : null;
  const loadFailureMessage = loadFailureError
    ? getThesisErrorMessage(loadFailureError, 'Không thể tải dữ liệu báo cáo giữa kỳ.')
    : null;

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-6">
      <PageHeader
        title="Hồ sơ khóa luận - Báo cáo & tiến độ"
        description={`${groupCode} - ${topicCode} - GVHD: ${advisorName}`}
        icon={<FileUp />}
        navigation={<ThesisSubmissionNavigation />}
      >
        <span className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200/80">
          {batchName}
        </span>
        <MidtermStatusBadge status={currentStatus} className="text-xs px-3 py-1.5" />
      </PageHeader>

      {notEligibleReason && (
        <div className="p-4 rounded-xl bg-amber-50/90 border border-amber-200 text-amber-900 flex items-start gap-3.5 shadow-xs">
          <div className="p-2 rounded-lg bg-amber-100/80 text-amber-700 shrink-0 mt-0.5">
            <Clock className="size-5" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="font-semibold text-sm text-amber-900">
              Chưa đến thời điểm nộp Báo cáo giữa kỳ (Cổng 2)
            </p>
            <p className="text-xs mt-1 text-amber-800/90 leading-relaxed">
              {notEligibleReason}
            </p>
          </div>
          <Button
            asChild
            size="sm"
            variant="outline"
            className="border-amber-300 text-amber-900 hover:bg-amber-100/70 h-8 text-xs shrink-0 bg-white"
          >
            <Link to="/thesis/proposal">
              Xem Cổng 1 - Đề cương
            </Link>
          </Button>
        </div>
      )}

      {loadFailureKind && (
        <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-start gap-3">
            {loadFailureKind === 'forbidden' ? (
              <ShieldAlert className="size-5 shrink-0 text-amber-600 mt-0.5" />
            ) : (
              <AlertCircle className="size-5 shrink-0 text-rose-600 mt-0.5" />
            )}
            <div>
              <p className="font-semibold">
                {loadFailureKind === 'forbidden'
                  ? 'Không có quyền truy cập hồ sơ báo cáo giữa kỳ'
                  : 'Không tải được dữ liệu báo cáo giữa kỳ'}
              </p>
              <p className="text-xs mt-0.5 text-rose-700">{loadFailureMessage}</p>
            </div>
          </div>
          {loadFailureKind !== 'forbidden' && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => {
                void syllabusHistoryQuery.refetch();
                void historyQuery.refetch();
              }}
              className="h-8 text-xs border-rose-300 bg-white text-rose-700 hover:bg-rose-100"
            >
              <RotateCcw className="size-3.5 mr-1.5" />
              Thử lại
            </Button>
          )}
        </div>
      )}

      {actionError && <InlineAlert title="Không thể hoàn tất">{actionError}</InlineAlert>}

      <OperationToast message={actionSuccess} onConsumed={() => setActionSuccess(null)} />

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8 space-y-6">
          <MidtermUploadCard
            selectedFile={selectedFile}
            onFileSelect={setSelectedFile}
            disabled={!canSubmit || submitMutation.isPending}
            disabledReason={notEligibleReason}
          />

          <MidtermHistoryTable
            versions={versions}
            selectedFile={selectedFile}
            groupCode={groupCode}
            currentStatus={currentStatus}
            canSubmit={canSubmit}
            disabledReason={notEligibleReason}
            isSubmitting={submitMutation.isPending}
            onSubmit={() => {
              if (selectedFile) submitMutation.mutate(selectedFile);
            }}
            onDownload={handleDownload}
          />
        </div>

        <div className="lg:col-span-4 sticky top-6">
          <SyllabusTopicInfoCard
            detail={groupDetail}
            isLoading={groupDetailQuery.isLoading}
            isError={groupDetailQuery.isError}
            error={groupDetailQuery.error}
            onRetry={() => void groupDetailQuery.refetch()}
          />
        </div>
      </div>
    </div>
  );
}