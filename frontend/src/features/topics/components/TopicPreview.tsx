import { FileText, UsersRound } from 'lucide-react';
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
  return (
    <article className="space-y-6 p-5 sm:p-6">
      <div className="flex items-start gap-3">
        <span
          className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-primary ring-1 ring-inset ring-blue-100"
          aria-hidden="true"
        >
          <FileText className="size-5" />
        </span>
        <div className="min-w-0 space-y-2">
          <Badge variant="secondary">{previewLabel}</Badge>
          <h2 className="break-words text-xl font-bold leading-snug text-slate-900">
            {values.titleVi.trim() || 'Chưa nhập tên đề tài'}
          </h2>
          {values.titleEn.trim() && (
            <p className="break-words text-sm text-slate-500">{values.titleEn.trim()}</p>
          )}
        </div>
      </div>
      <TopicReviewNote note={reviewNote} />
      <dl className="grid gap-4 rounded-xl border border-slate-200/80 bg-slate-50 p-4 sm:grid-cols-2">
        <div>
          <dt className="text-xs text-slate-500">Giảng viên hướng dẫn</dt>
          <dd aria-busy={advisorLoading} className="mt-1 text-sm font-medium text-slate-800">
            {advisorLoading ? <Skeleton className="h-5 w-36 max-w-full" /> : advisorName}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-slate-500">Đợt KLTN</dt>
          <dd aria-busy={batchLoading} className="mt-1 text-sm font-medium text-slate-800">
            {batchLoading ? (
              <Skeleton className="h-5 w-32 max-w-full" />
            ) : (
              batchName || 'Chưa chọn đợt'
            )}
          </dd>
        </div>
      </dl>
      <div className="inline-flex items-center gap-2 rounded-lg bg-blue-50 px-3 py-2 text-sm text-blue-700">
        <UsersRound className="size-4" aria-hidden="true" />
        {values.studentNumber === null
          ? 'Chưa có chỉ tiêu sinh viên'
          : `Tối đa ${values.studentNumber} sinh viên`}
      </div>
      {values.studentCodes?.some((code) => code.trim()) && (
        <section className="space-y-2 rounded-xl border border-slate-200 p-4">
          <h3 className="text-sm font-semibold text-slate-800">Sinh viên chỉ định (MSSV)</h3>
          <p className="text-sm text-slate-600">
            {values.studentCodes
              .map((code) => code.trim())
              .filter(Boolean)
              .join(' · ')}
          </p>
        </section>
      )}
      {[
        { title: 'Mục tiêu và mô tả', content: values.description },
        { title: 'Yêu cầu công nghệ và kiến thức', content: values.requirements },
      ].map(({ title, content }) => (
        <section key={title} className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-800">{title}</h3>
          <p className="whitespace-pre-wrap break-words text-sm leading-7 text-slate-600">
            {content.trim() || 'Chưa bổ sung nội dung.'}
          </p>
        </section>
      ))}
    </article>
  );
}
