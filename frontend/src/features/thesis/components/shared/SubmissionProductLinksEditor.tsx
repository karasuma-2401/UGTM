import { useMemo } from 'react';
import { Code2, FileText, Globe, Link2, Plus, PlayCircle, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, type SelectOption } from '@/components/ui/select';
import { cn } from '@/lib/utils';
import type {
  SubmissionLinkCategory,
  SubmissionProductLinkInput,
} from '../../types/syllabus.types';
import {
  detectLinkCategory,
  getSafeHttpUrl,
  SUBMISSION_LINK_CATEGORIES,
  SUBMISSION_LINK_CATEGORY_LABELS,
} from '../../utils/milestoneFormatters';
import { PRODUCT_LINK_MAX_COUNT } from '../../services/submissionUploadHelper';

export interface ProductLinkDraft extends SubmissionProductLinkInput {
  id: string;
}

interface SubmissionProductLinksEditorProps {
  links: ProductLinkDraft[];
  onChange: (next: ProductLinkDraft[]) => void;
  required?: boolean;
  disabled?: boolean;
  title?: string;
  helperText?: string;
}

export const SUBMISSION_LINK_CATEGORY_ICONS: Record<SubmissionLinkCategory, typeof Code2> = {
  SourceCode: Code2,
  Demo: PlayCircle,
  Documentation: FileText,
  Other: Globe,
};

const categoryIcons = SUBMISSION_LINK_CATEGORY_ICONS;

export function createEmptyLinkDraft(
  category: SubmissionLinkCategory = 'SourceCode',
): ProductLinkDraft {
  return {
    id: `link-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
    category,
    label: '',
    url: '',
  };
}

export function validateProductLinksDraft(
  links: ProductLinkDraft[],
  required: boolean,
): { validLinks: SubmissionProductLinkInput[]; error: string | null } {
  const nonEmpty = links.filter((item) => item.url.trim().length > 0 || (item.label ?? '').trim().length > 0);
  if (required && nonEmpty.length === 0) {
    return {
      validLinks: [],
      error: 'Vui lòng cung cấp ít nhất 1 liên kết sản phẩm hoặc kho mã nguồn.',
    };
  }
  if (nonEmpty.length > PRODUCT_LINK_MAX_COUNT) {
    return {
      validLinks: [],
      error: `Chỉ được đính kèm tối đa ${PRODUCT_LINK_MAX_COUNT} liên kết.`,
    };
  }

  const seenUrls = new Set<string>();
  const validLinks: SubmissionProductLinkInput[] = [];

  for (let i = 0; i < nonEmpty.length; i += 1) {
    const item = nonEmpty[i];
    const trimmedUrl = item.url.trim();
    if (!trimmedUrl) {
      return {
        validLinks: [],
        error: `Liên kết thứ ${i + 1} chưa có địa chỉ URL.`,
      };
    }

    const safeUrl = getSafeHttpUrl(trimmedUrl);
    if (!safeUrl) {
      return {
        validLinks: [],
        error: `Liên kết "${trimmedUrl}" phải bắt đầu bằng http:// hoặc https:// và đúng định dạng URL.`,
      };
    }

    if (typeof window !== 'undefined') {
      try {
        const parsed = new URL(safeUrl);
        if (parsed.origin === window.location.origin) {
          return {
            validLinks: [],
            error:
              'Vui lòng nhập liên kết kho mã nguồn, demo hoặc tài liệu bên ngoài thay vì liên kết cổng UGTM.',
          };
        }
      } catch {
        return {
          validLinks: [],
          error: `Liên kết "${trimmedUrl}" không hợp lệ.`,
        };
      }
    }

    const normalizedKey = safeUrl.toLowerCase();
    if (seenUrls.has(normalizedKey)) {
      return {
        validLinks: [],
        error: `Liên kết "${safeUrl}" đang bị trùng lặp.`,
      };
    }
    seenUrls.add(normalizedKey);

    validLinks.push({
      category: item.category,
      label: item.label?.trim() || undefined,
      url: safeUrl,
    });
  }

  return { validLinks, error: null };
}

export function getProductLinkRowError(
  link: ProductLinkDraft,
  allLinks: ProductLinkDraft[],
): string | null {
  const trimmed = link.url.trim();
  if (!trimmed) return null;
  const safe = getSafeHttpUrl(trimmed);
  if (!safe) return 'URL cần bắt đầu bằng http:// hoặc https://';

  if (typeof window !== 'undefined') {
    try {
      if (new URL(safe).origin === window.location.origin) {
        return 'Không dùng lại liên kết cổng UGTM';
      }
    } catch {
      return 'URL không hợp lệ';
    }
  }

  const duplicateCount = allLinks.filter(
    (other) => other.url.trim().toLowerCase() === trimmed.toLowerCase(),
  ).length;
  if (duplicateCount > 1) return 'Liên kết đang bị trùng';

  return null;
}

const getRowInlineError = getProductLinkRowError;

export function SubmissionProductLinksEditor({
  links,
  onChange,
  required = false,
  disabled = false,
  title = 'Liên kết sản phẩm & tài nguyên',
  helperText = 'Phân loại mã nguồn, video demo hoặc tài liệu thiết kế để GVHD truy cập nhanh (tối đa 5 liên kết).',
}: SubmissionProductLinksEditorProps) {
  const canAddMore = links.length < PRODUCT_LINK_MAX_COUNT && !disabled;

  const handleUrlChange = (id: string, nextUrl: string) => {
    const detected = detectLinkCategory(nextUrl);
    onChange(
      links.map((item) => {
        if (item.id !== id) return item;
        const shouldAutoDetect =
          detected !== null && (item.url.trim() === '' || item.category === 'Other');
        return {
          ...item,
          url: nextUrl,
          category: shouldAutoDetect ? detected : item.category,
        };
      }),
    );
  };

  const handleAdd = () => {
    if (!canAddMore) return;
    const defaultCategory: SubmissionLinkCategory =
      links.length === 0 ? 'SourceCode' : links.length === 1 ? 'Demo' : 'Documentation';
    onChange([...links, createEmptyLinkDraft(defaultCategory)]);
  };

  const handleRemove = (id: string) => {
    if (disabled) return;
    const next = links.filter((item) => item.id !== id);
    if (required && next.length === 0) {
      onChange([createEmptyLinkDraft('SourceCode')]);
      return;
    }
    onChange(next);
  };

  const categoryOptions = useMemo<SelectOption<SubmissionLinkCategory>[]>(
    () =>
      SUBMISSION_LINK_CATEGORIES.map((cat) => {
        const Icon = categoryIcons[cat] || Globe;
        return {
          value: cat,
          label: SUBMISSION_LINK_CATEGORY_LABELS[cat],
          icon: <Icon className="size-3.5 text-slate-500" />,
        };
      }),
    [],
  );

  return (
    <div className="space-y-2.5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold text-slate-800">
              {title}
              {required && <span className="text-rose-500 ml-0.5">*</span>}
            </span>
            <span className="inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600 tabular-nums">
              {links.filter((l) => l.url.trim()).length}/{PRODUCT_LINK_MAX_COUNT}
            </span>
          </div>
          <p className="text-[11px] text-slate-500 mt-0.5">{helperText}</p>
        </div>

        {canAddMore && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={handleAdd}
            className="h-8 gap-1.5 text-xs bg-white"
          >
            <Plus className="size-3.5" />
            Thêm liên kết
          </Button>
        )}
      </div>

      {links.length === 0 ? (
        <button
          type="button"
          disabled={disabled}
          onClick={handleAdd}
          className="w-full rounded-xl border border-dashed border-slate-200/90 bg-slate-50/50 px-4 py-3 text-left text-xs text-slate-500 transition-colors hover:border-[#2054a5]/50 hover:bg-blue-50/20 hover:text-slate-700 flex items-center justify-between gap-2 disabled:opacity-60 disabled:cursor-not-allowed cursor-pointer"
        >
          <span className="inline-flex items-center gap-2">
            <Link2 className="size-4 text-slate-400" />
            <span>Chưa có liên kết nào. Nhấn để gắn link GitHub, bản Demo hoặc Figma/Docs…</span>
          </span>
          <span className="font-medium text-[#2054a5] shrink-0">+ Thêm</span>
        </button>
      ) : (
        <div className="space-y-2">
          {links.map((link, index) => {
            const inlineError = getRowInlineError(link, links);
            const canRemove = !disabled && (!required || links.length > 1);

            return (
              <div
                key={link.id}
                className={cn(
                  'rounded-xl border bg-slate-50/55 p-2.5 transition-colors',
                  inlineError
                    ? 'border-rose-300 bg-rose-50/35'
                    : 'border-slate-200/80 focus-within:border-[#2054a5]/60 focus-within:bg-white',
                )}
              >
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-12 sm:items-center">
                  <div className="sm:col-span-3">
                    <Select<SubmissionLinkCategory>
                      aria-label={`Phân loại liên kết thứ ${index + 1}`}
                      value={link.category}
                      options={categoryOptions}
                      disabled={disabled}
                      triggerClassName="rounded-lg font-medium text-slate-700"
                      onChange={(nextCategory: SubmissionLinkCategory) => {
                        onChange(
                          links.map((item) =>
                            item.id === link.id ? { ...item, category: nextCategory } : item,
                          ),
                        );
                      }}
                    />
                  </div>

                  <div className="sm:col-span-6">
                    <Input
                      type="url"
                      aria-label={`URL liên kết thứ ${index + 1}`}
                      value={link.url}
                      disabled={disabled}
                      placeholder={
                        link.category === 'SourceCode'
                          ? 'https://github.com/org/repository'
                          : link.category === 'Demo'
                            ? 'https://demo.example.com hoặc https://youtu.be/...'
                            : 'https://...'
                      }
                      onChange={(e) => handleUrlChange(link.id, e.target.value)}
                      className={cn(
                        'h-9 text-xs sm:text-sm bg-white',
                        inlineError && 'border-rose-300 focus-visible:ring-rose-500/20',
                      )}
                    />
                  </div>

                  <div className="sm:col-span-3 flex items-center gap-1.5">
                    <Input
                      type="text"
                      aria-label={`Nhãn mô tả liên kết thứ ${index + 1}`}
                      value={link.label ?? ''}
                      disabled={disabled}
                      maxLength={80}
                      placeholder="Nhãn ngắn (tuỳ chọn)"
                      onChange={(e) => {
                        const val = e.target.value;
                        onChange(
                          links.map((item) =>
                            item.id === link.id ? { ...item, label: val } : item,
                          ),
                        );
                      }}
                      className="h-9 text-xs bg-white"
                    />
                    {canRemove && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label={`Xóa liên kết thứ ${index + 1}`}
                        onClick={() => handleRemove(link.id)}
                        className="size-9 shrink-0 text-slate-400 hover:text-rose-600 hover:bg-rose-50"
                      >
                        <Trash2 className="size-3.5" />
                      </Button>
                    )}
                  </div>
                </div>

                {inlineError && (
                  <p className="mt-1.5 text-[11px] font-medium text-rose-600" role="alert">
                    {inlineError}
                  </p>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
