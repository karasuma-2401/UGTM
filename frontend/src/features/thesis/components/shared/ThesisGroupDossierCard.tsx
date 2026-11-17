import type { ReactNode } from 'react';
import { RefreshCw, Users, GraduationCap, CheckCircle2 } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Avatar } from '@/components/ui/avatar';
import { Skeleton } from '@/components/ui/skeleton';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';

export interface DossierMetaItem {
  label: string;
  value: ReactNode;
  accent?: boolean;
}

interface ThesisGroupDossierCardProps {
  title: string;
  groupDetail?: StudentGroupDetailDto | null;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
  topBanner?: ReactNode;
  metaItems?: DossierMetaItem[];
  rulesTitle?: string;
  rules?: string[];
}

export function ThesisGroupDossierCard({
  title,
  groupDetail,
  isLoading = false,
  isError = false,
  onRetry,
  topBanner,
  metaItems = [],
  rulesTitle = 'Lưu ý thực hiện',
  rules = [],
}: ThesisGroupDossierCardProps) {
  return (
    <Card className="bg-surface-card border-line/80 shadow-xs rounded-2xl overflow-hidden">
      <div className="px-5 py-3.5 border-b border-line-subtle flex items-center justify-between gap-2">
        <h3 className="text-sm font-bold text-ink-primary">{title}</h3>
        {groupDetail?.groupCode && (
          <span className="inline-flex items-center px-2 py-0.5 rounded-md font-mono text-xs font-medium bg-surface-muted text-ink-secondary">
            {groupDetail.groupCode}
          </span>
        )}
      </div>

      <div className="divide-y divide-line-subtle">
        {topBanner && <div className="px-5 py-3.5">{topBanner}</div>}

        {isLoading ? (
          <div className="p-5 space-y-4">
            <div className="space-y-2">
              <Skeleton className="h-3.5 w-24" />
              <Skeleton className="h-5 w-full" />
              <Skeleton className="h-4 w-3/4" />
            </div>
            <div className="pt-3 border-t border-line-subtle space-y-2">
              <Skeleton className="h-3.5 w-28" />
              <Skeleton className="h-10 w-full rounded-xl" />
            </div>
            <div className="pt-3 border-t border-line-subtle space-y-2">
              <Skeleton className="h-3.5 w-20" />
              <Skeleton className="h-4 w-40" />
            </div>
          </div>
        ) : isError ? (
          <div className="p-5">
            <InlineAlert
              tone="warning"
              title="Chưa tải được hồ sơ nhóm"
              action={
                onRetry ? (
                  <Button type="button" size="sm" variant="outline" onClick={onRetry}>
                    <RefreshCw className="size-3.5 mr-1.5" />
                    Thử lại
                  </Button>
                ) : undefined
              }
            >
              Không thể đồng bộ thông tin thành viên và đề tài lúc này.
            </InlineAlert>
          </div>
        ) : groupDetail ? (
          <>
            <div className="px-5 py-4 space-y-1">
              <div className="flex flex-wrap items-center gap-1.5 text-xs text-ink-muted">
                <span className="font-mono font-semibold text-ink-secondary">
                  {groupDetail.topicCode || 'Đề tài KLTN'}
                </span>
                {groupDetail.batchName && (
                  <>
                    <span className="text-ink-faint">·</span>
                    <span>{groupDetail.batchName}</span>
                  </>
                )}
              </div>
              <p className="text-sm font-semibold text-ink-primary leading-snug">
                {groupDetail.topicTitleVi}
              </p>
              {groupDetail.topicTitleEn && (
                <p className="text-xs text-ink-muted leading-relaxed">
                  {groupDetail.topicTitleEn}
                </p>
              )}
            </div>

            <div className="px-5 py-3.5 space-y-2.5">
              <div className="flex items-center justify-between text-xs">
                <span className="inline-flex items-center gap-1.5 font-semibold text-ink-body">
                  <Users className="size-3.5 text-ink-subtle" aria-hidden="true" />
                  Thành viên nhóm
                </span>
                <span className="tabular-nums text-ink-muted">
                  {groupDetail.members.length} sinh viên
                </span>
              </div>
              {groupDetail.members.length > 0 ? (
                <ul className="space-y-2">
                  {groupDetail.members.map((member) => (
                    <li
                      key={member.studentId}
                      className="flex items-center justify-between gap-2.5 text-sm"
                    >
                      <div className="flex min-w-0 items-center gap-2.5">
                        <Avatar
                          name={member.fullName || 'Sinh viên'}
                          className="size-6 shrink-0 text-[10px]"
                        />
                        <span className="truncate font-medium text-ink-heading">
                          {member.fullName}
                        </span>
                      </div>
                      <span className="shrink-0 font-mono text-xs text-ink-muted tabular-nums">
                        {member.userCode}
                      </span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-xs text-ink-muted">Chưa có danh sách thành viên.</p>
              )}
            </div>

            <div className="px-5 py-3.5 space-y-2">
              <p className="inline-flex items-center gap-1.5 text-xs font-semibold text-ink-body">
                <GraduationCap className="size-3.5 text-ink-subtle" aria-hidden="true" />
                Giảng viên hướng dẫn
              </p>
              <div className="flex items-center gap-2.5">
                {groupDetail.advisor?.fullName && (
                  <Avatar
                    name={groupDetail.advisor.fullName}
                    className="size-6 shrink-0 text-[10px]"
                  />
                )}
                <div className="min-w-0">
                  <p className="text-sm font-medium text-ink-primary truncate">
                    {groupDetail.advisor?.fullName || 'Chưa phân công GVHD'}
                  </p>
                  {groupDetail.advisor?.email && (
                    <p className="text-xs text-ink-muted truncate">{groupDetail.advisor.email}</p>
                  )}
                </div>
              </div>
            </div>
          </>
        ) : (
          <p className="px-5 py-4 text-xs text-ink-muted">Đang đồng bộ thông tin hồ sơ nhóm…</p>
        )}

        {metaItems.length > 0 && (
          <div className="px-5 py-3.5 space-y-2.5">
            {metaItems.map((item) => (
              <div key={item.label} className="space-y-0.5">
                <p className="text-xs text-ink-muted">{item.label}</p>
                <div
                  className={
                    item.accent
                      ? 'text-sm font-semibold text-brand-ink'
                      : 'text-xs text-ink-secondary leading-relaxed'
                  }
                >
                  {item.value}
                </div>
              </div>
            ))}
          </div>
        )}

        {rules.length > 0 && (
          <div className="px-5 py-3.5 space-y-2 bg-surface-subtle/40">
            <p className="text-xs font-semibold text-ink-secondary">{rulesTitle}</p>
            <ul className="space-y-1.5">
              {rules.map((rule) => (
                <li
                  key={rule}
                  className="flex items-start gap-2 text-xs text-ink-body leading-relaxed"
                >
                  <CheckCircle2 className="size-3.5 text-tone-success-icon shrink-0 mt-0.5" />
                  <span>{rule}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </Card>
  );
}
