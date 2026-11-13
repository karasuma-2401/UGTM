import { toast, type ExternalToast } from 'sonner';

export type NotificationTone = 'success' | 'info' | 'warning' | 'error' | 'loading';
export interface NotificationOptions {
  id?: string;
  title?: string;
  /** Zero keeps the notification open until dismissed. */
  duration?: number;
  dedupeKey?: string;
  action?: { label: string; onClick: () => void | Promise<unknown> };
}

const titles: Record<NotificationTone, string> = {
  success: 'Thành công',
  info: 'Thông tin',
  warning: 'Cần lưu ý',
  error: 'Không thể hoàn tất',
  loading: 'Đang xử lý',
};
const duplicates = new Map<string, string>();
const pendingActions = new Set<string>();
let sequence = 0;
let generation = 0;

function forget(id: string) {
  for (const [key, value] of duplicates) if (value === id) duplicates.delete(key);
}

function show(tone: NotificationTone, message: string, options: NotificationOptions = {}) {
  if (!message.trim()) return '';
  const key =
    options.dedupeKey ?? `${tone}:${options.title ?? ''}:${message}:${options.action?.label ?? ''}`;
  const id = options.id ?? duplicates.get(key) ?? `ugtm-toast-${++sequence}`;
  forget(id);
  duplicates.set(key, id);
  const duration =
    options.duration === 0
      ? Infinity
      : (options.duration ??
        (options.action || tone === 'warning' || tone === 'error' || tone === 'loading'
          ? Infinity
          : 5000));
  const data: ExternalToast = {
    id,
    description: message,
    duration,
    testId: id,
    dismissible: true,
    onDismiss: () => forget(id),
    onAutoClose: () => forget(id),
    action: options.action
      ? {
          label: options.action.label,
          onClick(event) {
            event.preventDefault();
            if (pendingActions.has(id)) return;
            const actionGeneration = generation;
            pendingActions.add(id);
            toast.loading('Đang xử lý…', {
              id,
              description: message,
              duration: Infinity,
              dismissible: false,
              action: undefined,
              testId: id,
            });
            void Promise.resolve()
              .then(() => options.action!.onClick())
              .then(() => {
                if (actionGeneration === generation) {
                  forget(id);
                  toast.dismiss(id);
                }
              })
              .catch(() => {
                if (actionGeneration === generation)
                  show('error', 'Không thể hoàn tất thao tác. Vui lòng thử lại.', {
                    ...options,
                    id,
                  });
              })
              .finally(() => {
                if (actionGeneration === generation) pendingActions.delete(id);
              });
          },
        }
      : undefined,
  };
  toast[tone](options.title ?? titles[tone], data);
  return id;
}

/** Product policy only; Sonner owns rendering, stacking, animation and timers. */
export const notify = {
  success: (message: string, options?: NotificationOptions) => show('success', message, options),
  info: (message: string, options?: NotificationOptions) => show('info', message, options),
  warning: (message: string, options?: NotificationOptions) => show('warning', message, options),
  error: (message: string, options?: NotificationOptions) => show('error', message, options),
  loading: (message: string, options?: NotificationOptions) => show('loading', message, options),
  dismiss(id: string) {
    forget(id);
    toast.dismiss(id);
  },
  dismissAll() {
    generation++;
    duplicates.clear();
    pendingActions.clear();
    toast.dismiss();
  },
};
