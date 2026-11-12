import { InlineAlert } from '@/components/ui/inline-alert';
import { OperationToast } from '@/shared/notifications';
import { useNavigate } from 'react-router-dom';
import { useAdvisorMilestones } from '../hooks/useAdvisorMilestones';
import {
  AdvisorMilestoneTable,
  AdvisorMilestoneForm,
  AdvisorGroupPlanCard,
} from '../components/milestone';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { AlertCircle, Target, Users, ShieldAlert, RotateCcw } from 'lucide-react';
import { classifyThesisError, getThesisErrorMessage } from '../utils/thesisErrors';

export function AdvisorMilestoneSetupPage() {
  const navigate = useNavigate();
  const {
    topicsQuery,
    milestonesQuery,
    advisedGroups,
    selectedGroupId,
    setSelectedGroupId,
    selectedGroup,
    milestones,
    selectedMilestone,
    setSelectedMilestone,
    actionError,
    actionSuccess,
    setActionSuccess,
    createMutation,
    canCreateMilestones,
    ineligibleReason,
  } = useAdvisorMilestones();

  const groupCode = selectedGroup?.groupCode || 'Nhóm KLTN';

  const groupOptions = advisedGroups.map((g) => ({
    value: g.groupId,
    label: `${g.groupCode} - ${g.studentNames}`,
  }));

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-6">
      <PageHeader
        title="Thiết lập milestones"
        description={`GVHD thiết lập mốc riêng cho ${groupCode}.`}
        icon={<Target />}
      >
        <span className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200/80">
          {milestones.length} mốc đang theo dõi
        </span>
      </PageHeader>

      <div className="p-4 rounded-xl bg-white border border-slate-200/80 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="flex flex-col sm:flex-row sm:items-center gap-3 w-full sm:max-w-xl">
          <label
            htmlFor="select-group"
            className="text-xs font-semibold text-slate-700 uppercase tracking-wide whitespace-nowrap flex items-center gap-1.5"
          >
            <Users className="size-4 text-slate-500" />
            Nhóm hướng dẫn
          </label>
          <div className="flex-1 min-w-0">
            <Select
              options={groupOptions}
              value={selectedGroupId || ''}
              onChange={(val) => {
                setSelectedGroupId(val ? String(val) : null);
                setSelectedMilestone(null);
              }}
              placeholder="Chọn nhóm hướng dẫn"
              triggerClassName="h-10 text-sm bg-white"
            />
          </div>
        </div>

        {advisedGroups.length === 0 && !topicsQuery.isLoading && !topicsQuery.isError && (
          <span className="text-xs text-amber-700 font-medium bg-amber-50 px-3 py-1.5 rounded-md border border-amber-200">
            Bạn chưa có nhóm khóa luận nào được phân công trong đợt này.
          </span>
        )}
      </div>

      {(topicsQuery.isError || milestonesQuery.isError) && (() => {
        const err = topicsQuery.error || milestonesQuery.error;
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
                    ? 'Không có quyền truy cập danh sách mốc tiến độ'
                    : 'Không tải được danh sách mốc tiến độ'}
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
                  if (topicsQuery.isError) void topicsQuery.refetch();
                  if (milestonesQuery.isError) void milestonesQuery.refetch();
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

      {!canCreateMilestones && ineligibleReason && !topicsQuery.isError && (
        <div className="p-4 rounded-xl bg-amber-50/90 border border-amber-200 text-amber-900 flex items-start gap-3.5 shadow-xs">
          <div className="p-2 rounded-lg bg-amber-100/80 text-amber-700 shrink-0 mt-0.5">
            <AlertCircle className="size-5" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="font-semibold text-sm text-amber-900">
              Nhóm chưa ở trạng thái cho phép thiết lập mốc tiến độ
            </p>
            <p className="text-xs mt-1 text-amber-800/90 leading-relaxed">
              {ineligibleReason}
            </p>
          </div>
        </div>
      )}

      {actionError && <InlineAlert title="Không thể hoàn tất">{actionError}</InlineAlert>}

      <OperationToast message={actionSuccess} onConsumed={() => setActionSuccess(null)} />

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8 space-y-6">
          <AdvisorMilestoneTable
            milestones={milestones}
            selectedMilestoneId={selectedMilestone?.milestone.id}
            onSelectMilestone={(item) => setSelectedMilestone(item)}
            onReviewMilestone={(id) => navigate(`/advisor/milestones/${id}/review`)}
          />

          <AdvisorMilestoneForm
            selectedMilestone={selectedMilestone}
            isSubmitting={createMutation.isPending}
            disabled={!canCreateMilestones}
            disabledReason={ineligibleReason}
            onSubmit={(data) => createMutation.mutate(data)}
            onResetForm={() => setSelectedMilestone(null)}
            onNavigateReview={() => {
              if (selectedMilestone) {
                navigate(`/advisor/milestones/${selectedMilestone.milestone.id}/review`);
              }
            }}
          />
        </div>

        <div className="lg:col-span-4 sticky top-6">
          <AdvisorGroupPlanCard
            group={selectedGroup}
            milestones={milestones}
            selectedMilestone={selectedMilestone}
          />
        </div>
      </div>
    </div>
  );
}
