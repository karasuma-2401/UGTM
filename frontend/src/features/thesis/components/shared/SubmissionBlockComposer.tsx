import { useEffect, useMemo, useRef, useState } from 'react';
import {
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  ClipboardPaste,
  Globe,
  ImagePlus,
  Link2,
  Maximize2,
} from 'lucide-react';
import {
  BlockComposer,
  type ComposerBlockItem,
  type ComposerBlockTypeOption,
} from '@/components/ui/block-composer';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, type SelectOption } from '@/components/ui/select';
import { cn } from '@/lib/utils';
import type { SubmissionLinkCategory } from '../../types/syllabus.types';
import {
  detectLinkCategory,
  formatFileSize,
  SUBMISSION_LINK_CATEGORIES,
  SUBMISSION_LINK_CATEGORY_LABELS,
} from '../../utils/milestoneFormatters';
import {
  IMAGE_MAX_COUNT,
  PRODUCT_LINK_MAX_COUNT,
  validateImageAttachmentFile,
} from '../../services/submissionUploadHelper';
import {
  createEmptyLinkDraft,
  getProductLinkRowError,
  SUBMISSION_LINK_CATEGORY_ICONS,
  type ProductLinkDraft,
} from './SubmissionProductLinksEditor';

export type SubmissionComposerBlockKind = 'link' | 'image';

interface InternalBlockSlot {
  id: string;
  kind: SubmissionComposerBlockKind;
  refId: string;
  isPendingImage?: boolean;
}

export interface SubmissionComposerRenderBlock
  extends ComposerBlockItem<SubmissionComposerBlockKind> {
  refId: string;
  link?: ProductLinkDraft;
  imageFile?: File;
  imageUrl?: string;
  imageIndex?: number;
  isPendingImage?: boolean;
}

export interface SubmissionBlockComposerProps {
  productLinks: ProductLinkDraft[];
  onProductLinksChange: (links: ProductLinkDraft[]) => void;
  images: File[];
  onImagesChange: (files: File[]) => void;
  linksRequired?: boolean;
  disabled?: boolean;
  title?: string;
  subtitle?: string;
}

let uidCounter = 0;
function nextUid(prefix: string): string {
  uidCounter += 1;
  return `${prefix}-${uidCounter}`;
}

function makeSubmissionImageKey(file: File): string {
  return `${file.name}-${file.size}-${file.lastModified}`;
}

function buildInitialSlots(
  productLinks: ProductLinkDraft[],
  images: File[],
): InternalBlockSlot[] {
  const slots: InternalBlockSlot[] = [];
  for (const link of productLinks) {
    slots.push({ id: `blk-link-${link.id}`, kind: 'link', refId: link.id });
  }
  for (const file of images) {
    const key = makeSubmissionImageKey(file);
    slots.push({ id: `blk-img-${key}`, kind: 'image', refId: key });
  }
  return slots;
}

function reconcileSlots(
  currentSlots: InternalBlockSlot[],
  productLinks: ProductLinkDraft[],
  images: File[],
): InternalBlockSlot[] {
  const linkIds = new Set(productLinks.map((l) => l.id));
  const imageKeys = new Set(images.map(makeSubmissionImageKey));

  const next: InternalBlockSlot[] = [];
  const seenLinkIds = new Set<string>();
  const seenImageKeys = new Set<string>();

  for (const slot of currentSlots) {
    if (slot.kind === 'link') {
      if (linkIds.has(slot.refId) && !seenLinkIds.has(slot.refId)) {
        seenLinkIds.add(slot.refId);
        next.push(slot);
      }
      continue;
    }

    if (slot.isPendingImage) {
      next.push(slot);
      continue;
    }
    if (imageKeys.has(slot.refId) && !seenImageKeys.has(slot.refId)) {
      seenImageKeys.add(slot.refId);
      next.push(slot);
    }
  }

  for (const link of productLinks) {
    if (!seenLinkIds.has(link.id)) {
      seenLinkIds.add(link.id);
      next.push({ id: `blk-link-${link.id}`, kind: 'link', refId: link.id });
    }
  }

  for (const file of images) {
    const key = makeSubmissionImageKey(file);
    if (!seenImageKeys.has(key)) {
      seenImageKeys.add(key);
      const pendingIdx = next.findIndex((s) => s.kind === 'image' && s.isPendingImage);
      if (pendingIdx !== -1) {
        next[pendingIdx] = {
          id: `blk-img-${key}`,
          kind: 'image',
          refId: key,
          isPendingImage: false,
        };
      } else {
        next.push({ id: `blk-img-${key}`, kind: 'image', refId: key });
      }
    }
  }

  return next;
}

export function SubmissionBlockComposer({
  productLinks,
  onProductLinksChange,
  images,
  onImagesChange,
  linksRequired = false,
  disabled = false,
  title = 'Liên kết & ảnh minh chứng sản phẩm',
  subtitle = 'Tối đa 5 liên kết và 5 ảnh minh chứng · Hỗ trợ kéo thả hoặc dán ảnh trực tiếp bằng Ctrl+V.',
}: SubmissionBlockComposerProps) {
  const imageInputRef = useRef<HTMLInputElement | null>(null);
  const pendingTargetBlockIdRef = useRef<string | null>(null);

  const [slots, setSlots] = useState<InternalBlockSlot[]>(() =>
    buildInitialSlots(productLinks, images),
  );
  const [imageError, setImageError] = useState<string | null>(null);
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);

  const imagePreviewEntries = useMemo(
    () =>
      images.map((file) => ({
        key: makeSubmissionImageKey(file),
        file,
        url:
          typeof URL !== 'undefined' &&
          typeof URL.createObjectURL === 'function' &&
          typeof Blob !== 'undefined' &&
          file instanceof Blob
            ? URL.createObjectURL(file)
            : '',
      })),
    [images],
  );

  useEffect(() => {
    return () => {
      for (const entry of imagePreviewEntries) {
        if (entry.url && typeof URL !== 'undefined' && typeof URL.revokeObjectURL === 'function') {
          URL.revokeObjectURL(entry.url);
        }
      }
    };
  }, [imagePreviewEntries]);

  const effectiveSlots = useMemo(
    () => reconcileSlots(slots, productLinks, images),
    [slots, productLinks, images],
  );

  const linkMap = useMemo(
    () => new Map(productLinks.map((link) => [link.id, link])),
    [productLinks],
  );

  const imageIndexMap = useMemo(() => {
    const map = new Map<string, { index: number; file: File; url: string }>();
    imagePreviewEntries.forEach((entry, index) => {
      map.set(entry.key, { index, file: entry.file, url: entry.url });
    });
    return map;
  }, [imagePreviewEntries]);

  const displayBlocks = useMemo<SubmissionComposerRenderBlock[]>(() => {
    return effectiveSlots.map((slot) => {
      if (slot.kind === 'link') {
        return {
          id: slot.id,
          kind: 'link',
          refId: slot.refId,
          link: linkMap.get(slot.refId),
        };
      }
      const entry = slot.isPendingImage ? undefined : imageIndexMap.get(slot.refId);
      return {
        id: slot.id,
        kind: 'image',
        refId: slot.refId,
        isPendingImage: slot.isPendingImage || !entry,
        imageFile: entry?.file,
        imageUrl: entry?.url,
        imageIndex: entry?.index,
      };
    });
  }, [effectiveSlots, linkMap, imageIndexMap]);

  const categoryOptions = useMemo<SelectOption<SubmissionLinkCategory>[]>(
    () =>
      SUBMISSION_LINK_CATEGORIES.map((cat) => {
        const Icon = SUBMISSION_LINK_CATEGORY_ICONS[cat] || Globe;
        return {
          value: cat,
          label: SUBMISSION_LINK_CATEGORY_LABELS[cat],
          icon: <Icon className="size-3.5 text-slate-500" />,
        };
      }),
    [],
  );

  const commitSlotOrderAndSyncArrays = (nextSlots: InternalBlockSlot[]) => {
    setSlots(nextSlots);

    const orderedLinks: ProductLinkDraft[] = [];
    for (const s of nextSlots) {
      if (s.kind === 'link') {
        const found = linkMap.get(s.refId);
        if (found) orderedLinks.push(found);
      }
    }
    if (
      orderedLinks.length === productLinks.length &&
      orderedLinks.some((item, idx) => item.id !== productLinks[idx]?.id)
    ) {
      onProductLinksChange(orderedLinks);
    }

    const orderedImages: File[] = [];
    for (const s of nextSlots) {
      if (s.kind === 'image' && !s.isPendingImage) {
        const found = imageIndexMap.get(s.refId);
        if (found) orderedImages.push(found.file);
      }
    }
    if (
      orderedImages.length === images.length &&
      orderedImages.some(
        (file, idx) => makeSubmissionImageKey(file) !== makeSubmissionImageKey(images[idx]),
      )
    ) {
      onImagesChange(orderedImages);
    }
  };

  const appendImageFiles = (incomingFiles: File[], targetPendingSlotId?: string | null) => {
    if (disabled || incomingFiles.length === 0) return;
    setImageError(null);

    const remaining = IMAGE_MAX_COUNT - images.length;
    if (remaining <= 0) {
      setImageError(`Đã đạt tối đa ${IMAGE_MAX_COUNT} ảnh minh chứng cho mỗi lần nộp.`);
      return;
    }

    const errors: string[] = [];
    const existingKeys = new Set(images.map(makeSubmissionImageKey));
    const accepted: File[] = [];

    for (const file of incomingFiles) {
      const validationError = validateImageAttachmentFile(file);
      if (validationError) {
        errors.push(validationError);
        continue;
      }

      const key = makeSubmissionImageKey(file);
      if (existingKeys.has(key)) {
        errors.push(`Ảnh "${file.name}" đã có trong danh sách.`);
        continue;
      }

      if (accepted.length >= remaining) {
        errors.push(`Chỉ giữ ${IMAGE_MAX_COUNT} ảnh đầu tiên theo giới hạn.`);
        break;
      }

      existingKeys.add(key);
      accepted.push(file);
    }

    if (errors.length > 0) {
      setImageError(errors[0]);
    }

    if (accepted.length === 0) return;

    const nextSlots = [...effectiveSlots];
    if (targetPendingSlotId) {
      const idx = nextSlots.findIndex((s) => s.id === targetPendingSlotId);
      if (idx !== -1) {
        const replacements: InternalBlockSlot[] = accepted.map((file) => {
          const key = makeSubmissionImageKey(file);
          return { id: `blk-img-${key}`, kind: 'image', refId: key, isPendingImage: false };
        });
        nextSlots.splice(idx, 1, ...replacements);
        setSlots(nextSlots);
        onImagesChange([...images, ...accepted]);
        return;
      }
    }

    for (const file of accepted) {
      const key = makeSubmissionImageKey(file);
      const pendingIdx = nextSlots.findIndex((s) => s.kind === 'image' && s.isPendingImage);
      const populatedSlot: InternalBlockSlot = {
        id: `blk-img-${key}`,
        kind: 'image',
        refId: key,
        isPendingImage: false,
      };
      if (pendingIdx !== -1) {
        nextSlots[pendingIdx] = populatedSlot;
      } else {
        nextSlots.push(populatedSlot);
      }
    }
    setSlots(nextSlots);
    onImagesChange([...images, ...accepted]);
  };

  const pendingImageCount = effectiveSlots.filter(
    (s) => s.kind === 'image' && s.isPendingImage,
  ).length;
  const totalImageSlots = images.length + pendingImageCount;

  const blockTypes = useMemo<ComposerBlockTypeOption<SubmissionComposerBlockKind>[]>(
    () => [
      {
        kind: 'link',
        label: 'Liên kết sản phẩm',
        description: 'Gắn URL GitHub, Video Demo, Figma hoặc Tài liệu',
        icon: <Link2 className="size-3.5" />,
        badgeText: `${productLinks.length}/${PRODUCT_LINK_MAX_COUNT}`,
        disabled: productLinks.length >= PRODUCT_LINK_MAX_COUNT,
        disabledReason: `Đã đạt tối đa ${PRODUCT_LINK_MAX_COUNT} liên kết`,
      },
      {
        kind: 'image',
        label: 'Ảnh minh chứng',
        description: 'Chèn ảnh chụp màn hình sản phẩm (PNG, JPG, WEBP ≤ 5 MB)',
        icon: <ImagePlus className="size-3.5" />,
        badgeText: `${images.length}/${IMAGE_MAX_COUNT}`,
        disabled: totalImageSlots >= IMAGE_MAX_COUNT,
        disabledReason: `Đã đạt tối đa ${IMAGE_MAX_COUNT} ảnh minh chứng`,
      },
    ],
    [images.length, productLinks.length, totalImageSlots],
  );

  const handleInsertBlock = (kind: SubmissionComposerBlockKind, atIndex?: number) => {
    if (disabled) return;
    const targetIndex =
      typeof atIndex === 'number'
        ? Math.max(0, Math.min(effectiveSlots.length, atIndex))
        : effectiveSlots.length;

    if (kind === 'link') {
      if (productLinks.length >= PRODUCT_LINK_MAX_COUNT) return;
      const defaultCategory: SubmissionLinkCategory =
        productLinks.length === 0
          ? 'SourceCode'
          : productLinks.length === 1
            ? 'Demo'
            : 'Documentation';
      const draft = createEmptyLinkDraft(defaultCategory);
      const newSlot: InternalBlockSlot = {
        id: `blk-link-${draft.id}`,
        kind: 'link',
        refId: draft.id,
      };
      const nextSlots = [...effectiveSlots];
      nextSlots.splice(targetIndex, 0, newSlot);
      setSlots(nextSlots);

      const orderedLinkInsertPos = nextSlots
        .slice(0, targetIndex)
        .filter((s) => s.kind === 'link').length;
      const nextLinks = [...productLinks];
      nextLinks.splice(orderedLinkInsertPos, 0, draft);
      onProductLinksChange(nextLinks);
      return;
    }

    if (totalImageSlots >= IMAGE_MAX_COUNT) return;
    const pendingSlot: InternalBlockSlot = {
      id: nextUid('blk-img-pending'),
      kind: 'image',
      refId: '',
      isPendingImage: true,
    };
    const nextSlots = [...effectiveSlots];
    nextSlots.splice(targetIndex, 0, pendingSlot);
    setSlots(nextSlots);
  };

  const handleReorder = (fromIndex: number, toIndex: number) => {
    if (disabled || fromIndex === toIndex) return;
    const nextSlots = [...effectiveSlots];
    const [moved] = nextSlots.splice(fromIndex, 1);
    if (!moved) return;
    nextSlots.splice(toIndex, 0, moved);
    commitSlotOrderAndSyncArrays(nextSlots);
  };

  const handleRemoveBlock = (blockId: string) => {
    if (disabled) return;
    const target = effectiveSlots.find((s) => s.id === blockId);
    if (!target) return;

    if (target.kind === 'link') {
      const nextSlots = effectiveSlots.filter((s) => s.id !== blockId);
      setSlots(nextSlots);
      onProductLinksChange(productLinks.filter((l) => l.id !== target.refId));
      return;
    }

    setImageError(null);
    const nextSlots = effectiveSlots.filter((s) => s.id !== blockId);
    setSlots(nextSlots);
    if (!target.isPendingImage) {
      onImagesChange(images.filter((file) => makeSubmissionImageKey(file) !== target.refId));
    }
  };

  const canRemoveBlock = () => true;

  const filledLinkCount = productLinks.filter((l) => l.url.trim().length > 0).length;
  const activeLightbox =
    lightboxIndex !== null && imagePreviewEntries[lightboxIndex]
      ? imagePreviewEntries[lightboxIndex]
      : null;

  return (
    <div className="flex flex-col gap-2.5">
      <input
        ref={imageInputRef}
        type="file"
        accept=".png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp"
        multiple
        hidden
        className="hidden"
        disabled={disabled}
        onChange={(event) => {
          const files = Array.from(event.target.files ?? []);
          const targetId = pendingTargetBlockIdRef.current;
          pendingTargetBlockIdRef.current = null;
          appendImageFiles(files, targetId);
          if (imageInputRef.current) imageInputRef.current.value = '';
        }}
      />

      <BlockComposer<SubmissionComposerBlockKind, SubmissionComposerRenderBlock>
        title={
          <span>
            {title}
            {linksRequired && <span className="text-rose-500 ml-0.5">*</span>}
          </span>
        }
        subtitle={subtitle}
        disabled={disabled}
        blocks={displayBlocks}
        blockTypes={blockTypes}
        onInsertBlock={handleInsertBlock}
        onReorder={handleReorder}
        onRemoveBlock={handleRemoveBlock}
        canRemoveBlock={canRemoveBlock}
        addButtonLabel="Thêm liên kết / ảnh"
        emptyStateIcon={<Link2 className="size-4" />}
        emptyStateLabel="Chưa có liên kết hoặc ảnh minh chứng"
        emptyStateHint="Nhấn để chọn loại nội dung, hoặc kéo thả / Ctrl+V ảnh từ clipboard"
        onPasteFiles={(files) => {
          const imgFiles = files.filter((f) => f.type.startsWith('image/'));
          if (imgFiles.length > 0) {
            appendImageFiles(imgFiles);
          }
        }}
        onDropFiles={(files) => {
          const imgFiles = files.filter((f) => f.type.startsWith('image/'));
          if (imgFiles.length > 0) {
            appendImageFiles(imgFiles);
          }
        }}
        headerMeta={
          <span className="inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600 tabular-nums">
            {filledLinkCount}/{PRODUCT_LINK_MAX_COUNT} liên kết · {images.length}/{IMAGE_MAX_COUNT} ảnh
          </span>
        }
        renderBlock={(block, ctx) => {
          if (block.kind === 'link' && block.link) {
            const link = block.link;
            const inlineError = getProductLinkRowError(link, productLinks);

            return (
              <div className="space-y-1.5">
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-12 sm:items-center">
                  <div className="sm:col-span-3">
                    <Select<SubmissionLinkCategory>
                      aria-label={`Phân loại liên kết khối ${ctx.index + 1}`}
                      value={link.category}
                      options={categoryOptions}
                      disabled={disabled}
                      triggerClassName="rounded-lg font-medium text-slate-700"
                      onChange={(nextCategory: SubmissionLinkCategory) => {
                        onProductLinksChange(
                          productLinks.map((item) =>
                            item.id === link.id ? { ...item, category: nextCategory } : item,
                          ),
                        );
                      }}
                    />
                  </div>

                  <div className="sm:col-span-6">
                    <Input
                      type="url"
                      aria-label={`URL liên kết khối ${ctx.index + 1}`}
                      value={link.url}
                      disabled={disabled}
                      placeholder={
                        link.category === 'SourceCode'
                          ? 'https://github.com/org/repository'
                          : link.category === 'Demo'
                            ? 'https://demo.example.com hoặc https://youtu.be/...'
                            : 'https://...'
                      }
                      onChange={(e) => {
                        const nextUrl = e.target.value;
                        const detected = detectLinkCategory(nextUrl);
                        onProductLinksChange(
                          productLinks.map((item) => {
                            if (item.id !== link.id) return item;
                            const shouldAutoDetect =
                              detected !== null &&
                              (item.url.trim() === '' || item.category === 'Other');
                            return {
                              ...item,
                              url: nextUrl,
                              category: shouldAutoDetect ? detected : item.category,
                            };
                          }),
                        );
                      }}
                      className={cn(
                        'h-9 text-xs sm:text-sm bg-white',
                        inlineError && 'border-rose-300 focus-visible:ring-rose-500/20',
                      )}
                    />
                  </div>

                  <div className="sm:col-span-3">
                    <Input
                      type="text"
                      aria-label={`Nhãn mô tả liên kết khối ${ctx.index + 1}`}
                      value={link.label ?? ''}
                      disabled={disabled}
                      maxLength={80}
                      placeholder="Nhãn ngắn (tuỳ chọn)"
                      onChange={(e) => {
                        const val = e.target.value;
                        onProductLinksChange(
                          productLinks.map((item) =>
                            item.id === link.id ? { ...item, label: val } : item,
                          ),
                        );
                      }}
                      className="h-9 text-xs bg-white"
                    />
                  </div>
                </div>

                {inlineError && (
                  <p className="text-[11px] font-medium text-rose-600" role="alert">
                    {inlineError}
                  </p>
                )}
              </div>
            );
          }

          if (block.isPendingImage || !block.imageFile) {
            return (
              <div
                role="button"
                tabIndex={disabled ? -1 : 0}
                onClick={() => {
                  if (disabled) return;
                  pendingTargetBlockIdRef.current = block.id;
                  imageInputRef.current?.click();
                }}
                onKeyDown={(event) => {
                  if (!disabled && (event.key === 'Enter' || event.key === ' ')) {
                    event.preventDefault();
                    pendingTargetBlockIdRef.current = block.id;
                    imageInputRef.current?.click();
                  }
                }}
                onDragOver={(event) => {
                  if (event.dataTransfer.types.includes('Files')) {
                    event.preventDefault();
                    event.stopPropagation();
                  }
                }}
                onDrop={(event) => {
                  const files = Array.from(event.dataTransfer.files ?? []);
                  if (files.length === 0) return;
                  event.preventDefault();
                  event.stopPropagation();
                  appendImageFiles(files, block.id);
                }}
                className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-dashed border-slate-200/90 bg-slate-50/55 px-3 py-2 text-xs text-slate-600 hover:border-[#2054a5]/60 hover:bg-blue-50/25 cursor-pointer transition-colors"
              >
                <div className="flex items-center gap-2 min-w-0">
                  <ImagePlus className="size-4 text-[#2054a5] shrink-0" />
                  <span className="truncate">
                    <span className="font-medium text-slate-800">Chọn ảnh minh chứng</span>{' '}
                    <span className="text-slate-500">· PNG, JPG, WEBP ≤ 5 MB</span>
                  </span>
                </div>
                <span className="inline-flex items-center gap-1 rounded border border-slate-200 bg-white px-2 py-0.5 text-[11px] font-medium text-slate-600 shrink-0">
                  <ClipboardPaste className="size-3 text-slate-400" />
                  Ctrl+V / Kéo thả
                </span>
              </div>
            );
          }

          const file = block.imageFile;
          const ext = file.name.split('.').pop()?.toUpperCase() || 'IMG';
          const imgIndex = block.imageIndex ?? 0;

          return (
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-3 min-w-0">
                <button
                  type="button"
                  onClick={() => setLightboxIndex(imgIndex)}
                  title={`Phóng to ${file.name}`}
                  className="group/thumb relative h-12 w-16 rounded-lg overflow-hidden border border-slate-200/90 bg-slate-100 shrink-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring cursor-pointer"
                >
                  {block.imageUrl ? (
                    <img
                      src={block.imageUrl}
                      alt={file.name}
                      className="h-full w-full object-cover transition-transform duration-200 group-hover/thumb:scale-105"
                    />
                  ) : (
                    <div className="h-full w-full flex items-center justify-center text-slate-400">
                      <ImagePlus className="size-4" />
                    </div>
                  )}
                </button>

                <div className="min-w-0">
                  <p
                    className="text-xs sm:text-sm font-medium text-slate-800 truncate"
                    title={file.name}
                  >
                    {file.name}
                  </p>
                  <div className="flex flex-wrap items-center gap-1.5 mt-0.5 text-[11px] text-slate-500">
                    <span className="inline-flex items-center rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-semibold uppercase text-slate-600">
                      {ext}
                    </span>
                    <span className="tabular-nums font-medium text-slate-600">
                      {formatFileSize(file.size)}
                    </span>
                  </div>
                </div>
              </div>

              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => setLightboxIndex(imgIndex)}
                aria-label={`Phóng to ${file.name}`}
                className="inline-flex h-7 px-2 text-xs text-slate-500 hover:text-slate-800 shrink-0"
              >
                <Maximize2 className="size-3.5 sm:mr-1" />
                <span className="hidden sm:inline">Phóng to</span>
              </Button>
            </div>
          );
        }}
      />

      {imageError && (
        <div
          role="alert"
          className="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50/75 px-3 py-2 text-xs text-rose-700"
        >
          <AlertCircle className="size-3.5 shrink-0" />
          <span>{imageError}</span>
        </div>
      )}

      {activeLightbox && lightboxIndex !== null && (
        <Dialog
          open
          onClose={() => setLightboxIndex(null)}
          title={
            <span className="text-sm font-semibold text-slate-900 truncate block">
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

            {imagePreviewEntries.length > 1 && (
              <div className="flex items-center gap-3 text-xs text-slate-300">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setLightboxIndex(
                      (lightboxIndex - 1 + imagePreviewEntries.length) %
                        imagePreviewEntries.length,
                    )
                  }
                  className="bg-white/10 border-white/20 text-white hover:bg-white/20 hover:text-white"
                >
                  <ChevronLeft className="size-4 mr-1" />
                  Trước
                </Button>
                <span className="tabular-nums font-medium">
                  {lightboxIndex + 1} / {imagePreviewEntries.length}
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setLightboxIndex((lightboxIndex + 1) % imagePreviewEntries.length)
                  }
                  className="bg-white/10 border-white/20 text-white hover:bg-white/20 hover:text-white"
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
