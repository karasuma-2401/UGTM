import { useState, type FormEvent } from 'react';
import { KeyRound } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PasswordField } from '@/features/auth/components/PasswordField';
import { PasswordFields } from '@/features/auth/components/PasswordFields';
import { isStrongPassword } from '@/features/auth/utils/passwordValidation';
import type { UserProfile } from '@/features/auth/types/auth.types';
import { formatVietnamDateTime } from '../utils/profileFormatters';

interface ProfileSecurityTabProps {
  user: UserProfile;
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
  onCurrentPasswordChange: (val: string) => void;
  onNewPasswordChange: (val: string) => void;
  onConfirmPasswordChange: (val: string) => void;
  isSubmitting: boolean;
  serverError: string | null;
  onSubmitChangePassword: () => void;
  onResetForm: () => void;
}

export function ProfileSecurityTab({
  user,
  currentPassword,
  newPassword,
  confirmPassword,
  onCurrentPasswordChange,
  onNewPasswordChange,
  onConfirmPasswordChange,
  isSubmitting,
  serverError,
  onSubmitChangePassword,
  onResetForm,
}: ProfileSecurityTabProps) {
  const [validationError, setValidationError] = useState<string | null>(null);

  const hasAnyInput = Boolean(currentPassword || newPassword || confirmPassword);
  const canSubmit =
    Boolean(currentPassword) &&
    isStrongPassword(newPassword) &&
    newPassword === confirmPassword &&
    currentPassword !== newPassword &&
    !isSubmitting;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (isSubmitting) return;

    if (!currentPassword) {
      setValidationError('Vui lòng nhập mật khẩu hiện tại.');
      return;
    }

    if (!isStrongPassword(newPassword)) {
      setValidationError(
        'Mật khẩu mới chưa đạt đủ tiêu chí bảo mật (tối thiểu 12 ký tự, chữ hoa, chữ thường, số, ký tự đặc biệt và không khoảng trắng).',
      );
      return;
    }

    if (newPassword === currentPassword) {
      setValidationError('Mật khẩu mới không được trùng với mật khẩu hiện tại.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setValidationError('Mật khẩu xác nhận không khớp.');
      return;
    }

    setValidationError(null);
    onSubmitChangePassword();
  };

  return (
    <div className="space-y-4">
      {user.mustChangePassword && (
        <InlineAlert tone="warning" title="Yêu cầu đổi mật khẩu bắt buộc">
          Tài khoản của bạn vừa được Quản trị viên cấp lại mật khẩu tạm thời. Vui lòng thiết lập mật khẩu cá nhân mới bên dưới để mở khóa toàn bộ các chức năng của hệ thống.
        </InlineAlert>
      )}

      <Card className="overflow-hidden divide-y divide-line-subtle">
        <div className="p-5 sm:p-6 flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-base font-semibold text-ink-primary">
              Đổi mật khẩu tài khoản
            </h2>
            <p className="mt-0.5 text-xs text-ink-muted leading-relaxed">
              Sử dụng mật khẩu mạnh từ 12–128 ký tự để bảo vệ hồ sơ và dữ liệu khóa luận.
            </p>
            {user.passwordChangedAt && (
              <p className="mt-1 text-xs text-ink-muted">
                Lần đổi gần nhất:{' '}
                <span className="font-medium tabular-nums text-ink-secondary">
                  {formatVietnamDateTime(user.passwordChangedAt)}
                </span>
                .
              </p>
            )}
          </div>
        </div>

        <form onSubmit={handleSubmit} className="divide-y divide-line-subtle">
          <div className="p-5 sm:p-6 space-y-5">
            {(validationError || serverError) && (
              <InlineAlert tone="error" title="Không thể đổi mật khẩu">
                {validationError || serverError}
              </InlineAlert>
            )}

            <div className="space-y-4 max-w-xl">
              <PasswordField
                id="profile-current-password"
                name="current-password"
                label="Mật khẩu hiện tại"
                autoComplete="current-password"
                placeholder="Nhập mật khẩu hiện tại..."
                value={currentPassword}
                disabled={isSubmitting}
                onChange={(val: string) => {
                  setValidationError(null);
                  onCurrentPasswordChange(val);
                }}
              />

              <div className="pt-2 border-t border-line-subtle space-y-4">
                <PasswordFields
                  password={newPassword}
                  confirmationPassword={confirmPassword}
                  onPasswordChange={(val) => {
                    setValidationError(null);
                    onNewPasswordChange(val);
                  }}
                  onConfirmationChange={(val) => {
                    setValidationError(null);
                    onConfirmPasswordChange(val);
                  }}
                  disabled={isSubmitting}
                  passwordLabel="Mật khẩu mới"
                  confirmationLabel="Xác nhận mật khẩu mới"
                  passwordPlaceholder="Tối thiểu 12 ký tự..."
                  confirmationPlaceholder="Nhập lại mật khẩu mới..."
                  showRules
                  autoValidateMismatch
                  strongLabel="✓ Mật khẩu mới đạt chuẩn bảo mật UGTM"
                />
              </div>
            </div>

            <p className="text-xs leading-relaxed text-ink-muted">
              Sau khi đổi mật khẩu, mọi phiên đăng nhập trên các thiết bị khác sẽ tự động đăng xuất và bạn cần đăng nhập lại bằng mật khẩu mới.
            </p>
          </div>

          <div className="bg-surface-subtle/50 px-5 sm:px-6 py-4 flex flex-wrap items-center justify-end gap-3">
            <div className="flex items-center gap-2">
              {hasAnyInput && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={isSubmitting}
                  onClick={() => {
                    setValidationError(null);
                    onResetForm();
                  }}
                >
                  Xóa biểu mẫu
                </Button>
              )}
              <Button
                type="submit"
                disabled={!canSubmit}
                isLoading={isSubmitting}
                loadingText="Đang cập nhật..."
                leftIcon={<KeyRound className="size-4" />}
              >
                Đổi mật khẩu
              </Button>
            </div>
          </div>
        </form>
      </Card>
    </div>
  );
}
