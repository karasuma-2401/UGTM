import React from 'react';
import { Ban, Clock3, UsersRound } from 'lucide-react';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';

export interface UserStatusTabsProps {
  activeKey: string; // '' | 'PendingActivation' | 'Disabled' | 'none'
  onSelect: (status: '' | 'PendingActivation' | 'Disabled') => void;
  stats?: {
    total?: number;
    pendingActivation?: number;
    disabled?: number;
  };
  loading?: boolean;
}

export const UserStatusTabs: React.FC<UserStatusTabsProps> = ({
  activeKey,
  onSelect,
  stats,
  loading = false,
}) => {
  const formatCount = (value?: number) =>
    value !== undefined ? value.toLocaleString('vi-VN') : '—';

  const items: StatisticItem[] = [
    {
      key: 'total',
      label: 'Tổng tài khoản',
      value: formatCount(stats?.total),
      icon: <UsersRound />,
      tone: 'primary',
      active: activeKey === '',
      actionLabel: 'Lọc tổng tài khoản',
      onClick: () => onSelect(''),
    },
    {
      key: 'pending',
      label: 'Chờ kích hoạt',
      value: formatCount(stats?.pendingActivation),
      icon: <Clock3 />,
      tone: 'warning',
      active: activeKey === 'PendingActivation',
      actionLabel: 'Lọc tài khoản chờ kích hoạt',
      onClick: () => onSelect('PendingActivation'),
    },
    {
      key: 'disabled',
      label: 'Vô hiệu hóa',
      value: formatCount(stats?.disabled),
      icon: <Ban />,
      tone: 'danger',
      active: activeKey === 'Disabled',
      actionLabel: 'Lọc tài khoản vô hiệu hóa',
      onClick: () => onSelect('Disabled'),
    },
  ];

  return <StatisticsBar items={items} loading={loading} className="w-full lg:w-auto" />;
};
