import {
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type PointerEvent as ReactPointerEvent,
} from 'react';
import { FileText, GripHorizontal, Maximize2, Minimize2, X } from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { formatFileSize } from '../../utils/milestoneFormatters';

type PreviewWindowPreset = 'compact' | 'standard' | 'wide';
type ResizeDirection = 'n' | 's' | 'e' | 'w' | 'ne' | 'nw' | 'se' | 'sw';

interface WindowRect {
  left: number;
  top: number;
  width: number;
  height: number;
}

const PREVIEW_WINDOW_PRESETS: Record<
  PreviewWindowPreset,
  { label: string; width: string; height: string }
> = {
  compact: {
    label: 'Nhỏ gọn',
    width: 'min(92vw, 768px)',
    height: 'min(64dvh, 580px)',
  },
  standard: {
    label: 'Tiêu chuẩn',
    width: 'min(94vw, 1024px)',
    height: 'min(78dvh, 760px)',
  },
  wide: {
    label: 'Mở rộng',
    width: 'min(96vw, 1360px)',
    height: 'min(88dvh, 900px)',
  },
};

const PRESET_ORDER: PreviewWindowPreset[] = ['compact', 'standard', 'wide'];
const MIN_WIDTH = 480;
const MIN_HEIGHT = 360;
const VIEWPORT_PADDING = 8;

const RESIZE_HANDLES: Array<{
  direction: ResizeDirection;
  label: string;
  className: string;
}> = [
  {
    direction: 'n',
    label: 'Kéo cạnh trên để chỉnh chiều cao',
    className:
      'top-0 right-3 left-3 z-20 h-1.5 cursor-ns-resize hover:bg-primary/20 transition-colors',
  },
  {
    direction: 's',
    label: 'Kéo cạnh dưới để chỉnh chiều cao',
    className:
      'right-3 bottom-0 left-3 z-20 h-1.5 cursor-ns-resize hover:bg-primary/20 transition-colors',
  },
  {
    direction: 'w',
    label: 'Kéo cạnh trái để chỉnh chiều rộng',
    className:
      'top-3 bottom-3 left-0 z-20 w-1.5 cursor-ew-resize hover:bg-primary/20 transition-colors',
  },
  {
    direction: 'e',
    label: 'Kéo cạnh phải để chỉnh chiều rộng',
    className:
      'top-3 right-0 bottom-3 z-20 w-1.5 cursor-ew-resize hover:bg-primary/20 transition-colors',
  },
  {
    direction: 'nw',
    label: 'Kéo góc trên-trái để thay đổi kích thước khung xem',
    className:
      'top-0 left-0 z-30 size-3 cursor-nwse-resize rounded-tl-2xl hover:bg-primary/25 transition-colors',
  },
  {
    direction: 'ne',
    label: 'Kéo góc trên-phải để thay đổi kích thước khung xem',
    className:
      'top-0 right-0 z-30 size-3 cursor-nesw-resize rounded-tr-2xl hover:bg-primary/25 transition-colors',
  },
  {
    direction: 'sw',
    label: 'Kéo góc dưới-trái để thay đổi kích thước khung xem',
    className:
      'bottom-0 left-0 z-30 size-3 cursor-nesw-resize rounded-bl-2xl hover:bg-primary/25 transition-colors',
  },
  {
    direction: 'se',
    label: 'Kéo góc dưới-phải để thay đổi kích thước khung xem trước',
    className:
      'right-0 bottom-0 z-30 flex size-5 cursor-nwse-resize items-end justify-end rounded-br-2xl p-1 text-slate-400 hover:text-slate-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
  },
];

function clamp(value: number, min: number, max: number): number {
  if (max <= min) return min;
  return Math.min(max, Math.max(min, value));
}

export interface PdfPreviewDialogProps {
  open: boolean;
  onClose: () => void;
  previewUrl: string;
  fileName?: string;
  fileSize?: number;
}

export function PdfPreviewDialog({
  open,
  onClose,
  previewUrl,
  fileName,
  fileSize,
}: PdfPreviewDialogProps) {
  const [sizePreset, setSizePreset] = useState<PreviewWindowPreset>('standard');
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [customRect, setCustomRect] = useState<WindowRect | null>(null);
  const [activeInteraction, setActiveInteraction] = useState<'move' | 'resize' | null>(null);

  const frameRef = useRef<HTMLDivElement>(null);
  const interactionCleanupRef = useRef<(() => void) | null>(null);

  useEffect(() => {
    return () => {
      interactionCleanupRef.current?.();
      interactionCleanupRef.current = null;
    };
  }, []);

  useEffect(() => {
    if (!customRect || isFullscreen) return;
    const handleWindowResize = () => {
      const vw = window.innerWidth;
      const vh = window.innerHeight;
      setCustomRect((prev) => {
        if (!prev) return prev;
        const width = Math.round(clamp(prev.width, MIN_WIDTH, vw - VIEWPORT_PADDING * 2));
        const height = Math.round(clamp(prev.height, MIN_HEIGHT, vh - VIEWPORT_PADDING * 2));
        const left = Math.round(clamp(prev.left, VIEWPORT_PADDING, vw - width - VIEWPORT_PADDING));
        const top = Math.round(clamp(prev.top, VIEWPORT_PADDING, vh - height - VIEWPORT_PADDING));
        if (
          left === prev.left &&
          top === prev.top &&
          width === prev.width &&
          height === prev.height
        ) {
          return prev;
        }
        return { left, top, width, height };
      });
    };

    window.addEventListener('resize', handleWindowResize);
    return () => window.removeEventListener('resize', handleWindowResize);
  }, [customRect, isFullscreen]);

  const handleSelectPreset = (preset: PreviewWindowPreset) => {
    setSizePreset(preset);
    setCustomRect(null);
    setIsFullscreen(false);
  };

  const handleToggleFullscreen = () => {
    setIsFullscreen((prev) => !prev);
  };

  const startPointerInteraction = (
    mode: 'move' | 'resize',
    onMove: (dx: number, dy: number, startRect: DOMRect) => void,
    event: ReactPointerEvent<HTMLElement>,
  ) => {
    if (isFullscreen) return;
    if (event.pointerType === 'mouse' && event.button !== 0) return;

    const frameEl = frameRef.current;
    if (!frameEl) return;

    event.preventDefault();
    event.stopPropagation();

    interactionCleanupRef.current?.();

    const startRect = frameEl.getBoundingClientRect();
    const startX = event.clientX;
    const startY = event.clientY;

    setActiveInteraction(mode);

    const handlePointerMove = (moveEvent: PointerEvent) => {
      onMove(moveEvent.clientX - startX, moveEvent.clientY - startY, startRect);
    };

    const stopInteraction = () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', stopInteraction);
      window.removeEventListener('pointercancel', stopInteraction);
      interactionCleanupRef.current = null;
      setActiveInteraction(null);
    };

    interactionCleanupRef.current = stopInteraction;
    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', stopInteraction);
    window.addEventListener('pointercancel', stopInteraction);
  };

  const handleStartMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (isFullscreen) return;
    if (window.innerWidth < 640) return;
    const target = event.target as HTMLElement | null;
    if (target?.closest('button, a, input, select, textarea, [data-no-drag="true"]')) {
      return;
    }

    startPointerInteraction(
      'move',
      (dx, dy, startRect) => {
        const vw = window.innerWidth;
        const vh = window.innerHeight;
        const width = Math.round(startRect.width);
        const height = Math.round(startRect.height);
        const left = Math.round(
          clamp(startRect.left + dx, VIEWPORT_PADDING, vw - width - VIEWPORT_PADDING),
        );
        const top = Math.round(
          clamp(startRect.top + dy, VIEWPORT_PADDING, vh - height - VIEWPORT_PADDING),
        );

        setCustomRect({ left, top, width, height });
      },
      event,
    );
  };

  const handleStartResize = (
    direction: ResizeDirection,
    event: ReactPointerEvent<HTMLElement>,
  ) => {
    const resizesEast = direction.includes('e');
    const resizesWest = direction.includes('w');
    const resizesSouth = direction.includes('s');
    const resizesNorth = direction.includes('n');

    startPointerInteraction(
      'resize',
      (dx, dy, startRect) => {
        const vw = window.innerWidth;
        const vh = window.innerHeight;
        const rightEdge = startRect.left + startRect.width;
        const bottomEdge = startRect.top + startRect.height;

        let nextLeft = startRect.left;
        let nextWidth = startRect.width;
        let nextTop = startRect.top;
        let nextHeight = startRect.height;

        if (resizesEast) {
          nextWidth = clamp(startRect.width + dx, MIN_WIDTH, vw - startRect.left - VIEWPORT_PADDING);
        } else if (resizesWest) {
          const maxLeft = rightEdge - MIN_WIDTH;
          nextLeft = clamp(startRect.left + dx, VIEWPORT_PADDING, maxLeft);
          nextWidth = rightEdge - nextLeft;
        }

        if (resizesSouth) {
          nextHeight = clamp(startRect.height + dy, MIN_HEIGHT, vh - startRect.top - VIEWPORT_PADDING);
        } else if (resizesNorth) {
          const maxTop = bottomEdge - MIN_HEIGHT;
          nextTop = clamp(startRect.top + dy, VIEWPORT_PADDING, maxTop);
          nextHeight = bottomEdge - nextTop;
        }

        setCustomRect({
          left: Math.round(nextLeft),
          top: Math.round(nextTop),
          width: Math.round(nextWidth),
          height: Math.round(nextHeight),
        });
      },
      event,
    );
  };

  const activePreset = PREVIEW_WINDOW_PRESETS[sizePreset];
  const isInteracting = activeInteraction !== null;

  const dialogStyle: CSSProperties | undefined = isFullscreen
    ? undefined
    : customRect
      ? {
          left: `${customRect.left}px`,
          top: `${customRect.top}px`,
          right: 'auto',
          bottom: 'auto',
          margin: 0,
          width: `min(calc(100vw - 1rem), ${customRect.width}px)`,
          height: `min(calc(100dvh - 1rem), ${customRect.height}px)`,
        }
      : {
          width: activePreset.width,
          height: activePreset.height,
        };

  const resolvedTitle = fileName || 'Xem trước tài liệu PDF';

  return (
    <Dialog
      open={open}
      onClose={onClose}
      hideHeader
      title={resolvedTitle}
      style={dialogStyle}
      className={cn(
        'flex flex-col overflow-hidden p-0 max-w-none',
        !isInteracting &&
          'transition-[width,height] duration-150 ease-out motion-reduce:transition-none',
        isFullscreen
          ? 'inset-0 m-0 h-dvh max-h-dvh w-dvw rounded-none border-0'
          : cn(
              'max-h-[calc(100dvh-1rem)] w-[calc(100%-1rem)] rounded-2xl',
              customRect && 'inset-auto m-0',
            ),
      )}
    >
      <div
        ref={frameRef}
        className={cn(
          'relative flex h-full w-full flex-col min-h-0',
          isInteracting && 'select-none',
        )}
      >
        <div
          onPointerDown={handleStartMove}
          onDoubleClick={(event) => {
            const target = event.target as HTMLElement | null;
            if (target?.closest('button, [data-no-drag="true"]')) return;
            handleToggleFullscreen();
          }}
          title={!isFullscreen ? 'Giữ chuột để kéo di chuyển cửa sổ · Nhấp đúp để toàn màn hình' : undefined}
          className={cn(
            'flex shrink-0 items-center justify-between gap-3 border-b border-slate-100 bg-white/95 px-4 py-3 sm:px-5 backdrop-blur-md',
            !isFullscreen && 'sm:cursor-grab',
            activeInteraction === 'move' && 'sm:cursor-grabbing',
          )}
        >
          <div className="flex min-w-0 flex-1 items-center gap-2.5">
            {!isFullscreen && (
              <GripHorizontal
                className="hidden sm:block size-4 shrink-0 text-slate-300"
                aria-hidden="true"
              />
            )}
            <span className="inline-flex size-7 shrink-0 items-center justify-center rounded-lg border border-rose-200/80 bg-rose-50 text-rose-600">
              <FileText className="size-4" />
            </span>
            <div className="min-w-0">
              <h3
                className="truncate text-sm font-semibold tracking-tight text-slate-900"
                title={resolvedTitle}
              >
                {resolvedTitle}
              </h3>
            </div>
            {typeof fileSize === 'number' && fileSize > 0 && (
              <span className="hidden md:inline-flex shrink-0 items-center rounded-md border border-slate-200/80 bg-slate-50 px-1.5 py-0.5 text-[11px] font-medium tabular-nums text-slate-600">
                {formatFileSize(fileSize)}
              </span>
            )}
          </div>

          <div className="flex shrink-0 items-center gap-1.5 sm:gap-2" data-no-drag="true">
            {customRect && !isFullscreen && (
              <span
                className="hidden lg:inline-flex items-center rounded-md border border-slate-200/80 bg-slate-50 px-2 py-1 text-[11px] font-medium tabular-nums text-slate-600"
                title="Kích thước cửa sổ tùy chỉnh"
              >
                {customRect.width} × {customRect.height}
              </span>
            )}

            <div
              role="group"
              aria-label="Chọn kích thước cửa sổ xem trước"
              className="hidden sm:inline-flex items-center rounded-lg border border-slate-200/80 bg-slate-100/90 p-0.5 select-none"
            >
              {PRESET_ORDER.map((presetKey) => {
                const isActive = !isFullscreen && !customRect && sizePreset === presetKey;
                return (
                  <button
                    key={presetKey}
                    type="button"
                    aria-pressed={isActive}
                    onClick={() => handleSelectPreset(presetKey)}
                    className={cn(
                      'rounded-md px-2.5 py-1 text-xs font-medium transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                      isActive
                        ? 'bg-white text-slate-900 shadow-2xs border border-slate-200/80'
                        : 'border border-transparent text-slate-600 hover:text-slate-900',
                    )}
                  >
                    {PREVIEW_WINDOW_PRESETS[presetKey].label}
                  </button>
                );
              })}
            </div>

            <Button
              type="button"
              variant="outline"
              size="sm"
              aria-pressed={isFullscreen}
              aria-label={isFullscreen ? 'Thu nhỏ cửa sổ xem trước' : 'Mở toàn màn hình'}
              onClick={handleToggleFullscreen}
              className="h-8 rounded-lg border-slate-200 bg-white px-2.5 text-xs text-slate-700 hover:bg-slate-50"
            >
              {isFullscreen ? (
                <>
                  <Minimize2 className="size-3.5 md:mr-1.5" />
                  <span className="hidden md:inline">Thu nhỏ</span>
                </>
              ) : (
                <>
                  <Maximize2 className="size-3.5 md:mr-1.5" />
                  <span className="hidden md:inline">Toàn màn hình</span>
                </>
              )}
            </Button>

            <span className="hidden sm:block h-5 w-px bg-slate-200/80" aria-hidden="true" />

            <Button
              type="button"
              variant="ghost"
              size="icon"
              aria-label="Đóng cửa sổ"
              onClick={onClose}
              className="h-8 w-8 shrink-0 rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700"
            >
              <X className="size-4" />
            </Button>
          </div>
        </div>

        <div
          className={cn(
            'relative flex-1 min-h-0 flex flex-col',
            isFullscreen ? 'bg-slate-100 p-0' : 'bg-slate-100/70 p-2.5 sm:p-3.5',
          )}
        >
          <div
            className={cn(
              'h-full w-full flex-1 min-h-0 overflow-hidden bg-white',
              isFullscreen
                ? 'rounded-none border-0'
                : 'rounded-xl border border-slate-200/90 shadow-2xs',
            )}
          >
            <iframe
              src={previewUrl}
              title={fileName || 'Xem trước PDF'}
              className={cn('h-full w-full border-0', isInteracting && 'pointer-events-none')}
            />
          </div>

          {isInteracting && <div className="absolute inset-0 z-10" aria-hidden="true" />}

          {activeInteraction === 'resize' && customRect && (
            <div className="pointer-events-none absolute bottom-5 left-1/2 z-20 -translate-x-1/2 rounded-full bg-slate-900/85 px-3 py-1 text-xs font-medium tabular-nums text-white shadow-md backdrop-blur-xs">
              {customRect.width} × {customRect.height} px
            </div>
          )}
        </div>

        {!isFullscreen &&
          RESIZE_HANDLES.map((handle) => {
            const isCornerGrip = handle.direction === 'se';
            if (isCornerGrip) {
              return (
                <button
                  key={handle.direction}
                  type="button"
                  aria-label={handle.label}
                  title={handle.label}
                  onPointerDown={(event) => handleStartResize(handle.direction, event)}
                  className={cn('hidden sm:flex absolute', handle.className)}
                >
                  <svg viewBox="0 0 12 12" className="size-3" fill="none" aria-hidden="true">
                    <path
                      d="M10.5 3.5 3.5 10.5M10.5 7.5 7.5 10.5"
                      stroke="currentColor"
                      strokeWidth="1.5"
                      strokeLinecap="round"
                    />
                  </svg>
                </button>
              );
            }

            return (
              <div
                key={handle.direction}
                role="presentation"
                data-resize-handle={handle.direction}
                aria-hidden="true"
                title={handle.label}
                onPointerDown={(event) => handleStartResize(handle.direction, event)}
                className={cn('hidden sm:block absolute', handle.className)}
              />
            );
          })}
      </div>
    </Dialog>
  );
}
