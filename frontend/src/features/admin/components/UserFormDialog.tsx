import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { UserForm } from './UserForm';
import type {
UserFormValues,
UserSummary,
} from '../types/user.types';

interface UserFormDialogProps {
user: UserSummary | null;
isSaving: boolean;
error: string | null;
onSave: (values: UserFormValues) => void;
onClose: () => void;
}

export function UserFormDialog({
user,
isSaving,
error,
onSave,
onClose,
}: UserFormDialogProps) {
const dialogRef = useRef<HTMLDialogElement>(null);

useEffect(() => {
    const dialog = dialogRef.current;

    if (!dialog) return;

    // showModal giúp trình duyệt quản lý focus và chặn tương tác phía sau.
    if (!dialog.open) {
    dialog.showModal();
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
    dialog.close();
    document.body.style.overflow = previousOverflow;
    };
}, []);

return createPortal(
    <dialog
    ref={dialogRef}
    aria-label={user ? 'Chi tiết tài khoản' : 'Tạo tài khoản'}
    onCancel={(event) => {
        // Đồng bộ việc đóng bằng Escape với state của React.
        event.preventDefault();

        if (!isSaving) {
        onClose();
        }
    }}
    className="
        fixed inset-0 m-auto
        max-h-[90dvh] w-[calc(100%-2rem)] max-w-2xl
        overflow-y-auto rounded-2xl border border-slate-200
        bg-white p-0 text-slate-800 shadow-2xl
        backdrop:bg-slate-900/50 backdrop:backdrop-blur-sm
    "
    >
    <div className="flex items-center justify-between border-b border-slate-100 px-5 py-3">
        <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
        Quản lý tài khoản
        </span>

        <Button
        type="button"
        variant="ghost"
        size="icon"
        aria-label="Đóng cửa sổ tài khoản"
        disabled={isSaving}
        onClick={onClose}
        className="h-8 w-8 rounded-full text-slate-500 hover:bg-slate-100"
        >
        <X />
        </Button>
    </div>

    <UserForm
        user={user}
        isSaving={isSaving}
        error={error}
        onSave={onSave}
        onCancel={onClose}
    />
    </dialog>,
    document.body,
);
}
