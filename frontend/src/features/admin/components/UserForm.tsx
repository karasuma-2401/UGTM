import { useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';

import { SYSTEM_ROLES } from '../constants/systemRoles';
import { Save, UserPlus } from 'lucide-react';

import type { SystemRole, UserFormValues, UserSummary } from '../types/user.types';

interface UserFormProps {
  user: UserSummary | null;
  isSaving: boolean;
  error: string | null;
  onSave: (values: UserFormValues) => void;
  onCancel: () => void;
}

export function UserForm({ user, isSaving, error, onSave, onCancel }: UserFormProps) {
  const [values, setValues] = useState<UserFormValues>(() => ({
    email: user?.email ?? '',
    fullName: user?.fullName ?? '',
    userCode: user?.userCode ?? '',
    phoneNumber: user?.phoneNumber ?? '',
    roles: user ? [...user.roles] : ['Student'],
  }));

  const [validationError, setValidationError] = useState('');

  const cannotBecomeStudent = user?.roles.some((role) => role !== 'Student') ?? false;

  function isRoleDisabled(role: SystemRole, roles: SystemRole[]) {
    if (roles.includes(role)) return false;
    return role === 'Student'
      ? cannotBecomeStudent || roles.some((item) => item !== 'Student')
      : roles.includes('Student');
  }

  function toggleRole(role: SystemRole, checked: boolean) {
    if (isSaving) return;
    setValidationError('');
    setValues((previous) => {
      if (checked && isRoleDisabled(role, previous.roles)) return previous;
      return {
        ...previous,
        roles: checked
          ? [...new Set([...previous.roles, role])]
          : previous.roles.filter((item) => item !== role),
      };
    });
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSaving) return;

    setValidationError('');

    if (!values.fullName.trim() || !values.email.trim()) {
      setValidationError('Vui lòng nhập họ tên và email.');
      return;
    }

    if (values.roles.length === 0) {
      setValidationError('Chọn ít nhất một vai trò.');
      return;
    }

    if (values.roles.includes('Student') && values.roles.length > 1) {
      setValidationError('Vai trò Sinh viên không được kết hợp với vai trò khác.');
      return;
    }

    if (values.roles.includes('Student') && cannotBecomeStudent) {
      setValidationError('Tài khoản đã có vai trò khác không được chuyển thành Sinh viên.');
      return;
    }

    onSave(values);
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5 bg-white p-5 sm:p-6">
      <div>
        <h2 className="text-lg font-semibold text-slate-800">
          {user ? 'Chi tiết tài khoản' : 'Tạo tài khoản'}
        </h2>

        {!user && (
          <p className="mt-1 text-sm text-slate-500">
            Người dùng sẽ nhận email để kích hoạt và đặt mật khẩu.
          </p>
        )}
      </div>

      <fieldset disabled={isSaving} className="space-y-5">
        <div className="grid gap-4 md:grid-cols-2">
          <label className="space-y-1 text-sm">
            <span>Họ và tên</span>
            <Input
              required
              maxLength={150}
              value={values.fullName}
              onChange={(event) => setValues({ ...values, fullName: event.target.value })}
            />
          </label>

          <label className="space-y-1 text-sm">
            <span>Email</span>
            <Input
              required
              type="email"
              maxLength={150}
              disabled={Boolean(user)}
              value={values.email}
              onChange={(event) => setValues({ ...values, email: event.target.value })}
            />
          </label>

          <label className="space-y-1 text-sm">
            <span>Mã người dùng</span>
            <Input
              maxLength={20}
              value={values.userCode}
              onChange={(event) => setValues({ ...values, userCode: event.target.value })}
            />
          </label>

          <label className="space-y-1 text-sm">
            <span>Số điện thoại</span>
            <Input
              type="tel"
              placeholder="0901234567"
              value={values.phoneNumber}
              onChange={(event) => setValues({ ...values, phoneNumber: event.target.value })}
            />
          </label>
        </div>

        <fieldset aria-describedby="account-role-help">
          <legend className="mb-2 text-sm font-medium">Vai trò hệ thống</legend>

          <div className="grid gap-3 sm:grid-cols-2">
            {SYSTEM_ROLES.map((role) => {
              const roleName = role.roleName;
              const checked = values.roles.includes(roleName);

              return (
                <Checkbox
                  key={role.id}
                  className={`w-full rounded-lg border p-3 transition-colors focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2 ${checked ? 'border-primary/40 bg-primary/5' : 'border-border bg-background'}`}
                  label={role.title}
                  checked={checked}
                  disabled={isSaving || isRoleDisabled(roleName, values.roles)}
                  onCheckedChange={(nextChecked) => toggleRole(roleName, nextChecked)}
                />
              );
            })}
          </div>
          <p id="account-role-help" className="mt-3 text-xs leading-relaxed text-muted-foreground">
            {cannotBecomeStudent
              ? 'Tài khoản này đã có vai trò khác nên không được chuyển thành Sinh viên.'
              : 'Sinh viên là vai trò độc lập. Bỏ chọn Sinh viên trước khi chọn vai trò khác.'}
          </p>
        </fieldset>
      </fieldset>

      {(validationError || error) && (
        <p role="alert" className="text-sm text-red-600">
          {validationError || error}
        </p>
      )}

      <div className="flex justify-end gap-3 border-t border-slate-100 pt-5">
        <Button
          type="button"
          variant="outline"
          disabled={isSaving}
          onClick={onCancel}
          className="h-10 px-5"
        >
          Đóng
        </Button>

        <Button
          type="submit"
          isLoading={isSaving}
          loadingText="Đang lưu..."
          leftIcon={user ? <Save /> : <UserPlus />}
          className="h-10 px-5"
        >
          {user ? 'Lưu thay đổi' : 'Tạo và gửi kích hoạt'}
        </Button>
      </div>
    </form>
  );
}
