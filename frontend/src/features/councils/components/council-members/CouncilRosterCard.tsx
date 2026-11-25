import { forwardRef } from 'react';
import { Loader2, Plus } from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Select, type SelectOption } from '@/components/ui/select';
import { StatusBadge } from '@/components/ui/status-badge';
import type { CouncilCandidate, CouncilRole } from '../../services/councilMembersApi';
import {
  COUNCIL_ROLES,
  getRoleAvailability,
  roleLabel,
  type RosterMember,
} from '../../utils/councilMemberWorkspace';

export const CouncilRosterCard = forwardRef<
  HTMLHeadingElement,
  {
    councilCode: string;
    roster: RosterMember[];
    candidates: CouncilCandidate[] | undefined;
    candidatesError: boolean;
    editable: boolean;
    submitted: boolean;
    /** Khóa của thao tác đang chạy trên hội đồng này, ví dụ `role:<userId>`. */
    pendingKey: string | undefined;
    error: string;
    onChangeRole: (member: RosterMember, role: CouncilRole) => void;
    onRemove: (member: RosterMember) => void;
    onAdd: () => void;
  }
>(function CouncilRosterCard(
  { councilCode, roster, candidates, candidatesError, editable, submitted, pendingKey, error, onChangeRole, onRemove, onAdd },
  headingRef,
) {
  const busy = Boolean(pendingKey);
  return (
    <Card className="min-w-0 p-5 sm:p-6" aria-busy={busy}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 ref={headingRef} tabIndex={-1} className="text-base font-semibold text-ink-heading outline-none">
          Thành viên {councilCode} ({roster.length})
        </h2>
        {editable && <Button type="button" size="sm" disabled={busy} onClick={onAdd}><Plus aria-hidden="true" /> Thêm thành viên</Button>}
      </div>
      <p className="mt-1 text-sm text-ink-muted">
        Mỗi hội đồng cần 1 Chủ tịch, 1 Thư ký và Ủy viên. Khi gán nhóm mà chưa có Ủy viên, hệ thống mặc định thêm GVPB kiêm nhiệm. Bạn có thể gỡ và chọn giảng viên khác trước khi gửi PĐT.
      </p>

      {!editable && (
        <div className="mt-4">
          <InlineAlert tone="info">
            Hội đồng đã được phê duyệt hoặc hoàn thành. Bạn có thể xem thành viên nhưng không thể thay đổi danh sách này.
          </InlineAlert>
        </div>
      )}
      {editable && submitted && (
        <div className="mt-4">
          <InlineAlert tone="warning" title="Hồ sơ đã gửi PĐT">
            Nếu thay đổi thành viên, hồ sơ sẽ quay về bước chuẩn bị. Khoa cần gửi lại cho PĐT sau khi chỉnh xong.
          </InlineAlert>
        </div>
      )}
      {error && (
        <div className="mt-4">
          <InlineAlert title="Chưa cập nhật được thành viên">{error}</InlineAlert>
        </div>
      )}

      {roster.length === 0 ? (
        <p className="mt-4 rounded-xl border border-dashed border-line-input p-4 text-sm text-ink-muted">
          {editable
            ? 'Hội đồng chưa có thành viên. Bấm “Thêm thành viên” để tìm và phân công giảng viên.'
            : 'Hội đồng này không có thành viên được ghi nhận.'}
        </p>
      ) : (
        <ul className="mt-4 space-y-2" aria-label="Thành viên hội đồng">
          {roster.map((member) => {
            const candidate = candidates?.find((item) => item.userId === member.userId);
            const rolePending = pendingKey === `role:${member.userId}`;
            const removePending = pendingKey === `remove:${member.userId}`;
            const rolesKnown = candidates !== undefined || candidatesError;
            return (
              <li
                key={member.userId}
                className="flex flex-col gap-3 rounded-xl border border-line/80 bg-surface-subtle/50 p-3 sm:flex-row sm:items-center"
              >
                <div className="flex min-w-0 flex-1 items-center gap-3">
                  <Avatar name={member.fullName} className="size-9" />
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-ink-heading [overflow-wrap:anywhere]">
                      {member.fullName}
                    </p>
                    <div className="text-sm text-ink-muted [overflow-wrap:anywhere]">
                      {candidate?.userCode || candidate?.email || ''}
                      {member.isReviewer && (
                        <StatusBadge tone="info" className="mt-1 whitespace-normal text-left">
                          GVPB kiêm Ủy viên · dùng điểm phản biện đã chốt
                        </StatusBadge>
                      )}
                    </div>
                  </div>
                </div>
                {editable ? (
                  <div className="flex flex-wrap items-end gap-2 sm:flex-nowrap">
                    <div className="min-w-36 flex-1 space-y-1 sm:flex-none">
                      <p
                        id={`role-label-${member.userId}`}
                        className="flex items-center gap-1.5 text-sm font-medium text-ink-secondary"
                      >
                        Vai trò
                        {rolePending && (
                          <span role="status" className="inline-flex items-center gap-1 font-normal text-ink-muted">
                            <Loader2 className="size-3.5 animate-spin" aria-hidden="true" />
                            Đang lưu…
                          </span>
                        )}
                      </p>
                      <Select<CouncilRole>
                        value={member.role}
                        disabled={busy || !rolesKnown}
                        aria-label={`Vai trò của ${member.fullName}`}
                        aria-labelledby={`role-label-${member.userId}`}
                        size="lg"
                        triggerClassName="min-w-36 text-sm"
                        options={COUNCIL_ROLES.map((item): SelectOption<CouncilRole> => {
                          const availability =
                            item.value === member.role
                              ? ({ allowed: true } as const)
                              : getRoleAvailability(roster, candidate, item.value, member.userId);
                          return {
                            value: item.value,
                            label: item.label,
                            disabled: !availability.allowed,
                            description: availability.allowed ? undefined : shortReason(availability.reason),
                          };
                        })}
                        onChange={(role) => onChangeRole(member, role)}
                      />
                    </div>
                    <Button
                      type="button"
                      variant="outline"
                      disabled={busy}
                      isLoading={removePending}
                      loadingText="Đang gỡ…"
                      onClick={() => onRemove(member)}
                      aria-label={`Gỡ ${member.fullName} khỏi hội đồng`}
                      className="h-10"
                    >
                      Gỡ
                    </Button>
                  </div>
                ) : (
                  <StatusBadge tone="neutral">{roleLabel(member.role)}</StatusBadge>
                )}
              </li>
            );
          })}
        </ul>
      )}
      {editable && candidates === undefined && !candidatesError && roster.length > 0 && (
        <p role="status" className="mt-3 text-sm text-ink-muted">
          Đang kiểm tra vai trò hợp lệ của từng giảng viên…
        </p>
      )}
    </Card>
  );
});

function shortReason(reason: string): string {
  const held = reason.match(/^Đã có ([^:]+):/);
  if (held) return `đã có ${held[1]}`;
  if (reason.includes('GVPB')) return 'GVPB chỉ làm Ủy viên';
  if (reason.includes('GVHD')) return 'là GVHD';
  if (reason.includes('lịch')) return 'trùng lịch';
  return 'không hợp lệ';
}
