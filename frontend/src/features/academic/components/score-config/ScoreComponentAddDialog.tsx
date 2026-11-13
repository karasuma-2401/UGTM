import React, { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { FormErrorSummary, type FormFieldError } from '@/components/ui/form-error-summary';
import type {
  ScoreComponentInput,
  ScoreComponentSource,
  ScoreComponentApplicability,
  ScoreCalculationMethod,
} from '../../types/scoreConfig.types';
import { COMPONENT_SOURCE_LABELS, APPLICABILITY_LABELS } from '../../types/scoreConfig.types';

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
  const [applicability, setApplicability] = useState<ScoreComponentApplicability>('Always');
  const [weight, setWeight] = useState<string>('1');
  const [isRequired, setIsRequired] = useState(true);
  const [requiresApproval, setRequiresApproval] = useState(false);
  const [minimumScore, setMinimumScore] = useState<string>('');
  const [errors, setErrors] = useState<FormFieldError[]>([]);
  const [submissionKey, setSubmissionKey] = useState(0);

  const resetForm = () => {
    setComponentCode('');
    setComponentName('');
    setSource('Custom');
    setApplicability('Always');
    setWeight('1');
    setIsRequired(true);
    setRequiresApproval(false);
    setMinimumScore('');
    setErrors([]);
    setSubmissionKey(0);
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const cleanCode = componentCode.trim().toUpperCase();
    const cleanName = componentName.trim();

    const nextErrors: FormFieldError[] = [];
    if (!cleanCode)
      nextErrors.push({
        fieldId: 'score-component-code',
        message: 'Mã thành phần điểm không được để trống.',
      });
    else if (existingCodes.includes(cleanCode))
      nextErrors.push({
        fieldId: 'score-component-code',
        message: 'Mã thành phần điểm này đã tồn tại trong danh sách.',
      });
    if (!cleanName)
      nextErrors.push({
        fieldId: 'score-component-name',
        message: 'Tên thành phần điểm không được để trống.',
      });
    const weightNum = parseFloat(weight);
    if (!Number.isFinite(weightNum) || weightNum < 0)
      nextErrors.push({
        fieldId: 'score-component-weight',
        message: 'Trọng số/hệ số phải là số không âm.',
      });
    else if (calculationMethod === 'IntegerCoefficient' && !Number.isInteger(weightNum))
      nextErrors.push({
        fieldId: 'score-component-weight',
        message: 'Hệ số phải là số nguyên không âm.',
      });
    else if (calculationMethod === 'Percentage' && weightNum > 100)
      nextErrors.push({
        fieldId: 'score-component-weight',
        message: 'Tỷ lệ không được vượt quá 100%.',
      });

    let minScoreNum: number | null = null;
    if (minimumScore.trim() !== '') {
      const parsed = parseFloat(minimumScore);
      if (isNaN(parsed) || parsed < 0 || parsed > 10) {
        nextErrors.push({
          fieldId: 'score-component-minimum',
          message: 'Điểm sàn phải nằm trong khoảng từ 0.0 đến 10.0.',
        });
      }
      minScoreNum = parsed;
    }
    setErrors(nextErrors);
    setSubmissionKey((previous) => previous + 1);
    if (nextErrors.length) return;

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

  const sourceOptions = (Object.keys(COMPONENT_SOURCE_LABELS) as ScoreComponentSource[]).map(
    (key) => ({
      value: key,
      label: COMPONENT_SOURCE_LABELS[key],
    }),
  );

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
      className="max-w-lg"
      headerClassName="border-b border-slate-200/80 bg-white px-5 py-4 sm:px-6"
    >
      <form
        onSubmit={handleSubmit}
        noValidate
        className="text-sm"
        onChange={(event) => {
          const fieldId = (event.target as HTMLElement).id;
          if (errors.some((entry) => entry.fieldId === fieldId))
            setErrors((previous) => previous.filter((entry) => entry.fieldId !== fieldId));
        }}
      >
        <div className="p-5 sm:p-6 space-y-4">
          <p className="text-sm leading-relaxed text-slate-600">
            Thiết lập thành phần điểm và điều kiện áp dụng cho đợt đã chọn.
          </p>
          <FormErrorSummary errors={errors} submissionKey={submissionKey} />

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-1">
              <label htmlFor="score-component-code" className="font-semibold text-slate-700">
                Mã đầu điểm *
              </label>
              <Input
                id="score-component-code"
                error={errors.find((entry) => entry.fieldId === 'score-component-code')?.message}
                value={componentCode}
                onChange={(e) => setComponentCode(e.target.value.toUpperCase())}
                placeholder="VD: DIEM_DO_AN"
                className="h-10 rounded-xl uppercase font-mono text-sm"
                required
              />
            </div>

            <div className="space-y-1">
              <label htmlFor="score-component-weight" className="font-semibold text-slate-700">
                {calculationMethod === 'Percentage' ? 'Tỷ lệ (%) *' : 'Hệ số *'}
              </label>
              <Input
                id="score-component-weight"
                error={errors.find((entry) => entry.fieldId === 'score-component-weight')?.message}
                type="number"
                step={calculationMethod === 'Percentage' ? '0.01' : '1'}
                min="0"
                value={weight}
                onChange={(e) => setWeight(e.target.value)}
                placeholder="1"
                className="h-10 rounded-xl text-sm"
                required
              />
            </div>
          </div>

          <div className="space-y-1">
            <label htmlFor="score-component-name" className="font-semibold text-slate-700">
              Tên hiển thị *
            </label>
            <Input
              id="score-component-name"
              error={errors.find((entry) => entry.fieldId === 'score-component-name')?.message}
              value={componentName}
              onChange={(e) => setComponentName(e.target.value)}
              placeholder="VD: Điểm đánh giá đồ án tốt nghiệp"
              className="h-10 rounded-xl text-sm"
              required
            />
          </div>

          <div className="space-y-1">
            <p className="font-semibold text-slate-700">Nguồn chấm điểm</p>
            <Select
              aria-label="Nguồn chấm điểm"
              options={sourceOptions}
              value={source}
              onChange={(val) => setSource(val as ScoreComponentSource)}
              triggerClassName="h-10 rounded-xl text-sm"
            />
          </div>

          <div className="space-y-1">
            <p className="font-semibold text-slate-700">Điều kiện áp dụng</p>
            <Select
              aria-label="Điều kiện áp dụng"
              options={applicabilityOptions}
              value={applicability}
              onChange={(val) => setApplicability(val as ScoreComponentApplicability)}
              triggerClassName="h-10 rounded-xl text-sm"
            />
          </div>

          <div className="space-y-1">
            <label htmlFor="score-component-minimum" className="font-semibold text-slate-700">
              Điểm sàn (nếu có)
            </label>
            <Input
              id="score-component-minimum"
              error={errors.find((entry) => entry.fieldId === 'score-component-minimum')?.message}
              type="number"
              step="0.1"
              min="0"
              max="10"
              value={minimumScore}
              onChange={(e) => setMinimumScore(e.target.value)}
              placeholder="Để trống nếu không áp dụng điểm sàn"
              className="h-10 rounded-xl text-sm"
            />
          </div>

          <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-4 space-y-3">
            <p className="font-semibold text-slate-800">Quy tắc áp dụng</p>
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
        </div>

        <div className="sticky bottom-0 z-10 border-t border-slate-200/80 bg-white/95 backdrop-blur-md px-5 py-3.5 flex flex-wrap items-center justify-end gap-2.5 sm:px-6">
          <Button type="button" variant="outline" onClick={handleClose} className="h-9 rounded-xl px-4 text-xs font-semibold">
            Hủy
          </Button>
          <Button type="submit" className="h-9 rounded-xl px-4 text-xs font-semibold">
            Thêm vào bảng
          </Button>
        </div>
      </form>
    </Dialog>
  );
};
