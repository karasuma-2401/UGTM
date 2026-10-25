import { useRosterData } from './useRosterData';
import { useRosterImport } from './useRosterImport';

export function useSchoolRoster() {
  const data = useRosterData(() => {});
  const roster = useRosterImport(data.existingUsers, data.refreshUsers);

  return {
    data,
    roster,
  };
}

