import { AlertTriangle, CheckCircle2 } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { ProgressBar } from '@/components/ui/progress';
import { StatusBadge } from '@/components/ui/status-badge';
import { SYSTEM_ROLES } from '@/features/identity/constants/systemRoles';
import type { UserProfile } from '@/features/auth/types/auth.types';
import { cn } from '@/lib/utils';
import { computeSecurityHealth } from '../utils/profileFormatters';
import { ProfileThesisContextSection } from './ProfileThesisContextSection';

interface ProfileSecuritySidebarProps {
  user: UserProfile;
  activeSessionCount: number | null;
  onJumpToSecurity: () => void;
}

export function ProfileSecuritySidebar({
  user,
  activeSessionCount,
  onJumpToSecurity,
}: ProfileSecuritySidebarProps) {
  const health = computeSecurityHealth(user, activeSessionCount);

  return (
    <aside className="lg:sticky lg:top-24 space-y-4">
      <Card className="overflow-hidden divide-y divide-line-subtle">
        {/* Block 1: Real Security Posture */}
        <section className="p-5 space-y-4" aria-label="Sức khỏe bảo mật tài khoản">
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-sm font-semibold text-ink-primary">Sức khỏe bảo mật</h2>
            <StatusBadge tone={health.tone}>{health.statusLabel}</StatusBadge>
          </div>

          <ProgressBar
            value={health.percent}
            size="sm"
            variant={
              health.tone === 'success'
                ? 'success'
                : health.tone === 'warning'
                  ? 'warning'
                  : 'destructive'
            }
            label="Mức hoàn thiện"
            valueText={`${health.passedCount}/${health.totalCount} tiêu chí`}
            valueClassName="tabular-nums"
          />

          <ul className="space-y-2.5 text-xs">
            {health.checks.map((item) => (
              <li key={item.id} className="flex items-start justify-between gap-2">
                <span className="flex items-center gap-2 text-ink-body min-w-0">
                  {item.passed ? (
                    <CheckCircle2
                      className="size-4 shrink-0 text-tone-success-icon"
                      aria-hidden="true"
                    />
                  ) : (
                    <AlertTriangle
                      className="size-4 shrink-0 text-tone-warning-icon"
                      aria-hidden="true"
                    />
                  )}
                  <span className="truncate">{item.label}</span>
                </span>
                <span
                  className={cn(
                    'shrink-0 font-medium tabular-nums text-right',
                    item.passed ? 'text-ink-primary' : 'text-tone-warning-text',
                  )}
                >
                  {item.detail}
                </span>
              </li>
            ))}
          </ul>

          {user.mustChangePassword && (
            <button
              type="button"
              onClick={onJumpToSecurity}
              className="w-full rounded-xl border border-tone-warning-border bg-tone-warning-bg px-3 py-2 text-xs font-semibold text-tone-warning-strong hover:opacity-90 transition-opacity cursor-pointer"
            >
              Chuyển tới Đổi mật khẩu ngay →
            </button>
          )}
        </section>

        {/* Block 2: Assigned System Roles */}
        <section className="p-5 space-y-3" aria-label="Vai trò hệ thống">
          <h2 className="text-sm font-semibold text-ink-primary">Vai trò được cấp</h2>

          <div className="space-y-2">
            {user.roles.map((roleName) => {
              const meta = SYSTEM_ROLES.find(
                (r) => r.roleName.toLowerCase() === roleName.toLowerCase(),
              );
              return (
                <div
                  key={roleName}
                  className="rounded-xl border border-line-subtle bg-surface-subtle/60 p-3 space-y-1"
                >
                  <div className="text-xs font-semibold text-ink-primary">
                    {meta?.title ?? roleName}
                  </div>
                  {meta?.description && (
                    <p className="text-[11px] leading-relaxed text-ink-muted">
                      {meta.description}
                    </p>
                  )}
                </div>
              );
            })}
          </div>
        </section>

        {/* Block 3: Role-Aware Thesis Context (Student / Lecturer) */}
        <ProfileThesisContextSection user={user} />
      </Card>
    </aside>
  );
}
