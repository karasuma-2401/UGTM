import { useState, useEffect } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { PlusCircle, Save, CheckCircle } from 'lucide-react';
import type { MilestoneWithReportDto } from '../../types/milestone.types';

interface AdvisorMilestoneFormProps {
  selectedMilestone?: MilestoneWithReportDto | null;
  isSubmitting?: boolean;
  onSubmit: (data: { title: string; description: string; deadlineUtc: string }) => void;
  onNavigateReview?: () => void;
  onResetForm?: () => void;
}

export function AdvisorMilestoneForm({
  selectedMilestone,
  isSubmitting = false,
  onSubmit,
  onNavigateReview,
  onResetForm,
}: AdvisorMilestoneFormProps) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [deadlineLocal, setDeadlineLocal] = useState('');

  useEffect(() => {
    if (selectedMilestone) {
      setTitle(selectedMilestone.milestone.title);
      setDescription(selectedMilestone.milestone.description || '');
      try {
        const d = new Date(selectedMilestone.milestone.deadlineUtc);
        const pad = (n: number) => String(n).padStart(2, '0');
        const formatted = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
        setDeadlineLocal(formatted);
      } catch {
        setDeadlineLocal('');
      }
    } else {
      setTitle('');
      setDescription('');
      const defaultDate = new Date();
      defaultDate.setDate(defaultDate.getDate() + 7);
      defaultDate.setHours(23, 59, 0, 0);
      const pad = (n: number) => String(n).padStart(2, '0');
      const formatted = `${defaultDate.getFullYear()}-${pad(defaultDate.getMonth() + 1)}-${pad(defaultDate.getDate())}T${pad(defaultDate.getHours())}:${pad(defaultDate.getMinutes())}`;
      setDeadlineLocal(formatted);
    }
  }, [selectedMilestone]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !deadlineLocal) return;

    const dateObj = new Date(deadlineLocal);
    const deadlineUtc = dateObj.toISOString();

    onSubmit({
      title: title.trim(),
      description: description.trim(),
      deadlineUtc,
    });
  };

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-5">
      <div className="flex items-center justify-between">
        <h3 className="text-base font-semibold text-slate-900">
          {selectedMilestone ? 'Chỉnh sửa mốc tiến độ' : 'Tạo / sửa mốc'}
        </h3>
        {selectedMilestone && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="text-xs text-primary"
            onClick={onResetForm}
          >
            <PlusCircle className="size-3.5 mr-1" />
            Tạo mốc mới
          </Button>
        )}
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-1.5">
            <label htmlFor="milestone-title" className="text-xs font-medium text-slate-700 block">
              Tên mốc <span className="text-rose-500">*</span>
            </label>
            <Input
              id="milestone-title"
              placeholder="VD: Thực nghiệm mô hình"
              value={title}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setTitle(e.target.value)}
              required
              className="h-10 text-sm bg-white"
            />
          </div>

          <div className="space-y-1.5">
            <label htmlFor="milestone-deadline" className="text-xs font-medium text-slate-700 block">
              Hạn nộp <span className="text-rose-500">*</span>
            </label>
            <Input
              id="milestone-deadline"
              type="datetime-local"
              value={deadlineLocal}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setDeadlineLocal(e.target.value)}
              required
              className="h-10 text-sm bg-white"
            />
          </div>
        </div>

        <div className="space-y-1.5">
          <label htmlFor="milestone-products" className="text-xs font-medium text-slate-700 block">
            Sản phẩm cần nộp
          </label>
          <textarea
            id="milestone-products"
            placeholder="Báo cáo - mã nguồn / link - ghi chú"
            value={description}
            onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => setDescription(e.target.value)}
            rows={2}
            className="w-full rounded-md border border-input bg-white px-3 py-2 text-sm shadow-xs placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring resize-none"
          />
        </div>

        <div className="flex items-center gap-3 pt-2">
          <Button
            type="submit"
            disabled={isSubmitting || !title.trim() || !deadlineLocal}
            className="bg-primary hover:bg-primary/90 text-white font-medium px-5 h-10 shadow-xs"
          >
            {isSubmitting ? (
              <span className="flex items-center gap-2">
                <span className="size-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                Đang lưu...
              </span>
            ) : (
              <span className="flex items-center gap-1.5">
                <Save className="size-4" />
                Lưu mốc
              </span>
            )}
          </Button>

          {selectedMilestone && onNavigateReview && (
            <Button
              type="button"
              variant="outline"
              onClick={onNavigateReview}
              className="border-slate-300 text-slate-700 hover:bg-slate-50 h-10 px-4"
            >
              <CheckCircle className="size-4 mr-1.5 text-slate-500" />
              Đánh giá sản phẩm
            </Button>
          )}
        </div>
      </form>
    </Card>
  );
}
