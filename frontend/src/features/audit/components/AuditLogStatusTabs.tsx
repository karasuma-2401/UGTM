import React from 'react';
import { CheckCircle2, ScrollText, ShieldAlert, XCircle } from 'lucide-react';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';

export interface AuditOutcomeStats {
  total: number;
  success: number;
  denied: number;
  failed: number;
  other?: number;
}

interface AuditLogStatusTabsProps {
  currentOutcome: string;
  onSelectOutcome: (outcome: string) => void;
  stats: AuditOutcomeStats;
  loading?: boolean;
  error?: boolean;
}

export const AuditLogStatusTabs: React.FC<AuditLogStatusTabsProps> = ({
  currentOutcome,
  onSelectOutcome,
  stats,
  loading = false,
  error = false,
}) => {
  const formatValue = (val: number) => (error ? '—' : val.toLocaleString('vi-VN'));

  const items: StatisticItem[] = [
    {
      key: 'all',
      label: 'Tất cả',
      value: formatValue(stats.total),
      icon: <ScrollText />,
      tone: 'primary',
      active: currentOutcome === '',
      actionLabel: 'Lọc tất cả sự kiện',
      onClick: () => onSelectOutcome(''),
    },
    {
      key: 'success',
      label: 'Thành công',
      value: formatValue(stats.success),
      icon: <CheckCircle2 />,
      tone: 'success',
      active: currentOutcome === 'Success',
      actionLabel: 'Lọc sự kiện thành công',
      onClick: () => onSelectOutcome('Success'),
    },
    {
      key: 'denied',
      label: 'Bị từ chối',
      value: formatValue(stats.denied),
      icon: <ShieldAlert />,
      tone: 'warning',
      active: currentOutcome === 'Denied',
      actionLabel: 'Lọc sự kiện bị từ chối',
      onClick: () => onSelectOutcome('Denied'),
    },
    {
      key: 'failed',
      label: 'Thất bại',
      value: formatValue(stats.failed),
      icon: <XCircle />,
      tone: 'danger',
      active: currentOutcome === 'Failed',
      actionLabel: 'Lọc sự kiện thất bại',
      onClick: () => onSelectOutcome('Failed'),
    },
  ];

  return (
    <StatisticsBar
      items={items}
      loading={loading && !error}
      className="w-full lg:w-auto"
    />
  );
};
