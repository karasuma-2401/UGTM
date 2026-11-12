import { InlineAlert } from '@/components/ui/inline-alert';
import { OperationToast } from '@/shared/notifications';
import { useStudentMilestones } from '../hooks/useStudentMilestones';
import {
  MilestoneStatusBadge,
  StudentMilestoneUploadCard,
  StudentMilestoneHistoryTable,
  StudentMilestoneDetailCard,
} from '../components/milestone';
import { ThesisSubmissionNavigation } from '../components/navigation';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { AlertCircle, BookOpen, Flag, ShieldAlert, RotateCcw } from 'lucide-react';
import { Link } from 'react-router-dom';
import { classifyThesisError, getThesisErrorMessage } from '../utils/thesisErrors';

export function StudentMilestoneSubmissionPage() {
  const {
    statusQuery,
    groupDetailQuery,
    milestonesQuery,
    milestones,
    selectedMilestoneId,
    setSelectedMilestoneId,
    selectedMilestone,
    selectedMilestoneIndex,
    reportQuery,
    report,
    versions,
    currentStatus,
    nextVersionNo,
    canSubmit,
    selectedFile,
    setSelectedFile,
    gitUrl,
    setGitUrl,
    studentNote,
    setStudentNote,
    actionError,
    actionSuccess,
    setActionSuccess,
    submitMutation,
    handleDownload,
  } = useStudentMilestones();

  const groupDetail = groupDetailQuery.data;
  const groupCode =
    groupDetail?.groupCode ||
    (statusQuery.data?.groupId
      ? `Nhóm ${statusQuery.data.groupId.slice(0, 4).toUpperCase()}`
      : 'Nhóm KLTN');

  const milestoneCode = String(selectedMilestoneIndex + 1).padStart(2, '0');

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
              ? 'Không có quyền truy cập mốc tiến độ'
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
            Bạn chưa đăng ký đề tài nào trong đợt KLTN này hoặc đăng ký chưa được xác nhận. Vui lòng tra cứu và đăng ký đề tài trước khi nộp tiến độ.
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

  const milestoneOptions = milestones.map((m, idx) => ({
    value: m.id,
    label: `Mốc ${String(idx + 1).padStart(2, '0')} - ${m.title}`,
  }));

  const getDisabledReason = () => {
    if (milestones.length === 0) {
      return 'GVHD chưa thiết lập mốc tiến độ nào cho nhóm.';
    }
    if (currentStatus === 'Submitted') {
      return 'Báo cáo của mốc này đang ở trạng thái Chờ đánh giá từ GVHD.';
    }
    if (currentStatus === 'Accepted') {
      return 'Mốc báo cáo này đã được GVHD đánh giá Đạt.';
    }
    return null;
  };

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-6">
      <PageHeader
        title="Nộp sản phẩm theo mốc"
        description={
          selectedMilestone
            ? `Mốc báo cáo ${milestoneCode} - ${selectedMilestone.title}`
            : 'Theo dõi và nộp báo cáo tiến độ theo yêu cầu của GVHD'
        }
        icon={<Flag />}
        navigation={<ThesisSubmissionNavigation />}
      >
        <MilestoneStatusBadge
          status={currentStatus}
          versionNo={report?.currentVersionNo}
          className="text-xs px-3 py-1.5"
        />
      </PageHeader>

      <div className="p-4 rounded-xl bg-white border border-slate-200/80 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="flex flex-col sm:flex-row sm:items-center gap-3 w-full sm:max-w-xl">
          <label
            htmlFor="select-milestone"
            className="text-xs font-semibold text-slate-700 uppercase tracking-wide whitespace-nowrap"
          >
            Mốc báo cáo
          </label>
          <div className="flex-1 min-w-0">
            <Select
              options={milestoneOptions}
              value={selectedMilestoneId || ''}
              onChange={(val) => {
                setSelectedMilestoneId(val ? String(val) : null);
                setSelectedFile(null);
                setStudentNote('');
              }}
              placeholder={
                milestonesQuery.isLoading
                  ? 'Đang tải danh sách mốc...'
                  : milestones.length === 0
                  ? 'Chưa có mốc tiến độ'
                  : 'Chọn mốc báo cáo'
              }
              triggerClassName="h-10 text-sm bg-white"
            />
          </div>
        </div>

        {milestones.length === 0 && !milestonesQuery.isLoading && !milestonesQuery.isError && (
          <span className="text-xs text-amber-700 font-medium bg-amber-50 px-3 py-1.5 rounded-md border border-amber-200">
            GVHD chưa tạo mốc tiến độ nào cho nhóm bạn.
          </span>
        )}
      </div>

      {(milestonesQuery.isError || reportQuery.isError) && (() => {
        const err = milestonesQuery.error || reportQuery.error;
        const kind = classifyThesisError(err);
        const msg = getThesisErrorMessage(err, 'Không thể tải dữ liệu mốc tiến độ.');
        return (
          <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-start gap-3">
              {kind === 'forbidden' ? (
                <ShieldAlert className="size-5 shrink-0 text-amber-600 mt-0.5" />
              ) : (
                <AlertCircle className="size-5 shrink-0 text-rose-600 mt-0.5" />
              )}
              <div>
                <p className="font-semibold">
                  {kind === 'forbidden'
                    ? 'Không có quyền truy cập mốc tiến độ'
                    : 'Không tải được dữ liệu mốc tiến độ'}
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
                  void milestonesQuery.refetch();
                  if (selectedMilestoneId) void reportQuery.refetch();
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
          <StudentMilestoneUploadCard
            versionNo={nextVersionNo}
            selectedFile={selectedFile}
            gitUrl={gitUrl}
            studentNote={studentNote}
            onFileSelect={setSelectedFile}
            onGitUrlChange={setGitUrl}
            onNoteChange={setStudentNote}
            disabled={!canSubmit || submitMutation.isPending || milestones.length === 0}
            disabledReason={getDisabledReason()}
          />

          <StudentMilestoneHistoryTable
            versions={versions}
            nextVersionNo={nextVersionNo}
            selectedFile={selectedFile}
            groupCode={groupCode}
            canSubmit={canSubmit && milestones.length > 0}
            isSubmitting={submitMutation.isPending}
            onSubmit={() => submitMutation.mutate()}
            onDownload={handleDownload}
          />
        </div>

        <div className="lg:col-span-4 sticky top-6">
          <StudentMilestoneDetailCard
            milestone={selectedMilestone}
            report={report}
            groupDetail={groupDetail}
            milestoneIndex={selectedMilestoneIndex}
          />
        </div>
      </div>
    </div>
  );
}