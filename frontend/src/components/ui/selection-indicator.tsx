import { useLayoutEffect, useRef } from 'react';
import { cn } from '@/lib/utils';

type Bounds = { x: number; y: number; width: number; height: number };
// Identity tabs live in different route pages, so their highlight needs a short handoff.
const handoffs = new Map<string, { bounds: Bounds; at: number }>();

interface SelectionIndicatorProps {
  activeKey: string | number | null | undefined;
  selector: string;
  className?: string;
  variant?: 'surface' | 'rail';
  continuityKey?: string;
}

export function SelectionIndicator({
  activeKey,
  selector,
  className,
  variant = 'surface',
  continuityKey,
}: SelectionIndicatorProps) {
  const ref = useRef<HTMLSpanElement>(null);
  const previous = useRef<Bounds | null>(null);

  useLayoutEffect(() => {
    const indicator = ref.current;
    const parent = indicator?.parentElement;
    if (!indicator || !parent) return;
    let animation: Animation | undefined;
    const media = window.matchMedia('(prefers-reduced-motion: reduce)');
    const boundsOf = (element: Element): Bounds => {
      const rect = element.getBoundingClientRect();
      const root = parent.getBoundingClientRect();
      return {
        x: rect.left - root.left + parent.scrollLeft - parent.clientLeft,
        y: rect.top - root.top + parent.scrollTop - parent.clientTop,
        width: rect.width,
        height: rect.height,
      };
    };
    const update = (animate: boolean) => {
      const target = parent.querySelector<HTMLElement>(selector);
      if (!target || !target.getClientRects().length) {
        indicator.hidden = true;
        previous.current = null;
        return;
      }
      let next = boundsOf(target);
      if (variant === 'rail')
        next = { x: 0, y: next.y + 6, width: 4, height: Math.max(0, next.height - 12) };
      if (
        !animate &&
        previous.current &&
        Object.keys(next).every(
          (key) => next[key as keyof Bounds] === previous.current![key as keyof Bounds],
        )
      )
        return;
      const cached = continuityKey ? handoffs.get(continuityKey) : undefined;
      const from =
        animation?.playState === 'running'
          ? boundsOf(indicator)
          : (previous.current ?? (cached && Date.now() - cached.at < 1200 ? cached.bounds : null));
      animation?.cancel();
      indicator.hidden = false;
      indicator.style.width = `${next.width}px`;
      indicator.style.height = `${next.height}px`;
      indicator.style.transform = `translate(${next.x}px, ${next.y}px)`;
      // Only the overlay transforms; text, table geometry and button hit areas stay still.
      if (animate && from && !media.matches && next.width > 0 && next.height > 0) {
        animation = indicator.animate(
          [
            {
              transform: `translate(${from.x}px, ${from.y}px) scale(${from.width / next.width}, ${from.height / next.height})`,
            },
            { transform: `translate(${next.x}px, ${next.y}px) scale(1, 1)` },
          ],
          { duration: 360, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' },
        );
      }
      previous.current = next;
    };
    update(true);
    const resize = new ResizeObserver(() => update(false));
    resize.observe(parent);
    parent.querySelectorAll('[data-selection-item]').forEach((element) => resize.observe(element));
    const preferenceChanged = () => {
      animation?.cancel();
      update(false);
    };
    media.addEventListener('change', preferenceChanged);
    return () => {
      // Preserve the current frame when another selection interrupts the slide.
      if (!indicator.hidden && previous.current) {
        previous.current = boundsOf(indicator);
        if (continuityKey)
          handoffs.set(continuityKey, { bounds: previous.current, at: Date.now() });
      }
      animation?.cancel();
      resize.disconnect();
      media.removeEventListener('change', preferenceChanged);
    };
  }, [activeKey, selector, variant, continuityKey]);

  return (
    <span
      ref={ref}
      hidden
      aria-hidden="true"
      data-selection-indicator={variant}
      className={cn('pointer-events-none absolute left-0 top-0 origin-top-left', className)}
    />
  );
}
