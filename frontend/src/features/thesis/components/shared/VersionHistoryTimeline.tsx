import { useState, type ReactNode } from 'react';
import {
  FileText,
  Download,
  Clock,
  MessageSquareText,
  ChevronDown,
} from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { cn } from '@/lib/utils';
import type {
  SubmissionAttachmentDto,
  SubmissionProductLinkDto,
} from '../../types/syllabus.types';
import {
  formatFileSize,
  formatFileSizeDelta,
  formatMilestoneDateTime,
  formatSubmissionDelta,
} from '../../utils/milestoneFormatters';
import {
  SubmissionAttachmentGallery,
  SubmissionProductLinksList,
} from './SubmissionAttachmentGallery';

export interface TimelineReviewInfo {
  decisionLabel: string;
  tone: 'success' | 'warning' | 'error' | 'info' | 'neutral';
  feedback?: string | null;
  reviewedAtUtc?: string | null;
}

export interface TimelineVersionItem {
  key: string | number;
  versionNo: number;
  fileId?: string | null;
  fileName: string;
  fileSize?: number | null;
  timestampUtc?: string | null;
  actorLabel?: string | null;
  statusBadge: ReactNode;
  gitUrl?: string | null;
  productLinks?: SubmissionProductLinkDto[] | null;
  attachments?: SubmissionAttachmentDto[] | null;
  studentNote?: string | null;
  deadlineUtc?: string | null;
  isLate?: boolean;
  review?: TimelineReviewInfo | null;
  extraActions?: ReactNode;
}

interface VersionHistoryTimelineProps {
  title: string;
  items: TimelineVersionItem[];
  onDownload?: (fileId: string) => void;
  resolveAttachmentUrl?: (attachmentId: string) => Promise<string | null>;
  downloadLabel?: string;
  isLoading?: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
  headerAction?: ReactNode;
  omitLatestReview?: boolean;
}

const reviewToneClasses: Record<TimelineReviewInfo['tone'], string> = {
  success: 'border-slate-200/80 bg-slate-50/70 text-slate-800',
  warning: 'border-amber-200/80 bg-amber-50/55 text-amber-950',
  error: 'border-rose-200/80 bg-rose-50/55 text-rose-950',
  info: 'border-slate-200/80 bg-slate-50/70 text-slate-800',
  neutral: 'border-slate-200/80 bg-slate-50/70 text-slate-800',
};

export function VersionHistoryTimeline({
  title,
  items,
  onDownload,
  resolveAttachmentUrl,
  downloadLabel = 'Tải tệp',
  isLoading = false,
  emptyTitle = 'Chưa có phiên bản nào được nộp',
  emptyDescription = 'Khi bạn tải tệp lên và nhấn nộp, lịch sử các phiên bản sẽ hiển thị tại đây.',
  headerAction,
  omitLatestReview = false,
}: VersionHistoryTimelineProps) {
  const [showOlderVersions, setShowOlderVersions] = useState(true);
  const sortedItems = [...items].sort((a, b) => b.versionNo - a.versionNo);
  const highestVersion = sortedItems[0]?.versionNo;
  const olderCount = Math.max(0, sortedItems.length - 1);

  return (
    <Card className="bg-white border-slate-200/80 shadow-xs rounded-2xl overflow-hidden">
      <div className="px-5 sm:px-6 py-4 border-b border-slate-100 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2.5">
          <h3 className="text-base font-semibold text-slate-900">{title}</h3>
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-600 tabular-nums">
            {items.length} phiên bản
          </span>
        </div>
        <div className="flex items-center gap-2">
          {olderCount > 0 && (
            <button
              type="button"
              onClick={() => setShowOlderVersions((prev) => !prev)}
              className="inline-flex items-center gap-1 rounded-lg px-2.5 py-1 text-xs font-medium text-slate-600 hover:bg-slate-100 transition-colors cursor-pointer"
            >
              <ChevronDown
                className={cn(
                  'size-3.5 transition-transform duration-200',
                  showOlderVersions ? 'rotate-180' : '',
                )}
              />
              {showOlderVersions ? 'Thu gọn bản cũ' : `Hiện ${olderCount} bản cũ`}
            </button>
          )}
          {headerAction}
        </div>
      </div>

      {isLoading ? (
        <div className="p-8 text-center text-sm text-slate-500">Đang tải lịch sử nộp…</div>
      ) : sortedItems.length === 0 ? (
        <div className="px-6 py-10 text-center space-y-1.5">
          <div className="size-10 rounded-full bg-slate-100 text-slate-400 flex items-center justify-center mx-auto mb-2">
            <FileText className="size-5" />
          </div>
          <p className="text-sm font-semibold text-slate-800">{emptyTitle}</p>
          <p className="text-xs text-slate-500 max-w-md mx-auto leading-relaxed">
            {emptyDescription}
          </p>
        </div>
      ) : (
        <div className="px-5 sm:px-6 py-4 space-y-4">
          {sortedItems.map((item, index) => {
            const isLatest = item.versionNo === highestVersion;
            const visible = isLatest || showOlderVersions;
            if (!visible) return null;

            const hasLinks = Boolean((item.productLinks && item.productLinks.length > 0) || item.gitUrl);
            const hasAttachments = Boolean(item.attachments && item.attachments.length > 0);
            const olderItem = sortedItems[index + 1];
            const sizeDelta = formatFileSizeDelta(item.fileSize, olderItem?.fileSize);
            const delta =
              item.deadlineUtc && item.timestampUtc
                ? formatSubmissionDelta(item.deadlineUtc, item.timestampUtc)
                : null;
            const hasNextVisible =
              showOlderVersions ? index < sortedItems.length - 1 : false;
            const shouldShowReview = Boolean(item.review && !(omitLatestReview && isLatest));

            return (
              <div
                key={item.key}
                className={cn('relative flex gap-3', !isLatest && 'opacity-85')}
              >
                <div className="flex flex-col items-center shrink-0">
                  <span
                    className={cn(
                      'relative z-10 inline-flex items-center justify-center h-6 min-w-7 px-1.5 rounded-md text-[11px] font-semibold tabular-nums border',
                      isLatest
                        ? 'bg-blue-50 text-[#2054a5] border-blue-200/80'
                        : 'bg-slate-50 text-slate-600 border-slate-200/80',
                    )}
                  >
                    v{item.versionNo}
                  </span>
                  {hasNextVisible && (
                    <span
                      aria-hidden="true"
                      className="w-px flex-1 bg-slate-200 mt-1.5 -mb-2"
                    />
                  )}
                </div>

                <div
                  className={cn(
                    'min-w-0 flex-1 space-y-2 pb-1',
                    hasNextVisible && 'border-b border-slate-100 pb-4',
                  )}
                >
                  <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-2.5">
                    <div className="min-w-0 space-y-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <p
                          className="text-sm font-semibold text-slate-900 break-words [overflow-wrap:anywhere]"
                          title={item.fileName}
                        >
                          {item.fileName}
                        </p>
                        {item.statusBadge}
                      </div>

                      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500">
                        {isLatest && (
                          <span className="font-medium text-slate-700">Bản hiện hành</span>
                        )}
                        {item.fileSize != null && (
                          <span className="tabular-nums">
                            {formatFileSize(item.fileSize)}
                            {sizeDelta && (
                              <span className="text-slate-400 ml-1">({sizeDelta})</span>
                            )}
                          </span>
                        )}
                        {item.timestampUtc && (
                          <span className="inline-flex items-center gap-1">
                            <Clock className="size-3.5 text-slate-400" />
                            {formatMilestoneDateTime(item.timestampUtc)}
                          </span>
                        )}
                        {item.actorLabel && <span>{item.actorLabel}</span>}
                        {(item.isLate || delta) && (
                          <span
                            className={cn(
                              'font-medium',
                              item.isLate || delta?.isLate ? 'text-rose-600' : 'text-slate-600',
                            )}
                          >
                            {delta ? delta.label : 'Nộp trễ hạn'}
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="flex flex-wrap items-center gap-2 shrink-0">
                      {item.extraActions}
                      {item.fileId && onDownload && (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => onDownload(item.fileId!)}
                        >
                          <Download className="size-3.5 mr-1.5" />
                          {downloadLabel}
                        </Button>
                      )}
                    </div>
                  </div>

                  {(hasLinks || hasAttachments || item.studentNote) && (
                    <div className="space-y-2.5">
                      {hasLinks && (
                        <SubmissionProductLinksList
                          links={item.productLinks}
                          legacyGitUrl={item.gitUrl}
                        />
                      )}
                      {hasAttachments && (
                        <SubmissionAttachmentGallery
                          attachments={item.attachments}
                          resolveAttachmentUrl={resolveAttachmentUrl}
                        />
                      )}
                      {item.studentNote && (
                        <div className="rounded-xl border border-slate-200/80 bg-slate-50/80 px-3.5 py-2 text-xs text-slate-700">
                          <span className="font-semibold text-slate-800 block mb-0.5">
                            Ghi chú của sinh viên
                          </span>
                          <RichTextContent content={item.studentNote} className="text-xs" />
                        </div>
                      )}
                    </div>
                  )}

                  {shouldShowReview && item.review && (
                    <div
                      className={cn(
                        'rounded-xl border px-3.5 py-2.5 space-y-1',
                        reviewToneClasses[item.review.tone],
                      )}
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                        <span className="inline-flex items-center gap-1.5 font-semibold">
                          <MessageSquareText className="size-3.5 shrink-0" />
                          {item.review.decisionLabel}
                        </span>
                        {item.review.reviewedAtUtc && (
                          <span className="opacity-80 tabular-nums">
                            {formatMilestoneDateTime(item.review.reviewedAtUtc)}
                          </span>
                        )}
                      </div>
                      {item.review.feedback ? (
                        <RichTextContent content={item.review.feedback} className="text-xs" />
                      ) : (
                        <p className="text-xs opacity-90">
                          Không có nhận xét chi tiết kèm theo.
                        </p>
                      )}
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </Card>
  );
}
