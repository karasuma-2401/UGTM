import React, { useState } from 'react';
import { ArrowLeft, Check } from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import type {
  ScoreComponentInput,
  ScoreConfigurationDto,
  ScoreConfigurationStatus,
} from '../../types/scoreConfig.types';
import {
  CALCULATION_METHOD_LABELS,
  CONFIG_STATUS_LABELS,
} from '../../types/scoreConfig.types';

export interface ScoreConfigHistoryDialogProps {
  open: boolean;
  onClose: () => void;
  history: ScoreConfigurationDto[];
  onSelectVersion?: (config: ScoreConfigurationDto) => void;
}

const statusToneMap: Record<ScoreConfigurationStatus, StatusTone> = {
  Active: 'success',
  Draft: 'warning',
  Retired: 'neutral',
};

function formatDateTime(value?: string | null): string {
  if (!value) return 'Chưa xác định';
  return new Date(value).toLocaleString('vi-VN');
}

function getConditionSummary(item: ScoreComponentInput): string {
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
    return parts.join(' · ');
  }
  return item.isRequired ? 'Bắt buộc khi áp dụng' : 'Tùy chọn';
}

function getSourceBadge(source: ScoreComponentInput['source']) {
  if (source === 'Advisor') {
    return <Badge variant="info">Nguồn riêng</Badge>;
  }
  if (source === 'Reviewer') {
    return <Badge variant="warning">Bắt buộc</Badge>;
  }
  if (source.startsWith('Council')) {
    return <Badge variant="secondary">Điểm HĐ</Badge>;
  }
  return <Badge variant="default">Tùy biến</Badge>;
}

export const ScoreConfigHistoryDialog: React.FC<ScoreConfigHistoryDialogProps> = ({
  open,
  onClose,
  history,
  onSelectVersion,
}) => {
  const [viewingConfig, setViewingConfig] = useState<ScoreConfigurationDto | null>(null);

  const activeConfig = open ? viewingConfig : null;

  const handleClose = () => {
    setViewingConfig(null);
    onClose();
  };

  const totalWeight = activeConfig
    ? Math.round(
        activeConfig.components.reduce((acc, item) => acc + (Number(item.weight) || 0), 0) * 100,
      ) / 100
    : 0;

  const sortedComponents = activeConfig
    ? [...activeConfig.components].sort((a, b) => a.orderIndex - b.orderIndex)
    : [];

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      title={
        activeConfig
          ? `Chi tiết phiên bản v${activeConfig.versionNo}`
          : 'Lịch sử các phiên bản cấu hình điểm'
      }
      className="max-w-2xl"
      headerClassName="border-b border-line/80 bg-surface-card px-5 py-4 sm:px-6"
    >
      <div className="p-5 sm:p-6 space-y-4">
        {activeConfig ? (
          <>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="h-8 rounded-lg px-3 text-xs font-medium"
                onClick={() => setViewingConfig(null)}
              >
                <ArrowLeft className="size-3.5" />
                Quay lại danh sách
              </Button>

              <div className="flex flex-wrap items-center gap-2">
                <StatusBadge tone={statusToneMap[activeConfig.status]}>
                  {CONFIG_STATUS_LABELS[activeConfig.status].label}
                </StatusBadge>
                <Badge variant="default">Chế độ chỉ đọc</Badge>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-3 rounded-xl border border-line/80 bg-surface-subtle/70 p-4 sm:grid-cols-3">
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-wide text-ink-subtle">
                  Phương thức tính
                </p>
                <p className="mt-1 text-sm font-semibold text-ink-primary">
                  {CALCULATION_METHOD_LABELS[activeConfig.calculationMethod]}
                </p>
              </div>
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-wide text-ink-subtle">
                  Tổng trọng số
                </p>
                <p className="mt-1 text-sm font-semibold text-tone-info-text tabular-nums">
                  {totalWeight}
                  {activeConfig.calculationMethod === 'Percentage' ? '%' : ' hệ số'}
                </p>
              </div>
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-wide text-ink-subtle">
                  Ghi nhận lúc
                </p>
                <p className="mt-1 text-sm font-medium text-ink-heading tabular-nums">
                  {formatDateTime(activeConfig.publishedAt || activeConfig.createdAt)}
                </p>
              </div>
              {activeConfig.changeReason && (
                <div className="sm:col-span-3 border-t border-line/70 pt-2.5">
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-ink-subtle">
                    Lý do thay đổi
                  </p>
                  <p className="mt-1 text-xs leading-relaxed text-ink-secondary break-words">
                    {activeConfig.changeReason}
                  </p>
                </div>
              )}
            </div>

            <div className="overflow-hidden rounded-xl border border-line/80 bg-surface-card">
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-sm">
                  <thead>
                    <tr className="border-b border-line/80 bg-surface-subtle/80 text-[11px] font-semibold uppercase tracking-wider text-ink-muted">
                      <th className="px-4 py-2.5">Thành phần</th>
                      <th className="px-4 py-2.5">Nguồn</th>
                      <th className="px-4 py-2.5 text-right">Trọng số</th>
                      <th className="px-4 py-2.5">Điều kiện</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line-subtle">
                    {sortedComponents.map((item, idx) => {
                      const percentEquiv =
                        totalWeight > 0
                          ? Math.round((Number(item.weight) / totalWeight) * 100 * 100) / 100
                          : 0;

                      return (
                        <tr key={item.componentCode || idx} className="hover:bg-surface-hover/60">
                          <td className="px-4 py-3 font-semibold text-ink-primary">
                            {item.componentName}
                          </td>
                          <td className="px-4 py-3">{getSourceBadge(item.source)}</td>
                          <td className="px-4 py-3 text-right font-semibold text-ink-heading tabular-nums whitespace-nowrap">
                            {item.weight}
                            {activeConfig.calculationMethod === 'Percentage' ? '%' : ' hệ số'}
                            {activeConfig.calculationMethod === 'IntegerCoefficient' && (
                              <span className="ml-1 text-xs font-normal text-ink-subtle">
                                ({percentEquiv}%)
                              </span>
                            )}
                          </td>
                          <td className="px-4 py-3 text-xs text-ink-body">
                            {getConditionSummary(item)}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </>
        ) : history.length === 0 ? (
          <p className="rounded-xl border border-line/80 bg-surface-subtle/70 py-10 text-center text-sm text-ink-muted">
            Đợt khóa luận này chưa có lịch sử cấu hình điểm nào.
          </p>
        ) : (
          <div className="space-y-2.5">
            {history.map((config) => {
              const statusInfo = CONFIG_STATUS_LABELS[config.status];
              const formattedDate = formatDateTime(config.publishedAt || config.createdAt);

              return (
                <div
                  key={config.id}
                  className="flex flex-col gap-3 rounded-xl border border-line/80 bg-surface-subtle/60 p-4 transition-colors hover:bg-surface-hover sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="space-y-1.5 min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-bold text-ink-primary text-sm">
                        Phiên bản {config.versionNo}
                      </span>
                      <StatusBadge tone={statusToneMap[config.status]}>
                        {statusInfo.label}
                      </StatusBadge>
                      <span className="text-xs text-ink-body font-medium">
                        {CALCULATION_METHOD_LABELS[config.calculationMethod]}
                      </span>
                    </div>

                    {config.changeReason && (
                      <p className="text-xs text-ink-body italic break-words">
                        Lý do: "{config.changeReason}"
                      </p>
                    )}

                    <div className="text-xs text-ink-muted tabular-nums">
                      Ghi nhận lúc: {formattedDate} · {config.components.length} đầu điểm
                    </div>
                  </div>

                  <div className="shrink-0">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-8 rounded-md border-line-input bg-surface-elevated px-3 text-xs font-medium text-ink-secondary shadow-sm hover:border-line-strong hover:bg-surface-hover hover:text-ink-primary"
                      onClick={() => setViewingConfig(config)}
                      aria-label={`Xem phiên bản ${config.versionNo}`}
                    >
                      Xem
                    </Button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      <div className="sticky bottom-0 z-10 flex flex-wrap items-center justify-end gap-2 border-t border-line/80 bg-surface-card/95 px-5 py-3.5 backdrop-blur-md sm:px-6">
        {activeConfig && onSelectVersion && (
          <Button
            type="button"
            variant="default"
            onClick={() => {
              onSelectVersion(activeConfig);
              handleClose();
            }}
            className="h-9 rounded-xl px-4 text-xs font-semibold"
          >
            <Check className="size-3.5" />
            Áp dụng thông số
          </Button>
        )}
        <Button
          type="button"
          variant="outline"
          onClick={handleClose}
          className="h-9 rounded-xl px-4 text-xs font-semibold"
        >
          Đóng
        </Button>
      </div>
    </Dialog>
  );
};