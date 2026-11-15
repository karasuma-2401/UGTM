import { useEffect, useState } from 'react';
import { Eye, EyeOff, Lock, Search, Unlock, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { StatusBadge } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import type { MinuteQueueFilter } from '../../hooks/useCouncilMinutes';
import type { CouncilOverviewApiItemDto } from '../../types/councilApproval.types';
import {
  formatDefenseDateVi,
  getMinuteLockStatusMeta,
} from '../../utils/councilMinuteFormatting';

interface GroupSwitcherDrawerProps {
  open: boolean;
  onClose: () => void;
  items: CouncilOverviewApiItemDto[];
  selectedAssignmentId: string | null;
  search: string;
  onSearchChange: (value: string) => void;
  filter: MinuteQueueFilter;
  onFilterChange: (filter: MinuteQueueFilter) => void;
  stats: {
    all: number;
    ready: number;
    locked: number;
    unlocked: number;
    open: number;
  };
  onSelect: (assignmentId: string) => void;
}

const FILTER_TABS: Array<{ key: MinuteQueueFilter; label: string }> = [
  { key: 'all', label: 'Tất cả' },
  { key: 'discrepancy', label: 'Chưa khóa / Chờ xử lý' },
  { key: 'ready', label: 'Đã đủ điểm' },
  { key: 'locked', label: 'Đã khóa' },
  { key: 'unlocked', label: 'Mở khóa đặc biệt' },
];

export function CouncilMinuteGroupDrawer({
  open,
  onClose,
  items,
  selectedAssignmentId,
  search,
  onSearchChange,
  filter,
  onFilterChange,
  stats,
  onSelect,
}: GroupSwitcherDrawerProps) {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      variant="drawer"
      closeOnBackdropClick
      title={`Danh sách biên bản (${stats.all})`}
      className="sm:max-w-[580px]"
    >
      <div className="p-5 space-y-4">
        <div className="relative">
          <Search
            className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 size-4 text-slate-400"
            aria-hidden="true"
          />
          <Input
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="Tìm theo mã HĐ, tên đề tài, phòng thi, Chủ tịch, Thư ký..."
            className="h-10 pl-9 pr-9 rounded-xl text-sm"
          />
          {search && (
            <button
              type="button"
              aria-label="Xóa từ khóa tìm kiếm"
              onClick={() => onSearchChange('')}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
            >
              <X className="size-3.5" />
            </button>
          )}
        </div>

        <div className="flex flex-wrap gap-1.5">
          {FILTER_TABS.map((tab) => {
            const active = filter === tab.key;
            const count =
              tab.key === 'all'
                ? stats.all
                : tab.key === 'ready'
                  ? stats.ready
                  : tab.key === 'locked'
                    ? stats.locked
                    : tab.key === 'unlocked'
                      ? stats.unlocked
                      : stats.open + stats.ready + stats.unlocked;

            return (
              <button
                key={tab.key}
                type="button"
                onClick={() => onFilterChange(tab.key)}
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium transition-colors',
                  active
                    ? 'border-blue-500 bg-blue-50 text-blue-700 font-semibold'
                    : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50',
                )}
              >
                <span>{tab.label}</span>
                <span className="tabular-nums text-[11px] opacity-80">({count})</span>
              </button>
            );
          })}
        </div>

        {items.length === 0 ? (
          <div className="rounded-xl border border-dashed border-slate-200 bg-slate-50/70 px-4 py-10 text-center text-xs text-slate-500">
            Không có nhóm bảo vệ nào khớp với điều kiện lọc hiện tại.
          </div>
        ) : (
          <ul className="space-y-2.5">
            {items.map((row) => {
              if (!row.assignmentId) return null;
              const isSelected = row.assignmentId === selectedAssignmentId;
              const lockMeta = getMinuteLockStatusMeta(row.minuteLockStatus);
              return (
                <li key={row.assignmentId}>
                  <button
                    type="button"
                    onClick={() => onSelect(row.assignmentId!)}
                    className={cn(
                      'w-full rounded-xl border p-3.5 text-left transition-all',
                      isSelected
                        ? 'border-blue-500 bg-blue-50/70 shadow-2xs'
                        : 'border-slate-200/90 bg-white hover:border-slate-300 hover:bg-slate-50/70',
                    )}
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span className="text-xs font-bold text-slate-900">
                        {row.councilCode} · Nhóm {row.groupCode || '—'}
                      </span>
                      <StatusBadge tone={lockMeta.tone}>{lockMeta.label}</StatusBadge>
                    </div>
                    <p className="mt-1.5 text-sm font-semibold text-slate-900 line-clamp-2">
                      {row.topicTitle || 'Chưa có tên đề tài'}
                    </p>
                    <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500">
                      <span>{formatDefenseDateVi(row.defenseDate)}</span>
                      <span>{row.timeSlot}</span>
                      <span>Phòng {row.room}</span>
                      <span>{row.profileStatusLabel}</span>
                    </div>
                    <div className="mt-1.5 text-[11px] text-slate-500">
                      CT: {row.chairman?.fullName ?? '—'} · TK: {row.secretary?.fullName ?? '—'}
                    </div>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </Dialog>
  );
}

interface LockMinuteDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (password: string) => void;
  isLocking: boolean;
  errorMessage: string | null;
  councilCode: string;
  groupCode: string;
  finalScore: number | null;
}

export function LockMinuteDialog({
  open,
  onClose,
  onConfirm,
  isLocking,
  errorMessage,
  councilCode,
  groupCode,
  finalScore,
}: LockMinuteDialogProps) {
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);

  useEffect(() => {
    if (!open) {
      setPassword('');
      setShowPassword(false);
    }
  }, [open]);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      busy={isLocking}
      title="Xác thực khóa biên bản điểm Hội đồng"
      className="max-w-md"
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onConfirm(password);
        }}
        className="p-5 space-y-4"
      >
        <div className="rounded-xl border border-blue-200/70 bg-blue-50/60 p-3.5 text-xs text-slate-700 space-y-1">
          <p className="font-semibold text-slate-900">
            {councilCode} · Nhóm {groupCode}
            {finalScore !== null ? ` — Điểm dự kiến: ${finalScore.toFixed(2)}` : ''}
          </p>
          <p className="leading-relaxed">
            Sau khi khóa, toàn bộ bảng điểm của nhóm sẽ chuyển sang trạng thái{' '}
            <strong>chỉ đọc</strong>. Thành viên Hội đồng không thể tự chỉnh sửa điểm nếu chưa có
            quyết định Mở khóa đặc biệt từ Lãnh đạo Khoa.
          </p>
        </div>

        {errorMessage && (
          <InlineAlert tone="error" title="Không thể khóa biên bản">
            {errorMessage}
          </InlineAlert>
        )}

        <div className="space-y-1.5">
          <label
            htmlFor="lock-minute-password"
            className="text-xs font-semibold text-slate-800"
          >
            Mật khẩu tài khoản hiện tại <span className="text-rose-600">*</span>
          </label>
          <div className="relative">
            <Input
              id="lock-minute-password"
              type={showPassword ? 'text' : 'password'}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={isLocking}
              autoFocus
              placeholder="Nhập mật khẩu để xác thực danh tính..."
              className="h-10 pr-10 rounded-xl text-sm"
            />
            <button
              type="button"
              onClick={() => setShowPassword((prev) => !prev)}
              aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
            >
              {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            </button>
          </div>
        </div>

        <div className="flex flex-col-reverse sm:flex-row sm:justify-end gap-2 pt-2">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            disabled={isLocking}
            className="h-9 rounded-xl px-4 text-xs font-medium"
          >
            Hủy
          </Button>
          <Button
            type="submit"
            disabled={isLocking || !password.trim()}
            isLoading={isLocking}
            loadingText="Đang xác thực..."
            className="h-9 rounded-xl px-4 text-xs font-semibold"
            leftIcon={<Lock className="size-3.5" />}
          >
            Xác nhận khóa biên bản
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

interface UnlockMinuteDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (overrideReason: string) => void;
  isUnlocking: boolean;
  errorMessage: string | null;
  councilCode: string;
  groupCode: string;
}

export function UnlockMinuteDialog({
  open,
  onClose,
  onConfirm,
  isUnlocking,
  errorMessage,
  councilCode,
  groupCode,
}: UnlockMinuteDialogProps) {
  const [overrideReason, setOverrideReason] = useState('');

  useEffect(() => {
    if (!open) {
      setOverrideReason('');
    }
  }, [open]);

  const trimmedLen = overrideReason.trim().length;
  const isValid = trimmedLen >= 10 && trimmedLen <= 500;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      busy={isUnlocking}
      title="Mở khóa đặc biệt biên bản điểm Hội đồng"
      className="max-w-md"
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onConfirm(overrideReason);
        }}
        className="p-5 space-y-4"
      >
        <div className="rounded-xl border border-rose-200/80 bg-rose-50/50 p-3.5 text-xs text-rose-950 space-y-1">
          <p className="font-semibold">
            {councilCode} · Nhóm {groupCode}
          </p>
          <p className="leading-relaxed">
            Thao tác này mở lại quyền chỉnh sửa điểm cho thành viên Hội đồng khi có khiếu nại hoặc
            sai sót. Toàn bộ thông tin người mở khóa, thời điểm và lý do giải trình sẽ được ghi
            vào Nhật ký kiểm toán (AuditLog).
          </p>
        </div>

        {errorMessage && (
          <InlineAlert tone="error" title="Không thể mở khóa biên bản">
            {errorMessage}
          </InlineAlert>
        )}

        <div className="space-y-1.5">
          <div className="flex items-center justify-between gap-2">
            <label
              htmlFor="unlock-override-reason"
              className="text-xs font-semibold text-slate-800"
            >
              Lý do giải trình mở khóa <span className="text-rose-600">*</span>
            </label>
            <span
              className={cn(
                'text-[11px] tabular-nums font-medium',
                isValid ? 'text-emerald-700' : 'text-amber-700',
              )}
            >
              {trimmedLen}/500 ký tự (tối thiểu 10)
            </span>
          </div>
          <textarea
            id="unlock-override-reason"
            rows={3}
            value={overrideReason}
            onChange={(e) => setOverrideReason(e.target.value)}
            disabled={isUnlocking}
            autoFocus
            placeholder="Ví dụ: Mở khóa theo đơn phúc khảo số 14/PK-KTPM để Hội đồng rà soát lại tiêu chí 2.2..."
            className="w-full rounded-xl border border-slate-200/90 bg-white px-3.5 py-2.5 text-xs sm:text-sm text-slate-800 placeholder:text-slate-400 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/15"
          />
        </div>

        <div className="flex flex-col-reverse sm:flex-row sm:justify-end gap-2 pt-2">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            disabled={isUnlocking}
            className="h-9 rounded-xl px-4 text-xs font-medium"
          >
            Hủy
          </Button>
          <Button
            type="submit"
            variant="destructive"
            disabled={isUnlocking || !isValid}
            isLoading={isUnlocking}
            loadingText="Đang mở khóa..."
            className="h-9 rounded-xl px-4 text-xs font-semibold"
            leftIcon={<Unlock className="size-3.5" />}
          >
            Xác nhận mở khóa
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
