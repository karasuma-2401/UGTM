import { useEffect, useRef, useState } from 'react';
import {
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  ClipboardPaste,
  ImagePlus,
  Maximize2,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import { formatFileSize } from '../../utils/milestoneFormatters';
import {
  IMAGE_MAX_COUNT,
  validateImageAttachmentFile,
} from '../../services/submissionUploadHelper';

interface SubmissionImageAttachmentsPickerProps {
  images: File[];
  onChange: (next: File[]) => void;
  disabled?: boolean;
  title?: string;
  helperText?: string;
}

interface PreviewEntry {
  key: string;
  file: File;
  url: string;
}

function makeFileKey(file: File): string {
  return `${file.name}-${file.size}-${file.lastModified}`;
}

export function SubmissionImageAttachmentsPicker({
  images,
  onChange,
  disabled = false,
  title = 'Ảnh minh chứng sản phẩm (tuỳ chọn)',
  helperText = 'Hỗ trợ PNG, JPG, WEBP · tối đa 5 ảnh · ≤ 5 MB/ảnh · Hỗ trợ Ctrl+V dán ảnh chụp màn hình.',
}: SubmissionImageAttachmentsPickerProps) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const [previews, setPreviews] = useState<PreviewEntry[]>([]);
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);

  useEffect(() => {
    const entries = images.map((file) => ({
      key: makeFileKey(file),
      file,
      url: URL.createObjectURL(file),
    }));
    setPreviews(entries);
    return () => {
      for (const entry of entries) {
        URL.revokeObjectURL(entry.url);
      }
    };
  }, [images]);

  const appendFiles = (incomingList: File[]) => {
    if (disabled || incomingList.length === 0) return;
    setLocalError(null);

    const remainingSlots = IMAGE_MAX_COUNT - images.length;
    if (remainingSlots <= 0) {
      setLocalError(`Đã đạt tối đa ${IMAGE_MAX_COUNT} ảnh minh chứng cho mỗi lần nộp.`);
      return;
    }

    const errors: string[] = [];
    const existingKeys = new Set(images.map(makeFileKey));
    const accepted: File[] = [];

    for (const file of incomingList) {
      const validationError = validateImageAttachmentFile(file);
      if (validationError) {
        errors.push(validationError);
        continue;
      }

      const key = makeFileKey(file);
      if (existingKeys.has(key)) {
        errors.push(`Ảnh "${file.name}" đã có trong danh sách.`);
        continue;
      }

      if (accepted.length >= remainingSlots) {
        errors.push(`Chỉ giữ ${IMAGE_MAX_COUNT} ảnh đầu tiên theo giới hạn.`);
        break;
      }

      existingKeys.add(key);
      accepted.push(file);
    }

    if (errors.length > 0) {
      setLocalError(errors[0]);
    }

    if (accepted.length > 0) {
      onChange([...images, ...accepted]);
    }
  };

  const handlePaste = (event: React.ClipboardEvent<HTMLDivElement>) => {
    if (disabled) return;
    const clipboardFiles: File[] = [];
    const items = event.clipboardData?.items;
    if (items) {
      for (let i = 0; i < items.length; i += 1) {
        const item = items[i];
        if (item.kind === 'file' && item.type.startsWith('image/')) {
          const blob = item.getAsFile();
          if (blob) {
            const ext =
              blob.type === 'image/webp'
                ? 'webp'
                : blob.type === 'image/jpeg'
                  ? 'jpg'
                  : 'png';
            const now = new Date();
            const stamp = `${String(now.getHours()).padStart(2, '0')}${String(
              now.getMinutes(),
            ).padStart(2, '0')}${String(now.getSeconds()).padStart(2, '0')}`;
            const namedFile =
              blob.name && blob.name !== 'image.png'
                ? blob
                : new File([blob], `screenshot-${stamp}.${ext}`, {
                    type: blob.type || `image/${ext}`,
                    lastModified: Date.now(),
                  });
            clipboardFiles.push(namedFile);
          }
        }
      }
    }

    if (clipboardFiles.length > 0) {
      event.preventDefault();
      appendFiles(clipboardFiles);
    }
  };

  const activeLightbox =
    lightboxIndex !== null && previews[lightboxIndex] ? previews[lightboxIndex] : null;

  return (
    <div className="space-y-2.5" onPaste={handlePaste}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold text-ink-heading">{title}</span>
            <span className="inline-flex items-center rounded-full bg-surface-muted px-2 py-0.5 text-[11px] font-medium text-ink-body tabular-nums">
              {images.length}/{IMAGE_MAX_COUNT}
            </span>
          </div>
          <p className="text-[11px] text-ink-muted mt-0.5">{helperText}</p>
        </div>

        {!disabled && images.length > 0 && images.length < IMAGE_MAX_COUNT && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => inputRef.current?.click()}
            className="h-8 gap-1.5 text-xs bg-surface-card"
          >
            <ImagePlus className="size-3.5" />
            Thêm ảnh
          </Button>
        )}
      </div>

      <input
        ref={inputRef}
        type="file"
        accept=".png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp"
        multiple
        hidden
        className="hidden"
        disabled={disabled}
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          appendFiles(files);
          if (inputRef.current) inputRef.current.value = '';
        }}
      />

      {previews.length > 0 ? (
        <div className="space-y-2.5">
          <div
            onDrop={(e) => {
              e.preventDefault();
              setIsDragging(false);
              appendFiles(Array.from(e.dataTransfer.files ?? []));
            }}
            onDragOver={(e) => {
              e.preventDefault();
              if (!disabled && !isDragging) setIsDragging(true);
            }}
            onDragLeave={() => setIsDragging(false)}
            className={cn(
              'grid grid-cols-2 gap-2.5 sm:grid-cols-3 md:grid-cols-5 rounded-xl p-1 transition-colors',
              isDragging && 'bg-tone-info-bg ring-2 ring-[#2054a5]/40',
            )}
          >
            {previews.map((entry, idx) => (
              <div
                key={entry.key}
                className="group relative overflow-hidden rounded-xl border border-line-input/90 bg-surface-muted shadow-2xs"
              >
                <button
                  type="button"
                  onClick={() => setLightboxIndex(idx)}
                  className="block w-full aspect-4/3 overflow-hidden bg-surface-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring cursor-pointer"
                  title={`Xem lớn ${entry.file.name}`}
                >
                  <img
                    src={entry.url}
                    alt={entry.file.name}
                    className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-105"
                  />
                </button>

                <div className="flex items-center justify-between gap-1 px-2 py-1.5 bg-surface-card border-t border-line-subtle">
                  <div className="min-w-0">
                    <p className="truncate text-[11px] font-medium text-ink-heading" title={entry.file.name}>
                      {entry.file.name}
                    </p>
                    <p className="text-[10px] text-ink-muted tabular-nums">
                      {formatFileSize(entry.file.size)}
                    </p>
                  </div>
                  <div className="flex items-center gap-0.5 shrink-0">
                    <button
                      type="button"
                      onClick={() => setLightboxIndex(idx)}
                      aria-label={`Phóng to ${entry.file.name}`}
                      className="rounded-md p-1 text-ink-subtle hover:bg-surface-muted hover:text-ink-secondary cursor-pointer"
                    >
                      <Maximize2 className="size-3" />
                    </button>
                    {!disabled && (
                      <button
                        type="button"
                        onClick={() => {
                          setLocalError(null);
                          onChange(images.filter((_, i) => i !== idx));
                        }}
                        aria-label={`Gỡ ảnh ${entry.file.name}`}
                        className="rounded-md p-1 text-ink-subtle hover:bg-tone-danger-bg hover:text-tone-danger-icon cursor-pointer"
                      >
                        <Trash2 className="size-3" />
                      </button>
                    )}
                  </div>
                </div>
              </div>
            ))}

            {!disabled && previews.length < IMAGE_MAX_COUNT && (
              <button
                type="button"
                onClick={() => inputRef.current?.click()}
                className="aspect-4/3 sm:aspect-auto min-h-[104px] rounded-xl border border-dashed border-line-strong/90 bg-surface-subtle/40 hover:border-[#2054a5]/60 hover:bg-tone-info-bg flex flex-col items-center justify-center gap-1 text-xs text-ink-muted hover:text-brand-ink transition-colors cursor-pointer"
              >
                <ImagePlus className="size-4" />
                <span className="font-medium">Thêm ({IMAGE_MAX_COUNT - previews.length})</span>
                <span className="text-[10px] text-ink-subtle inline-flex items-center gap-0.5">
                  <ClipboardPaste className="size-2.5" /> Ctrl+V
                </span>
              </button>
            )}
          </div>
        </div>
      ) : (
        <div
          role="button"
          tabIndex={disabled ? -1 : 0}
          aria-disabled={disabled}
          onDrop={(e) => {
            e.preventDefault();
            setIsDragging(false);
            appendFiles(Array.from(e.dataTransfer.files ?? []));
          }}
          onDragOver={(e) => {
            e.preventDefault();
            if (!disabled && !isDragging) setIsDragging(true);
          }}
          onDragLeave={() => setIsDragging(false)}
          onClick={() => !disabled && inputRef.current?.click()}
          onKeyDown={(e) => {
            if (!disabled && (e.key === 'Enter' || e.key === ' ')) {
              e.preventDefault();
              inputRef.current?.click();
            }
          }}
          className={cn(
            'rounded-xl border border-dashed px-4 py-3.5 transition-colors flex flex-wrap items-center justify-between gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            disabled
              ? 'border-line bg-surface-subtle/70 opacity-60 cursor-not-allowed'
              : isDragging
                ? 'border-[#2054a5] bg-tone-info-bg cursor-copy'
                : 'border-line-input/90 bg-surface-subtle/45 hover:border-[#2054a5]/60 hover:bg-tone-info-bg cursor-pointer',
          )}
        >
          <div className="flex items-center gap-2.5 text-xs text-ink-body">
            <div className="size-8 rounded-lg bg-surface-elevated border border-line-input/80 flex items-center justify-center text-ink-muted shrink-0 shadow-2xs">
              <ImagePlus className="size-4" />
            </div>
            <div>
              <p className="font-medium text-ink-heading">
                Kéo thả ảnh màn hình sản phẩm, hoặc{' '}
                <span className="text-brand-ink">chọn ảnh</span>
              </p>
              <p className="text-[11px] text-ink-muted">
                Có thể nhấn Ctrl+V trực tiếp tại đây để dán ảnh từ clipboard
              </p>
            </div>
          </div>
          <span className="inline-flex items-center gap-1 rounded-md border border-line-input bg-surface-elevated px-2 py-1 text-[11px] font-medium text-ink-body">
            <ClipboardPaste className="size-3 text-ink-subtle" />
            Ctrl+V
          </span>
        </div>
      )}

      {localError && (
        <div
          role="alert"
          className="flex items-center gap-2 rounded-lg border border-tone-danger-border bg-tone-danger-bg px-3 py-2 text-xs text-tone-danger-text"
        >
          <AlertCircle className="size-3.5 shrink-0" />
          <span>{localError}</span>
        </div>
      )}

      {activeLightbox && lightboxIndex !== null && (
        <Dialog
          open
          onClose={() => setLightboxIndex(null)}
          title={
            <span className="text-sm font-semibold text-ink-primary truncate block">
              {activeLightbox.file.name} · {formatFileSize(activeLightbox.file.size)}
            </span>
          }
          className="max-w-4xl"
        >
          <div className="p-4 sm:p-5 bg-slate-950/95 flex flex-col items-center gap-3">
            <div className="relative w-full flex items-center justify-center min-h-[320px] max-h-[68vh]">
              <img
                src={activeLightbox.url}
                alt={activeLightbox.file.name}
                className="max-h-[65vh] max-w-full rounded-lg object-contain"
              />
            </div>

            {previews.length > 1 && (
              <div className="flex items-center gap-3 text-xs text-ink-faint">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setLightboxIndex((lightboxIndex - 1 + previews.length) % previews.length)
                  }
                  className="bg-surface-card/10 border-white/20 text-white hover:bg-surface-hover/20 hover:text-white"
                >
                  <ChevronLeft className="size-4 mr-1" />
                  Trước
                </Button>
                <span className="tabular-nums font-medium">
                  {lightboxIndex + 1} / {previews.length}
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setLightboxIndex((lightboxIndex + 1) % previews.length)}
                  className="bg-surface-card/10 border-white/20 text-white hover:bg-surface-hover/20 hover:text-white"
                >
                  Sau
                  <ChevronRight className="size-4 ml-1" />
                </Button>
              </div>
            )}
          </div>
        </Dialog>
      )}
    </div>
  );
}
