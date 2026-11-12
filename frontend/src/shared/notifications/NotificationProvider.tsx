import { useLayoutEffect, useRef, useState, useSyncExternalStore, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { CircleAlert, CircleCheck, CircleX, Info, Loader2 } from 'lucide-react';
import { Toaster, toast, useSonner, type ToastT } from 'sonner';
import { getNotificationDialog, subscribeNotificationDialog } from './dialog-host';
import { notify } from './notify';
import { useToastCountdown } from './useToastCountdown';

function NotificationIcon({ children }: { children: ReactNode }) {
  return (
    <>
      {children}
      <span
        aria-hidden="true"
        className="pointer-events-none absolute inset-x-0 bottom-0 hidden h-[3px] bg-slate-100 group-data-[countdown=true]/toast:block"
      >
        <span
          data-toast-countdown=""
          className="block size-full origin-left bg-[var(--toast-accent)]"
        />
      </span>
    </>
  );
}

function setDuration(item: ToastT, duration: number) {
  const options = { ...item, duration };
  switch (item.type) {
    case 'success':
      toast.success(item.title, options);
      break;
    case 'error':
      toast.error(item.title, options);
      break;
    case 'warning':
      toast.warning(item.title, options);
      break;
    case 'info':
      toast.info(item.title, options);
      break;
    case 'loading':
      toast.loading(item.title, options);
      break;
    default:
      toast.message(item.title, options);
  }
}

function NotificationViewport() {
  const dialog = useSyncExternalStore(subscribeNotificationDialog, getNotificationDialog);
  const { toasts } = useSonner();
  const [host] = useState(() => {
    const element = document.createElement('div');
    element.setAttribute('popover', 'manual');
    element.className =
      'fixed inset-0 m-0 h-dvh w-screen max-h-none max-w-none overflow-visible border-0 bg-transparent p-0 pointer-events-none backdrop:bg-transparent backdrop:pointer-events-none';
    return element;
  });
  const previousFocus = useRef<HTMLElement | null>(null);
  const focusedDurations = useRef(new Map<string | number, number>());
  useToastCountdown(host, toasts);

  // Move a stable portal into the native dialog's top layer without remounting Sonner.
  useLayoutEffect(() => {
    const parent = dialog ?? document.body;
    if (host.parentElement !== parent) {
      if (host.matches(':popover-open')) host.hidePopover();
      parent.appendChild(host);
    }
    if (toasts.length && host.showPopover && !host.matches(':popover-open')) host.showPopover();
  }, [dialog, host, toasts.length]);
  useLayoutEffect(() => () => host.remove(), [host]);

  useLayoutEffect(() => {
    if (!host.contains(document.activeElement)) return;
    for (const item of toasts) {
      if (Number.isFinite(item.duration) && !focusedDurations.current.has(item.id)) {
        focusedDurations.current.set(item.id, item.duration!);
        setDuration(item, Infinity);
      }
    }
  }, [host, toasts]);

  const resumeFocusTimers = () => {
    for (const item of toast.getToasts()) {
      const duration = focusedDurations.current.get(item.id);
      if (duration !== undefined && 'title' in item) setDuration(item, duration);
    }
    focusedDurations.current.clear();
  };

  return createPortal(
    <div
      onFocusCapture={(event) => {
        if (event.currentTarget.contains(event.relatedTarget as Node)) return;
        previousFocus.current = event.relatedTarget as HTMLElement | null;
        // Sonner pauses hover/hidden tabs. Keyboard readers also get an uninterrupted reading period.
        for (const item of toast.getToasts()) {
          if ('title' in item && Number.isFinite(item.duration)) {
            focusedDurations.current.set(item.id, item.duration!);
            setDuration(item, Infinity);
          }
        }
      }}
      onBlurCapture={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node)) resumeFocusTimers();
      }}
      onKeyDownCapture={(event) => {
        if (event.key !== 'Escape') return;
        event.preventDefault();
        event.stopPropagation();
        const card = (event.target as HTMLElement).closest<HTMLElement>('[data-sonner-toast]');
        if (card?.dataset.testid && card.dataset.dismissible !== 'false')
          notify.dismiss(card.dataset.testid);
        else if (!card) notify.dismissAll();
        if (previousFocus.current?.isConnected) previousFocus.current.focus();
      }}
    >
      <Toaster
        position="top-right"
        theme="light"
        hotkey={['F8']}
        visibleToasts={3}
        duration={5000}
        gap={12}
        closeButton
        containerAriaLabel="Thông báo gần đây"
        customAriaLabel="Thông báo gần đây — nhấn F8 để xem"
        offset="max(1rem, env(safe-area-inset-top))"
        mobileOffset="16px"
        className="min-[601px]:!w-[400px] pointer-events-auto"
        icons={{
          success: (
            <NotificationIcon>
              <CircleCheck
                aria-hidden="true"
                className="size-[22px] fill-emerald-700 text-white"
                strokeWidth={2.5}
              />
            </NotificationIcon>
          ),
          info: (
            <NotificationIcon>
              <Info
                aria-hidden="true"
                className="size-[22px] fill-blue-600 text-white"
                strokeWidth={2.5}
              />
            </NotificationIcon>
          ),
          warning: (
            <NotificationIcon>
              <CircleAlert
                aria-hidden="true"
                className="size-[22px] fill-amber-700 text-white"
                strokeWidth={2.5}
              />
            </NotificationIcon>
          ),
          error: (
            <NotificationIcon>
              <CircleX
                aria-hidden="true"
                className="size-[22px] fill-rose-700 text-white"
                strokeWidth={2.5}
              />
            </NotificationIcon>
          ),
          loading: (
            <Loader2
              aria-hidden="true"
              className="size-5 animate-spin motion-reduce:animate-none"
            />
          ),
        }}
        toastOptions={{
          unstyled: true,
          closeButtonAriaLabel: 'Đóng thông báo',
          classNames: {
            toast:
              'group/toast relative flex w-full flex-wrap items-center gap-x-3 gap-y-2 overflow-hidden rounded-xl border border-slate-200/80 bg-white p-4 pr-11 text-slate-900 shadow-[0_4px_16px_rgba(15,23,42,0.08)] pointer-events-auto',
            content: 'min-w-0 flex-1',
            title: 'text-sm font-semibold leading-5 text-slate-900',
            description: 'mt-1 text-sm leading-6 text-slate-600 [overflow-wrap:anywhere]',
            icon: 'flex shrink-0 items-center justify-center text-[var(--toast-accent)]',
            closeButton:
              'absolute right-0 top-0 flex size-11 items-center justify-center rounded-lg border-0 bg-transparent text-slate-500 cursor-pointer hover:bg-slate-100 hover:text-slate-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary sm:right-1 sm:top-1 sm:size-7',
            actionButton:
              'ml-8 min-h-11 rounded-md bg-transparent px-2 py-1 text-sm font-semibold text-[var(--toast-accent)] underline underline-offset-4 cursor-pointer focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary sm:min-h-9',
            success: '[--toast-accent:theme(colors.emerald.700)]',
            info: '[--toast-accent:theme(colors.blue.600)]',
            warning: '[--toast-accent:theme(colors.amber.700)]',
            error: '[--toast-accent:theme(colors.rose.700)]',
            loading: '[--toast-accent:theme(colors.blue.600)]',
            default: '[--toast-accent:theme(colors.blue.600)]',
          },
        }}
      />
    </div>,
    host,
  );
}

export function NotificationProvider({ children }: { children: ReactNode }) {
  return (
    <>
      {children}
      <NotificationViewport />
    </>
  );
}
