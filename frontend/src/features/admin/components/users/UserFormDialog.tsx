import { Dialog } from '@/components/ui/dialog';
import { UserForm } from './UserForm';
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
  return (
    <Dialog
      title={accountGroup === 'Student' ? 'Tài khoản sinh viên' : 'Tài khoản giảng viên & cán bộ'}
      busy={isSaving}
      onClose={onClose}
    >
      <UserForm
        accountGroup={accountGroup}
        user={user}
        isSaving={isSaving}
        error={error}
        onSave={onSave}
        onCancel={onClose}
      />
    </Dialog>
  );
}
