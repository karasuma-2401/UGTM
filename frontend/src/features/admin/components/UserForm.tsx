import { useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';

import { SYSTEM_ROLES } from '../constants/systemRoles';
import { Save, UserPlus } from 'lucide-react';

import type { AccountGroup, SystemRole, UserFormValues, UserSummary } from '../types/user.types';
import { defaultRolesForGroup, rolesBelongToGroup } from '../utils/accountGroups';

interface UserFormProps {
  accountGroup: AccountGroup;
  user: UserSummary | null;
  isSaving: boolean;
  error: string | null;
  onSave: (values: UserFormValues) => void;
  onCancel: () => void;
}

export function UserForm({ accountGroup, user, isSaving, error, onSave, onCancel }: UserFormProps) {
  const isStudent = accountGroup === 'Student';
  const [values, setValues] = useState<UserFormValues>(() => ({
    email: user?.email ?? '',
    fullName: user?.fullName ?? '',
    userCode: user?.userCode ?? '',
    phoneNumber: user?.phoneNumber ?? '',
    roles: user ? [...user.roles] : defaultRolesForGroup(accountGroup),
  }));

  const [validationError, setValidationError] = useState('');

  function toggleRole(role: SystemRole, checked: boolean) {
    if (isSaving) return;
    setValidationError('');
    setValues((previous) => {
      if (role === 'Student' || isStudent) return previous;
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

    if (!rolesBelongToGroup(values.roles, accountGroup)) {
      setValidationError('Vai trò không phù hợp với nhóm tài khoản đang quản lý.');
      return;
    }

    if (isStudent && !values.userCode.trim()) {
      setValidationError('Vui lòng nhập mã số sinh viên.');
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
            Vui lòng nhập đầy đủ thông tin.
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
            <span>{isStudent ? 'Mã số sinh viên (MSSV)' : 'Mã cán bộ'}</span>
            <Input
              required={isStudent}
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

        {isStudent ? (
          <p className="rounded-lg bg-blue-50 p-3 text-sm text-blue-800">
            Tài khoản được khởi tạo với vai trò sinh viên.
          </p>
        ) : (
          <fieldset aria-describedby="account-role-help">
            <legend className="mb-2 text-sm font-medium">Vai trò hệ thống</legend>

            <div className="grid gap-3 sm:grid-cols-2">
              {SYSTEM_ROLES.filter((role) => role.roleName !== 'Student').map((role) => {
                const roleName = role.roleName;
                const checked = values.roles.includes(roleName);

                return (
                  <Checkbox
                    key={role.id}
                    className={`w-full rounded-lg border p-3 transition-colors focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2 ${checked ? 'border-primary/40 bg-primary/5' : 'border-border bg-background'}`}
                    label={role.title}
                    checked={checked}
                    disabled={isSaving}
                    onCheckedChange={(nextChecked) => toggleRole(roleName, nextChecked)}
                  />
                );
              })}
            </div>
            <p
              id="account-role-help"
              className="mt-3 text-xs leading-relaxed text-muted-foreground"
            >
              Chọn một hoặc nhiều vai trò cho giảng viên, cán bộ.
            </p>
          </fieldset>
        )}
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
