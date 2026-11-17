import { createContext, useContext, useEffect } from 'react';

export interface PresenceSnapshot {
  visitors: string[];
  serverTime: number;
}
export interface PresenceContext {
  snapshot: PresenceSnapshot | null;
  status: 'connecting' | 'connected' | 'error';
  watch: () => () => void;
}
export const CampusPresenceContext = createContext<PresenceContext | null>(null);

export function useCampusPresence() {
  const presence = useContext(CampusPresenceContext);
  if (!presence) throw new Error('CampusPresenceProvider is required');
  const { watch } = presence;
  useEffect(() => watch(), [watch]);
  return presence;
}
