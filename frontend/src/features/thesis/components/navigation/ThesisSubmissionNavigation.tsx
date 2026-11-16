import { useMemo } from 'react';
import { useLocation } from 'react-router-dom';
import { CheckCircle2 } from 'lucide-react';
import { NavigationTabs, type TabItem } from '@/components/ui/navigation-tabs';
import { cn } from '@/lib/utils';
import type { JourneyGateState, JourneyGateSummary } from '../../hooks/useThesisJourneySummary';

const BASE_TABS: Array<{
  id: JourneyGateSummary['id'];
  stepCode: string;
  label: string;
  href: string;
}> = [
  {
    id: 'milestones',
    stepCode: '01',
    label: 'Báo cáo tiến độ',
    href: '/thesis/milestones',
  },
  {
    id: 'proposal',
    stepCode: '02',
    label: 'Đề cương chi tiết',
    href: '/thesis/proposal',
  },
  {
    id: 'midterm',
    stepCode: '03',
    label: 'Báo cáo giữa kỳ',
    href: '/thesis/midterm',
  },
  {
    id: 'final-report',
    stepCode: '04',
    label: 'Báo cáo cuối kỳ',
    href: '/thesis/final-report',
  },
];

const pillToneClasses: Record<JourneyGateState, string> = {
  completed: 'bg-surface-muted/90 text-tone-success-text border-line/80',
  pending: 'bg-tone-info-bg text-brand-ink border-tone-info-border',
  revision: 'bg-tone-warning-bg text-tone-warning-strong border-tone-warning-border',
  rejected: 'bg-tone-danger-bg text-tone-danger-text border-tone-danger-border',
  locked: 'bg-surface-muted/80 text-ink-muted border-line-input/80',
  active: 'bg-surface-muted/80 text-ink-body border-line-input/80',
};

export function ThesisSubmissionNavigation({
  activeId,
  gates,
}: {
  activeId?: JourneyGateSummary['id'];
  gates?: JourneyGateSummary[];
} = {}) {
  const { pathname } = useLocation();

  const resolvedActiveId = useMemo(() => {
    if (activeId) return activeId;
    const matched = BASE_TABS.find((tab) => pathname.startsWith(tab.href));
    return matched?.id ?? 'milestones';
  }, [activeId, pathname]);

  const completedGatesCount = useMemo(() => {
    if (!gates?.length) return 0;
    return gates.filter((g) => g.state === 'completed').length;
  }, [gates]);

  const tabs = useMemo<TabItem[]>(() => {
    return BASE_TABS.map((tab) => {
      const gate = gates?.find((g) => g.id === tab.id);
      return {
        ...tab,
        label: `${tab.stepCode} · ${tab.label}`,
        badge:
          gate && !gate.isLoading && gate.shortLabel ? (
            <span
              className={cn(
                'hidden md:inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-semibold leading-none whitespace-nowrap',
                pillToneClasses[gate.state],
              )}
            >
              {gate.state === 'completed' && (
                <CheckCircle2 className="size-3 text-tone-success-icon shrink-0" aria-hidden="true" />
              )}
              <span>{gate.shortLabel}</span>
            </span>
          ) : undefined,
      };
    });
  }, [gates]);

  return (
    <div className="overflow-x-auto sm:overflow-visible">
      <NavigationTabs
        tabs={tabs}
        activeId={resolvedActiveId}
        continuityKey="thesis-submission-tabs"
        ariaLabel={`Điều hướng báo cáo và tiến độ (${completedGatesCount}/4 hoàn thành)`}
        variant="underline"
        className="min-w-max sm:min-w-0 [&_a]:whitespace-nowrap [&_a]:shrink-0 [&_span]:overflow-visible [&_span]:text-clip"
      />
    </div>
  );
}
