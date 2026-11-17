import { useState } from 'react';

export function useScoreConfigModals() {
  const [isAddModalOpen, setIsAddModalOpen] = useState<boolean>(false);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState<boolean>(false);
  const [isCloneReasonModalOpen, setIsCloneReasonModalOpen] = useState<boolean>(false);
  const [isResetConfirmModalOpen, setIsResetConfirmModalOpen] = useState<boolean>(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  return {
    isAddModalOpen,
    setIsAddModalOpen,
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