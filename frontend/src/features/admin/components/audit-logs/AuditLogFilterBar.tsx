import React from 'react';
import { Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, type SelectOption } from '@/components/ui/select';
import type { AuditLogFilterState } from '../../types/auditLog.types';

interface AuditLogFilterBarProps {
  filter: AuditLogFilterState;
  onFilterChange: (next: AuditLogFilterState) => void;
  onApply: () => void;
  onReset: () => void;
  isLoading?: boolean;
}

const ACTION_OPTIONS: SelectOption[] = [
  { value: '', label: 'Tất cả thao tác' },
  { value: 'Identity.User.StatusChanged', label: 'Đổi trạng thái tài khoản' },
  { value: 'Identity.Role.PermissionsChanged', label: 'Cập nhật quyền vai trò' },
  { value: 'Identity.User.Created', label: 'Tạo tài khoản' },
  { value: 'Identity.User.Updated', label: 'Cập nhật tài khoản' },
  { value: 'Identity.User.RolesChanged', label: 'Thay đổi vai trò người dùng' },
  { value: 'Identity.Authentication.Login', label: 'Đăng nhập hệ thống' },
  { value: 'Identity.Session.Revoked', label: 'Thu hồi phiên làm việc' },
  { value: 'Identity.Account.PasswordReset', label: 'Đặt lại mật khẩu' },
];

export const AuditLogFilterBar: React.FC<AuditLogFilterBarProps> = ({
  filter,
  onFilterChange,
  onApply,
  onReset,
  isLoading = false,
}) => {
  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onApply();
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="bg-white border border-slate-200 rounded-xl p-4 sm:p-5 shadow-xs space-y-4"
    >
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3">
        <div className="flex-1 min-w-0">
          <Input
            value={filter.searchTerm}
            onChange={(e) => onFilterChange({ ...filter, searchTerm: e.target.value })}
            placeholder="Tìm người thực hiện, mã hồ sơ hoặc nội dung thay đổi"
            leftIcon={<Search className="size-4 text-slate-400" />}
            className="h-10 text-sm bg-slate-50/50 focus:bg-white"
          />
        </div>

        <div className="w-full sm:w-56 shrink-0">
          <Select
            options={ACTION_OPTIONS}
            value={filter.action}
            onChange={(val) => onFilterChange({ ...filter, action: String(val || '') })}
            placeholder="Tất cả thao tác"
            triggerClassName="h-10 text-sm bg-slate-50/50"
          />
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <Button
            type="submit"
            disabled={isLoading}
            className="h-10 px-5 font-medium shadow-xs"
          >
            Áp dụng
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={isLoading}
            onClick={onReset}
            className="h-10 px-4 text-slate-600 hover:text-slate-900"
          >
            Đặt lại
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 sm:gap-4 pt-1">
        <div className="space-y-1.5">
          <label className="text-xs font-semibold text-slate-700">Từ ngày</label>
          <input
            type="date"
            value={filter.fromUtc}
            onChange={(e) => onFilterChange({ ...filter, fromUtc: e.target.value })}
            className="w-full h-10 px-3 text-sm rounded-md border border-slate-200 bg-slate-50/50 focus:bg-white focus:outline-none focus:ring-1 focus:ring-primary text-slate-700"
          />
        </div>

        <div className="space-y-1.5">
          <label className="text-xs font-semibold text-slate-700">Đến ngày</label>
          <input
            type="date"
            value={filter.toUtc}
            onChange={(e) => onFilterChange({ ...filter, toUtc: e.target.value })}
            className="w-full h-10 px-3 text-sm rounded-md border border-slate-200 bg-slate-50/50 focus:bg-white focus:outline-none focus:ring-1 focus:ring-primary text-slate-700"
          />
        </div>
      </div>
    </form>
  );
};

