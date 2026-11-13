import { useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';

import { SYSTEM_ROLES } from '../../constants/systemRoles';
import { Save, UserPlus } from 'lucide-react';

import type { AccountGroup, SystemRole, UserFormValues, UserSummary } from '../../types/user.types';
import { defaultRolesForGroup, rolesBelongToGroup } from '../../utils/accountGroups';

interface UserFormProps {
  accountGroup: AccountGroup;
  user: UserSummary | null;
  isSaving: boolean;
  error: string | null;
  onSave: (values: UserFormValues) => void;
  onCancel: () => void;
  onDirtyChange?: (dirty: boolean) => void;
}

export function UserForm({
  accountGroup,
  user,
  isSaving,
  error,
  onSave,
  onCancel,
  onDirtyChange,
}: UserFormProps) {
  const isStudent = accountGroup === 'Student';
  const [values, setValues] = useState<UserFormValues>(() => ({
    email: user?.email ?? '',
    fullName: user?.fullName ?? '',
    userCode: user?.userCode ?? '',
    phoneNumber: user?.phoneNumber ?? '',
    roles: user ? [...user.roles] : defaultRolesForGroup(accountGroup),
  }));

  const [validationError, setValidationError] = useState('');
  const initialRoles = user?.roles ?? defaultRolesForGroup(accountGroup);
  function isDirty(next: UserFormValues) {
    return (
      next.fullName !== (user?.fullName ?? '') ||
      next.email !== (user?.email ?? '') ||
      next.userCode !== (user?.userCode ?? '') ||
      next.phoneNumber !== (user?.phoneNumber ?? '') ||
      next.roles.length !== initialRoles.length ||
      next.roles.some((role) => !initialRoles.includes(role))
    );
  }
  const dirty = isDirty(values);
  function updateValues(next: UserFormValues) {
    if (isSaving) return;
    setValues(next);
    onDirtyChange?.(isDirty(next));
  }

  function toggleRole(role: SystemRole, checked: boolean) {
    if (isSaving || role === 'Student' || isStudent) return;
    setValidationError('');
    updateValues({
      ...values,
      roles: checked
        ? [...new Set([...values.roles, role])]
        : values.roles.filter((item) => item !== role),
    });
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSaving || (user && !dirty)) return;

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
    <form onSubmit={handleSubmit} className="flex flex-1 flex-col bg-white">
      <div className="flex-1 space-y-6 p-5 sm:p-6">
        <div className="flex items-center justify-between gap-3">
          <h3 className="text-sm font-semibold text-slate-900">Thông tin cá nhân</h3>
          <p className="text-xs text-slate-500">
            <span className="text-rose-600">*</span> Bắt buộc
          </p>
        </div>
        <fieldset disabled={isSaving} className="space-y-5">
          <div className={`grid gap-x-4 gap-y-5 ${user ? '' : 'sm:grid-cols-2'}`}>
            <label className="space-y-1 text-sm">
              <span>
                Họ và tên{' '}
                <span aria-hidden="true" className="text-rose-600">
                  *
                </span>
              </span>
              <Input
                required
                autoFocus
                aria-label="Họ và tên"
                maxLength={150}
                autoComplete="name"
                placeholder="Nhập họ và tên"
                value={values.fullName}
                onChange={(event) => updateValues({ ...values, fullName: event.target.value })}
              />
            </label>

            <label className="space-y-1 text-sm">
              <span>
                Email{' '}
                <span aria-hidden="true" className="text-rose-600">
                  *
                </span>
              </span>
              <Input
                required
                type="email"
                aria-label="Email"
                maxLength={150}
                autoComplete="email"
                placeholder={isStudent ? 'sinhvien@example.edu.vn' : 'giangvien@example.edu.vn'}
                disabled={Boolean(user)}
                value={values.email}
                onChange={(event) => updateValues({ ...values, email: event.target.value })}
              />
              <span className="block text-xs leading-5 text-slate-500">
                {user
                  ? 'Email đăng nhập không thể thay đổi.'
                  : 'Email nhận liên kết kích hoạt tài khoản.'}
              </span>
            </label>

            <label className="space-y-1 text-sm">
              <span>
                {isStudent ? 'Mã số sinh viên (MSSV)' : 'Mã cán bộ'}{' '}
                {isStudent && (
                  <span aria-hidden="true" className="text-rose-600">
                    *
                  </span>
                )}
              </span>
              <Input
                required={isStudent}
                aria-label={isStudent ? 'Mã số sinh viên (MSSV)' : 'Mã cán bộ'}
                maxLength={20}
                placeholder={isStudent ? 'Nhập mã số sinh viên' : 'Nhập mã cán bộ'}
                value={values.userCode}
                onChange={(event) => updateValues({ ...values, userCode: event.target.value })}
              />
            </label>

            <label className="space-y-1 text-sm">
              <span>
                Số điện thoại <span className="text-xs text-slate-400">(không bắt buộc)</span>
              </span>
              <Input
                type="tel"
                aria-label="Số điện thoại"
                autoComplete="tel"
                placeholder="0901234567"
                value={values.phoneNumber}
                onChange={(event) => updateValues({ ...values, phoneNumber: event.target.value })}
              />
            </label>
          </div>

          {isStudent ? (
            <p className="rounded-xl border border-blue-100 bg-blue-50/60 px-4 py-3 text-sm leading-6 text-blue-800">
              {user
                ? 'Tài khoản thuộc nhóm sinh viên. Vai trò này không thể thay đổi.'
                : 'Tài khoản được khởi tạo với vai trò sinh viên.'}
            </p>
          ) : (
            <fieldset aria-describedby="account-role-help">
              <legend className="mb-2 text-sm font-medium">Vai trò hệ thống</legend>

              <div className={`grid gap-2.5 ${user ? '' : 'sm:grid-cols-2'}`}>
                {SYSTEM_ROLES.filter((role) => role.roleName !== 'Student').map((role) => {
                  const roleName = role.roleName;
                  const checked = values.roles.includes(roleName);

                  return (
                    <Checkbox
                      key={role.id}
                      className={`w-full rounded-xl border p-3 transition-colors focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2 ${checked ? 'border-primary/40 bg-primary/5' : 'border-slate-200 bg-white'}`}
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
          <p
            role="alert"
            className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700"
          >
            {validationError || error}
          </p>
        )}
      </div>
      <div className="sticky bottom-0 z-10 flex flex-col-reverse justify-end gap-2.5 border-t border-slate-200/80 bg-white/95 px-5 py-3.5 backdrop-blur-md sm:flex-row sm:px-6">
        <Button
          type="button"
          variant="outline"
          disabled={isSaving}
          onClick={onCancel}
          className="h-9 rounded-xl px-4 text-xs font-semibold"
        >
          {user ? 'Hủy chỉnh sửa' : 'Đóng'}
        </Button>

        <Button
          type="submit"
          disabled={Boolean(user) && !dirty}
          isLoading={isSaving}
          loadingText="Đang lưu..."
          leftIcon={user ? <Save /> : <UserPlus />}
          className="h-9 rounded-xl px-4 text-xs font-semibold"
        >
          {user ? 'Lưu thay đổi' : 'Tạo và gửi kích hoạt'}
        </Button>
      </div>
    </form>
  );
}
