import { useMemo } from 'react';
import { useQueries, useQuery } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { syllabusApi } from '../services/syllabusApi';
import { midtermApi } from '../services/midtermApi';
import { finalReportApi } from '../services/finalReportApi';
import { milestoneApi } from '../services/milestoneApi';
import type { ProgressMilestoneDto, ProgressReportHistoryDto } from '../types/milestone.types';
import { toReportStatus } from '../utils/milestoneFormatters';

export type JourneyGateState =
  | 'completed'
  | 'pending'
  | 'revision'
  | 'active'
  | 'locked'
  | 'rejected';

export interface JourneyGateSummary {
  id: 'milestones' | 'proposal' | 'midterm' | 'final-report';
  stepLabel: string;
  title: string;
  href: string;
  statusLabel: string;
  shortLabel: string;
  state: JourneyGateState;
  isLoading: boolean;
}

export interface MilestoneWithStatus {
  milestone: ProgressMilestoneDto;
  report: ProgressReportHistoryDto | null | undefined;
  status: ReturnType<typeof toReportStatus>;
  currentVersionNo: number;
  isLoading: boolean;
}

export function useThesisJourneySummary(groupId?: string | null) {
  const { user } = useAuth();
  const enabled = Boolean(user?.id && groupId);

  const milestonesQuery = useQuery({
    queryKey: ['student-milestones', user?.id, groupId],
    queryFn: ({ signal }) => milestoneApi.getMilestones(groupId!, signal),
    enabled,
  });

  const milestones = useMemo(() => milestonesQuery.data || [], [milestonesQuery.data]);

  const reportQueries = useQueries({
    queries: milestones.map((milestone) => ({
      queryKey: ['student-milestone-report', user?.id, milestone.id],
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        milestoneApi.getProgressReport(milestone.id, signal),
      enabled: Boolean(user?.id && milestone.id),
      retry: false,
    })),
  });

  const syllabusHistoryQuery = useQuery({
    queryKey: ['syllabus-history', user?.id, groupId],
    queryFn: ({ signal }) => syllabusApi.getSyllabusHistory(groupId!, signal),
    enabled,
    retry: false,
  });

  const midtermHistoryQuery = useQuery({
    queryKey: ['midterm-history', user?.id, groupId],
    queryFn: ({ signal }) => midtermApi.getMidtermHistory(groupId!, signal),
    enabled,
    retry: false,
  });

  const finalReportHistoryQuery = useQuery({
    queryKey: ['final-report-history', user?.id, groupId],
    queryFn: ({ signal }) => finalReportApi.getFinalReportHistory(groupId!, signal),
    enabled,
    retry: false,
  });

  const milestoneItems = useMemo<MilestoneWithStatus[]>(() => {
    return milestones.map((milestone, index) => {
      const rq = reportQueries[index];
      const report = rq?.data ?? null;
      const status = toReportStatus(report?.currentStatus);
      return {
        milestone,
        report,
        status,
        currentVersionNo: report?.currentVersionNo ?? 0,
        isLoading: Boolean(rq?.isLoading),
      };
    });
  }, [milestones, reportQueries]);

  const gates = useMemo<JourneyGateSummary[]>(() => {
    const totalMilestones = milestoneItems.length;
    const acceptedMilestones = milestoneItems.filter((item) => item.status === 'Accepted').length;
    const revisionMilestones = milestoneItems.filter((item) => item.status === 'NeedRevision').length;
    const pendingMilestones = milestoneItems.filter((item) => item.status === 'Submitted').length;
    const anyMilestoneReportLoading =
      milestonesQuery.isLoading || milestoneItems.some((item) => item.isLoading);

    let milestoneState: JourneyGateState = 'active';
    let milestoneLabel = 'Chưa có mốc';
    if (totalMilestones > 0) {
      milestoneLabel = `${acceptedMilestones}/${totalMilestones} mốc đạt`;
      if (acceptedMilestones === totalMilestones) {
        milestoneState = 'completed';
      } else if (revisionMilestones > 0) {
        milestoneState = 'revision';
      } else if (pendingMilestones > 0 && acceptedMilestones + pendingMilestones === totalMilestones) {
        milestoneState = 'pending';
      } else {
        milestoneState = 'active';
      }
    }

    const syllabusStatus = syllabusHistoryQuery.data?.currentStatus;
    let proposalState: JourneyGateState = 'active';
    let proposalLabel = 'Chưa nộp';
    if (syllabusStatus === 'Approved') {
      proposalState = 'completed';
      proposalLabel = 'Đã đạt';
    } else if (syllabusStatus === 'Submitted') {
      proposalState = 'pending';
      proposalLabel = 'Chờ thẩm định';
    } else if (syllabusStatus === 'NeedRevision') {
      proposalState = 'revision';
      proposalLabel = 'Cần chỉnh sửa';
    } else if (syllabusStatus === 'Rejected') {
      proposalState = 'rejected';
      proposalLabel = 'Không đạt';
    }

    const syllabusApproved = syllabusStatus === 'Approved';
    const midtermStatus = midtermHistoryQuery.data?.currentStatus;
    let midtermState: JourneyGateState = syllabusApproved ? 'active' : 'locked';
    let midtermLabel = syllabusApproved ? 'Sẵn sàng nộp' : 'Chờ Cổng 1';
    if (midtermStatus === 'Approved') {
      midtermState = 'completed';
      midtermLabel = 'Đã đạt';
    } else if (midtermStatus === 'Submitted') {
      midtermState = 'pending';
      midtermLabel = 'Chờ thẩm định';
    } else if (midtermStatus === 'NeedRevision') {
      midtermState = 'revision';
      midtermLabel = 'Cần chỉnh sửa';
    } else if (midtermStatus === 'Rejected') {
      midtermState = 'rejected';
      midtermLabel = 'Không đạt';
    }

    const midtermApproved = midtermStatus === 'Approved';
    const finalData = finalReportHistoryQuery.data;
    const finalStatus = finalData?.currentStatus;
    const finalVersionNo = finalData?.currentVersionNo;
    let finalState: JourneyGateState = midtermApproved ? 'active' : 'locked';
    let finalLabel = midtermApproved ? 'Sẵn sàng nộp' : 'Chờ Cổng 2';
    if (finalStatus === 'Approved') {
      finalState = 'completed';
      finalLabel = 'Chuyển phản biện';
    } else if (finalStatus === 'UnderEvaluation') {
      finalState = 'pending';
      finalLabel = 'GVHD đang chấm';
    } else if (finalStatus === 'Submitted') {
      finalState = 'pending';
      finalLabel = finalVersionNo ? `Đã nộp v${finalVersionNo}` : 'Chờ GVHD chấm';
    } else if (finalStatus === 'Rejected') {
      finalState = 'revision';
      finalLabel = 'Cần chỉnh sửa';
    }

    return [
      {
        id: 'milestones',
        stepLabel: 'Thường kỳ',
        title: 'Báo cáo tiến độ',
        href: '/thesis/milestones',
        statusLabel: milestoneLabel,
        shortLabel: totalMilestones > 0 ? `${acceptedMilestones}/${totalMilestones}` : '0 mốc',
        state: milestoneState,
        isLoading: anyMilestoneReportLoading,
      },
      {
        id: 'proposal',
        stepLabel: 'Cổng 1',
        title: 'Đề cương chi tiết',
        href: '/thesis/proposal',
        statusLabel: proposalLabel,
        shortLabel:
          syllabusStatus === 'Approved'
            ? 'Đạt'
            : syllabusStatus === 'Submitted'
              ? 'Chờ duyệt'
              : syllabusStatus === 'NeedRevision'
                ? 'Sửa lại'
                : syllabusStatus === 'Rejected'
                  ? 'Không đạt'
                  : 'Chưa nộp',
        state: proposalState,
        isLoading: syllabusHistoryQuery.isLoading,
      },
      {
        id: 'midterm',
        stepLabel: 'Cổng 2',
        title: 'Báo cáo giữa kỳ',
        href: '/thesis/midterm',
        statusLabel: midtermLabel,
        shortLabel:
          midtermStatus === 'Approved'
            ? 'Đạt'
            : midtermStatus === 'Submitted'
              ? 'Chờ duyệt'
              : midtermStatus === 'NeedRevision'
                ? 'Sửa lại'
                : midtermStatus === 'Rejected'
                  ? 'Không đạt'
                  : syllabusApproved
                    ? 'Sẵn sàng'
                    : 'Chờ Cổng 1',
        state: midtermState,
        isLoading: midtermHistoryQuery.isLoading || syllabusHistoryQuery.isLoading,
      },
      {
        id: 'final-report',
        stepLabel: 'Cổng 3',
        title: 'Báo cáo cuối kỳ',
        href: '/thesis/final-report',
        statusLabel: finalLabel,
        shortLabel:
          finalStatus === 'Approved'
            ? 'Phản biện'
            : finalStatus === 'UnderEvaluation'
              ? 'Đang chấm'
              : finalStatus === 'Submitted'
                ? finalVersionNo
                  ? `v${finalVersionNo}`
                  : 'Đã nộp'
                : finalStatus === 'Rejected'
                  ? 'Sửa lại'
                  : midtermApproved
                    ? 'Sẵn sàng'
                    : 'Chờ Cổng 2',
        state: finalState,
        isLoading: finalReportHistoryQuery.isLoading || midtermHistoryQuery.isLoading,
      },
    ];
  }, [
    milestoneItems,
    milestonesQuery.isLoading,
    syllabusHistoryQuery.data?.currentStatus,
    syllabusHistoryQuery.isLoading,
    midtermHistoryQuery.data?.currentStatus,
    midtermHistoryQuery.isLoading,
    finalReportHistoryQuery.data,
    finalReportHistoryQuery.isLoading,
  ]);

  return {
    gates,
    milestoneItems,
    milestonesQuery,
    syllabusStatus: syllabusHistoryQuery.data?.currentStatus ?? null,
    midtermStatus: midtermHistoryQuery.data?.currentStatus ?? null,
    finalStatus: finalReportHistoryQuery.data?.currentStatus ?? null,
  };
}
