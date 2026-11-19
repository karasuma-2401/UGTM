import React from 'react';
import { Pencil, Trash2, ShieldAlert, Plus, Minus, Calculator } from 'lucide-react';
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
import type {
  ScoreComponentInput,
  ScoreCalculationMethod,
  ScoreConfigurationStatus,
} from '../../types/scoreConfig.types';
import { Badge } from '@/components/ui/badge';

export interface ScoreComponentTableProps {
  components: ScoreComponentInput[];
  calculationMethod: ScoreCalculationMethod;
  onChangeComponents: (components: ScoreComponentInput[]) => void;
  isReadOnly?: boolean;
  isLoading?: boolean;
  isDeleting?: boolean;
  configStatus?: ScoreConfigurationStatus | null;
  onAddComponent?: () => void;
  onEditComponent?: (component: ScoreComponentInput, index: number) => void;
  onDeleteComponent?: (component: ScoreComponentInput, index: number) => void;
  isDirty?: boolean;
  hasSavedConfig?: boolean;
  isValid?: boolean;
}

export const ScoreComponentTable: React.FC<ScoreComponentTableProps> = ({
  components,
  calculationMethod,
  onChangeComponents,
  isReadOnly = false,
  isLoading = false,
  isDeleting = false,
  configStatus = null,
  onAddComponent,
  onEditComponent,
  onDeleteComponent,
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
      className="score-components bg-surface-card border border-line/80 rounded-2xl shadow-xs overflow-hidden"
    >
      <div className="p-5 sm:px-6 border-b border-line-subtle flex flex-wrap gap-3 items-center justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h2 className="text-base font-semibold tracking-tight text-ink-primary">
              Thành phần điểm{' '}
              <span className="ml-0.5 text-sm font-normal text-ink-muted">
                ({components.length})
              </span>
            </h2>
            <span
              role="status"
              className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
                isLoading
                  ? 'bg-surface-muted text-ink-body'
                  : configStatus === 'Active'
                    ? 'bg-tone-success-bg text-tone-success-text ring-1 ring-inset ring-emerald-200/80'
                    : configStatus === 'Retired'
                      ? 'bg-surface-muted text-ink-body'
                      : isDirty
                        ? 'bg-tone-warning-bg text-tone-warning-strong ring-1 ring-inset ring-amber-200/80'
                        : 'bg-surface-muted text-ink-body'
              }`}
            >
              {isLoading
                ? 'Đang tải cấu hình'
                : configStatus === 'Active'
                  ? 'Đã công bố (chỉ đọc)'
                  : configStatus === 'Retired'
                    ? 'Đã lưu trữ (chỉ đọc)'
                    : isDirty
                      ? 'Có thay đổi chưa lưu'
                      : hasSavedConfig
                        ? 'Đã lưu'
                        : 'Chưa có cấu hình đã lưu'}
            </span>
          </div>
          <p className="text-xs text-ink-muted mt-1">
            Danh sách các thành phần điểm và điều kiện áp dụng cho từng nguồn chấm.
          </p>
        </div>
        {onAddComponent && !isReadOnly && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onAddComponent}
            disabled={isReadOnly || isDeleting}
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
          className="border-b border-tone-warning-border bg-tone-warning-bg px-5 py-3 text-sm text-tone-warning-strong sm:px-6"
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
              <TableHead className="w-[22%] px-5 py-3.5">Nguồn / thành viên</TableHead>
              <TableHead className="w-[33%] px-5 py-3.5">Điểm / trọng số</TableHead>
              <TableHead className="w-[23%] px-5 py-3.5">Điều kiện</TableHead>
              <TableHead align="right" className="w-[22%] px-5 py-3.5 whitespace-nowrap">
                {isReadOnly ? 'Phân loại' : 'Phân loại & thao tác'}
              </TableHead>
            </tr>
          </TableHeader>

          <TableBody>
            {components.length === 0 ? (
              <tr>
                <TableCell colSpan={4} className="px-5 py-12 text-center text-ink-subtle text-sm">
                  Chưa có đầu điểm nào trong cấu hình. Bấm "Thêm đầu điểm" để bắt đầu.
                </TableCell>
              </tr>
            ) : (
              components.map((item, index) => {
                const isLastSavedDraftComponent =
                  configStatus === 'Draft' && components.length <= 1;

                // Tính tỷ lệ phần trăm quy đổi động
                const percentEquiv =
                  totalWeight > 0
                    ? Math.round((Number(item.weight) / totalWeight) * 100 * 100) / 100
                    : 0;

                return (
                  <TableRow
                    key={item.componentCode || index}
                    hideSelectionMarker
                    className="hover:bg-surface-hover/70 transition-colors"
                  >
                    {/* Cột 1: Nguồn / thành viên */}
                    <TableCell className="px-5 py-4">
                      <div className="font-semibold text-ink-primary text-sm">
                        {item.componentName}
                      </div>
                    </TableCell>

                    {/* Cột 2: Điểm / Trọng số */}
                    <TableCell className="px-5 py-4 tabular-nums">
                      <div className="flex items-center gap-2 flex-wrap">
                        {isReadOnly ? (
                          <div className="flex items-baseline gap-1.5">
                            <span className="font-semibold text-ink-heading text-sm">
                              {item.weight}
                            </span>
                            <span className="text-xs text-ink-muted font-medium">
                              {calculationMethod === 'Percentage' ? '%' : 'hệ số'}
                            </span>
                            {calculationMethod === 'IntegerCoefficient' && (
                              <span className="text-xs text-ink-subtle font-medium">
                                (≈ {percentEquiv}%)
                              </span>
                            )}
                          </div>
                        ) : (
                          <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5 md:flex-nowrap md:whitespace-nowrap">
                            <div className="flex items-center gap-2 whitespace-nowrap">
                              <div className="inline-flex shrink-0 items-center rounded-xl border border-line-input bg-surface-card focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/15">
                                <button
                                  type="button"
                                  aria-label={`Giảm trọng số ${item.componentName}`}
                                  title={`Giảm ${weightStep}`}
                                  disabled={item.weight <= 0}
                                  onClick={() => handleStepWeight(index, -1)}
                                  className="flex size-11 md:size-9 shrink-0 items-center justify-center rounded-l-xl bg-surface-subtle/80 text-ink-body transition-colors hover:bg-tone-info-bg hover:text-primary active:bg-blue-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary disabled:cursor-not-allowed disabled:text-ink-faint disabled:hover:bg-surface-hover"
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
                                  className="score-weight-input w-14 h-11 md:w-12 md:h-9 min-w-0 border-x border-line-input px-1 text-base sm:text-sm font-semibold tabular-nums text-center text-ink-primary bg-surface-card caret-blue-600 focus:outline-none dark:caret-blue-400"
                                />
                                <button
                                  type="button"
                                  aria-label={`Tăng trọng số ${item.componentName}`}
                                  title={`Tăng ${weightStep}`}
                                  disabled={item.weight >= maximumWeight}
                                  onClick={() => handleStepWeight(index, 1)}
                                  className="flex size-11 md:size-9 shrink-0 items-center justify-center rounded-r-xl bg-surface-subtle/80 text-ink-body transition-colors hover:bg-tone-info-bg hover:text-primary active:bg-blue-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary disabled:cursor-not-allowed disabled:text-ink-faint disabled:hover:bg-surface-hover"
                                >
                                  <Plus className="size-4" aria-hidden="true" />
                                </button>
                              </div>
                              <span className="text-xs text-ink-muted font-medium whitespace-nowrap">
                                {calculationMethod === 'Percentage' ? '%' : 'hệ số'}
                              </span>
                            </div>
                            {calculationMethod === 'IntegerCoefficient' && (
                              <span className="text-xs text-ink-muted whitespace-nowrap">
                                <span className="sr-only">Tỷ lệ tham khảo: </span>(≈ {percentEquiv}
                                %)
                              </span>
                            )}
                          </div>
                        )}
                      </div>
                    </TableCell>

                    {/* Cột 3: Điều kiện */}
                    <TableCell className="px-5 py-4 text-xs leading-relaxed text-ink-body">
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <span>{getConditionText(item)}</span>
                        {item.minimumScore !== null && (
                          <span
                            title="Có áp dụng điểm sàn"
                            className="text-amber-500 dark:text-amber-400 inline-flex items-center"
                          >
                            <ShieldAlert className="size-3.5" />
                          </span>
                        )}
                      </div>
                    </TableCell>

                    {/* Cột 4: Phân loại và thao tác */}
                    <TableCell align="right" className="px-5 py-4 whitespace-nowrap">
                      <div className="inline-flex items-center justify-end gap-1.5 shrink-0">
                        {getCategoryBadge(item)}

                        {!isReadOnly && (
                          <div className="inline-flex items-center gap-0.5 ml-1">
                            {onEditComponent && (
                              <button
                                type="button"
                                onClick={() => onEditComponent(item, index)}
                                disabled={isDeleting}
                                title="Chỉnh sửa đầu điểm"
                                aria-label={`Chỉnh sửa ${item.componentName}`}
                                className="p-2 rounded-lg text-ink-muted hover:text-primary hover:bg-tone-info-bg transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:opacity-40 disabled:cursor-not-allowed"
                              >
                                <Pencil className="size-4" />
                              </button>
                            )}
                            <button
                              type="button"
                              onClick={() =>
                                onDeleteComponent
                                  ? onDeleteComponent(item, index)
                                  : handleRemoveComponent(index)
                              }
                              disabled={isDeleting || isLastSavedDraftComponent}
                              title={
                                isLastSavedDraftComponent
                                  ? 'Bản nháp phải có ít nhất 1 đầu điểm'
                                  : 'Xóa đầu điểm'
                              }
                              aria-label={`Xóa ${item.componentName}`}
                              className="p-2 rounded-lg text-ink-muted hover:text-tone-danger-icon hover:bg-tone-danger-bg transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:opacity-40 disabled:cursor-not-allowed"
                            >
                              <Trash2 className="size-4" />
                            </button>
                          </div>
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

      <div className="relative overflow-hidden border-t border-line/80 bg-gradient-to-br from-surface-subtle via-tone-info-bg/45 to-tone-warning-bg/35 p-5 sm:px-6">
        <div className="pointer-events-none absolute inset-x-0 top-0 h-[3px] bg-gradient-to-r from-blue-500 via-indigo-500 to-amber-500 opacity-85" />
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2.5">
            <span className="inline-flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-br from-blue-600 to-indigo-600 text-white shadow-sm ring-1 ring-blue-500/20">
              <Calculator className="size-4" />
            </span>
            <div>
              <h3 className="text-sm font-semibold text-ink-primary">Xem trước cách tính</h3>
              <p className="text-xs text-ink-muted">
                Công thức tổng hợp điểm theo trọng số hiện hành
              </p>
            </div>
          </div>
          <span className="rounded-full border border-tone-info-border bg-surface-card/90 px-3 py-1 text-xs font-semibold text-tone-info-text shadow-xs tabular-nums">
            Tổng trọng số: {totalWeight}
            {calculationMethod === 'Percentage' ? '%' : ''}
          </span>
        </div>

        {components.length > 0 && totalWeight > 0 ? (
          <div
            className={`mt-3.5 rounded-xl border border-line/90 bg-surface-card/95 px-3 py-2.5 shadow-xs lg:px-2 ${
              calculationMethod === 'Percentage' ? '' : 'min-[1440px]:px-2.5 2xl:px-3.5'
            }`}
          >
            <div
              className={`flex flex-wrap items-center gap-x-1 gap-y-1.5 text-xs text-ink-secondary lg:gap-x-0.5 lg:text-[10.5px] ${
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
                      <span className="font-semibold text-ink-subtle">(</span>
                    ) : (
                      <span className="font-semibold text-ink-subtle">+</span>
                    )}
                    <span
                      className={`inline-flex min-w-0 max-w-full items-center gap-0.5 whitespace-nowrap rounded-md border border-tone-info-border bg-gradient-to-r from-tone-info-bg/90 to-tone-violet-bg/60 px-1.5 py-0.5 lg:px-1 ${
                        calculationMethod === 'Percentage' ? '' : 'min-[1440px]:px-1.5'
                      }`}
                    >
                      <span className="truncate font-medium text-ink-primary">
                        {item.componentName}
                      </span>
                      <span className="text-[10.5px] text-ink-subtle">×</span>
                      <span className="font-semibold text-tone-info-text tabular-nums">
                        {item.weight}
                        {calculationMethod === 'Percentage' ? '%' : ''}
                      </span>
                    </span>
                    {isLast && <span className="font-semibold text-ink-subtle">)</span>}
                  </span>
                );
              })}
              <span
                className={`inline-flex items-center gap-1 whitespace-nowrap lg:gap-0.5 ${
                  calculationMethod === 'Percentage' ? '' : '2xl:gap-1'
                }`}
              >
                <span className="font-semibold text-ink-subtle">÷</span>
                <span className="inline-flex items-center rounded-md border border-tone-warning-border bg-gradient-to-r from-tone-warning-bg to-tone-orange-bg/75 px-1.5 py-0.5 font-semibold text-tone-warning-strong tabular-nums">
                  {totalWeight}
                  {calculationMethod === 'Percentage' ? '%' : ''}
                </span>
              </span>
            </div>
          </div>
        ) : (
          <div className="mt-3.5 rounded-xl border border-dashed border-line-input bg-surface-card/80 px-4 py-3 text-sm text-ink-body">
            {components.length === 0
              ? 'Thêm thành phần điểm để xem cách tính.'
              : 'Tổng trọng số phải lớn hơn 0 để xem cách tính.'}
          </div>
        )}

        <p className="mt-2.5 text-xs leading-relaxed text-ink-muted">
          Minh họa khi tất cả thành phần trên đều áp dụng. Khi một nguồn không áp dụng, điểm và
          trọng số của nguồn đó được loại khỏi phép tính.
        </p>
      </div>
    </section>
  );
};
