import { Dialog } from '@/components/ui/dialog';
import { UserForm } from './UserForm';
import type { UserFormValues, UserSummary } from '../types/user.types';

interface UserFormDialogProps {
  user: UserSummary | null;
  isSaving: boolean;
  error: string | null;
  onSave: (values: UserFormValues) => void;
  onClose: () => void;
}

export function UserFormDialog({ user, isSaving, error, onSave, onClose }: UserFormDialogProps) {
  return (
    <Dialog title="Quản lý tài khoản" busy={isSaving} onClose={onClose}>
      <UserForm user={user} isSaving={isSaving} error={error} onSave={onSave} onCancel={onClose} />
    </Dialog>
  );
}
