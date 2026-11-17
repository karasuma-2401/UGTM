import { useLayoutEffect, useRef } from 'react';
import type { ToastT } from 'sonner';

interface Countdown {
  element: HTMLElement;
  item: ToastT;
  duration: number;
  remaining: number;
  animation: Animation;
  paused: boolean;
  playedAt: number;
}

/** Mirrors Sonner's pause/update states visually; Sonner still owns auto-dismissal. */
export function useToastCountdown(host: HTMLElement, toasts: ToastT[]) {
  const items = useRef(toasts);
  const synchronize = useRef<(() => void) | null>(null);
  useLayoutEffect(() => {
    items.current = toasts;
    synchronize.current?.();
  }, [toasts]);
  useLayoutEffect(() => {
    const countdowns = new Map<string, Countdown>();
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    let interacting = false;

    function animate(progress: HTMLElement, duration: number, remaining: number) {
      return progress.animate(
        [{ transform: `scaleX(${remaining / duration})` }, { transform: 'scaleX(0)' }],
        {
          duration: remaining,
          fill: 'forwards',
          easing: reducedMotion.matches
            ? `steps(${Math.max(1, Math.ceil(remaining / 1000))}, end)`
            : 'linear',
        },
      );
    }

    function sync() {
      const active = new Set<HTMLElement>();
      for (const element of host.querySelectorAll<HTMLElement>('[data-sonner-toast]')) {
        const id = element.dataset.testid;
        const item = items.current.find((entry) => entry.id === id);
        const progress = element.querySelector<HTMLElement>('[data-toast-countdown]');
        if (!id || !item || !progress || element.dataset.removed === 'true') continue;
        active.add(element);
        const previous = countdowns.get(id);
        const duration = item.duration ?? 5000;
        const keyboardPaused = host.contains(document.activeElement);
        const finite = Number.isFinite(duration) && duration > 0 && item.type !== 'loading';
        if (!finite && !(previous && keyboardPaused && item.type !== 'loading')) {
          previous?.animation.cancel();
          countdowns.delete(id);
          element.removeAttribute('data-countdown');
          continue;
        }

        let countdown = previous;
        if (!countdown || countdown.element !== element) {
          if (!finite) continue;
          previous?.animation.cancel();
          element.setAttribute('data-countdown', 'true');
          const animation = animate(progress, duration, duration);
          countdown = {
            element,
            item,
            duration,
            remaining: duration,
            animation,
            paused: false,
            playedAt: 0,
          };
          countdowns.set(id, countdown);
        }

        if (countdown.item !== item) {
          // Sonner restarts its remaining timer on an update, and resets it when duration changes.
          if (finite) {
            if (countdown.item.duration !== item.duration) {
              countdown.duration = duration;
              countdown.remaining = duration;
            }
            countdown.animation.cancel();
            countdown.animation = animate(progress, countdown.duration, countdown.remaining);
            countdown.playedAt = 0;
            countdown.paused = false;
          }
          countdown.item = item;
        }

        const paused =
          document.hidden ||
          interacting ||
          keyboardPaused ||
          element.dataset.expanded === 'true' ||
          !finite;
        if (paused && !countdown.paused) {
          countdown.remaining = Math.max(
            0,
            countdown.remaining -
              (Number(countdown.animation.currentTime ?? 0) - countdown.playedAt),
          );
          countdown.animation.pause();
        } else if (!paused && countdown.paused) {
          countdown.playedAt = Number(countdown.animation.currentTime ?? 0);
          countdown.animation.play();
        }
        countdown.paused = paused;
      }
      for (const [id, countdown] of countdowns) {
        if (active.has(countdown.element)) continue;
        countdown.animation.cancel();
        countdown.element.removeAttribute('data-countdown');
        countdowns.delete(id);
      }
    }

    const observer = new MutationObserver(sync);
    observer.observe(host, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['data-expanded', 'data-removed'],
    });
    const pointerDown = () => {
      interacting = true;
      sync();
    };
    const pointerUp = () => {
      interacting = false;
      sync();
    };
    host.addEventListener('pointerdown', pointerDown);
    host.addEventListener('pointerup', pointerUp);
    host.addEventListener('pointercancel', pointerUp);
    document.addEventListener('visibilitychange', sync);
    host.addEventListener('focusin', sync);
    host.addEventListener('focusout', sync);
    synchronize.current = sync;
    sync();
    return () => {
      observer.disconnect();
      synchronize.current = null;
      host.removeEventListener('pointerdown', pointerDown);
      host.removeEventListener('pointerup', pointerUp);
      host.removeEventListener('pointercancel', pointerUp);
      document.removeEventListener('visibilitychange', sync);
      host.removeEventListener('focusin', sync);
      host.removeEventListener('focusout', sync);
      for (const countdown of countdowns.values()) {
        countdown.animation.cancel();
        countdown.element.removeAttribute('data-countdown');
      }
    };
  }, [host]);
}
