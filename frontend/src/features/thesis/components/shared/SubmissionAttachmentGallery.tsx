import { useEffect, useState } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  Code2,
  Download,
  ExternalLink,
  FileText,
  Globe,
  Image as ImageIcon,
  Maximize2,
  PlayCircle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import type {
  SubmissionAttachmentDto,
  SubmissionLinkCategory,
  SubmissionProductLinkDto,
} from '../../types/syllabus.types';
import {
  formatFileSize,
  formatGitSourceLink,
  getSafeHttpUrl,
  SUBMISSION_LINK_CATEGORY_LABELS,
} from '../../utils/milestoneFormatters';

const categoryIcons: Record<string, typeof Globe> = {
  SourceCode: Code2,
  Demo: PlayCircle,
  Documentation: FileText,
  Other: Globe,
};

interface SubmissionProductLinksListProps {
  links?: SubmissionProductLinkDto[] | null;
  legacyGitUrl?: string | null;
}

export function SubmissionProductLinksList({
  links,
  legacyGitUrl,
}: SubmissionProductLinksListProps) {
  const normalizedLinks: { id: string; category: string; label?: string | null; url: string }[] =
    links && links.length > 0
      ? links
      : legacyGitUrl
        ? [{ id: 'legacy-git', category: 'SourceCode', label: null, url: legacyGitUrl }]
        : [];

  if (normalizedLinks.length === 0) return null;

  return (
    <div className="flex flex-wrap gap-2">
      {normalizedLinks.map((link) => {
        const safeUrl = getSafeHttpUrl(link.url);
        if (!safeUrl) return null;
        const meta = formatGitSourceLink(safeUrl);
        const categoryLabel =
          SUBMISSION_LINK_CATEGORY_LABELS[link.category as SubmissionLinkCategory] || 'Liên kết';
        const Icon = categoryIcons[link.category] || Globe;

        const text = meta.isLocalPortalUrl
          ? `Liên kết hệ thống (${meta.displayText})`
          : link.label?.trim()
            ? `${link.label.trim()} · ${meta.displayText}`
            : `${categoryLabel}: ${meta.displayText}`;

        return (
          <a
            key={link.id}
            href={safeUrl}
            target="_blank"
            rel="noopener noreferrer"
            title={safeUrl}
            className={cn(
              'inline-flex max-w-full items-center gap-1.5 rounded-lg border px-2.5 py-1 text-xs transition-colors',
              meta.isLocalPortalUrl
                ? 'border-slate-200/80 bg-slate-50 text-slate-600 hover:bg-slate-100'
                : 'border-slate-200/80 bg-slate-50/75 text-[#2054a5] font-medium hover:border-blue-200 hover:bg-blue-50/50',
            )}
          >
            <Icon className="size-3.5 shrink-0 text-slate-500" aria-hidden="true" />
            <span className="truncate">{text}</span>
            <ExternalLink className="size-3 shrink-0 opacity-75" aria-hidden="true" />
          </a>
        );
      })}
    </div>
  );
}

interface SubmissionAttachmentGalleryProps {
  attachments?: SubmissionAttachmentDto[] | null;
  resolveAttachmentUrl?: (attachmentId: string) => Promise<string | null>;
}

export function SubmissionAttachmentGallery({
  attachments,
  resolveAttachmentUrl,
}: SubmissionAttachmentGalleryProps) {
  const [urlMap, setUrlMap] = useState<Record<string, string>>({});
  const [failedMap, setFailedMap] = useState<Record<string, boolean>>({});
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);

  useEffect(() => {
    if (!attachments || attachments.length === 0 || !resolveAttachmentUrl) return;
    let cancelled = false;

    for (const item of attachments) {
      if (urlMap[item.id] || failedMap[item.id]) continue;
      void resolveAttachmentUrl(item.id)
        .then((url) => {
          if (cancelled) return;
          if (url) {
            setUrlMap((prev) => (prev[item.id] ? prev : { ...prev, [item.id]: url }));
          } else {
            setFailedMap((prev) => ({ ...prev, [item.id]: true }));
          }
        })
        .catch(() => {
          if (!cancelled) {
            setFailedMap((prev) => ({ ...prev, [item.id]: true }));
          }
        });
    }

    return () => {
      cancelled = true;
    };
  }, [attachments, resolveAttachmentUrl, urlMap, failedMap]);

  if (!attachments || attachments.length === 0) return null;

  const activeItem =
    lightboxIndex !== null && attachments[lightboxIndex] ? attachments[lightboxIndex] : null;
  const activeUrl = activeItem ? urlMap[activeItem.id] : undefined;

  return (
    <div className="space-y-1.5">
      <p className="inline-flex items-center gap-1.5 text-[11px] font-medium text-slate-600">
        <ImageIcon className="size-3.5 text-slate-400" aria-hidden="true" />
        <span>Ảnh minh chứng ({attachments.length})</span>
      </p>

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 md:grid-cols-5">
        {attachments.map((attachment, idx) => {
          const signedUrl = urlMap[attachment.id];
          const isFailed = failedMap[attachment.id];

          return (
            <button
              key={attachment.id}
              type="button"
              onClick={() => setLightboxIndex(idx)}
              className="group relative overflow-hidden rounded-xl border border-slate-200/80 bg-slate-50 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring cursor-pointer"
              title={`${attachment.fileName} (${formatFileSize(attachment.fileSize)})`}
            >
              <div className="aspect-4/3 w-full bg-slate-100 flex items-center justify-center overflow-hidden">
                {signedUrl ? (
                  <img
                    src={signedUrl}
                    alt={attachment.fileName}
                    className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-105"
                  />
                ) : (
                  <ImageIcon
                    className={cn(
                      'size-5',
                      isFailed ? 'text-slate-300' : 'text-slate-400 animate-pulse',
                    )}
                  />
                )}
              </div>
              <div className="flex items-center justify-between gap-1 px-2 py-1 bg-white border-t border-slate-100">
                <span className="truncate text-[11px] text-slate-700">{attachment.fileName}</span>
                <Maximize2 className="size-3 shrink-0 text-slate-400 group-hover:text-[#2054a5]" />
              </div>
            </button>
          );
        })}
      </div>

      {activeItem && lightboxIndex !== null && (
        <Dialog
          open
          onClose={() => setLightboxIndex(null)}
          title={
            <span className="text-sm font-semibold text-slate-900 truncate block">
              {activeItem.fileName} · {formatFileSize(activeItem.fileSize)}
            </span>
          }
          className="max-w-4xl"
        >
          <div className="p-4 sm:p-5 bg-slate-950/95 flex flex-col items-center gap-3">
            <div className="w-full flex items-center justify-center min-h-[300px] max-h-[68vh]">
              {activeUrl ? (
                <img
                  src={activeUrl}
                  alt={activeItem.fileName}
                  className="max-h-[64vh] max-w-full rounded-lg object-contain"
                />
              ) : (
                <p className="text-xs text-slate-300">Đang tải ảnh minh chứng…</p>
              )}
            </div>

            <div className="flex flex-wrap items-center justify-between w-full gap-3">
              <div className="flex items-center gap-2 text-xs text-slate-300">
                {attachments.length > 1 && (
                  <>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() =>
                        setLightboxIndex(
                          (lightboxIndex - 1 + attachments.length) % attachments.length,
                        )
                      }
                      className="bg-white/10 border-white/20 text-white hover:bg-white/20 hover:text-white"
                    >
                      <ChevronLeft className="size-4 mr-1" />
                      Trước
                    </Button>
                    <span className="tabular-nums font-medium px-1">
                      {lightboxIndex + 1} / {attachments.length}
                    </span>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => setLightboxIndex((lightboxIndex + 1) % attachments.length)}
                      className="bg-white/10 border-white/20 text-white hover:bg-white/20 hover:text-white"
                    >
                      Sau
                      <ChevronRight className="size-4 ml-1" />
                    </Button>
                  </>
                )}
              </div>

              {activeUrl && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => window.open(activeUrl, '_blank', 'noopener,noreferrer')}
                  className="bg-white/10 border-white/20 text-white hover:bg-white/20 hover:text-white"
                >
                  <Download className="size-3.5 mr-1.5" />
                  Mở ảnh gốc
                </Button>
              )}
            </div>
          </div>
        </Dialog>
      )}
    </div>
  );
}
