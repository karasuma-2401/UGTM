import type { ReactNode } from 'react';
import { FileText } from 'lucide-react';
import { cn } from '@/lib/utils';
import { getStudentAccent, type StudentProgressSummary } from './gradingShared';

export interface GradingProgressAsideProps {
  students: readonly StudentProgressSummary[];
  criteriaCount: number;
  hasUnsavedChanges?: boolean;
  noteText: string;
  actions: ReactNode;
}

/** Right sticky progress summary panel with per-student distinct colors. */
export function GradingProgressAside({
  students,
  criteriaCount,
  hasUnsavedChanges = false,
  noteText,
  actions,
}: GradingProgressAsideProps) {
  return (
    <aside className="min-w-0 space-y-4 xl:sticky xl:top-5" aria-label="Tổng quan điểm">
      <section className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-sm">
        <h2 className="font-semibold text-slate-900">Tiến độ chấm</h2>
        <p className="mt-1 text-sm text-slate-500">Điểm tổng hợp theo từng sinh viên.</p>

        <div className="mt-4 divide-y divide-slate-100">
          {students.map((student, index) => {
            const accent = getStudentAccent(index);
            return (
              <div key={student.id} className="py-4 first:pt-0 last:pb-0">
                <div className="flex items-center gap-3">
                  <span
                    className={cn(
                      'flex size-9 shrink-0 items-center justify-center rounded-full border text-xs font-semibold',
                      accent.badgeClass,
                    )}
                  >
                    {student.initials}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-semibold text-slate-900">{student.name}</p>
                    <p className="text-xs text-slate-500">
                      SV{index + 1} · {student.code}
                    </p>
                  </div>
                  <span className="text-lg font-semibold tabular-nums text-slate-900">
                    {student.total.toFixed(2)}
                    <span className="text-xs font-normal text-slate-500"> / 10</span>
                  </span>
                </div>
                <div className="mt-3 flex items-center justify-between text-xs text-slate-600">
                  <span>
                    {student.count}/{criteriaCount} tiêu chí
                  </span>
                  <span>{student.count === criteriaCount ? 'Đã nhập đủ' : 'Chưa hoàn tất'}</span>
                </div>
                <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-100">
                  <div
                    className={cn('h-full rounded-full transition-[width] duration-300', accent.barClass)}
                    style={{
                      width: `${criteriaCount > 0 ? (student.count / criteriaCount) * 100 : 0}%`,
                    }}
                  />
                </div>
              </div>
            );
          })}
        </div>

        <div className="mt-5 space-y-2.5 border-t border-slate-100 pt-4">
          {hasUnsavedChanges && (
            <p className="flex items-center gap-2 text-xs font-medium text-amber-700" role="status">
              <span className="size-1.5 shrink-0 rounded-full bg-amber-500" aria-hidden="true" />
              <span>Có thay đổi chưa lưu</span>
            </p>
          )}
          {actions}
        </div>
      </section>

      <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
        <div className="flex items-center gap-2 font-semibold text-slate-800">
          <FileText className="size-4" aria-hidden="true" /> Lưu ý khi chấm
        </div>
        <p className="mt-2 leading-relaxed">{noteText}</p>
      </div>
    </aside>
  );
}

export interface GradingMobileDockProps {
  students: readonly StudentProgressSummary[];
  visible: boolean;
  actions: ReactNode;
}

/** Fixed bottom bar on mobile và tablet viewports so live scores and Save/Submit stay in reach. */
export function GradingMobileDock({ students, visible, actions }: GradingMobileDockProps) {
  if (!visible || students.length === 0) return null;

  return (
    <div className="sticky bottom-3 z-30 rounded-2xl border border-slate-200/90 bg-white/95 px-4 py-3 shadow-[0_8px_24px_rgba(15,23,42,0.1)] backdrop-blur-md xl:hidden">
      <div className="flex flex-wrap items-center justify-between gap-2.5">
        <div className="flex flex-wrap items-center gap-2">
          {students.map((student, index) => {
            const accent = getStudentAccent(index);
            return (
              <span
                key={student.id}
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-lg border px-2 py-1 text-xs font-semibold tabular-nums',
                  accent.badgeClass,
                )}
              >
                <span className={cn('size-1.5 rounded-full', accent.dotClass)} aria-hidden="true" />
                <span>SV{index + 1}:</span>
                <span className="text-slate-900">{student.total.toFixed(1)}</span>
              </span>
            );
          })}
        </div>
        <div className="flex flex-1 items-center justify-end gap-2 sm:flex-none">{actions}</div>
      </div>
    </div>
  );
}
