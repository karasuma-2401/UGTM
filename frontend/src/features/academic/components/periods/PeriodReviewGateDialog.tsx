import React, { useState, useEffect } from 'react';
import { AlertCircle, Sparkles, CheckCircle2 } from 'lucide-react';
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

  // Fluent animation & feedback states
  const [justApplied, setJustApplied] = useState(false);
  const [isFlashing, setIsFlashing] = useState(false);

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
      setJustApplied(false);
      setIsFlashing(false);
    }
  }, [open, syllabusGate, midtermGate, defaultSyllabusDeadline, defaultMidtermDeadline]);

  const handleApplyDefaults = () => {
    if (activeTab === 'Syllabus' && defaultSyllabusDeadline) {
      setSFinal(defaultSyllabusDeadline);
      setSFaculty(addDaysToDateString(defaultSyllabusDeadline, -2));
      setSInitial(addDaysToDateString(defaultSyllabusDeadline, -5));
      setError(null);
      setJustApplied(true);
      setIsFlashing(true);
      setTimeout(() => setIsFlashing(false), 700);
      setTimeout(() => setJustApplied(false), 3000);
    } else if (activeTab === 'Midterm' && defaultMidtermDeadline) {
      setMFinal(defaultMidtermDeadline);
      setMFaculty(addDaysToDateString(defaultMidtermDeadline, -2));
      setMInitial(addDaysToDateString(defaultMidtermDeadline, -5));
      setError(null);
      setJustApplied(true);
      setIsFlashing(true);
      setTimeout(() => setIsFlashing(false), 700);
      setTimeout(() => setJustApplied(false), 3000);
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
        {/* Segmented Tab Controls with Fluent styling */}
        <div className="flex rounded-xl border border-slate-200 bg-slate-100/80 p-1 select-none">
          <button
            type="button"
            onClick={() => {
              setActiveTab('Syllabus');
              setJustApplied(false);
            }}
            className={`flex-1 flex items-center justify-center gap-2 rounded-lg py-2 text-xs font-semibold select-none transition-all duration-200 ${
              activeTab === 'Syllabus'
                ? 'bg-white text-slate-900 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <span
              className={`size-1.5 rounded-full transition-colors ${
                activeTab === 'Syllabus' ? 'bg-primary' : 'bg-transparent'
              }`}
            />
            Cổng 1: Đề cương chi tiết
          </button>
          <button
            type="button"
            onClick={() => {
              setActiveTab('Midterm');
              setJustApplied(false);
            }}
            className={`flex-1 flex items-center justify-center gap-2 rounded-lg py-2 text-xs font-semibold select-none transition-all duration-200 ${
              activeTab === 'Midterm'
                ? 'bg-white text-slate-900 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <span
              className={`size-1.5 rounded-full transition-colors ${
                activeTab === 'Midterm' ? 'bg-primary' : 'bg-transparent'
              }`}
            />
            Cổng 2: Báo cáo giữa kỳ
          </button>
        </div>

        {/* 3-Step Process Stepper Card with Action Button */}
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3.5 space-y-3">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs font-bold text-slate-900">
              Quy trình thẩm định 3 bước
            </span>

            {canEdit && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleApplyDefaults}
                className={`h-7 px-2.5 gap-1.5 rounded-lg text-xs font-semibold transition-all duration-200 active:scale-95 shadow-2xs select-none ${
                  justApplied
                    ? 'border-emerald-300 bg-emerald-50 text-emerald-800'
                    : 'border-blue-200 bg-blue-50/90 text-primary hover:bg-blue-100 hover:border-blue-300'
                }`}
                title="Tự động tính: Hạn chót, Khoa phản hồi (-2 ngày), SV nộp (-5 ngày)"
              >
                {justApplied ? (
                  <>
                    <CheckCircle2 className="size-3 text-emerald-600" />
                    Đã áp dụng mốc chuẩn!
                  </>
                ) : (
                  <>
                    <Sparkles className="size-3 text-primary" />
                    Gợi ý mốc chuẩn theo đợt
                  </>
                )}
              </Button>
            )}
          </div>

          {/* Stepper Rail Cards */}
          <div className="grid grid-cols-3 gap-2">
            <div className="flex flex-col items-center text-center p-2 rounded-lg bg-white border border-slate-200 shadow-2xs">
              <span className="size-5 rounded-full bg-slate-100 text-slate-700 text-[10px] font-bold flex items-center justify-center mb-1">
                1
              </span>
              <span className="text-[11px] font-semibold text-slate-800 leading-tight">
                SV nộp ban đầu
              </span>
              <span className="text-[10px] text-slate-400 mt-0.5">
                Mốc -5 ngày
              </span>
            </div>

            <div className="flex flex-col items-center text-center p-2 rounded-lg bg-white border border-slate-200 shadow-2xs">
              <span className="size-5 rounded-full bg-slate-100 text-slate-700 text-[10px] font-bold flex items-center justify-center mb-1">
                2
              </span>
              <span className="text-[11px] font-semibold text-slate-800 leading-tight">
                Khoa/GV phản hồi
              </span>
              <span className="text-[10px] text-slate-400 mt-0.5">
                Mốc -2 ngày
              </span>
            </div>

            <div className="flex flex-col items-center text-center p-2 rounded-lg bg-white border border-slate-200 shadow-2xs">
              <span className="size-5 rounded-full bg-primary text-white text-[10px] font-bold flex items-center justify-center mb-1">
                3
              </span>
              <span className="text-[11px] font-semibold text-slate-900 leading-tight">
                Chốt đóng cổng
              </span>
              <span className="text-[10px] text-primary font-medium mt-0.5">
                Hạn chót đợt
              </span>
            </div>
          </div>

          {/* Feedback notice when defaults applied */}
          {justApplied && (
            <div className="flex items-center gap-2 rounded-lg bg-emerald-50 border border-emerald-200/90 px-3 py-1.5 text-xs text-emerald-800 animate-in fade-in slide-in-from-top-1 duration-200">
              <CheckCircle2 className="size-3.5 text-emerald-600 shrink-0" />
              <span>
                Đã điền 3 mốc chuẩn theo đợt: Hạn chót đợt, GV phản hồi (-2 ngày), SV nộp (-5 ngày).
              </span>
            </div>
          )}
        </div>

        {error && (
          <div className="flex items-start gap-2 rounded-xl bg-rose-50 border border-rose-200 p-3 text-xs font-medium text-rose-700">
            <AlertCircle className="size-4 shrink-0 mt-0.5" />
            <span>{error}</span>
          </div>
        )}

        {/* Tab 1: Cổng Đề cương chi tiết */}
        {activeTab === 'Syllabus' && (
          <div
            key="tab-syllabus"
            className="space-y-3.5 animate-in fade-in slide-in-from-right-2 duration-250 ease-out"
          >
            <div>
              <label className="mb-1.5 flex items-center justify-between text-xs font-semibold text-slate-700">
                <span>1. Hạn sinh viên nộp đề cương ban đầu</span>
                <span className="text-[11px] font-normal text-slate-400">Trước phản hồi 3 ngày</span>
              </label>
              <DatePicker
                aria-label="Hạn sinh viên nộp đề cương ban đầu"
                disabled={!canEdit}
                value={sInitial}
                onValueChange={(value) => {
                  setSInitial(value);
                  setError(null);
                }}
                className={`h-9 text-sm rounded-lg border-slate-200 bg-white transition-all ${
                  isFlashing ? 'ring-2 ring-primary/40 border-primary' : ''
                }`}
              />
            </div>

            <div>
              <label className="mb-1.5 flex items-center justify-between text-xs font-semibold text-slate-700">
                <span>2. Hạn Khoa / GV phản hồi & thẩm định</span>
                <span className="text-[11px] font-normal text-slate-400">Trước hạn chót 2 ngày</span>
              </label>
              <DatePicker
                aria-label="Hạn Khoa / GV phản hồi đề cương"
                disabled={!canEdit}
                value={sFaculty}
                onValueChange={(value) => {
                  setSFaculty(value);
                  setError(null);
                }}
                className={`h-9 text-sm rounded-lg border-slate-200 bg-white transition-all ${
                  isFlashing ? 'ring-2 ring-primary/40 border-primary' : ''
                }`}
              />
            </div>

            <div>
              <label className="mb-1.5 flex items-center justify-between text-xs font-semibold text-slate-700">
                <span>3. Hạn chót đóng cổng & nộp hoàn thiện</span>
                <span className="text-[11px] font-normal text-primary">Mốc deadline đợt</span>
              </label>
              <DatePicker
                aria-label="Hạn chót đóng cổng đề cương"
                disabled={!canEdit}
                value={sFinal}
                onValueChange={(value) => {
                  setSFinal(value);
                  setError(null);
                }}
                className={`h-9 text-sm rounded-lg border-slate-200 bg-white transition-all ${
                  isFlashing ? 'ring-2 ring-primary/40 border-primary' : ''
                }`}
              />
            </div>
          </div>
        )}

        {/* Tab 2: Cổng Báo cáo giữa kỳ */}
        {activeTab === 'Midterm' && (
          <div
            key="tab-midterm"
            className="space-y-3.5 animate-in fade-in slide-in-from-right-2 duration-250 ease-out"
          >
            <div>
              <label className="mb-1.5 flex items-center justify-between text-xs font-semibold text-slate-700">
                <span>1. Hạn sinh viên nộp báo cáo giữa kỳ</span>
                <span className="text-[11px] font-normal text-slate-400">Trước đánh giá 3 ngày</span>
              </label>
              <DatePicker
                aria-label="Hạn sinh viên nộp báo cáo giữa kỳ"
                disabled={!canEdit}
                value={mInitial}
                onValueChange={(value) => {
                  setMInitial(value);
                  setError(null);
                }}
                className={`h-9 text-sm rounded-lg border-slate-200 bg-white transition-all ${
                  isFlashing ? 'ring-2 ring-primary/40 border-primary' : ''
                }`}
              />
            </div>

            <div>
              <label className="mb-1.5 flex items-center justify-between text-xs font-semibold text-slate-700">
                <span>2. Hạn GVHD / Khoa đánh giá giữa kỳ</span>
                <span className="text-[11px] font-normal text-slate-400">Trước kết luận 2 ngày</span>
              </label>
              <DatePicker
                aria-label="Hạn GVHD / Khoa đánh giá giữa kỳ"
                disabled={!canEdit}
                value={mFaculty}
                onValueChange={(value) => {
                  setMFaculty(value);
                  setError(null);
                }}
                className={`h-9 text-sm rounded-lg border-slate-200 bg-white transition-all ${
                  isFlashing ? 'ring-2 ring-primary/40 border-primary' : ''
                }`}
              />
            </div>

            <div>
              <label className="mb-1.5 flex items-center justify-between text-xs font-semibold text-slate-700">
                <span>3. Hạn chót kết luận đánh giá giữa kỳ</span>
                <span className="text-[11px] font-normal text-primary">Mốc deadline đợt</span>
              </label>
              <DatePicker
                aria-label="Hạn chót kết luận đánh giá giữa kỳ"
                disabled={!canEdit}
                value={mFinal}
                onValueChange={(value) => {
                  setMFinal(value);
                  setError(null);
                }}
                className={`h-9 text-sm rounded-lg border-slate-200 bg-white transition-all ${
                  isFlashing ? 'ring-2 ring-primary/40 border-primary' : ''
                }`}
              />
            </div>
          </div>
        )}

        {/* Action Buttons Footer */}
        <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-slate-200">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onClose}
            className="rounded-xl border-slate-200 text-xs font-semibold h-9 px-4 hover:bg-slate-50"
          >
            Đóng
          </Button>
          {canEdit && (
            <Button
              type="button"
              size="sm"
              disabled={isSubmitting}
              onClick={handleSave}
              className="rounded-xl bg-primary hover:bg-primary/90 text-white font-semibold text-xs h-9 px-5 shadow-sm active:scale-[0.98] transition-all"
            >
              {isSubmitting ? 'Đang lưu...' : 'Lưu cổng thẩm định'}
            </Button>
          )}
        </div>
      </div>
    </Dialog>
  );
};
