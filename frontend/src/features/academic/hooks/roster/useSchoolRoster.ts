import { useCallback, useEffect, useRef } from 'react';
import { useRosterData } from './useRosterData';
import { useRosterImport } from './useRosterImport';

export function useSchoolRoster() {
  const setImportErrorRef = useRef<((msg: string | null) => void) | null>(null);

  const handleDataError = useCallback((msg: string) => {
    setImportErrorRef.current?.(msg);
  }, []);

  const data = useRosterData(handleDataError);
  const roster = useRosterImport(data.existingUsers, data.refreshUsers);

  useEffect(() => {
    setImportErrorRef.current = roster.setErrorMessage;
  }, [roster.setErrorMessage]);

  return {
    data,
    roster,
  };
}

