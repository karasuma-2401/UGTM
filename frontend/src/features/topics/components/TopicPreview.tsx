import { BookOpenCheck, ListChecks, Target, UsersRound } from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { Skeleton } from '@/components/ui/skeleton';
import type { TopicFormValues } from '../types/topic.types';
import { TopicReviewNote } from './TopicReviewNote';

interface TopicPreviewProps {
  values: Omit<TopicFormValues, 'studentNumber'> & { studentNumber: number | null };
  advisorName: string;
  batchName?: string;
  previewLabel?: string;
  advisorLoading?: boolean;
  batchLoading?: boolean;
  reviewNote?: string | null;
  showQuota?: boolean;
}

const LIST_LINE_PATTERN = /^\s*(?:[-*•]|(?:\d+[.)]))\s+(.+)$/;

function parseRequirementItems(raw: string): string[] | null {
  const lines = raw
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
  if (lines.length === 0) return null;

  const items: string[] = [];
  for (const line of lines) {
    const match = LIST_LINE_PATTERN.exec(line);
    if (!match) return null;
    items.push(match[1].trim());
  }
  return items;
}

export function TopicPreview({
  values,
  advisorName,
  batchName,
  previewLabel = 'Nội dung đề tài',
  advisorLoading = false,
  batchLoading = false,
  reviewNote,
  showQuota = true,
}: TopicPreviewProps) {
  const assignedStudents = (values.studentCodes ?? []).map((code) => code.trim()).filter(Boolean);
  const trimmedDescription = values.description.trim();
  const trimmedRequirements = values.requirements.trim();
  const requirementItems = parseRequirementItems(trimmedRequirements);

  return (
    <article aria-label={previewLabel || 'Nội dung đề tài'} className="space-y-5 p-5 sm:p-6">
      <header className="space-y-1.5">
        <h2 className="break-words text-lg font-bold leading-snug tracking-tight text-slate-900 sm:text-[19px]">
          {values.titleVi.trim() || 'Chưa nhập tên đề tài'}
        </h2>
        {values.titleEn.trim() && (
          <p className="break-words text-[13px] italic leading-relaxed text-slate-500">
            {values.titleEn.trim()}
          </p>
        )}
      </header>

      <dl className="flex flex-wrap items-center gap-x-4 gap-y-2.5 border-y border-slate-100 py-3">
        <div className="flex min-w-0 items-center gap-2.5">
          <dt className="sr-only">Giảng viên hướng dẫn</dt>
          <dd aria-busy={advisorLoading} className="flex min-w-0 items-center gap-2.5">
            {advisorLoading ? (
              <Skeleton className="h-7 w-40 max-w-full rounded-full" />
            ) : (
              <>
                <Avatar
                  name={advisorName || '?'}
                  className="size-7 text-[11px] ring-1 ring-slate-200/80"
                />
                <div className="min-w-0 leading-tight">
                  <span className="block text-[11px] text-slate-500">Giảng viên hướng dẫn</span>
                  <span className="block truncate text-[13px] font-semibold text-slate-900">
                    {advisorName}
                  </span>
                </div>
              </>
            )}
          </dd>
        </div>

        <span className="hidden h-6 w-px bg-slate-200/80 sm:block" aria-hidden="true" />

        <div className="flex min-w-0 items-center gap-2">
          <dt className="sr-only">Đợt khóa luận</dt>
          <dd aria-busy={batchLoading} className="min-w-0">
            {batchLoading ? (
              <Skeleton className="h-6 w-32 max-w-full rounded-full" />
            ) : (
              <span className="inline-flex max-w-full items-center gap-1.5 rounded-lg border border-slate-200/80 bg-slate-50/80 px-2.5 py-1 text-xs font-medium text-slate-700">
                <BookOpenCheck className="size-3.5 shrink-0 text-slate-500" aria-hidden="true" />
                <span className="truncate">{batchName || 'Chưa chọn đợt'}</span>
              </span>
            )}
          </dd>
        </div>

        {showQuota && (
          <span className="inline-flex items-center gap-1.5 rounded-lg border border-blue-200/70 bg-blue-50/70 px-2.5 py-1 text-xs font-medium tabular-nums text-primary">
            <UsersRound className="size-3.5 shrink-0" aria-hidden="true" />
            {values.studentNumber === null
              ? 'Chưa có chỉ tiêu'
              : `Tối đa ${values.studentNumber} sinh viên`}
          </span>
        )}
      </dl>

      <TopicReviewNote note={reviewNote} />

      {assignedStudents.length > 0 && (
        <section className="space-y-2.5">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-[13px] font-semibold text-slate-900">Sinh viên chỉ định (MSSV)</h3>
            <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-[11px] font-medium tabular-nums text-slate-600">
              {assignedStudents.length} sinh viên
            </span>
          </div>
          <div className="flex flex-wrap gap-2">
            {assignedStudents.map((code, index) => (
              <span
                key={`${code}-${index}`}
                className="inline-flex items-center rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1 font-mono text-xs font-medium tabular-nums text-slate-700"
              >
                {code}
              </span>
            ))}
          </div>
        </section>
      )}

      <section className="space-y-2 pt-1">
        <h3 className="flex items-center gap-2 text-[13px] font-semibold text-slate-900">
          <Target className="size-4 text-primary" aria-hidden="true" />
          <span>Mục tiêu và mô tả</span>
        </h3>
        {trimmedDescription ? (
          <RichTextContent
            content={trimmedDescription}
            className="text-sm leading-6 text-slate-700"
          />
        ) : (
          <p className="text-sm italic leading-6 text-slate-500">Chưa bổ sung nội dung.</p>
        )}
      </section>

      <section className="space-y-2.5 rounded-xl border border-slate-200/80 bg-slate-50/70 p-4">
        <div className="flex items-center justify-between gap-2">
          <h3 className="flex items-center gap-2 text-[13px] font-semibold text-slate-900">
            <ListChecks className="size-4 text-slate-600" aria-hidden="true" />
            <span>Yêu cầu công nghệ và kiến thức</span>
          </h3>
          {requirementItems && (
            <span className="rounded-full bg-white px-2 py-0.5 text-[11px] font-medium tabular-nums text-slate-600 ring-1 ring-inset ring-slate-200/80">
              {`${requirementItems.length} tiêu chí`}
            </span>
          )}
        </div>

        {!trimmedRequirements ? (
          <p className="text-sm italic leading-6 text-slate-500">Chưa bổ sung nội dung.</p>
        ) : requirementItems ? (
          <ul className="space-y-2">
            {requirementItems.map((item, index) => (
              <li key={`${index}-${item.slice(0, 24)}`} className="flex items-start gap-2.5">
                <span
                  aria-hidden="true"
                  className="mt-2.5 size-1.5 shrink-0 rounded-full bg-primary/70"
                />
                <RichTextContent
                  content={item}
                  className="min-w-0 flex-1 text-sm leading-6 text-slate-700"
                />
              </li>
            ))}
          </ul>
        ) : (
          <RichTextContent
            content={trimmedRequirements}
            className="text-sm leading-6 text-slate-700"
          />
        )}
      </section>
    </article>
  );
}
