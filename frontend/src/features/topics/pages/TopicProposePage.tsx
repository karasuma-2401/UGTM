import { InlineAlert } from '@/components/ui/inline-alert';
import { notify } from '@/shared/notifications';
import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { useBeforeUnload, useBlocker } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  FilePlus2,
  FileText,
  Clock3,
  RotateCcw,
  Search,
  Save,
  Send,
  Eye,
  CheckCircle2,
  Circle,
  ArrowLeft,
  FunnelX,
  Info,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Dialog } from '@/components/ui/dialog';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TopicStatusBadge } from '../components/TopicStatusBadge';
import { TopicsTable } from '../components/TopicsTable';
import { TopicProposalSchedule } from '../components/TopicProposalSchedule';
import { TableActiveFilters } from '@/components/ui/table';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import { Skeleton } from '@/components/ui/skeleton';
import {
  EMPTY_PROPOSAL,
  topicToValues as toFormValues,
  copyProposal,
  validateProposal,
  readLocalProposal,
  saveLocalProposal,
  removeLocalProposal,
  sameProposal,
  sameStudentCodes,
  type LocalProposal,
  type ProposalErrors,
} from '../proposalEditing';
import { TopicPreview } from '../components/TopicPreview';
import { TopicReviewNote } from '../components/TopicReviewNote';
import {
  TopicStudentAssignmentField,
  type StudentCodeLookupState,
} from '../components/TopicStudentAssignmentField';
import { PageHeader } from '@/components/ui/page-header';
import { useAuth } from '@/shared/auth';
import {
  canSelectAcademicBatch,
  getActiveOpenBatch,
} from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';

import {
  canEditTopic,
  TOPIC_STATUS_LABELS,
  TOPIC_STATUS_HINTS,
  type Topic,
  type TopicFormValues,
  type TopicFilters,
  type TopicStatus,
  type TopicSortField,
} from '../types/topic.types';
import { getTopicErrorMessage, topicApi } from '../services/topicApi';

const INITIAL_VALUES = EMPTY_PROPOSAL;

interface SaveProposal {
  id: string | null;
  values: TopicFormValues;
  submitImmediately: boolean;
  hasChanges: boolean;
  updateAssignedStudents?: boolean;
}

type PendingConfirmation =
  | { kind: 'open'; topic: Topic | null; intent: 'edit' | 'copy' | 'submit' }
  | { kind: 'close' }
  | { kind: 'submit'; proposal: SaveProposal };

const INITIAL_FILTERS: TopicFilters = { keyword: '', batchId: '', status: '' };
const studentLookupCache = new Map<string, StudentCodeLookupState>();

export function TopicProposePage({
  pageTitle = 'Đề xuất đề tài',
  navigation,
  initialFilters = INITIAL_FILTERS,
  onFiltersChange,
}: {
  pageTitle?: string;
  navigation?: ReactNode;
  initialFilters?: TopicFilters;
  onFiltersChange?: (filters: TopicFilters) => void;
} = {}) {
  const { user, hasRole, hasPermission } = useAuth();
  const canSelectBatch = canSelectAcademicBatch(hasRole);
  const canReadBatches = hasPermission('academic.batches.read');
  const queryClient = useQueryClient();
  const [selectedTopic, setSelectedTopic] = useState<Topic | null>(null);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [values, setValues] = useState<TopicFormValues>(INITIAL_VALUES);
  const [baseline, setBaseline] = useState<TopicFormValues>(INITIAL_VALUES);
  const dirty = !sameProposal(values, baseline);
  const [notice, setNotice] = useState('');
  const [formError, setFormError] = useState('');
  const [fieldErrors, setFieldErrors] = useState<ProposalErrors>({});
  const [studentLookups, setStudentLookups] = useState<Record<string, StudentCodeLookupState>>({});
  const [focusField, setFocusField] = useState<keyof TopicFormValues | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sortBy, setSortBy] = useState<TopicSortField>('createdAt');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [pendingConfirmation, setPendingConfirmation] = useState<PendingConfirmation | null>(null);
  const [draftFilters, setDraftFilters] = useState<TopicFilters>(initialFilters);
  const [filters, setFilters] = useState<TopicFilters>(initialFilters);
  const [hasExplicitBatchChoice, setHasExplicitBatchChoice] = useState<boolean>(
    Boolean(initialFilters.batchId),
  );
  const initialFiltersKey = `${initialFilters.batchId}|${initialFilters.status}|${initialFilters.keyword}`;
  const [syncedInitialFiltersKey, setSyncedInitialFiltersKey] = useState(initialFiltersKey);
  if (initialFiltersKey !== syncedInitialFiltersKey) {
    setSyncedInitialFiltersKey(initialFiltersKey);
    setFilters(initialFilters);
    setDraftFilters(initialFilters);
    if (initialFilters.batchId) {
      setHasExplicitBatchChoice(true);
    }
    setPageNumber(1);
    setSelectedRowId(null);
  }
  const [recovery, setRecovery] = useState<LocalProposal | null>(null);
  const [localSavedAt, setLocalSavedAt] = useState('');
  const [localError, setLocalError] = useState('');
  const [newLocalDraft, setNewLocalDraft] = useState<{
    userId: string;
    draft: LocalProposal;
  } | null>(() => {
    if (!user?.id) return null;
    try {
      const draft = readLocalProposal(user.id, null);
      return draft ? { userId: user.id, draft } : null;
    } catch {
      return null;
    }
  });
  const availableLocalDraft = newLocalDraft?.userId === user?.id ? newLocalDraft?.draft : null;
  const editable = !selectedTopic || canEditTopic(selectedTopic.status);
  const hasUnsavedChanges = isFormOpen && editable && dirty;
  const ownerId = user?.id;
  const editingTopicId = selectedTopic?.id ?? null;
  const persistDraft = useCallback(() => {
    if (!ownerId || !isFormOpen || !editable || !dirty || recovery) return;
    try {
      const draft = saveLocalProposal(ownerId, editingTopicId, values, baseline);
      setLocalSavedAt(draft.savedAt);
      setLocalError('');
      if (!editingTopicId) setNewLocalDraft({ userId: ownerId, draft });
    } catch {
      setLocalError('Chưa thể lưu tạm trên thiết bị. Hãy dùng nút Lưu nháp để lưu lên hệ thống.');
    }
  }, [ownerId, editingTopicId, isFormOpen, editable, dirty, recovery, values, baseline]);
  const blocker = useBlocker(hasUnsavedChanges);
  useBeforeUnload((event) => {
    if (!hasUnsavedChanges) return;
    persistDraft();
    event.preventDefault();
    event.returnValue = '';
  });

  useEffect(() => {
    if (!hasUnsavedChanges || recovery) return;
    const timer = window.setTimeout(persistDraft, 600);
    return () => window.clearTimeout(timer);
  }, [hasUnsavedChanges, recovery, persistDraft]);

  useEffect(() => {
    if (!focusField || !isFormOpen || isPreviewOpen || recovery) return;
    const ids: Record<keyof TopicFormValues, string> = {
      batchId: 'topic-batch-field',
      titleVi: 'topic-title-vi',
      titleEn: 'topic-title-en',
      description: 'topic-description',
      requirements: 'topic-requirements',
      studentNumber: 'topic-capacity-field',
      studentCodes: 'topic-student-codes',
    };
    const frame = window.requestAnimationFrame(() => {
      const container = document.getElementById(ids[focusField]);
      const target = container?.matches('input, textarea, [contenteditable="true"]')
        ? container
        : container?.querySelector('input, [contenteditable="true"], button');
      if (target instanceof HTMLElement) {
        target.focus();
        target.scrollIntoView({ block: 'center', behavior: 'smooth' });
      }
      setFocusField(null);
    });
    return () => window.cancelAnimationFrame(frame);
  }, [focusField, isFormOpen, isPreviewOpen, recovery]);

  const studentCodesKey = Array.from(
    new Set(
      (values.studentCodes ?? [])
        .map((code) => code.trim().toUpperCase())
        .filter(Boolean),
    ),
  ).join('|');
  const knownStudentCodesKey = (selectedTopic?.group?.students ?? [])
    .map((student) => student.userCode?.trim().toUpperCase())
    .filter((code): code is string => Boolean(code))
    .join('|');

  const lookupBatchId = selectedTopic?.batchId ?? values.batchId;
  const lookupExcludeTopicId = selectedTopic?.id;

  useEffect(() => {
    if (
      !isFormOpen ||
      !editable ||
      Boolean(recovery) ||
      !studentCodesKey ||
      typeof topicApi.lookupStudentByCode !== 'function'
    ) {
      return;
    }

    const knownCodes = new Set(knownStudentCodesKey ? knownStudentCodesKey.split('|') : []);
    const cacheKeyFor = (code: string) =>
      `${lookupBatchId || ''}|${lookupExcludeTopicId || ''}|${code}`;
    const pendingCodes = studentCodesKey
      .split('|')
      .filter((code) => !knownCodes.has(code) && !studentLookupCache.has(cacheKeyFor(code)));

    if (pendingCodes.length === 0) return;

    let cancelled = false;
    const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
    const timer = window.setTimeout(() => {
      setStudentLookups((prev) => {
        const next = { ...prev };
        for (const code of pendingCodes) {
          if (!studentLookupCache.has(cacheKeyFor(code))) {
            next[code] = { status: 'loading' };
          }
        }
        return next;
      });

      void Promise.all(
        pendingCodes.map(async (code) => {
          try {
            const match = await topicApi.lookupStudentByCode(code, {
              batchId: lookupBatchId || undefined,
              excludeTopicId: lookupExcludeTopicId,
              signal: controller?.signal,
            });
            if (cancelled || controller?.signal.aborted) return;
            const resolved: StudentCodeLookupState = match
              ? {
                  status: 'found',
                  fullName: match.fullName,
                  isAlreadyRegisteredInBatch: Boolean(match.isAlreadyRegisteredInBatch),
                  isRegistrationBlocked: Boolean(match.isRegistrationBlocked),
                }
              : { status: 'not_found' };
            studentLookupCache.set(cacheKeyFor(code), resolved);
            setStudentLookups((prev) => ({
              ...prev,
              [code]: resolved,
            }));
          } catch {
            if (cancelled || controller?.signal.aborted) return;
            setStudentLookups((prev) => ({
              ...prev,
              [code]: { status: 'error' },
            }));
          }
        }),
      );
    }, 350);

    return () => {
      cancelled = true;
      controller?.abort();
      window.clearTimeout(timer);
    };
  }, [
    isFormOpen,
    editable,
    recovery,
    studentCodesKey,
    knownStudentCodesKey,
    lookupBatchId,
    lookupExcludeTopicId,
  ]);

  const checklist = [
    { label: 'Đã chọn đợt khóa luận', complete: Boolean(values.batchId) },
    {
      label: 'Tên tiếng Việt hợp lệ (tối đa 300 ký tự)',
      complete: Boolean(values.titleVi.trim()) && values.titleVi.trim().length <= 300,
    },
    {
      label: 'Chỉ tiêu 1 hoặc 2 sinh viên',
      complete: values.studentNumber === 1 || values.studentNumber === 2,
    },
    {
      label: 'Tên tiếng Anh hợp lệ (tối đa 350 ký tự)',
      complete: Boolean(values.titleEn.trim()) && values.titleEn.trim().length <= 350,
    },
    { label: 'Trình bày mục tiêu và mô tả', complete: Boolean(values.description.trim()) },
    {
      label: 'Bổ sung yêu cầu công nghệ, kiến thức',
      complete: Boolean(values.requirements.trim()),
    },
  ];
  const completedChecklistCount = checklist.filter((item) => item.complete).length;
  const isChecklistComplete = completedChecklistCount === checklist.length;
  const assignedStudentCount =
    selectedTopic?.group?.students?.length ??
    (values.studentCodes?.filter((code) => code.trim()).length || 0);

  const batchesQuery = useAcademicBatches();
  const batches = batchesQuery.data ?? [];
  const openBatch = getActiveOpenBatch(batches);
  const effectiveBatchId = canSelectBatch
    ? hasExplicitBatchChoice
      ? filters.batchId
      : (openBatch?.id ?? '')
    : (openBatch?.id ?? '');
  const effectiveDraftBatchId = canSelectBatch
    ? hasExplicitBatchChoice
      ? draftFilters.batchId
      : (openBatch?.id ?? '')
    : (openBatch?.id ?? '');
  const effectiveFilters: TopicFilters = {
    ...filters,
    batchId: effectiveBatchId,
  };
  const hasFilters = Boolean(
    filters.keyword ||
      draftFilters.keyword ||
      filters.status ||
      draftFilters.status ||
      (canSelectBatch && (effectiveBatchId || effectiveDraftBatchId)),
  );
  const hasActiveBatchScope = canSelectBatch || Boolean(openBatch?.id);
  const topicsQuery = useQuery({
    queryKey: [
      'my-proposed-topics',
      user?.id,
      effectiveFilters,
      pageNumber,
      pageSize,
      sortBy,
      sortOrder,
    ],
    enabled: Boolean(user?.id) && hasActiveBatchScope,
    queryFn: ({ signal }) =>
      topicApi.getMyTopics(
        { ...effectiveFilters, advisorId: user!.id, pageNumber, pageSize, sortBy, sortOrder },
        signal,
      ),
    placeholderData: (previousData, previousQuery) =>
      previousQuery?.queryKey[1] === user?.id ? previousData : undefined,
  });
  const statisticsQuery = useQuery({
    queryKey: ['my-proposed-topics', user?.id, 'statistics', effectiveBatchId],
    enabled: Boolean(user?.id) && hasActiveBatchScope,
    queryFn: ({ signal }) => topicApi.getStatistics(user!.id, effectiveBatchId || undefined, signal),
    placeholderData: (previousData, previousQuery) =>
      previousQuery?.queryKey[1] === user?.id ? previousData : undefined,
  });
  const statistics =
    !hasActiveBatchScope || statisticsQuery.isError ? undefined : statisticsQuery.data;
  const formatCount = (count: number | undefined) =>
    count === undefined ? '—' : count.toLocaleString('vi-VN');

  const detailMutation = useMutation({
    mutationFn: ({ id }: { id: string; intent: 'edit' | 'copy' | 'submit' }) =>
      topicApi.getDetail(id),
    onSuccess: async (topic, { intent }) => {
      const fresh = toFormValues(topic);
      if (intent === 'copy') {
        const nextBatchId = canSelectBatch ? '' : (openBatch?.id ?? '');
        beginEditor(
          null,
          { ...copyProposal(topic), batchId: nextBatchId },
          { ...INITIAL_VALUES, batchId: nextBatchId },
        );
        setNotice(
          canSelectBatch
            ? 'Đã nhân bản nội dung. Chọn đợt và kiểm tra tên trước khi lưu đề xuất mới.'
            : 'Đã nhân bản nội dung cho đợt KLTN đang mở hiện tại. Kiểm tra tên trước khi lưu đề xuất mới.',
        );
      } else if (intent === 'submit') {
        if (!canEditTopic(topic.status)) {
          setNotice('Trạng thái đề tài đã thay đổi. Đề tài hiện không thể gửi duyệt.');
          await queryClient.invalidateQueries({ queryKey: ['my-proposed-topics'] });
          return;
        }
        const cached = loadRecovery(topic.id, fresh);
        const errors = validateProposal(fresh, true);
        if (cached || Object.keys(errors).length) {
          beginEditor(topic, fresh);
          if (cached)
            setFormError(
              'Có bản chỉnh sửa trên thiết bị. Hãy khôi phục hoặc bỏ bản lưu tạm trước khi gửi.',
            );
          else showValidation(errors);
          return;
        }
        setPendingConfirmation({
          kind: 'submit',
          proposal: {
            id: topic.id,
            values: fresh,
            submitImmediately: true,
            hasChanges: topic.studentNumber !== fresh.studentNumber,
          },
        });
      } else beginEditor(topic, fresh);
    },
  });

  const saveMutation = useMutation({
    mutationFn: ({
      id,
      values,
      submitImmediately,
      hasChanges,
      updateAssignedStudents,
    }: SaveProposal) => {
      if (id && submitImmediately && !hasChanges) return topicApi.submit(id);
      if (updateAssignedStudents) return topicApi.save(id, values, submitImmediately, true);
      return topicApi.save(id, values, submitImmediately);
    },
    onSuccess: async (topic, { submitImmediately, id }) => {
      clearCachedDraft(id);
      setSelectedTopic(topic);
      setValues(toFormValues(topic));
      setBaseline(toFormValues(topic));
      setFormError('');
      setFieldErrors({});
      setIsFormOpen(false);
      setIsPreviewOpen(false);
      setRecovery(null);
      setLocalSavedAt('');
      notify.success(
        submitImmediately
          ? 'Đã gửi đề tài đến Khoa để thẩm định.'
          : topic.status === 'NeedRevision'
            ? 'Đã lưu chỉnh sửa trên hệ thống. Bạn có thể gửi duyệt lại khi sẵn sàng.'
            : 'Đã lưu bản nháp đề tài trên hệ thống.',
      );
      setPageNumber(1);
      setSelectedRowId(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['my-proposed-topics'] }),
        queryClient.invalidateQueries({ queryKey: ['topic-review'] }),
        queryClient.invalidateQueries({ queryKey: ['topic-advising'] }),
        queryClient.invalidateQueries({ queryKey: ['topic-advising-group-detail'] }),
      ]);
    },
  });

  const busy = saveMutation.isPending || detailMutation.isPending;
  const selectedBatch = batches.find((batch) => batch.id === values.batchId);
  const filterBatch = batches.find((batch) => batch.id === effectiveBatchId);
  const batchesLoading = canReadBatches && (batchesQuery.isPending || batchesQuery.isFetching);
  const editorBatches =
    !canSelectBatch && !selectedTopic ? (openBatch ? [openBatch] : []) : batches;

  function clearCachedDraft(id: string | null) {
    if (!user?.id) return;
    try {
      removeLocalProposal(user.id, id);
      if (!id) setNewLocalDraft(null);
      setLocalError('');
    } catch {
      setLocalError('Chưa thể xóa bản lưu tạm trên thiết bị.');
    }
  }
  function loadRecovery(id: string | null, initial: TopicFormValues): LocalProposal | null {
    if (!user?.id) return null;
    try {
      const draft = readLocalProposal(user.id, id);
      return draft && !sameProposal(draft.values, initial) ? draft : null;
    } catch {
      setLocalError('Không thể đọc bản lưu tạm trên thiết bị.');
      return null;
    }
  }
  function beginEditor(topic: Topic | null, initial: TopicFormValues, initialBaseline = initial) {
    studentLookupCache.clear();
    setStudentLookups({});
    setSelectedTopic(topic);
    setValues(initial);
    setBaseline(initialBaseline);
    setFieldErrors({});
    setFormError('');
    setFocusField(null);
    setLocalSavedAt('');
    setRecovery(
      !topic || canEditTopic(topic.status)
        ? loadRecovery(topic?.id ?? null, initialBaseline)
        : null,
    );
    setIsPreviewOpen(false);
    setIsFormOpen(true);
  }
  function restoreRecovery() {
    if (!recovery) return;
    setValues({
      ...recovery.values,
      batchId:
        selectedTopic?.batchId ??
        (canSelectBatch ? recovery.values.batchId : (openBatch?.id ?? '')),
      // Drafts saved before assignment support must not clear the server roster.
      studentCodes: recovery.values.studentCodes ?? baseline.studentCodes ?? [],
    });
    setLocalSavedAt(recovery.savedAt);
    setRecovery(null);
    setFormError('');
    setFieldErrors({});
  }
  function restoreNewDraft() {
    if (!user?.id || !availableLocalDraft || busy) return;
    const lockedBatchId = canSelectBatch ? availableLocalDraft.values.batchId : (openBatch?.id ?? '');
    beginEditor(
      null,
      { ...availableLocalDraft.values, batchId: lockedBatchId },
      { ...INITIAL_VALUES, batchId: canSelectBatch ? '' : (openBatch?.id ?? '') },
    );
    setRecovery(null);
    setLocalSavedAt(availableLocalDraft.savedAt);
  }
  function discardRecovery() {
    clearCachedDraft(selectedTopic?.id ?? null);
    setRecovery(null);
    setFormError('');
  }
  function updateField<K extends keyof TopicFormValues>(key: K, value: TopicFormValues[K]) {
    const next = { ...values, [key]: value };
    setValues(next);
    if (!recovery && sameProposal(next, baseline)) {
      clearCachedDraft(selectedTopic?.id ?? null);
      setLocalSavedAt('');
    }
    setFieldErrors((previous) => {
      const next = { ...previous };
      delete next[key];
      if (key === 'studentNumber') delete next.studentCodes;
      return next;
    });
    setNotice('');
    setFormError('');
    saveMutation.reset();
    detailMutation.reset();
  }
  function openTopic(topic: Topic | null, intent: 'edit' | 'copy' | 'submit' = 'edit') {
    if (busy) return;
    if (hasUnsavedChanges) {
      setPendingConfirmation({ kind: 'open', topic, intent });
      return;
    }
    continueOpenTopic(topic, intent);
  }
  function continueOpenTopic(topic: Topic | null, intent: 'edit' | 'copy' | 'submit') {
    setNotice('');
    setFormError('');
    setFieldErrors({});
    saveMutation.reset();
    detailMutation.reset();
    if (topic) detailMutation.mutate({ id: topic.id, intent });
    else beginEditor(null, { ...INITIAL_VALUES, batchId: effectiveBatchId });
  }
  function closeForm() {
    if (busy) return;
    if (blocker.state === 'blocked') {
      blocker.reset();
      return;
    }
    if (isPreviewOpen) {
      setIsPreviewOpen(false);
      return;
    }
    if (hasUnsavedChanges) {
      setPendingConfirmation({ kind: 'close' });
      return;
    }
    finishCloseForm();
  }
  function finishCloseForm() {
    if (dirty && !recovery) clearCachedDraft(selectedTopic?.id ?? null);
    setValues(baseline);
    setIsFormOpen(false);
    setRecovery(null);
  }
  function leaveWithoutChanges() {
    if (blocker.state !== 'blocked') return;
    if (!recovery) clearCachedDraft(selectedTopic?.id ?? null);
    setBaseline(values);
    blocker.proceed();
  }
  function showValidation(errors: ProposalErrors) {
    setFieldErrors(errors);
    setFormError(`Vui lòng kiểm tra các trường: ${Object.values(errors).join(' ')}`);
    setFocusField(Object.keys(errors)[0] as keyof TopicFormValues);
  }
  function confirmPendingAction() {
    if (!pendingConfirmation || busy) return;
    const action = pendingConfirmation;
    setPendingConfirmation(null);
    if (action.kind === 'open') continueOpenTopic(action.topic, action.intent);
    else if (action.kind === 'close') finishCloseForm();
    else saveMutation.mutate(action.proposal);
  }
  function quickFilter(status: TopicStatus | '') {
    const next = { keyword: '', batchId: effectiveBatchId, status };
    setFilters(next);
    setDraftFilters(next);
    setPageNumber(1);
    setSelectedRowId(null);
    onFiltersChange?.(next);
  }
  function applyFiltersNow(nextDraft: TopicFilters) {
    const next = { ...nextDraft, keyword: nextDraft.keyword.trim() };
    setDraftFilters(nextDraft);
    setFilters(next);
    setPageNumber(1);
    setSelectedRowId(null);
    onFiltersChange?.(next);
  }
  function handleApplyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    applyFiltersNow({ ...draftFilters, batchId: effectiveDraftBatchId });
  }
  function clearFilters() {
    setHasExplicitBatchChoice(true);
    setDraftFilters({ ...INITIAL_FILTERS });
    setFilters({ ...INITIAL_FILTERS });
    setPageNumber(1);
    setSelectedRowId(null);
    onFiltersChange?.({ ...INITIAL_FILTERS });
  }
  function removeFilter(key: keyof TopicFilters) {
    if (key === 'batchId') {
      setHasExplicitBatchChoice(true);
    }
    const next = { ...effectiveFilters, [key]: '' };
    setDraftFilters(next);
    setFilters(next);
    setPageNumber(1);
    setSelectedRowId(null);
    onFiltersChange?.(next);
  }
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editable || busy || recovery) return;
    const submitImmediately =
      (event.nativeEvent as SubmitEvent).submitter?.getAttribute('value') === 'submit';
    const errors = validateProposal(values, submitImmediately);
    const updateAssignedStudents =
      Boolean(selectedTopic) && !sameStudentCodes(values.studentCodes, baseline.studentCodes);
    if (
      updateAssignedStudents &&
      selectedTopic?.group?.students.some((student) => !student.userCode?.trim())
    ) {
      errors.studentCodes = 'Chưa có đủ MSSV để thay đổi danh sách sinh viên hiện tại.';
    }
    // The existing backend validates assignments against the persisted quota
    // before updating topic details. Avoid submitting a combined request it rejects.
    if (
      updateAssignedStudents &&
      selectedTopic?.studentNumber &&
      (values.studentCodes ?? []).filter((code) => code.trim()).length >
        selectedTopic.studentNumber &&
      values.studentNumber > selectedTopic.studentNumber
    ) {
      errors.studentCodes = 'Hãy lưu chỉ tiêu mới trước, sau đó chỉ định thêm sinh viên.';
    }
    if (!errors.studentCodes && (!selectedTopic || updateAssignedStudents)) {
      const knownCodes = new Set(knownStudentCodesKey ? knownStudentCodesKey.split('|') : []);
      const normalizedCodes = (values.studentCodes ?? [])
        .map((code) => code.trim().toUpperCase())
        .filter((code) => Boolean(code) && !knownCodes.has(code));
      const conflictCode = normalizedCodes.find((code) => {
        const lookup = studentLookups[code];
        return lookup?.status === 'found' && lookup.isAlreadyRegisteredInBatch;
      });
      if (conflictCode) {
        errors.studentCodes = `Sinh viên ${conflictCode} đã đăng ký hoặc được gán vào đề tài khác trong đợt KLTN này.`;
      } else {
        const blockedCode = normalizedCodes.find((code) => {
          const lookup = studentLookups[code];
          return lookup?.status === 'found' && lookup.isRegistrationBlocked;
        });
        if (blockedCode) {
          errors.studentCodes = `Sinh viên ${blockedCode} đang bị khóa quyền đăng ký đề tài.`;
        }
      }
    }
    if (Object.keys(errors).length) {
      showValidation(errors);
      return;
    }
    const proposal: SaveProposal = {
      id: selectedTopic?.id ?? null,
      values: { ...values },
      submitImmediately,
      hasChanges:
        dirty || Boolean(selectedTopic && selectedTopic.studentNumber !== values.studentNumber),
      ...(updateAssignedStudents ? { updateAssignedStudents: true } : {}),
    };
    if (submitImmediately) {
      setPendingConfirmation({ kind: 'submit', proposal });
      return;
    }
    setNotice('');
    setFormError('');
    setFieldErrors({});
    saveMutation.mutate(proposal);
  }

  return (
    <div className="space-y-6 pb-12 animate-in fade-in-50 duration-300">
      <PageHeader
        compact
        accent
        title={pageTitle}
        description="Quản lý danh sách đề tài đề xuất, theo dõi nhóm sinh viên đăng ký và tiến độ hướng dẫn khóa luận tốt nghiệp"
        navigation={navigation}
      >
        <Button
          type="button"
          leftIcon={<FilePlus2 />}
          disabled={busy}
          onClick={() => openTopic(null)}
          className="h-10 rounded-xl px-4 font-semibold shadow-xs"
        >
          Đề xuất đề tài
        </Button>
      </PageHeader>
      {availableLocalDraft && !isFormOpen && (
        <div
          role="status"
          className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-tone-warning-border bg-tone-warning-bg p-4 text-sm text-tone-warning-strong"
        >
          <div>
            <p className="font-medium">Có đề xuất đang soạn được lưu trên thiết bị</p>
            <p className="mt-1 text-xs text-tone-warning-strong">
              Lưu lúc {new Date(availableLocalDraft.savedAt).toLocaleString('vi-VN')}. Nội dung này
              chưa được lưu lên hệ thống.
            </p>
          </div>
          <div className="flex gap-2">
            <Button type="button" variant="outline" disabled={busy} onClick={restoreNewDraft}>
              Khôi phục nội dung
            </Button>
            <Button
              type="button"
              variant="ghost"
              disabled={busy}
              onClick={() => clearCachedDraft(null)}
            >
              Bỏ bản lưu tạm
            </Button>
          </div>
        </div>
      )}
      {localError && !isFormOpen && <InlineAlert tone="error">{localError}</InlineAlert>}
      {notice && <InlineAlert tone="info">{notice}</InlineAlert>}
      {detailMutation.isPending && (
        <p role="status" className="text-sm text-ink-muted">
          Đang tải nội dung đề tài...
        </p>
      )}
      {detailMutation.isError && (
        <InlineAlert tone="error">{getTopicErrorMessage(detailMutation.error)}</InlineAlert>
      )}

      {isFormOpen && (
        <Dialog
          title={
            isPreviewOpen
              ? 'Xem trước đề tài'
              : selectedTopic
                ? editable
                  ? 'Chỉnh sửa đề tài'
                  : 'Chi tiết đề tài'
                : 'Đề xuất đề tài'
          }
          busy={busy}
          onClose={closeForm}
          className={isPreviewOpen ? 'max-w-4xl' : 'max-w-5xl'}
          headerClassName="border-b border-line/80 px-5 py-3.5 sm:px-6"
        >
          {isPreviewOpen ? (
            <>
              <TopicPreview
                reviewNote={selectedTopic?.reviewNote}
                values={values}
                advisorName={user?.fullName ?? ''}
                batchName={
                  selectedBatch?.batchName ||
                  (values.batchId ? `Mã đợt: ${values.batchId}` : undefined)
                }
                batchLoading={Boolean(values.batchId) && !selectedBatch && batchesLoading}
              />
              <div className="sticky bottom-0 z-10 flex justify-end border-t border-line/80 bg-surface-card/95 px-5 py-3.5 backdrop-blur-md sm:px-6">
                <Button
                  type="button"
                  variant="outline"
                  leftIcon={<ArrowLeft />}
                  onClick={() => setIsPreviewOpen(false)}
                  className="h-9 rounded-xl border-line-input px-4 text-xs font-semibold"
                >
                  {editable ? 'Quay lại chỉnh sửa' : 'Quay lại chi tiết'}
                </Button>
              </div>
            </>
          ) : (
            <form onSubmit={handleSubmit} noValidate>
              <div className="grid grid-cols-1 lg:grid-cols-12">
                <div className="space-y-5 bg-surface-card p-5 sm:p-6 lg:col-span-8">
                  {selectedTopic?.reviewNote && <TopicReviewNote note={selectedTopic.reviewNote} />}

                  {recovery && (
                    <div
                      role="status"
                      className="space-y-3 rounded-xl border border-tone-warning-border bg-tone-warning-bg p-3.5 text-sm text-tone-warning-strong"
                    >
                      <p className="font-semibold">Có nội dung chưa lưu lên hệ thống</p>
                      <p className="text-xs leading-relaxed text-tone-warning-strong">
                        Bản lưu trên thiết bị lúc{' '}
                        {new Date(recovery.savedAt).toLocaleString('vi-VN')}. Chọn khôi phục hoặc bỏ
                        bản lưu tạm để tiếp tục.
                      </p>
                      {selectedTopic && !sameProposal(recovery.baseline, baseline) && (
                        <p className="text-xs font-medium">
                          Nội dung trên hệ thống đã thay đổi từ lần lưu tạm. Hãy kiểm tra kỹ nội
                          dung sau khi khôi phục.
                        </p>
                      )}
                      <div className="flex flex-wrap gap-2">
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={restoreRecovery}
                          className="h-8 rounded-lg border-tone-warning-border bg-surface-card text-xs font-semibold text-tone-warning-strong hover:bg-tone-warning-bg"
                        >
                          Khôi phục nội dung
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          variant="ghost"
                          onClick={discardRecovery}
                          className="h-8 rounded-lg text-xs font-medium text-tone-warning-strong hover:bg-tone-warning-bg"
                        >
                          Bỏ bản lưu tạm
                        </Button>
                      </div>
                    </div>
                  )}

                  <div
                    className={`flex items-start gap-2.5 rounded-xl border px-3.5 py-2.5 text-xs leading-relaxed ${
                      selectedTopic?.status === 'NeedRevision'
                        ? 'border-tone-warning-border bg-tone-warning-bg text-tone-warning-strong'
                        : 'border-line bg-surface-subtle/80 text-ink-body'
                    }`}
                  >
                    <Info
                      className={`mt-0.5 size-4 shrink-0 ${
                        selectedTopic?.status === 'NeedRevision' ? 'text-tone-warning-icon' : 'text-primary'
                      }`}
                      aria-hidden="true"
                    />
                    <p>{TOPIC_STATUS_HINTS[selectedTopic?.status ?? 'Draft']}</p>
                  </div>

                  {localError && (
                    <p
                      role="alert"
                      className="rounded-xl border border-tone-warning-border bg-tone-warning-bg px-3.5 py-2.5 text-xs font-medium text-tone-warning-strong"
                    >
                      {localError}
                    </p>
                  )}

                  <fieldset
                    disabled={busy || !editable || Boolean(recovery)}
                    className="min-w-0 space-y-5"
                  >
                    <div className="space-y-4">
                      <div className="space-y-1.5">
                        <label
                          id="topic-batch-label"
                          className="block text-xs font-semibold text-ink-secondary"
                        >
                          Đợt khóa luận <span className="text-tone-danger-icon">*</span>
                        </label>
                        <Select<string>
                          id="topic-batch-field"
                          error={fieldErrors.batchId}
                          triggerClassName="h-10 rounded-xl bg-surface-subtle/60 shadow-none focus-visible:bg-surface-card"
                          aria-labelledby="topic-batch-label"
                          value={values.batchId}
                          options={editorBatches.map((batch) => ({
                            value: batch.id,
                            label: batch.batchName,
                            description: `${batch.academicYear} - HK${batch.semester}`,
                          }))}
                          onChange={(value: string) => updateField('batchId', value)}
                          placeholder={
                            !canReadBatches
                              ? 'Chưa có quyền xem đợt KLTN'
                              : batchesQuery.isPending
                                ? 'Đang tải danh sách đợt...'
                                : !canSelectBatch && !selectedTopic && !openBatch
                                  ? 'Hiện chưa có đợt KLTN nào đang mở'
                                  : 'Chọn đợt KLTN'
                          }
                          searchable
                          disabled={
                            busy ||
                            !canReadBatches ||
                            Boolean(recovery) ||
                            !editable ||
                            Boolean(selectedTopic) ||
                            (!canSelectBatch && !selectedTopic) ||
                            batchesQuery.isPending ||
                            batchesQuery.isError
                          }
                        />
                        {!canReadBatches && (
                          <p role="alert" className="text-xs text-tone-warning-text">
                            Bạn chưa có quyền xem danh sách đợt KLTN. Liên hệ quản trị viên để được
                            cấp quyền.
                          </p>
                        )}
                        {!canSelectBatch && !selectedTopic && openBatch && (
                          <p className="text-xs text-ink-muted">
                            Đề tài mới luôn thuộc đợt KLTN đang mở hiện tại.
                          </p>
                        )}
                        {!canSelectBatch &&
                          !selectedTopic &&
                          canReadBatches &&
                          !batchesQuery.isPending &&
                          !batchesQuery.isError &&
                          !openBatch && (
                            <p role="alert" className="text-xs text-tone-warning-text">
                              Hiện chưa có đợt KLTN nào đang mở để tạo đề tài mới.
                            </p>
                          )}
                        {selectedTopic && (
                          <p className="text-xs text-ink-muted">
                            Đợt KLTN được giữ nguyên sau khi tạo đề tài.
                          </p>
                        )}
                      </div>

                      <div className="space-y-1.5">
                        <label
                          htmlFor="topic-title-vi"
                          className="flex items-center justify-between gap-2 text-xs font-semibold text-ink-secondary"
                        >
                          <span>
                            Tên đề tài tiếng Việt <span className="text-tone-danger-icon">*</span>
                          </span>
                          <span className="text-[11px] font-medium tabular-nums text-ink-subtle">
                            {values.titleVi.length}/300
                          </span>
                        </label>
                        <Input
                          className="h-10 rounded-xl bg-surface-subtle/60 shadow-none focus-visible:bg-surface-card"
                          id="topic-title-vi"
                          error={fieldErrors.titleVi}
                          value={values.titleVi}
                          onChange={(event) => updateField('titleVi', event.target.value)}
                          maxLength={300}
                          required
                          placeholder="Nhập tên đề tài tiếng Việt"
                        />
                      </div>

                      <div className="space-y-1.5">
                        <label
                          htmlFor="topic-title-en"
                          className="flex items-center justify-between gap-2 text-xs font-semibold text-ink-secondary"
                        >
                          <span>
                            Tên đề tài tiếng Anh{' '}
                            <span className="font-normal text-ink-subtle">(cần khi gửi)</span>
                          </span>
                          <span className="text-[11px] font-medium tabular-nums text-ink-subtle">
                            {values.titleEn.length}/350
                          </span>
                        </label>
                        <Input
                          className="h-10 rounded-xl bg-surface-subtle/60 shadow-none focus-visible:bg-surface-card"
                          id="topic-title-en"
                          error={fieldErrors.titleEn}
                          value={values.titleEn}
                          onChange={(event) => updateField('titleEn', event.target.value)}
                          maxLength={350}
                          placeholder="Nhập tên tiếng Anh (cần khi gửi duyệt)"
                        />
                      </div>

                      <div className="space-y-1.5">
                        <label
                          id="topic-description-label"
                          className="block text-xs font-semibold text-ink-secondary"
                        >
                          Mục tiêu và mô tả{' '}
                          <span className="font-normal text-ink-subtle">(cần khi gửi)</span>
                        </label>
                        <RichTextEditor
                          id="topic-description"
                          ariaLabelledBy="topic-description-label"
                          ariaDescribedBy={
                            fieldErrors.description ? 'topic-description-error' : undefined
                          }
                          value={values.description}
                          invalid={Boolean(fieldErrors.description)}
                          disabled={busy || !editable || Boolean(recovery)}
                          expandable
                          expandedTitle="Soạn mục tiêu và mô tả đề tài"
                          expandedSubtitle={values.titleVi.trim() || undefined}
                          placeholder="Trình bày mục tiêu, phạm vi nghiên cứu và sản phẩm dự kiến..."
                          minHeightClassName="min-h-[160px]"
                          onChange={(nextValue) => updateField('description', nextValue)}
                        />
                        {fieldErrors.description && (
                          <p
                            id="topic-description-error"
                            role="alert"
                            className="text-xs text-tone-danger-icon"
                          >
                            {fieldErrors.description}
                          </p>
                        )}
                      </div>

                      <div className="space-y-1.5">
                        <label
                          id="topic-requirements-label"
                          className="block text-xs font-semibold text-ink-secondary"
                        >
                          Yêu cầu công nghệ và kiến thức{' '}
                          <span className="font-normal text-ink-subtle">(cần khi gửi)</span>
                        </label>
                        <RichTextEditor
                          id="topic-requirements"
                          ariaLabelledBy="topic-requirements-label"
                          ariaDescribedBy={
                            fieldErrors.requirements ? 'topic-requirements-error' : undefined
                          }
                          value={values.requirements}
                          invalid={Boolean(fieldErrors.requirements)}
                          disabled={busy || !editable || Boolean(recovery)}
                          expandable
                          expandedTitle="Soạn yêu cầu công nghệ và kiến thức"
                          expandedSubtitle={values.titleVi.trim() || undefined}
                          placeholder="Công nghệ dự kiến, kiến thức nền tảng cần có..."
                          minHeightClassName="min-h-[130px]"
                          onChange={(nextValue) => updateField('requirements', nextValue)}
                        />
                        {fieldErrors.requirements && (
                          <p
                            id="topic-requirements-error"
                            role="alert"
                            className="text-xs text-tone-danger-icon"
                          >
                            {fieldErrors.requirements}
                          </p>
                        )}
                      </div>
                    </div>

                    <div className="space-y-4 border-t border-line-subtle pt-5">
                      <div className="space-y-1.5">
                        <label
                          id="topic-capacity-label"
                          className="block text-xs font-semibold text-ink-secondary"
                        >
                          Số sinh viên tối đa
                        </label>
                        <Select<number>
                          id="topic-capacity-field"
                          error={fieldErrors.studentNumber}
                          triggerClassName="h-10 rounded-xl bg-surface-subtle/60 shadow-none focus-visible:bg-surface-card"
                          aria-labelledby="topic-capacity-label"
                          value={values.studentNumber}
                          options={[
                            { value: 1, label: '1 sinh viên' },
                            { value: 2, label: '2 sinh viên' },
                          ]}
                          disabled={busy || !editable || Boolean(recovery)}
                          onChange={(value: number) =>
                            updateField('studentNumber', value === 1 ? 1 : 2)
                          }
                        />
                      </div>

                      <TopicStudentAssignmentField
                        codes={values.studentCodes ?? []}
                        capacity={values.studentNumber}
                        currentStudents={selectedTopic?.group?.students}
                        studentLookups={studentLookups}
                        disabled={busy || !editable || Boolean(recovery)}
                        error={fieldErrors.studentCodes}
                        onChange={(codes) => updateField('studentCodes', codes)}
                      />
                    </div>
                  </fieldset>

                  {(formError || saveMutation.isError) && (
                    <p
                      role="alert"
                      className="rounded-xl border border-tone-danger-border bg-tone-danger-bg px-3.5 py-2.5 text-xs font-medium text-tone-danger-text"
                    >
                      {formError || getTopicErrorMessage(saveMutation.error)}
                    </p>
                  )}
                </div>

                <aside className="space-y-4 border-t border-line/80 bg-surface-subtle/70 p-5 sm:p-6 lg:col-span-4 lg:border-l lg:border-t-0">
                  <div className="flex items-center justify-between gap-2">
                    <h3 className="text-sm font-bold tracking-tight text-ink-primary">
                      {editable ? 'Tóm tắt đề xuất' : 'Thông tin đề tài'}
                    </h3>
                    <TopicStatusBadge status={selectedTopic?.status ?? 'Draft'} />
                  </div>

                  {/* KPI Mini-Tiles matching PeriodSummaryCard & RosterGuideCard */}
                  <div className="grid grid-cols-2 gap-2.5">
                    <div className="rounded-xl border border-line bg-surface-card p-3 shadow-2xs">
                      <span className="mb-0.5 block text-[11px] font-medium text-ink-muted">
                        Chỉ tiêu nhóm
                      </span>
                      <div className="flex items-baseline gap-1">
                        <span className="text-lg font-bold tabular-nums text-ink-primary">
                          {values.studentNumber}
                        </span>
                        <span className="text-xs text-ink-muted">sinh viên</span>
                      </div>
                      <span className="mt-1 inline-block text-[10px] text-ink-subtle">
                        Tối đa cho đề tài
                      </span>
                    </div>

                    {editable ? (
                      <div className="rounded-xl border border-line bg-surface-card p-3 shadow-2xs">
                        <span className="mb-0.5 block text-[11px] font-medium text-ink-muted">
                          Mức hoàn thiện
                        </span>
                        <div className="flex items-baseline gap-1">
                          <span
                            className={`text-lg font-bold tabular-nums ${
                              isChecklistComplete ? 'text-tone-success-text' : 'text-ink-primary'
                            }`}
                          >
                            {completedChecklistCount}/{checklist.length}
                          </span>
                          <span className="text-xs text-ink-muted">tiêu chí</span>
                        </div>
                        <div className="mt-1.5 h-1.5 w-full overflow-hidden rounded-full bg-surface-muted">
                          <div
                            className={`h-full rounded-full transition-all duration-300 ${
                              isChecklistComplete ? 'bg-emerald-500' : 'bg-primary'
                            }`}
                            style={{
                              width: `${Math.round((completedChecklistCount / checklist.length) * 100)}%`,
                            }}
                          />
                        </div>
                      </div>
                    ) : (
                      <div className="rounded-xl border border-line bg-surface-card p-3 shadow-2xs">
                        <span className="mb-0.5 block text-[11px] font-medium text-ink-muted">
                          Sinh viên ghi nhận
                        </span>
                        <div className="flex items-baseline gap-1">
                          <span className="text-lg font-bold tabular-nums text-ink-primary">
                            {assignedStudentCount}
                          </span>
                          <span className="text-xs text-ink-muted">sinh viên</span>
                        </div>
                        <span className="mt-1 inline-block text-[10px] text-ink-subtle">
                          Theo phân công hiện tại
                        </span>
                      </div>
                    )}
                  </div>

                  {/* Key-Value Metadata Panel matching PeriodSummaryCard & RosterGuideCard */}
                  <dl className="space-y-2.5 rounded-xl border border-line bg-surface-card p-3.5 text-xs shadow-2xs">
                    <div className="flex items-center justify-between gap-3">
                      <dt className="shrink-0 font-medium text-ink-muted">Người đề xuất:</dt>
                      <dd className="truncate font-semibold text-ink-primary" title={user?.fullName}>
                        {user?.fullName}
                      </dd>
                    </div>
                    <div className="flex items-center justify-between gap-3 border-t border-line-subtle pt-2.5">
                      <dt className="shrink-0 font-medium text-ink-muted">Đợt khóa luận:</dt>
                      <dd
                        className="truncate text-right font-semibold text-ink-primary"
                        title={
                          selectedBatch?.batchName ||
                          (values.batchId ? `Mã đợt: ${values.batchId}` : 'Chưa chọn đợt')
                        }
                      >
                        {selectedBatch?.batchName ||
                          (values.batchId && batchesLoading ? (
                            <Skeleton className="h-4 w-28 max-w-full" />
                          ) : values.batchId ? (
                            `Mã đợt: ${values.batchId}`
                          ) : (
                            <span className="font-medium text-ink-subtle">Chưa chọn đợt</span>
                          ))}
                      </dd>
                    </div>
                  </dl>

                  {values.batchId && <TopicProposalSchedule batchId={values.batchId} />}

                  <Button
                    type="button"
                    variant="outline"
                    leftIcon={<Eye />}
                    className="h-9 w-full rounded-xl border-line-input bg-surface-card text-xs font-semibold text-ink-secondary shadow-2xs hover:border-line-strong hover:bg-surface-hover"
                    disabled={busy || Boolean(recovery)}
                    onClick={() => setIsPreviewOpen(true)}
                  >
                    Xem trước đề tài
                  </Button>

                  {editable && (
                    <div className="space-y-3 rounded-xl border border-line bg-surface-card p-3.5 shadow-2xs">
                      <div className="flex items-center justify-between gap-2">
                        <p className="text-xs font-bold text-ink-heading">
                          Cần hoàn thành trước khi gửi
                        </p>
                        <span
                          className={`inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-full border px-2 py-0.5 text-[11px] font-semibold tabular-nums ${
                            isChecklistComplete
                              ? 'border-tone-success-border bg-tone-success-bg text-tone-success-strong'
                              : 'border-line-input bg-surface-muted text-ink-secondary'
                          }`}
                        >
                          {completedChecklistCount}/{checklist.length} mục
                        </span>
                      </div>
                      <ul className="space-y-2 text-xs leading-5">
                        {checklist.map((item) => (
                          <li
                            key={item.label}
                            className={`flex items-start gap-2 ${
                              item.complete ? 'font-medium text-tone-success-text' : 'text-ink-body'
                            }`}
                          >
                            {item.complete ? (
                              <CheckCircle2
                                className="mt-0.5 size-4 shrink-0 text-tone-success-icon"
                                aria-hidden="true"
                              />
                            ) : (
                              <Circle
                                className="mt-0.5 size-4 shrink-0 text-ink-faint"
                                aria-hidden="true"
                              />
                            )}
                            <span>
                              <span className="sr-only">
                                {item.complete ? 'Đã đủ: ' : 'Chưa đủ: '}
                              </span>
                              {item.label}
                            </span>
                          </li>
                        ))}
                      </ul>
                      <p className="border-t border-line-subtle pt-2.5 text-[11px] leading-relaxed text-ink-subtle">
                        Lưu nháp chỉ cần đợt khóa luận, tên tiếng Việt và chỉ tiêu. Hoàn thành đủ 6
                        mục trước khi gửi Khoa thẩm định.
                      </p>
                    </div>
                  )}

                  {batchesQuery.isError && (
                    <InlineAlert
                      tone="error"
                      action={
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => void batchesQuery.refetch()}
                        >
                          Tải lại danh sách đợt
                        </Button>
                      }
                    >
                      {getTopicErrorMessage(batchesQuery.error)}
                    </InlineAlert>
                  )}

                  {batchesQuery.isSuccess && batches.length === 0 && (
                    <InlineAlert tone="warning">
                      Chưa có đợt khóa luận. Vui lòng liên hệ đơn vị quản lý.
                    </InlineAlert>
                  )}
                </aside>
              </div>

              <div className="sticky bottom-0 z-10 flex flex-wrap items-center justify-between gap-3 border-t border-line/80 bg-surface-card/95 px-5 py-3.5 backdrop-blur-md sm:px-6">
                <div className="min-w-0 text-xs">
                  {hasUnsavedChanges ? (
                    <span
                      role="status"
                      className="inline-flex items-center gap-1.5 font-medium text-tone-warning-text"
                    >
                      <span className="size-1.5 rounded-full bg-amber-500" aria-hidden="true" />
                      Có thay đổi chưa lưu trên hệ thống
                    </span>
                  ) : editable && localSavedAt ? (
                    <span role="status" className="text-ink-muted">
                      Đã lưu trên thiết bị lúc {new Date(localSavedAt).toLocaleTimeString('vi-VN')}.
                      Dùng Lưu nháp để lưu lên hệ thống.
                    </span>
                  ) : null}
                </div>

                {editable ? (
                  <div className="ml-auto flex flex-wrap items-center justify-end gap-2.5">
                    <Button
                      type="button"
                      variant="outline"
                      disabled={busy}
                      onClick={closeForm}
                      className="h-9 rounded-xl border-line-input px-4 text-xs font-semibold hover:bg-surface-hover"
                    >
                      Đóng
                    </Button>
                    <Button
                      type="submit"
                      name="action"
                      value="draft"
                      variant="outline"
                      leftIcon={<Save />}
                      className="h-9 rounded-xl border-line-input px-4 text-xs font-semibold hover:bg-surface-hover"
                      disabled={busy || Boolean(recovery)}
                      isLoading={
                        saveMutation.isPending && !saveMutation.variables?.submitImmediately
                      }
                    >
                      {selectedTopic?.status === 'NeedRevision' ? 'Lưu chỉnh sửa' : 'Lưu nháp'}
                    </Button>
                    <Button
                      type="submit"
                      name="action"
                      value="submit"
                      leftIcon={<Send />}
                      className="h-9 rounded-xl px-5 text-xs font-semibold shadow-xs transition-all active:scale-[0.98]"
                      disabled={busy || Boolean(recovery)}
                      isLoading={
                        saveMutation.isPending &&
                        saveMutation.variables?.submitImmediately === true
                      }
                    >
                      {selectedTopic?.status === 'NeedRevision'
                        ? 'Gửi duyệt lại'
                        : 'Gửi Khoa thẩm định'}
                    </Button>
                  </div>
                ) : (
                  <div className="ml-auto flex justify-end">
                    <Button
                      type="button"
                      variant="outline"
                      className="h-9 rounded-xl border-line-input px-4 text-xs font-semibold hover:bg-surface-hover"
                      onClick={closeForm}
                    >
                      Đóng
                    </Button>
                  </div>
                )}
              </div>
            </form>
          )}
        </Dialog>
      )}

      <ConfirmDialog
        open={Boolean(pendingConfirmation)}
        onClose={() => setPendingConfirmation(null)}
        onConfirm={confirmPendingAction}
        type="warning"
        title={
          pendingConfirmation?.kind === 'submit' ? 'Gửi đề tài thẩm định' : 'Bỏ thay đổi chưa lưu'
        }
        description={
          pendingConfirmation?.kind === 'submit'
            ? 'Sau khi gửi, bạn chỉ có thể sửa đề tài khi Khoa yêu cầu chỉnh sửa.'
            : pendingConfirmation?.kind === 'close'
              ? 'Bỏ các thay đổi chưa lưu và đóng cửa sổ?'
              : 'Bỏ các thay đổi chưa lưu để tiếp tục?'
        }
        confirmText={pendingConfirmation?.kind === 'submit' ? 'Gửi Khoa' : 'Bỏ thay đổi'}
        isConfirming={busy}
      />
      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onClose={() => {
          if (blocker.state === 'blocked') blocker.reset();
        }}
        onConfirm={leaveWithoutChanges}
        type="warning"
        title="Rời trang?"
        description="Bạn đang có nội dung chưa lưu. Rời trang sẽ bỏ những thay đổi này."
        confirmText="Bỏ thay đổi và rời trang"
        cancelText="Tiếp tục soạn"
        isConfirming={busy}
      />

      <section aria-label="Thống kê đề tài" className="space-y-2">
        <p className="text-sm text-ink-body">
          {effectiveBatchId
            ? (filterBatch?.batchName ?? 'Đợt đã chọn')
            : canSelectBatch
              ? 'Tất cả đợt khóa luận'
              : 'Chưa có đợt KLTN đang mở'}
        </p>
        <StatisticsBar
          layout="card"
          className="grid-cols-3"
          loading={hasActiveBatchScope && statisticsQuery.isPending && !statistics}
          items={[
            {
              key: 'total',
              label: 'Tổng đề tài',
              value: formatCount(statistics?.total),
              icon: <FileText />,
              tone: 'primary',
              active: !filters.status && !filters.keyword,
              onClick: () => quickFilter(''),
            },
            {
              key: 'submitted',
              label: 'Chờ duyệt',
              value: formatCount(statistics?.submitted),
              icon: <Clock3 />,
              active: filters.status === 'Submitted' && !filters.keyword,
              onClick: () => quickFilter('Submitted'),
            },
            {
              key: 'revision',
              label: 'Cần chỉnh sửa',
              value: formatCount(statistics?.needRevision),
              icon: <RotateCcw />,
              tone: 'warning',
              active: filters.status === 'NeedRevision' && !filters.keyword,
              onClick: () => quickFilter('NeedRevision'),
            },
          ]}
        />
        {statisticsQuery.isError && (
          <div role="alert" className="text-sm text-tone-danger-text">
            Không tải được thống kê đề tài.{' '}
            <Button variant="link" size="sm" onClick={() => void statisticsQuery.refetch()}>
              Thử lại thống kê
            </Button>
          </div>
        )}
      </section>
      <Card className="rounded-2xl border-line/80 p-4 shadow-xs sm:p-5">
        <form
          onSubmit={handleApplyFilters}
          aria-label="Bộ lọc đề tài"
          className={`grid grid-cols-1 gap-3 sm:grid-cols-2 ${canSelectBatch ? 'lg:grid-cols-4' : 'lg:grid-cols-3'}`}
        >
        <label className="block space-y-1.5 sm:col-span-2">
          <span className="text-xs font-medium text-ink-secondary">Tìm kiếm đề tài</span>
          <Input
            leftIcon={<Search className="size-4" />}
            placeholder="Tên đề tài tiếng Việt hoặc tiếng Anh"
            value={draftFilters.keyword}
            onChange={(event) =>
              applyFiltersNow({ ...draftFilters, keyword: event.target.value })
            }
          />
        </label>
        {canSelectBatch && (
          <div className="space-y-1.5">
            <span className="text-xs font-medium text-ink-secondary">Đợt khóa luận</span>
            <Select<string>
              aria-label="Lọc theo đợt KLTN"
              placeholder="Tất cả đợt khóa luận"
              value={effectiveDraftBatchId}
              options={[
                { value: '', label: 'Tất cả đợt khóa luận' },
                ...batches.map((batch) => ({ value: batch.id, label: batch.batchName })),
              ]}
              onChange={(batchId) => {
                setHasExplicitBatchChoice(true);
                applyFiltersNow({ ...draftFilters, batchId });
              }}
              searchable
              disabled={!canReadBatches || batchesQuery.isPending || batchesQuery.isError}
            />
          </div>
        )}
        <div className="space-y-1.5">
          <span className="text-xs font-medium text-ink-secondary">Trạng thái thẩm định</span>
          <Select<TopicStatus | ''>
            aria-label="Lọc theo trạng thái đề tài"
            placeholder="Tất cả trạng thái"
            value={draftFilters.status}
            options={[
              { value: '', label: 'Tất cả trạng thái' },
              ...Object.entries(TOPIC_STATUS_LABELS).map(([value, label]) => ({
                value: value as TopicStatus,
                label,
              })),
            ]}
            onChange={(status) => applyFiltersNow({ ...draftFilters, status })}
          />
        </div>
        <div
          className={`flex flex-wrap items-center gap-2 sm:col-span-2 ${canSelectBatch ? 'lg:col-span-4' : 'lg:col-span-3'}`}
        >
          <Button type="button" variant="outline" disabled={!hasFilters} onClick={clearFilters}>
            Bỏ lọc
          </Button>
          <details className="text-xs text-ink-body sm:ml-auto">
            <summary className="cursor-pointer py-2">Quy định đề xuất</summary>
            <p className="max-w-xl py-2 leading-5">
              Mỗi đề tài tối đa 2 sinh viên. Khoa thẩm định trước khi công bố. Giảng viên chỉ sửa đề
              tài ở trạng thái Bản nháp hoặc Yêu cầu chỉnh sửa.
            </p>
          </details>
        </div>
        </form>
      </Card>

      {effectiveBatchId && <TopicProposalSchedule batchId={effectiveBatchId} />}

      {!canSelectBatch &&
        canReadBatches &&
        !batchesQuery.isPending &&
        !batchesQuery.isError &&
        !openBatch &&
        !isFormOpen && (
          <InlineAlert tone="info">
            Hiện chưa có đợt KLTN nào đang mở. Danh sách đề tài của đợt hiện tại sẽ hiển thị khi có
            đợt KLTN được mở.
          </InlineAlert>
        )}

      {!canReadBatches && !isFormOpen && (
        <InlineAlert tone="warning">
          Bạn chưa có quyền xem danh sách đợt khóa luận. Liên hệ quản trị viên để được cấp quyền.
        </InlineAlert>
      )}

      {batchesQuery.isError && !isFormOpen && (
        <InlineAlert
          tone="error"
          action={
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => void batchesQuery.refetch()}
            >
              Tải lại danh sách đợt
            </Button>
          }
        >
          {getTopicErrorMessage(batchesQuery.error)}
        </InlineAlert>
      )}

      {/* Row 2: Table Card */}
      <Card className="overflow-hidden rounded-2xl border-line/80 shadow-xs transition-all hover:border-line-strong">
        <CardHeader className="flex-row flex-wrap items-center justify-between gap-3 space-y-0 border-b border-line-subtle/90 bg-gradient-to-r from-surface-subtle/80 via-surface-card to-surface-card px-5 py-4 sm:px-6">
          <div className="space-y-0.5">
            <CardTitle className="text-base font-semibold tracking-tight text-ink-primary">
              Danh sách đề tài đã đề xuất
            </CardTitle>
            <p className="text-xs text-ink-muted">
              {canSelectBatch && effectiveBatchId
                ? 'Đang lọc theo đợt khóa luận đã chọn'
                : 'Theo dõi tiến trình thẩm định và kết quả duyệt đề tài'}
            </p>
          </div>

          <span className="rounded-full border border-line-input bg-surface-muted px-2.5 py-1 text-xs font-semibold tabular-nums text-ink-secondary shadow-2xs">
            {!hasActiveBatchScope || topicsQuery.isError ? '—' : (topicsQuery.data?.totalCount ?? '—')}{' '}
            đề tài
          </span>
        </CardHeader>

        <TableActiveFilters
          chips={[
            ...(filters.keyword
              ? [
                  {
                    key: 'keyword',
                    label: 'Từ khóa',
                    value: filters.keyword,
                    onRemove: () => removeFilter('keyword'),
                  },
                ]
              : []),
            ...(canSelectBatch && effectiveBatchId
              ? [
                  {
                    key: 'batchId',
                    label: 'Đợt khóa luận',
                    value: filterBatch?.batchName ?? 'Đợt đã chọn',
                    onRemove: () => removeFilter('batchId'),
                  },
                ]
              : []),
            ...(filters.status
              ? [
                  {
                    key: 'status',
                    label: 'Trạng thái',
                    value: TOPIC_STATUS_LABELS[filters.status],
                    onRemove: () => removeFilter('status'),
                  },
                ]
              : []),
          ]}
          onClearAll={clearFilters}
        />

        <TopicsTable
          topics={hasActiveBatchScope ? (topicsQuery.data?.items ?? []) : []}
          batches={batches}
          batchesLoading={batchesLoading}
          pageNumber={topicsQuery.data?.pageNumber ?? pageNumber}
          pageSize={topicsQuery.data?.pageSize ?? pageSize}
          busy={busy || topicsQuery.isFetching}
          loading={hasActiveBatchScope ? topicsQuery.isPending : batchesQuery.isPending}
          error={
            topicsQuery.isError
              ? {
                  message: getTopicErrorMessage(topicsQuery.error),
                  onRetry: () => void topicsQuery.refetch(),
                }
              : null
          }
          onOpen={openTopic}
          selectedRowId={selectedRowId}
          onSelectRow={setSelectedRowId}
          onCopy={(topic) => openTopic(topic, 'copy')}
          onSubmit={(topic) => openTopic(topic, 'submit')}
          sortField={sortBy}
          sortOrder={sortOrder}
          onSortChange={(field) => {
            setSortOrder(sortBy === field && sortOrder === 'asc' ? 'desc' : 'asc');
            setSortBy(field);
            setPageNumber(1);
            setSelectedRowId(null);
          }}
          empty={{
            icon: <FileText className="size-9" />,
            title:
              pageNumber > 1 && topicsQuery.data?.totalCount
                ? 'Trang này không còn đề tài'
                : 'Không có đề tài',
            description:
              filters.keyword || filters.status || (canSelectBatch && filters.batchId)
                ? 'Không có đề tài phù hợp với bộ lọc.'
                : !canSelectBatch && !openBatch
                  ? 'Hiện chưa có đợt KLTN nào đang mở.'
                  : 'Chưa có đề tài. Tạo đề xuất đầu tiên để bắt đầu.',
            action:
              pageNumber > 1 ? (
                <Button variant="outline" onClick={() => setPageNumber(1)}>
                  Về trang đầu
                </Button>
              ) : filters.keyword || filters.status || (canSelectBatch && filters.batchId) ? (
                <Button variant="outline" leftIcon={<FunnelX />} onClick={clearFilters}>
                  Bỏ lọc
                </Button>
              ) : undefined,
          }}
          pagination={
            topicsQuery.data
              ? {
                  pageNumber: topicsQuery.data.pageNumber,
                  pageSize: topicsQuery.data.pageSize,
                  totalCount: topicsQuery.data.totalCount,
                  hasPreviousPage: topicsQuery.data.hasPreviousPage,
                  hasNextPage: topicsQuery.data.hasNextPage,
                  onPageChange: (next) => {
                    setPageNumber(next);
                    setSelectedRowId(null);
                  },
                  onPageSizeChange: (size) => {
                    setPageSize(size);
                    setPageNumber(1);
                    setSelectedRowId(null);
                  },
                  itemLabel: 'đề tài',
                }
              : undefined
          }
        />
      </Card>

      {/* Floating Action Dock for Selected Row / Topics */}
      {selectedRowId && (
        <TableFloatingBar
          aria-label="Thao tác với đề tài đã chọn"
          selectedCount={1}
          totalCount={topicsQuery.data?.totalCount ?? 0}
          onClearSelection={() => {
            setSelectedRowId(null);
          }}
        >
          {(() => {
            const activeId = selectedRowId;
            const topic = topicsQuery.data?.items.find((item) => item.id === activeId);
            if (!topic) return null;
            return (
              <>
                <Button
                  type="button"
                  size="sm"
                  variant="default"
                  className="h-8 text-xs font-semibold rounded-xl shadow-xs"
                  disabled={busy || topicsQuery.isFetching}
                  onClick={() => openTopic(topic)}
                >
                  Xem
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-8 text-xs font-semibold rounded-xl"
                  disabled={busy || topicsQuery.isFetching}
                  onClick={() => openTopic(topic, 'copy')}
                >
                  Nhân bản
                </Button>
                {canEditTopic(topic.status) && (
                  <Button
                    type="button"
                    variant="successOutline"
                    size="sm"
                    className="h-8 text-xs font-semibold rounded-xl"
                    disabled={busy || topicsQuery.isFetching}
                    onClick={() => openTopic(topic, 'submit')}
                  >
                    Gửi duyệt
                  </Button>
                )}
              </>
            );
          })()}
        </TableFloatingBar>
      )}
    </div>
  );
}
