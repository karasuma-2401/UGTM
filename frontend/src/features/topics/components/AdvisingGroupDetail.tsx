import { useQuery, useQueries } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { StatusBadge } from '@/components/ui/status-badge';
import { useAuth } from '@/shared/auth';
import { advisingApi } from '../services/advisingApi';
import { getTopicErrorMessage } from '../services/topicApi';
import { GROUP_STATUS_LABELS } from '../types/advising.types';
import { confirmationLabel, formatAdvisingDate, groupLabel } from '../advising';
import { syllabusApi } from '@/features/thesis/services/syllabusApi';
import { midtermApi } from '@/features/thesis/services/midtermApi';
import { finalReportApi } from '@/features/thesis/services/finalReportApi';
import { milestoneApi } from '@/features/thesis/services/milestoneApi';
import { SYLLABUS_STATUS_LABELS } from '@/features/thesis/types/syllabus.types';
import { MIDTERM_STATUS_LABELS } from '@/features/thesis/types/midterm.types';
import { FINAL_REPORT_STATUS_LABELS } from '@/features/thesis/types/finalReport.types';
import { PROGRESS_REPORT_STATUS_LABELS } from '@/features/thesis/types/milestone.types';
import { SimilarityReportPanel } from '@/features/thesis/components/SimilarityReportPanel';

export function GroupStatusBadge({ status }: { status: string }) {
  const tone =
    status === 'Stopped'
      ? 'error'
      : status === 'SyllabusRevisionRequired'
        ? 'warning'
        : ['Completed', 'Defended', 'ScoreLocked'].includes(status)
          ? 'success'
          : 'info';
  return (
    <StatusBadge tone={tone} className="max-w-full whitespace-normal text-left">
      {GROUP_STATUS_LABELS[status as keyof typeof GROUP_STATUS_LABELS] ?? 'Chưa rõ tiến độ'}
    </StatusBadge>
  );
}

function ProgressSection({
  title,
  query,
  compact = false,
  className = '',
  children,
}: {
  title: string;
  query: { isPending: boolean; isError: boolean; error: unknown; refetch: () => unknown };
  compact?: boolean;
  className?: string;
  children: ReactNode;
}) {
  const containerClass = compact
    ? `space-y-1.5 ${className}`.trim()
    : `space-y-2.5 rounded-xl border border-slate-200/80 bg-slate-50/70 p-4 ${className}`.trim();

  return (
    <section className={containerClass}>
      <h3 className={compact ? 'text-xs font-medium text-slate-500' : 'text-sm font-semibold text-slate-900'}>
        {title}
      </h3>
      {query.isPending ? (
        <p role="status" className="text-sm text-slate-500">
          Đang tải…
        </p>
      ) : query.isError ? (
        <div role="alert" className="flex flex-wrap items-center gap-2 text-sm text-rose-700">
          <span>{getTopicErrorMessage(query.error)}</span>
          <Button size="sm" variant="outline" onClick={() => void query.refetch()}>
            Thử lại {title.toLowerCase()}
          </Button>
        </div>
      ) : (
        children
      )}
    </section>
  );
}

export function AdvisingGroupDetail({
  groupId,
  onClose,
}: {
  groupId: string;
  onClose: () => void;
}) {
  const { user } = useAuth();
  const [facultyOpen, setFacultyOpen] = useState(false);
  const [copyNotice, setCopyNotice] = useState('');
  const detail = useQuery({
    queryKey: ['topic-advising-group-detail', user?.id, groupId],
    queryFn: ({ signal }) => advisingApi.getGroupDetail(groupId, signal),
    enabled: Boolean(user?.id),
    staleTime: 0,
    refetchInterval: 30_000,
  });
  const enabled = Boolean(detail.data && !detail.isError);
  const syllabus = useQuery({
    queryKey: ['topic-advising-syllabus', user?.id, groupId],
    queryFn: ({ signal }) => syllabusApi.getSyllabusHistory(groupId, signal),
    enabled,
  });
  const midterm = useQuery({
    queryKey: ['topic-advising-midterm', user?.id, groupId],
    queryFn: ({ signal }) => midtermApi.getMidtermHistory(groupId, signal),
    enabled,
  });
  const finalReport = useQuery({
    queryKey: ['topic-advising-final-report', user?.id, groupId],
    queryFn: ({ signal }) => finalReportApi.getFinalReportHistory(groupId, signal),
    enabled,
  });
  const milestones = useQuery({
    queryKey: ['topic-advising-milestones', user?.id, groupId],
    queryFn: ({ signal }) => milestoneApi.getMilestones(groupId, signal),
    enabled,
  });
  const milestoneRows = milestones.isError ? [] : (milestones.data ?? []);
  const reports = useQueries({
    queries: milestoneRows.map((milestone) => ({
      queryKey: ['topic-advising-progress-report', user?.id, groupId, milestone.id],
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        milestoneApi.getProgressReport(milestone.id, signal),
      enabled,
    })),
  });
  const group = detail.isError ? undefined : detail.data;
  const facultyText = group
    ? `Kính gửi Văn phòng Khoa,\nTôi là ${user?.fullName ?? ''}. Tôi cần trao đổi về nhóm ${group.groupId}, đề tài ${group.topicTitleVi}.\nSinh viên: ${group.members.map((s) => `${s.fullName ?? 'Chưa có tên'} (${s.userCode ?? s.studentId})`).join(', ')}.\nLý do đề nghị xử lý: [Giảng viên bổ sung lý do và minh chứng].`
    : '';
  async function copy() {
    try {
      await navigator.clipboard.writeText(facultyText);
      setCopyNotice('Đã sao chép nội dung.');
    } catch {
      setCopyNotice('Không thể sao chép tự động. Bạn có thể chọn nội dung bên dưới để sao chép.');
    }
  }
  const confirmedCount = group
    ? group.members.filter((s) => s.confirmationStatus === 'Confirmed').length
    : 0;

  return (
    <>
      <Dialog
        title="Chi tiết nhóm & tiến độ"
        onClose={onClose}
        className="max-w-5xl"
        headerClassName="border-b border-slate-200/80 px-5 py-3.5 sm:px-6"
      >
        {detail.isPending ? (
          <div className="p-6">
            <p role="status" className="text-sm text-slate-500">
              Đang tải thông tin nhóm…
            </p>
          </div>
        ) : detail.isError ? (
          <div role="alert" className="space-y-3 p-6 text-sm text-rose-700">
            <p>{getTopicErrorMessage(detail.error)}</p>
            <Button variant="outline" onClick={() => void detail.refetch()}>
              Thử lại
            </Button>
          </div>
        ) : (
          group && (
            <div className="grid grid-cols-1 lg:grid-cols-12">
              <div className="space-y-5 bg-white p-5 sm:p-6 lg:col-span-8">
                <header className="space-y-2.5">
                  <div className="flex flex-wrap items-center gap-2">
                    <GroupStatusBadge status={group.groupStatus ?? ''} />
                    <span className="inline-flex items-center rounded-full border border-slate-200 bg-slate-50 px-2.5 py-0.5 text-xs font-medium text-slate-600">
                      {groupLabel(group.groupId)}
                    </span>
                  </div>
                  <h2 className="break-words text-xl font-bold leading-snug tracking-tight text-slate-900">
                    {group.topicTitleVi}
                  </h2>
                  {group.topicTitleEn && (
                    <p className="break-words text-sm leading-6 text-slate-500">
                      {group.topicTitleEn}
                    </p>
                  )}
                  <p className="break-all font-mono text-xs text-slate-400">
                    Mã đầy đủ: {group.groupId}
                  </p>
                </header>

                <details className="rounded-xl border border-slate-200/80 bg-slate-50/50">
                  <summary className="cursor-pointer select-none rounded-xl px-4 py-3 text-sm font-semibold text-slate-900 transition-colors hover:bg-slate-100/70">
                    Nội dung đề tài
                  </summary>
                  <div className="space-y-3.5 rounded-b-xl border-t border-slate-200/80 bg-white px-4 py-4">
                    <div className="space-y-1">
                      <h3 className="text-xs font-semibold text-slate-500">Mục tiêu và mô tả</h3>
                      <p className="whitespace-pre-wrap break-words text-sm leading-6 text-slate-700">
                        {group.description || 'Chưa có nội dung.'}
                      </p>
                    </div>
                    <div className="space-y-1 border-t border-slate-100 pt-3">
                      <h3 className="text-xs font-semibold text-slate-500">Yêu cầu</h3>
                      <p className="whitespace-pre-wrap break-words text-sm leading-6 text-slate-700">
                        {group.requirements || 'Chưa có nội dung.'}
                      </p>
                    </div>
                  </div>
                </details>

                <section className="space-y-3">
                  <h3 className="text-sm font-semibold text-slate-900">Tiến độ hồ sơ & báo cáo</h3>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <ProgressSection title="Đề cương" query={syllabus}>
                      <StatusBadge tone={syllabus.data ? 'info' : 'neutral'}>
                        {syllabus.data
                          ? (SYLLABUS_STATUS_LABELS[syllabus.data.currentStatus] ??
                            syllabus.data.currentStatus)
                          : 'Chưa nộp đề cương'}
                      </StatusBadge>
                    </ProgressSection>

                    <ProgressSection title="Báo cáo giữa kỳ" query={midterm}>
                      <StatusBadge tone={midterm.data ? 'info' : 'neutral'}>
                        {midterm.data
                          ? (MIDTERM_STATUS_LABELS[midterm.data.currentStatus] ??
                            midterm.data.currentStatus)
                          : 'Chưa nộp báo cáo giữa kỳ'}
                      </StatusBadge>
                    </ProgressSection>

                    <ProgressSection
                      title="Báo cáo cuối kỳ"
                      query={finalReport}
                      className="sm:col-span-2"
                    >
                      <StatusBadge tone={finalReport.data ? 'info' : 'neutral'}>
                        {finalReport.data
                          ? (FINAL_REPORT_STATUS_LABELS[
                              finalReport.data.currentStatus as keyof typeof FINAL_REPORT_STATUS_LABELS
                            ] ?? finalReport.data.currentStatus)
                          : 'Chưa nộp báo cáo cuối kỳ'}
                      </StatusBadge>
                      {finalReport.data && (
                        <div className="pt-1">
                          <SimilarityReportPanel
                            reportId={finalReport.data.finalReportId}
                            versionNo={finalReport.data.currentVersionNo}
                          />
                        </div>
                      )}
                    </ProgressSection>

                    <ProgressSection
                      title="Mốc tiến độ"
                      query={milestones}
                      className="sm:col-span-2"
                    >
                      {!milestones.data?.length && (
                        <p className="text-sm text-slate-500">Chưa thiết lập mốc tiến độ.</p>
                      )}
                      {milestoneRows.length > 0 && (
                        <div className="space-y-2.5">
                          {milestoneRows.map((milestone, index) => (
                            <div
                              key={milestone.id}
                              className="space-y-1.5 rounded-lg border border-slate-200/80 bg-white p-3.5"
                            >
                              <p className="text-sm font-semibold text-slate-900">
                                {milestone.title}
                              </p>
                              <ProgressSection
                                title="Báo cáo tiến độ"
                                query={reports[index]}
                                compact
                              >
                                <StatusBadge tone={reports[index].data ? 'info' : 'neutral'}>
                                  {reports[index].data
                                    ? (PROGRESS_REPORT_STATUS_LABELS[
                                        reports[index].data
                                          .currentStatus as keyof typeof PROGRESS_REPORT_STATUS_LABELS
                                      ] ?? reports[index].data.currentStatus)
                                    : 'Chưa nộp'}
                                </StatusBadge>
                              </ProgressSection>
                            </div>
                          ))}
                        </div>
                      )}
                    </ProgressSection>
                  </div>
                </section>
              </div>

              <aside className="space-y-4 border-t border-slate-200/80 bg-slate-50/70 p-5 sm:p-6 lg:col-span-4 lg:border-l lg:border-t-0">
                <dl className="space-y-3 rounded-xl border border-slate-200/80 bg-white p-4 text-sm">
                  <div>
                    <dt className="text-xs font-medium text-slate-500">Đã xác nhận / chỉ tiêu</dt>
                    <dd className="mt-1 flex items-baseline justify-between gap-2">
                      <span className="text-lg font-bold tabular-nums text-slate-900">
                        {confirmedCount}/{group.studentNumber ?? '—'}
                        <span className="ml-1.5 text-xs font-medium text-slate-500">sinh viên</span>
                      </span>
                    </dd>
                  </div>
                  <div className="border-t border-slate-100 pt-2.5">
                    <dt className="text-xs font-medium text-slate-500">Đợt khóa luận</dt>
                    <dd className="mt-1 break-words font-medium text-slate-800">
                      {group.batchName ?? 'Chưa có tên đợt'}
                    </dd>
                  </div>
                  <div className="border-t border-slate-100 pt-2.5">
                    <dt className="text-xs font-medium text-slate-500">Giảng viên</dt>
                    <dd className="mt-1 font-medium text-slate-800">
                      {group.advisor?.fullName ?? 'Chưa có tên giảng viên'}
                    </dd>
                  </div>
                </dl>

                <section className="space-y-3">
                  <div className="flex items-center justify-between gap-2">
                    <h3 className="text-sm font-semibold text-slate-900">Thành viên & lời mời</h3>
                    <span className="rounded-full border border-slate-200 bg-white px-2.5 py-0.5 text-xs font-medium tabular-nums text-slate-600">
                      {group.members.length}
                    </span>
                  </div>
                  {group.members.length === 0 && (
                    <p className="rounded-xl border border-slate-200/80 bg-white p-3.5 text-sm text-slate-500">
                      Nhóm chưa có thành viên.
                    </p>
                  )}
                  <div className="space-y-2.5">
                    {group.members.map((student) => (
                      <div
                        key={student.studentId}
                        className="space-y-1.5 rounded-xl border border-slate-200/80 bg-white p-3.5 text-sm"
                      >
                        <div className="flex flex-wrap items-center justify-between gap-1.5">
                          <p className="font-semibold text-slate-900">
                            {student.fullName ?? 'Chưa có tên sinh viên'}
                          </p>
                          <span className="rounded-md bg-slate-100 px-2 py-0.5 font-mono text-xs text-slate-600">
                            {student.userCode ?? 'Chưa có MSSV'}
                          </span>
                        </div>
                        <p
                          className={
                            student.invitationExpired
                              ? 'text-xs font-medium text-rose-700'
                              : student.confirmationStatus === 'Confirmed'
                                ? 'text-xs font-medium text-emerald-700'
                                : 'text-xs font-medium text-amber-700'
                          }
                        >
                          {confirmationLabel(
                            student.confirmationStatus,
                            student.confirmationExpiresAtUtc,
                            student.invitationExpired,
                          )}
                        </p>
                        {student.email && (
                          <p className="break-all text-xs text-slate-600">{student.email}</p>
                        )}
                        {student.confirmedAtUtc && (
                          <p className="text-xs text-slate-500">
                            Xác nhận lúc {formatAdvisingDate(student.confirmedAtUtc)}
                          </p>
                        )}
                        {student.confirmationStatus !== 'Confirmed' &&
                          student.confirmationExpiresAtUtc && (
                            <p className="text-xs text-slate-500">
                              Hạn xác nhận: {formatAdvisingDate(student.confirmationExpiresAtUtc)}
                            </p>
                          )}
                      </div>
                    ))}
                  </div>
                  <p className="text-xs leading-5 text-slate-500">
                    Sinh viên được mời có 24 giờ để xác nhận theo hạn hệ thống. Sinh viên đăng ký
                    trực tiếp hoặc được giảng viên chỉ định có hiệu lực ngay.
                  </p>
                </section>
              </aside>
            </div>
          )
        )}
        <div className="sticky bottom-0 z-10 flex flex-wrap items-center justify-between gap-2.5 border-t border-slate-200/80 bg-white/95 px-5 py-3.5 backdrop-blur-md sm:px-6">
          <div>
            {group && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setFacultyOpen(true);
                  setCopyNotice('');
                }}
                className="h-9 rounded-xl border-slate-200 px-3.5 text-xs font-semibold"
              >
                Soạn nội dung liên hệ Khoa
              </Button>
            )}
          </div>
          <Button
            variant="outline"
            onClick={onClose}
            className="h-9 rounded-xl border-slate-200 px-4 text-xs font-semibold"
          >
            Đóng
          </Button>
        </div>
      </Dialog>
      {facultyOpen && (
        <Dialog
          title="Soạn nội dung liên hệ Khoa"
          onClose={() => setFacultyOpen(false)}
          className="max-w-xl"
          headerClassName="border-b border-slate-200/80 px-5 py-3.5 sm:px-6"
        >
          <div className="space-y-4 p-5 sm:p-6">
            <p className="text-sm text-slate-600">
              Sao chép nội dung để gửi qua kênh liên hệ của Khoa. Thao tác này chưa gửi yêu cầu.
            </p>
            <label className="block space-y-2 text-sm">
              <span className="font-medium text-slate-800">Nội dung tham khảo</span>
              <textarea
                readOnly
                value={facultyText}
                rows={8}
                className="w-full rounded-xl border border-slate-200 bg-slate-50/70 p-3 text-sm leading-relaxed text-slate-800 focus:border-primary focus:bg-white focus:outline-none"
              />
            </label>
            <p role="status" className="text-sm text-emerald-700">
              {copyNotice}
            </p>
          </div>
          <div className="flex justify-end gap-2.5 border-t border-slate-200/80 bg-slate-50/70 px-5 py-3.5 sm:px-6">
            <Button
              variant="outline"
              onClick={() => setFacultyOpen(false)}
              className="h-9 rounded-xl border-slate-200 px-4 text-xs font-semibold"
            >
              Đóng
            </Button>
            <Button
              onClick={() => void copy()}
              className="h-9 rounded-xl px-4 text-xs font-semibold"
            >
              Sao chép nội dung
            </Button>
          </div>
        </Dialog>
      )}
    </>
  );
}
