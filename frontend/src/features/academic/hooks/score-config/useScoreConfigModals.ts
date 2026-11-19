import { useState } from 'react';
import type { ScoreComponentInput } from '../../types/scoreConfig.types';

export interface ScoreComponentTarget {
  component: ScoreComponentInput;
  index: number;
}

export function useScoreConfigModals() {
  const [isAddModalOpen, setIsAddModalOpen] = useState<boolean>(false);
  const [editingComponentTarget, setEditingComponentTarget] =
    useState<ScoreComponentTarget | null>(null);
  const [deletingComponentTarget, setDeletingComponentTarget] =
    useState<ScoreComponentTarget | null>(null);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState<boolean>(false);
  const [isCloneReasonModalOpen, setIsCloneReasonModalOpen] = useState<boolean>(false);
  const [isResetConfirmModalOpen, setIsResetConfirmModalOpen] = useState<boolean>(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  return {
    isAddModalOpen,
    setIsAddModalOpen,
    editingComponentTarget,
    setEditingComponentTarget,
    deletingComponentTarget,
    setDeletingComponentTarget,
    isHistoryModalOpen,
    setIsHistoryModalOpen,
    isCloneReasonModalOpen,
    setIsCloneReasonModalOpen,
    isResetConfirmModalOpen,
    setIsResetConfirmModalOpen,
    notice,
    setNotice,
    errorMessage,
    setErrorMessage,
  };
}
