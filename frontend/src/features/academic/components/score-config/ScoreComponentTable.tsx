import React from 'react';
import { Trash2, ShieldAlert, Plus, Minus, Calculator } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableHeader,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
} from '@/components/ui/table';
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
      className="score-components bg-white border border-slate-200/80 rounded-2xl shadow-xs overflow-hidden"
    >
      <div className="p-5 sm:px-6 border-b border-slate-100 flex flex-wrap gap-3 items-center justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h2 className="text-base font-semibold tracking-tight text-slate-900">
              Thành phần điểm{' '}
              <span className="ml-0.5 text-sm font-normal text-slate-500">
                ({components.length})
              </span>
            </h2>
            <span
              role="status"
              className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
                isDirty
                  ? 'bg-amber-50 text-amber-800 ring-1 ring-inset ring-amber-200/80'
                  : 'bg-slate-100 text-slate-600'
              }`}
            >
              {isReadOnly
                ? 'Đang tải cấu hình'
                : isDirty
                  ? 'Có thay đổi chưa lưu'
                  : hasSavedConfig
                    ? 'Đã lưu'
                    : 'Chưa có cấu hình đã lưu'}
            </span>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Danh sách các thành phần điểm và điều kiện áp dụng cho từng nguồn chấm.
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
            className="h-9 rounded-xl px-3.5 text-xs font-semibold"
          >
            Thêm đầu điểm
          </Button>
        )}
      </div>

      {!isReadOnly && !isValid && (
        <div
          role="status"
          className="border-b border-amber-200 bg-amber-50 px-5 py-3 text-sm text-amber-900 sm:px-6"
        >
          {components.length === 0
            ? 'Hãy thêm ít nhất một thành phần điểm.'
            : calculationMethod === 'Percentage'
              ? `Tổng trọng số hiện tại là ${totalWeight}%; cần bằng 100%.`
              : `Tổng hệ số hiện tại là ${totalWeight}; cần lớn hơn 0.`}
        </div>
      )}

      <div className="min-h-[220px]">
        <Table>
          <TableHeader>
            <tr>
              <TableHead className="w-[24%] px-5 py-3.5">Nguồn / thành viên</TableHead>
              <TableHead className="w-[36%] px-5 py-3.5">Điểm / trọng số</TableHead>
              <TableHead className="w-[24%] px-5 py-3.5">Điều kiện</TableHead>
              <TableHead align="right" className="w-[16%] px-5 py-3.5 whitespace-nowrap">
                Phân loại
              </TableHead>
            </tr>
          </TableHeader>

          <TableBody>
            {components.length === 0 ? (
              <tr>
                <TableCell colSpan={4} className="px-5 py-12 text-center text-slate-400 text-sm">
                  Chưa có đầu điểm nào trong cấu hình. Bấm "Thêm đầu điểm" để bắt đầu.
                </TableCell>
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
                  <TableRow
                    key={item.componentCode || index}
                    hideSelectionMarker
                    className="hover:bg-slate-50/70 transition-colors"
                  >
                    {/* Cột 1: Nguồn / thành viên */}
                    <TableCell className="px-5 py-4">
                      <div className="font-semibold text-slate-900 text-sm">
                        {item.componentName}
                      </div>
                    </TableCell>

                    {/* Cột 2: Điểm / Trọng số */}
                    <TableCell className="px-5 py-4 tabular-nums">
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
                              <span className="text-xs text-slate-400 font-medium">
                                (≈ {percentEquiv}%)
                              </span>
                            )}
                          </div>
                        ) : (
                          <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5 md:flex-nowrap md:whitespace-nowrap">
                            <div className="flex items-center gap-2 whitespace-nowrap">
                              <div className="inline-flex shrink-0 items-center rounded-xl border border-slate-200 bg-white focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/15">
                                <button
                                  type="button"
                                  aria-label={`Giảm trọng số ${item.componentName}`}
                                  title={`Giảm ${weightStep}`}
                                  disabled={item.weight <= 0}
                                  onClick={() => handleStepWeight(index, -1)}
                                  className="flex size-11 md:size-9 shrink-0 items-center justify-center rounded-l-xl bg-slate-50/80 text-slate-600 transition-colors hover:bg-blue-50 hover:text-primary active:bg-blue-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary disabled:cursor-not-allowed disabled:text-slate-300 disabled:hover:bg-slate-50"
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
                                  className="flex size-11 md:size-9 shrink-0 items-center justify-center rounded-r-xl bg-slate-50/80 text-slate-600 transition-colors hover:bg-blue-50 hover:text-primary active:bg-blue-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary disabled:cursor-not-allowed disabled:text-slate-300 disabled:hover:bg-slate-50"
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
                    </TableCell>

                    {/* Cột 3: Điều kiện */}
                    <TableCell className="px-5 py-4 text-xs leading-relaxed text-slate-600">
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
                    </TableCell>

                    {/* Cột 4: Phân loại & thao tác */}
                    <TableCell align="right" className="px-5 py-4 whitespace-nowrap">
                      <div className="inline-flex items-center justify-end gap-2 shrink-0">
                        {getCategoryBadge(item)}

                        {isCustom && !isReadOnly && (
                          <button
                            type="button"
                            onClick={() => handleRemoveComponent(index)}
                            title="Xóa đầu điểm tùy biến"
                            aria-label={`Xóa ${item.componentName}`}
                            className="p-2 rounded-lg text-slate-500 hover:text-rose-600 hover:bg-rose-50 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                          >
                            <Trash2 className="size-4" />
                          </button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </div>

      <div className="relative overflow-hidden border-t border-slate-200/80 bg-gradient-to-br from-slate-50 via-blue-50/40 to-amber-50/35 p-5 sm:px-6">
        <div className="pointer-events-none absolute inset-x-0 top-0 h-[3px] bg-gradient-to-r from-blue-500 via-indigo-500 to-amber-500 opacity-85" />
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2.5">
            <span className="inline-flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-br from-blue-600 to-indigo-600 text-white shadow-sm ring-1 ring-blue-500/20">
              <Calculator className="size-4" />
            </span>
            <div>
              <h3 className="text-sm font-semibold text-slate-900">Xem trước cách tính</h3>
              <p className="text-xs text-slate-500">
                Công thức tổng hợp điểm theo trọng số hiện hành
              </p>
            </div>
          </div>
          <span className="rounded-full border border-blue-200/80 bg-white/90 px-3 py-1 text-xs font-semibold text-blue-700 shadow-xs tabular-nums">
            Tổng trọng số: {totalWeight}
            {calculationMethod === 'Percentage' ? '%' : ''}
          </span>
        </div>

        {components.length > 0 && totalWeight > 0 ? (
          <div
            className={`mt-3.5 rounded-xl border border-slate-200/90 bg-white/95 px-3 py-2.5 shadow-xs lg:px-2 ${
              calculationMethod === 'Percentage' ? '' : 'min-[1440px]:px-2.5 2xl:px-3.5'
            }`}
          >
            <div
              className={`flex flex-wrap items-center gap-x-1 gap-y-1.5 text-xs text-slate-700 lg:gap-x-0.5 lg:text-[10.5px] ${
                calculationMethod === 'Percentage'
                  ? ''
                  : 'min-[1440px]:text-[11px] 2xl:gap-x-1 2xl:text-[11.5px]'
              }`}
            >
              {components.map((item, index) => {
                const isLast = index === components.length - 1;
                return (
                  <span
                    key={item.componentCode || index}
                    className={`inline-flex max-w-full items-center gap-1 lg:gap-0.5 ${
                      calculationMethod === 'Percentage' ? '' : '2xl:gap-1'
                    }`}
                  >
                    {index === 0 ? (
                      <span className="font-semibold text-slate-400">(</span>
                    ) : (
                      <span className="font-semibold text-slate-400">+</span>
                    )}
                    <span
                      className={`inline-flex min-w-0 max-w-full items-center gap-0.5 whitespace-nowrap rounded-md border border-blue-100/90 bg-gradient-to-r from-blue-50/80 to-indigo-50/55 px-1.5 py-0.5 lg:px-1 ${
                        calculationMethod === 'Percentage' ? '' : 'min-[1440px]:px-1.5'
                      }`}
                    >
                      <span className="truncate font-medium text-slate-900">
                        {item.componentName}
                      </span>
                      <span className="text-[10.5px] text-slate-400">×</span>
                      <span className="font-semibold text-blue-700 tabular-nums">
                        {item.weight}
                        {calculationMethod === 'Percentage' ? '%' : ''}
                      </span>
                    </span>
                    {isLast && <span className="font-semibold text-slate-400">)</span>}
                  </span>
                );
              })}
              <span
                className={`inline-flex items-center gap-1 whitespace-nowrap lg:gap-0.5 ${
                  calculationMethod === 'Percentage' ? '' : '2xl:gap-1'
                }`}
              >
                <span className="font-semibold text-slate-400">÷</span>
                <span className="inline-flex items-center rounded-md border border-amber-200/80 bg-gradient-to-r from-amber-50 to-orange-50/70 px-1.5 py-0.5 font-semibold text-amber-800 tabular-nums">
                  {totalWeight}
                  {calculationMethod === 'Percentage' ? '%' : ''}
                </span>
              </span>
            </div>
          </div>
        ) : (
          <div className="mt-3.5 rounded-xl border border-dashed border-slate-200 bg-white/80 px-4 py-3 text-sm text-slate-600">
            {components.length === 0
              ? 'Thêm thành phần điểm để xem cách tính.'
              : 'Tổng trọng số phải lớn hơn 0 để xem cách tính.'}
          </div>
        )}

        <p className="mt-2.5 text-xs leading-relaxed text-slate-500">
          Minh họa khi tất cả thành phần trên đều áp dụng. Khi một nguồn không áp dụng, điểm và
          trọng số của nguồn đó được loại khỏi phép tính.
        </p>
      </div>
    </section>
  );
};
