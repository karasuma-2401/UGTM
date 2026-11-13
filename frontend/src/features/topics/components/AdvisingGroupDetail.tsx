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
  children,
}: {
  title: string;
  query: { isPending: boolean; isError: boolean; error: unknown; refetch: () => unknown };
  children: ReactNode;
}) {
  return (
    <section className="space-y-2 border-t border-slate-200 pt-4">
      <h3 className="text-sm font-semibold text-slate-900">{title}</h3>
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
  return (
    <>
      <Dialog title="Chi tiết nhóm & tiến độ" onClose={onClose} className="max-w-4xl">
        <div className="space-y-5 p-4 sm:p-6">
          {detail.isPending ? (
            <p role="status" className="text-sm text-slate-500">
              Đang tải thông tin nhóm…
            </p>
          ) : detail.isError ? (
            <div role="alert" className="space-y-3 text-sm text-rose-700">
              <p>{getTopicErrorMessage(detail.error)}</p>
              <Button variant="outline" onClick={() => void detail.refetch()}>
                Thử lại
              </Button>
            </div>
          ) : (
            group && (
              <>
                <div className="space-y-2">
                  <GroupStatusBadge status={group.groupStatus ?? ''} />
                  <h2 className="text-lg font-semibold leading-7 text-slate-900">
                    {group.topicTitleVi}
                  </h2>
                  {group.topicTitleEn && (
                    <p className="text-sm text-slate-500">{group.topicTitleEn}</p>
                  )}
                  <p className="break-all text-xs text-slate-500">
                    {groupLabel(group.groupId)} · Mã đầy đủ: {group.groupId}
                  </p>
                </div>
                <dl className="grid gap-4 rounded-lg bg-slate-50 p-4 text-sm sm:grid-cols-3">
                  <div>
                    <dt className="text-slate-500">Đợt khóa luận</dt>
                    <dd className="mt-1 break-words">{group.batchName ?? 'Chưa có tên đợt'}</dd>
                  </div>
                  <div>
                    <dt className="text-slate-500">Giảng viên</dt>
                    <dd className="mt-1">{group.advisor?.fullName ?? 'Chưa có tên giảng viên'}</dd>
                  </div>
                  <div>
                    <dt className="text-slate-500">Đã xác nhận / chỉ tiêu</dt>
                    <dd className="mt-1">
                      {group.members.filter((s) => s.confirmationStatus === 'Confirmed').length}/
                      {group.studentNumber ?? '—'} sinh viên
                    </dd>
                  </div>
                </dl>
                <section className="space-y-3">
                  <h3 className="text-sm font-semibold">Thành viên & lời mời</h3>
                  {group.members.length === 0 && (
                    <p className="text-sm text-slate-500">Nhóm chưa có thành viên.</p>
                  )}
                  {group.members.map((student) => (
                    <div
                      key={student.studentId}
                      className="space-y-1 border-l border-slate-200 pl-3 text-sm"
                    >
                      <p className="font-medium">
                        {student.fullName ?? 'Chưa có tên sinh viên'}{' '}
                        <span className="font-normal text-slate-500">
                          · {student.userCode ?? 'Chưa có MSSV'}
                        </span>
                      </p>
                      <p
                        className={
                          student.invitationExpired
                            ? 'text-rose-700'
                            : student.confirmationStatus === 'Confirmed'
                              ? 'text-emerald-700'
                              : 'text-amber-700'
                        }
                      >
                        {confirmationLabel(
                          student.confirmationStatus,
                          student.confirmationExpiresAtUtc,
                          student.invitationExpired,
                        )}
                      </p>
                      {student.email && <p className="break-all text-slate-600">{student.email}</p>}
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
                  <p className="text-xs leading-5 text-slate-500">
                    Sinh viên được mời có 24 giờ để xác nhận theo hạn hệ thống. Sinh viên đăng ký
                    trực tiếp hoặc được giảng viên chỉ định có hiệu lực ngay.
                  </p>
                </section>
                <details className="border-t border-slate-200 pt-4">
                  <summary className="cursor-pointer text-sm font-semibold">
                    Nội dung đề tài
                  </summary>
                  <h3 className="mt-3 text-sm font-medium">Mục tiêu và mô tả</h3>
                  <p className="mt-1 whitespace-pre-wrap break-words text-sm leading-6 text-slate-600">
                    {group.description || 'Chưa có nội dung.'}
                  </p>
                  <h3 className="mt-3 text-sm font-medium">Yêu cầu</h3>
                  <p className="mt-1 whitespace-pre-wrap break-words text-sm leading-6 text-slate-600">
                    {group.requirements || 'Chưa có nội dung.'}
                  </p>
                </details>
                <ProgressSection title="Đề cương" query={syllabus}>
                  <p className="text-sm">
                    {syllabus.data
                      ? (SYLLABUS_STATUS_LABELS[syllabus.data.currentStatus] ??
                        syllabus.data.currentStatus)
                      : 'Chưa nộp đề cương'}
                  </p>
                </ProgressSection>
                <ProgressSection title="Báo cáo giữa kỳ" query={midterm}>
                  <p className="text-sm">
                    {midterm.data
                      ? (MIDTERM_STATUS_LABELS[midterm.data.currentStatus] ??
                        midterm.data.currentStatus)
                      : 'Chưa nộp báo cáo giữa kỳ'}
                  </p>
                </ProgressSection>
                <ProgressSection title="Báo cáo cuối kỳ" query={finalReport}>
                  <p className="text-sm">
                    {finalReport.data
                      ? (FINAL_REPORT_STATUS_LABELS[
                          finalReport.data.currentStatus as keyof typeof FINAL_REPORT_STATUS_LABELS
                        ] ?? finalReport.data.currentStatus)
                      : 'Chưa nộp báo cáo cuối kỳ'}
                  </p>
                  {finalReport.data && (
                    <SimilarityReportPanel
                      reportId={finalReport.data.finalReportId}
                      versionNo={finalReport.data.currentVersionNo}
                    />
                  )}
                </ProgressSection>
                <ProgressSection title="Mốc tiến độ" query={milestones}>
                  {!milestones.data?.length && (
                    <p className="text-sm text-slate-500">Chưa thiết lập mốc tiến độ.</p>
                  )}
                  {milestoneRows.map((milestone, index) => (
                    <div key={milestone.id} className="space-y-1 py-2">
                      <p className="text-sm font-medium">{milestone.title}</p>
                      <ProgressSection title="Báo cáo tiến độ" query={reports[index]}>
                        <p className="text-sm text-slate-600">
                          {reports[index].data
                            ? (PROGRESS_REPORT_STATUS_LABELS[
                                reports[index].data
                                  .currentStatus as keyof typeof PROGRESS_REPORT_STATUS_LABELS
                              ] ?? reports[index].data.currentStatus)
                            : 'Chưa nộp'}
                        </p>
                      </ProgressSection>
                    </div>
                  ))}
                </ProgressSection>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setFacultyOpen(true);
                    setCopyNotice('');
                  }}
                >
                  Soạn nội dung liên hệ Khoa
                </Button>
              </>
            )
          )}
          <div className="flex justify-end border-t border-slate-200 pt-4">
            <Button variant="outline" onClick={onClose}>
              Đóng
            </Button>
          </div>
        </div>
      </Dialog>
      {facultyOpen && (
        <Dialog title="Soạn nội dung liên hệ Khoa" onClose={() => setFacultyOpen(false)}>
          <div className="space-y-4 p-5">
            <p className="text-sm text-slate-600">
              Sao chép nội dung để gửi qua kênh liên hệ của Khoa. Thao tác này chưa gửi yêu cầu.
            </p>
            <label className="block space-y-2 text-sm">
              <span>Nội dung tham khảo</span>
              <textarea
                readOnly
                value={facultyText}
                rows={8}
                className="w-full rounded-lg border border-slate-200 p-3"
              />
            </label>
            <p role="status" className="text-sm">
              {copyNotice}
            </p>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setFacultyOpen(false)}>
                Đóng
              </Button>
              <Button onClick={() => void copy()}>Sao chép nội dung</Button>
            </div>
          </div>
        </Dialog>
      )}
    </>
  );
}
