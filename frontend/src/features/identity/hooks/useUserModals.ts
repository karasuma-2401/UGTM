import { useState } from 'react';
import type { UserSummary } from '../types/user.types';

export function useUserModals() {
  const [isImportOpen, setIsImportOpen] = useState(false);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserSummary | null>(null);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [notice, setNotice] = useState('');

  return {
    isImportOpen,
    setIsImportOpen,
    isFormOpen,
    setIsFormOpen,
    editingUser,
    setEditingUser,
    selectedRowId,
    setSelectedRowId,
    notice,
    setNotice,
  };
}