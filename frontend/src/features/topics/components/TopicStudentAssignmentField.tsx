import { AlertCircle, CheckCircle2, Loader2, UsersRound, X } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import type { TopicStudent } from '../types/topic.types';

export type StudentCodeLookupState =
  | { status: 'loading' }
  | {
      status: 'found';
      fullName: string;
      isAlreadyRegisteredInBatch?: boolean;
      isRegistrationBlocked?: boolean;
    }
  | { status: 'not_found' }
  | { status: 'error' };

interface TopicStudentAssignmentFieldProps {
  codes: string[];
  capacity: 1 | 2;
  currentStudents?: TopicStudent[];
  studentLookups?: Record<string, StudentCodeLookupState>;
  disabled?: boolean;
  error?: string;
  onChange: (codes: string[]) => void;
}

export function TopicStudentAssignmentField({
  codes,
  capacity,
  currentStudents = [],
  studentLookups,
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
      className="space-y-3 rounded-xl border border-line bg-surface-subtle/60 p-4"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3
          id="topic-student-codes-label"
          className="flex items-center gap-2 text-sm font-semibold text-ink-heading"
        >
          <UsersRound className="size-4 text-primary" aria-hidden="true" />
          Chỉ định sinh viên{' '}
          <span className="text-xs font-normal text-ink-muted">(tùy chọn)</span>
        </h3>
        <span className="text-xs tabular-nums text-ink-muted">
          {count}/{capacity} sinh viên
        </span>
      </div>
      {currentStudents.length > 0 && (
        <div
          className="space-y-1 text-xs text-ink-body"
          aria-label="Sinh viên đang được ghi nhận"
        >
          {currentStudents.map((student) => (
            <p key={student.studentId} className="break-words">
              {student.fullName} - {student.userCode || student.studentId}
            </p>
          ))}
        </div>
      )}
      <div className="grid gap-3 sm:grid-cols-2">
        {Array.from({ length: Math.max(capacity, Math.min(codes.length, 2)) }, (_, index) => {
          const rawCode = codes[index] ?? '';
          const normalizedCode = rawCode.trim().toUpperCase();
          const currentMatch = normalizedCode
            ? currentStudents.find(
                (student) => student.userCode?.trim().toUpperCase() === normalizedCode,
              )
            : undefined;
          const lookup = normalizedCode && !currentMatch ? studentLookups?.[normalizedCode] : undefined;

          return (
            <div key={index} className="space-y-1.5">
              <label
                htmlFor={`topic-student-code-${index + 1}`}
                className="text-xs font-medium text-ink-body"
              >
                MSSV {index + 1}
              </label>
              <Input
                id={`topic-student-code-${index + 1}`}
                value={rawCode}
                onChange={(event) => changeCode(index, event.target.value)}
                disabled={locked}
                autoComplete="off"
                spellCheck={false}
                maxLength={20}
                placeholder="Nhập mã số sinh viên"
                className="h-10 rounded-lg bg-surface-card shadow-none"
                isInvalid={Boolean(error)}
                aria-describedby={
                  error
                    ? 'topic-student-codes-help topic-student-codes-error'
                    : 'topic-student-codes-help'
                }
                rightIcon={
                  rawCode && !locked ? (
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
              {currentMatch ? (
                <p className="flex items-center gap-1.5 text-xs font-medium text-tone-success-text">
                  <CheckCircle2 className="size-3.5 shrink-0 text-tone-success-icon" aria-hidden="true" />
                  <span className="truncate" title={currentMatch.fullName}>
                    {currentMatch.fullName}
                  </span>
                </p>
              ) : lookup?.status === 'found' && lookup.isAlreadyRegisteredInBatch ? (
                <p role="alert" className="flex items-start gap-1.5 text-xs font-medium text-tone-danger-text">
                  <AlertCircle className="mt-0.5 size-3.5 shrink-0 text-tone-danger-icon" aria-hidden="true" />
                  <span>
                    {lookup.fullName} — Sinh viên này đã đăng ký hoặc được gán vào đề tài khác trong đợt này.
                  </span>
                </p>
              ) : lookup?.status === 'found' && lookup.isRegistrationBlocked ? (
                <p role="alert" className="flex items-start gap-1.5 text-xs font-medium text-tone-danger-text">
                  <AlertCircle className="mt-0.5 size-3.5 shrink-0 text-tone-danger-icon" aria-hidden="true" />
                  <span>
                    {lookup.fullName} — Sinh viên này đang bị khóa quyền đăng ký đề tài.
                  </span>
                </p>
              ) : lookup?.status === 'found' ? (
                <p className="flex items-center gap-1.5 text-xs font-medium text-tone-success-text">
                  <CheckCircle2 className="size-3.5 shrink-0 text-tone-success-icon" aria-hidden="true" />
                  <span className="truncate" title={lookup.fullName}>
                    {lookup.fullName}
                  </span>
                </p>
              ) : lookup?.status === 'loading' ? (
                <p className="flex items-center gap-1.5 text-xs text-ink-muted">
                  <Loader2 className="size-3.5 shrink-0 animate-spin" aria-hidden="true" />
                  <span>Đang kiểm tra MSSV...</span>
                </p>
              ) : lookup?.status === 'not_found' ? (
                <p className="flex items-center gap-1.5 text-xs font-medium text-tone-warning-text">
                  <AlertCircle className="size-3.5 shrink-0 text-tone-warning-icon" aria-hidden="true" />
                  <span>Không tìm thấy sinh viên với MSSV này</span>
                </p>
              ) : lookup?.status === 'error' ? (
                <p className="text-xs text-ink-subtle">Không tra cứu được tên sinh viên lúc này</p>
              ) : null}
            </div>
          );
        })}
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p id="topic-student-codes-help" className="text-xs leading-5 text-ink-muted">
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
        <p className="text-xs text-tone-warning-text">
          Lưu đề tài để bỏ danh sách chỉ định hiện tại.
        </p>
      )}
      {error && (
        <p id="topic-student-codes-error" role="alert" className="text-xs text-tone-danger-icon">
          {error}
        </p>
      )}
    </section>
  );
}
