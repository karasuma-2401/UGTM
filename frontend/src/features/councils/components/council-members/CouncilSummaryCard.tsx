import { Check, CircleDashed, X } from 'lucide-react';
import type { ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { StatusBadge } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import type { CouncilDraftItemDto } from '../../types/councilApproval.types';
import { formatViDate, formatViDateTime } from '../../utils/councilApprovalReadiness';
import {
  councilPhase,
  evaluateSubmitReadiness,
  phaseLabelFor,
  type RosterMember,
} from '../../utils/councilMemberWorkspace';

type CheckState = 'ok' | 'missing' | 'optional';

function CheckRow({ state, label, children }: { state: CheckState; label: string; children: ReactNode }) {
  const Icon = state === 'ok' ? Check : state === 'missing' ? X : CircleDashed;
  return (
    <li className="flex items-start gap-3 py-2">
      <span
        aria-hidden="true"
        className={cn(
          'mt-0.5 flex size-5 shrink-0 items-center justify-center rounded-full',
          state === 'ok' && 'bg-tone-success-bg text-tone-success-text',
          state === 'missing' && 'bg-tone-danger-bg text-tone-danger-text',
          state === 'optional' && 'bg-surface-muted text-ink-muted',
        )}
      >
        <Icon className="size-3.5" />
      </span>
      <div className="min-w-0 text-sm">
        <p className="font-medium text-ink-heading">
          {label}
          <span className="sr-only">
            {state === 'ok' ? ': đã đạt' : state === 'missing' ? ': chưa đạt' : ': tùy chọn'}
          </span>
        </p>
        <p className="text-ink-muted [overflow-wrap:anywhere]">{children}</p>
      </div>
    </li>
  );
}

export function CouncilSummaryCard({
  council,
  roster,
  isAdminActing,
  submitting,
  busyElsewhere,
  submitError,
  onSubmit,
}: {
  council: CouncilDraftItemDto;
  roster: RosterMember[];
  isAdminActing: boolean;
  submitting: boolean;
  /** Đang có thao tác phân công khác trên hội đồng này. */
  busyElsewhere: boolean;
  submitError: string;
  onSubmit: () => void;
}) {
  const phase = councilPhase(council);
  const readiness = evaluateSubmitReadiness(council, roster);
  const chairman = roster.find((m) => m.role === 'Chairman');
  const secretary = roster.find((m) => m.role === 'Secretary');
  const memberCount = roster.filter((m) => m.role === 'Member').length;
  const needsRoles = !readiness.hasChairman || !readiness.hasSecretary;
  const submitHintId = 'council-submit-hint';

  return (
    <Card className="min-w-0 p-5 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-medium text-ink-muted">Thông tin và điều kiện hồ sơ</p>
          <h2 className="text-xl font-semibold text-ink-heading">{council.councilCode}</h2>
          <p className="mt-1 text-sm text-ink-muted">
            {council.facultyName} · {formatViDate(council.defenseDate)} · {council.timeSlot} · Phòng {council.room}
          </p>
        </div>
        <StatusBadge
          tone={council.status === 'Completed' ? 'neutral' : phase === 'approved' ? 'success' : phase === 'submitted' ? 'info' : 'warning'}
        >
          {phaseLabelFor(council)}
        </StatusBadge>
      </div>

      {isAdminActing && (
        <p className="mt-3 rounded-xl bg-surface-subtle px-3 py-2 text-sm text-ink-secondary">
          Bạn đang thao tác thay Khoa với quyền Quản trị viên.
        </p>
      )}

      <h3 className="mt-5 text-sm font-semibold text-ink-heading">Điều kiện để gửi PĐT</h3>
      <ul className="mt-1 grid gap-x-8 md:grid-cols-2">
        <CheckRow state={chairman ? 'ok' : 'missing'} label="Chủ tịch">
          {chairman ? chairman.fullName : 'Chưa phân công. Cần đúng 1 Chủ tịch.'}
        </CheckRow>
        <CheckRow state={secretary ? 'ok' : 'missing'} label="Thư ký">
          {secretary ? secretary.fullName : 'Chưa phân công. Cần đúng 1 Thư ký.'}
        </CheckRow>
        <CheckRow state={memberCount > 0 ? 'ok' : 'missing'} label="Ủy viên">
          {memberCount > 0 ? `${memberCount} người` : 'Chưa có. Thêm GVPB kiêm nhiệm hoặc chọn giảng viên khác.'}
        </CheckRow>
        <CheckRow state={council.groupCodes.length > 0 ? 'ok' : 'missing'} label="Nhóm bảo vệ">
          {council.groupCodes.length > 0
            ? `${council.groupCodes.length} nhóm: ${council.groupCodes.join(', ')}`
            : 'Cần gán ít nhất một nhóm bảo vệ vào hội đồng trước khi gửi PĐT.'}
        </CheckRow>
        <CheckRow state={readiness.groupEligibilityIssue ? 'missing' : council.groupCodes.length > 0 ? 'ok' : 'optional'} label="Điều kiện của nhóm bảo vệ">
          {readiness.groupEligibilityIssue || (council.groupCodes.length > 0
            ? 'Các nhóm đã gán đều đủ điều kiện bảo vệ.'
            : 'Chưa có nhóm để kiểm tra điều kiện bảo vệ.')}
        </CheckRow>
        <CheckRow state={readiness.conflicts.length === 0 ? 'ok' : 'missing'} label="Lịch và phân công giảng viên">
          {readiness.conflicts.length === 0 ? 'Chưa thấy trùng lịch hoặc phân công sai vai trò.' : readiness.conflicts.join(' ')}
        </CheckRow>
      </ul>

      <div className="mt-4 border-t border-line-subtle pt-4">
        {phase === 'approved' && (
          <InlineAlert tone="info" title="Hội đồng đã được PĐT phê duyệt">
            Bạn không thể thay đổi thành viên khi hội đồng đã được phê duyệt.
          </InlineAlert>
        )}

        {phase === 'submitted' && (
          <InlineAlert tone="info" title="Đã gửi PĐT, đang chờ kiểm tra">
            {council.submittedAt ? `Gửi lúc ${formatViDateTime(council.submittedAt)}. ` : ''}
            Nếu bạn thay đổi thành viên lúc này, hồ sơ sẽ quay về bước chuẩn bị và Khoa cần gửi lại cho PĐT.
          </InlineAlert>
        )}

        {phase === 'preparing' && (
          <div className="space-y-3">
            <div>
              <p className="text-sm font-semibold text-ink-heading">Bước tiếp theo</p>
              <p id={submitHintId} className="mt-0.5 text-sm text-ink-muted">
                {readiness.canSubmit
                  ? 'Hồ sơ đã đủ điều kiện. Gửi PĐT để thẩm định và phê duyệt.'
                  : needsRoles
                    ? `Phân công ${[!readiness.hasChairman && 'Chủ tịch', !readiness.hasSecretary && 'Thư ký'].filter(Boolean).join(' và ')} trước khi gửi.`
                    : 'Xem các mục có dấu ✕ ở trên để biết cần xử lý gì trước khi gửi.'}
              </p>
            </div>
            {submitError && <InlineAlert title="Chưa gửi được hồ sơ">{submitError}</InlineAlert>}
            <div>
              <Button
                type="button"
                disabled={!readiness.canSubmit || busyElsewhere}
                isLoading={submitting}
                loadingText="Đang gửi…"
                aria-describedby={submitHintId}
                onClick={onSubmit}
              >
                Gửi PĐT thẩm định
              </Button>
            </div>
          </div>
        )}
      </div>
    </Card>
  );
}
