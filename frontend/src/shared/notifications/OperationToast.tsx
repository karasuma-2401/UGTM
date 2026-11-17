import { useEffect, useRef } from 'react';
import { notify, type NotificationTone } from './notify';

// Bridges existing feature outcome state without making the shared API client guess business outcomes.
export function OperationToast({
  message,
  tone = 'success',
  onConsumed,
}: {
  message?: string | null;
  tone?: NotificationTone;
  onConsumed?: () => void;
}) {
  const previous = useRef<string | null>(null);
  useEffect(() => {
    if (!message) {
      previous.current = null;
      return;
    }
    if (previous.current === `${tone}:${message}`) return;
    previous.current = `${tone}:${message}`;
    notify[tone](message);
    onConsumed?.();
  }, [message, tone, onConsumed]);
  return null;
}
