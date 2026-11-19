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
  onAdd?: (component: ScoreComponentInput) => void;
  onSave?: (component: ScoreComponentInput) => void;
  initialComponent?: ScoreComponentInput | null;
  calculationMethod: ScoreCalculationMethod;
  existingCodes: string[];
  nextOrderIndex: number;
}

interface ScoreComponentDialogFormProps {
  onClose: () => void;
  onSubmitComponent: (component: ScoreComponentInput) => void;
  initialComponent: ScoreComponentInput | null;
  calculationMethod: ScoreCalculationMethod;
  existingCodes: string[];
  nextOrderIndex: number;
}

const ScoreComponentDialogForm: React.FC<ScoreComponentDialogFormProps> = ({
  onClose,
  onSubmitComponent,
  initialComponent,
  calculationMethod,
  existingCodes,
  nextOrderIndex,
}) => {
  const isEditMode = Boolean(initialComponent);

  const [componentCode, setComponentCode] = useState(initialComponent?.componentCode ?? '');
  const [componentName, setComponentName] = useState(initialComponent?.componentName ?? '');
  const [source, setSource] = useState<ScoreComponentSource>(initialComponent?.source ?? 'Custom');
  const [applicability, setApplicability] = useState<ScoreComponentApplicability>(
    initialComponent?.applicability ?? 'Always',
  );
  const [weight, setWeight] = useState<string>(
    initialComponent !== null ? String(initialComponent.weight) : '1',
  );
  const [isRequired, setIsRequired] = useState(initialComponent?.isRequired ?? true);
  const [requiresApproval, setRequiresApproval] = useState(
    initialComponent?.requiresApproval ?? false,
  );
  const [minimumScore, setMinimumScore] = useState<string>(
    initialComponent?.minimumScore !== null && initialComponent?.minimumScore !== undefined
      ? String(initialComponent.minimumScore)
      : '',
  );
  const [errors, setErrors] = useState<FormFieldError[]>([]);
  const [submissionKey, setSubmissionKey] = useState(0);

  const normalizedOriginalCode = initialComponent?.componentCode.trim().toUpperCase() ?? null;
  const normalizedExistingCodes = existingCodes.map((code) => code.trim().toUpperCase());

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
    else if (
      cleanCode !== normalizedOriginalCode &&
      normalizedExistingCodes.includes(cleanCode)
    )
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

    onSubmitComponent({
      componentCode: cleanCode,
      componentName: cleanName,
      source,
      applicability,
      weight: weightNum,
      isRequired,
      minimumScore: minScoreNum,
      requiresApproval,
      orderIndex: initialComponent ? initialComponent.orderIndex : nextOrderIndex,
    });

    onClose();
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
        <p className="text-sm leading-relaxed text-ink-body">
          {isEditMode
            ? 'Cập nhật thông tin thành phần điểm và điều kiện áp dụng cho đợt đã chọn.'
            : 'Thiết lập thành phần điểm và điều kiện áp dụng cho đợt đã chọn.'}
        </p>
        <FormErrorSummary errors={errors} submissionKey={submissionKey} />

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="space-y-1">
            <label htmlFor="score-component-code" className="font-semibold text-ink-secondary">
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
            <label htmlFor="score-component-weight" className="font-semibold text-ink-secondary">
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
          <label htmlFor="score-component-name" className="font-semibold text-ink-secondary">
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
          <p className="font-semibold text-ink-secondary">Nguồn chấm điểm</p>
          <Select
            aria-label="Nguồn chấm điểm"
            options={sourceOptions}
            value={source}
            onChange={(val) => setSource(val as ScoreComponentSource)}
            triggerClassName="h-10 rounded-xl text-sm"
          />
        </div>

        <div className="space-y-1">
          <p className="font-semibold text-ink-secondary">Điều kiện áp dụng</p>
          <Select
            aria-label="Điều kiện áp dụng"
            options={applicabilityOptions}
            value={applicability}
            onChange={(val) => setApplicability(val as ScoreComponentApplicability)}
            triggerClassName="h-10 rounded-xl text-sm"
          />
        </div>

        <div className="space-y-1">
          <label htmlFor="score-component-minimum" className="font-semibold text-ink-secondary">
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

        <div className="rounded-xl border border-line/80 bg-surface-subtle/70 p-4 space-y-3">
          <p className="font-semibold text-ink-heading">Quy tắc áp dụng</p>
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

      <div className="sticky bottom-0 z-10 border-t border-line/80 bg-surface-card/95 backdrop-blur-md px-5 py-3.5 flex flex-wrap items-center justify-end gap-2.5 sm:px-6">
        <Button
          type="button"
          variant="outline"
          onClick={onClose}
          className="h-9 rounded-xl px-4 text-xs font-semibold"
        >
          Hủy
        </Button>
        <Button type="submit" className="h-9 rounded-xl px-4 text-xs font-semibold">
          {isEditMode ? 'Lưu thay đổi' : 'Thêm vào bảng'}
        </Button>
      </div>
    </form>
  );
};

export const ScoreComponentAddDialog: React.FC<ScoreComponentAddDialogProps> = ({
  open,
  onClose,
  onAdd,
  onSave,
  initialComponent = null,
  calculationMethod,
  existingCodes,
  nextOrderIndex,
}) => {
  const isEditMode = Boolean(initialComponent);
  const submitHandler = onSave ?? onAdd ?? (() => {});
  const formKey = initialComponent
    ? `edit-${initialComponent.componentCode}-${initialComponent.orderIndex}`
    : 'add';

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEditMode ? 'Chỉnh sửa đầu điểm' : 'Thêm đầu điểm vào cấu hình'}
      className="max-w-lg"
      headerClassName="border-b border-line/80 bg-surface-card px-5 py-4 sm:px-6"
    >
      {open && (
        <ScoreComponentDialogForm
          key={formKey}
          onClose={onClose}
          onSubmitComponent={submitHandler}
          initialComponent={initialComponent}
          calculationMethod={calculationMethod}
          existingCodes={existingCodes}
          nextOrderIndex={nextOrderIndex}
        />
      )}
    </Dialog>
  );
};
