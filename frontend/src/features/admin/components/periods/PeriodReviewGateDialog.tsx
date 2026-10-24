import React, { useState, useEffect } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { ReviewGateConfigurationDto } from '../../types/period.types';

interface PeriodReviewGateDialogProps {
  open: boolean;
  onClose: () => void;
  canEdit: boolean;
  syllabusGate?: ReviewGateConfigurationDto | null;
  midtermGate?: ReviewGateConfigurationDto | null;
  onSaveGates: (
    syllabus: {
      initial: string;
      faculty: string;
      final: string;
    },
    midterm: {
      initial: string;
      faculty: string;
      final: string;
    },
  ) => Promise<void>;
}

function toLocalDatetimeInput(utcStr?: string): string {
  if (!utcStr) return '';
  try {
    const d = new Date(utcStr);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    const yyyy = d.getFullYear();
    const mm = pad(d.getMonth() + 1);
    const dd = pad(d.getDate());
    const hh = pad(d.getHours());
    const min = pad(d.getMinutes());
    return `${yyyy}-${mm}-${dd}T${hh}:${min}`;
  } catch {
    return '';
  }
}

export const PeriodReviewGateDialog: React.FC<PeriodReviewGateDialogProps> = ({
  open,
  onClose,
  canEdit,
  syllabusGate,
  midtermGate,
  onSaveGates,
}) => {
  const [activeTab, setActiveTab] = useState<'Syllabus' | 'Midterm'>('Syllabus');

  const [sInitial, setSInitial] = useState('');
  const [sFaculty, setSFaculty] = useState('');
  const [sFinal, setSFinal] = useState('');

  const [mInitial, setMInitial] = useState('');
  const [mFaculty, setMFaculty] = useState('');
  const [mFinal, setMFinal] = useState('');

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setSInitial(toLocalDatetimeInput(syllabusGate?.initialSubmissionDeadlineUtc));
      setSFaculty(toLocalDatetimeInput(syllabusGate?.facultyResponseDeadlineUtc));
      setSFinal(toLocalDatetimeInput(syllabusGate?.finalClosureDeadlineUtc));

      setMInitial(toLocalDatetimeInput(midtermGate?.initialSubmissionDeadlineUtc));
      setMFaculty(toLocalDatetimeInput(midtermGate?.facultyResponseDeadlineUtc));
      setMFinal(toLocalDatetimeInput(midtermGate?.finalClosureDeadlineUtc));
      setError(null);
    }
  }, [open, syllabusGate, midtermGate]);

  const handleSave = async () => {
    setError(null);

    if (sInitial && sFaculty && sFinal) {
      if (new Date(sInitial) >= new Date(sFaculty) || new Date(sFaculty) >= new Date(sFinal)) {
        setError('Cổng Đề cương: Hạn sinh viên nộp < Hạn Khoa phản hồi < Hạn chót đóng cổng.');
        return;
      }
    }

    if (mInitial && mFaculty && mFinal) {
      if (new Date(mInitial) >= new Date(mFaculty) || new Date(mFaculty) >= new Date(mFinal)) {
        setError('Cổng Giữa kỳ: Hạn sinh viên nộp < Hạn Khoa phản hồi < Hạn chót đóng cổng.');
        return;
      }
    }

    setIsSubmitting(true);
    try {
      await onSaveGates(
        { initial: sInitial, faculty: sFaculty, final: sFinal },
        { initial: mInitial, faculty: mFaculty, final: mFinal },
      );
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Lỗi khi lưu cấu hình cổng.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Cấu hình chi tiết mốc nộp & phản hồi các cổng thẩm định"
      className="max-w-lg"
    >
      <div className="space-y-4">
        <div className="flex border-b border-slate-200">
          <button
            type="button"
            onClick={() => setActiveTab('Syllabus')}
            className={`px-4 py-2 text-sm font-semibold border-b-2 transition-colors ${
              activeTab === 'Syllabus'
                ? 'border-[#2054A5] text-[#2054A5]'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            Cổng 1: Đề cương chi tiết
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('Midterm')}
            className={`px-4 py-2 text-sm font-semibold border-b-2 transition-colors ${
              activeTab === 'Midterm'
                ? 'border-[#2054A5] text-[#2054A5]'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            Cổng 2: Báo cáo giữa kỳ
          </button>
        </div>

        {error && (
          <div className="rounded-md bg-rose-50 p-3 text-xs font-medium text-rose-700">
            {error}
          </div>
        )}

        {activeTab === 'Syllabus' ? (
          <div className="space-y-3.5 py-2">
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                1. Hạn sinh viên nộp đề cương ban đầu
              </label>
              <Input
                type="datetime-local"
                disabled={!canEdit}
                value={sInitial}
                onChange={(e) => setSInitial(e.target.value)}
                className="h-10 text-sm"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                2. Hạn Khoa / GV phản hồi & thẩm định
              </label>
              <Input
                type="datetime-local"
                disabled={!canEdit}
                value={sFaculty}
                onChange={(e) => setSFaculty(e.target.value)}
                className="h-10 text-sm"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                3. Hạn chót đóng cổng & nộp hoàn thiện
              </label>
              <Input
                type="datetime-local"
                disabled={!canEdit}
                value={sFinal}
                onChange={(e) => setSFinal(e.target.value)}
                className="h-10 text-sm"
              />
            </div>
          </div>
        ) : (
          <div className="space-y-3.5 py-2">
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                1. Hạn sinh viên nộp báo cáo giữa kỳ
              </label>
              <Input
                type="datetime-local"
                disabled={!canEdit}
                value={mInitial}
                onChange={(e) => setMInitial(e.target.value)}
                className="h-10 text-sm"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                2. Hạn GVHD / Khoa đánh giá giữa kỳ
              </label>
              <Input
                type="datetime-local"
                disabled={!canEdit}
                value={mFaculty}
                onChange={(e) => setMFaculty(e.target.value)}
                className="h-10 text-sm"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                3. Hạn chót kết luận đánh giá giữa kỳ
              </label>
              <Input
                type="datetime-local"
                disabled={!canEdit}
                value={mFinal}
                onChange={(e) => setMFinal(e.target.value)}
                className="h-10 text-sm"
              />
            </div>
          </div>
        )}

        <div className="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
          >
            Đóng
          </Button>
          {canEdit && (
            <Button
              type="button"
              disabled={isSubmitting}
              onClick={handleSave}
              className="bg-[#2054A5] hover:bg-[#1a4485] text-white"
            >
              {isSubmitting ? 'Đang lưu...' : 'Lưu cổng thẩm định'}
            </Button>
          )}
        </div>
      </div>
    </Dialog>
  );
};