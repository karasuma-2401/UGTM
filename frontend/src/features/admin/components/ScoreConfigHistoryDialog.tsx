import React from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import type { ScoreConfigurationDto } from '../types/scoreConfig.types';
import {
  CALCULATION_METHOD_LABELS,
  CONFIG_STATUS_LABELS,
} from '../types/scoreConfig.types';

export interface ScoreConfigHistoryDialogProps {
  open: boolean;
  onClose: () => void;
  history: ScoreConfigurationDto[];
  onSelectVersion?: (config: ScoreConfigurationDto) => void;
}

export const ScoreConfigHistoryDialog: React.FC<ScoreConfigHistoryDialogProps> = ({
  open,
  onClose,
  history,
  onSelectVersion,
}) => {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Lịch sử các phiên bản cấu hình điểm"
      className="max-w-2xl"
    >
      <div className="p-5 space-y-4">
        {history.length === 0 ? (
          <p className="text-sm text-slate-400 text-center py-8">
            Đợt khóa luận này chưa có lịch sử cấu hình điểm nào.
          </p>
        ) : (
          <div className="divide-y divide-slate-100 border border-slate-200 rounded-xl overflow-hidden">
            {history.map((config) => {
              const statusInfo = CONFIG_STATUS_LABELS[config.status];
              const dateStr = config.publishedAt || config.createdAt;
              const formattedDate = dateStr
                ? new Date(dateStr).toLocaleString('vi-VN')
                : 'Chưa xác định';

              return (
                <div
                  key={config.id}
                  className="p-4 hover:bg-slate-50/60 transition-colors flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 text-xs"
                >
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="font-bold text-slate-800 text-sm">
                        Phiên bản {config.versionNo}
                      </span>
                      <Badge variant={statusInfo.badgeVariant} className="text-[10px]">
                        {statusInfo.label}
                      </Badge>
                      <span className="text-slate-400">·</span>
                      <span className="text-slate-600 font-medium">
                        {CALCULATION_METHOD_LABELS[config.calculationMethod]}
                      </span>
                    </div>

                    {config.changeReason && (
                      <p className="text-slate-600 italic">
                        Lý do: "{config.changeReason}"
                      </p>
                    )}

                    <div className="text-[11px] text-slate-400">
                      Ghi nhận lúc: {formattedDate} ({config.components.length} đầu điểm)
                    </div>
                  </div>

                  {onSelectVersion && (
                    <div className="shrink-0">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => {
                          onSelectVersion(config);
                          onClose();
                        }}
                      >
                        Xem chi tiết
                      </Button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}

        <div className="pt-2 flex justify-end">
          <Button type="button" variant="outline" size="sm" onClick={onClose}>
            Đóng
          </Button>
        </div>
      </div>
    </Dialog>
  );
};