import { useState } from 'react';

export function usePeriodModals() {
  const [isGateDetailsOpen, setIsGateDetailsOpen] = useState<boolean>(false);
  const [isImportOpen, setIsImportOpen] = useState<boolean>(false);
  const [isChangeReasonOpen, setIsChangeReasonOpen] = useState<boolean>(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  return {
    isGateDetailsOpen,
    setIsGateDetailsOpen,
    isImportOpen,
    setIsImportOpen,
    isChangeReasonOpen,
    setIsChangeReasonOpen,
    notice,
    setNotice,
    errorMessage,
    setErrorMessage,
  };
}