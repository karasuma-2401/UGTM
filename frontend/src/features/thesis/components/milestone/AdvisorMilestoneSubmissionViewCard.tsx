import { ArrowUpRight, Download, FileText, GitBranch } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { RichTextContent } from '@/components/ui/rich-text-content';
import type { SubmissionFileDto } from '../../types/syllabus.types';
import {
  formatFileSize,
  formatMilestoneDateTime,
  getSafeHttpUrl,
} from '../../utils/milestoneFormatters';

export interface AdvisorMilestoneSubmissionViewCardProps {
  versionNo: number;
  groupCode?: string;
  file?: SubmissionFileDto | null;
  gitUrl?: string | null;
  studentNote?: string | null;
  onDownloadFile: (fileId: string) => void;
  isDownloading?: boolean;
}

export function AdvisorMilestoneSubmissionViewCard({
  versionNo,
  groupCode = 'Nhóm KLTN',
  file,
  gitUrl,
  studentNote,
  onDownloadFile,
  isDownloading = false,
}: AdvisorMilestoneSubmissionViewCardProps) {
  const safeGitUrl = getSafeHttpUrl(gitUrl);

  return (
    <section className="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-2xs">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-5 py-4">
        <div className="space-y-0.5">
          <h2 className="text-base font-semibold text-slate-900">
            Nội dung bài nộp · Phiên bản v{versionNo}
          </h2>
          <p className="text-xs text-slate-600">{groupCode}</p>
        </div>
        {file?.uploadedAtUtc && (
          <span className="text-xs text-slate-600">
            Tải lên lúc {formatMilestoneDateTime(file.uploadedAtUtc)}
          </span>
        )}
      </div>

      <div className="space-y-4 p-5">
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5">
            <p className="text-xs font-semibold text-slate-600">Tệp báo cáo đính kèm</p>
            {file ? (
              <div className="mt-2 flex items-center justify-between gap-3">
                <div className="flex min-w-0 items-center gap-2.5">
                  <FileText className="size-4 shrink-0 text-primary" aria-hidden="true" />
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-slate-900">{file.fileName}</p>
                    <p className="text-xs text-slate-600">{formatFileSize(file.fileSize)}</p>
                  </div>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={isDownloading}
                  onClick={() => onDownloadFile(file.id)}
                  className="h-8 shrink-0 gap-1.5 text-xs"
                >
                  <Download className="size-3.5" aria-hidden="true" />
                  <span>{isDownloading ? 'Đang tải...' : 'Tải xuống'}</span>
                </Button>
              </div>
            ) : (
              <p className="mt-2 text-sm text-slate-500">Không có tệp đính kèm</p>
            )}
          </div>

          <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5">
            <p className="text-xs font-semibold text-slate-600">Liên kết mã nguồn (Git)</p>
            {safeGitUrl ? (
              <a
                href={safeGitUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="mt-2 inline-flex max-w-full items-center gap-1.5 text-sm font-medium text-primary hover:underline"
              >
                <GitBranch className="size-4 shrink-0" aria-hidden="true" />
                <span className="truncate">{gitUrl}</span>
                <ArrowUpRight className="size-3.5 shrink-0" aria-hidden="true" />
              </a>
            ) : (
              <p className="mt-2 text-sm text-slate-500">{gitUrl || 'Không có liên kết Git'}</p>
            )}
          </div>
        </div>

        <div className="space-y-1.5">
          <p className="text-xs font-semibold text-slate-600">Ghi chú của sinh viên</p>
          <div className="rounded-xl border border-slate-100 bg-slate-50/60 p-4 text-sm text-slate-700">
            <RichTextContent
              content={studentNote}
              emptyFallback="Sinh viên không để lại ghi chú cho phiên bản này."
            />
          </div>
        </div>
      </div>
    </section>
  );
}
