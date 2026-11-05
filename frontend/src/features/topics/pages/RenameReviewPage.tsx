import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardCheck } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { useAuth } from '@/shared/auth';
import { studentTopicApi } from '../services/studentTopicApi';
import { renameRequestApi } from '../services/renameRequestApi';
import type { RenameRequest, RenameRequestStatus } from '../types/renameRequest.types';
import { getTopicErrorMessage } from '../services/topicApi';

type Level = 'advisor' | 'faculty' | 'academic-affairs';
type ReviewDecision = {
  request: RenameRequest;
  value: 'Approve' | 'Reject';
  reason: string | null;
};
const levels: Record<
  Level,
  { number: number; label: string; status: RenameRequestStatus; prior: string }
> = {
  advisor: {
    number: 1,
    label: 'Giảng viên hướng dẫn',
    status: 'Created',
    prior: 'Sinh viên đã gửi',
  },
  faculty: { number: 2, label: 'Khoa', status: 'AdvisorConfirmed', prior: 'Giảng viên đã đồng ý' },
  'academic-affairs': {
    number: 3,
    label: 'Phòng đào tạo',
    status: 'FacultyApproved',
    prior: 'Giảng viên và Khoa đã đồng ý',
  },
};
const date = (value: string) =>
  new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(new Date(value));

export function RenameReviewPage({ level }: { level: Level }) {
  const { user } = useAuth();
  return <RenameReviewContent key={`${user?.id ?? 'anonymous'}-${level}`} level={level} />;
}

export function RenameReviewEntryPage() {
  const { hasRole } = useAuth();
  const [adminLevel, setAdminLevel] = useState<Level>('advisor');
  const isAdmin = hasRole('Admin');
  const level: Level = isAdmin
    ? adminLevel
    : hasRole('Lecturer')
      ? 'advisor'
      : hasRole('Faculty')
        ? 'faculty'
        : 'academic-affairs';
  return (
    <div className="space-y-5">
      {isAdmin && (
        <nav aria-label="Chọn cấp duyệt" className="flex flex-wrap gap-2">
          {(['advisor', 'faculty', 'academic-affairs'] as const).map((item) => (
            <Button
              key={item}
              type="button"
              variant={level === item ? 'default' : 'outline'}
              aria-current={level === item ? 'page' : undefined}
              onClick={() => setAdminLevel(item)}
            >
              Cấp {levels[item].number} · {levels[item].label}
            </Button>
          ))}
        </nav>
      )}
      <RenameReviewPage level={level} />
    </div>
  );
}

function RenameReviewContent({ level }: { level: Level }) {
  const { user } = useAuth();
  const config = levels[level];
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [rejectionReason, setRejectionReason] = useState('');
  const [confirmation, setConfirmation] = useState<ReviewDecision | null>(null);
  const reasonRef = useRef<HTMLTextAreaElement>(null);
  const [notice, setNotice] = useState('');
  const requests = useQuery({
    queryKey: ['rename-requests-review', user?.id, config.status],
    queryFn: ({ signal }) => renameRequestApi.list(config.status, signal),
    refetchInterval: 30_000,
  });
  const queue = (requests.data ?? []).filter((item) => item.status === config.status);
  const selected: RenameRequest | undefined =
    queue.find((item) => item.id === selectedId) ?? queue[0];
  const group = useQuery({
    queryKey: ['rename-review-group', user?.id, selected?.groupId],
    queryFn: ({ signal }) => studentTopicApi.getGroupDetail(selected!.groupId, signal),
    enabled: Boolean(selected?.groupId),
  });
  const review = useMutation({
    mutationFn: (choice: ReviewDecision) =>
      renameRequestApi.review(choice.request.id, level, {
        decision: choice.value,
        rejectionReason: choice.reason,
      }),
    onSuccess: (_result, choice) => {
      setNotice(
        choice.value === 'Approve'
          ? level === 'academic-affairs'
            ? 'Đã phê duyệt. Tên đề tài đã được cập nhật chính thức.'
            : 'Đã phê duyệt đơn và chuyển sang cấp tiếp theo.'
          : 'Đã từ chối đơn.',
      );
      setConfirmation(null);
      setRejectionReason('');
      setSelectedId(null);
      void queryClient.invalidateQueries({ queryKey: ['rename-requests-review'] });
      void queryClient.invalidateQueries({ queryKey: ['rename-requests-group'] });
      void queryClient.invalidateQueries({ queryKey: ['student-topic-group'] });
    },
  });
  function openDecision(value: 'Approve' | 'Reject') {
    if (!selected || review.isPending) return;
    if (value === 'Reject' && !rejectionReason.trim()) {
      reasonRef.current?.focus();
      return;
    }
    review.reset();
    setConfirmation({
      request: selected,
      value,
      reason: value === 'Reject' ? rejectionReason.trim() : null,
    });
  }
  useEffect(() => {
    function shortcut(event: KeyboardEvent) {
      if (
        !selected ||
        confirmation ||
        !event.altKey ||
        event.ctrlKey ||
        event.metaKey ||
        event.shiftKey
      )
        return;
      if (event.key !== '1' && event.key !== '2') return;
      event.preventDefault();
      openDecision(event.key === '1' ? 'Approve' : 'Reject');
    }
    window.addEventListener('keydown', shortcut);
    return () => window.removeEventListener('keydown', shortcut);
  }, [selected, confirmation, rejectionReason, review.isPending]);

  const loading = requests.isPending;
  const error = requests.error;
  return (
    <div className="space-y-5">
      <PageHeader title={`Đổi tên đề tài · Phê duyệt cấp ${config.number}`} icon={<ClipboardCheck />} />
      <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-slate-500">
        <p>Chỉ hiển thị đơn đã hoàn thành cấp trước và đang chờ {config.label} xử lý.</p>
        <StatusBadge tone="info">
          Chờ phê duyệt cấp {config.number}: {queue.length}
        </StatusBadge>
      </div>
      {notice && (
        <p role="status" className="rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">
          {notice}
        </p>
      )}
      {error && (
        <div role="alert" className="rounded-xl bg-rose-50 p-3 text-sm text-rose-700">
          {getTopicErrorMessage(error)}{' '}
          <Button type="button" variant="link" onClick={() => void requests.refetch()}>
            Thử lại
          </Button>
        </div>
      )}
      <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(18rem,22rem)]">
        <div className="space-y-5">
          <Card className="space-y-4 border-slate-200 p-5 shadow-sm">
            <h2 className="font-semibold text-slate-900">Danh sách đơn đang chờ duyệt</h2>
            {loading ? (
              <p role="status" className="text-sm text-slate-500">
                Đang tải danh sách...
              </p>
            ) : !queue.length ? (
              <p className="text-sm text-slate-500">Chưa có đơn đủ điều kiện duyệt ở cấp này.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[520px] text-left text-sm">
                  <thead className="border-b bg-slate-50 text-xs text-slate-600">
                    <tr>
                      <th className="p-3">Đề tài đề xuất</th>
                      <th className="p-3">Nhóm</th>
                      <th className="p-3">Ngày gửi</th>
                      <th className="p-3">Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {queue.map((item) => (
                      <tr
                        key={item.id}
                        className={`cursor-pointer border-b hover:bg-slate-50 ${selected?.id === item.id ? 'bg-blue-50/60' : ''}`}
                        onClick={() => {
                          setSelectedId(item.id);
                          setRejectionReason('');
                        }}
                      >
                        <td className="p-3">
                          <button
                            type="button"
                            className="font-medium text-primary underline-offset-2 hover:underline"
                            onClick={() => setSelectedId(item.id)}
                          >
                            {item.newTitleVi}
                          </button>
                        </td>
                        <td className="p-3 text-slate-600">{item.groupId.slice(0, 8)}</td>
                        <td className="p-3 text-slate-600">{date(item.createdAt)}</td>
                        <td className="p-3">
                          <StatusBadge tone="warning">Chờ cấp {config.number}</StatusBadge>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
          {selected && (
            <>
              <Card className="space-y-4 border-slate-200 p-5 shadow-sm">
                <h2 className="font-semibold text-slate-900">Lịch sử & nhận xét</h2>
                <div className="grid gap-3 rounded-lg bg-slate-50 p-3 text-sm sm:grid-cols-3">
                  <p>
                    <span className="text-slate-500">Cấp trước</span>
                    <br />
                    {config.prior}
                  </p>
                  <p>
                    <span className="text-slate-500">Hồ sơ</span>
                    <br />
                    Đủ điều kiện xử lý
                  </p>
                  <p>
                    <span className="text-slate-500">Trạng thái</span>
                    <br />
                    Đúng thứ tự
                  </p>
                </div>
                <p className="text-xs text-slate-500">
                  Đơn gửi: {date(selected.createdAt)}
                  {selected.advisorConfirmedAt
                    ? ` · GVHD duyệt: ${date(selected.advisorConfirmedAt)}`
                    : ''}
                  {selected.facultyApprovedAt
                    ? ` · Khoa duyệt: ${date(selected.facultyApprovedAt)}`
                    : ''}
                </p>
                <label className="block space-y-1 text-sm font-medium">
                  Lý do từ chối{' '}
                  <span className="font-normal text-slate-500">(chỉ cần nhập khi từ chối)</span>
                  <textarea
                    ref={reasonRef}
                    value={rejectionReason}
                    onChange={(event) => setRejectionReason(event.target.value)}
                    rows={3}
                    className="w-full rounded-md border border-input bg-white px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                    placeholder="Nêu rõ lý do để sinh viên biết cần sửa gì"
                  />
                </label>
              </Card>
              <Card className="flex flex-wrap items-center gap-2 border-slate-200 p-4 shadow-sm">
                <Button
                  type="button"
                  disabled={review.isPending}
                  onClick={() => openDecision('Approve')}
                >
                  Phê duyệt cấp {config.number}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  disabled={review.isPending || !rejectionReason.trim()}
                  onClick={() => openDecision('Reject')}
                >
                  Từ chối có lý do
                </Button>
                <span className="text-xs text-slate-500">Alt+1 duyệt · Alt+2 từ chối</span>
              </Card>
            </>
          )}
        </div>
        <Card className="space-y-4 border-slate-200 p-5 shadow-sm">
          <h2 className="font-semibold text-slate-900">Đơn đổi tên đang chọn</h2>
          {selected ? (
            <>
              <p className="text-sm">
                <span className="text-slate-500">Nhóm</span>
                <br />
                {group.data?.members
                  .map((member) => member.fullName || member.userCode)
                  .join(', ') || selected.groupId}
              </p>
              <p className="text-sm">
                <span className="text-slate-500">Cấp xử lý</span>
                <br />
                {config.label}
              </p>
              <p className="text-sm">
                <span className="text-slate-500">Cấp trước</span>
                <br />
                {config.prior}
              </p>
              <p className="text-sm">
                <span className="text-slate-500">Tên hiện tại</span>
                <br />
                {selected.oldTitleVi}
              </p>
              <p className="text-sm">
                <span className="text-slate-500">Tên đề xuất</span>
                <br />
                {selected.newTitleVi}
              </p>
              <p className="text-sm">
                <span className="text-slate-500">Tên tiếng Anh</span>
                <br />
                {selected.newTitleEn || 'Không đổi'}
              </p>
              <p className="text-sm">
                <span className="text-slate-500">Lý do</span>
                <br />
                {selected.reason}
              </p>
              {group.isError && (
                <p role="alert" className="text-sm text-rose-700">
                  {getTopicErrorMessage(group.error)}
                </p>
              )}
              {level === 'academic-affairs' && (
                <p className="text-xs text-slate-500">
                  Sau khi duyệt, tên đề tài sẽ được cập nhật chính thức.
                </p>
              )}
            </>
          ) : (
            <p className="text-sm text-slate-500">Chọn một đơn trong danh sách để xem chi tiết.</p>
          )}
        </Card>
      </div>
      <ConfirmDialog
        open={Boolean(confirmation)}
        onClose={() => setConfirmation(null)}
        onConfirm={() => {
          if (confirmation) review.mutate(confirmation);
        }}
        isConfirming={review.isPending}
        type={confirmation?.value === 'Reject' ? 'warning' : 'info'}
        title={
          confirmation?.value === 'Approve' ? `Phê duyệt cấp ${config.number}?` : 'Từ chối đơn?'
        }
        description={
          <>
            {confirmation?.request.newTitleVi}
            {confirmation?.value === 'Reject' && (
              <span className="block">Lý do: {confirmation.reason}</span>
            )}
            {review.isError && (
              <span className="block text-rose-600">{getTopicErrorMessage(review.error)}</span>
            )}
          </>
        }
        confirmText={confirmation?.value === 'Approve' ? 'Phê duyệt' : 'Từ chối'}
      />
    </div>
  );
}
