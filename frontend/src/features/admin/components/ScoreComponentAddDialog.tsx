import React, { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import type {
  ScoreComponentInput,
  ScoreComponentSource,
  ScoreComponentApplicability,
  ScoreCalculationMethod,
} from '../types/scoreConfig.types';
import {
  COMPONENT_SOURCE_LABELS,
  APPLICABILITY_LABELS,
} from '../types/scoreConfig.types';

export interface ScoreComponentAddDialogProps {
  open: boolean;
  onClose: () => void;
  onAdd: (component: ScoreComponentInput) => void;
  calculationMethod: ScoreCalculationMethod;
  existingCodes: string[];
  nextOrderIndex: number;
}

export const ScoreComponentAddDialog: React.FC<ScoreComponentAddDialogProps> = ({
  open,
  onClose,
  onAdd,
  calculationMethod,
  existingCodes,
  nextOrderIndex,
}) => {
  const [componentCode, setComponentCode] = useState('');
  const [componentName, setComponentName] = useState('');
  const [source, setSource] = useState<ScoreComponentSource>('Custom');
  const [applicability, setApplicability] =
    useState<ScoreComponentApplicability>('Always');
  const [weight, setWeight] = useState<string>('1');
  const [isRequired, setIsRequired] = useState(true);
  const [requiresApproval, setRequiresApproval] = useState(false);
  const [minimumScore, setMinimumScore] = useState<string>('');
  const [error, setError] = useState<string | null>(null);

  const resetForm = () => {
    setComponentCode('');
    setComponentName('');
    setSource('Custom');
    setApplicability('Always');
    setWeight('1');
    setIsRequired(true);
    setRequiresApproval(false);
    setMinimumScore('');
    setError(null);
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const cleanCode = componentCode.trim().toUpperCase();
    const cleanName = componentName.trim();

    if (!cleanCode) {
      setError('Mã thành phần điểm không được để trống.');
      return;
    }

    if (existingCodes.includes(cleanCode)) {
      setError('Mã thành phần điểm này đã tồn tại trong danh sách.');
      return;
    }

    if (!cleanName) {
      setError('Tên thành phần điểm không được để trống.');
      return;
    }

    const weightNum = parseFloat(weight);
    if (isNaN(weightNum) || weightNum < 0) {
      setError('Trọng số/hệ số phải là số không âm.');
      return;
    }

    let minScoreNum: number | null = null;
    if (minimumScore.trim() !== '') {
      const parsed = parseFloat(minimumScore);
      if (isNaN(parsed) || parsed < 0 || parsed > 10) {
        setError('Điểm sàn phải nằm trong khoảng từ 0.0 đến 10.0.');
        return;
      }
      minScoreNum = parsed;
    }

    onAdd({
      componentCode: cleanCode,
      componentName: cleanName,
      source,
      applicability,
      weight: weightNum,
      isRequired,
      minimumScore: minScoreNum,
      requiresApproval,
      orderIndex: nextOrderIndex,
    });

    handleClose();
  };

  const sourceOptions = (
    Object.keys(COMPONENT_SOURCE_LABELS) as ScoreComponentSource[]
  ).map((key) => ({
    value: key,
    label: COMPONENT_SOURCE_LABELS[key],
  }));

  const applicabilityOptions = (
    Object.keys(APPLICABILITY_LABELS) as ScoreComponentApplicability[]
  ).map((key) => ({
    value: key,
    label: APPLICABILITY_LABELS[key],
  }));

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      title="Thêm đầu điểm vào cấu hình"
      className="max-w-md"
    >
      <form onSubmit={handleSubmit} className="p-5 space-y-4 text-xs">
        {error && (
          <div className="p-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-700 font-medium">
            {error}
          </div>
        )}

        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <label className="font-semibold text-slate-700">Mã đầu điểm *</label>
            <Input
              value={componentCode}
              onChange={(e) => setComponentCode(e.target.value.toUpperCase())}
              placeholder="VD: DIEM_DO_AN"
              className="h-9 uppercase font-mono text-xs"
              required
            />
          </div>

          <div className="space-y-1">
            <label className="font-semibold text-slate-700">
              {calculationMethod === 'Percentage' ? 'Tỷ lệ (%) *' : 'Hệ số *'}
            </label>
            <Input
              type="number"
              step={calculationMethod === 'Percentage' ? '1' : '0.5'}
              min="0"
              value={weight}
              onChange={(e) => setWeight(e.target.value)}
              placeholder="1"
              className="h-9 text-xs"
              required
            />
          </div>
        </div>

        <div className="space-y-1">
          <label className="font-semibold text-slate-700">Tên hiển thị *</label>
          <Input
            value={componentName}
            onChange={(e) => setComponentName(e.target.value)}
            placeholder="VD: Điểm đánh giá đồ án tốt nghiệp"
            className="h-9 text-xs"
            required
          />
        </div>

        <div className="space-y-1">
          <label className="font-semibold text-slate-700">Nguồn chấm điểm</label>
          <Select
            options={sourceOptions}
            value={source}
            onChange={(val) => setSource(val as ScoreComponentSource)}
            triggerClassName="h-9 text-xs"
          />
        </div>

        <div className="space-y-1">
          <label className="font-semibold text-slate-700">Điều kiện áp dụng</label>
          <Select
            options={applicabilityOptions}
            value={applicability}
            onChange={(val) => setApplicability(val as ScoreComponentApplicability)}
            triggerClassName="h-9 text-xs"
          />
        </div>

        <div className="space-y-1">
          <label className="font-semibold text-slate-700">Điểm sàn (nếu có)</label>
          <Input
            type="number"
            step="0.1"
            min="0"
            max="10"
            value={minimumScore}
            onChange={(e) => setMinimumScore(e.target.value)}
            placeholder="Để trống nếu không áp dụng điểm sàn"
            className="h-9 text-xs"
          />
        </div>

        <div className="pt-2 border-t border-slate-100 space-y-2">
          <Checkbox
            id="isRequired"
            checked={isRequired}
            onCheckedChange={(checked) => setIsRequired(Boolean(checked))}
            label="Bắt buộc phải có điểm để tính kết quả"
          />

          <Checkbox
            id="requiresApproval"
            checked={requiresApproval}
            onCheckedChange={(checked) => setRequiresApproval(Boolean(checked))}
            label="Yêu cầu phê duyệt trước khi công bố"
          />
        </div>

        <div className="pt-3 border-t border-slate-100 flex items-center justify-end gap-2">
          <Button type="button" variant="outline" size="sm" onClick={handleClose}>
            Hủy
          </Button>
          <Button
            type="submit"
            size="sm"
            className="bg-primary text-white hover:bg-primary/90"
          >
            Thêm vào bảng
          </Button>
        </div>
      </form>
    </Dialog>
  );
};