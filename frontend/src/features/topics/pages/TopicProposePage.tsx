import { useState, type FormEvent } from 'react';
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
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Dialog } from '@/components/ui/dialog';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { TopicStatusBadge } from '../components/TopicStatusBadge';
import { TopicsTable } from '../components/TopicsTable';
import { TablePagination } from '@/components/ui/table-pagination';
import { useAuth } from '@/shared/auth';

import {
  canEditTopic,
  TOPIC_STATUS_LABELS,
  type Topic,
  type TopicFormValues,
  type TopicFilters,
  type TopicStatus,
} from '../types/topic.types';
import { getTopicErrorMessage, topicApi } from '../services/topicApi';

const INITIAL_VALUES: TopicFormValues = {
  batchId: '',
  titleVi: '',
  titleEn: '',
  description: '',
  requirements: '',
  studentNumber: 2,
};

interface SaveProposal {
  id: string | null;
  values: TopicFormValues;
  submitImmediately: boolean;
  hasChanges: boolean;
}

const INITIAL_FILTERS: TopicFilters = { keyword: '', batchId: '', status: '' };

const textareaClass =
  'min-h-28 w-full rounded-lg border border-input bg-slate-50/70 px-3 py-2 ' +
  'text-sm shadow-sm placeholder:text-muted-foreground ' +
  'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring ' +
  'disabled:cursor-not-allowed disabled:opacity-50';

function toFormValues(topic: Topic): TopicFormValues {
  return {
    batchId: topic.batchId,
    titleVi: topic.titleVi,
    titleEn: topic.titleEn ?? '',
    description: topic.description ?? '',
    requirements: topic.requirements ?? '',
    studentNumber: topic.studentNumber === 1 ? 1 : 2,
  };
}

export function TopicProposePage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const [selectedTopic, setSelectedTopic] = useState<Topic | null>(null);
  const [values, setValues] = useState<TopicFormValues>(INITIAL_VALUES);
  const [dirty, setDirty] = useState(false);
  const [notice, setNotice] = useState('');
  const [formError, setFormError] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [draftFilters, setDraftFilters] = useState<TopicFilters>(INITIAL_FILTERS);
  const [filters, setFilters] = useState<TopicFilters>(INITIAL_FILTERS);

  const editable = !selectedTopic || canEditTopic(selectedTopic.status);

  const batchesQuery = useQuery({
    queryKey: ['topic-batches', user?.id],
    queryFn: ({ signal }) => topicApi.getBatches(signal),
  });

  const topicsQuery = useQuery({
    queryKey: ['my-proposed-topics', user?.id, filters, pageNumber, pageSize],
    enabled: Boolean(user?.id),
    queryFn: ({ signal }) =>
      topicApi.getMyTopics({ ...filters, advisorId: user!.id, pageNumber, pageSize }, signal),
  });

  const statisticsQuery = useQuery({
    queryKey: ['my-proposed-topics', user?.id, 'statistics', filters.batchId],
    enabled: Boolean(user?.id),
    queryFn: ({ signal }) => topicApi.getStatistics(user!.id, filters.batchId || undefined, signal),
  });
  const statistics = statisticsQuery.isError ? undefined : statisticsQuery.data;
  const formatCount = (count: number | undefined) =>
    count === undefined ? '—' : count.toLocaleString('vi-VN');

  const detailMutation = useMutation({
    mutationFn: (id: string) => topicApi.getDetail(id),
    onSuccess: (topic) => {
      setSelectedTopic(topic);
      setValues(toFormValues(topic));
      setDirty(false);
      setIsFormOpen(true);
    },
  });

  const saveMutation = useMutation({
    mutationFn: ({ id, values, submitImmediately, hasChanges }: SaveProposal) => {
      // An unchanged saved draft only needs the submit endpoint. Edited content
      // is saved and submitted atomically through PUT to avoid partial success.
      if (id && submitImmediately && !hasChanges) return topicApi.submit(id);
      return topicApi.save(id, values, submitImmediately);
    },
    onSuccess: async (topic, { submitImmediately, id }) => {
      setSelectedTopic(topic);
      setValues(toFormValues(topic));
      setDirty(false);
      setFormError('');
      setIsFormOpen(false);
      setNotice(
        submitImmediately
          ? 'Đã gửi đề tài đến Khoa để thẩm định.'
          : topic.status === 'NeedRevision'
            ? 'Đã lưu chỉnh sửa. Bạn có thể gửi duyệt lại khi sẵn sàng.'
            : 'Đã lưu bản nháp đề tài.',
      );
      if (!id) setPageNumber(1);
      await queryClient.invalidateQueries({
        queryKey: ['my-proposed-topics'],
      });
    },
  });

  const busy = saveMutation.isPending || detailMutation.isPending;
  const batches = batchesQuery.data ?? [];
  const selectedBatch = batches.find((batch) => batch.id === values.batchId);

  function updateField<K extends keyof TopicFormValues>(key: K, value: TopicFormValues[K]) {
    setValues((previous) => ({ ...previous, [key]: value }));
    setDirty(true);
    setNotice('');
    setFormError('');
    saveMutation.reset();
    detailMutation.reset();
  }

  function openTopic(topic: Topic | null) {
    if (busy) return;

    if (dirty && !window.confirm('Bỏ các thay đổi chưa lưu để tiếp tục?')) {
      return;
    }

    setNotice('');
    setFormError('');
    saveMutation.reset();
    detailMutation.reset();

    if (topic) {
      detailMutation.mutate(topic.id);
    } else {
      setSelectedTopic(null);
      setValues({ ...INITIAL_VALUES, batchId: filters.batchId });
      setDirty(false);
      setIsFormOpen(true);
    }
  }

  function closeForm() {
    if (busy) return;
    if (dirty && !window.confirm('Bỏ các thay đổi chưa lưu để đóng cửa sổ?')) return;
    setDirty(false);
    setIsFormOpen(false);
  }

  function handleApplyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFilters({ ...draftFilters, keyword: draftFilters.keyword.trim() });
    setPageNumber(1);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editable || busy) return;

    const submitter = (event.nativeEvent as SubmitEvent).submitter;
    const submitImmediately =
      submitter instanceof HTMLButtonElement && submitter.value === 'submit';

    if (!values.batchId) {
      setFormError('Vui lòng chọn đợt KLTN.');
      return;
    }
    if (!values.titleVi.trim()) {
      setFormError('Vui lòng nhập tên đề tài tiếng Việt.');
      return;
    }
    if (values.titleVi.trim().length > 300) {
      setFormError('Tên tiếng Việt không được vượt quá 300 ký tự.');
      return;
    }
    if (values.titleEn.trim().length > 350) {
      setFormError('Tên tiếng Anh không được vượt quá 350 ký tự.');
      return;
    }
    if (values.studentNumber !== 1 && values.studentNumber !== 2) {
      setFormError('Chỉ tiêu sinh viên chỉ được chọn 1 hoặc 2.');
      return;
    }

    if (
      submitImmediately &&
      !window.confirm(
        'Gửi đề tài đến Khoa thẩm định? Sau khi gửi, bạn chỉ có thể sửa khi Khoa yêu cầu chỉnh sửa.',
      )
    ) {
      return;
    }

    setNotice('');
    setFormError('');
    saveMutation.mutate({
      id: selectedTopic?.id ?? null,
      values: { ...values },
      submitImmediately,
      hasChanges: dirty,
    });
  }

  return (
    <div className="min-w-0 space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">Đề xuất đề tài</h1>
          <p className="mt-2 text-sm text-slate-500">
            Quản lý đề xuất, lưu nháp và theo dõi kết quả thẩm định của Khoa.
          </p>
        </div>
        <Button
          type="button"
          leftIcon={<FilePlus2 />}
          disabled={busy}
          onClick={() => openTopic(null)}
          className="h-10 rounded-lg px-4 shadow-sm"
        >
          Tạo đề tài
        </Button>
      </div>

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
        <Dialog title="Đề xuất đề tài" busy={busy} onClose={closeForm} className="max-w-5xl">
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
                {!editable && (
                  <p className="mb-5 rounded-lg bg-blue-50 p-3 text-sm text-blue-700">
                    Đề tài ở trạng thái hiện tại chỉ được xem nội dung.
                  </p>
                )}

                {selectedTopic?.status === 'NeedRevision' && (
                  <p className="mb-5 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
                    Khoa yêu cầu chỉnh sửa. Bạn có thể cập nhật nội dung và gửi duyệt lại.
                  </p>
                )}

                <form onSubmit={handleSubmit} className="space-y-5">
                  <fieldset disabled={busy || !editable} className="min-w-0 space-y-5">
                    <div className="space-y-2">
                      <label id="topic-batch-label" className="text-sm font-medium text-slate-700">
                        Đợt KLTN <span className="text-rose-600">*</span>
                      </label>
                      <Select<string>
                        triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
                        aria-labelledby="topic-batch-label"
                        value={values.batchId}
                        options={batches.map((batch) => ({
                          value: batch.id,
                          label: batch.batchName,
                          description: `${batch.academicYear} · HK${batch.semester}`,
                        }))}
                        onChange={(value: string) => updateField('batchId', value)}
                        placeholder={
                          batchesQuery.isPending ? 'Đang tải danh sách đợt...' : 'Chọn đợt KLTN'
                        }
                        searchable
                        disabled={
                          busy ||
                          !editable ||
                          Boolean(selectedTopic) ||
                          batchesQuery.isPending ||
                          batchesQuery.isError
                        }
                      />
                      {selectedTopic && (
                        <p className="text-xs text-slate-500">
                          Đợt KLTN được giữ nguyên sau khi tạo đề tài.
                        </p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <label
                        htmlFor="topic-title-vi"
                        className="text-sm font-medium text-slate-700"
                      >
                        Tên đề tài tiếng Việt <span className="text-rose-600">*</span>
                      </label>
                      <Input
                        className="h-10 rounded-lg bg-slate-50/70 shadow-none focus-visible:bg-white"
                        id="topic-title-vi"
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
                        className="text-sm font-medium text-slate-700"
                      >
                        Tên đề tài tiếng Anh
                      </label>
                      <Input
                        className="h-10 rounded-lg bg-slate-50/70 shadow-none focus-visible:bg-white"
                        id="topic-title-en"
                        value={values.titleEn}
                        onChange={(event) => updateField('titleEn', event.target.value)}
                        maxLength={350}
                        placeholder="Nhập tên tiếng Anh nếu có"
                      />
                    </div>

                    <div className="space-y-2">
                      <label
                        htmlFor="topic-description"
                        className="text-sm font-medium text-slate-700"
                      >
                        Mục tiêu và mô tả
                      </label>
                      <textarea
                        id="topic-description"
                        className={textareaClass}
                        value={values.description}
                        onChange={(event) => updateField('description', event.target.value)}
                        placeholder="Trình bày mục tiêu, phạm vi nghiên cứu và sản phẩm dự kiến..."
                        rows={5}
                      />
                    </div>

                    <div className="space-y-2">
                      <label
                        htmlFor="topic-requirements"
                        className="text-sm font-medium text-slate-700"
                      >
                        Yêu cầu công nghệ và kiến thức
                      </label>
                      <textarea
                        id="topic-requirements"
                        className={textareaClass}
                        value={values.requirements}
                        onChange={(event) => updateField('requirements', event.target.value)}
                        placeholder="Công nghệ dự kiến, kiến thức nền tảng cần có..."
                        rows={3}
                      />
                    </div>

                    <div className="space-y-2">
                      <label
                        id="topic-capacity-label"
                        className="text-sm font-medium text-slate-700"
                      >
                        Số sinh viên tối đa
                      </label>
                      <Select<number>
                        triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
                        aria-labelledby="topic-capacity-label"
                        value={values.studentNumber}
                        options={[
                          { value: 1, label: '1 sinh viên' },
                          { value: 2, label: '2 sinh viên' },
                        ]}
                        disabled={busy || !editable}
                        onChange={(value: number) =>
                          updateField('studentNumber', value === 1 ? 1 : 2)
                        }
                      />
                    </div>
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
                        disabled={busy}
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
                        disabled={busy}
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
                <CardTitle className="text-base">Trước khi gửi Khoa</CardTitle>
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
                      {selectedBatch?.batchName ?? 'Chưa chọn đợt'}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs text-slate-500">Chỉ tiêu</dt>
                    <dd className="mt-1 text-slate-800">Tối đa {values.studentNumber} sinh viên</dd>
                  </div>
                </dl>

                <div className="border-t border-slate-100 pt-4">
                  <p className="font-medium text-slate-800">Kiểm tra nội dung</p>
                  <ul className="mt-2 list-disc space-y-2 pl-4 text-xs leading-5 text-slate-500">
                    <li>Tên Việt và Anh thống nhất về nội dung.</li>
                    <li>Mục tiêu và sản phẩm dự kiến có thể đánh giá.</li>
                    <li>Chỉ tiêu phù hợp với phạm vi đề tài.</li>
                    <li>Sau khi gửi, đề tài chuyển sang chờ Khoa duyệt.</li>
                  </ul>
                </div>

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
        <Button
          type="submit"
          variant="soft"
          leftIcon={<SlidersHorizontal />}
          className="h-10 rounded-lg px-5"
        >
          Áp dụng
        </Button>
      </form>

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
        <CardHeader className="grid min-h-[104px] grid-cols-1 items-center gap-4 space-y-0 border-b border-slate-100 bg-gradient-to-r from-slate-50/80 to-white px-5 py-4 xl:grid-cols-[minmax(0,1fr)_420px]">
          <div className="space-y-1">
            <CardTitle className="text-base text-slate-900">Đề tài của tôi</CardTitle>
            <p className="text-xs text-slate-500">
              {filters.batchId
                ? 'Thống kê tất cả trạng thái trong đợt đang chọn'
                : 'Nội dung đề xuất và trạng thái thẩm định'}
            </p>
          </div>
          <section
            className="w-full"
            aria-label="Thống kê đề tài"
            aria-busy={statisticsQuery.isFetching}
          >
            <StatisticsBar
              items={[
                {
                  key: 'total',
                  label: 'Tổng đề tài',
                  value: formatCount(statistics?.total),
                  icon: <FileText />,
                  tone: 'primary',
                },
                {
                  key: 'submitted',
                  label: 'Chờ duyệt',
                  value: formatCount(statistics?.submitted),
                  icon: <Clock3 />,
                  tone: 'muted',
                },
                {
                  key: 'revision',
                  label: 'Cần chỉnh sửa',
                  value: formatCount(statistics?.needRevision),
                  icon: <RotateCcw />,
                  tone: 'warning',
                },
              ]}
            />
            {statisticsQuery.isPending && (
              <p role="status" className="mt-2 text-xs text-muted-foreground">
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
        </CardHeader>

        <div className="h-[min(60vh,40rem)] min-h-[20rem] overflow-auto">
          {topicsQuery.isPending ? (
            <p
              role="status"
              className="flex h-full items-center justify-center p-8 text-center text-sm text-slate-500"
            >
              Đang tải danh sách đề tài...
            </p>
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
              pageNumber={topicsQuery.data.pageNumber}
              pageSize={topicsQuery.data.pageSize}
              busy={busy}
              onOpen={openTopic}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-3 p-8 text-center">
              <FileText className="size-9 text-slate-300" aria-hidden="true" />
              <p className="text-sm text-slate-500">
                {filters.keyword || filters.status || filters.batchId
                  ? 'Không có đề tài phù hợp với bộ lọc.'
                  : 'Chưa có đề tài. Tạo đề xuất đầu tiên để bắt đầu.'}
              </p>
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
