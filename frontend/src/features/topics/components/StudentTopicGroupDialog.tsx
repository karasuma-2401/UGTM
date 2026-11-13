import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ArrowUpRight, GraduationCap } from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { InfoCell } from '@/components/ui/info-cell';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { useAuth } from '@/shared/auth';
import { syllabusApi } from '@/features/thesis/services/syllabusApi';
import { midtermApi } from '@/features/thesis/services/midtermApi';
import { finalReportApi } from '@/features/thesis/services/finalReportApi';
import { milestoneApi } from '@/features/thesis/services/milestoneApi';
import { SYLLABUS_STATUS_LABELS } from '@/features/thesis/types/syllabus.types';
import { MIDTERM_STATUS_LABELS } from '@/features/thesis/types/midterm.types';
import { FINAL_REPORT_STATUS_LABELS } from '@/features/thesis/types/finalReport.types';
import { PROGRESS_REPORT_STATUS_LABELS } from '@/features/thesis/types/milestone.types';
import { GROUP_STATUS_LABELS } from '../types/advising.types';
import { studentTopicApi } from '../services/studentTopicApi';
import { getTopicErrorMessage } from '../services/topicApi';

const memberStatusLabels: Record<string, string> = {
  Confirmed: 'Đã xác nhận',
  Pending: 'Chờ xác nhận',
  Expired: 'Hết hạn xác nhận',
};

export function StudentTopicGroupDialog({
  groupId,
  onClose,
}: {
  groupId: string;
  onClose: () => void;
}) {
  const { user } = useAuth();
  const scope = [user?.id, groupId];
  const group = useQuery({
    queryKey: ['student-topic-group', ...scope],
    queryFn: ({ signal }) => studentTopicApi.getGroupDetail(groupId, signal),
  });
  const syllabus = useQuery({
    queryKey: ['student-topic-syllabus', ...scope],
    queryFn: ({ signal }) => syllabusApi.getSyllabusHistory(groupId, signal),
    enabled: group.isSuccess,
  });
  const midterm = useQuery({
    queryKey: ['student-topic-midterm', ...scope],
    queryFn: ({ signal }) => midtermApi.getMidtermHistory(groupId, signal),
    enabled: group.isSuccess,
  });
  const finalReport = useQuery({
    queryKey: ['student-topic-final-report', ...scope],
    queryFn: ({ signal }) => finalReportApi.getFinalReportHistory(groupId, signal),
    enabled: group.isSuccess,
  });
  const milestones = useQuery({
    queryKey: ['student-topic-milestones', ...scope],
    queryFn: async ({ signal }) => {
      const items = await milestoneApi.getMilestones(groupId, signal);
      return Promise.all(
        items.map(async (milestone) => ({
          milestone,
          report: await milestoneApi.getProgressReport(milestone.id, signal),
        })),
      );
    },
    enabled: group.isSuccess,
  });

  const progress = [
    {
      label: 'Đề cương',
      status: syllabus.data
        ? (SYLLABUS_STATUS_LABELS[syllabus.data.currentStatus] ?? syllabus.data.currentStatus)
        : 'Chưa nộp',
      query: syllabus,
      path: '/thesis/proposal',
    },
    {
      label: 'Báo cáo giữa kỳ',
      status: midterm.data
        ? (MIDTERM_STATUS_LABELS[midterm.data.currentStatus] ?? midterm.data.currentStatus)
        : 'Chưa nộp',
      query: midterm,
      path: '/thesis/midterm',
    },
    {
      label: 'Báo cáo cuối kỳ',
      status: finalReport.data
        ? (FINAL_REPORT_STATUS_LABELS[
            finalReport.data.currentStatus as keyof typeof FINAL_REPORT_STATUS_LABELS
          ] ?? finalReport.data.currentStatus)
        : 'Chưa nộp',
      query: finalReport,
      path: '/thesis/final-report',
    },
  ];

  return (
    <Dialog title="Theo dõi đề tài của tôi" onClose={onClose} className="max-w-3xl">
      <div className="space-y-5 p-5 sm:p-6">
        {group.isPending ? (
          <div role="status" className="space-y-3">
            <span className="sr-only">Đang tải thông tin nhóm...</span>
            <Skeleton className="h-6 w-64" />
            <Skeleton className="h-20 w-full" />
          </div>
        ) : group.isError ? (
          <div role="alert" className="space-y-2 text-sm text-rose-700">
            <p>{getTopicErrorMessage(group.error)}</p>
            <Button type="button" variant="outline" size="sm" onClick={() => void group.refetch()}>
              Thử lại thông tin nhóm
            </Button>
          </div>
        ) : group.data ? (
          <>
            <div className="rounded-xl border border-slate-200/80 bg-gradient-to-r from-slate-50/80 via-white to-blue-50/40 p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0 flex-1 space-y-1">
                  <h2 className="text-lg font-semibold leading-snug text-slate-900">
                    {group.data.topicTitleVi}
                  </h2>
                  {group.data.topicTitleEn && (
                    <p className="text-sm text-slate-500">{group.data.topicTitleEn}</p>
                  )}
                </div>
                <StatusBadge tone="info">
                  {GROUP_STATUS_LABELS[group.data.groupStatus as keyof typeof GROUP_STATUS_LABELS] ??
                    group.data.groupStatus}
                </StatusBadge>
              </div>
              <div className="mt-3 flex flex-wrap items-center gap-x-5 gap-y-1.5 text-xs text-slate-600">
                <span>{group.data.batchName || `Mã đợt: ${group.data.batchId}`}</span>
                <span className="inline-flex items-center gap-1.5">
                  <GraduationCap className="size-3.5 text-slate-400" aria-hidden="true" />
                  Giảng viên hướng dẫn:{' '}
                  <strong className="font-medium text-slate-800">
                    {group.data.advisor.fullName || 'Chưa cập nhật'}
                  </strong>
                </span>
              </div>
            </div>
            <section className="space-y-2.5" aria-label="Thành viên nhóm">
              <h3 className="text-sm font-semibold text-slate-800">Thành viên nhóm</h3>
              <div className="grid gap-2.5 sm:grid-cols-2">
                {group.data.members.map((member) => {
                  const displayName = member.fullName || member.userCode || 'Chưa cập nhật tên';
                  const statusTone =
                    member.confirmationStatus === 'Confirmed'
                      ? 'success'
                      : member.confirmationStatus === 'Expired'
                        ? 'error'
                        : 'warning';
                  return (
                    <div
                      key={member.studentId}
                      className="flex items-center justify-between gap-3 rounded-xl border border-slate-200/80 bg-slate-50/60 p-3 text-sm"
                    >
                      <InfoCell
                        leading={<Avatar name={displayName} className="size-8 text-[11px]" />}
                        title={
                          <span className="text-sm font-medium text-slate-800">{displayName}</span>
                        }
                        description={
                          member.userCode ? (
                            <span className="font-mono text-xs text-slate-500">
                              {member.userCode}
                            </span>
                          ) : undefined
                        }
                      />
                      <StatusBadge tone={statusTone}>
                        {memberStatusLabels[member.confirmationStatus] ?? member.confirmationStatus}
                      </StatusBadge>
                    </div>
                  );
                })}
              </div>
            </section>
          </>
        ) : null}
        {group.data && (
          <section className="space-y-3 border-t border-slate-100 pt-4" aria-label="Tiến độ hồ sơ">
            <h3 className="text-sm font-semibold text-slate-800">Tiến độ hồ sơ</h3>
            <div className="grid gap-3 sm:grid-cols-3">
              {progress.map(({ label, status, query, path }) => (
                <div
                  key={label}
                  className="flex flex-col justify-between gap-3 rounded-xl border border-slate-200 p-3 text-sm"
                >
                  <div>
                    <p className="font-medium text-slate-800">{label}</p>
                    {query.isPending ? (
                      <Skeleton className="mt-2 h-4 w-24" />
                    ) : query.isError ? (
                      <p role="alert" className="mt-2 text-xs text-rose-700">
                        {getTopicErrorMessage(query.error)}
                      </p>
                    ) : (
                      <p className="mt-2 text-xs text-slate-600">{status}</p>
                    )}
                  </div>
                  {query.isError ? (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => void query.refetch()}
                    >
                      Thử lại
                    </Button>
                  ) : (
                    <Link
                      className="text-xs font-medium text-primary hover:underline"
                      to={path}
                      onClick={onClose}
                    >
                      Xem hồ sơ
                    </Link>
                  )}
                </div>
              ))}
            </div>
            {milestones.isPending ? (
              <Skeleton className="h-14 w-full" />
            ) : milestones.isError ? (
              <div role="alert" className="flex flex-wrap items-center gap-2 text-xs text-rose-700">
                <span>{getTopicErrorMessage(milestones.error)}</span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => void milestones.refetch()}
                >
                  Thử lại mốc tiến độ
                </Button>
              </div>
            ) : milestones.data.length > 0 ? (
              <div className="space-y-2">
                <h4 className="text-xs font-semibold text-slate-700">Mốc tiến độ</h4>
                {milestones.data.map(({ milestone, report }) => (
                  <div
                    key={milestone.id}
                    className="flex flex-wrap justify-between gap-2 rounded-lg bg-slate-50 p-3 text-sm"
                  >
                    <span>{milestone.title}</span>
                    <span className="text-slate-600">
                      {report
                        ? (PROGRESS_REPORT_STATUS_LABELS[
                            report.currentStatus as keyof typeof PROGRESS_REPORT_STATUS_LABELS
                          ] ?? report.currentStatus)
                        : 'Chưa nộp'}
                    </span>
                  </div>
                ))}
                <Link
                  className="inline-block text-xs font-medium text-primary hover:underline"
                  to="/thesis/milestones"
                  onClick={onClose}
                >
                  Xem báo cáo tiến độ
                </Link>
              </div>
            ) : (
              <p className="text-xs text-slate-500">Chưa có mốc tiến độ.</p>
            )}
          </section>
        )}
        <div className="flex flex-wrap items-center justify-end gap-3 border-t border-slate-100 pt-4">
          <Link
            to="/topics/rename"
            onClick={onClose}
            className="mr-auto inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline"
          >
            <span>Xin đổi tên đề tài</span>
            <ArrowUpRight className="size-3.5" aria-hidden="true" />
          </Link>
          <Button type="button" variant="outline" onClick={onClose} className="h-9 rounded-xl">
            Đóng
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
