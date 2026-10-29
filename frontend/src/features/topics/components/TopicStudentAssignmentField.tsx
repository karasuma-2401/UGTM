import { UsersRound, X } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import type { TopicStudent } from '../types/topic.types';

interface TopicStudentAssignmentFieldProps {
  codes: string[];
  capacity: 1 | 2;
  currentStudents?: TopicStudent[];
  disabled?: boolean;
  error?: string;
  onChange: (codes: string[]) => void;
}

export function TopicStudentAssignmentField({
  codes,
  capacity,
  currentStudents = [],
  disabled = false,
  error,
  onChange,
}: TopicStudentAssignmentFieldProps) {
  // Without MSSV we cannot round-trip the existing roster through studentCodes.
  const missingCodes = currentStudents.some((student) => !student.userCode?.trim());
  const locked = disabled || missingCodes;
  const count = codes.filter((code) => code.trim()).length;
  function changeCode(index: number, value: string) {
    const next = Array.from(
      { length: Math.max(codes.length, index + 1) },
      (_, i) => codes[i] ?? '',
    );
    next[index] = value;
    while (next.length && next[next.length - 1] === '') next.pop();
    onChange(next);
  }

  return (
    <section
      id="topic-student-codes"
      aria-labelledby="topic-student-codes-label"
      className="space-y-3 rounded-xl border border-slate-200 bg-slate-50/60 p-4"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3
          id="topic-student-codes-label"
          className="flex items-center gap-2 text-sm font-semibold text-slate-800"
        >
          <UsersRound className="size-4 text-primary" aria-hidden="true" />
          Chỉ định sinh viên <span className="text-xs font-normal text-slate-500">(tùy chọn)</span>
        </h3>
        <span className="text-xs tabular-nums text-slate-500">
          {count}/{capacity} sinh viên
        </span>
      </div>
      {currentStudents.length > 0 && (
        <div className="space-y-1 text-xs text-slate-600" aria-label="Sinh viên đang được ghi nhận">
          {currentStudents.map((student) => (
            <p key={student.studentId} className="break-words">
              {student.fullName} · {student.userCode || student.studentId}
            </p>
          ))}
        </div>
      )}
      <div className="grid gap-3 sm:grid-cols-2">
        {Array.from({ length: Math.max(capacity, Math.min(codes.length, 2)) }, (_, index) => (
          <div key={index} className="space-y-1.5">
            <label
              htmlFor={`topic-student-code-${index + 1}`}
              className="text-xs font-medium text-slate-600"
            >
              MSSV {index + 1}
            </label>
            <Input
              id={`topic-student-code-${index + 1}`}
              value={codes[index] ?? ''}
              onChange={(event) => changeCode(index, event.target.value)}
              disabled={locked}
              autoComplete="off"
              spellCheck={false}
              maxLength={20}
              placeholder="Nhập mã số sinh viên"
              className="h-10 rounded-lg bg-white shadow-none"
              isInvalid={Boolean(error)}
              aria-describedby={
                error
                  ? 'topic-student-codes-help topic-student-codes-error'
                  : 'topic-student-codes-help'
              }
              rightIcon={
                codes[index] && !locked ? (
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="size-6"
                    aria-label={`Xóa MSSV ${index + 1}`}
                    onClick={() => changeCode(index, '')}
                  >
                    <X className="size-3" />
                  </Button>
                ) : undefined
              }
            />
          </div>
        ))}
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p id="topic-student-codes-help" className="text-xs leading-5 text-slate-500">
          {missingCodes
            ? 'Chưa có đủ MSSV để chỉnh sửa danh sách hiện tại.'
            : 'Chỉ định khi lưu lên hệ thống, trong giai đoạn chuẩn bị đề tài.'}
        </p>
        {count > 0 && !locked && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 text-xs"
            onClick={() => onChange([])}
          >
            Xóa danh sách
          </Button>
        )}
      </div>
      {currentStudents.length > 0 && count === 0 && !locked && (
        <p className="text-xs text-amber-700">Lưu đề tài để bỏ danh sách chỉ định hiện tại.</p>
      )}
      {error && (
        <p id="topic-student-codes-error" role="alert" className="text-xs text-rose-600">
          {error}
        </p>
      )}
    </section>
  );
}
