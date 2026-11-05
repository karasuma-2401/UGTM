import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
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
            <div>
              <h2 className="text-lg font-semibold text-slate-900">{group.data.topicTitleVi}</h2>
              {group.data.topicTitleEn && (
                <p className="mt-1 text-sm text-slate-500">{group.data.topicTitleEn}</p>
              )}
              <p className="mt-2 text-xs text-slate-600">
                {group.data.batchName || `Mã đợt: ${group.data.batchId}`} ·{' '}
                {GROUP_STATUS_LABELS[group.data.groupStatus] ?? group.data.groupStatus}
              </p>
              <p className="mt-1 text-xs text-slate-600">
                Giảng viên hướng dẫn: {group.data.advisor.fullName || 'Chưa cập nhật'}
              </p>
            </div>
            <section className="space-y-2" aria-label="Thành viên nhóm">
              <h3 className="text-sm font-semibold text-slate-800">Thành viên nhóm</h3>
              {group.data.members.map((member) => (
                <div key={member.studentId} className="rounded-lg bg-slate-50 p-3 text-sm">
                  <p className="font-medium text-slate-800">
                    {member.fullName || member.userCode || 'Chưa cập nhật tên'}
                    {member.userCode && (
                      <span className="ml-2 font-normal text-slate-500">{member.userCode}</span>
                    )}
                  </p>
                  <p className="mt-1 text-xs text-slate-600">
                    {memberStatusLabels[member.confirmationStatus] ?? member.confirmationStatus}
                  </p>
                </div>
              ))}
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
        <div className="flex justify-end border-t border-slate-100 pt-4">
          <Link
            to="/topics/rename"
            onClick={onClose}
            className="mr-auto inline-flex items-center text-sm font-medium text-primary hover:underline"
          >
            Xin đổi tên đề tài
          </Link>
          <Button type="button" variant="outline" onClick={onClose}>
            Đóng
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
