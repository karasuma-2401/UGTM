import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { useBeforeUnload, useBlocker } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  FilePlus2,
  FileText,
  Clock3,
  RotateCcw,
  Search,
  SlidersHorizontal,
  Save,
  Send,
  Eye,
  CheckCircle2,
  Circle,
  X,
  ArrowLeft,
  FunnelX,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Dialog } from '@/components/ui/dialog';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { TopicStatusBadge } from '../components/TopicStatusBadge';
import { TopicsTable } from '../components/TopicsTable';
import { TopicProposalSchedule } from '../components/TopicProposalSchedule';
import { TableSkeleton } from '@/components/ui/table-skeleton';
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
import { TopicStudentAssignmentField } from '../components/TopicStudentAssignmentField';
import { PageHeader } from '@/components/ui/page-header';
import { TablePagination } from '@/components/ui/table-pagination';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';

import {
  canEditTopic,
  TOPIC_STATUS_LABELS,
  TOPIC_STATUS_HINTS,
  type Topic,
  type TopicFormValues,
  type TopicFilters,
  type TopicStatus,
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

const INITIAL_FILTERS: TopicFilters = { keyword: '', batchId: '', status: '' };

const textareaClass =
  'min-h-28 w-full rounded-lg border border-input bg-slate-50/70 px-3 py-2 ' +
  'text-sm shadow-sm placeholder:text-muted-foreground ' +
  'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring ' +
  'disabled:cursor-not-allowed disabled:opacity-50';

export function TopicProposePage({
  pageTitle = 'Đề xuất đề tài',
  navigation,
}: { pageTitle?: string; navigation?: ReactNode } = {}) {
  const { user, hasPermission } = useAuth();
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
  const [focusField, setFocusField] = useState<keyof TopicFormValues | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [draftFilters, setDraftFilters] = useState<TopicFilters>(INITIAL_FILTERS);
  const [filters, setFilters] = useState<TopicFilters>(INITIAL_FILTERS);
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
      const target = container?.matches('input, textarea')
        ? container
        : container?.querySelector('input, button');
      if (target instanceof HTMLElement) {
        target.focus();
        target.scrollIntoView({ block: 'center', behavior: 'smooth' });
      }
      setFocusField(null);
    });
    return () => window.cancelAnimationFrame(frame);
  }, [focusField, isFormOpen, isPreviewOpen, recovery]);

  const hasFilters = [filters, draftFilters].some((value) =>
    Boolean(value.keyword || value.batchId || value.status),
  );
  const checklist = [
    { label: 'Đã chọn đợt KLTN', complete: Boolean(values.batchId) },
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

  const batchesQuery = useAcademicBatches();
  const topicsQuery = useQuery({
    queryKey: ['my-proposed-topics', user?.id, filters, pageNumber, pageSize],
    enabled: Boolean(user?.id),
    queryFn: ({ signal }) =>
      topicApi.getMyTopics({ ...filters, advisorId: user!.id, pageNumber, pageSize }, signal),
    placeholderData: (previousData, previousQuery) =>
      previousQuery?.queryKey[1] === user?.id ? previousData : undefined,
  });
  const statisticsQuery = useQuery({
    queryKey: ['my-proposed-topics', user?.id, 'statistics', filters.batchId],
    enabled: Boolean(user?.id),
    queryFn: ({ signal }) => topicApi.getStatistics(user!.id, filters.batchId || undefined, signal),
  });
  const statistics = statisticsQuery.isError ? undefined : statisticsQuery.data;
  const isRefreshing = topicsQuery.isFetching && !topicsQuery.isPending;
  const formatCount = (count: number | undefined) =>
    count === undefined ? '—' : count.toLocaleString('vi-VN');

  const detailMutation = useMutation({
    mutationFn: ({ id }: { id: string; intent: 'edit' | 'copy' | 'submit' }) =>
      topicApi.getDetail(id),
    onSuccess: async (topic, { intent }) => {
      const fresh = toFormValues(topic);
      if (intent === 'copy') {
        beginEditor(null, copyProposal(topic), { ...INITIAL_VALUES });
        setNotice('Đã nhân bản nội dung. Chọn đợt và kiểm tra tên trước khi lưu đề xuất mới.');
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
        if (confirmSubmission())
          saveMutation.mutate({
            id: topic.id,
            values: fresh,
            submitImmediately: true,
            hasChanges: topic.studentNumber !== fresh.studentNumber,
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
      setNotice(
        submitImmediately
          ? 'Đã gửi đề tài đến Khoa để thẩm định.'
          : topic.status === 'NeedRevision'
            ? 'Đã lưu chỉnh sửa trên hệ thống. Bạn có thể gửi duyệt lại khi sẵn sàng.'
            : 'Đã lưu bản nháp đề tài trên hệ thống.',
      );
      setPageNumber(1);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['my-proposed-topics'] }),
        queryClient.invalidateQueries({ queryKey: ['topic-review'] }),
        queryClient.invalidateQueries({ queryKey: ['topic-advising'] }),
        queryClient.invalidateQueries({ queryKey: ['topic-advising-detail'] }),
      ]);
    },
  });

  const busy = saveMutation.isPending || detailMutation.isPending;
  const batches = batchesQuery.data ?? [];
  const selectedBatch = batches.find((batch) => batch.id === values.batchId);
  const batchesLoading = canReadBatches && (batchesQuery.isPending || batchesQuery.isFetching);

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
      batchId: selectedTopic?.batchId ?? recovery.values.batchId,
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
    beginEditor(null, { ...availableLocalDraft.values }, { ...INITIAL_VALUES });
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
    if (hasUnsavedChanges && !window.confirm('Bỏ các thay đổi chưa lưu để tiếp tục?')) return;
    setNotice('');
    setFormError('');
    setFieldErrors({});
    saveMutation.reset();
    detailMutation.reset();
    if (topic) detailMutation.mutate({ id: topic.id, intent });
    else beginEditor(null, { ...INITIAL_VALUES, batchId: filters.batchId });
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
    if (hasUnsavedChanges && !window.confirm('Bỏ các thay đổi chưa lưu để đóng cửa sổ?')) return;
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
  function confirmSubmission() {
    return window.confirm(
      'Gửi đề tài đến Khoa thẩm định? Sau khi gửi, bạn chỉ có thể sửa khi Khoa yêu cầu chỉnh sửa.',
    );
  }
  function quickFilter(status: TopicStatus | '') {
    const next = { keyword: '', batchId: filters.batchId, status };
    setFilters(next);
    setDraftFilters(next);
    setPageNumber(1);
  }
  function handleApplyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFilters({ ...draftFilters, keyword: draftFilters.keyword.trim() });
    setPageNumber(1);
  }
  function clearFilters() {
    setDraftFilters({ ...INITIAL_FILTERS });
    setFilters({ ...INITIAL_FILTERS });
    setPageNumber(1);
  }
  function removeFilter(key: keyof TopicFilters) {
    const next = { ...filters, [key]: '' };
    setDraftFilters(next);
    setFilters(next);
    setPageNumber(1);
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
    if (Object.keys(errors).length) {
      showValidation(errors);
      return;
    }
    if (submitImmediately && !confirmSubmission()) return;
    setNotice('');
    setFormError('');
    setFieldErrors({});
    saveMutation.mutate({
      id: selectedTopic?.id ?? null,
      values: { ...values },
      submitImmediately,
      hasChanges:
        dirty || Boolean(selectedTopic && selectedTopic.studentNumber !== values.studentNumber),
      ...(updateAssignedStudents ? { updateAssignedStudents: true } : {}),
    });
  }

  return (
    <div className="min-w-0 space-y-5">
      <PageHeader icon={<FileText />} title={pageTitle} navigation={navigation}>
        <Button
          type="button"
          leftIcon={<FilePlus2 />}
          disabled={busy}
          onClick={() => openTopic(null)}
          className="h-10 rounded-lg px-4 shadow-sm"
        >
          Đề xuất đề tài
        </Button>
      </PageHeader>
      {availableLocalDraft && !isFormOpen && (
        <div
          role="status"
          className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900"
        >
          <div>
            <p className="font-medium">Có đề xuất đang soạn được lưu trên thiết bị</p>
            <p className="mt-1 text-xs text-amber-800">
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
      {localError && !isFormOpen && (
        <p role="alert" className="rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
          {localError}
        </p>
      )}
      {notice && (
        <p role="status" className="rounded-lg bg-emerald-50 p-3 text-sm text-emerald-700">
          {notice}
        </p>
      )}
      {detailMutation.isPending && (
        <p role="status" className="text-sm text-slate-500">
          Đang tải nội dung đề tài...
        </p>
      )}
      {detailMutation.isError && (
        <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">
          {getTopicErrorMessage(detailMutation.error)}
        </p>
      )}

      {isFormOpen && (
        <Dialog
          title={
            blocker.state === 'blocked'
              ? 'Thay đổi chưa được lưu'
              : isPreviewOpen
                ? 'Xem trước đề tài'
                : 'Đề xuất đề tài'
          }
          busy={busy}
          onClose={closeForm}
          className={
            blocker.state === 'blocked' ? 'max-w-lg' : isPreviewOpen ? 'max-w-3xl' : 'max-w-5xl'
          }
        >
          {blocker.state === 'blocked' ? (
            <div className="space-y-5 p-5">
              <p className="text-sm leading-6 text-slate-600">
                Bạn đang có nội dung chưa lưu. Rời trang sẽ bỏ những thay đổi này.
              </p>
              <div className="flex flex-wrap justify-end gap-3">
                <Button
                  type="button"
                  variant="outline"
                  disabled={busy}
                  onClick={() => blocker.reset()}
                >
                  Tiếp tục soạn
                </Button>
                <Button
                  type="button"
                  variant="destructive"
                  disabled={busy}
                  onClick={() => {
                    leaveWithoutChanges();
                  }}
                >
                  Bỏ thay đổi và rời trang
                </Button>
              </div>
            </div>
          ) : isPreviewOpen ? (
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
              <div className="flex justify-end border-t border-slate-100 px-5 py-4">
                <Button
                  type="button"
                  variant="outline"
                  leftIcon={<ArrowLeft />}
                  onClick={() => setIsPreviewOpen(false)}
                >
                  {editable ? 'Quay lại chỉnh sửa' : 'Quay lại chi tiết'}
                </Button>
              </div>
            </>
          ) : (
            <div className="grid items-start gap-5 bg-slate-50/60 p-5 lg:grid-cols-[minmax(0,1fr)_260px]">
              <Card className="min-w-0">
                <CardHeader>
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <CardTitle className="text-base">
                      {selectedTopic
                        ? editable
                          ? 'Chỉnh sửa đề tài'
                          : 'Thông tin đề tài'
                        : 'Nội dung đề xuất'}
                    </CardTitle>
                    <TopicStatusBadge status={selectedTopic?.status ?? 'Draft'} />
                  </div>
                </CardHeader>

                <CardContent>
                  {selectedTopic?.reviewNote && (
                    <div className="mb-4">
                      <TopicReviewNote note={selectedTopic.reviewNote} />
                    </div>
                  )}
                  {recovery && (
                    <div
                      role="status"
                      className="mb-4 space-y-3 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900"
                    >
                      <p className="font-medium">Có nội dung chưa lưu lên hệ thống</p>
                      <p className="text-xs">
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
                        <Button type="button" size="sm" variant="outline" onClick={restoreRecovery}>
                          Khôi phục nội dung
                        </Button>
                        <Button type="button" size="sm" variant="ghost" onClick={discardRecovery}>
                          Bỏ bản lưu tạm
                        </Button>
                      </div>
                    </div>
                  )}
                  <p
                    className={`mb-5 rounded-lg p-3 text-sm ${selectedTopic?.status === 'NeedRevision' ? 'bg-amber-50 text-amber-800' : 'bg-blue-50 text-blue-700'}`}
                  >
                    {TOPIC_STATUS_HINTS[selectedTopic?.status ?? 'Draft']}
                  </p>
                  {hasUnsavedChanges && (
                    <p role="status" className="mb-4 text-xs font-medium text-amber-700">
                      Có thay đổi chưa lưu trên hệ thống
                    </p>
                  )}
                  {editable && localSavedAt && (
                    <p role="status" className="mb-4 text-xs text-slate-500">
                      Đã lưu trên thiết bị lúc {new Date(localSavedAt).toLocaleTimeString('vi-VN')}.
                      Dùng Lưu nháp để lưu lên hệ thống.
                    </p>
                  )}
                  {localError && (
                    <p role="alert" className="mb-4 text-xs text-amber-700">
                      {localError}
                    </p>
                  )}

                  <form onSubmit={handleSubmit} noValidate className="space-y-5">
                    <fieldset
                      disabled={busy || !editable || Boolean(recovery)}
                      className="min-w-0 space-y-5"
                    >
                      <div className="space-y-2">
                        <label
                          id="topic-batch-label"
                          className="text-sm font-medium text-slate-700"
                        >
                          Đợt KLTN <span className="text-rose-600">*</span>
                        </label>
                        <Select<string>
                          id="topic-batch-field"
                          error={fieldErrors.batchId}
                          triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
                          aria-labelledby="topic-batch-label"
                          value={values.batchId}
                          options={batches.map((batch) => ({
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
                                : 'Chọn đợt KLTN'
                          }
                          searchable
                          disabled={
                            busy ||
                            !canReadBatches ||
                            Boolean(recovery) ||
                            !editable ||
                            Boolean(selectedTopic) ||
                            batchesQuery.isPending ||
                            batchesQuery.isError
                          }
                        />
                        {!canReadBatches && (
                          <p role="alert" className="text-xs text-amber-700">
                            Bạn chưa có quyền xem danh sách đợt KLTN. Liên hệ quản trị viên để được
                            cấp quyền.
                          </p>
                        )}
                        {selectedTopic && (
                          <p className="text-xs text-slate-500">
                            Đợt KLTN được giữ nguyên sau khi tạo đề tài.
                          </p>
                        )}
                      </div>

                      <div className="space-y-2">
                        <label
                          htmlFor="topic-title-vi"
                          className="flex items-center justify-between gap-2 text-sm font-medium text-slate-700"
                        >
                          <span>
                            Tên đề tài tiếng Việt <span className="text-rose-600">*</span>
                          </span>
                          <span className="text-[11px] font-normal tabular-nums text-slate-400">
                            {values.titleVi.length}/300
                          </span>
                        </label>
                        <Input
                          className="h-10 rounded-lg bg-slate-50/70 shadow-none focus-visible:bg-white"
                          id="topic-title-vi"
                          error={fieldErrors.titleVi}
                          value={values.titleVi}
                          onChange={(event) => updateField('titleVi', event.target.value)}
                          maxLength={300}
                          required
                          placeholder="Nhập tên đề tài tiếng Việt"
                        />
                      </div>

                      <div className="space-y-2">
                        <label
                          htmlFor="topic-title-en"
                          className="flex items-center justify-between gap-2 text-sm font-medium text-slate-700"
                        >
                          <span>
                            Tên đề tài tiếng Anh{' '}
                            <span className="text-[11px] font-normal text-slate-500">
                              (cần khi gửi)
                            </span>
                          </span>
                          <span className="text-[11px] font-normal tabular-nums text-slate-400">
                            {values.titleEn.length}/350
                          </span>
                        </label>
                        <Input
                          className="h-10 rounded-lg bg-slate-50/70 shadow-none focus-visible:bg-white"
                          id="topic-title-en"
                          error={fieldErrors.titleEn}
                          value={values.titleEn}
                          onChange={(event) => updateField('titleEn', event.target.value)}
                          maxLength={350}
                          placeholder="Nhập tên tiếng Anh (cần khi gửi duyệt)"
                        />
                      </div>

                      <div className="space-y-2">
                        <label
                          htmlFor="topic-description"
                          className="text-sm font-medium text-slate-700"
                        >
                          Mục tiêu và mô tả{' '}
                          <span className="text-[11px] font-normal text-slate-500">
                            (cần khi gửi)
                          </span>
                        </label>
                        <textarea
                          id="topic-description"
                          className={`${textareaClass} ${fieldErrors.description ? 'border-rose-400 focus-visible:ring-rose-500' : ''}`}
                          aria-invalid={Boolean(fieldErrors.description)}
                          aria-describedby={
                            fieldErrors.description ? 'topic-description-error' : undefined
                          }
                          value={values.description}
                          onChange={(event) => updateField('description', event.target.value)}
                          placeholder="Trình bày mục tiêu, phạm vi nghiên cứu và sản phẩm dự kiến..."
                          rows={5}
                        />
                        {fieldErrors.description && (
                          <p
                            id="topic-description-error"
                            role="alert"
                            className="text-xs text-rose-600"
                          >
                            {fieldErrors.description}
                          </p>
                        )}
                      </div>

                      <div className="space-y-2">
                        <label
                          htmlFor="topic-requirements"
                          className="text-sm font-medium text-slate-700"
                        >
                          Yêu cầu công nghệ và kiến thức{' '}
                          <span className="text-[11px] font-normal text-slate-500">
                            (cần khi gửi)
                          </span>
                        </label>
                        <textarea
                          id="topic-requirements"
                          className={`${textareaClass} ${fieldErrors.requirements ? 'border-rose-400 focus-visible:ring-rose-500' : ''}`}
                          aria-invalid={Boolean(fieldErrors.requirements)}
                          aria-describedby={
                            fieldErrors.requirements ? 'topic-requirements-error' : undefined
                          }
                          value={values.requirements}
                          onChange={(event) => updateField('requirements', event.target.value)}
                          placeholder="Công nghệ dự kiến, kiến thức nền tảng cần có..."
                          rows={3}
                        />
                        {fieldErrors.requirements && (
                          <p
                            id="topic-requirements-error"
                            role="alert"
                            className="text-xs text-rose-600"
                          >
                            {fieldErrors.requirements}
                          </p>
                        )}
                      </div>

                      <div className="space-y-2">
                        <label
                          id="topic-capacity-label"
                          className="text-sm font-medium text-slate-700"
                        >
                          Số sinh viên tối đa
                        </label>
                        <Select<number>
                          id="topic-capacity-field"
                          error={fieldErrors.studentNumber}
                          triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
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
                        disabled={busy || !editable || Boolean(recovery)}
                        error={fieldErrors.studentCodes}
                        onChange={(codes) => updateField('studentCodes', codes)}
                      />
                    </fieldset>

                    {(formError || saveMutation.isError) && (
                      <p role="alert" className="text-sm text-rose-600">
                        {formError || getTopicErrorMessage(saveMutation.error)}
                      </p>
                    )}

                    {editable && (
                      <div className="flex flex-wrap justify-end gap-3 border-t border-slate-100 pt-5">
                        <Button
                          type="button"
                          variant="outline"
                          disabled={busy}
                          onClick={closeForm}
                          className="h-10 rounded-lg px-5"
                        >
                          Đóng
                        </Button>
                        <Button
                          type="submit"
                          name="action"
                          value="draft"
                          variant="outline"
                          leftIcon={<Save />}
                          className="h-10 rounded-lg px-5"
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
                          className="h-10 rounded-lg px-5"
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
                    )}
                  </form>
                  {!editable && (
                    <div className="mt-5 flex justify-end border-t border-slate-100 pt-5">
                      <Button
                        type="button"
                        variant="outline"
                        className="h-10 rounded-lg px-5"
                        onClick={closeForm}
                      >
                        Đóng
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle className="text-base">
                    {editable ? 'Trước khi gửi Khoa' : 'Thông tin đề tài'}
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-5 text-sm">
                  <dl className="space-y-4">
                    <div>
                      <dt className="text-xs text-slate-500">Người đề xuất</dt>
                      <dd className="mt-1 font-medium text-slate-800">{user?.fullName}</dd>
                    </div>
                    <div>
                      <dt className="text-xs text-slate-500">Đợt KLTN</dt>
                      <dd className="mt-1 text-slate-800">
                        {selectedBatch?.batchName ||
                          (values.batchId && batchesLoading ? (
                            <Skeleton className="h-5 w-32 max-w-full" />
                          ) : values.batchId ? (
                            `Mã đợt: ${values.batchId}`
                          ) : (
                            'Chưa chọn đợt'
                          ))}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-xs text-slate-500">Chỉ tiêu</dt>
                      <dd className="mt-1 text-slate-800">
                        Tối đa {values.studentNumber} sinh viên
                      </dd>
                    </div>
                  </dl>
                  {values.batchId && <TopicProposalSchedule batchId={values.batchId} />}

                  <Button
                    type="button"
                    variant="soft"
                    leftIcon={<Eye />}
                    className="h-10 w-full rounded-lg"
                    disabled={busy || Boolean(recovery)}
                    onClick={() => setIsPreviewOpen(true)}
                  >
                    Xem trước đề tài
                  </Button>
                  {editable && (
                    <div className="border-t border-slate-100 pt-4">
                      <p className="font-medium text-slate-800">Cần hoàn thành trước khi gửi</p>
                      <p className="mt-1 text-xs text-slate-500">
                        {checklist.filter((item) => item.complete).length}/{checklist.length} mục đã
                        hoàn thành
                      </p>
                      <ul className="mt-3 space-y-3 text-xs leading-5">
                        {checklist.map((item) => (
                          <li
                            key={item.label}
                            className={`flex items-start gap-2 ${item.complete ? 'text-emerald-700' : 'text-slate-500'}`}
                          >
                            {item.complete ? (
                              <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                            ) : (
                              <Circle
                                className="mt-0.5 size-4 shrink-0 text-slate-300"
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
                      <p className="mt-3 text-[11px] leading-5 text-slate-400">
                        Lưu nháp chỉ cần đợt KLTN, tên tiếng Việt và chỉ tiêu. Hoàn thành đủ 6 mục
                        trước khi gửi Khoa thẩm định.
                      </p>
                    </div>
                  )}

                  {batchesQuery.isError && (
                    <div role="alert" className="space-y-2 text-rose-600">
                      <p>{getTopicErrorMessage(batchesQuery.error)}</p>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => void batchesQuery.refetch()}
                      >
                        Tải lại danh sách đợt
                      </Button>
                    </div>
                  )}

                  {batchesQuery.isSuccess && batches.length === 0 && (
                    <p className="text-amber-700">
                      Chưa có đợt KLTN. Vui lòng liên hệ đơn vị quản lý.
                    </p>
                  )}
                </CardContent>
              </Card>
            </div>
          )}
        </Dialog>
      )}

      <form
        onSubmit={handleApplyFilters}
        className="grid items-end gap-4 rounded-xl border border-slate-200/80 bg-white p-4 shadow-sm md:grid-cols-2 xl:grid-cols-[2fr_1fr_1fr_auto]"
      >
        <label className="block space-y-2 text-sm text-slate-700">
          <span>Tìm đề tài</span>
          <Input
            leftIcon={<Search className="size-4" />}
            className="h-10 rounded-lg bg-slate-50/70 shadow-none focus-visible:bg-white"
            placeholder="Tên đề tài tiếng Việt hoặc tiếng Anh"
            value={draftFilters.keyword}
            onChange={(event) => setDraftFilters({ ...draftFilters, keyword: event.target.value })}
          />
        </label>
        <div className="space-y-2 text-sm text-slate-700">
          <span>Đợt KLTN</span>
          <Select<string>
            aria-label="Lọc theo đợt KLTN"
            triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
            value={draftFilters.batchId}
            options={[
              { value: '', label: 'Tất cả đợt' },
              ...batches.map((batch) => ({ value: batch.id, label: batch.batchName })),
            ]}
            onChange={(value: string) => setDraftFilters({ ...draftFilters, batchId: value })}
            searchable
            disabled={batchesQuery.isPending || batchesQuery.isError}
          />
        </div>
        <div className="space-y-2 text-sm text-slate-700">
          <span>Trạng thái</span>
          <Select<TopicStatus | ''>
            aria-label="Lọc theo trạng thái đề tài"
            triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
            value={draftFilters.status}
            options={[
              { value: '', label: 'Tất cả trạng thái' },
              ...Object.entries(TOPIC_STATUS_LABELS).map(([value, label]) => ({
                value: value as TopicStatus,
                label,
              })),
            ]}
            onChange={(value: TopicStatus | '') =>
              setDraftFilters({ ...draftFilters, status: value })
            }
          />
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            type="submit"
            variant="soft"
            leftIcon={<SlidersHorizontal />}
            className="h-10 rounded-lg px-5"
          >
            Áp dụng
          </Button>
          <Button
            type="button"
            variant="outline"
            leftIcon={<FunnelX />}
            className="h-10 rounded-lg px-4"
            disabled={!hasFilters}
            onClick={clearFilters}
          >
            Bỏ lọc
          </Button>
        </div>
      </form>

      {Boolean(filters.keyword || filters.batchId || filters.status) && (
        <div className="flex flex-wrap items-center gap-2" aria-label="Bộ lọc đang áp dụng">
          <span className="text-xs text-slate-500">Đang lọc:</span>
          {filters.keyword && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              rightIcon={<X />}
              onClick={() => removeFilter('keyword')}
              aria-label={`Bỏ lọc từ khóa ${filters.keyword}`}
            >
              Từ khóa: {filters.keyword}
            </Button>
          )}
          {filters.batchId && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              rightIcon={<X />}
              onClick={() => removeFilter('batchId')}
              aria-label="Bỏ lọc đợt KLTN"
            >
              {batches.find((batch) => batch.id === filters.batchId)?.batchName ?? 'Đợt đã chọn'}
            </Button>
          )}
          {filters.status && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              rightIcon={<X />}
              onClick={() => removeFilter('status')}
              aria-label="Bỏ lọc trạng thái"
            >
              {TOPIC_STATUS_LABELS[filters.status]}
            </Button>
          )}
        </div>
      )}
      {filters.batchId && <TopicProposalSchedule batchId={filters.batchId} />}

      {!canReadBatches && !isFormOpen && (
        <p role="status" className="rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
          Bạn chưa có quyền xem danh sách đợt KLTN. Liên hệ quản trị viên để được cấp quyền.
        </p>
      )}

      {batchesQuery.isError && !isFormOpen && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-3 rounded-lg bg-rose-50 p-3 text-sm text-rose-700"
        >
          <p>{getTopicErrorMessage(batchesQuery.error)}</p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void batchesQuery.refetch()}
          >
            Tải lại danh sách đợt
          </Button>
        </div>
      )}

      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <ListCardHeader
          title="Đề tài của tôi"
          icon={<FileText />}
          description={
            filters.batchId
              ? 'Thống kê trong đợt đang chọn'
              : 'Nội dung đề xuất và kết quả thẩm định'
          }
          className="xl:grid-cols-[minmax(0,1fr)_420px]"
        >
          <section
            className="w-full"
            aria-label="Thống kê đề tài"
            aria-busy={statisticsQuery.isFetching}
          >
            <StatisticsBar
              loading={statisticsQuery.isPending}
              items={[
                {
                  key: 'total',
                  onClick: () => quickFilter(''),
                  active: filters.status === '' && !filters.keyword,
                  label: 'Tổng đề tài',
                  value: formatCount(statistics?.total),
                  icon: <FileText />,
                  tone: 'primary',
                },
                {
                  key: 'submitted',
                  onClick: () => quickFilter('Submitted'),
                  active: filters.status === 'Submitted' && !filters.keyword,
                  label: 'Chờ duyệt',
                  value: formatCount(statistics?.submitted),
                  icon: <Clock3 />,
                  tone: 'muted',
                },
                {
                  key: 'revision',
                  onClick: () => quickFilter('NeedRevision'),
                  active: filters.status === 'NeedRevision' && !filters.keyword,
                  label: 'Cần chỉnh sửa',
                  value: formatCount(statistics?.needRevision),
                  icon: <RotateCcw />,
                  tone: 'warning',
                },
              ]}
            />
            {statisticsQuery.isPending && (
              <p role="status" className="sr-only">
                Đang tải thống kê...
              </p>
            )}
            {statisticsQuery.isError && (
              <div role="alert" className="mt-2 flex flex-wrap items-center gap-2">
                <p className="text-xs text-rose-700">Không thể tải thống kê đề tài.</p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={statisticsQuery.isFetching}
                  onClick={() => void statisticsQuery.refetch()}
                >
                  Thử lại
                </Button>
              </div>
            )}
          </section>
        </ListCardHeader>

        <div
          aria-busy={topicsQuery.isFetching}
          className={`h-[min(60vh,40rem)] min-h-[20rem] overflow-auto transition-opacity ${isRefreshing ? 'opacity-60' : ''}`}
        >
          {topicsQuery.isPending ? (
            <TableSkeleton
              className="min-w-[1200px]"
              columns={[
                { label: 'STT', width: 'w-14', variant: 'number' },
                { label: 'Đề tài', variant: 'topic' },
                { label: 'Đợt KLTN', width: 'w-40' },
                { label: 'Chỉ tiêu', width: 'w-24' },
                { label: 'Ngày tạo', width: 'w-32' },
                { label: 'Trạng thái', width: 'w-44', variant: 'badge' },
                { label: 'Thao tác', width: 'w-72', variant: 'actions', actionCount: 2 },
              ]}
            />
          ) : topicsQuery.isError ? (
            <div
              role="alert"
              className="flex h-full flex-col items-center justify-center gap-3 p-6 text-center"
            >
              <p className="text-sm text-rose-600">{getTopicErrorMessage(topicsQuery.error)}</p>
              <Button type="button" variant="outline" onClick={() => void topicsQuery.refetch()}>
                Thử lại
              </Button>
            </div>
          ) : topicsQuery.data?.items.length ? (
            <TopicsTable
              topics={topicsQuery.data.items}
              batches={batches}
              batchesLoading={batchesLoading}
              pageNumber={topicsQuery.data.pageNumber}
              pageSize={topicsQuery.data.pageSize}
              busy={busy || topicsQuery.isFetching}
              onOpen={openTopic}
              selectedRowId={selectedRowId}
              onSelectRow={setSelectedRowId}
              onCopy={(topic) => openTopic(topic, 'copy')}
              onSubmit={(topic) => openTopic(topic, 'submit')}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-3 p-8 text-center">
              <FileText className="size-9 text-slate-300" aria-hidden="true" />
              <p className="text-sm text-slate-500">
                {filters.keyword || filters.status || filters.batchId
                  ? 'Không có đề tài phù hợp với bộ lọc.'
                  : 'Chưa có đề tài. Tạo đề xuất đầu tiên để bắt đầu.'}
              </p>
              {Boolean(filters.keyword || filters.status || filters.batchId) && (
                <Button
                  type="button"
                  variant="outline"
                  leftIcon={<FunnelX />}
                  onClick={clearFilters}
                >
                  Bỏ lọc
                </Button>
              )}
            </div>
          )}
        </div>

        {topicsQuery.data && !topicsQuery.isError && (
          <>
            <div className="flex items-center justify-end gap-3 border-t border-slate-100 px-5 py-3 text-sm">
              <label htmlFor="topic-page-size">Số đề tài mỗi trang</label>
              <select
                id="topic-page-size"
                value={pageSize}
                className="rounded-md border border-slate-200 bg-white p-2"
                disabled={topicsQuery.isFetching || busy}
                onChange={(event) => {
                  setPageSize(Number(event.target.value));
                  setPageNumber(1);
                }}
              >
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
            </div>
            <TablePagination
              pageNumber={topicsQuery.data.pageNumber}
              pageSize={topicsQuery.data.pageSize}
              totalCount={topicsQuery.data.totalCount}
              hasPreviousPage={topicsQuery.data.hasPreviousPage}
              hasNextPage={topicsQuery.data.hasNextPage}
              isLoading={topicsQuery.isFetching || busy}
              onPageChange={setPageNumber}
              itemLabel="đề tài"
            />
          </>
        )}
      </Card>
    </div>
  );
}
