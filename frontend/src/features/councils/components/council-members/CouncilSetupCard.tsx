import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowDown, ArrowUp, Trash2 } from 'lucide-react';
import { DatePicker } from '@/components/ui/calendar';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PendingState } from '@/components/ui/pending-state';
import { Select } from '@/components/ui/select';
import type { AcademicBatch } from '@/shared/academic-periods/types';
import type { CouncilDraftItemDto, CouncilScheduleGroupDto } from '../../types/councilApproval.types';
import { formatViDate } from '../../utils/councilApprovalReadiness';
import { councilPlanningApi, councilScheduleKey, eligibilityMessage } from '../../services/councilPlanningApi';
import { memberError } from '../../services/councilMembersApi';
import { useCouncilDefenseStage } from '../../hooks/useCouncilDefenseStage';
import { CouncilDefenseStageNotice } from './CouncilDefenseStageNotice';
import { councilDateWindow } from '../../utils/councilDefenseWindow';

export function CouncilSetupCard({ council, batch, onUpdated }: {
  council: CouncilDraftItemDto;
  batch?: AcademicBatch;
  onUpdated: () => Promise<void>;
}) {
  const queryClient = useQueryClient();
  const editable = council.status === 'Draft';
  const [editing, setEditing] = useState(false);
  const [defenseDate, setDefenseDate] = useState(council.defenseDate);
  const [timeSlot, setTimeSlot] = useState(council.timeSlot);
  const [room, setRoom] = useState(council.room);
  const [groupId, setGroupId] = useState('');
  const [orderText, setOrderText] = useState('');
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState('');
  const [success, setSuccess] = useState('');
  const [orderedIds, setOrderedIds] = useState<string[] | null>(null);
  const [removeTarget, setRemoveTarget] = useState<CouncilScheduleGroupDto | null>(null);
  const [showGroupPicker, setShowGroupPicker] = useState(false);
  const [confirmCancelSchedule, setConfirmCancelSchedule] = useState(false);
  const { query: timelineQuery, canRead } = useCouncilDefenseStage(council.batchId);
  const window = councilDateWindow(timelineQuery.data, batch);

  const schedule = useQuery({
    queryKey: councilScheduleKey(council.id),
    queryFn: ({ signal }) => councilPlanningApi.schedule(council.id, signal),
    staleTime: 30_000,
    retry: false,
  });
  const candidates = useQuery({
    queryKey: ['council-group-candidates', council.batchId],
    queryFn: ({ signal }) => councilPlanningApi.groupCandidates(council.batchId, signal),
    enabled: editable && showGroupPicker,
    staleTime: 30_000,
    retry: false,
  });
  const eligibility = useQuery({
    queryKey: ['council-group-eligibility', groupId],
    queryFn: () => councilPlanningApi.defenseEligibility(groupId),
    enabled: Boolean(groupId) && editable && showGroupPicker,
    staleTime: 15_000,
    retry: false,
  });

  const groups = schedule.data?.groups ?? [];
  const currentIds = groups.map((group) => group.groupId);
  const displayIds = orderedIds && orderedIds.length === currentIds.length && orderedIds.every((id) => currentIds.includes(id))
    ? orderedIds : currentIds;
  const orderedGroups = displayIds.map((id) => groups.find((group) => group.groupId === id)).filter((group): group is CouncilScheduleGroupDto => Boolean(group));
  const orderDirty = displayIds.some((id, index) => id !== currentIds[index]);
  const assignedIds = new Set(groups.map((group) => group.groupId));
  const available = (candidates.data ?? []).filter((group) => !assignedIds.has(group.groupId));
  const assignable = available.filter((group) => !group.assignedCouncilId);
  const nextOrder = Math.max(0, ...groups.map((group) => group.orderNumber)) + 1;
  const chosen = available.find((group) => group.groupId === groupId);
  const order = Number(orderText || nextOrder);
  const orderTaken = groups.some((group) => group.orderNumber === order);
  const scheduleDirty = defenseDate !== council.defenseDate || timeSlot !== council.timeSlot || room !== council.room;

  function cancelScheduleEdit() {
    setEditing(false);
    setDefenseDate(council.defenseDate);
    setTimeSlot(council.timeSlot);
    setRoom(council.room);
    setConfirmCancelSchedule(false);
  }

  async function saveSchedule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    if (!defenseDate || !timeSlot.trim() || !room.trim()) {
      setActionError('Nhập ngày, ca và phòng bảo vệ trước khi lưu.');
      return;
    }
    setSaving(true);
    setActionError('');
    setSuccess('');
    try {
      await councilPlanningApi.updateSchedule(council.id, { defenseDate, timeSlot: timeSlot.trim(), room: room.trim() });
      await onUpdated();
      setEditing(false);
      setSuccess('Đã cập nhật ngày, ca và phòng bảo vệ.');
    } catch (cause) {
      setActionError(memberError(cause));
    } finally {
      setSaving(false);
    }
  }

  async function addGroup(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || !groupId) return;
    if (!Number.isInteger(order) || order < 1 || orderTaken) {
      setActionError('Thứ tự bảo vệ phải là số nguyên dương và chưa được dùng trong hội đồng này.');
      return;
    }
    setSaving(true);
    setActionError('');
    setSuccess('');
    try {
      const latest = await councilPlanningApi.defenseEligibility(groupId);
      if (!latest.canProceed) {
        setActionError(eligibilityMessage(latest.reasonCode));
        return;
      }
      await councilPlanningApi.addGroup(council.id, groupId, order);
      await queryClient.invalidateQueries({ queryKey: councilScheduleKey(council.id) });
      await onUpdated();
      setGroupId('');
      setOrderText('');
      setOrderedIds(null);
      setShowGroupPicker(false);
      setSuccess('Đã gán nhóm vào lịch bảo vệ.');
    } catch (cause) {
      setActionError(memberError(cause));
    } finally {
      setSaving(false);
    }
  }

  async function saveOrder() {
    if (saving || !orderDirty) return;
    setSaving(true);
    setActionError('');
    setSuccess('');
    try {
      await councilPlanningApi.reorderGroups(council.id, orderedGroups.map((group, index) => ({ groupId: group.groupId, orderNumber: index + 1 })));
      await queryClient.invalidateQueries({ queryKey: councilScheduleKey(council.id) });
      await onUpdated();
      setOrderedIds(null);
      setSuccess('Đã lưu thứ tự bảo vệ mới.');
    } catch (cause) {
      setActionError(memberError(cause));
    } finally {
      setSaving(false);
    }
  }

  async function removeGroup() {
    if (!removeTarget?.assignmentId || saving) return;
    setSaving(true);
    setActionError('');
    setSuccess('');
    try {
      await councilPlanningApi.removeGroup(council.id, removeTarget.assignmentId);
      await queryClient.invalidateQueries({ queryKey: councilScheduleKey(council.id) });
      await onUpdated();
      setOrderedIds(null);
      setRemoveTarget(null);
      setSuccess('Đã gỡ nhóm khỏi hội đồng.');
    } catch (cause) {
      setActionError(memberError(cause));
      setRemoveTarget(null);
    } finally {
      setSaving(false);
    }
  }

  function moveGroup(index: number, offset: number) {
    const next = [...displayIds];
    [next[index], next[index + offset]] = [next[index + offset], next[index]];
    setOrderedIds(next);
    setSuccess('');
  }

  return (
    <Card className="min-w-0 p-5 sm:p-6" aria-busy={saving}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-ink-heading">Kế hoạch bảo vệ</h2>
          <p className="mt-1 text-sm text-ink-muted">Nhóm nào bảo vệ, theo thứ tự nào, ở đâu và khi nào.</p>
        </div>
        {editable && !editing && (
          <Button type="button" variant="outline" size="sm" disabled={saving} onClick={() => { setEditing(true); setActionError(''); }}>
            Sửa ngày, ca và phòng
          </Button>
        )}
      </div>

      {editing ? (
        <form onSubmit={(event) => void saveSchedule(event)} className="mt-4 space-y-3">
          <CouncilDefenseStageNotice
            window={window}
            date={defenseDate}
            loading={timelineQuery.isPending && timelineQuery.fetchStatus === 'fetching'}
            error={timelineQuery.isError}
            canRead={canRead}
            onRetry={() => void timelineQuery.refetch()}
          />
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1.5">
              <label htmlFor="edit-council-date" className="text-sm font-medium text-ink-secondary">Ngày bảo vệ *</label>
              <DatePicker id="edit-council-date" value={defenseDate} onValueChange={setDefenseDate} required disabled={saving} />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="edit-council-time" className="text-sm font-medium text-ink-secondary">Ca bảo vệ *</label>
              <Input id="edit-council-time" value={timeSlot} onChange={(event) => setTimeSlot(event.target.value)} required maxLength={50} disabled={saving} />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="edit-council-room" className="text-sm font-medium text-ink-secondary">Phòng bảo vệ *</label>
              <Input id="edit-council-room" value={room} onChange={(event) => setRoom(event.target.value)} required maxLength={50} disabled={saving} />
            </div>
          </div>
          {council.isSubmittedToAcademicAffairs && <p className="text-sm text-tone-warning-text">Đổi lịch sẽ đưa hồ sơ về bước chuẩn bị. Khoa cần gửi lại cho PĐT.</p>}
          <div className="flex flex-wrap gap-2">
            <Button type="submit" isLoading={saving} loadingText="Đang lưu…">Lưu lịch bảo vệ</Button>
            <Button type="button" variant="outline" disabled={saving} onClick={() => scheduleDirty ? setConfirmCancelSchedule(true) : cancelScheduleEdit()}>Hủy</Button>
          </div>
        </form>
      ) : (
        <p className="mt-4 rounded-xl bg-surface-subtle px-3 py-2 text-sm text-ink-secondary">
          <strong className="text-ink-heading">{formatViDate(council.defenseDate)}</strong> · {council.timeSlot} · Phòng {council.room}
        </p>
      )}

      <div className="mt-5 border-t border-line-subtle pt-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="text-sm font-semibold text-ink-heading">Nhóm bảo vệ ({groups.length})</h3>
          {editable && schedule.isSuccess && (
            <Button type="button" size="sm" variant="outline" aria-expanded={showGroupPicker} onClick={() => setShowGroupPicker((value) => !value)} disabled={saving || orderDirty}>
              {showGroupPicker ? 'Đóng chọn nhóm' : 'Gán thêm nhóm'}
            </Button>
          )}
        </div>
        {schedule.isLoading && <PendingState className="mt-3" label="Đang tải nhóm bảo vệ…" />}
        {schedule.isError && <InlineAlert title="Không tải được nhóm bảo vệ" action={<button type="button" className="font-medium underline" onClick={() => void schedule.refetch()}>Thử lại</button>}>{memberError(schedule.error)}</InlineAlert>}
        {schedule.isSuccess && groups.length === 0 && <p className="mt-2 text-sm text-ink-muted">Chưa gán nhóm nào. Bấm “Gán thêm nhóm” để chọn nhóm bảo vệ.</p>}
        {groups.length > 0 && (
          <ol className="mt-3 space-y-2" aria-label="Thứ tự bảo vệ của các nhóm">
            {orderedGroups.map((group, index) => (
              <li key={group.groupId} className="flex flex-wrap items-center gap-3 rounded-xl border border-line/80 px-3 py-2.5 text-sm">
                <span className="shrink-0 font-semibold text-primary">{orderDirty ? index + 1 : group.orderNumber}.</span>
                <span className="min-w-0 flex-1">
                  <strong className="block text-ink-heading">{group.topicTitleVi}</strong>
                  <span className="text-ink-muted">GV hướng dẫn: {group.advisorName || 'Chưa rõ'} · GV phản biện: {group.reviewerName || 'Chưa có'}</span>
                </span>
                {editable && (
                  <span className="flex shrink-0 gap-1">
                    <Button type="button" size="sm" variant="outline" aria-label={`Đưa ${group.topicTitleVi} lên trước`} disabled={saving || index === 0} onClick={() => moveGroup(index, -1)}><ArrowUp className="size-4" aria-hidden="true" /></Button>
                    <Button type="button" size="sm" variant="outline" aria-label={`Đưa ${group.topicTitleVi} xuống sau`} disabled={saving || index === orderedGroups.length - 1} onClick={() => moveGroup(index, 1)}><ArrowDown className="size-4" aria-hidden="true" /></Button>
                    {group.assignmentId && <Button type="button" size="sm" variant="outline" aria-label={`Gỡ ${group.topicTitleVi} khỏi hội đồng`} disabled={saving || orderDirty} onClick={() => setRemoveTarget(group)}><Trash2 className="size-4" aria-hidden="true" /></Button>}
                  </span>
                )}
              </li>
            ))}
          </ol>
        )}
        {editable && orderDirty && (
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Button type="button" size="sm" isLoading={saving} loadingText="Đang lưu…" onClick={() => void saveOrder()}>Lưu thứ tự mới</Button>
            <Button type="button" size="sm" variant="outline" disabled={saving} onClick={() => setOrderedIds(null)}>Bỏ thay đổi</Button>
            {council.isSubmittedToAcademicAffairs && <p className="text-sm text-tone-warning-text">Lưu thứ tự mới sẽ rút hồ sơ đã gửi PĐT.</p>}
          </div>
        )}
      </div>

      {editable && schedule.isSuccess && showGroupPicker && (
        <form onSubmit={(event) => void addGroup(event)} className="mt-4 space-y-3 rounded-xl bg-surface-subtle p-3 sm:p-4">
          <h3 className="text-sm font-semibold text-ink-heading">Chọn nhóm và thứ tự bảo vệ</h3>
          <p className="text-sm text-ink-muted">Danh sách gồm các nhóm đã có giảng viên phản biện trong đợt này. Hệ thống kiểm tra kết quả phản biện trước khi gán.</p>
          <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_9rem]">
            <div className="min-w-0 space-y-1.5">
              <p id="council-group-label" className="text-sm font-medium text-ink-secondary">Nhóm / đề tài *</p>
              <Select<string>
                aria-labelledby="council-group-label"
                value={groupId}
                onChange={(value) => { setGroupId(value); setActionError(''); }}
                options={available.map((group) => ({
                  value: group.groupId,
                  label: group.topicTitleVi,
                  description: group.assignedCouncilId
                    ? 'Đã gán vào hội đồng khác'
                    : `${group.hasEvaluation ? 'Đã có đánh giá phản biện' : 'Chưa có đánh giá phản biện'} · GV phản biện: ${group.reviewerFullName || 'Chưa rõ'}`,
                  disabled: Boolean(group.assignedCouncilId),
                }))}
                placeholder={candidates.isLoading ? 'Đang tải nhóm…' : 'Chọn nhóm bảo vệ'}
                searchable
                searchPlaceholder="Tìm tên đề tài"
                disabled={saving || candidates.isLoading || candidates.isError || assignable.length === 0}
                size="lg"
                triggerClassName="text-sm"
              />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="council-group-order" className="text-sm font-medium text-ink-secondary">Thứ tự *</label>
              <Input id="council-group-order" type="number" min={1} step={1} value={orderText || nextOrder} onChange={(event) => setOrderText(event.target.value)} disabled={saving} required />
            </div>
          </div>
          {candidates.isError && <InlineAlert title="Không tải được nhóm có thể gán" action={<button type="button" className="font-medium underline" onClick={() => void candidates.refetch()}>Thử lại</button>}>{memberError(candidates.error)}</InlineAlert>}
          {candidates.isSuccess && assignable.length === 0 && (
            <p className="text-sm text-ink-muted">
              {candidates.data.length === 0
                ? 'Đợt này chưa có nhóm được phân công giảng viên phản biện. Hoàn tất phân công phản biện trước khi xếp lịch bảo vệ.'
                : 'Không còn nhóm chưa gán trong đợt này. Các nhóm có giảng viên phản biện đã được xếp vào hội đồng; hãy kiểm tra hội đồng đã nhận nhóm hoặc chọn đợt khác.'}
            </p>
          )}
          {chosen && eligibility.isLoading && <PendingState label="Đang kiểm tra điều kiện bảo vệ…" />}
          {chosen && eligibility.isSuccess && !eligibility.data.canProceed && <p className="text-sm text-tone-warning-text">{eligibilityMessage(eligibility.data.reasonCode)}</p>}
          {chosen && eligibility.isError && <InlineAlert title="Không kiểm tra được điều kiện nhóm">{memberError(eligibility.error)}</InlineAlert>}
          {council.isSubmittedToAcademicAffairs && <p className="text-sm text-tone-warning-text">Gán thêm nhóm sẽ đưa hồ sơ về bước chuẩn bị. Khoa cần gửi lại cho PĐT.</p>}
          {orderDirty && <p className="text-sm text-tone-warning-text">Lưu hoặc bỏ thay đổi thứ tự trước khi gán nhóm khác.</p>}
          <Button type="submit" disabled={orderDirty || !groupId || orderTaken || eligibility.isLoading || eligibility.isError || (eligibility.isSuccess && !eligibility.data.canProceed)} isLoading={saving} loadingText="Đang gán…">Gán nhóm vào hội đồng</Button>
          {orderTaken && <p className="text-sm text-tone-warning-text">Thứ tự này đã được dùng. Hãy chọn số khác.</p>}
        </form>
      )}
      {actionError && <div className="mt-4"><InlineAlert title="Chưa lưu được thay đổi">{actionError}</InlineAlert></div>}
      {success && <p className="mt-3 text-sm text-tone-success-text" role="status">{success}</p>}
      <ConfirmDialog
        open={Boolean(removeTarget)}
        onClose={() => !saving && setRemoveTarget(null)}
        onConfirm={() => void removeGroup()}
        title="Gỡ nhóm khỏi hội đồng?"
        description={`Nhóm “${removeTarget?.topicTitleVi ?? ''}” sẽ không còn trong lịch bảo vệ của hội đồng này.${council.isSubmittedToAcademicAffairs ? ' Khoa cần gửi lại hồ sơ cho PĐT.' : ''}`}
        confirmText="Gỡ nhóm"
        cancelText="Quay lại"
        type="warning"
        isConfirming={saving && Boolean(removeTarget)}
      />
      <ConfirmDialog
        open={confirmCancelSchedule}
        onClose={() => setConfirmCancelSchedule(false)}
        onConfirm={cancelScheduleEdit}
        title="Bỏ thay đổi lịch bảo vệ?"
        description="Ngày, ca và phòng bạn vừa nhập chưa được lưu."
        confirmText="Bỏ thay đổi"
        cancelText="Tiếp tục sửa"
        type="warning"
      />
    </Card>
  );
}
