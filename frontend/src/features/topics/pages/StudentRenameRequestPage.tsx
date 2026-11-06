import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FilePenLine } from 'lucide-react';
import { Link } from 'react-router-dom';
import { PageHeader } from '@/components/ui/page-header';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { StatusBadge } from '@/components/ui/status-badge';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { useAuth } from '@/shared/auth';
import { studentTopicApi } from '../services/studentTopicApi';
import { renameRequestApi } from '../services/renameRequestApi';
import { renameStatusLabels } from '../types/renameRequest.types';
import { getTopicErrorMessage } from '../services/topicApi';

const activeStatuses = ['Created', 'AdvisorConfirmed', 'FacultyApproved'];
const date = (value: string) =>
  new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(new Date(value));

export function StudentRenameRequestPage() {
  const { user } = useAuth();
  return <StudentRenameRequestContent key={user?.id ?? 'anonymous'} />;
}

function StudentRenameRequestContent() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [newTitleVi, setNewTitleVi] = useState('');
  const [newTitleEn, setNewTitleEn] = useState('');
  const [reason, setReason] = useState('');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [notice, setNotice] = useState('');
  const initializedGroup = useRef<string | null>(null);
  const status = useQuery({
    queryKey: ['student-registration', user?.id],
    queryFn: ({ signal }) => studentTopicApi.getMyStatus(signal),
  });
  const groupId = status.data?.confirmationStatus === 'Confirmed' ? status.data.groupId : null;
  const group = useQuery({
    queryKey: ['student-topic-group', user?.id, groupId],
    queryFn: ({ signal }) => studentTopicApi.getGroupDetail(groupId!, signal),
    enabled: Boolean(groupId),
    refetchInterval: 30_000,
  });
  const history = useQuery({
    queryKey: ['rename-requests-group', user?.id, groupId],
    queryFn: ({ signal }) => renameRequestApi.listByGroup(groupId!, signal),
    enabled: Boolean(groupId),
    refetchInterval: 30_000,
  });
  const timeline = useQuery({
    queryKey: ['rename-request-timeline', group.data?.batchId],
    queryFn: ({ signal }) => studentTopicApi.getTimeline(group.data!.batchId, signal),
    enabled: Boolean(group.data?.batchId),
    refetchInterval: 60_000,
  });
  useEffect(() => {
    if (group.data && initializedGroup.current !== group.data.groupId) {
      initializedGroup.current = group.data.groupId;
      setNewTitleEn(group.data.topicTitleEn || '');
    }
  }, [group.data]);
  const create = useMutation({
    mutationFn: () =>
      renameRequestApi.create(groupId!, {
        newTitleVi: newTitleVi.trim(),
        newTitleEn: newTitleEn.trim() || null,
        reason: reason.trim(),
      }),
    onSuccess: () => {
      setConfirmOpen(false);
      setNewTitleVi('');
      setNewTitleEn(group.data?.topicTitleEn || '');
      setReason('');
      setNotice('Đã gửi đơn. Giảng viên hướng dẫn sẽ xem xét ở cấp 1.');
      void queryClient.invalidateQueries({
        queryKey: ['rename-requests-group', user?.id, groupId],
      });
    },
  });
  const activeRequest = history.data?.some((item) => activeStatuses.includes(item.status));
  const stage3 = timeline.data?.stages.find(
    (stage) => stage.stageCode === 'PROGRESS_EXECUTION' || stage.orderIndex === 3,
  );
  const stage4 = timeline.data?.stages.find(
    (stage) => stage.stageCode === 'MIDTERM_GATE' || stage.orderIndex === 4,
  );
  const windowClosed = Boolean(
    timeline.data &&
    (timeline.data.status !== 'Open' ||
      !stage3 ||
      !stage4 ||
      timeline.data.currentLocalDate < stage3.startDate ||
      Date.now() > Date.parse(stage4.deadlineAtUtc)),
  );
  const validGroupStatus =
    group.data?.groupStatus === 'InProgress' || group.data?.groupStatus === 'MidtermSubmitted';
  const canSubmit = Boolean(
    group.data &&
    validGroupStatus &&
    !windowClosed &&
    history.isSuccess &&
    !activeRequest &&
    newTitleVi.trim() &&
    reason.trim() &&
    (newTitleVi.trim().toLocaleLowerCase() !== group.data.topicTitleVi.trim().toLocaleLowerCase() ||
      newTitleEn.trim().toLocaleLowerCase() !==
        (group.data.topicTitleEn || '').trim().toLocaleLowerCase()),
  );

  function submit(event: FormEvent) {
    event.preventDefault();
    if (canSubmit) setConfirmOpen(true);
  }

  return (
    <div className="space-y-5">
      <PageHeader title="Đơn xin đổi tên đề tài" icon={<FilePenLine />} />
      <p className="text-sm text-slate-500">
        Sinh viên đã xác nhận trong nhóm có thể gửi đơn trong thời hạn quy định của đợt.
      </p>
      {stage4 && (
        <p className="text-sm text-slate-600">
          Hạn gửi đơn: {date(stage4.deadlineAtUtc)}.{' '}
          {windowClosed
            ? 'Hiện ngoài thời hạn gửi đơn.'
            : 'Đơn sẽ lần lượt qua giảng viên, Khoa và Phòng đào tạo.'}
        </p>
      )}
      {notice && (
        <p role="status" className="rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">
          {notice}
        </p>
      )}
      <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(18rem,22rem)]">
        <div className="space-y-5">
          <Card className="space-y-5 border-slate-200 p-5 shadow-sm">
            <h2 className="font-semibold text-slate-900">Thông tin đơn</h2>
            {status.isPending || (group.isPending && groupId) || (history.isPending && groupId) ? (
              <p role="status" className="text-sm text-slate-500">
                Đang tải thông tin đề tài...
              </p>
            ) : status.isError || group.isError || history.isError ? (
              <p role="alert" className="text-sm text-rose-700">
                {getTopicErrorMessage(status.error || group.error || history.error)}
              </p>
            ) : !groupId ? (
              <p className="text-sm text-slate-600">
                Bạn cần có nhóm đề tài đã xác nhận trước khi gửi đơn.{' '}
                <Link className="text-primary underline" to="/topics/catalog">
                  Xem đăng ký đề tài
                </Link>
              </p>
            ) : group.data ? (
              <form id="rename-request-form" onSubmit={submit} className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <label className="space-y-1 text-sm font-medium">
                    Tên hiện tại
                    <Input value={group.data.topicTitleVi} readOnly className="bg-slate-50" />
                  </label>
                  <label className="space-y-1 text-sm font-medium">
                    Tên mới tiếng Việt <span className="text-rose-600">*</span>
                    <Input
                      value={newTitleVi}
                      onChange={(event) => setNewTitleVi(event.target.value)}
                      maxLength={300}
                      required
                      disabled={Boolean(activeRequest)}
                      placeholder="Nhập tên đề tài mới"
                    />
                  </label>
                  <label className="space-y-1 text-sm font-medium">
                    Tên mới tiếng Anh
                    <Input
                      value={newTitleEn}
                      onChange={(event) => setNewTitleEn(event.target.value)}
                      maxLength={350}
                      disabled={Boolean(activeRequest)}
                      placeholder="Không bắt buộc"
                    />
                  </label>
                  <label className="space-y-1 text-sm font-medium">
                    Lý do <span className="text-rose-600">*</span>
                    <textarea
                      value={reason}
                      onChange={(event) => setReason(event.target.value)}
                      required
                      disabled={Boolean(activeRequest)}
                      rows={4}
                      className="w-full rounded-md border border-input bg-white px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                      placeholder="Nêu lý do điều chỉnh tên đề tài"
                    />
                  </label>
                </div>
                {activeRequest && (
                  <p className="text-sm text-amber-700">
                    Nhóm đã có đơn đang chờ duyệt. Bạn có thể gửi đơn mới sau khi đơn này kết thúc.
                  </p>
                )}
                {!validGroupStatus && (
                  <p className="text-sm text-amber-700">
                    Nhóm cần ở giai đoạn thực hiện hoặc đã nộp báo cáo giữa kỳ để gửi đơn.
                  </p>
                )}
                {windowClosed && (
                  <p className="text-sm text-amber-700">
                    Hiện ngoài thời hạn gửi đơn đổi tên của đợt này.
                  </p>
                )}
                {newTitleVi.trim().toLocaleLowerCase() ===
                  group.data.topicTitleVi.trim().toLocaleLowerCase() &&
                  newTitleEn.trim().toLocaleLowerCase() ===
                    (group.data.topicTitleEn || '').trim().toLocaleLowerCase() && (
                    <p className="text-sm text-rose-700">Tên mới cần khác tên hiện tại.</p>
                  )}
              </form>
            ) : null}
          </Card>
          {group.data && (
            <Card className="border-slate-200 p-4 shadow-sm">
              <Button
                type="submit"
                form="rename-request-form"
                disabled={!canSubmit || create.isPending}
              >
                Gửi đơn
              </Button>
            </Card>
          )}
          <Card className="space-y-3 border-slate-200 p-5 shadow-sm">
            <h2 className="font-semibold text-slate-900">Lịch sử đơn của nhóm</h2>
            {history.isPending && groupId ? (
              <p role="status" className="text-sm text-slate-500">
                Đang tải lịch sử đơn...
              </p>
            ) : history.isError ? (
              <p role="alert" className="text-sm text-rose-700">
                {getTopicErrorMessage(history.error)}
              </p>
            ) : history.data?.length ? (
              history.data.map((item) => (
                <div key={item.id} className="rounded-lg border border-slate-200 p-3 text-sm">
                  <div className="flex flex-wrap justify-between gap-2">
                    <strong>{item.newTitleVi}</strong>
                    <StatusBadge
                      tone={
                        item.status.startsWith('Rejected')
                          ? 'error'
                          : item.status === 'Completed'
                            ? 'success'
                            : 'warning'
                      }
                    >
                      {renameStatusLabels[item.status] ?? item.status}
                    </StatusBadge>
                  </div>
                  <p className="mt-1 text-slate-500">Gửi lúc {date(item.createdAt)}</p>
                  <p className="mt-1 text-slate-500">
                    GVHD: {item.advisorConfirmedAt ? date(item.advisorConfirmedAt) : 'chưa duyệt'} ·
                    Khoa: {item.facultyApprovedAt ? date(item.facultyApprovedAt) : 'chưa duyệt'} ·
                    PĐT: {item.aaApprovedAt ? date(item.aaApprovedAt) : 'chưa duyệt'}
                  </p>
                  {item.rejectionReason && (
                    <p className="mt-2 text-rose-700">Lý do từ chối: {item.rejectionReason}</p>
                  )}
                </div>
              ))
            ) : (
              <p className="text-sm text-slate-500">Chưa có đơn nào.</p>
            )}
          </Card>
        </div>
        <Card className="space-y-4 border-slate-200 p-5 shadow-sm">
          <h2 className="font-semibold text-slate-900">Theo dõi phê duyệt</h2>
          <p className="text-sm">
            <span className="text-slate-500">Đề tài</span>
            <br />
            {group.data?.topicTitleVi ?? 'Chưa có đề tài'}
          </p>
          <p className="text-sm">
            <span className="text-slate-500">Nhóm</span>
            <br />
            {group.data?.members.map((member) => member.fullName || member.userCode).join(', ') ||
              '—'}
          </p>
          <p className="text-sm">
            <span className="text-slate-500">Giảng viên hướng dẫn</span>
            <br />
            {group.data?.advisor.fullName || '—'}
          </p>
          <ol className="list-inside list-decimal space-y-1 text-sm text-slate-600">
            <li>Giảng viên hướng dẫn xem xét</li>
            <li>Khoa phê duyệt</li>
            <li>Phòng đào tạo phê duyệt và cập nhật tên</li>
          </ol>
        </Card>
      </div>
      <ConfirmDialog
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={() => create.mutate()}
        isConfirming={create.isPending}
        title="Gửi đơn đổi tên?"
        description={
          <>
            Đơn sẽ được chuyển đến giảng viên hướng dẫn.{' '}
            {create.isError && (
              <span className="block text-rose-600">{getTopicErrorMessage(create.error)}</span>
            )}
          </>
        }
        confirmText="Gửi đơn"
      />
    </div>
  );
}
