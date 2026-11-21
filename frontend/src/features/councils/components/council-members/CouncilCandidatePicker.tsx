import { useEffect, useRef, useState } from 'react';
import { Search, X } from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PendingState } from '@/components/ui/pending-state';
import type { CouncilCandidate, CouncilRole } from '../../services/councilMembersApi';
import {
  COUNCIL_ROLES,
  getCandidateStatus,
  getRoleAvailability,
  pickerCandidates,
  roleLabel,
  type RosterMember,
} from '../../utils/councilMemberWorkspace';

const PAGE_SIZE = 8;

export function CouncilCandidatePicker({
  councilCode,
  candidates,
  isLoading,
  error,
  onRetry,
  roster,
  keyword,
  onKeywordChange,
  role,
  onRoleChange,
  pendingKey,
  actionError,
  onAdd,
  onClose,
}: {
  councilCode: string;
  candidates: CouncilCandidate[] | undefined;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
  roster: RosterMember[];
  keyword: string;
  onKeywordChange: (value: string) => void;
  role: CouncilRole;
  onRoleChange: (role: CouncilRole) => void;
  pendingKey: string | undefined;
  actionError: string;
  onAdd: (candidate: CouncilCandidate, role: CouncilRole) => void;
  onClose: () => void;
}) {
  const [limit, setLimit] = useState(PAGE_SIZE);
  const searchRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    const frame = window.requestAnimationFrame(() => searchRef.current?.focus());
    return () => window.cancelAnimationFrame(frame);
  }, []);
  const busy = Boolean(pendingKey);
  const hasSearch = Boolean(keyword.trim());
  const matching = pickerCandidates(candidates ?? [], keyword, role, roster);
  const shown = matching.slice(0, limit);
  const takenSlots = roster.filter((member) => member.role !== 'Member');

  return (
    <Dialog title={`Thêm thành viên · ${councilCode}`} onClose={onClose} busy={busy} className="max-w-3xl">
      <div className="space-y-4 px-5 py-4 sm:px-6">
        <div>
          <p id="add-member-role-label" className="text-sm font-medium text-ink-secondary">Vai trò cần phân công</p>
          <div role="group" aria-labelledby="add-member-role-label" className="mt-2 flex flex-wrap gap-2">
            {COUNCIL_ROLES.map((item) => {
              const taken = item.value !== 'Member' ? roster.find((member) => member.role === item.value) : undefined;
              return (
                <Button
                  key={item.value}
                  type="button"
                  size="sm"
                  variant={role === item.value ? 'default' : 'outline'}
                  aria-pressed={role === item.value}
                  disabled={busy || Boolean(taken)}
                  title={taken ? `Đã có ${item.label}: ${taken.fullName}` : undefined}
                  onClick={() => { onRoleChange(item.value); setLimit(PAGE_SIZE); }}
                >
                  {item.label}
                </Button>
              );
            })}
          </div>
          {takenSlots.length > 0 && (
            <p className="mt-2 text-xs text-ink-muted">
              Đã phân công: {takenSlots.map((member) => `${roleLabel(member.role)} · ${member.fullName}`).join('; ')}.
            </p>
          )}
        </div>

        <div className="space-y-1.5">
          <label htmlFor="member-search" className="text-sm font-medium text-ink-secondary">Tìm giảng viên</label>
          <Input
            id="member-search"
            ref={searchRef}
            autoComplete="off"
            leftIcon={<Search />}
            rightIcon={keyword ? (
              <button type="button" aria-label="Xóa từ khóa tìm giảng viên" className="pointer-events-auto rounded p-0.5 hover:bg-surface-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary" onClick={() => { onKeywordChange(''); setLimit(PAGE_SIZE); }}>
                <X />
              </button>
            ) : undefined}
            value={keyword}
            onChange={(event) => { onKeywordChange(event.target.value); setLimit(PAGE_SIZE); }}
            placeholder="Nhập tên, mã giảng viên hoặc email"
          />
        </div>

        {actionError && <InlineAlert title="Chưa thêm được thành viên">{actionError} Bạn có thể chọn lại giảng viên hoặc vai trò.</InlineAlert>}
        {error && <InlineAlert title="Không tải được giảng viên" action={<button type="button" className="font-medium underline" onClick={onRetry}>Thử lại</button>}>{error}</InlineAlert>}
        {isLoading && <PendingState label="Đang tải giảng viên…" />}
        {!isLoading && !error && !hasSearch && role !== 'Member' && <p className="rounded-xl bg-surface-subtle px-3 py-4 text-sm text-ink-muted">Nhập tên hoặc mã giảng viên để tìm, sau đó bấm “Thêm”.</p>}
        {!isLoading && !error && (hasSearch || role === 'Member') && (
          <div>
            <p className="text-sm text-ink-muted" aria-live="polite">
              {hasSearch
                ? `${matching.length} giảng viên khớp tìm kiếm${role === 'Member' ? ' · GVPB của nhóm được ưu tiên' : ''}`
                : `${matching.length} GVPB của nhóm chưa ở hội đồng. Bạn cũng có thể tìm giảng viên khác ở ô trên.`}
            </p>
            {matching.length === 0 && <p className="mt-2 rounded-xl bg-surface-subtle px-3 py-4 text-sm text-ink-muted">{hasSearch ? 'Không tìm thấy giảng viên. Thử tên, mã hoặc email khác.' : 'Chưa có GVPB nào để gợi ý. Tìm giảng viên khác bằng tên, mã hoặc email.'}</p>}
            {matching.length > 0 && (
              <ul className="mt-2 max-h-[min(46dvh,24rem)] space-y-1 overflow-y-auto pr-1" aria-label="Kết quả tìm giảng viên">
                {shown.map((candidate) => {
                  const status = getCandidateStatus(candidate);
                  const availability = getRoleAvailability(roster, candidate, role, candidate.userId);
                  const alreadyAssigned = status.kind === 'in-council' || roster.some((member) => member.userId === candidate.userId);
                  const canAdd = !alreadyAssigned && availability.allowed;
                  const reason = alreadyAssigned
                    ? `Đã là ${roleLabel(roster.find((member) => member.userId === candidate.userId)?.role ?? (status.kind === 'in-council' ? status.role : 'Member'))} của hội đồng này.`
                    : availability.allowed ? '' : availability.reason;
                  const rowPending = pendingKey === `add:${candidate.userId}`;
                  return (
                    <li key={candidate.userId} className="flex flex-wrap items-center gap-3 rounded-xl border border-line/80 px-3 py-2.5">
                      <Avatar name={candidate.fullName} muted={!canAdd} className="size-9" />
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <p className="text-sm font-medium text-ink-heading [overflow-wrap:anywhere]">{candidate.fullName}</p>
                          {candidate.isReviewerForCouncilGroups && <Badge variant="info">GVPB của nhóm</Badge>}
                        </div>
                        <p className="text-xs text-ink-muted [overflow-wrap:anywhere]">{[candidate.userCode, candidate.email].filter(Boolean).join(' · ')}</p>
                        {reason && <p className="mt-0.5 text-xs text-tone-warning-text">{reason}</p>}
                      </div>
                      <Button type="button" size="sm" disabled={busy || !canAdd} isLoading={rowPending} loadingText="Đang thêm…" onClick={() => onAdd(candidate, role)} aria-label={`Thêm ${candidate.fullName} làm ${roleLabel(role)}`}>
                        Thêm
                      </Button>
                    </li>
                  );
                })}
              </ul>
            )}
            {matching.length > shown.length && (
              <Button type="button" size="sm" variant="outline" className="mt-2" onClick={() => setLimit((value) => value + PAGE_SIZE)}>
                Hiển thị thêm {Math.min(PAGE_SIZE, matching.length - shown.length)} người
              </Button>
            )}
          </div>
        )}
      </div>
    </Dialog>
  );
}
