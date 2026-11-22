import { useEffect, useMemo, useState } from 'react';
import {
  CheckCircle2,
  Clock3,
  Eye,
  FilePenLine,
  Layers,
  ListFilter,
  Lock,
  Save,
  Search,
  Send,
  ShieldCheck,
  SquarePen,
  X,
  XCircle,
} from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { NavigationTabs } from '@/components/ui/navigation-tabs';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { Input } from '@/components/ui/input';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { Select } from '@/components/ui/select';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import {
  canSelectAcademicBatch,
  getActiveOpenBatch,
  getPreferredDefaultBatch,
} from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { notify } from '@/shared/notifications';
import { advisingApi } from '@/features/topics/services/advisingApi';
import { PdfPreviewDialog } from '../components/shared/PdfPreviewDialog';
import {
  GradingMobileDock,
  GradingProgressAside,
} from '../components/grading/GradingProgressAside';
import { GradingBackLink } from '../components/grading/GradingBackLink';
import { GradingRubricBoard } from '../components/grading/GradingRubricBoard';
import { GradingStatusCell, GradingStudentsCell } from '../components/grading/GradingTableCells';
import {
  computeStudentProgressList,
  countFilledGroupCriteria,
  type GradingStudentInfo,
  type GradingStudentScoresMap,
} from '../components/grading/gradingShared';
import { finalReportApi } from '../services/finalReportApi';
import { syllabusApi } from '../services/syllabusApi';
import {
  lecturerGradingApi,
  type GradingCriterionDto,
} from '../services/lecturerGradingApi';
import {
  buildRubricSections,
  buildStudentScoresPayload,
  getStudentInitials,
  hydrateGroupScoresFromDto,
} from '../utils/gradingRubricHelper';
import { getThesisErrorMessage } from '../utils/thesisErrors';

type Role = 'advisor' | 'reviewer';
type Scores = GradingStudentScoresMap;
type GroupStatus = 'all' | 'empty' | 'progress' | 'complete';
type GroupSort =
  | 'default'
  | 'title-asc'
  | 'title-desc'
  | 'student-asc'
  | 'student-desc'
  | 'progress-asc'
  | 'progress-desc';
type SortColumn = 'topic' | 'students' | 'status';

interface GradingGroupItem {
  id: string;
  groupId: string;
  title: string;
  batchName: string;
  students: GradingStudentInfo[];
  finalReportId?: string | null;
  reportVersionNo?: number | null;
  submissionFileId?: string | null;
  submissionFileName?: string | null;
  submissionFileSize?: number;
  canDownloadReport?: boolean;
  assignmentId?: string | null;
  assignmentVersion?: number | null;
  evaluationVersion?: number | null;
  evaluationStatus?: 'Draft' | 'Submitted' | string | null;
  isLocked?: boolean;
  defenseApproval?: 'Approved' | 'Rejected' | null;
}

function serializeEvaluationSnapshot(
  scores: Scores | undefined,
  comment: string,
  decision: string,
): string {
  const normalizedStudents: Record<string, Record<string, string>> = {};
  if (scores) {
    for (const studentId of Object.keys(scores).sort()) {
      const studentEntries: Record<string, string> = {};
      const cellMap = scores[studentId] ?? {};
      for (const criterionId of Object.keys(cellMap).sort()) {
        const trimmed = (cellMap[criterionId] ?? '').trim();
        if (trimmed !== '') {
          studentEntries[criterionId] = trimmed;
        }
      }
      if (Object.keys(studentEntries).length > 0) {
        normalizedStudents[studentId] = studentEntries;
      }
    }
  }
  return JSON.stringify({
    scores: normalizedStudents,
    comment: comment.trim(),
    decision,
  });
}

export function LecturerGradingPage() {
  const { hasRole } = useAuth();
  const canSelectBatch = canSelectAcademicBatch(hasRole);
  const [role, setRole] = useState<Role>('advisor');
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<GroupStatus>('all');
  const [sortBy, setSortBy] = useState<GroupSort>('default');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [activeGroupId, setActiveGroupId] = useState<string | null>(null);

  const [apiCriteria, setApiCriteria] = useState<GradingCriterionDto[] | null>(null);
  const [apiGroupsByRole, setApiGroupsByRole] = useState<{
    advisor: GradingGroupItem[];
    reviewer: GradingGroupItem[];
  }>({ advisor: [], reviewer: [] });
  const [roleLoadErrors, setRoleLoadErrors] = useState<{
    advisor: string | null;
    reviewer: string | null;
  }>({ advisor: null, reviewer: null });
  const [loadedRequestKey, setLoadedRequestKey] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

  const [scoresByGroup, setScoresByGroup] = useState<Record<string, Scores>>({});
  const [comments, setComments] = useState<Record<string, string>>({});
  const [advisorDecisionByGroup, setAdvisorDecisionByGroup] = useState<
    Record<string, 'Approve' | 'Reject'>
  >({});
  const [advisorRejectionReasonByGroup, setAdvisorRejectionReasonByGroup] = useState<
    Record<string, string>
  >({});
  const [reviewerDecisionByGroup, setReviewerDecisionByGroup] = useState<
    Record<string, 'Approved' | 'Rejected'>
  >({});
  const [savedSnapshotByGroup, setSavedSnapshotByGroup] = useState<Record<string, string>>({});
  const [groupMetaOverrides, setGroupMetaOverrides] = useState<
    Record<
      string,
      {
        evaluationVersion?: number | null;
        evaluationStatus?: string | null;
        isLocked?: boolean;
      }
    >
  >({});

  const [isSavingDraft, setIsSavingDraft] = useState(false);
  const [isSubmittingScore, setIsSubmittingScore] = useState(false);
  const [isDecidingTransfer, setIsDecidingTransfer] = useState(false);
  const [confirmSubmitOpen, setConfirmSubmitOpen] = useState(false);
  const [confirmTransferOpen, setConfirmTransferOpen] = useState(false);
  const [pendingLeaveTarget, setPendingLeaveTarget] = useState<'list' | string | null>(null);
  const [isLoadingPdf, setIsLoadingPdf] = useState(false);
  const [pdfPreviewState, setPdfPreviewState] = useState<{
    open: boolean;
    url: string;
    fileName?: string;
    fileSize?: number;
  }>({ open: false, url: '' });

  const batchesQuery = useAcademicBatches();
  const batches = useMemo(() => batchesQuery.data ?? [], [batchesQuery.data]);
  const [explicitBatchId, setExplicitBatchId] = useState<string | null>(null);
  const defaultBatch = useMemo(() => {
    if (!canSelectBatch) {
      return getActiveOpenBatch(batches);
    }
    return getPreferredDefaultBatch(batches);
  }, [batches, canSelectBatch]);
  const activeBatch = useMemo(() => {
    if (!canSelectBatch) {
      return defaultBatch;
    }
    if (explicitBatchId) {
      const matched = batches.find((batch) => batch.id === explicitBatchId);
      if (matched) return matched;
    }
    return defaultBatch;
  }, [batches, canSelectBatch, explicitBatchId, defaultBatch]);
  const batchOptions = useMemo(
    () =>
      batches.map((batch) => ({
        value: batch.id,
        label: `${batch.batchName} (${batch.academicYear} - HK${batch.semester})`,
      })),
    [batches],
  );

  function handleBatchChange(nextBatchId: string) {
    setExplicitBatchId(nextBatchId);
    setSelectedRowId(null);
    setActiveGroupId(null);
    setPageNumber(1);
    setApiGroupsByRole({ advisor: [], reviewer: [] });
    setRoleLoadErrors({ advisor: null, reviewer: null });
  }

  const sections = useMemo(() => buildRubricSections(apiCriteria), [apiCriteria]);
  const criteria = useMemo(() => sections.flatMap((section) => section.criteria), [sections]);

  const currentRequestKey = activeBatch?.id ? `${activeBatch.id}:${reloadNonce}` : null;

  useEffect(() => {
    if (!activeBatch?.id || !currentRequestKey) return;
    const controller = new AbortController();
    const requestKey = currentRequestKey;

    async function loadGradingWorkspace() {
      let advisorError: string | null = null;
      let reviewerError: string | null = null;

      try {
        const rubric = await lecturerGradingApi
          .getGradingRubric(activeBatch!.id, controller.signal)
          .catch(() => null);
        const resolvedCriteria = rubric?.criteria?.length ? rubric.criteria : null;
        if (resolvedCriteria) {
          setApiCriteria(resolvedCriteria);
        }
        const rubricViewCriteria = buildRubricSections(resolvedCriteria).flatMap((s) => s.criteria);

        const [advisingResult, reviewerTasks] = await Promise.all([
          advisingApi
            .getGroups(
              {
                batchId: activeBatch!.id,
                keyword: '',
                scope: 'all',
                status: '',
                confirmation: '',
                pageNumber: 1,
                pageSize: 100,
                sortBy: 'createdAt',
                sortOrder: 'desc',
              },
              controller.signal,
            )
            .catch((err) => {
              if (!controller.signal.aborted) {
                advisorError = getThesisErrorMessage(
                  err,
                  'Không thể tải danh sách nhóm hướng dẫn.',
                );
              }
              return null;
            }),
          lecturerGradingApi
            .getMyReviewerTasks(activeBatch!.id, controller.signal)
            .catch((err) => {
              if (!controller.signal.aborted) {
                reviewerError = getThesisErrorMessage(
                  err,
                  'Không thể tải danh sách nhóm phản biện.',
                );
              }
              return null;
            }),
        ]);

        const nextScores: Record<string, Scores> = {};
        const nextComments: Record<string, string> = {};
        const nextReviewerDecision: Record<string, 'Approved' | 'Rejected'> = {};
        const nextSnapshots: Record<string, string> = {};

        const advisingItems = advisingResult?.page.items ?? [];
        let loadedAdvisorGroups: GradingGroupItem[] = [];
        if (advisingItems.length > 0) {
          loadedAdvisorGroups = await Promise.all(
            advisingItems.map(async (item) => {
              const [report, evaluation] = await Promise.all([
                finalReportApi.getFinalReportHistory(item.groupId, controller.signal).catch(() => null),
                lecturerGradingApi
                  .getAdvisorEvaluation(item.groupId, controller.signal)
                  .catch(() => null),
              ]);

              const students: GradingStudentInfo[] = (item.students ?? []).map((student) => {
                const fullName = student.fullName || student.userCode || 'Sinh viên';
                return {
                  id: student.studentId,
                  name: fullName,
                  code: student.userCode || '—',
                  initials: getStudentInitials(fullName),
                };
              });

              const studentIds = students.map((s) => s.id);
              if (evaluation) {
                nextScores[item.groupId] = hydrateGroupScoresFromDto(
                  studentIds,
                  rubricViewCriteria,
                  evaluation.studentScores,
                  evaluation.details,
                );
                if (evaluation.comment) {
                  nextComments[item.groupId] = evaluation.comment;
                }
              }
              nextSnapshots[item.groupId] = serializeEvaluationSnapshot(
                nextScores[item.groupId],
                nextComments[item.groupId] ?? '',
                'Approve',
              );

              const latestReportFile =
                report?.versions && report.versions.length > 0
                  ? report.versions[report.versions.length - 1]?.file
                  : undefined;

              return {
                id: item.groupId,
                groupId: item.groupId,
                title: item.titleVi,
                batchName: item.batchName || activeBatch!.batchName,
                students,
                finalReportId: evaluation?.finalReportId ?? report?.finalReportId ?? null,
                reportVersionNo: report?.currentVersionNo ?? 1,
                submissionFileId: latestReportFile?.id ?? null,
                submissionFileName: latestReportFile?.fileName ?? null,
                submissionFileSize: latestReportFile?.fileSize,
                canDownloadReport: Boolean(latestReportFile?.id),
                evaluationVersion: evaluation?.evaluationVersion ?? null,
                evaluationStatus: evaluation?.status ?? null,
                isLocked: evaluation?.isLocked ?? false,
              };
            }),
          );
        }

        const safeReviewerTasks = reviewerTasks ?? [];
        let loadedReviewerGroups: GradingGroupItem[] = [];
        if (safeReviewerTasks.length > 0) {
          loadedReviewerGroups = await Promise.all(
            safeReviewerTasks.map(async (task) => {
              const hasInlineStudents = Boolean(task.students && task.students.length > 0);
              const [groupDetail, evaluation] = await Promise.all([
                hasInlineStudents
                  ? Promise.resolve(null)
                  : syllabusApi
                      .getGroupDetail(
                        task.groupId,
                        { batchName: activeBatch!.batchName },
                        controller.signal,
                      )
                      .catch(() => null),
                lecturerGradingApi
                  .getReviewerEvaluation(task.id, controller.signal)
                  .catch(() => null),
              ]);

              const rawStudents = hasInlineStudents
                ? task.students!
                : (groupDetail?.members ?? []);

              const students: GradingStudentInfo[] = rawStudents.map((member) => {
                const fullName = member.fullName || member.userCode || 'Sinh viên';
                return {
                  id: member.studentId,
                  name: fullName,
                  code: member.userCode || '—',
                  initials: getStudentInitials(fullName),
                };
              });

              const studentIds = students.map((s) => s.id);
              if (evaluation) {
                nextScores[task.id] = hydrateGroupScoresFromDto(
                  studentIds,
                  rubricViewCriteria,
                  evaluation.studentScores,
                  evaluation.details,
                );
                if (evaluation.comment) {
                  nextComments[task.id] = evaluation.comment;
                }
                if (evaluation.defenseApproval) {
                  nextReviewerDecision[task.id] = evaluation.defenseApproval;
                }
              }
              nextSnapshots[task.id] = serializeEvaluationSnapshot(
                nextScores[task.id],
                nextComments[task.id] ?? '',
                nextReviewerDecision[task.id] ?? 'Approved',
              );

              return {
                id: task.id,
                groupId: task.groupId,
                title: task.topicTitleVi,
                batchName: activeBatch!.batchName,
                students,
                assignmentId: task.id,
                assignmentVersion: task.assignmentVersion,
                finalReportId: task.finalReportId,
                reportVersionNo: task.reportVersionNo ?? 1,
                submissionFileId: task.submissionFileId,
                submissionFileName: task.submissionFileId ? 'Báo cáo cuối kỳ.pdf' : null,
                canDownloadReport: task.canDownloadReport,
                evaluationVersion: evaluation?.evaluationVersion ?? null,
                evaluationStatus: evaluation?.status ?? null,
                isLocked: evaluation?.isLocked ?? false,
                defenseApproval: evaluation?.defenseApproval ?? null,
              };
            }),
          );
        }

        if (!controller.signal.aborted) {
          setApiGroupsByRole({
            advisor: loadedAdvisorGroups,
            reviewer: loadedReviewerGroups,
          });
          setRoleLoadErrors({ advisor: advisorError, reviewer: reviewerError });
          if (Object.keys(nextScores).length > 0) {
            setScoresByGroup((prev) => ({ ...prev, ...nextScores }));
          }
          if (Object.keys(nextComments).length > 0) {
            setComments((prev) => ({ ...prev, ...nextComments }));
          }
          if (Object.keys(nextReviewerDecision).length > 0) {
            setReviewerDecisionByGroup((prev) => ({ ...prev, ...nextReviewerDecision }));
          }
          setSavedSnapshotByGroup((prev) => ({ ...prev, ...nextSnapshots }));
        }
      } catch (err) {
        if (!controller.signal.aborted) {
          const message = getThesisErrorMessage(err, 'Không thể tải dữ liệu chấm điểm khóa luận.');
          setRoleLoadErrors({ advisor: message, reviewer: message });
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoadedRequestKey(requestKey);
        }
      }
    }

    void loadGradingWorkspace();
    return () => controller.abort();
  }, [activeBatch, currentRequestKey]);

  const roleGroups = useMemo<Record<Role, GradingGroupItem[]>>(() => {
    if (!activeBatch?.id) {
      return { advisor: [], reviewer: [] };
    }
    const applyOverrides = (items: GradingGroupItem[]) =>
      items.map((item) => {
        const override = groupMetaOverrides[item.id];
        return override ? { ...item, ...override } : item;
      });

    return {
      advisor: applyOverrides(apiGroupsByRole.advisor),
      reviewer: applyOverrides(apiGroupsByRole.reviewer),
    };
  }, [activeBatch?.id, apiGroupsByRole, groupMetaOverrides]);

  const currentRoleGroups = roleGroups[role];
  const group = currentRoleGroups.find((item) => item.id === activeGroupId) ?? null;
  const students = group?.students ?? [];
  const scores = group ? (scoresByGroup[group.id] ?? {}) : {};
  const isLocked = Boolean(group?.isLocked);
  const searchText = search.trim().toLocaleLowerCase('vi');
  const currentRoleError = batchesQuery.isError
    ? getThesisErrorMessage(batchesQuery.error, 'Không thể tải danh sách đợt khóa luận.')
    : activeBatch?.id
      ? roleLoadErrors[role]
      : null;
  const isLoading =
    batchesQuery.isLoading || Boolean(currentRequestKey && loadedRequestKey !== currentRequestKey);

  function handleRetryLoad() {
    if (batchesQuery.isError) {
      void batchesQuery.refetch();
    }
    setReloadNonce((prev) => prev + 1);
  }

  const groupProgress = (item: GradingGroupItem) =>
    countFilledGroupCriteria(item.students, criteria, scoresByGroup[item.id]);

  const statusOf = (item: GradingGroupItem): Exclude<GroupStatus, 'all'> => {
    const filled = groupProgress(item);
    if (filled === 0) return 'empty';
    if (filled === criteria.length * item.students.length) return 'complete';
    return 'progress';
  };

  function openGradingSheet(id: string) {
    setSelectedRowId(id);
    setActiveGroupId(id);
    window.scrollTo({ top: 0 });
  }

  const visibleGroups = currentRoleGroups
    .filter(
      (item) =>
        (statusFilter === 'all' || statusOf(item) === statusFilter) &&
        [item.title, ...item.students.flatMap((student) => [student.name, student.code])].some(
          (value) => value.toLocaleLowerCase('vi').includes(searchText),
        ),
    )
    .sort((a, b) => {
      switch (sortBy) {
        case 'title-asc':
          return a.title.localeCompare(b.title, 'vi');
        case 'title-desc':
          return b.title.localeCompare(a.title, 'vi');
        case 'student-asc':
          return (a.students[0]?.name ?? '').localeCompare(b.students[0]?.name ?? '', 'vi');
        case 'student-desc':
          return (b.students[0]?.name ?? '').localeCompare(a.students[0]?.name ?? '', 'vi');
        case 'progress-asc':
          return groupProgress(a) - groupProgress(b);
        case 'progress-desc':
          return groupProgress(b) - groupProgress(a);
        default:
          return 0;
      }
    });

  const groupCounts = currentRoleGroups.reduce(
    (counts, item) => {
      const filled = groupProgress(item);
      if (filled === 0) counts.empty += 1;
      else if (filled === criteria.length * item.students.length) counts.complete += 1;
      else counts.inProgress += 1;
      return counts;
    },
    { empty: 0, inProgress: 0, complete: 0 },
  );

  const currentPage = Math.min(pageNumber, Math.max(1, Math.ceil(visibleGroups.length / pageSize)));
  const pageGroups = visibleGroups.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const selectedGroupRow = visibleGroups.find((item) => item.id === selectedRowId) ?? null;
  const chooseStatus = (status: GroupStatus) => {
    setStatusFilter(status);
    setSelectedRowId(null);
    setPageNumber(1);
  };
  const activeSort: { field: SortColumn; direction: 'asc' | 'desc' } | undefined =
    sortBy === 'default'
      ? undefined
      : {
          field: sortBy.startsWith('title')
            ? 'topic'
            : sortBy.startsWith('student')
              ? 'students'
              : 'status',
          direction: sortBy.endsWith('asc') ? 'asc' : 'desc',
        };

  function sortColumn(field: SortColumn) {
    const prefix = field === 'topic' ? 'title' : field === 'students' ? 'student' : 'progress';
    setSortBy(
      `${prefix}-${activeSort?.field === field && activeSort.direction === 'asc' ? 'desc' : 'asc'}` as GroupSort,
    );
    setPageNumber(1);
  }

  const groupColumns: DataTableColumn<GradingGroupItem, SortColumn>[] = [
    {
      id: 'number',
      header: 'STT',
      widthClassName: 'w-[6%]',
      headerClassName: 'whitespace-nowrap',
      cell: (_item, index) => (
        <span className="tabular-nums text-ink-muted">
          {(currentPage - 1) * pageSize + index + 1}
        </span>
      ),
    },
    {
      id: 'topic',
      header: 'Đề tài',
      sortKey: 'topic',
      widthClassName: 'w-[31%]',
      cell: (item) => (
        <span className="block min-w-0">
          <span className="block font-semibold leading-relaxed text-ink-primary">{item.title}</span>
          <span className="mt-1 block text-xs text-ink-muted">{item.batchName}</span>
        </span>
      ),
    },
    {
      id: 'students',
      header: 'Sinh viên',
      sortKey: 'students',
      widthClassName: 'w-[31%]',
      cell: (item) => (
        <GradingStudentsCell
          students={item.students}
          ariaLabel={`Thành viên nhóm ${item.title}`}
        />
      ),
    },
    {
      id: 'status',
      header: 'Trạng thái',
      sortKey: 'status',
      widthClassName: 'w-[17%]',
      headerClassName: 'whitespace-nowrap',
      cell: (item) => (
        <GradingStatusCell
          status={statusOf(item)}
          filledCount={groupProgress(item)}
          totalCount={criteria.length * item.students.length}
          lockedLabel={item.isLocked ? 'Đã chốt phiếu' : null}
        />
      ),
    },
    {
      id: 'action',
      header: 'Thao tác',
      widthClassName: 'w-[15%]',
      headerClassName: 'whitespace-nowrap',
      align: 'right',
      cell: (item) => (
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => openGradingSheet(item.id)}
          className="gap-1.5 rounded-md border-line bg-surface-card text-ink-secondary shadow-2xs hover:border-tone-info-border hover:bg-tone-info-bg hover:text-tone-info-text"
        >
          <SquarePen className="size-3.5" aria-hidden="true" />
          {item.isLocked ? 'Xem phiếu' : 'Chấm điểm'}
        </Button>
      ),
    },
  ];

  const progress = computeStudentProgressList(students, criteria, scores);
  const allStudentsComplete =
    students.length > 0 && progress.every((item) => item.count === criteria.length);
  const anyStudentBelowFive = progress.some((item) => item.total < 5);

  const advisorDecision = group ? (advisorDecisionByGroup[group.id] ?? 'Approve') : 'Approve';
  const advisorRejectionReason = group ? (advisorRejectionReasonByGroup[group.id] ?? '') : '';
  const reviewerDecision = group ? (reviewerDecisionByGroup[group.id] ?? 'Approved') : 'Approved';
  const activeDecisionValue = role === 'advisor' ? advisorDecision : reviewerDecision;
  const currentGroupSnapshot = group
    ? serializeEvaluationSnapshot(scoresByGroup[group.id], comments[group.id] ?? '', activeDecisionValue)
    : '';
  const baselineSnapshot = group
    ? (savedSnapshotByGroup[group.id] ??
      serializeEvaluationSnapshot(undefined, '', role === 'advisor' ? 'Approve' : 'Approved'))
    : '';
  const hasUnsavedChanges = Boolean(group && !isLocked && currentGroupSnapshot !== baselineSnapshot);

  function requestNavigation(target: 'list' | string) {
    if (hasUnsavedChanges) {
      setPendingLeaveTarget(target);
      return;
    }
    if (target === 'list') {
      setActiveGroupId(null);
    } else {
      openGradingSheet(target);
    }
  }

  function applyConfirmedLeave() {
    if (!pendingLeaveTarget) return;
    const target = pendingLeaveTarget;
    setPendingLeaveTarget(null);
    if (target === 'list') {
      setActiveGroupId(null);
    } else {
      openGradingSheet(target);
    }
  }

  async function handlePreviewReportPdf() {
    if (!group?.submissionFileId) {
      notify.info('Nhóm này chưa có tệp báo cáo cuối kỳ để xem trước.');
      return;
    }
    try {
      setIsLoadingPdf(true);
      const downloadInfo = await finalReportApi.getDownloadUrl(group.submissionFileId, {
        inline: true,
      });
      setPdfPreviewState({
        open: true,
        url: downloadInfo.downloadUrl,
        fileName: group.submissionFileName || 'Báo cáo cuối kỳ.pdf',
        fileSize: group.submissionFileSize,
      });
    } catch (error) {
      notify.error(getThesisErrorMessage(error, 'Không thể tải liên kết xem trước báo cáo PDF.'));
    } finally {
      setIsLoadingPdf(false);
    }
  }

  function updateScore(studentId: string, criterionId: string, value: string) {
    if (!group || isLocked) return;
    setScoresByGroup((previous) => ({
      ...previous,
      [group.id]: {
        ...previous[group.id],
        [studentId]: {
          ...previous[group.id]?.[studentId],
          [criterionId]: value,
        },
      },
    }));
  }

  async function handleSaveEvaluation(action: 'SaveDraft' | 'Submit'): Promise<boolean> {
    if (!group || isLocked) return false;

    const studentScoresPayload = buildStudentScoresPayload(
      students.map((s) => s.id),
      criteria,
      scoresByGroup[group.id],
    );

    if (action === 'Submit' && !allStudentsComplete) {
      notify.error('Vui lòng nhập đầy đủ và hợp lệ tất cả tiêu chí cho từng sinh viên trước khi chốt điểm.');
      return false;
    }

    const commentText = (comments[group.id] ?? '').trim();
    if (role === 'reviewer' && action === 'Submit' && !commentText) {
      notify.error('Vui lòng nhập nhận xét chuyên môn của Giảng viên phản biện trước khi chốt phiếu.');
      return false;
    }

    try {
      if (action === 'SaveDraft') setIsSavingDraft(true);
      else setIsSubmittingScore(true);

      if (role === 'advisor') {
        if (!group.finalReportId) {
          notify.warning('Nhóm sinh viên chưa nộp báo cáo cuối kỳ nên chưa thể lưu phiếu lên máy chủ.');
          return false;
        }
        const saved = await lecturerGradingApi.saveAdvisorEvaluation({
          finalReportId: group.finalReportId,
          expectedReportVersionNo: group.reportVersionNo ?? 1,
          expectedEvaluationVersion: group.evaluationVersion ?? null,
          comment: commentText || null,
          studentScores: studentScoresPayload,
          action,
        });
        setGroupMetaOverrides((prev) => ({
          ...prev,
          [group.id]: {
            evaluationVersion: saved.evaluationVersion,
            evaluationStatus: saved.status,
            isLocked: saved.isLocked,
          },
        }));
        notify.success(
          action === 'SaveDraft'
            ? 'Đã lưu nháp phiếu chấm điểm GVHD.'
            : 'Đã lưu điểm chính thức phiếu chấm GVHD.',
        );
      } else {
        if (!group.assignmentId) {
          notify.warning('Không tìm thấy mã phân công phản biện hợp lệ.');
          return false;
        }
        const saved = await lecturerGradingApi.saveReviewerEvaluation({
          assignmentId: group.assignmentId,
          action,
          expectedAssignmentVersion: group.assignmentVersion ?? 1,
          expectedEvaluationVersion: group.evaluationVersion ?? null,
          comment: commentText || null,
          defenseApproval: reviewerDecision,
          studentScores: studentScoresPayload,
        });
        setGroupMetaOverrides((prev) => ({
          ...prev,
          [group.id]: {
            evaluationVersion: saved.evaluationVersion,
            evaluationStatus: saved.status,
            isLocked: saved.isLocked,
          },
        }));
        notify.success(
          action === 'SaveDraft'
            ? 'Đã lưu nháp phiếu đánh giá phản biện.'
            : 'Đã chốt phiếu đánh giá phản biện thành công.',
        );
      }
      setSavedSnapshotByGroup((prev) => ({
        ...prev,
        [group.id]: currentGroupSnapshot,
      }));
      setConfirmSubmitOpen(false);
      return true;
    } catch (error) {
      notify.error(getThesisErrorMessage(error, 'Không thể lưu phiếu chấm điểm.'));
      return false;
    } finally {
      setIsSavingDraft(false);
      setIsSubmittingScore(false);
    }
  }

  async function handleConfirmAdvisorTransferDecision() {
    if (!group || isLocked) return;
    const allowsReviewerAssignment = advisorDecision === 'Approve';
    const trimmedReason = advisorRejectionReason.trim();

    if (!allowsReviewerAssignment && !trimmedReason) {
      notify.error('Vui lòng nhập lý do chưa đồng ý chuyển phản biện để sinh viên chỉnh sửa.');
      return;
    }

    if (allowsReviewerAssignment && !allStudentsComplete) {
      notify.error('Cần chấm đủ điểm tất cả tiêu chí cho từng sinh viên trước khi duyệt chuyển phản biện.');
      return;
    }

    if (!group.finalReportId) {
      notify.warning('Nhóm sinh viên chưa nộp báo cáo cuối kỳ nên chưa thể duyệt chuyển phản biện.');
      return;
    }

    try {
      setIsDecidingTransfer(true);
      const studentScoresPayload = buildStudentScoresPayload(
        students.map((s) => s.id),
        criteria,
        scoresByGroup[group.id],
      );
      let currentEvalVersion = group.evaluationVersion ?? null;

      if (allowsReviewerAssignment || studentScoresPayload.length > 0) {
        const savedEval = await lecturerGradingApi.saveAdvisorEvaluation({
          finalReportId: group.finalReportId,
          expectedReportVersionNo: group.reportVersionNo ?? 1,
          expectedEvaluationVersion: currentEvalVersion,
          comment: (comments[group.id] ?? '').trim() || null,
          studentScores: studentScoresPayload,
          action: allowsReviewerAssignment ? 'Submit' : 'SaveDraft',
        });
        currentEvalVersion = savedEval.evaluationVersion;
      }

      const decided = await lecturerGradingApi.decideAdvisorEvaluation(group.finalReportId, {
        expectedReportVersionNo: group.reportVersionNo ?? 1,
        expectedEvaluationVersion: currentEvalVersion,
        allowsReviewerAssignment,
        rejectionReason: allowsReviewerAssignment ? null : trimmedReason,
      });

      setGroupMetaOverrides((prev) => ({
        ...prev,
        [group.id]: {
          evaluationVersion: decided.evaluationVersion,
          evaluationStatus: decided.status,
          isLocked: decided.isLocked,
        },
      }));
      setSavedSnapshotByGroup((prev) => ({
        ...prev,
        [group.id]: currentGroupSnapshot,
      }));
      setConfirmTransferOpen(false);
      notify.success(
        allowsReviewerAssignment
          ? 'Đã duyệt chuyển báo cáo sang phản biện và khóa phiếu GVHD.'
          : 'Đã từ chối chuyển phản biện và yêu cầu nhóm chỉnh sửa báo cáo.',
      );
    } catch (error) {
      notify.error(getThesisErrorMessage(error, 'Không thể thực hiện quyết định chuyển phản biện.'));
    } finally {
      setIsDecidingTransfer(false);
    }
  }

  return (
    <div className="space-y-5 pb-24 xl:pb-10">
      {activeGroupId && group && (
        <GradingBackLink onClick={() => requestNavigation('list')} />
      )}

      <PageHeader
        accent
        compact={!activeGroupId}
        title={
          activeGroupId
            ? `Phiếu chấm điểm · ${role === 'advisor' ? 'Giảng viên hướng dẫn' : 'Giảng viên phản biện'}`
            : 'Chấm điểm khóa luận'
        }
        description={
          activeGroupId
            ? 'Điểm được nhập riêng cho từng sinh viên trong nhóm theo mẫu phiếu ĐHQG-HCM / UIT.'
            : 'Chọn nhóm được phân công để bắt đầu chấm điểm.'
        }
        navigation={
          !activeGroupId ? (
            <NavigationTabs
              tabs={[
                { id: 'advisor', label: 'Hướng dẫn', count: roleGroups.advisor.length },
                { id: 'reviewer', label: 'Phản biện', count: roleGroups.reviewer.length },
              ]}
              activeId={role}
              onSelect={(id) => {
                if (id === 'advisor' || id === 'reviewer') {
                  setRole(id);
                  setSelectedRowId(null);
                  setPageNumber(1);
                }
              }}
              ariaLabel="Chọn loại phân công chấm điểm"
              continuityKey="lecturer-grading-role-tabs"
            />
          ) : undefined
        }
      >
        {!activeGroupId && canSelectBatch && (
          <div className="w-full sm:w-80 md:w-[380px]">
            <Select
              aria-label="Chọn đợt khóa luận tốt nghiệp"
              value={activeBatch?.id ?? ''}
              disabled={batchesQuery.isLoading || batches.length === 0}
              placeholder={batchesQuery.isLoading ? 'Đang tải đợt KLTN...' : 'Chọn đợt KLTN'}
              options={batchOptions}
              onChange={handleBatchChange}
              triggerClassName="h-10 rounded-xl border-line/90 bg-surface-card text-xs font-semibold text-ink-heading shadow-2xs hover:border-line-strong"
              contentClassName="w-max min-w-full sm:min-w-[380px] max-w-[90vw] left-0 right-auto p-1.5 rounded-xl border-line/90 shadow-lg"
              optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-ink-secondary hover:bg-surface-hover"
            />
          </div>
        )}
        {activeGroupId && group && group.submissionFileId && group.canDownloadReport !== false && (
          <Button
            type="button"
            variant="outline"
            isLoading={isLoadingPdf}
            loadingText="Đang mở..."
            onClick={() => void handlePreviewReportPdf()}
            className="gap-2 rounded-lg border-tone-info-border bg-surface-card text-tone-info-text hover:bg-tone-info-bg"
          >
            <Eye className="size-4" aria-hidden="true" />
            Xem báo cáo (PDF)
          </Button>
        )}
      </PageHeader>

      {!activeGroupId && (
        <section
          className="overflow-hidden rounded-2xl border border-line/80 bg-surface-card shadow-sm"
          aria-label="Chọn nhóm chấm điểm"
        >
          <ListCardHeader
            title="Nhóm được phân công"
            description={`${role === 'advisor' ? 'Hướng dẫn' : 'Phản biện'} - ${activeBatch?.batchName ?? (!canSelectBatch && !batchesQuery.isLoading ? 'Chưa có đợt KLTN đang mở' : 'Đợt KLTN hiện hành')}`}
            className="lg:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_auto]"
          >
            <StatisticsBar
              items={[
                {
                  key: 'total',
                  label: 'Tổng nhóm',
                  value: currentRoleGroups.length,
                  icon: <Layers />,
                  tone: 'muted',
                  active: statusFilter === 'all',
                  onClick: () => chooseStatus('all'),
                },
                {
                  key: 'empty',
                  label: 'Chưa nhập điểm',
                  value: groupCounts.empty,
                  icon: <Clock3 />,
                  tone: 'warning',
                  active: statusFilter === 'empty',
                  onClick: () => chooseStatus('empty'),
                },
                {
                  key: 'progress',
                  label: 'Đang chấm',
                  value: groupCounts.inProgress,
                  icon: <FilePenLine />,
                  tone: 'primary',
                  active: statusFilter === 'progress',
                  onClick: () => chooseStatus('progress'),
                },
                {
                  key: 'complete',
                  label: 'Đã nhập đủ',
                  value: groupCounts.complete,
                  icon: <CheckCircle2 />,
                  tone: 'success',
                  active: statusFilter === 'complete',
                  onClick: () => chooseStatus('complete'),
                },
              ]}
            />
          </ListCardHeader>
          <div className="flex flex-wrap items-center gap-3 border-b border-line-subtle bg-surface-subtle/45 px-5 py-3.5">
            <div className="relative min-w-[220px] flex-1">
              <Input
                id="grading-group-search"
                aria-label="Tìm nhóm theo đề tài, tên hoặc mã sinh viên"
                leftIcon={<Search />}
                value={search}
                onChange={(event) => {
                  setSearch(event.target.value);
                  setPageNumber(1);
                }}
                placeholder="Tìm theo đề tài, tên hoặc MSSV..."
                className="pr-9"
              />
              {search && (
                <button
                  type="button"
                  aria-label="Xóa từ khóa tìm kiếm"
                  onClick={() => {
                    setSearch('');
                    setPageNumber(1);
                  }}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded p-1 text-ink-muted hover:bg-surface-muted"
                >
                  <X className="size-3.5" />
                </button>
              )}
            </div>
            <div className="w-full sm:w-48">
              <Select<GroupStatus>
                aria-label="Lọc theo trạng thái chấm"
                leftIcon={<ListFilter className="size-4" />}
                value={statusFilter}
                onChange={(value: GroupStatus) => chooseStatus(value)}
                options={[
                  { value: 'all', label: 'Tất cả trạng thái' },
                  { value: 'empty', label: 'Chưa nhập điểm' },
                  { value: 'progress', label: 'Đang chấm' },
                  { value: 'complete', label: 'Đã nhập đủ' },
                ]}
              />
            </div>
            <div className="w-full sm:w-52">
              <Select<GroupSort>
                aria-label="Sắp xếp nhóm"
                value={sortBy}
                onChange={(value: GroupSort) => {
                  setSortBy(value);
                  setPageNumber(1);
                }}
                options={[
                  { value: 'default', label: 'Thứ tự mặc định' },
                  { value: 'title-asc', label: 'Đề tài A–Z' },
                  { value: 'title-desc', label: 'Đề tài Z–A' },
                  { value: 'student-asc', label: 'Sinh viên A–Z' },
                  { value: 'student-desc', label: 'Sinh viên Z–A' },
                  { value: 'progress-asc', label: 'Ít điểm đã nhập' },
                  { value: 'progress-desc', label: 'Nhiều điểm đã nhập' },
                ]}
              />
            </div>
          </div>
          <DataTable
            data={pageGroups}
            columns={groupColumns}
            getRowId={(item) => item.id}
            selectedRowId={selectedRowId}
            onRowSelect={(id) => setSelectedRowId((prev) => (prev === id ? null : id))}
            onRowOpen={(item) => openGradingSheet(item.id)}
            ariaLabel={`Danh sách nhóm ${role === 'advisor' ? 'hướng dẫn' : 'phản biện'}`}
            tableClassName="min-w-[900px]"
            sort={activeSort}
            onSortChange={sortColumn}
            loading={isLoading}
            error={
              currentRoleError
                ? { message: currentRoleError, onRetry: handleRetryLoad }
                : null
            }
            empty={{
              title:
                search || statusFilter !== 'all'
                  ? 'Không có nhóm phù hợp'
                  : 'Chưa có nhóm được phân công',
              description:
                search || statusFilter !== 'all'
                  ? 'Hãy thử từ khóa hoặc trạng thái khác.'
                  : role === 'advisor'
                    ? 'Bạn chưa được phân công hướng dẫn nhóm nào trong đợt hiện hành.'
                    : 'Bạn chưa được phân công phản biện nhóm nào trong đợt hiện hành.',
            }}
            pagination={{
              pageNumber: currentPage,
              pageSize,
              totalCount: visibleGroups.length,
              onPageChange: setPageNumber,
              onPageSizeChange: (size) => {
                setPageSize(size);
                setPageNumber(1);
              },
              itemLabel: 'nhóm',
            }}
            renderMobileItem={(item) => (
              <div className="space-y-3">
                <div>
                  <p className="font-semibold text-ink-primary">{item.title}</p>
                  <div className="mt-2">
                    <GradingStudentsCell
                      students={item.students}
                      ariaLabel={`Thành viên nhóm ${item.title}`}
                    />
                  </div>
                </div>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  {groupColumns[3].cell(item, 0)}
                  {groupColumns[4].cell(item, 0)}
                </div>
              </div>
            )}
          />
        </section>
      )}

      <TableFloatingBar
        selectedCount={selectedGroupRow && !activeGroupId ? 1 : 0}
        onClearSelection={() => setSelectedRowId(null)}
      >
        {selectedGroupRow && (
          <Button
            type="button"
            size="sm"
            onClick={() => openGradingSheet(selectedGroupRow.id)}
            className="h-8 gap-1.5 rounded-lg px-3 text-xs font-semibold"
          >
            <SquarePen className="size-3.5" aria-hidden="true" />
            {selectedGroupRow.isLocked ? 'Xem phiếu' : 'Chấm điểm'}
          </Button>
        )}
      </TableFloatingBar>

      <ConfirmDialog
        open={Boolean(pendingLeaveTarget)}
        onClose={() => setPendingLeaveTarget(null)}
        onConfirm={applyConfirmedLeave}
        type="warning"
        title="Thay đổi chưa được lưu"
        confirmText="Rời khỏi phiếu"
        cancelText="Ở lại"
        description={
          <div className="space-y-2 text-sm text-ink-body">
            <p>
              Bạn đang có điểm hoặc nhận xét mới chỉnh sửa chưa lưu nháp lên máy chủ.
            </p>
            <p className="text-xs text-ink-muted">
              Bạn có muốn rời khỏi phiếu chấm này ngay bây giờ không?
            </p>
          </div>
        }
      />

      <PdfPreviewDialog
        open={pdfPreviewState.open}
        onClose={() => setPdfPreviewState((prev) => ({ ...prev, open: false }))}
        previewUrl={pdfPreviewState.url}
        fileName={pdfPreviewState.fileName}
        fileSize={pdfPreviewState.fileSize}
      />

      <ConfirmDialog
        open={confirmSubmitOpen && !!group}
        onClose={() => setConfirmSubmitOpen(false)}
        onConfirm={() => void handleSaveEvaluation('Submit')}
        isConfirming={isSubmittingScore}
        title={
          role === 'advisor'
            ? 'Xác nhận lưu điểm hướng dẫn chính thức'
            : 'Xác nhận chốt phiếu chấm phản biện'
        }
        confirmText={role === 'advisor' ? 'Xác nhận lưu điểm' : 'Chốt phiếu phản biện'}
        cancelText="Kiểm tra lại"
        description={
          group && (
            <div className="space-y-3">
              <p>
                Bạn đang thực hiện{' '}
                {role === 'advisor' ? 'lưu điểm chính thức' : 'chốt phiếu phản biện'} cho đề tài{' '}
                <strong>{group.title}</strong>:
              </p>
              <div className="rounded-xl border border-line bg-surface-subtle p-3.5 space-y-2">
                {progress.map((student) => (
                  <div key={student.id} className="flex items-center justify-between text-sm">
                    <span className="font-medium text-ink-heading">
                      {student.name} <span className="text-ink-muted">({student.code})</span>
                    </span>
                    <span className="font-bold tabular-nums text-tone-info-text">
                      {student.total.toFixed(2)} / 10
                    </span>
                  </div>
                ))}
                {role === 'reviewer' && (
                  <div className="border-t border-line pt-2 mt-2 flex items-center justify-between text-xs">
                    <span className="text-ink-muted">Đề nghị bảo vệ:</span>
                    <span
                      className={
                        reviewerDecision === 'Approved'
                          ? 'font-semibold text-tone-success-text'
                          : 'font-semibold text-tone-danger-text'
                      }
                    >
                      {reviewerDecision === 'Approved'
                        ? 'Đồng ý cho bảo vệ'
                        : 'Không đồng ý cho bảo vệ'}
                    </span>
                  </div>
                )}
              </div>
              {role === 'reviewer' && (
                <p className="text-xs text-ink-muted">
                  Lưu ý: Sau khi chốt phiếu phản biện, phiếu chấm sẽ được khóa để chuyển sang xét điều
                  kiện ra Hội đồng.
                </p>
              )}
            </div>
          )
        }
      />

      <ConfirmDialog
        open={confirmTransferOpen && !!group}
        onClose={() => setConfirmTransferOpen(false)}
        onConfirm={() => void handleConfirmAdvisorTransferDecision()}
        isConfirming={isDecidingTransfer}
        title={
          advisorDecision === 'Approve'
            ? 'Xác nhận duyệt chuyển sang phản biện'
            : 'Xác nhận yêu cầu chỉnh sửa báo cáo cuối kỳ'
        }
        confirmText={
          advisorDecision === 'Approve' ? 'Duyệt và Khóa phiếu' : 'Gửi yêu cầu chỉnh sửa'
        }
        cancelText="Hủy"
        type={advisorDecision === 'Approve' ? 'info' : 'warning'}
        description={
          group && (
            <div className="space-y-3">
              <p>
                {advisorDecision === 'Approve'
                  ? 'Sau khi xác nhận đồng ý chuyển phản biện, phiếu chấm của GVHD sẽ được khóa và nhóm chuyển sang trạng thái chờ phân công Giảng viên phản biện.'
                  : 'Nhóm sinh viên sẽ nhận được lý do từ chối và cần nộp lại phiên bản báo cáo cuối kỳ mới.'}
              </p>
              <div className="rounded-xl border border-line bg-surface-subtle p-3.5 space-y-1.5 text-sm">
                <p className="font-semibold text-ink-primary">{group.title}</p>
                {progress.map((student) => (
                  <div key={student.id} className="flex items-center justify-between text-xs">
                    <span className="text-ink-body">
                      {student.name} ({student.code})
                    </span>
                    <span className="font-semibold tabular-nums text-ink-primary">
                      {student.total.toFixed(2)} / 10
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )
        }
      />

      {activeGroupId && group && (
        <>
          <section className="rounded-2xl border border-tone-info-border bg-tone-info-bg px-5 py-5 sm:px-6">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-xs font-medium text-ink-body">
                  {group.batchName} · Báo cáo cuối kỳ
                </p>
                <h2 className="mt-2 text-lg font-semibold text-ink-primary">{group.title}</h2>
                <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1">
                  {students.map((student, idx) => (
                    <span key={student.id} className="text-sm text-ink-secondary">
                      <span className="mr-1.5 text-xs font-semibold text-ink-muted">
                        SV{idx + 1}:
                      </span>
                      {student.name} <span className="text-ink-muted">· {student.code}</span>
                    </span>
                  ))}
                </div>
              </div>
              <span
                className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold ring-1 ${
                  isLocked
                    ? 'bg-tone-success-bg text-tone-success-text ring-tone-success-border'
                    : group.evaluationStatus === 'Submitted'
                      ? 'bg-tone-info-bg text-tone-info-text ring-tone-info-border'
                      : group.evaluationStatus === 'Draft'
                        ? 'bg-tone-warning-bg text-tone-warning-text ring-tone-warning-border'
                        : 'bg-surface-card text-ink-body ring-line'
                }`}
              >
                {isLocked && <Lock className="size-3.5" aria-hidden="true" />}
                {isLocked
                  ? 'Đã chốt và khóa phiếu'
                  : group.evaluationStatus === 'Submitted'
                    ? 'Đã lưu điểm chính thức'
                    : group.evaluationStatus === 'Draft'
                      ? 'Đã lưu nháp'
                      : 'Đang chấm điểm'}
              </span>
            </div>
          </section>

          <div className="grid grid-cols-1 items-start gap-5 xl:grid-cols-[minmax(0,1fr)_310px]">
            <GradingRubricBoard
              idPrefix={role}
              groupTitle={group.title}
              sections={sections}
              criteriaCount={criteria.length}
              students={progress}
              scores={scores}
              isLocked={isLocked}
              onUpdateScore={updateScore}
            >
              <div className="border-t border-line-subtle p-5 sm:p-6">
                <label
                  id="grading-comment-label"
                  htmlFor="grading-comment"
                  className="text-sm font-semibold text-ink-primary"
                >
                  Nhận xét chung
                </label>
                <p className="mt-1 mb-3 text-sm text-ink-muted">
                  Ghi lại nhận xét chuyên môn cho phiếu{' '}
                  {role === 'advisor' ? 'hướng dẫn' : 'phản biện'}.
                </p>
                <RichTextEditor
                  id="grading-comment"
                  ariaLabelledBy="grading-comment-label"
                  disabled={isLocked}
                  readOnly={isLocked}
                  value={comments[group.id] ?? ''}
                  onChange={(nextValue) =>
                    setComments((prev) => ({ ...prev, [group.id]: nextValue }))
                  }
                  maxLength={2000}
                  placeholder="Nhập nhận xét về kết quả thực hiện khóa luận..."
                  expandedTitle={`Nhận xét chung (${role === 'advisor' ? 'Hướng dẫn' : 'Phản biện'})`}
                  expandedSubtitle={group.title}
                />
              </div>

              {role === 'advisor' ? (
                <div className="border-t border-line bg-surface-subtle/60 p-5 sm:p-6 space-y-3">
                  <div>
                    <h3 className="text-sm font-semibold text-ink-primary">
                      Quyết định chuyển báo cáo sang giai đoạn Phản biện
                    </h3>
                    <p className="mt-1 text-xs text-ink-muted">
                      Sau khi chấm điểm hướng dẫn, GVHD xác nhận cho phép nhóm chuyển sang phân công
                      Giảng viên phản biện hoặc yêu cầu chỉnh sửa lại báo cáo.
                    </p>
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <button
                      type="button"
                      disabled={isLocked}
                      onClick={() =>
                        setAdvisorDecisionByGroup((prev) => ({ ...prev, [group.id]: 'Approve' }))
                      }
                      className={`flex items-start gap-3 rounded-xl border p-3.5 text-left transition ${
                        advisorDecision === 'Approve'
                          ? 'border-primary bg-tone-info-bg ring-2 ring-tone-info-border/70'
                          : 'border-line bg-surface-card hover:border-line-strong'
                      } disabled:opacity-60`}
                    >
                      <ShieldCheck
                        className={`mt-0.5 size-4 shrink-0 ${
                          advisorDecision === 'Approve' ? 'text-tone-info-icon' : 'text-ink-subtle'
                        }`}
                      />
                      <div>
                        <p className="text-sm font-semibold text-ink-primary">
                          Đồng ý chuyển phản biện
                        </p>
                        <p className="mt-0.5 text-xs text-ink-muted">
                          Khóa phiếu chấm GVHD và chuyển nhóm sang chờ phân công GVPB.
                        </p>
                      </div>
                    </button>

                    <button
                      type="button"
                      disabled={isLocked}
                      onClick={() =>
                        setAdvisorDecisionByGroup((prev) => ({ ...prev, [group.id]: 'Reject' }))
                      }
                      className={`flex items-start gap-3 rounded-xl border p-3.5 text-left transition ${
                        advisorDecision === 'Reject'
                          ? 'border-tone-warning-icon bg-tone-warning-bg ring-2 ring-tone-warning-border/70'
                          : 'border-line bg-surface-card hover:border-line-strong'
                      } disabled:opacity-60`}
                    >
                      <XCircle
                        className={`mt-0.5 size-4 shrink-0 ${
                          advisorDecision === 'Reject' ? 'text-tone-warning-icon' : 'text-ink-subtle'
                        }`}
                      />
                      <div>
                        <p className="text-sm font-semibold text-ink-primary">
                          Chưa đồng ý · Yêu cầu sửa
                        </p>
                        <p className="mt-0.5 text-xs text-ink-muted">
                          Yêu cầu sinh viên chỉnh sửa và nộp lại báo cáo cuối kỳ.
                        </p>
                      </div>
                    </button>
                  </div>

                  {advisorDecision === 'Reject' && (
                    <div className="pt-1">
                      <label
                        htmlFor="advisor-rejection-reason"
                        className="block text-xs font-semibold text-ink-secondary"
                      >
                        Lý do yêu cầu chỉnh sửa <span className="text-tone-danger-icon">*</span>
                      </label>
                      <textarea
                        id="advisor-rejection-reason"
                        disabled={isLocked}
                        value={advisorRejectionReason}
                        onChange={(event) =>
                          setAdvisorRejectionReasonByGroup((prev) => ({
                            ...prev,
                            [group.id]: event.target.value,
                          }))
                        }
                        rows={2}
                        placeholder="Nhập nội dung cần nhóm sinh viên bổ sung hoặc hoàn thiện..."
                        className="mt-1.5 w-full rounded-lg border border-line-strong bg-surface-card p-2.5 text-sm text-ink-primary outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                      />
                    </div>
                  )}
                </div>
              ) : (
                <div className="border-t border-line bg-surface-subtle/60 p-5 sm:p-6 space-y-3">
                  <div>
                    <h3 className="text-sm font-semibold text-ink-primary">
                      Kết luận của Giảng viên phản biện
                    </h3>
                    <p className="mt-1 text-xs text-ink-muted">
                      Đề nghị cho phép hoặc không cho phép nhóm sinh viên bảo vệ trước Hội đồng
                      (theo quy chế ĐHQG-HCM / UIT, mỗi sinh viên cần đạt điểm phản biện từ 5.0 trở
                      lên để đủ điều kiện ra Hội đồng).
                    </p>
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <button
                      type="button"
                      disabled={isLocked}
                      onClick={() =>
                        setReviewerDecisionByGroup((prev) => ({
                          ...prev,
                          [group.id]: 'Approved',
                        }))
                      }
                      className={`flex items-start gap-3 rounded-xl border p-3.5 text-left transition ${
                        reviewerDecision === 'Approved'
                          ? 'border-tone-success-icon bg-tone-success-bg ring-2 ring-tone-success-border/70'
                          : 'border-line bg-surface-card hover:border-line-strong'
                      } disabled:opacity-60`}
                    >
                      <CheckCircle2
                        className={`mt-0.5 size-4 shrink-0 ${
                          reviewerDecision === 'Approved' ? 'text-tone-success-icon' : 'text-ink-subtle'
                        }`}
                      />
                      <div>
                        <p className="text-sm font-semibold text-ink-primary">
                          Đề nghị cho bảo vệ (Approved)
                        </p>
                        <p className="mt-0.5 text-xs text-ink-muted">
                          Báo cáo đạt yêu cầu chuyên môn để đưa ra Hội đồng bảo vệ KLTN.
                        </p>
                      </div>
                    </button>

                    <button
                      type="button"
                      disabled={isLocked}
                      onClick={() =>
                        setReviewerDecisionByGroup((prev) => ({
                          ...prev,
                          [group.id]: 'Rejected',
                        }))
                      }
                      className={`flex items-start gap-3 rounded-xl border p-3.5 text-left transition ${
                        reviewerDecision === 'Rejected'
                          ? 'border-tone-danger-icon bg-tone-danger-bg ring-2 ring-tone-danger-border/70'
                          : 'border-line bg-surface-card hover:border-line-strong'
                      } disabled:opacity-60`}
                    >
                      <XCircle
                        className={`mt-0.5 size-4 shrink-0 ${
                          reviewerDecision === 'Rejected' ? 'text-tone-danger-icon' : 'text-ink-subtle'
                        }`}
                      />
                      <div>
                        <p className="text-sm font-semibold text-ink-primary">
                          Không đồng ý bảo vệ (Rejected)
                        </p>
                        <p className="mt-0.5 text-xs text-ink-muted">
                          Báo cáo chưa đạt yêu cầu để bảo vệ tại Hội đồng.
                        </p>
                      </div>
                    </button>
                  </div>

                  {reviewerDecision === 'Approved' && allStudentsComplete && anyStudentBelowFive && (
                    <div className="rounded-xl border border-tone-warning-border bg-tone-warning-bg px-3.5 py-2.5 text-xs text-tone-warning-strong">
                      Lưu ý: Có sinh viên trong nhóm đang có tổng điểm phản biện dưới{' '}
                      <strong>5.0</strong>. Khi xét điều kiện xếp lịch Hội đồng, hệ thống sẽ chặn nếu
                      điểm phản biện của sinh viên thấp hơn ngưỡng quy định.
                    </div>
                  )}
                </div>
              )}
            </GradingRubricBoard>

            <GradingProgressAside
              students={progress}
              criteriaCount={criteria.length}
              hasUnsavedChanges={hasUnsavedChanges && !isLocked}
              noteText={`Điểm tổng của từng sinh viên được cộng từ các tiêu chí thành phần. Cần nhập đủ ${criteria.length} tiêu chí cho mọi sinh viên trong nhóm trước khi chốt điểm chính thức.`}
              actions={
                isLocked ? (
                  <div className="flex items-center gap-2 rounded-xl border border-tone-success-border bg-tone-success-bg px-3.5 py-2.5 text-xs font-medium text-tone-success-strong">
                    <Lock className="size-4 shrink-0" />
                    <span>Phiếu chấm đã được chốt và khóa bảo vệ dữ liệu.</span>
                  </div>
                ) : (
                  <>
                    <div className="grid grid-cols-2 gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        disabled={isSavingDraft || isSubmittingScore || isDecidingTransfer}
                        isLoading={isSavingDraft}
                        loadingText="Đang lưu..."
                        onClick={() => void handleSaveEvaluation('SaveDraft')}
                        className="w-full gap-1.5 rounded-xl text-xs font-semibold"
                      >
                        <Save className="size-3.5" aria-hidden="true" />
                        Lưu nháp
                      </Button>
                      <Button
                        type="button"
                        variant={role === 'advisor' ? 'soft' : 'default'}
                        disabled={
                          !allStudentsComplete ||
                          isSavingDraft ||
                          isSubmittingScore ||
                          isDecidingTransfer
                        }
                        onClick={() => setConfirmSubmitOpen(true)}
                        className="w-full gap-1.5 rounded-xl text-xs font-semibold"
                      >
                        <Send className="size-3.5" aria-hidden="true" />
                        {role === 'advisor' ? 'Lưu điểm' : 'Chốt phiếu'}
                      </Button>
                    </div>

                    {role === 'advisor' && (
                      <div className="border-t border-line-subtle pt-2.5">
                        <Button
                          type="button"
                          variant="default"
                          disabled={
                            (advisorDecision === 'Approve' && !allStudentsComplete) ||
                            isSavingDraft ||
                            isSubmittingScore ||
                            isDecidingTransfer
                          }
                          onClick={() => setConfirmTransferOpen(true)}
                          className={cn(
                            'h-10 w-full rounded-xl text-xs font-semibold',
                            advisorDecision === 'Reject' &&
                              'bg-amber-600 text-white shadow-sm hover:bg-amber-700 active:bg-amber-800 focus-visible:ring-amber-600/30',
                          )}
                        >
                          {advisorDecision === 'Approve'
                            ? 'Duyệt chuyển sang phản biện'
                            : 'Gửi yêu cầu sửa báo cáo'}
                        </Button>
                      </div>
                    )}
                  </>
                )
              }
            />
          </div>

          <GradingMobileDock
            visible={!isLocked}
            students={progress}
            actions={
              <>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={isSavingDraft || isSubmittingScore || isDecidingTransfer}
                  isLoading={isSavingDraft}
                  loadingText="Đang lưu..."
                  onClick={() => void handleSaveEvaluation('SaveDraft')}
                  className="gap-1.5 rounded-xl text-xs font-semibold"
                >
                  <Save className="size-3.5" aria-hidden="true" />
                  Lưu nháp
                </Button>
                <Button
                  type="button"
                  variant={role === 'advisor' ? 'soft' : 'default'}
                  size="sm"
                  disabled={
                    !allStudentsComplete ||
                    isSavingDraft ||
                    isSubmittingScore ||
                    isDecidingTransfer
                  }
                  onClick={() => setConfirmSubmitOpen(true)}
                  className="gap-1.5 rounded-xl text-xs font-semibold"
                >
                  <Send className="size-3.5" aria-hidden="true" />
                  {role === 'advisor' ? 'Lưu điểm' : 'Chốt phiếu'}
                </Button>
              </>
            }
          />
        </>
      )}
    </div>
  );
}
