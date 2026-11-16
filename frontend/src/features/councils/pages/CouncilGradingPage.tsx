import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  CalendarClock,
  CheckCircle2,
  Clock3,
  FileCheck2,
  Download,
  FilePenLine,
  Layers,
  ListFilter,
  Lock,
  MapPin,
  Save,
  Search,
  Send,
  SquarePen,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { Input } from '@/components/ui/input';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { PageHeader } from '@/components/ui/page-header';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { Select } from '@/components/ui/select';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import { getCurrentBatches } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { notify } from '@/shared/notifications';
import {
  GradingMobileDock,
  GradingProgressAside,
} from '@/features/thesis/components/grading/GradingProgressAside';
import { GradingBackLink } from '@/features/thesis/components/grading/GradingBackLink';
import { GradingRubricBoard } from '@/features/thesis/components/grading/GradingRubricBoard';
import {
  GradingStatusCell,
  GradingStudentsCell,
} from '@/features/thesis/components/grading/GradingTableCells';
import {
  computeStudentProgressList,
  countFilledGroupCriteria,
  type GradingStudentInfo,
  type GradingStudentScoresMap,
} from '@/features/thesis/components/grading/gradingShared';
import type { GradingCriterionDto } from '@/features/thesis/services/lecturerGradingApi';
import {
  buildRubricSections,
  buildStudentScoresPayload,
  getStudentInitials,
} from '@/features/thesis/utils/gradingRubricHelper';
import { getThesisErrorMessage } from '@/features/thesis/utils/thesisErrors';
import { finalReportApi } from '@/features/thesis/services/finalReportApi';
import { councilGradingApi } from '../services/councilGradingApi';

type Student = GradingStudentInfo;
type CouncilGroup = {
  id: string;
  groupId: string;
  assignmentId?: string | null;
  councilCode: string;
  memberLabel: string;
  title: string;
  session: 'Sáng' | 'Chiều';
  schedule: string;
  room: string;
  students: Student[];
  isLocked?: boolean;
  isSubmitted?: boolean;
};
type Scores = GradingStudentScoresMap;
type GroupStatus = 'all' | 'empty' | 'progress' | 'complete';
type GroupSort =
  | 'default'
  | 'title-asc'
  | 'title-desc'
  | 'session-asc'
  | 'session-desc'
  | 'progress-asc'
  | 'progress-desc';
type SortColumn = 'topic' | 'session' | 'status';

const ROLE_LABELS: Record<string, string> = {
  Chairman: 'Chủ tịch HĐ',
  Secretary: 'Thư ký HĐ',
  Member: 'Ủy viên HĐ',
};

function serializeCouncilSnapshot(
  scores: GradingStudentScoresMap | undefined,
  comment: string,
): string {
  const sortedScores: Record<string, Record<string, string>> = {};
  for (const studentId of Object.keys(scores ?? {}).sort()) {
    const inner = scores?.[studentId] ?? {};
    const sortedInner: Record<string, string> = {};
    for (const criterionId of Object.keys(inner).sort()) {
      const value = inner[criterionId]?.trim();
      if (value) sortedInner[criterionId] = value;
    }
    if (Object.keys(sortedInner).length > 0) {
      sortedScores[studentId] = sortedInner;
    }
  }
  return JSON.stringify({ scores: sortedScores, comment: comment.trim() });
}

function resolveSession(timeSlot: string): 'Sáng' | 'Chiều' {
  const normalized = timeSlot.toLocaleLowerCase('vi');
  if (normalized.includes('chiều') || normalized.includes('13:') || normalized.includes('14:')) {
    return 'Chiều';
  }
  return 'Sáng';
}

export function CouncilGradingPage() {
  const { user } = useAuth();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<GroupStatus>('all');
  const [sessionFilter, setSessionFilter] = useState<'all' | CouncilGroup['session']>('all');
  const [sortBy, setSortBy] = useState<GroupSort>('default');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [activeGroupId, setActiveGroupId] = useState<string | null>(null);
  const [pendingLeaveTarget, setPendingLeaveTarget] = useState<'list' | string | null>(null);
  const [savedSnapshotByGroup, setSavedSnapshotByGroup] = useState<Record<string, string>>({});

  const [apiCriteria, setApiCriteria] = useState<GradingCriterionDto[] | null>(null);
  const [apiGroups, setApiGroups] = useState<CouncilGroup[]>([]);
  const [loadedRequestKey, setLoadedRequestKey] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);
  const [scoresByGroup, setScoresByGroup] = useState<Record<string, Scores>>({});
  const [comments, setComments] = useState<Record<string, string>>({});
  const [submittedGroupIds, setSubmittedGroupIds] = useState<Record<string, boolean>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [confirmSubmitOpen, setConfirmSubmitOpen] = useState(false);

  const batchesQuery = useAcademicBatches();
  const activeBatch = useMemo(() => {
    const batches = batchesQuery.data ?? [];
    const current = getCurrentBatches(batches);
    return current[0] ?? batches[0] ?? null;
  }, [batchesQuery.data]);

  const sections = useMemo(() => buildRubricSections(apiCriteria), [apiCriteria]);
  const criteria = useMemo(() => sections.flatMap((section) => section.criteria), [sections]);

  const currentRequestKey = activeBatch?.id ? `${activeBatch.id}:${reloadNonce}` : null;

  useEffect(() => {
    if (!activeBatch?.id || !currentRequestKey) return;
    const controller = new AbortController();
    const requestKey = currentRequestKey;

    async function loadCouncilWorkspace() {
      try {
        const [rubric, overviewRows] = await Promise.all([
          councilGradingApi.getGradingRubric(activeBatch!.id, controller.signal).catch(() => null),
          councilGradingApi.getCouncilOverviewRows(activeBatch!.id, controller.signal),
        ]);

        if (rubric?.criteria?.length) {
          setApiCriteria(rubric.criteria);
        }

        const uniqueCouncilIds = Array.from(
          new Set(overviewRows.filter((r) => r.status === 'Active' || r.status === 'Draft').map((r) => r.id)),
        );

        const builtGroups: CouncilGroup[] = [];
        if (uniqueCouncilIds.length > 0) {
          const schedules = await Promise.all(
            uniqueCouncilIds.map((id) =>
              councilGradingApi.getCouncilSchedule(id, controller.signal).catch(() => null),
            ),
          );

          const assignmentMap = new Map<string, { assignmentId: string | null; isLocked: boolean }>();
          for (const row of overviewRows) {
            if (row.assignmentId && row.topicTitle) {
              assignmentMap.set(`${row.id}-${row.topicTitle}`, {
                assignmentId: row.assignmentId,
                isLocked: row.minuteLockStatus === 'Locked',
              });
            }
          }

          for (const schedule of schedules) {
            if (!schedule) continue;
            const matchedMember =
              schedule.members.find((m) => m.userId === user?.id) ?? schedule.members[0];
            const memberLabel = matchedMember
              ? `${matchedMember.fullName} · ${ROLE_LABELS[matchedMember.role] ?? matchedMember.role}`
              : 'Thành viên Hội đồng';

            for (const grp of schedule.groups) {
              const key = `${schedule.councilId}-${grp.topicTitleVi}`;
              const meta = assignmentMap.get(key);
              const resolvedAssignmentId = grp.assignmentId ?? meta?.assignmentId ?? null;
              const rowId = resolvedAssignmentId ?? `${schedule.councilId}-${grp.groupId}`;

              builtGroups.push({
                id: rowId,
                groupId: grp.groupId,
                assignmentId: resolvedAssignmentId,
                councilCode: schedule.councilCode,
                memberLabel,
                title: grp.topicTitleVi,
                session: resolveSession(schedule.timeSlot),
                schedule: `${schedule.timeSlot} · ${schedule.defenseDate}`,
                room: schedule.room,
                isLocked: meta?.isLocked ?? false,
                students: grp.students.map((st) => ({
                  id: st.studentId,
                  name: st.fullName,
                  code: st.userCode || '—',
                  initials: getStudentInitials(st.fullName),
                })),
              });
            }
          }
        }

        if (!controller.signal.aborted) {
          setApiGroups(builtGroups);
          setLoadError(null);
        }
      } catch (err) {
        if (!controller.signal.aborted) {
          setLoadError(getThesisErrorMessage(err, 'Không thể tải danh sách lịch chấm điểm Hội đồng.'));
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoadedRequestKey(requestKey);
        }
      }
    }

    void loadCouncilWorkspace();
    return () => controller.abort();
  }, [activeBatch, user?.id, currentRequestKey]);

  const groups = useMemo(() => {
    if (!activeBatch?.id) return [];
    return apiGroups.map((item) =>
      submittedGroupIds[item.id] ? { ...item, isSubmitted: true } : item,
    );
  }, [activeBatch?.id, apiGroups, submittedGroupIds]);

  const isLoading =
    batchesQuery.isLoading || Boolean(currentRequestKey && loadedRequestKey !== currentRequestKey);
  const resolvedError = batchesQuery.isError
    ? getThesisErrorMessage(batchesQuery.error, 'Không thể tải danh sách đợt khóa luận.')
    : activeBatch?.id
      ? loadError
      : null;

  function handleRetryLoad() {
    if (batchesQuery.isError) {
      void batchesQuery.refetch();
    }
    setReloadNonce((prev) => prev + 1);
  }
  const activeGroup = groups.find((group) => group.id === activeGroupId);
  const revisedReportQuery = useQuery({
    queryKey: ['council-revised-final-report', activeGroup?.groupId],
    queryFn: ({ signal }) => finalReportApi.getRevisedFinalReportHistory(activeGroup!.groupId, signal),
    enabled: Boolean(activeGroup?.groupId),
    retry: false,
  });
  const latestRevisedVersion = revisedReportQuery.data?.versions.at(-1);
  const isLocked = Boolean(activeGroup?.isLocked);

  const groupProgress = (group: CouncilGroup) =>
    countFilledGroupCriteria(group.students, criteria, scoresByGroup[group.id]);

  const statusOf = (group: CouncilGroup): Exclude<GroupStatus, 'all'> => {
    const completed = groupProgress(group);
    if (completed === 0) return 'empty';
    return completed === criteria.length * group.students.length ? 'complete' : 'progress';
  };

  const counts = groups.reduce(
    (all, group) => ({ ...all, [statusOf(group)]: all[statusOf(group)] + 1 }),
    { empty: 0, progress: 0, complete: 0 },
  );
  const normalizedSearch = search.trim().toLocaleLowerCase('vi');
  const visibleGroups = groups
    .filter(
      (group) =>
        (statusFilter === 'all' || statusOf(group) === statusFilter) &&
        (sessionFilter === 'all' || group.session === sessionFilter) &&
        [
          group.title,
          group.room,
          ...group.students.flatMap((student) => [student.name, student.code]),
        ].some((item) => item.toLocaleLowerCase('vi').includes(normalizedSearch)),
    )
    .sort((left, right) => {
      if (sortBy === 'title-asc') return left.title.localeCompare(right.title, 'vi');
      if (sortBy === 'title-desc') return right.title.localeCompare(left.title, 'vi');
      if (sortBy === 'session-asc') return left.schedule.localeCompare(right.schedule, 'vi');
      if (sortBy === 'session-desc') return right.schedule.localeCompare(left.schedule, 'vi');
      if (sortBy === 'progress-asc') return groupProgress(left) - groupProgress(right);
      if (sortBy === 'progress-desc') return groupProgress(right) - groupProgress(left);
      return 0;
    });

  const currentPage = Math.min(pageNumber, Math.max(1, Math.ceil(visibleGroups.length / pageSize)));
  const pageGroups = visibleGroups.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const selectedGroupRow = visibleGroups.find((item) => item.id === selectedRowId) ?? null;
  const activeSort: { field: SortColumn; direction: 'asc' | 'desc' } | undefined =
    sortBy === 'default'
      ? undefined
      : {
          field: sortBy.startsWith('title')
            ? 'topic'
            : sortBy.startsWith('session')
              ? 'session'
              : 'status',
          direction: sortBy.endsWith('asc') ? 'asc' : 'desc',
        };
  const changeStatus = (status: GroupStatus) => {
    setStatusFilter(status);
    setPageNumber(1);
  };
  const updateSort = (field: SortColumn) => {
    const prefix = field === 'topic' ? 'title' : field === 'session' ? 'session' : 'progress';
    setSortBy(
      `${prefix}-${activeSort?.field === field && activeSort.direction === 'asc' ? 'desc' : 'asc'}` as GroupSort,
    );
    setPageNumber(1);
  };

  const columns: DataTableColumn<CouncilGroup, SortColumn>[] = [
    {
      id: 'number',
      header: 'STT',
      widthClassName: 'w-[6%]',
      cell: (_group, index) => (
        <span className="tabular-nums text-slate-500">
          {(currentPage - 1) * pageSize + index + 1}
        </span>
      ),
    },
    {
      id: 'topic',
      header: 'Đề tài',
      sortKey: 'topic',
      widthClassName: 'w-[27%]',
      cell: (group) => (
        <span className="block">
          <span className="block font-semibold leading-relaxed text-slate-900">{group.title}</span>
          <span className="mt-1 block text-xs text-slate-500">
            {group.councilCode} · {activeBatch?.batchName ?? 'Đợt KLTN hiện hành'}
          </span>
        </span>
      ),
    },
    {
      id: 'students',
      header: 'Sinh viên',
      widthClassName: 'w-[26%]',
      cell: (group) => (
        <GradingStudentsCell
          students={group.students}
          ariaLabel={`Thành viên nhóm ${group.title}`}
        />
      ),
    },
    {
      id: 'session',
      header: 'Lịch bảo vệ',
      sortKey: 'session',
      widthClassName: 'w-[16%]',
      cell: (group) => {
        const [timeSlot, defenseDate] = group.schedule.split(' · ');
        return (
          <span className="block text-sm text-slate-700">
            <span className="block font-medium text-slate-800">{timeSlot}</span>
            <span className="mt-1 block whitespace-nowrap text-xs text-slate-500">
              {defenseDate ? `${defenseDate} · ${group.room}` : group.room}
            </span>
          </span>
        );
      },
    },
    {
      id: 'status',
      header: 'Trạng thái',
      sortKey: 'status',
      widthClassName: 'w-[13%]',
      headerClassName: 'whitespace-nowrap',
      cell: (group) => (
        <GradingStatusCell
          status={statusOf(group)}
          filledCount={groupProgress(group)}
          totalCount={criteria.length * group.students.length}
          lockedLabel={
            group.isLocked ? 'Đã khóa biên bản' : group.isSubmitted ? 'Đã gửi điểm' : null
          }
        />
      ),
    },
    {
      id: 'action',
      header: 'Thao tác',
      widthClassName: 'w-[13%]',
      headerClassName: 'whitespace-nowrap',
      align: 'right',
      cell: (group) => (
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => openGradingSheet(group.id)}
          className="gap-1.5 rounded-md border-slate-200 bg-white text-slate-700 shadow-2xs hover:border-blue-300 hover:bg-blue-50/70 hover:text-blue-700"
        >
          <SquarePen className="size-3.5" aria-hidden="true" />
          {group.isLocked ? 'Xem phiếu' : 'Chấm điểm'}
        </Button>
      ),
    },
  ];

  const scores = activeGroup ? (scoresByGroup[activeGroup.id] ?? {}) : {};
  const progress = activeGroup
    ? computeStudentProgressList(activeGroup.students, criteria, scores)
    : [];

  const allStudentsComplete =
    progress.length > 0 && progress.every((student) => student.count === criteria.length);

  const currentGroupSnapshot = activeGroup
    ? serializeCouncilSnapshot(scoresByGroup[activeGroup.id], comments[activeGroup.id] ?? '')
    : '';
  const baselineSnapshot = activeGroup
    ? (savedSnapshotByGroup[activeGroup.id] ?? serializeCouncilSnapshot(undefined, ''))
    : '';
  const hasUnsavedChanges = Boolean(
    activeGroup && !isLocked && currentGroupSnapshot !== baselineSnapshot,
  );

  function openGradingSheet(groupId: string) {
    setSelectedRowId(groupId);
    setActiveGroupId(groupId);
    window.scrollTo({ top: 0 });
  }

  async function downloadRevisedReport(fileId: string) {
    try {
      const result = await finalReportApi.getDownloadUrl(fileId);
      window.open(result.downloadUrl, '_blank', 'noopener,noreferrer');
    } catch (error) {
      notify.error(getThesisErrorMessage(error, 'Không thể tải bản báo cáo chỉnh sửa.'));
    }
  }

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

  const updateScore = (studentId: string, criterionId: string, value: string) => {
    if (!activeGroup || isLocked) return;
    setScoresByGroup((previous) => ({
      ...previous,
      [activeGroup.id]: {
        ...previous[activeGroup.id],
        [studentId]: { ...previous[activeGroup.id]?.[studentId], [criterionId]: value },
      },
    }));
  };

  function handleSaveTempDraft() {
    if (!activeGroup || isLocked) return;
    setSavedSnapshotByGroup((prev) => ({
      ...prev,
      [activeGroup.id]: currentGroupSnapshot,
    }));
    notify.success('Đã lưu tạm điểm Hội đồng theo từng sinh viên trong phiên làm việc.');
  }

  async function handleSubmitCouncilScore() {
    if (!activeGroup || isLocked) return;
    if (!allStudentsComplete) {
      notify.error('Vui lòng nhập đầy đủ tất cả tiêu chí cho từng sinh viên trước khi gửi điểm.');
      return;
    }

    const studentScores = buildStudentScoresPayload(
      activeGroup.students.map((s) => s.id),
      criteria,
      scoresByGroup[activeGroup.id],
    );

    if (!activeGroup.assignmentId) {
      notify.error('Không tìm thấy mã phân công Hội đồng hợp lệ cho nhóm này.');
      setConfirmSubmitOpen(false);
      return;
    }

    try {
      setIsSubmitting(true);
      await councilGradingApi.submitCouncilScore(activeGroup.assignmentId, {
        studentScores,
        feedback: (comments[activeGroup.id] ?? '').trim() || null,
      });
      setSubmittedGroupIds((prev) => ({ ...prev, [activeGroup.id]: true }));
      setSavedSnapshotByGroup((prev) => ({
        ...prev,
        [activeGroup.id]: currentGroupSnapshot,
      }));
      setConfirmSubmitOpen(false);
      notify.success('Đã gửi điểm Hội đồng cho từng sinh viên lên hệ thống.');
    } catch (error) {
      notify.error(getThesisErrorMessage(error, 'Không thể gửi điểm Hội đồng.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="space-y-5 pb-24 xl:pb-10">
      {activeGroup && <GradingBackLink onClick={() => requestNavigation('list')} />}

      <PageHeader
        title={activeGroup ? 'Phiếu chấm điểm Hội đồng' : 'Chấm điểm Hội đồng'}
        description={
          activeGroup
            ? 'Nhập điểm riêng cho từng sinh viên trong đúng phiên bảo vệ theo mẫu phiếu Hội đồng.'
            : 'Chọn nhóm được phân công trong phiên bảo vệ để bắt đầu chấm điểm.'
        }
      />

      {!activeGroup && (
        <section
          className="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm"
          aria-label="Danh sách nhóm được phân công"
        >
          <ListCardHeader
            title="Nhóm được phân công"
            description={
              groups.length > 0
                ? `${groups[0].councilCode} · Thành viên: ${groups[0].memberLabel}`
                : `${activeBatch?.batchName ?? 'Đợt KLTN hiện hành'} · Lịch bảo vệ được phân công`
            }
          >
            <StatisticsBar
              items={[
                {
                  key: 'total',
                  label: 'Tổng nhóm',
                  value: groups.length,
                  icon: <Layers />,
                  tone: 'muted',
                  active: statusFilter === 'all',
                  onClick: () => changeStatus('all'),
                },
                {
                  key: 'empty',
                  label: 'Chưa nhập điểm',
                  value: counts.empty,
                  icon: <Clock3 />,
                  tone: 'warning',
                  active: statusFilter === 'empty',
                  onClick: () => changeStatus('empty'),
                },
                {
                  key: 'progress',
                  label: 'Đang chấm',
                  value: counts.progress,
                  icon: <FilePenLine />,
                  tone: 'primary',
                  active: statusFilter === 'progress',
                  onClick: () => changeStatus('progress'),
                },
                {
                  key: 'complete',
                  label: 'Đã nhập đủ',
                  value: counts.complete,
                  icon: <CheckCircle2 />,
                  tone: 'success',
                  active: statusFilter === 'complete',
                  onClick: () => changeStatus('complete'),
                },
              ]}
            />
          </ListCardHeader>
          <div className="flex flex-wrap items-center gap-3 border-b border-slate-100 bg-slate-50/45 px-5 py-3.5">
            <div className="relative min-w-[230px] flex-1">
              <Input
                id="council-grading-search"
                aria-label="Tìm theo đề tài, sinh viên hoặc phòng bảo vệ"
                leftIcon={<Search />}
                value={search}
                onChange={(event) => {
                  setSearch(event.target.value);
                  setPageNumber(1);
                }}
                placeholder="Tìm theo đề tài, sinh viên hoặc MSSV..."
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
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded p-1 text-slate-500 hover:bg-slate-100"
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
                onChange={(value) => changeStatus(value)}
                options={[
                  { value: 'all', label: 'Tất cả trạng thái' },
                  { value: 'empty', label: 'Chưa nhập điểm' },
                  { value: 'progress', label: 'Đang chấm' },
                  { value: 'complete', label: 'Đã nhập đủ' },
                ]}
              />
            </div>
            <div className="w-full sm:w-36">
              <Select<'all' | CouncilGroup['session']>
                aria-label="Lọc theo buổi bảo vệ"
                leftIcon={<CalendarClock className="size-4" />}
                value={sessionFilter}
                onChange={(value) => {
                  setSessionFilter(value);
                  setPageNumber(1);
                }}
                options={[
                  { value: 'all', label: 'Tất cả buổi' },
                  { value: 'Sáng', label: 'Buổi sáng' },
                  { value: 'Chiều', label: 'Buổi chiều' },
                ]}
              />
            </div>
            <div className="w-full sm:w-52">
              <Select<GroupSort>
                aria-label="Sắp xếp nhóm"
                value={sortBy}
                onChange={(value) => {
                  setSortBy(value);
                  setPageNumber(1);
                }}
                options={[
                  { value: 'default', label: 'Thứ tự lịch bảo vệ' },
                  { value: 'title-asc', label: 'Đề tài A–Z' },
                  { value: 'title-desc', label: 'Đề tài Z–A' },
                  { value: 'session-asc', label: 'Lịch sớm nhất' },
                  { value: 'session-desc', label: 'Lịch muộn nhất' },
                  { value: 'progress-asc', label: 'Ít điểm đã nhập' },
                  { value: 'progress-desc', label: 'Nhiều điểm đã nhập' },
                ]}
              />
            </div>
          </div>
          <DataTable
            data={pageGroups}
            columns={columns}
            getRowId={(group) => group.id}
            selectedRowId={selectedRowId}
            onRowSelect={(id) => setSelectedRowId((prev) => (prev === id ? null : id))}
            onRowOpen={(group) => openGradingSheet(group.id)}
            ariaLabel="Danh sách nhóm Hội đồng được phân công"
            tableClassName="min-w-[960px]"
            sort={activeSort}
            onSortChange={updateSort}
            loading={isLoading}
            error={resolvedError ? { message: resolvedError, onRetry: handleRetryLoad } : null}
            empty={{
              title:
                search || statusFilter !== 'all' || sessionFilter !== 'all'
                  ? 'Không có nhóm phù hợp'
                  : 'Chưa có nhóm được phân công',
              description:
                search || statusFilter !== 'all' || sessionFilter !== 'all'
                  ? 'Hãy thử thay đổi từ khóa hoặc bộ lọc.'
                  : 'Tài khoản hiện chưa có lịch chấm điểm Hội đồng nào trong đợt hiện hành.',
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
            renderMobileItem={(group) => (
              <div className="space-y-3">
                <div>
                  <p className="font-semibold text-slate-900">{group.title}</p>
                  <div className="mt-2">
                    <GradingStudentsCell
                      students={group.students}
                      ariaLabel={`Thành viên nhóm ${group.title}`}
                    />
                  </div>
                  <p className="mt-2 text-xs text-slate-500">
                    {group.schedule} · {group.room}
                  </p>
                </div>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  {columns[4].cell(group, 0)}
                  {columns[5].cell(group, 0)}
                </div>
              </div>
            )}
          />
        </section>
      )}

      <TableFloatingBar
        selectedCount={selectedGroupRow && !activeGroup ? 1 : 0}
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
        title="Thay đổi chưa được gửi"
        confirmText="Rời khỏi phiếu"
        cancelText="Ở lại"
        description={
          <div className="space-y-2 text-sm text-slate-600">
            <p>
              Bạn đang có điểm hoặc nhận xét mới chỉnh sửa chưa được lưu tạm hoặc gửi vào Biên bản Hội đồng.
            </p>
            <p className="text-xs text-slate-500">
              Bạn có chắc chắn muốn rời khỏi phiếu chấm điểm này không?
            </p>
          </div>
        }
      />

      <ConfirmDialog
        open={confirmSubmitOpen}
        onClose={() => setConfirmSubmitOpen(false)}
        onConfirm={() => void handleSubmitCouncilScore()}
        isConfirming={isSubmitting}
        title="Xác nhận gửi điểm Hội đồng"
        confirmText="Gửi điểm Hội đồng"
        cancelText="Kiểm tra lại"
        description={
          activeGroup && (
            <div className="space-y-3">
              <p>
                Xác nhận gửi điểm chấm Hội đồng cho đề tài <strong>{activeGroup.title}</strong>:
              </p>
              <div className="rounded-xl border border-slate-200 bg-slate-50 p-3.5 space-y-2">
                {progress.map((student) => (
                  <div key={student.id} className="flex items-center justify-between text-sm">
                    <span className="font-medium text-slate-800">
                      {student.name} <span className="text-slate-500">({student.code})</span>
                    </span>
                    <span className="font-bold tabular-nums text-blue-700">
                      {student.total.toFixed(2)} / 10
                    </span>
                  </div>
                ))}
              </div>
              <p className="text-xs text-slate-500">
                Điểm của từng sinh viên sẽ được cập nhật vào Biên bản Hội đồng bảo vệ KLTN.
              </p>
            </div>
          )
        }
      />

      {activeGroup && (
        <>
          <section className="rounded-2xl border border-blue-200 bg-blue-50/60 px-5 py-5 sm:px-6">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-xs font-medium text-slate-600">
                  <span className="font-semibold text-blue-700">{activeGroup.councilCode}</span> ·{' '}
                  {activeGroup.memberLabel}
                </p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">{activeGroup.title}</h2>
                <div className="mt-3 flex flex-wrap gap-x-5 gap-y-2 text-sm text-slate-700">
                  <span className="inline-flex items-center gap-1.5">
                    <CalendarClock className="size-4 text-slate-500" />
                    {activeGroup.schedule}
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <MapPin className="size-4 text-slate-500" />
                    {activeGroup.room}
                  </span>
                </div>
                <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1">
                  {activeGroup.students.map((student, idx) => (
                    <span key={student.id} className="text-sm text-slate-700">
                      <span className="mr-1.5 text-xs font-semibold text-slate-500">
                        SV{idx + 1}:
                      </span>
                      {student.name} <span className="text-slate-500">· {student.code}</span>
                    </span>
                  ))}
                </div>
              </div>
              <div className="flex flex-wrap items-center gap-2 shrink-0">
                <span
                  className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold ring-1 ${
                    isLocked
                      ? 'bg-emerald-50 text-emerald-700 ring-emerald-200'
                      : activeGroup.isSubmitted
                        ? 'bg-blue-50 text-blue-700 ring-blue-200'
                        : 'bg-white text-slate-600 ring-slate-200'
                  }`}
                >
                  {isLocked && <Lock className="size-3.5" aria-hidden="true" />}
                  {isLocked
                    ? 'Đã khóa biên bản'
                    : activeGroup.isSubmitted
                      ? 'Đã gửi điểm Hội đồng'
                      : 'Đang chấm điểm'}
                </span>
                {activeGroup.assignmentId && (
                  <Link
                    to={`/councils/minutes?assignmentId=${activeGroup.assignmentId}`}
                    className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-primary shadow-xs transition-colors hover:border-primary/40 hover:bg-blue-50/60"
                  >
                    <FileCheck2 className="size-3.5" aria-hidden="true" />
                    Biên bản hội đồng
                  </Link>
                )}
              </div>
            </div>
          </section>

          <section className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white px-5 py-4 shadow-xs" aria-label="Báo cáo phục vụ Hội đồng">
            <div className="min-w-0">
              <p className="text-sm font-semibold text-slate-900">Báo cáo phục vụ Hội đồng</p>
              {revisedReportQuery.isLoading ? (
                <p className="mt-1 text-sm text-slate-500">Đang kiểm tra bản chỉnh sửa sau phản biện…</p>
              ) : revisedReportQuery.isError ? (
                <p className="mt-1 text-sm text-rose-700">Không thể tải thông tin báo cáo. Vui lòng thử lại.</p>
              ) : latestRevisedVersion ? (
                <p className="mt-1 truncate text-sm text-slate-600">Bản chỉnh sửa v{latestRevisedVersion.versionNo} · {latestRevisedVersion.file.fileName}</p>
              ) : (
                <p className="mt-1 text-sm text-slate-500">Nhóm chưa nộp bản chỉnh sửa sau phản biện.</p>
              )}
            </div>
            {revisedReportQuery.isError ? (
              <Button type="button" variant="outline" size="sm" onClick={() => void revisedReportQuery.refetch()}>Thử lại</Button>
            ) : latestRevisedVersion ? (
              <Button type="button" variant="outline" size="sm" onClick={() => void downloadRevisedReport(latestRevisedVersion.file.id)}>
                <Download className="mr-2 size-4" aria-hidden="true" />Tải báo cáo
              </Button>
            ) : null}
          </section>

          <div className="grid grid-cols-1 items-start gap-5 xl:grid-cols-[minmax(0,1fr)_310px]">
            <GradingRubricBoard
              idPrefix="council"
              groupTitle={activeGroup.title}
              sections={sections}
              criteriaCount={criteria.length}
              students={progress}
              scores={scores}
              isLocked={isLocked}
              onUpdateScore={updateScore}
            >
              <div className="border-t border-slate-100 p-5 sm:p-6">
                <label
                  id="council-comment-label"
                  htmlFor="council-comment"
                  className="text-sm font-semibold text-slate-900"
                >
                  Nhận xét của Hội đồng
                </label>
                <p className="mt-1 mb-3 text-sm text-slate-500">
                  Ghi nhận nội dung cần lưu ý trong phiên bảo vệ này.
                </p>
                <RichTextEditor
                  id="council-comment"
                  ariaLabelledBy="council-comment-label"
                  disabled={isLocked}
                  readOnly={isLocked}
                  value={comments[activeGroup.id] ?? ''}
                  onChange={(nextValue) =>
                    setComments((previous) => ({
                      ...previous,
                      [activeGroup.id]: nextValue,
                    }))
                  }
                  maxLength={2000}
                  placeholder="Nhập nhận xét về phần trình bày, sản phẩm hoặc phần trả lời..."
                  expandedTitle={`Nhận xét của Hội đồng (${activeGroup.councilCode})`}
                  expandedSubtitle={activeGroup.title}
                />
              </div>
            </GradingRubricBoard>

            <GradingProgressAside
              students={progress}
              criteriaCount={criteria.length}
              hasUnsavedChanges={hasUnsavedChanges && !isLocked}
              noteText={`Điểm tổng được cộng từ các tiêu chí đã nhập cho từng sinh viên. Thành viên Hội đồng cần nhập đủ ${criteria.length} tiêu chí trước khi gửi điểm vào Biên bản Hội đồng.`}
              actions={
                isLocked ? (
                  <div className="flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-3.5 py-2.5 text-xs font-medium text-emerald-800">
                    <Lock className="size-4 shrink-0" />
                    <span>Biên bản Hội đồng đã khóa, không thể chỉnh sửa điểm.</span>
                  </div>
                ) : (
                  <div className="grid grid-cols-2 gap-2">
                    <Button
                      type="button"
                      variant="outline"
                      disabled={isSubmitting}
                      onClick={handleSaveTempDraft}
                      className="w-full gap-1.5 rounded-xl text-xs font-semibold"
                    >
                      <Save className="size-3.5" aria-hidden="true" />
                      Lưu nháp
                    </Button>
                    <Button
                      type="button"
                      variant="default"
                      disabled={!allStudentsComplete || isSubmitting}
                      onClick={() => setConfirmSubmitOpen(true)}
                      className="w-full gap-1.5 rounded-xl text-xs font-semibold"
                    >
                      <Send className="size-3.5" aria-hidden="true" />
                      {activeGroup.isSubmitted ? 'Cập nhật điểm' : 'Gửi điểm HĐ'}
                    </Button>
                  </div>
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
                  disabled={isSubmitting}
                  onClick={handleSaveTempDraft}
                  className="gap-1.5 rounded-xl text-xs font-semibold"
                >
                  <Save className="size-3.5" aria-hidden="true" />
                  Lưu nháp
                </Button>
                <Button
                  type="button"
                  variant="default"
                  size="sm"
                  disabled={!allStudentsComplete || isSubmitting}
                  onClick={() => setConfirmSubmitOpen(true)}
                  className="gap-1.5 rounded-xl text-xs font-semibold"
                >
                  <Send className="size-3.5" aria-hidden="true" />
                  {activeGroup.isSubmitted ? 'Cập nhật điểm' : 'Gửi điểm HĐ'}
                </Button>
              </>
            }
          />
        </>
      )}
    </div>
  );
}
