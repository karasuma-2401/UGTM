import * as React from 'react';
import { createPortal } from 'react-dom';
import {
  ArrowDown,
  ArrowUp,
  ChevronDown,
  GripVertical,
  Plus,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

export interface ComposerBlockItem<TKind extends string = string> {
  id: string;
  kind: TKind;
}

export interface ComposerBlockTypeOption<TKind extends string = string> {
  kind: TKind;
  label: string;
  description?: string;
  icon?: React.ReactNode;
  disabled?: boolean;
  disabledReason?: string;
  badgeText?: string;
}

export interface BlockRenderContext {
  index: number;
  total: number;
  isFirst: boolean;
  isLast: boolean;
  moveUp: () => void;
  moveDown: () => void;
  remove: () => void;
}

export interface BlockComposerProps<
  TKind extends string = string,
  TBlock extends ComposerBlockItem<TKind> = ComposerBlockItem<TKind>,
> {
  title?: React.ReactNode;
  subtitle?: string;
  headerMeta?: React.ReactNode;
  blocks: TBlock[];
  blockTypes: ComposerBlockTypeOption<TKind>[];
  onReorder: (fromIndex: number, toIndex: number) => void;
  onInsertBlock: (kind: TKind, atIndex?: number) => void;
  onRemoveBlock: (id: string) => void;
  canRemoveBlock?: (block: TBlock) => boolean;
  renderBlock: (block: TBlock, context: BlockRenderContext) => React.ReactNode;
  onPasteFiles?: (files: File[]) => void;
  onDropFiles?: (files: File[]) => void;
  addButtonLabel?: string;
  emptyStateIcon?: React.ReactNode;
  emptyStateLabel?: string;
  emptyStateHint?: string;
  disabled?: boolean;
  className?: string;
}

interface AddMenuState {
  insertAtIndex: number;
  anchorEl: HTMLElement;
}

interface FloatingCoords {
  top: number;
  left: number;
  width: number;
  placement: 'bottom' | 'top';
}

function computeFloatingCoords(anchorEl: HTMLElement): FloatingCoords {
  const rect = anchorEl.getBoundingClientRect();
  const estimatedHeight = 190;
  const spaceBelow = window.innerHeight - rect.bottom;
  const placement: 'bottom' | 'top' =
    spaceBelow < estimatedHeight && rect.top > estimatedHeight ? 'top' : 'bottom';
  const width = Math.min(320, Math.max(260, rect.width));
  const maxLeft = Math.max(8, window.innerWidth - width - 8);
  const alignRightLeft = rect.right - width;
  const preferredLeft = alignRightLeft >= 8 ? alignRightLeft : rect.left;
  const left = Math.min(Math.max(8, preferredLeft), maxLeft);
  const top = placement === 'bottom' ? rect.bottom + 6 : rect.top - 6;
  return { top, left, width, placement };
}

function extractClipboardFiles(clipboardData: DataTransfer | null): File[] {
  if (!clipboardData) return [];
  const files: File[] = [];
  if (clipboardData.files && clipboardData.files.length > 0) {
    files.push(...Array.from(clipboardData.files));
  } else if (clipboardData.items) {
    for (const item of Array.from(clipboardData.items)) {
      if (item.kind === 'file') {
        const f = item.getAsFile();
        if (f) files.push(f);
      }
    }
  }
  return files;
}

export function BlockComposer<
  TKind extends string = string,
  TBlock extends ComposerBlockItem<TKind> = ComposerBlockItem<TKind>,
>({
  title,
  subtitle,
  headerMeta,
  blocks,
  blockTypes,
  onReorder,
  onInsertBlock,
  onRemoveBlock,
  canRemoveBlock,
  renderBlock,
  onPasteFiles,
  onDropFiles,
  addButtonLabel = 'Thêm mục',
  emptyStateIcon,
  emptyStateLabel = 'Chưa có nội dung nào. Nhấn để thêm liên kết hoặc ảnh minh chứng…',
  emptyStateHint,
  disabled = false,
  className,
}: BlockComposerProps<TKind, TBlock>) {
  const menuRef = React.useRef<HTMLDivElement | null>(null);

  const [addMenu, setAddMenu] = React.useState<AddMenuState | null>(null);
  const [menuCoords, setMenuCoords] = React.useState<FloatingCoords | null>(null);
  const [draggedBlockId, setDraggedBlockId] = React.useState<string | null>(null);
  const [dropTarget, setDropTarget] = React.useState<{
    id: string;
    position: 'before' | 'after';
  } | null>(null);
  const [isExternalFileDrag, setIsExternalFileDrag] = React.useState(false);

  const closeAddMenu = React.useCallback(() => {
    setAddMenu(null);
    setMenuCoords(null);
  }, []);

  const toggleAddMenuAt = React.useCallback(
    (anchorEl: HTMLElement | null, insertAtIndex: number) => {
      if (disabled || !anchorEl) return;
      if (addMenu && addMenu.anchorEl === anchorEl) {
        closeAddMenu();
        return;
      }
      setAddMenu({ anchorEl, insertAtIndex });
      setMenuCoords(computeFloatingCoords(anchorEl));
    },
    [addMenu, closeAddMenu, disabled],
  );

  React.useEffect(() => {
    if (!addMenu) return;
    const updatePosition = () => {
      if (addMenu.anchorEl.isConnected) {
        setMenuCoords(computeFloatingCoords(addMenu.anchorEl));
      } else {
        closeAddMenu();
      }
    };
    const handleMouseDown = (event: MouseEvent) => {
      const target = event.target as Node;
      if (menuRef.current?.contains(target) || addMenu.anchorEl.contains(target)) {
        return;
      }
      closeAddMenu();
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        closeAddMenu();
      }
    };

    window.addEventListener('resize', updatePosition);
    window.addEventListener('scroll', updatePosition, true);
    document.addEventListener('mousedown', handleMouseDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('resize', updatePosition);
      window.removeEventListener('scroll', updatePosition, true);
      document.removeEventListener('mousedown', handleMouseDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [addMenu, closeAddMenu]);

  const handleSelectOption = (opt: ComposerBlockTypeOption<TKind>) => {
    if (opt.disabled || disabled) return;
    const insertIndex = addMenu?.insertAtIndex ?? blocks.length;
    closeAddMenu();
    onInsertBlock(opt.kind, insertIndex);
  };

  const handleCanvasPaste = (event: React.ClipboardEvent<HTMLDivElement>) => {
    if (disabled || !onPasteFiles) return;
    const files = extractClipboardFiles(event.clipboardData);
    if (files.length === 0) return;
    event.preventDefault();
    onPasteFiles(files);
  };

  const handleCanvasDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    if (disabled || draggedBlockId || !onDropFiles) return;
    if (event.dataTransfer.types.includes('Files')) {
      event.preventDefault();
      if (!isExternalFileDrag) setIsExternalFileDrag(true);
    }
  };

  const handleCanvasDragLeave = (event: React.DragEvent<HTMLDivElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setIsExternalFileDrag(false);
    }
  };

  const handleCanvasDrop = (event: React.DragEvent<HTMLDivElement>) => {
    if (disabled || draggedBlockId || !onDropFiles) return;
    const files = Array.from(event.dataTransfer.files ?? []);
    setIsExternalFileDrag(false);
    if (files.length === 0) return;
    event.preventDefault();
    onDropFiles(files);
  };

  const canDragBlocks = !disabled && blocks.length > 1;
  const allOptionsDisabled = blockTypes.every((opt) => opt.disabled);

  return (
    <div
      onPaste={handleCanvasPaste}
      onDragOver={handleCanvasDragOver}
      onDragLeave={handleCanvasDragLeave}
      onDrop={handleCanvasDrop}
      className={cn(
        'space-y-3 rounded-xl transition-colors',
        isExternalFileDrag && 'bg-blue-50/40 ring-2 ring-[#2054a5]/35 p-2 -m-2',
        className,
      )}
    >
      {(title || subtitle || headerMeta || !disabled) && (
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              {title && <h4 className="text-xs font-semibold text-slate-800">{title}</h4>}
              {headerMeta}
            </div>
            {subtitle && (
              <p className="text-[11px] text-slate-500 mt-0.5 leading-relaxed">{subtitle}</p>
            )}
          </div>

          {!disabled && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={allOptionsDisabled}
              aria-haspopup="menu"
              aria-expanded={Boolean(addMenu)}
              onClick={(event) => toggleAddMenuAt(event.currentTarget, blocks.length)}
              className="h-8 gap-1.5 bg-white text-xs shrink-0"
            >
              <Plus className="size-3.5" />
              <span>{addButtonLabel}</span>
              <ChevronDown className="size-3 opacity-60" />
            </Button>
          )}
        </div>
      )}

      {blocks.length === 0 ? (
        <button
          type="button"
          disabled={disabled || allOptionsDisabled}
          onClick={(event) => toggleAddMenuAt(event.currentTarget, 0)}
          className="w-full rounded-xl border border-dashed border-slate-200/90 bg-slate-50/45 px-4 py-3 text-left text-xs text-slate-600 transition-colors hover:border-[#2054a5]/50 hover:bg-blue-50/20 flex flex-wrap items-center justify-between gap-2 disabled:opacity-60 disabled:cursor-not-allowed cursor-pointer"
        >
          <span className="inline-flex items-center gap-2.5 min-w-0">
            {emptyStateIcon && (
              <span className="size-8 rounded-lg bg-white border border-slate-200/80 flex items-center justify-center text-slate-500 shrink-0 shadow-2xs">
                {emptyStateIcon}
              </span>
            )}
            <span className="min-w-0">
              <span className="font-medium text-slate-700 block">{emptyStateLabel}</span>
              {emptyStateHint && (
                <span className="text-[11px] text-slate-500 block mt-0.5">{emptyStateHint}</span>
              )}
            </span>
          </span>
          {!disabled && (
            <span className="text-xs font-medium text-[#2054a5] shrink-0">+ Thêm</span>
          )}
        </button>
      ) : (
        <div className="space-y-2" role="list" aria-label="Danh sách nội dung đính kèm">
          {blocks.map((block, index) => {
            const removable = !disabled && (canRemoveBlock ? canRemoveBlock(block) : true);
            const isDragging = draggedBlockId === block.id;
            const isDropBefore = dropTarget?.id === block.id && dropTarget.position === 'before';
            const isDropAfter = dropTarget?.id === block.id && dropTarget.position === 'after';

            const moveUp = () => {
              if (!disabled && index > 0) onReorder(index, index - 1);
            };
            const moveDown = () => {
              if (!disabled && index < blocks.length - 1) onReorder(index, index + 1);
            };
            const remove = () => {
              if (removable) onRemoveBlock(block.id);
            };

            return (
              <div
                key={block.id}
                role="listitem"
                data-block-row={block.id}
                data-block-kind={block.kind}
                onDragOver={(event) => {
                  if (!draggedBlockId || draggedBlockId === block.id) return;
                  event.preventDefault();
                  const rect = event.currentTarget.getBoundingClientRect();
                  const position =
                    event.clientY - rect.top < rect.height / 2 ? 'before' : 'after';
                  if (dropTarget?.id !== block.id || dropTarget.position !== position) {
                    setDropTarget({ id: block.id, position });
                  }
                }}
                onDrop={(event) => {
                  if (!draggedBlockId || draggedBlockId === block.id) return;
                  event.preventDefault();
                  event.stopPropagation();
                  const fromIndex = blocks.findIndex((b) => b.id === draggedBlockId);
                  if (fromIndex !== -1) {
                    const pos = dropTarget?.id === block.id ? dropTarget.position : 'after';
                    let toIndex = pos === 'after' ? index + 1 : index;
                    if (fromIndex < toIndex) toIndex -= 1;
                    toIndex = Math.max(0, Math.min(blocks.length - 1, toIndex));
                    if (fromIndex !== toIndex) {
                      onReorder(fromIndex, toIndex);
                    }
                  }
                  setDraggedBlockId(null);
                  setDropTarget(null);
                }}
                className={cn(
                  'group/block relative flex items-start gap-2 rounded-xl border bg-slate-50/55 px-2.5 py-2.5 transition-[border-color,box-shadow,opacity,background-color] duration-150',
                  isDragging
                    ? 'opacity-45 border-[#2054a5]/60 bg-blue-50/30'
                    : 'border-slate-200/80 hover:border-slate-300/90 focus-within:border-[#2054a5]/60 focus-within:bg-white',
                )}
              >
                {isDropBefore && (
                  <div
                    aria-hidden="true"
                    className="pointer-events-none absolute inset-x-2 -top-[5px] h-0.5 rounded-full bg-[#2054a5]"
                  />
                )}
                {isDropAfter && (
                  <div
                    aria-hidden="true"
                    className="pointer-events-none absolute inset-x-2 -bottom-[5px] h-0.5 rounded-full bg-[#2054a5]"
                  />
                )}

                {blocks.length > 1 && (
                  <button
                    type="button"
                    draggable={canDragBlocks}
                    disabled={!canDragBlocks}
                    aria-label={`Kéo để đổi vị trí mục ${index + 1}/${blocks.length} (hoặc phím mũi tên lên/xuống)`}
                    title="Kéo để sắp xếp thứ tự"
                    onDragStart={(event) => {
                      if (!canDragBlocks) return;
                      setDraggedBlockId(block.id);
                      event.dataTransfer.effectAllowed = 'move';
                      event.dataTransfer.setData('text/plain', block.id);
                      const rowEl = event.currentTarget.closest('[data-block-row]');
                      if (rowEl instanceof HTMLElement) {
                        event.dataTransfer.setDragImage(rowEl, 20, 20);
                      }
                    }}
                    onDragEnd={() => {
                      setDraggedBlockId(null);
                      setDropTarget(null);
                    }}
                    onKeyDown={(event) => {
                      if (!canDragBlocks) return;
                      if (event.key === 'ArrowUp' && index > 0) {
                        event.preventDefault();
                        onReorder(index, index - 1);
                      } else if (event.key === 'ArrowDown' && index < blocks.length - 1) {
                        event.preventDefault();
                        onReorder(index, index + 1);
                      }
                    }}
                    className={cn(
                      'mt-1.5 size-6 rounded-md inline-flex items-center justify-center text-slate-300 transition-colors shrink-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                      canDragBlocks
                        ? 'group-hover/block:text-slate-500 hover:bg-slate-200/60 hover:text-slate-700 cursor-grab active:cursor-grabbing'
                        : 'opacity-35 cursor-default',
                    )}
                  >
                    <GripVertical className="size-3.5" />
                  </button>
                )}

                <div className="min-w-0 flex-1">
                  {renderBlock(block, {
                    index,
                    total: blocks.length,
                    isFirst: index === 0,
                    isLast: index === blocks.length - 1,
                    moveUp,
                    moveDown,
                    remove,
                  })}
                </div>

                {(canDragBlocks || removable) && (
                  <div className="flex items-center gap-0.5 pt-0.5 shrink-0">
                    {canDragBlocks && (
                      <div className="hidden sm:flex items-center opacity-0 group-hover/block:opacity-100 group-focus-within/block:opacity-100 transition-opacity">
                        <button
                          type="button"
                          disabled={index === 0}
                          onClick={moveUp}
                          aria-label="Di chuyển lên"
                          title="Di chuyển lên"
                          className="size-7 rounded-lg inline-flex items-center justify-center text-slate-400 hover:bg-slate-200/60 hover:text-slate-700 disabled:opacity-30 disabled:pointer-events-none cursor-pointer"
                        >
                          <ArrowUp className="size-3.5" />
                        </button>
                        <button
                          type="button"
                          disabled={index === blocks.length - 1}
                          onClick={moveDown}
                          aria-label="Di chuyển xuống"
                          title="Di chuyển xuống"
                          className="size-7 rounded-lg inline-flex items-center justify-center text-slate-400 hover:bg-slate-200/60 hover:text-slate-700 disabled:opacity-30 disabled:pointer-events-none cursor-pointer"
                        >
                          <ArrowDown className="size-3.5" />
                        </button>
                      </div>
                    )}

                    {removable && (
                      <button
                        type="button"
                        onClick={remove}
                        aria-label="Xóa mục"
                        title="Xóa mục"
                        className="size-8 rounded-lg inline-flex items-center justify-center text-slate-400 hover:bg-rose-50 hover:text-rose-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring transition-colors cursor-pointer"
                      >
                        <Trash2 className="size-3.5" />
                      </button>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {addMenu &&
        menuCoords &&
        typeof document !== 'undefined' &&
        createPortal(
          <div
            ref={menuRef}
            role="menu"
            aria-label="Chọn loại nội dung muốn thêm"
            style={{
              position: 'fixed',
              left: menuCoords.left,
              top: menuCoords.top,
              width: menuCoords.width,
              transform: menuCoords.placement === 'top' ? 'translateY(-100%)' : undefined,
              zIndex: 80,
            }}
            className="rounded-xl border border-slate-200/90 bg-white p-1.5 shadow-lg animate-in fade-in duration-150"
          >
            <div className="space-y-0.5">
              {blockTypes.map((opt) => (
                <button
                  key={opt.kind}
                  type="button"
                  role="menuitem"
                  disabled={opt.disabled}
                  onClick={() => handleSelectOption(opt)}
                  className={cn(
                    'w-full flex items-center justify-between gap-2.5 rounded-lg px-2.5 py-2 text-left transition-colors',
                    opt.disabled
                      ? 'opacity-50 cursor-not-allowed'
                      : 'text-slate-700 hover:bg-slate-50 hover:text-slate-900 cursor-pointer',
                  )}
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <span className="text-slate-500 shrink-0">{opt.icon}</span>
                    <div className="min-w-0">
                      <span className="text-xs font-medium block truncate">{opt.label}</span>
                      {(opt.disabled ? opt.disabledReason : opt.description) && (
                        <p className="text-[11px] text-slate-500 truncate">
                          {opt.disabled ? opt.disabledReason : opt.description}
                        </p>
                      )}
                    </div>
                  </div>

                  {opt.badgeText && (
                    <span className="text-[11px] font-medium text-slate-400 tabular-nums shrink-0">
                      {opt.badgeText}
                    </span>
                  )}
                </button>
              ))}
            </div>
          </div>,
          document.body,
        )}
    </div>
  );
}
