import { BookOpenCheck, GraduationCap, UsersRound } from 'lucide-react';
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

export function TopicPreview({
  values,
  advisorName,
  batchName,
  previewLabel = 'Bản xem trước',
  advisorLoading = false,
  batchLoading = false,
  reviewNote,
  showQuota = true,
}: TopicPreviewProps) {
  const assignedStudents = (values.studentCodes ?? []).map((code) => code.trim()).filter(Boolean);

  return (
    <article className="space-y-5 p-5 sm:p-6">
      <header className="space-y-1.5">
        {previewLabel && (
          <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-400">
            {previewLabel}
          </p>
        )}
        <h2 className="break-words text-lg font-bold leading-snug tracking-tight text-slate-900 sm:text-xl">
          {values.titleVi.trim() || 'Chưa nhập tên đề tài'}
        </h2>
        {values.titleEn.trim() && (
          <p className="break-words text-sm leading-6 text-slate-500">{values.titleEn.trim()}</p>
        )}
      </header>

      <TopicReviewNote note={reviewNote} />

      <div
        className={`grid divide-y divide-slate-200/80 overflow-hidden rounded-xl border border-slate-200/80 bg-slate-50/60 sm:divide-x sm:divide-y-0 ${
          showQuota ? 'sm:grid-cols-3' : 'sm:grid-cols-2'
        }`}
      >
        <dl className="contents">
          <div className="p-3.5">
            <dt className="flex items-center gap-1.5 text-[11px] font-medium uppercase tracking-wider text-slate-400">
              <GraduationCap className="size-3.5 text-slate-400" aria-hidden="true" />
              Giảng viên hướng dẫn
            </dt>
            <dd aria-busy={advisorLoading} className="mt-1 text-sm font-semibold text-slate-900">
              {advisorLoading ? <Skeleton className="h-5 w-36 max-w-full" /> : advisorName}
            </dd>
          </div>
          <div className="p-3.5">
            <dt className="flex items-center gap-1.5 text-[11px] font-medium uppercase tracking-wider text-slate-400">
              <BookOpenCheck className="size-3.5 text-slate-400" aria-hidden="true" />
              Đợt khóa luận
            </dt>
            <dd aria-busy={batchLoading} className="mt-1 text-sm font-semibold text-slate-900">
              {batchLoading ? (
                <Skeleton className="h-5 w-32 max-w-full" />
              ) : (
                batchName || 'Chưa chọn đợt'
              )}
            </dd>
          </div>
        </dl>
        {showQuota && (
          <div className="bg-blue-50/40 p-3.5">
            <p className="flex items-center gap-1.5 text-[11px] font-medium uppercase tracking-wider text-primary">
              <UsersRound className="size-3.5 text-primary" aria-hidden="true" />
              Chỉ tiêu sinh viên
            </p>
            <p className="mt-1 text-sm font-semibold tabular-nums text-slate-900">
              {values.studentNumber === null
                ? 'Chưa có chỉ tiêu sinh viên'
                : `Tối đa ${values.studentNumber} sinh viên`}
            </p>
          </div>
        )}
      </div>

      {assignedStudents.length > 0 && (
        <section className="space-y-2.5 border-t border-slate-100 pt-4">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
              Sinh viên chỉ định (MSSV)
            </h3>
            <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-600 tabular-nums">
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

      <div className="space-y-4 divide-y divide-slate-100 border-t border-slate-100 pt-4">
        {[
          { title: 'Mục tiêu và mô tả', content: values.description },
          { title: 'Yêu cầu công nghệ và kiến thức', content: values.requirements },
        ].map(({ title, content }, index) => {
          const trimmed = content.trim();
          return (
            <section key={title} className={index > 0 ? 'space-y-1.5 pt-4' : 'space-y-1.5'}>
              <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                {title}
              </h3>
              <p
                className={
                  trimmed
                    ? 'whitespace-pre-wrap break-words text-sm leading-7 text-slate-700'
                    : 'text-sm italic leading-6 text-slate-400'
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
