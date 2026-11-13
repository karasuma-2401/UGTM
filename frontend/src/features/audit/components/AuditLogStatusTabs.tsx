import React from 'react';
import { ScrollText } from 'lucide-react';
import { StatusTabs, type StatusTabItem } from '@/components/ui/status-tabs';

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
  const valueOrFallback = (val: number) => (error ? '—' : loading ? undefined : val);

  const tabs: StatusTabItem<string>[] = [
    {
      key: '',
      label: 'Tất cả',
      count: valueOrFallback(stats.total),
      icon: <ScrollText className="size-3.5 text-slate-500" />,
      tone: 'primary',
    },
    {
      key: 'Success',
      label: 'Thành công',
      count: valueOrFallback(stats.success),
      tone: 'success',
    },
    {
      key: 'Denied',
      label: 'Bị từ chối',
      count: valueOrFallback(stats.denied),
      tone: 'warning',
    },
    {
      key: 'Failed',
      label: 'Thất bại',
      count: valueOrFallback(stats.failed),
      tone: 'danger',
    },
  ];

  return (
    <StatusTabs
      tabs={tabs}
      activeKey={currentOutcome}
      onSelect={onSelectOutcome}
      ariaLabel="Lọc nhanh theo kết quả thao tác"
      loading={loading}
    />
  );
};
