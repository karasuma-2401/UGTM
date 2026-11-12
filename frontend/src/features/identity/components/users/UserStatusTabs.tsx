import React from 'react';
import { UsersRound } from 'lucide-react';
import { StatusTabs, type StatusTabItem } from '@/components/ui/status-tabs';

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
  const tabs: StatusTabItem<'' | 'PendingActivation' | 'Disabled'>[] = [
    {
      key: '',
      label: 'Tổng tài khoản',
      count: stats?.total,
      icon: <UsersRound className="size-3.5 text-slate-500" />,
      tone: 'primary',
    },
    {
      key: 'PendingActivation',
      label: 'Chờ kích hoạt',
      count: stats?.pendingActivation,
      tone: 'warning',
    },
    {
      key: 'Disabled',
      label: 'Vô hiệu hóa',
      count: stats?.disabled,
      tone: 'danger',
    },
  ];

  return (
    <StatusTabs
      tabs={tabs}
      activeKey={activeKey as '' | 'PendingActivation' | 'Disabled' | 'none'}
      onSelect={onSelect}
      ariaLabel="Lọc nhanh trạng thái tài khoản"
      loading={loading}
    />
  );
};
