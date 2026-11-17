import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useAuth } from '@/shared/auth';
import { API_BASE_URL } from '@/shared/api/config';
import { tokenStorage } from '@/shared/api/tokenStorage';

import { CampusPresenceContext } from './campusPresenceContext';
import type { PresenceSnapshot, PresenceContext } from './campusPresenceContext';

function visitorId() {
  try {
    const stored = localStorage.getItem('ugtm_campus_visitor');
    if (stored && /^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i.test(stored)) return stored;
    const id = crypto.randomUUID();
    localStorage.setItem('ugtm_campus_visitor', id);
    return id;
  } catch {
    // Private browsing/storage restrictions still allow this tab to participate.
    return crypto.randomUUID();
  }
}

export function CampusPresenceProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated, user } = useAuth();
  const [watchers, setWatchers] = useState(0);
  const [snapshot, setSnapshot] = useState<PresenceSnapshot | null>(null);
  const [status, setStatus] = useState<PresenceContext['status']>('connecting');
  const refresh = useRef<(() => void) | null>(null);
  const watching = useRef(watchers);
  useEffect(() => {
    watching.current = watchers;
  }, [watchers]);
  const active = isAuthenticated || watchers > 0;
  const watch = useCallback(() => {
    setWatchers((count) => count + 1);
    return () => setWatchers((count) => Math.max(0, count - 1));
  }, []);

  useEffect(() => {
    if (!active) return;
    const visitor = visitorId();
    // Each mounted connection owns its lease, including StrictMode remounts.
    const tabId = crypto.randomUUID();
    let disposed = false;
    let timer: ReturnType<typeof setTimeout>;
    let request: AbortController | undefined;
    let generation = 0;
    const url = `${API_BASE_URL}/api/campus/presence`;
    function options(leaving = false): RequestInit {
      const token = tokenStorage.getAccessToken();
      return {
        method: 'POST',
        credentials: 'omit',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify({ visitorId: visitor, tabId, leaving }),
        ...(leaving ? { keepalive: true } : {}),
      };
    }
    async function heartbeat() {
      clearTimeout(timer);
      request?.abort();
      request = new AbortController();
      const current = ++generation;
      try {
        const response = await fetch(url, {
          ...options(),
          signal: AbortSignal.any([request.signal, AbortSignal.timeout(8000)]),
        });
        if (!response.ok) throw new Error('Presence unavailable');
        const data = (await response.json()) as PresenceSnapshot;
        if (
          !Array.isArray(data.visitors) ||
          !data.visitors.every((id) => typeof id === 'string') ||
          !Number.isFinite(data.serverTime)
        )
          throw new Error('Invalid presence snapshot');
        if (disposed || current !== generation) return;
        setSnapshot(data);
        setStatus('connected');
      } catch {
        if (disposed || current !== generation) return;
        setSnapshot(null);
        setStatus('error');
      } finally {
        if (!disposed && current === generation)
          timer = setTimeout(
            () => void heartbeat(),
            watching.current > 0 && !document.hidden ? 3000 : 15000,
          );
      }
    }
    function leave() {
      generation += 1;
      clearTimeout(timer);
      request?.abort();
      // Best effort on unload. The server also expires abruptly disconnected clients.
      void fetch(url, options(true)).catch(() => {});
    }
    function resume() {
      if (!document.hidden) void heartbeat();
    }
    refresh.current = () => void heartbeat();
    window.addEventListener('pagehide', leave);
    window.addEventListener('pageshow', resume);
    document.addEventListener('visibilitychange', resume);
    void heartbeat();
    return () => {
      disposed = true;
      refresh.current = null;
      window.removeEventListener('pagehide', leave);
      window.removeEventListener('pageshow', resume);
      document.removeEventListener('visibilitychange', resume);
      leave();
    };
  }, [active]);

  useEffect(() => {
    refresh.current?.();
  }, [isAuthenticated, user?.id]);

  return (
    <CampusPresenceContext.Provider value={{ snapshot, status, watch }}>
      {children}
    </CampusPresenceContext.Provider>
  );
}
