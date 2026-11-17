import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { getCurrentBatches } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { notify } from '@/shared/notifications';
import { councilGradingApi, type CouncilScheduleResponseDto } from '../services/councilGradingApi';
import { councilMinuteApi } from '../services/councilMinuteApi';
import type { CouncilOverviewApiItemDto } from '../types/councilApproval.types';
import type {
  CouncilDiscrepancyResponseDto,
  CouncilMinuteWorkspaceResponseDto,
  MinuteCalculationPreviewDto,
  MinuteDiscrepancySummaryDto,
  MinuteScoreItemDto,
  SecretaryMarking,
} from '../types/councilMinute.types';
import { getCouncilMinuteErrorMessage } from '../utils/councilMinuteFormatting';

export type MinuteQueueFilter =
  | 'all'
  | 'discrepancy'
  | 'ready'
  | 'locked'
  | 'unlocked';

interface DraftSnapshot {
  secretaryMarking: SecretaryMarking;
  targetComponentCode: string;
  studentFeedback: string;
  exclusionReason: string;
}

const VALID_EXCLUSION_CODES = new Set([
  'ADVISOR',
  'REVIEWER',
  'COUNCIL_CHAIRMAN',
  'COUNCIL_SECRETARY',
  'COUNCIL_MEMBER',
]);

function normalizeMarking(value: string | null | undefined): SecretaryMarking {
  if (value === 'AcceptedAsPerMinute') return 'AcceptedAsPerMinute';
  if (value === 'RejectedAsPerMinute') return 'RejectedAsPerMinute';
  return 'None';
}

function buildInitialSnapshot(workspace: CouncilMinuteWorkspaceResponseDto): DraftSnapshot {
  const marking = normalizeMarking(workspace.discrepancySummary.secretaryMarking);
  return {
    secretaryMarking: marking,
    targetComponentCode: workspace.discrepancySummary.ignoredComponentCode ?? '',
    studentFeedback: workspace.assignmentInfo.studentFeedback ?? '',
    exclusionReason: '',
  };
}

export function useCouncilMinutes() {
  const { user, hasRole, hasPermission } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedAssignmentId = searchParams.get('assignmentId');

  const batchesQuery = useAcademicBatches();
  const batches = useMemo(() => batchesQuery.data ?? [], [batchesQuery.data]);

  const [selectedBatchId, setSelectedBatchId] = useState<string>('');
  const [overviewRows, setOverviewRows] = useState<CouncilOverviewApiItemDto[]>([]);
  const [isLoadingList, setIsLoadingList] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [listReloadNonce, setListReloadNonce] = useState(0);

  const [selectedAssignmentId, setSelectedAssignmentId] = useState<string | null>(
    requestedAssignmentId,
  );
  const [pendingSwitchId, setPendingSwitchId] = useState<string | null>(null);
  const [isGroupDrawerOpen, setIsGroupDrawerOpen] = useState(false);
  const [queueSearch, setQueueSearch] = useState('');
  const [queueFilter, setQueueFilter] = useState<MinuteQueueFilter>('all');

  const [workspace, setWorkspace] = useState<CouncilMinuteWorkspaceResponseDto | null>(null);
  const [discrepancyDetail, setDiscrepancyDetail] =
    useState<CouncilDiscrepancyResponseDto | null>(null);
  const [schedule, setSchedule] = useState<CouncilScheduleResponseDto | null>(null);
  const [isLoadingWorkspace, setIsLoadingWorkspace] = useState(false);
  const [workspaceError, setWorkspaceError] = useState<string | null>(null);
  const [workspaceReloadNonce, setWorkspaceReloadNonce] = useState(0);

  const [secretaryMarking, setSecretaryMarking] = useState<SecretaryMarking>('None');
  const [targetComponentCode, setTargetComponentCode] = useState<string>('');
  const [studentFeedback, setStudentFeedback] = useState<string>('');
  const [exclusionReason, setExclusionReason] = useState<string>('');
  const [savedSnapshot, setSavedSnapshot] = useState<DraftSnapshot | null>(null);

  const [previewScoreItems, setPreviewScoreItems] = useState<MinuteScoreItemDto[] | null>(null);
  const [previewDiscrepancy, setPreviewDiscrepancy] =
    useState<MinuteDiscrepancySummaryDto | null>(null);
  const [previewCalculation, setPreviewCalculation] =
    useState<MinuteCalculationPreviewDto | null>(null);
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);

  const [isSaving, setIsSaving] = useState(false);
  const [isLockModalOpen, setIsLockModalOpen] = useState(false);
  const [isLocking, setIsLocking] = useState(false);
  const [lockModalError, setLockModalError] = useState<string | null>(null);

  const [isUnlockModalOpen, setIsUnlockModalOpen] = useState(false);
  const [isUnlocking, setIsUnlocking] = useState(false);
  const [unlockModalError, setUnlockModalError] = useState<string | null>(null);

  const [isPrintPreview, setIsPrintPreview] = useState(false);
  const previewRequestSeq = useRef(0);
  const workspaceHydratedAtRef = useRef<number>(0);
  const checkedCrossBatchAssignmentIdRef = useRef<string | null>(null);

  const isAdmin = hasRole('Admin');
  const isAcademicAffairs = hasRole('AcademicAffairs');
  const isFaculty = hasRole('Faculty');
  const isLecturer = hasRole('Lecturer');
  const isCouncilStaff = isAdmin || isAcademicAffairs || isFaculty;
  const hasUnlockPermission = hasPermission('councils.minutes.unlock');

  // Default active batch selection
  useEffect(() => {
    if (batches.length === 0 || selectedBatchId) return;
    const current = getCurrentBatches(batches);
    const defaultBatch = current[0] ?? batches[0];
    if (defaultBatch) {
      setSelectedBatchId(defaultBatch.id);
    }
  }, [batches, selectedBatchId]);

  // Sync URL query -> state when user navigates via link (depend only on URL param to avoid state bounce)
  useEffect(() => {
    if (!requestedAssignmentId) return;
    setSelectedAssignmentId((prev) =>
      prev === requestedAssignmentId ? prev : requestedAssignmentId,
    );
  }, [requestedAssignmentId]);

  // Load council overview assignments for selected batch (do not refetch when switching groups within same batch)
  useEffect(() => {
    if (!selectedBatchId) return;
    const controller = new AbortController();

    async function loadList() {
      setIsLoadingList(true);
      setListError(null);
      try {
        const rows = await councilGradingApi.getCouncilOverviewRows(
          selectedBatchId,
          controller.signal,
        );

        if (!controller.signal.aborted) {
          setOverviewRows(rows);
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          setListError(
            getCouncilMinuteErrorMessage(error, 'Không thể tải danh sách hội đồng bảo vệ.'),
          );
          setOverviewRows([]);
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoadingList(false);
        }
      }
    }

    void loadList();
    return () => controller.abort();
  }, [selectedBatchId, listReloadNonce]);

  // Fallback cross-batch lookup only when deep-linked assignmentId is missing from loaded overviewRows
  useEffect(() => {
    if (!requestedAssignmentId || !selectedBatchId || isLoadingList) return;
    if (overviewRows.some((r) => r.assignmentId === requestedAssignmentId)) {
      checkedCrossBatchAssignmentIdRef.current = requestedAssignmentId;
      return;
    }
    if (checkedCrossBatchAssignmentIdRef.current === requestedAssignmentId) return;
    checkedCrossBatchAssignmentIdRef.current = requestedAssignmentId;

    const controller = new AbortController();
    void councilGradingApi
      .getCouncilOverviewRows('all', controller.signal)
      .then((allRows) => {
        if (controller.signal.aborted) return;
        const matched = allRows.find((r) => r.assignmentId === requestedAssignmentId);
        if (!matched) return;
        setOverviewRows(allRows.filter((r) => r.batchId === matched.batchId));
        if (matched.batchId !== selectedBatchId) {
          setSelectedBatchId(matched.batchId);
        }
      })
      .catch(() => {
        // Keep current batch overview rows on cross-batch fallback failure
      });

    return () => controller.abort();
  }, [requestedAssignmentId, selectedBatchId, isLoadingList, overviewRows]);

  // Assignments accessible to current user
  const accessibleAssignments = useMemo(() => {
    const withAssignment = overviewRows.filter((row) => Boolean(row.assignmentId));
    if (isCouncilStaff) return withAssignment;
    if (!user?.id) return [];
    return withAssignment.filter(
      (row) =>
        row.chairman?.userId === user.id ||
        row.secretary?.userId === user.id ||
        row.members.some((m) => m.userId === user.id),
    );
  }, [overviewRows, isCouncilStaff, user?.id]);

  // Auto-select first accessible assignment if none selected or current not in list
  useEffect(() => {
    if (isLoadingList) return;
    if (accessibleAssignments.length === 0) {
      if (!requestedAssignmentId) {
        setSelectedAssignmentId(null);
      }
      return;
    }

    const existsInList =
      selectedAssignmentId &&
      accessibleAssignments.some((item) => item.assignmentId === selectedAssignmentId);

    if (!existsInList && !requestedAssignmentId) {
      const nextId = accessibleAssignments[0].assignmentId ?? null;
      setSelectedAssignmentId(nextId);
    }
  }, [accessibleAssignments, isLoadingList, selectedAssignmentId, requestedAssignmentId]);

  const activeOverviewRow = useMemo(
    () => accessibleAssignments.find((item) => item.assignmentId === selectedAssignmentId) ?? null,
    [accessibleAssignments, selectedAssignmentId],
  );

  // Load workspace + discrepancy + council schedule for active assignmentId
  useEffect(() => {
    if (!selectedAssignmentId) {
      setWorkspace(null);
      setDiscrepancyDetail(null);
      setSchedule(null);
      setWorkspaceError(null);
      return;
    }

    const controller = new AbortController();

    async function loadWorkspace() {
      setIsLoadingWorkspace(true);
      setWorkspaceError(null);
      setPreviewError(null);
      try {
        const ws = await councilMinuteApi.getMinuteWorkspace(
          selectedAssignmentId!,
          controller.signal,
        );

        const [discrepancy, councilSchedule] = await Promise.all([
          councilMinuteApi
            .getDiscrepancyCheck(selectedAssignmentId!, controller.signal)
            .catch(() => null),
          councilGradingApi
            .getCouncilSchedule(ws.assignmentInfo.councilId, controller.signal)
            .catch(() => null),
        ]);

        if (controller.signal.aborted) return;

        setWorkspace(ws);
        setDiscrepancyDetail(discrepancy);
        setSchedule(councilSchedule);

        const initial = buildInitialSnapshot(ws);
        workspaceHydratedAtRef.current = Date.now();
        setSecretaryMarking(initial.secretaryMarking);
        setTargetComponentCode(initial.targetComponentCode);
        setStudentFeedback(initial.studentFeedback);
        setExclusionReason(initial.exclusionReason);
        setSavedSnapshot(initial);
        setPreviewScoreItems(null);
        setPreviewDiscrepancy(null);
        setPreviewCalculation(null);
      } catch (error) {
        if (!controller.signal.aborted) {
          setWorkspace(null);
          setDiscrepancyDetail(null);
          setSchedule(null);
          setWorkspaceError(
            getCouncilMinuteErrorMessage(error, 'Không thể tải không gian làm việc biên bản.'),
          );
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoadingWorkspace(false);
        }
      }
    }

    void loadWorkspace();
    return () => controller.abort();
  }, [selectedAssignmentId, workspaceReloadNonce]);

  const effectiveScoreItems = previewScoreItems ?? workspace?.scoreItems ?? [];
  const effectiveDiscrepancy = previewDiscrepancy ?? workspace?.discrepancySummary ?? null;
  const effectiveCalculation = previewCalculation ?? workspace?.calculationPreview ?? null;

  const isLocked = workspace?.assignmentInfo.lockStatus === 'Locked';
  const councilStatus = activeOverviewRow?.status ?? schedule?.status ?? 'Active';
  const isCouncilActive = councilStatus === 'Active';

  const isCurrentUserSecretary = useMemo(() => {
    if (!user?.id) return false;
    if (activeOverviewRow?.secretary?.userId === user.id) return true;
    return Boolean(
      schedule?.members.some((m) => m.userId === user.id && m.role === 'Secretary'),
    );
  }, [user?.id, activeOverviewRow?.secretary?.userId, schedule?.members]);

  const canEditMinute =
    Boolean(workspace) &&
    !isLocked &&
    (isAdmin || isAcademicAffairs || (isLecturer && isCurrentUserSecretary));

  const canLockMinute = canEditMinute && isCouncilActive;

  const canUnlockMinute =
    Boolean(workspace) && isLocked && (isAdmin || (isFaculty && hasUnlockPermission));

  const currentGroupInSchedule = useMemo(() => {
    if (!schedule || !workspace) return null;
    return (
      schedule.groups.find(
        (g) =>
          g.assignmentId === workspace.assignmentInfo.assignmentId ||
          g.groupId === workspace.assignmentInfo.groupId,
      ) ?? null
    );
  }, [schedule, workspace]);

  const selectableExclusionTargets = useMemo(() => {
    return (workspace?.scoreItems ?? []).filter(
      (item) =>
        VALID_EXCLUSION_CODES.has(item.componentCode) &&
        item.score !== null &&
        !(item.componentCode === 'COUNCIL_MEMBER' && item.isDualRoleReviewer),
    );
  }, [workspace?.scoreItems]);

  const hasSavedExclusion = Boolean(workspace?.discrepancySummary.ignoredComponentCode);
  const changesExclusion = useMemo(() => {
    if (!workspace) return false;
    const initialCode = workspace.discrepancySummary.ignoredComponentCode ?? '';
    const nextCode = secretaryMarking === 'RejectedAsPerMinute' ? targetComponentCode : '';
    return initialCode !== nextCode;
  }, [workspace, secretaryMarking, targetComponentCode]);

  const requiresExclusionReason =
    changesExclusion || secretaryMarking === 'RejectedAsPerMinute' || (hasSavedExclusion && secretaryMarking !== 'AcceptedAsPerMinute');

  const normalizeFeedbackForCompare = (value: string) =>
    value
      .replace(/\u00a0/g, ' ')
      .replace(/\r\n/g, '\n')
      .replace(/\\([^\w\s])/g, '$1')
      .replace(/^(\s*)[-*+]\s+/gm, '$1* ')
      .replace(/\n{2,}/g, '\n')
      .trim();

  const handleChangeStudentFeedback = useCallback((nextValue: string) => {
    setStudentFeedback(nextValue);
    // Sync TipTap's initial Markdown canonicalization right after loading workspace
    if (Date.now() - workspaceHydratedAtRef.current < 350) {
      setSavedSnapshot((prev) => (prev ? { ...prev, studentFeedback: nextValue } : prev));
    }
  }, []);

  const isDirty = useMemo(() => {
    if (!savedSnapshot) return false;
    return (
      savedSnapshot.secretaryMarking !== secretaryMarking ||
      savedSnapshot.targetComponentCode !== targetComponentCode ||
      normalizeFeedbackForCompare(savedSnapshot.studentFeedback) !==
        normalizeFeedbackForCompare(studentFeedback) ||
      exclusionReason.trim() !== savedSnapshot.exclusionReason.trim()
    );
  }, [savedSnapshot, secretaryMarking, targetComponentCode, studentFeedback, exclusionReason]);

  const runLivePreview = useCallback(
    async (nextMarking: SecretaryMarking, nextTargetCode: string) => {
      if (!selectedAssignmentId) return;
      if (nextMarking === 'RejectedAsPerMinute' && !nextTargetCode) {
        setPreviewScoreItems(null);
        setPreviewDiscrepancy(null);
        setPreviewCalculation(null);
        setPreviewError(null);
        return;
      }

      const seq = ++previewRequestSeq.current;
      setIsPreviewing(true);
      setPreviewError(null);

      try {
        const res = await councilMinuteApi.previewMinute(selectedAssignmentId, {
          secretaryMarking: nextMarking,
          targetComponentCode:
            nextMarking === 'RejectedAsPerMinute' ? nextTargetCode : null,
          studentFeedback: studentFeedback.trim() || null,
        });
        if (seq !== previewRequestSeq.current) return;
        setPreviewScoreItems(res.scoreItems);
        setPreviewDiscrepancy(res.discrepancySummary);
        setPreviewCalculation(res.calculationPreview);
      } catch (error) {
        if (seq !== previewRequestSeq.current) return;
        setPreviewError(
          getCouncilMinuteErrorMessage(error, 'Không thể xem trước phép tính công thức.'),
        );
      } finally {
        if (seq === previewRequestSeq.current) {
          setIsPreviewing(false);
        }
      }
    },
    [selectedAssignmentId, studentFeedback],
  );

  const handleChangeSecretaryMarking = useCallback(
    (nextMarking: SecretaryMarking) => {
      if (!canEditMinute) return;
      setSecretaryMarking(nextMarking);
      let nextTarget = targetComponentCode;
      if (nextMarking !== 'RejectedAsPerMinute') {
        nextTarget = '';
        setTargetComponentCode('');
      } else if (!nextTarget && selectableExclusionTargets.length > 0) {
        // Pre-select nothing or keep explicit user choice unless already chosen
        nextTarget = targetComponentCode;
      }
      void runLivePreview(nextMarking, nextTarget);
    },
    [canEditMinute, targetComponentCode, selectableExclusionTargets.length, runLivePreview],
  );

  const handleChangeTargetComponent = useCallback(
    (nextCode: string) => {
      if (!canEditMinute) return;
      setSecretaryMarking('RejectedAsPerMinute');
      setTargetComponentCode(nextCode);
      void runLivePreview('RejectedAsPerMinute', nextCode);
    },
    [canEditMinute, runLivePreview],
  );

  const handleSaveMinute = useCallback(async (): Promise<boolean> => {
    if (!selectedAssignmentId || !workspace || !canEditMinute) return false;

    if (secretaryMarking === 'RejectedAsPerMinute' && !targetComponentCode) {
      notify.error('Vui lòng chọn một đầu điểm cụ thể để đánh dấu không công nhận.', {
        title: 'Thiếu đầu điểm cần loại',
      });
      return false;
    }

    const trimmedReason = exclusionReason.trim();
    if (
      (changesExclusion || secretaryMarking === 'RejectedAsPerMinute') &&
      (trimmedReason.length < 10 || trimmedReason.length > 1000)
    ) {
      notify.error(
        'Khi loại đầu điểm hoặc thay đổi cờ loại điểm, bắt buộc nhập lý do từ 10 đến 1000 ký tự.',
        {
          title: 'Thiếu lý do nội bộ',
        },
      );
      return false;
    }

    setIsSaving(true);
    try {
      const response = await councilMinuteApi.saveMinute(selectedAssignmentId, {
        secretaryMarking,
        targetComponentCode:
          secretaryMarking === 'RejectedAsPerMinute' ? targetComponentCode : null,
        studentFeedback: studentFeedback.trim() || null,
        chairmanConclusion: effectiveDiscrepancy?.chairmanConclusion ?? null,
        reason: trimmedReason || null,
      });

      setWorkspace((prev) =>
        prev
          ? {
              ...prev,
              assignmentInfo: {
                ...prev.assignmentInfo,
                lockStatus: response.lockStatus,
                studentFeedback: response.studentFeedback ?? null,
              },
              scoreItems: response.scoreItems,
              discrepancySummary: response.discrepancySummary,
              calculationPreview: response.calculationPreview,
            }
          : prev,
      );

      setPreviewScoreItems(null);
      setPreviewDiscrepancy(null);
      setPreviewCalculation(null);

      const nextSnapshot: DraftSnapshot = {
        secretaryMarking: normalizeMarking(response.discrepancySummary.secretaryMarking),
        targetComponentCode: response.discrepancySummary.ignoredComponentCode ?? '',
        studentFeedback: response.studentFeedback ?? '',
        exclusionReason: '',
      };
      setSecretaryMarking(nextSnapshot.secretaryMarking);
      setTargetComponentCode(nextSnapshot.targetComponentCode);
      setStudentFeedback(nextSnapshot.studentFeedback);
      setExclusionReason('');
      setSavedSnapshot(nextSnapshot);
      setListReloadNonce((n) => n + 1);

      notify.success('Công thức tính điểm và góp ý sinh viên đã được cập nhật.', {
        title: 'Đã lưu biên bản hội đồng',
      });
      return true;
    } catch (error) {
      notify.error(getCouncilMinuteErrorMessage(error, 'Lưu biên bản hội đồng thất bại.'), {
        title: 'Không thể lưu biên bản',
      });
      return false;
    } finally {
      setIsSaving(false);
    }
  }, [
    selectedAssignmentId,
    workspace,
    canEditMinute,
    secretaryMarking,
    targetComponentCode,
    exclusionReason,
    changesExclusion,
    studentFeedback,
    effectiveDiscrepancy?.chairmanConclusion,
  ]);

  const handleConfirmLock = useCallback(
    async (password: string) => {
      if (!selectedAssignmentId) return;
      if (!password.trim()) {
        setLockModalError('Vui lòng nhập mật khẩu hiện tại để xác thực khóa biên bản.');
        return;
      }

      setIsLocking(true);
      setLockModalError(null);
      try {
        if (isDirty) {
          const saved = await handleSaveMinute();
          if (!saved) {
            setIsLocking(false);
            return;
          }
        }

        const result = await councilMinuteApi.lockMinute(selectedAssignmentId, { password });
        setIsLockModalOpen(false);
        setWorkspaceReloadNonce((n) => n + 1);
        setListReloadNonce((n) => n + 1);
        notify.success(
          `Trạng thái hiện tại: ${result.lockStatusLabel}. Toàn bộ bảng điểm đã chuyển sang chỉ đọc.`,
          {
            title: 'Đã khóa biên bản hội đồng',
          },
        );
      } catch (error) {
        setLockModalError(
          getCouncilMinuteErrorMessage(error, 'Không thể khóa biên bản hội đồng.'),
        );
      } finally {
        setIsLocking(false);
      }
    },
    [selectedAssignmentId, isDirty, handleSaveMinute],
  );

  const handleConfirmUnlock = useCallback(
    async (overrideReason: string) => {
      if (!selectedAssignmentId) return;
      const trimmed = overrideReason.trim();
      if (trimmed.length < 10 || trimmed.length > 500) {
        setUnlockModalError('Lý do giải trình mở khóa đặc biệt phải từ 10 đến 500 ký tự.');
        return;
      }

      setIsUnlocking(true);
      setUnlockModalError(null);
      try {
        const result = await councilMinuteApi.unlockMinute(selectedAssignmentId, {
          overrideReason: trimmed,
        });
        setIsUnlockModalOpen(false);
        setWorkspaceReloadNonce((n) => n + 1);
        setListReloadNonce((n) => n + 1);
        notify.success(
          `Trạng thái mới: ${result.lockStatusLabel}. Đã ghi nhật ký kiểm toán.`,
          {
            title: 'Đã mở khóa đặc biệt biên bản',
          },
        );
      } catch (error) {
        setUnlockModalError(
          getCouncilMinuteErrorMessage(error, 'Không thể mở khóa biên bản hội đồng.'),
        );
      } finally {
        setIsUnlocking(false);
      }
    },
    [selectedAssignmentId],
  );

  const applySelectAssignment = useCallback(
    (assignmentId: string) => {
      setSelectedAssignmentId(assignmentId);
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.set('assignmentId', assignmentId);
          return next;
        },
        { replace: true },
      );
      setIsGroupDrawerOpen(false);
    },
    [setSearchParams],
  );

  const handleSelectAssignment = useCallback(
    (assignmentId: string) => {
      if (assignmentId === selectedAssignmentId) {
        setIsGroupDrawerOpen(false);
        return;
      }
      if (isDirty) {
        setPendingSwitchId(assignmentId);
        return;
      }
      applySelectAssignment(assignmentId);
    },
    [selectedAssignmentId, isDirty, applySelectAssignment],
  );

  const confirmDiscardAndSwitch = useCallback(() => {
    if (!pendingSwitchId) return;
    const nextId = pendingSwitchId;
    setPendingSwitchId(null);
    applySelectAssignment(nextId);
  }, [pendingSwitchId, applySelectAssignment]);

  const queueStats = useMemo(() => {
    return {
      all: accessibleAssignments.length,
      ready: accessibleAssignments.filter((r) => r.minuteLockStatus === 'AllScoresEntered').length,
      locked: accessibleAssignments.filter((r) => r.minuteLockStatus === 'Locked').length,
      unlocked: accessibleAssignments.filter((r) => r.minuteLockStatus === 'UnlockedByOverride')
        .length,
      open: accessibleAssignments.filter((r) => r.minuteLockStatus === 'Open').length,
    };
  }, [accessibleAssignments]);

  const filteredQueue = useMemo(() => {
    const kw = queueSearch.trim().toLowerCase();
    return accessibleAssignments.filter((item) => {
      if (queueFilter === 'ready' && item.minuteLockStatus !== 'AllScoresEntered') return false;
      if (queueFilter === 'locked' && item.minuteLockStatus !== 'Locked') return false;
      if (queueFilter === 'unlocked' && item.minuteLockStatus !== 'UnlockedByOverride') return false;
      if (queueFilter === 'discrepancy' && item.minuteLockStatus === 'Locked') return false;
      if (!kw) return true;
      return (
        item.councilCode.toLowerCase().includes(kw) ||
        (item.groupCode ?? '').toLowerCase().includes(kw) ||
        (item.topicTitle ?? '').toLowerCase().includes(kw) ||
        item.room.toLowerCase().includes(kw) ||
        (item.chairman?.fullName ?? '').toLowerCase().includes(kw) ||
        (item.secretary?.fullName ?? '').toLowerCase().includes(kw)
      );
    });
  }, [accessibleAssignments, queueFilter, queueSearch]);

  const sameCouncilAssignments = useMemo(() => {
    if (!activeOverviewRow) return accessibleAssignments.slice(0, 6);
    const same = accessibleAssignments.filter((r) => r.id === activeOverviewRow.id);
    return same.length > 0 ? same : accessibleAssignments.slice(0, 6);
  }, [accessibleAssignments, activeOverviewRow]);

  const activeBatch = useMemo(
    () => batches.find((b) => b.id === selectedBatchId) ?? null,
    [batches, selectedBatchId],
  );

  return {
    batches,
    activeBatch,
    selectedBatchId,
    setSelectedBatchId,
    isLoadingBatches: batchesQuery.isLoading,

    accessibleAssignments,
    filteredQueue,
    sameCouncilAssignments,
    activeOverviewRow,
    queueStats,
    queueSearch,
    setQueueSearch,
    queueFilter,
    setQueueFilter,
    isGroupDrawerOpen,
    setIsGroupDrawerOpen,

    isLoadingList,
    listError,
    reloadList: () => setListReloadNonce((n) => n + 1),

    selectedAssignmentId,
    handleSelectAssignment,
    pendingSwitchId,
    cancelPendingSwitch: () => setPendingSwitchId(null),
    confirmDiscardAndSwitch,

    workspace,
    discrepancyDetail,
    schedule,
    currentGroupInSchedule,
    isLoadingWorkspace,
    workspaceError,
    reloadWorkspace: () => setWorkspaceReloadNonce((n) => n + 1),

    effectiveScoreItems,
    effectiveDiscrepancy,
    effectiveCalculation,
    selectableExclusionTargets,

    secretaryMarking,
    targetComponentCode,
    studentFeedback,
    setStudentFeedback: handleChangeStudentFeedback,
    exclusionReason,
    setExclusionReason,
    requiresExclusionReason,
    changesExclusion,
    isDirty,
    isPreviewing,
    previewError,

    isLocked,
    isCouncilActive,
    isCurrentUserSecretary,
    canEditMinute,
    canLockMinute,
    canUnlockMinute,

    handleChangeSecretaryMarking,
    handleChangeTargetComponent,
    handleSaveMinute,
    isSaving,

    isLockModalOpen,
    setIsLockModalOpen: (open: boolean) => {
      setLockModalError(null);
      setIsLockModalOpen(open);
    },
    isLocking,
    lockModalError,
    handleConfirmLock,

    isUnlockModalOpen,
    setIsUnlockModalOpen: (open: boolean) => {
      setUnlockModalError(null);
      setIsUnlockModalOpen(open);
    },
    isUnlocking,
    unlockModalError,
    handleConfirmUnlock,

    isPrintPreview,
    setIsPrintPreview,
  };
}
