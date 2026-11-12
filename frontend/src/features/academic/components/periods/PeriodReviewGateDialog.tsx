import React, { useState, useEffect } from 'react';
import { ArrowRight, AlertCircle } from 'lucide-react';
import { DatePicker } from '@/components/ui/calendar';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { addDaysToDateString } from '../../utils/periodDates';
import type { ReviewGateConfigurationDto } from '../../types/period.types';

interface PeriodReviewGateDialogProps {
  open: boolean;
  onClose: () => void;
  canEdit: boolean;
  syllabusGate?: ReviewGateConfigurationDto | null;
  midtermGate?: ReviewGateConfigurationDto | null;
  defaultSyllabusDeadline?: string;
  defaultMidtermDeadline?: string;
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

function toLocalDateInput(utcStr?: string): string {
  if (!utcStr) return '';
  if (/^\d{4}-\d{2}-\d{2}$/.test(utcStr)) {
    return utcStr;
  }
  try {
    const d = new Date(utcStr);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
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
  defaultSyllabusDeadline,
  defaultMidtermDeadline,
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
      const savedSInitial = toLocalDateInput(syllabusGate?.initialSubmissionDeadlineUtc);
      const savedSFaculty = toLocalDateInput(syllabusGate?.facultyResponseDeadlineUtc);
      const savedSFinal = toLocalDateInput(syllabusGate?.finalClosureDeadlineUtc);

      if (savedSInitial || savedSFaculty || savedSFinal) {
        setSInitial(savedSInitial);
        setSFaculty(savedSFaculty);
        setSFinal(savedSFinal);
      } else if (defaultSyllabusDeadline) {
        setSFinal(defaultSyllabusDeadline);
        setSFaculty(addDaysToDateString(defaultSyllabusDeadline, -2));
        setSInitial(addDaysToDateString(defaultSyllabusDeadline, -5));
      } else {
        setSInitial('');
        setSFaculty('');
        setSFinal('');
      }

      const savedMInitial = toLocalDateInput(midtermGate?.initialSubmissionDeadlineUtc);
      const savedMFaculty = toLocalDateInput(midtermGate?.facultyResponseDeadlineUtc);
      const savedMFinal = toLocalDateInput(midtermGate?.finalClosureDeadlineUtc);

      if (savedMInitial || savedMFaculty || savedMFinal) {
        setMInitial(savedMInitial);
        setMFaculty(savedMFaculty);
        setMFinal(savedMFinal);
      } else if (defaultMidtermDeadline) {
        setMFinal(defaultMidtermDeadline);
        setMFaculty(addDaysToDateString(defaultMidtermDeadline, -2));
        setMInitial(addDaysToDateString(defaultMidtermDeadline, -5));
      } else {
        setMInitial('');
        setMFaculty('');
        setMFinal('');
      }

      setError(null);
    }
  }, [open, syllabusGate, midtermGate, defaultSyllabusDeadline, defaultMidtermDeadline]);

  const handleApplyDefaults = () => {
    if (activeTab === 'Syllabus' && defaultSyllabusDeadline) {
      setSFinal(defaultSyllabusDeadline);
      setSFaculty(addDaysToDateString(defaultSyllabusDeadline, -2));
      setSInitial(addDaysToDateString(defaultSyllabusDeadline, -5));
      setError(null);
    } else if (activeTab === 'Midterm' && defaultMidtermDeadline) {
      setMFinal(defaultMidtermDeadline);
      setMFaculty(addDaysToDateString(defaultMidtermDeadline, -2));
      setMInitial(addDaysToDateString(defaultMidtermDeadline, -5));
      setError(null);
    }
  };

  const handleSave = async () => {
    setError(null);

    if (sInitial && sFaculty && sFinal) {
      if (sInitial >= sFaculty || sFaculty >= sFinal) {
        setError('Cổng Đề cương: Hạn sinh viên nộp < Hạn Khoa phản hồi < Hạn chót đóng cổng.');
        return;
      }
    }

    if (mInitial && mFaculty && mFinal) {
      if (mInitial >= mFaculty || mFaculty >= mFinal) {
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
      const msg = err instanceof Error ? err.message : 'Có lỗi khi lưu cổng thẩm định.';
      setError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Cấu hình cổng thẩm định"
      className="max-w-lg"
    >
      <div className="space-y-4 p-5">
        {/* Tab selection */}
        <div className="flex rounded-xl bg-slate-100 p-1 gap-1">
          <button
            type="button"
            onClick={() => setActiveTab('Syllabus')}
            className={`flex-1 rounded-lg py-2 text-xs font-semibold transition-all ${
              activeTab === 'Syllabus'
                ? 'bg-white text-slate-900 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Cổng 1: Đề cương chi tiết
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('Midterm')}
            className={`flex-1 rounded-lg py-2 text-xs font-semibold transition-all ${
              activeTab === 'Midterm'
                ? 'bg-white text-slate-900 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Cổng 2: Báo cáo giữa kỳ
          </button>
        </div>

        {/* 3-Step Process Flow Preview */}
        <div className="rounded-xl border border-slate-200/90 bg-slate-50/70 p-3 text-xs">
          <div className="mb-2 flex items-center justify-between">
            <span className="font-semibold text-slate-700">Quy trình 3 bước:</span>
            {canEdit && (
              <button
                type="button"
                onClick={handleApplyDefaults}
                className="text-[11px] font-semibold text-primary hover:underline"
              >
                Gợi ý mốc chuẩn theo đợt
              </button>
            )}
          </div>
          <div className="flex items-center justify-between text-[11px] text-slate-600">
            <span className="font-medium text-slate-800">1. SV nộp</span>
            <ArrowRight className="size-3 text-slate-400" />
            <span className="font-medium text-slate-800">2. Khoa/GV phản hồi</span>
            <ArrowRight className="size-3 text-slate-400" />
            <span className="font-medium text-slate-800">3. Chốt đóng cổng</span>
          </div>
        </div>

        {error && (
          <div className="flex items-start gap-2 rounded-xl bg-rose-50 border border-rose-200 p-3 text-xs font-medium text-rose-700">
            <AlertCircle className="size-4 shrink-0 mt-0.5" />
            <span>{error}</span>
          </div>
        )}

        {/* Form fields */}
        {activeTab === 'Syllabus' ? (
          <div className="space-y-3.5">
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                1. Hạn sinh viên nộp đề cương ban đầu
              </label>
              <DatePicker
                aria-label="Hạn sinh viên nộp đề cương ban đầu"
                disabled={!canEdit}
                value={sInitial}
                onValueChange={(value) => setSInitial(value)}
                className="h-9 text-sm rounded-lg border-slate-200"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                2. Hạn Khoa / GV phản hồi & thẩm định
              </label>
              <DatePicker
                aria-label="Hạn Khoa / GV phản hồi đề cương"
                disabled={!canEdit}
                value={sFaculty}
                onValueChange={(value) => setSFaculty(value)}
                className="h-9 text-sm rounded-lg border-slate-200"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                3. Hạn chót đóng cổng & nộp hoàn thiện
              </label>
              <DatePicker
                aria-label="Hạn chót đóng cổng đề cương"
                disabled={!canEdit}
                value={sFinal}
                onValueChange={(value) => setSFinal(value)}
                className="h-9 text-sm rounded-lg border-slate-200"
              />
            </div>
          </div>
        ) : (
          <div className="space-y-3.5">
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                1. Hạn sinh viên nộp báo cáo giữa kỳ
              </label>
              <DatePicker
                aria-label="Hạn sinh viên nộp báo cáo giữa kỳ"
                disabled={!canEdit}
                value={mInitial}
                onValueChange={(value) => setMInitial(value)}
                className="h-9 text-sm rounded-lg border-slate-200"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                2. Hạn GVHD / Khoa đánh giá giữa kỳ
              </label>
              <DatePicker
                aria-label="Hạn GVHD / Khoa đánh giá giữa kỳ"
                disabled={!canEdit}
                value={mFaculty}
                onValueChange={(value) => setMFaculty(value)}
                className="h-9 text-sm rounded-lg border-slate-200"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                3. Hạn chót kết luận đánh giá giữa kỳ
              </label>
              <DatePicker
                aria-label="Hạn chót kết luận đánh giá giữa kỳ"
                disabled={!canEdit}
                value={mFinal}
                onValueChange={(value) => setMFinal(value)}
                className="h-9 text-sm rounded-lg border-slate-200"
              />
            </div>
          </div>
        )}

        <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-slate-100">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onClose}
            className="rounded-lg border-slate-200 text-xs"
          >
            Đóng
          </Button>
          {canEdit && (
            <Button
              type="button"
              size="sm"
              disabled={isSubmitting}
              onClick={handleSave}
              className="rounded-lg bg-primary hover:bg-primary/90 text-white font-semibold text-xs"
            >
              {isSubmitting ? 'Đang lưu...' : 'Lưu cổng thẩm định'}
            </Button>
          )}
        </div>
      </div>
    </Dialog>
  );
};
