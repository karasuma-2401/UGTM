import { useEffect, useState } from 'react';
import { useBlocker } from 'react-router-dom';
import { Pencil, UserPlus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Dialog } from '@/components/ui/dialog';
import { UserForm } from './UserForm';
import { UserAccountDetails, UserAccountHeader } from './UserAccountDetails';
import type { AccountGroup, UserFormValues, UserSummary } from '../../types/user.types';

interface UserFormDialogProps {
  accountGroup: AccountGroup;
  user: UserSummary | null;
  isSaving: boolean;
  error: string | null;
  onSave: (values: UserFormValues) => void;
  onClose: () => void;
}

export function UserFormDialog({
  accountGroup,
  user,
  isSaving,
  error,
  onSave,
  onClose,
}: UserFormDialogProps) {
  const [editing, setEditing] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [discardAction, setDiscardAction] = useState<'close' | 'cancel' | null>(null);
  const blocker = useBlocker(dirty || isSaving);
  const groupLabel = accountGroup === 'Student' ? 'sinh viên' : 'giảng viên & cán bộ';
  const title = `Tạo tài khoản ${groupLabel}`;

  useEffect(() => {
    if (!dirty && !isSaving) return;
    const beforeUnload = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', beforeUnload);
    return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [dirty, isSaving]);

  function requestClose(action: 'close' | 'cancel') {
    if (isSaving) return;
    if (dirty) {
      setDiscardAction(action);
      return;
    }
    if (action === 'cancel') setEditing(false);
    else onClose();
  }
  function discard() {
    if (isSaving) return;
    setDirty(false);
    setDiscardAction(null);
    if (blocker.state === 'blocked') blocker.proceed();
    else if (discardAction === 'cancel') setEditing(false);
    else onClose();
  }

  return (
    <>
      <Dialog
        variant={user ? 'drawer' : 'modal'}
        headerClassName={user ? 'bg-gradient-to-r from-blue-50 via-white to-orange-50' : undefined}
        title={
          user ? (
            <UserAccountHeader user={user} />
          ) : (
            <span className="flex items-center gap-3">
              <span
                aria-hidden="true"
                className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-primary"
              >
                <UserPlus className="size-5" />
              </span>
              <span className="min-w-0">
                <span className="block text-base font-semibold text-slate-900">{title}</span>
                <span className="mt-0.5 block text-xs font-normal text-slate-500">
                  Điền thông tin và gửi liên kết kích hoạt
                </span>
              </span>
            </span>
          )
        }
        busy={isSaving}
        onClose={() => requestClose('close')}
      >
        <div className={user ? 'flex min-h-[calc(100dvh-5rem)] flex-col' : undefined}>
          {user && !editing ? (
            <>
              <UserAccountDetails user={user} accountGroup={accountGroup} />
              <div className="sticky bottom-0 mt-auto flex justify-end gap-3 border-t border-slate-100 bg-white/95 px-5 py-4 backdrop-blur-md sm:px-6">
                <Button variant="outline" onClick={() => requestClose('close')}>
                  Đóng
                </Button>
                <Button leftIcon={<Pencil />} onClick={() => setEditing(true)}>
                  Chỉnh sửa
                </Button>
              </div>
            </>
          ) : (
            <UserForm
              accountGroup={accountGroup}
              user={user}
              isSaving={isSaving}
              error={error}
              onSave={onSave}
              onCancel={() => requestClose(user ? 'cancel' : 'close')}
              onDirtyChange={setDirty}
            />
          )}
        </div>
      </Dialog>
      <ConfirmDialog
        open={discardAction !== null || blocker.state === 'blocked'}
        type="warning"
        title={isSaving ? 'Đang lưu tài khoản' : 'Bỏ thay đổi chưa lưu?'}
        description={
          isSaving
            ? 'Vui lòng chờ lưu xong trước khi rời trang.'
            : 'Thông tin bạn vừa chỉnh sửa sẽ không được lưu.'
        }
        confirmText="Bỏ thay đổi"
        cancelText={isSaving ? 'Tiếp tục chờ' : 'Tiếp tục chỉnh sửa'}
        isConfirming={isSaving}
        onClose={() => {
          setDiscardAction(null);
          if (blocker.state === 'blocked') blocker.reset();
        }}
        onConfirm={discard}
      />
    </>
  );
}
