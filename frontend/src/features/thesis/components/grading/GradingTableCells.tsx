import { AvatarGroup } from '@/components/ui/avatar-group';
import { Badge } from '@/components/ui/badge';
import type { GradingStudentInfo } from './gradingShared';

export interface GradingStudentsCellProps {
  students: readonly GradingStudentInfo[];
  ariaLabel?: string;
}

/** Render overlapping student avatars alongside full names and student codes to match UGTM tables. */
export function GradingStudentsCell({
  students,
  ariaLabel = 'Thành viên nhóm',
}: GradingStudentsCellProps) {
  if (students.length === 0) {
    return <span className="text-sm text-slate-400">Chưa có sinh viên</span>;
  }

  return (
    <div className="flex items-start gap-3">
      <AvatarGroup
        items={students.map((student) => ({
          id: student.id,
          name: student.name,
          userCode: student.code !== '—' ? student.code : null,
        }))}
        max={2}
        size="md"
        className="mt-0.5 shrink-0"
        ariaLabel={ariaLabel}
      />
      <div className="min-w-0 space-y-1 text-sm">
        {students.map((student) => (
          <p key={student.id} className="truncate font-medium text-slate-800">
            {student.name}{' '}
            <span className="font-normal text-slate-500">· {student.code}</span>
          </p>
        ))}
      </div>
    </div>
  );
}

export interface GradingStatusCellProps {
  status: 'empty' | 'progress' | 'complete';
  filledCount: number;
  totalCount: number;
  lockedLabel?: string | null;
  submittedLabel?: string | null;
}

/** Render grading completion badge paired with a compact progress track for partial drafts. */
export function GradingStatusCell({
  status,
  filledCount,
  totalCount,
  lockedLabel,
  submittedLabel,
}: GradingStatusCellProps) {
  if (lockedLabel) {
    return (
      <Badge dot rounded="full" variant="success" className="max-w-full whitespace-nowrap">
        {lockedLabel}
      </Badge>
    );
  }

  if (submittedLabel) {
    return (
      <Badge dot rounded="full" variant="success" className="max-w-full whitespace-nowrap">
        {submittedLabel}
      </Badge>
    );
  }

  const ratio = totalCount > 0 ? Math.min(100, Math.round((filledCount / totalCount) * 100)) : 0;

  return (
    <div className="inline-flex flex-col items-start gap-1.5">
      <Badge
        dot
        rounded="full"
        variant={status === 'empty' ? 'warning' : status === 'progress' ? 'info' : 'success'}
        className="max-w-full whitespace-nowrap"
      >
        {status === 'empty'
          ? 'Chưa nhập điểm'
          : status === 'progress'
            ? `Đang chấm · ${filledCount}/${totalCount}`
            : 'Đã nhập đủ'}
      </Badge>
      {status === 'progress' && totalCount > 0 && (
        <div
          aria-hidden="true"
          className="h-1 w-24 overflow-hidden rounded-full bg-slate-100"
        >
          <div
            className="h-full rounded-full bg-blue-600 transition-[width] duration-300"
            style={{ width: `${ratio}%` }}
          />
        </div>
      )}
    </div>
  );
}
