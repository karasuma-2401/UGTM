import { BookOpenCheck, FileText, GraduationCap, UsersRound } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
}

export function TopicPreview({
  values,
  advisorName,
  batchName,
  previewLabel = 'Bản xem trước',
  advisorLoading = false,
  batchLoading = false,
  reviewNote,
}: TopicPreviewProps) {
  const assignedStudents = (values.studentCodes ?? []).map((code) => code.trim()).filter(Boolean);

  return (
    <article className="space-y-5 p-5 sm:p-6">
      <header className="flex items-start gap-3.5">
        <span
          className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-primary ring-1 ring-inset ring-blue-100"
          aria-hidden="true"
        >
          <FileText className="size-5" />
        </span>
        <div className="min-w-0 flex-1 space-y-1.5">
          <Badge variant="secondary">{previewLabel}</Badge>
          <h2 className="break-words text-xl font-bold leading-snug tracking-tight text-slate-900">
            {values.titleVi.trim() || 'Chưa nhập tên đề tài'}
          </h2>
          {values.titleEn.trim() && (
            <p className="break-words text-sm leading-6 text-slate-500">{values.titleEn.trim()}</p>
          )}
        </div>
      </header>

      <TopicReviewNote note={reviewNote} />

      <div className="grid gap-3 sm:grid-cols-3">
        <dl className="contents">
          <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5">
            <dt className="flex items-center gap-1.5 text-xs font-medium text-slate-500">
              <GraduationCap className="size-3.5 text-slate-400" aria-hidden="true" />
              Giảng viên hướng dẫn
            </dt>
            <dd aria-busy={advisorLoading} className="mt-1.5 text-sm font-semibold text-slate-900">
              {advisorLoading ? <Skeleton className="h-5 w-36 max-w-full" /> : advisorName}
            </dd>
          </div>
          <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5">
            <dt className="flex items-center gap-1.5 text-xs font-medium text-slate-500">
              <BookOpenCheck className="size-3.5 text-slate-400" aria-hidden="true" />
              Đợt khóa luận
            </dt>
            <dd aria-busy={batchLoading} className="mt-1.5 text-sm font-semibold text-slate-900">
              {batchLoading ? (
                <Skeleton className="h-5 w-32 max-w-full" />
              ) : (
                batchName || 'Chưa chọn đợt'
              )}
            </dd>
          </div>
        </dl>
        <div className="rounded-xl border border-blue-200/80 bg-blue-50/70 p-3.5">
          <p className="flex items-center gap-1.5 text-xs font-medium text-blue-700">
            <UsersRound className="size-3.5 text-primary" aria-hidden="true" />
            Chỉ tiêu sinh viên
          </p>
          <p className="mt-1.5 text-sm font-semibold tabular-nums text-blue-950">
            {values.studentNumber === null
              ? 'Chưa có chỉ tiêu sinh viên'
              : `Tối đa ${values.studentNumber} sinh viên`}
          </p>
        </div>
      </div>

      {assignedStudents.length > 0 && (
        <section className="space-y-2.5 rounded-xl border border-slate-200/80 bg-slate-50/70 p-4">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-sm font-semibold text-slate-900">Sinh viên chỉ định (MSSV)</h3>
            <span className="rounded-full bg-white px-2.5 py-0.5 text-xs font-medium text-slate-600 ring-1 ring-inset ring-slate-200 tabular-nums">
              {assignedStudents.length} sinh viên
            </span>
          </div>
          <div className="flex flex-wrap gap-2">
            {assignedStudents.map((code, index) => (
              <span
                key={`${code}-${index}`}
                className="inline-flex items-center rounded-lg border border-slate-200 bg-white px-2.5 py-1 font-mono text-xs font-medium tabular-nums text-slate-700"
              >
                {code}
              </span>
            ))}
          </div>
        </section>
      )}

      <div className="space-y-3.5">
        {[
          { title: 'Mục tiêu và mô tả', content: values.description },
          { title: 'Yêu cầu công nghệ và kiến thức', content: values.requirements },
        ].map(({ title, content }) => {
          const trimmed = content.trim();
          return (
            <section
              key={title}
              className="space-y-2 rounded-xl border border-slate-200/80 bg-slate-50/50 p-4 sm:p-5"
            >
              <h3 className="text-sm font-semibold text-slate-900">{title}</h3>
              <p
                className={
                  trimmed
                    ? 'whitespace-pre-wrap break-words text-sm leading-7 text-slate-700'
                    : 'text-sm italic leading-7 text-slate-400'
                }
              >
                {trimmed || 'Chưa bổ sung nội dung.'}
              </p>
            </section>
          );
        })}
      </div>
    </article>
  );
}
