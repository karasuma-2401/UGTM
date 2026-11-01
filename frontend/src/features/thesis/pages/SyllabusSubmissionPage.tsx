import { useSyllabusSubmission } from '../hooks/useSyllabusSubmission';
import {
  SyllabusStatusBadge,
  SyllabusUploadCard,
  SyllabusHistoryTable,
  SyllabusTopicInfoCard,
} from '../components/submission';
import { ThesisSubmissionNavigation } from '../components/navigation';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/ui/page-header';
import { AlertCircle, CheckCircle2, BookOpen, FileUp } from 'lucide-react';
import { Link } from 'react-router-dom';

export function SyllabusSubmissionPage() {
  const {
    statusQuery,
    groupDetailQuery,
    historyQuery,
    selectedFile,
    setSelectedFile,
    actionError,
    actionSuccess,
    submitMutation,
    handleDownload,
    currentStatus,
    canSubmit,
  } = useSyllabusSubmission();

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
            Bạn chưa đăng ký đề tài nào trong đợt KLTN này hoặc đăng ký chưa được xác nhận. Vui lòng tra cứu và đăng ký đề tài trước khi nộp đề cương.
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
        <SyllabusStatusBadge status={currentStatus} className="text-xs px-3 py-1.5" />
      </PageHeader>

      {actionError && (
        <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm flex items-start gap-3">
          <AlertCircle className="size-5 shrink-0 text-rose-600 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold">Thao tác không thành công</p>
            <p className="text-xs mt-0.5 text-rose-700">{actionError}</p>
          </div>
        </div>
      )}

      {actionSuccess && (
        <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-sm flex items-start gap-3">
          <CheckCircle2 className="size-5 shrink-0 text-emerald-600 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold">Thông báo</p>
            <p className="text-xs mt-0.5 text-emerald-700">{actionSuccess}</p>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8 space-y-6">
          <SyllabusUploadCard
            selectedFile={selectedFile}
            onFileSelect={setSelectedFile}
            disabled={!canSubmit || submitMutation.isPending}
          />

          <SyllabusHistoryTable
            versions={versions}
            selectedFile={selectedFile}
            groupCode={groupCode}
            currentStatus={currentStatus}
            canSubmit={canSubmit}
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
          />
        </div>
      </div>
    </div>
  );
}