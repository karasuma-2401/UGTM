import React from 'react';
import { Trash2, ShieldAlert, Plus, Minus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import './score-config.css';
import type { ScoreComponentInput, ScoreCalculationMethod } from '../../types/scoreConfig.types';
import { Badge } from '@/components/ui/badge';

export interface ScoreComponentTableProps {
  components: ScoreComponentInput[];
  calculationMethod: ScoreCalculationMethod;
  onChangeComponents: (components: ScoreComponentInput[]) => void;
  isReadOnly?: boolean;
  onAddComponent?: () => void;
  isDirty?: boolean;
  hasSavedConfig?: boolean;
  isValid?: boolean;
}

export const ScoreComponentTable: React.FC<ScoreComponentTableProps> = ({
  components,
  calculationMethod,
  onChangeComponents,
  isReadOnly = false,
  onAddComponent,
  isDirty = false,
  hasSavedConfig = false,
  isValid = true,
}) => {
  const totalWeight =
    Math.round(components.reduce((sum, c) => sum + (Number(c.weight) || 0), 0) * 100) / 100;
  const weightStep = calculationMethod === 'Percentage' ? 0.01 : 1;
  const maximumWeight = calculationMethod === 'Percentage' ? 100 : 99;

  const handleWeightChange = (index: number, newWeightStr: string) => {
    const val = parseFloat(newWeightStr);
    const updated = components.map((c, i) => {
      if (i !== index) return c;
      return {
        ...c,
        weight: isNaN(val) ? 0 : val,
      };
    });
    onChangeComponents(updated);
  };

  const handleRemoveComponent = (index: number) => {
    const updated = components.filter((_, i) => i !== index);
    const reindexed = updated.map((c, i) => ({
      ...c,
      orderIndex: i + 1,
    }));
    onChangeComponents(reindexed);
  };

  const handleStepWeight = (index: number, direction: -1 | 1) => {
    const currentWeight = Number(components[index].weight) || 0;
    const nextWeight = Math.min(
      maximumWeight,
      Math.max(0, Math.round((currentWeight + direction * weightStep) * 100) / 100),
    );
    handleWeightChange(index, String(nextWeight));
  };

  const getConditionText = (item: ScoreComponentInput): string => {
    const parts: string[] = [];
    if (item.minimumScore !== null) {
      parts.push(`Đạt sàn ≥ ${item.minimumScore}`);
    }
    if (item.requiresApproval) {
      parts.push('Cần duyệt');
    }
    if (item.applicability === 'ReviewerOutsideCouncil') {
      parts.push('GVPB kiêm UV không chấm');
    } else if (item.applicability === 'ReviewerInCouncil') {
      parts.push('GVPB trong HĐ');
    }

    if (parts.length > 0) {
      return parts.join(' + ');
    }
    return item.isRequired ? 'Bắt buộc khi áp dụng' : 'Tùy chọn';
  };

  const getCategoryBadge = (item: ScoreComponentInput) => {
    if (item.source === 'Advisor') {
      return (
        <Badge
          variant="info"
          className="whitespace-nowrap shrink-0 px-3 py-1 font-medium text-xs shadow-2xs"
        >
          Nguồn riêng
        </Badge>
      );
    }
    if (item.source === 'Reviewer') {
      return (
        <Badge
          variant="warning"
          className="whitespace-nowrap shrink-0 px-3 py-1 font-medium text-xs shadow-2xs"
        >
          Bắt buộc
        </Badge>
      );
    }
    if (item.source.startsWith('Council')) {
      return (
        <Badge
          variant="secondary"
          className="whitespace-nowrap shrink-0 px-3 py-1 font-medium text-xs shadow-2xs"
        >
          Điểm HĐ
        </Badge>
      );
    }
    return (
      <Badge
        variant="default"
        className="whitespace-nowrap shrink-0 px-3 py-1 font-medium text-xs shadow-2xs"
      >
        Tùy biến
      </Badge>
    );
  };

  return (
    <section
      aria-label="Thành phần điểm"
      className="score-components bg-white border border-slate-200 rounded-2xl shadow-sm overflow-hidden"
    >
      <div className="p-5 sm:px-6 border-b border-slate-100 flex flex-wrap gap-3 items-center justify-between">
        <div>
          <h2 className="text-base font-semibold text-slate-900">
            Thành phần điểm{' '}
            <span className="ml-1 text-sm font-normal text-slate-500">({components.length})</span>
          </h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Danh sách các thành phần điểm và điều kiện áp dụng cho từng nguồn chấm.
          </p>
          <p role="status" className="mt-2 text-xs font-medium text-slate-600">
            {isReadOnly
              ? 'Đang tải cấu hình'
              : isDirty
                ? 'Có thay đổi chưa lưu'
                : hasSavedConfig
                  ? 'Đã lưu'
                  : 'Chưa có cấu hình đã lưu'}
          </p>
        </div>
        {onAddComponent && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onAddComponent}
            disabled={isReadOnly}
            leftIcon={<Plus className="size-4" />}
          >
            Thêm đầu điểm
          </Button>
        )}
      </div>

      {!isReadOnly && !isValid && (
        <div
          role="status"
          className="border-b border-amber-200 bg-amber-50 px-5 py-3 text-sm text-amber-900"
        >
          {components.length === 0
            ? 'Hãy thêm ít nhất một thành phần điểm.'
            : calculationMethod === 'Percentage'
              ? `Tổng trọng số hiện tại là ${totalWeight}%; cần bằng 100%.`
              : `Tổng hệ số hiện tại là ${totalWeight}; cần lớn hơn 0.`}
        </div>
      )}

      <div className="overflow-x-auto min-h-[220px]">
        <table className="w-full text-left text-sm border-collapse">
          <thead>
            <tr className="border-b border-slate-100 bg-slate-50/60 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
              <th scope="col" className="px-5 py-3.5 w-[22%]">
                Nguồn / thành viên
              </th>
              <th scope="col" className="px-5 py-3.5 w-[36%]">
                Điểm / trọng số
              </th>
              <th scope="col" className="px-5 py-3.5 w-[25%]">
                Điều kiện
              </th>
              <th scope="col" className="px-5 py-3.5 w-[17%] text-right whitespace-nowrap">
                Phân loại
              </th>
            </tr>
          </thead>

          <tbody className="divide-y divide-slate-100">
            {components.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-5 py-12 text-center text-slate-400 text-sm">
                  Chưa có đầu điểm nào trong cấu hình. Bấm "Thêm đầu điểm" để bắt đầu.
                </td>
              </tr>
            ) : (
              components.map((item, index) => {
                const isCustom =
                  item.source === 'Custom' || item.componentCode.startsWith('CUSTOM');

                // Tính tỷ lệ phần trăm quy đổi động
                const percentEquiv =
                  totalWeight > 0
                    ? Math.round((Number(item.weight) / totalWeight) * 100 * 100) / 100
                    : 0;

                return (
                  <tr
                    key={item.componentCode || index}
                    className="hover:bg-slate-50/50 transition-colors"
                  >
                    {/* Cột 1: Nguồn / thành viên */}
                    <td data-label="Nguồn / thành viên" className="px-5 py-4">
                      <div className="font-semibold text-slate-800 text-sm">
                        {item.componentName}
                      </div>
                    </td>

                    {/* Cột 2: Điểm / Trọng số */}
                    <td data-label="Trọng số" className="px-5 py-4 tabular-nums">
                      <div className="flex items-center gap-2 flex-wrap">
                        {isReadOnly ? (
                          <div className="flex items-baseline gap-1.5">
                            <span className="font-semibold text-slate-800 text-sm">
                              {item.weight}
                            </span>
                            <span className="text-xs text-slate-500 font-medium">
                              {calculationMethod === 'Percentage' ? '%' : 'hệ số'}
                            </span>
                            {calculationMethod === 'IntegerCoefficient' && (
                              <span className="text-[11px] text-slate-400 font-medium">
                                (≈ {percentEquiv}%)
                              </span>
                            )}
                          </div>
                        ) : (
                          <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5 md:flex-nowrap md:whitespace-nowrap">
                            <div className="flex items-center gap-2 whitespace-nowrap">
                              <div className="inline-flex shrink-0 items-center rounded-lg border border-slate-200 bg-white focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/15">
                                <button
                                  type="button"
                                  aria-label={`Giảm trọng số ${item.componentName}`}
                                  title={`Giảm ${weightStep}`}
                                  disabled={item.weight <= 0}
                                  onClick={() => handleStepWeight(index, -1)}
                                  className="flex size-11 md:size-9 shrink-0 items-center justify-center rounded-l-lg bg-slate-50 text-slate-600 transition-colors hover:bg-blue-50 hover:text-primary active:bg-blue-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary disabled:cursor-not-allowed disabled:text-slate-300 disabled:hover:bg-slate-50"
                                >
                                  <Minus className="size-4" aria-hidden="true" />
                                </button>
                                <input
                                  type="number"
                                  inputMode={
                                    calculationMethod === 'Percentage' ? 'decimal' : 'numeric'
                                  }
                                  aria-label={`Trọng số ${item.componentName}`}
                                  step={weightStep}
                                  min="0"
                                  max={maximumWeight}
                                  value={item.weight === 0 ? '' : item.weight}
                                  onChange={(e) => handleWeightChange(index, e.target.value)}
                                  placeholder="0"
                                  className="score-weight-input w-14 h-11 md:w-12 md:h-9 min-w-0 border-x border-slate-200 px-1 text-base sm:text-sm font-semibold tabular-nums text-center text-slate-900 bg-white caret-blue-600 focus:outline-none"
                                />
                                <button
                                  type="button"
                                  aria-label={`Tăng trọng số ${item.componentName}`}
                                  title={`Tăng ${weightStep}`}
                                  disabled={item.weight >= maximumWeight}
                                  onClick={() => handleStepWeight(index, 1)}
                                  className="flex size-11 md:size-9 shrink-0 items-center justify-center rounded-r-lg bg-slate-50 text-slate-600 transition-colors hover:bg-blue-50 hover:text-primary active:bg-blue-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary disabled:cursor-not-allowed disabled:text-slate-300 disabled:hover:bg-slate-50"
                                >
                                  <Plus className="size-4" aria-hidden="true" />
                                </button>
                              </div>
                              <span className="text-xs text-slate-500 font-medium whitespace-nowrap">
                                {calculationMethod === 'Percentage' ? '%' : 'hệ số'}
                              </span>
                            </div>
                            {calculationMethod === 'IntegerCoefficient' && (
                              <span className="text-xs text-slate-500 whitespace-nowrap">
                                <span className="sr-only">Tỷ lệ tham khảo: </span>(≈ {percentEquiv}
                                %)
                              </span>
                            )}
                          </div>
                        )}
                      </div>
                    </td>

                    {/* Cột 3: Điều kiện */}
                    <td
                      data-label="Điều kiện"
                      className="px-5 py-4 text-xs leading-relaxed text-slate-600"
                    >
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <span>{getConditionText(item)}</span>
                        {item.minimumScore !== null && (
                          <span
                            title="Có áp dụng điểm sàn"
                            className="text-amber-500 inline-flex items-center"
                          >
                            <ShieldAlert className="size-3.5" />
                          </span>
                        )}
                      </div>
                    </td>

                    {/* Cột 4: Phân loại & thao tác (Tăng width và không để wrap chữ) */}
                    <td data-label="Phân loại" className="px-5 py-4 text-right whitespace-nowrap">
                      <div className="inline-flex items-center justify-end gap-2.5 shrink-0">
                        {getCategoryBadge(item)}

                        {isCustom && !isReadOnly && (
                          <button
                            type="button"
                            onClick={() => handleRemoveComponent(index)}
                            title="Xóa đầu điểm tùy biến"
                            aria-label={`Xóa ${item.componentName}`}
                            className="p-2 rounded text-slate-500 hover:text-rose-600 hover:bg-rose-50 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                          >
                            <Trash2 className="size-4" />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      <div className="relative overflow-hidden p-5 sm:px-6 border-t border-slate-200 bg-gradient-to-br from-blue-100/80 via-blue-50/70 to-orange-100">
        <div
          aria-hidden="true"
          className="absolute inset-x-0 top-0 h-1 bg-[image:var(--gradient-brand)]"
        />
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h3 className="text-sm font-semibold text-slate-900">Xem trước cách tính</h3>
          <span className="rounded-full border border-blue-100 bg-white px-3 py-1 text-xs font-semibold text-slate-700 tabular-nums">
            Tổng trọng số: {totalWeight}
            {calculationMethod === 'Percentage' ? '%' : ''}
          </span>
        </div>
        {components.length > 0 && totalWeight > 0 ? (
          <p className="mt-3 rounded-xl border border-slate-200/80 bg-white/90 px-4 py-3 text-sm leading-7 text-slate-700 break-words">
            ({' '}
            {components.map((item, index) => (
              <React.Fragment key={item.componentCode || index}>
                {index > 0 && <span className="text-slate-400"> + </span>}
                <span className="inline-block max-w-full break-words">
                  <span className="font-medium text-slate-900">{item.componentName}</span> ×{' '}
                  {item.weight}
                  {calculationMethod === 'Percentage' ? '%' : ''}
                </span>
              </React.Fragment>
            ))}{' '}
            ) ÷ {totalWeight}
            {calculationMethod === 'Percentage' ? '%' : ''}
          </p>
        ) : (
          <p className="mt-3 text-sm text-slate-600">
            {components.length === 0
              ? 'Thêm thành phần điểm để xem cách tính.'
              : 'Tổng trọng số phải lớn hơn 0 để xem cách tính.'}
          </p>
        )}
        <p className="mt-2 text-xs leading-relaxed text-slate-600">
          Minh họa khi tất cả thành phần trên đều áp dụng. Khi một nguồn không áp dụng, điểm và
          trọng số của nguồn đó được loại khỏi phép tính.
        </p>
      </div>
    </section>
  );
};
