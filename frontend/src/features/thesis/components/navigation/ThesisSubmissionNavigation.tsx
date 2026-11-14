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
  completed: 'bg-slate-100/90 text-emerald-700 border-slate-200/80',
  pending: 'bg-blue-50/70 text-[#2054a5] border-blue-200/70',
  revision: 'bg-amber-50/80 text-amber-800 border-amber-200/80',
  rejected: 'bg-rose-50/80 text-rose-700 border-rose-200/80',
  locked: 'bg-slate-100/80 text-slate-500 border-slate-200/80',
  active: 'bg-slate-100/80 text-slate-600 border-slate-200/80',
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
                <CheckCircle2 className="size-3 text-emerald-600 shrink-0" aria-hidden="true" />
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
