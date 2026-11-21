import { useState, type FormEvent } from 'react';
import { DatePicker } from '@/components/ui/calendar';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import type { AcademicBatch } from '@/shared/academic-periods/types';
import { councilPlanningApi } from '../../services/councilPlanningApi';
import { memberError } from '../../services/councilMembersApi';
import { useCouncilDefenseStage } from '../../hooks/useCouncilDefenseStage';
import { CouncilDefenseStageNotice } from './CouncilDefenseStageNotice';
import { batchLocalDate, councilDateWindow, suggestedDefenseDate } from '../../utils/councilDefenseWindow';

export function CouncilCreateCard({
  batches,
  initialBatchId,
  onCancel,
  onCreated,
}: {
  batches: (AcademicBatch & { label: string })[];
  initialBatchId: string;
  onCancel: () => void;
  onCreated: (id: string, batchId: string) => Promise<void>;
}) {
  const [batchId, setBatchId] = useState(initialBatchId === 'all' ? batches[0]?.id ?? '' : initialBatchId);
  const [councilCode, setCouncilCode] = useState('');
  const [defenseDate, setDefenseDate] = useState<string | null>(null);
  const [timeSlot, setTimeSlot] = useState('');
  const [room, setRoom] = useState('');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [confirmClose, setConfirmClose] = useState(false);
  const { query: timelineQuery, canRead } = useCouncilDefenseStage(batchId);
  const batch = batches.find((item) => item.id === batchId);
  const window = councilDateWindow(timelineQuery.data, batch);
  const timelineLoading = canRead && timelineQuery.isPending && timelineQuery.fetchStatus === 'fetching';
  const suggestedDate = timelineLoading ? '' : suggestedDefenseDate(window, timelineQuery.data?.currentLocalDate ?? batchLocalDate(batch));
  const selectedDate = defenseDate ?? suggestedDate;
  const hasInput = Boolean(councilCode.trim() || defenseDate || timeSlot.trim() || room.trim());

  function requestClose() {
    if (hasInput) setConfirmClose(true);
    else onCancel();
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    if (!batchId || !councilCode.trim() || !selectedDate || !timeSlot.trim() || !room.trim()) {
      setError('Điền đầy đủ đợt, mã hội đồng, ngày, ca và phòng bảo vệ.');
      return;
    }
    setSaving(true);
    setError('');
    try {
      const created = await councilPlanningApi.create({
        batchId,
        councilCode: councilCode.trim(),
        defenseDate: selectedDate,
        timeSlot: timeSlot.trim(),
        room: room.trim(),
      });
      await onCreated(created.id, batchId);
    } catch (cause) {
      setError(memberError(cause));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Card className="min-w-0 p-5 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold text-ink-heading">Tạo hội đồng bảo vệ</h2>
          <p className="mt-1 text-sm text-ink-muted">Đặt lịch và phòng trước. Sau khi tạo, bạn sẽ gán nhóm rồi phân công giảng viên.</p>
        </div>
        <Button type="button" variant="outline" size="sm" disabled={saving} onClick={requestClose}>Đóng</Button>
      </div>
      <form onSubmit={(event) => void submit(event)} className="mt-5 space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <p id="create-council-batch-label" className="text-sm font-medium text-ink-secondary">Đợt khóa luận *</p>
            <Select<string>
              aria-labelledby="create-council-batch-label"
              options={batches.map((batch) => ({ value: batch.id, label: batch.label }))}
              value={batchId}
              onChange={setBatchId}
              placeholder="Chọn đợt khóa luận"
              size="lg"
              triggerClassName="text-sm"
              disabled={saving}
            />
          </div>
          <div className="space-y-1.5">
            <label htmlFor="create-council-code" className="text-sm font-medium text-ink-secondary">Mã hội đồng *</label>
            <Input id="create-council-code" value={councilCode} onChange={(event) => setCouncilCode(event.target.value)} maxLength={50} required disabled={saving} placeholder="Ví dụ: HĐ-CNTT-01" />
          </div>
          <div className="sm:col-span-2">
            <CouncilDefenseStageNotice
              window={window}
              date={selectedDate}
              loading={timelineLoading}
              error={timelineQuery.isError}
              canRead={canRead}
              onRetry={() => void timelineQuery.refetch()}
            />
          </div>
          <div className="space-y-1.5">
            <label htmlFor="create-council-date" className="text-sm font-medium text-ink-secondary">Ngày bảo vệ *</label>
            <DatePicker id="create-council-date" value={selectedDate} onValueChange={setDefenseDate} required disabled={saving} placeholder="Chọn ngày bảo vệ" />
          </div>
          <div className="space-y-1.5">
            <label htmlFor="create-council-time" className="text-sm font-medium text-ink-secondary">Ca bảo vệ *</label>
            <Input id="create-council-time" value={timeSlot} onChange={(event) => setTimeSlot(event.target.value)} maxLength={50} required disabled={saving} placeholder="Ví dụ: 08:00 - 11:30" />
          </div>
          <div className="space-y-1.5 sm:col-span-2">
            <label htmlFor="create-council-room" className="text-sm font-medium text-ink-secondary">Phòng bảo vệ *</label>
            <Input id="create-council-room" value={room} onChange={(event) => setRoom(event.target.value)} maxLength={50} required disabled={saving} placeholder="Ví dụ: B1.401" />
          </div>
        </div>
        {error && <InlineAlert title="Chưa tạo được hội đồng">{error}</InlineAlert>}
        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={batches.length === 0} isLoading={saving} loadingText="Đang tạo…">Tạo hội đồng</Button>
          <Button type="button" variant="outline" disabled={saving} onClick={requestClose}>Hủy</Button>
        </div>
      </form>
      <ConfirmDialog
        open={confirmClose}
        onClose={() => setConfirmClose(false)}
        onConfirm={onCancel}
        title="Bỏ thông tin hội đồng đang nhập?"
        description="Thông tin chưa lưu sẽ mất."
        confirmText="Bỏ thông tin"
        cancelText="Tiếp tục nhập"
        type="warning"
      />
    </Card>
  );
}
