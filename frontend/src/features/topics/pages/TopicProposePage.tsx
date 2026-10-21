import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FilePlus2, Save, Send } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Badge, type BadgeProps } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { TablePagination } from '@/components/ui/table-pagination';
import { useAuth } from '@/shared/auth';

import {
  canEditTopic,
  TOPIC_STATUS_LABELS,
  type Topic,
  type TopicFormValues,
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

const STATUS_VARIANTS: Record<TopicStatus, NonNullable<BadgeProps['variant']>> = {
  Draft: 'secondary',
  Submitted: 'info',
  NeedRevision: 'warning',
  Approved: 'success',
  Rejected: 'error',
  Published: 'success',
  Closed: 'secondary',
  Archived: 'secondary',
};

const textareaClass =
  'min-h-28 w-full rounded-md border border-input bg-white px-3 py-2 ' +
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
  const pageSize = 10;

  const editable = !selectedTopic || canEditTopic(selectedTopic.status);

  const batchesQuery = useQuery({
    queryKey: ['topic-batches', user?.id],
    queryFn: ({ signal }) => topicApi.getBatches(signal),
  });

  const topicsQuery = useQuery({
    queryKey: ['my-proposed-topics', user?.id, pageNumber],
    enabled: Boolean(user?.id),
    queryFn: ({ signal }) =>
      topicApi.getMyTopics({ advisorId: user!.id, pageNumber, pageSize }, signal),
  });

  const detailMutation = useMutation({
    mutationFn: (id: string) => topicApi.getDetail(id),
    onSuccess: (topic) => {
      setSelectedTopic(topic);
      setValues(toFormValues(topic));
      setDirty(false);
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
      setValues({ ...INITIAL_VALUES });
      setDirty(false);
    }
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
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">Đề xuất đề tài</h1>
          <p className="mt-2 text-sm text-slate-500">
            Soạn thảo, lưu nháp và gửi đề tài đến Khoa thẩm định.
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          leftIcon={<FilePlus2 />}
          disabled={busy}
          onClick={() => openTopic(null)}
        >
          Tạo đề tài mới
        </Button>
      </div>

      {notice && (
        <div
          role="status"
          className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700"
        >
          {notice}
        </div>
      )}

      {detailMutation.isPending && (
        <p role="status" className="text-sm text-slate-500">
          Đang tải nội dung đề tài...
        </p>
      )}
      {detailMutation.isError && (
        <p role="alert" className="text-sm text-rose-600">
          {getTopicErrorMessage(detailMutation.error)}
        </p>
      )}

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_300px]">
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
              <Badge variant={STATUS_VARIANTS[selectedTopic?.status ?? 'Draft']}>
                {TOPIC_STATUS_LABELS[selectedTopic?.status ?? 'Draft']}
              </Badge>
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
                  <label htmlFor="topic-title-vi" className="text-sm font-medium text-slate-700">
                    Tên đề tài tiếng Việt <span className="text-rose-600">*</span>
                  </label>
                  <Input
                    id="topic-title-vi"
                    value={values.titleVi}
                    onChange={(event) => updateField('titleVi', event.target.value)}
                    maxLength={300}
                    required
                    placeholder="Nhập tên đề tài tiếng Việt"
                  />
                </div>

                <div className="space-y-2">
                  <label htmlFor="topic-title-en" className="text-sm font-medium text-slate-700">
                    Tên đề tài tiếng Anh
                  </label>
                  <Input
                    id="topic-title-en"
                    value={values.titleEn}
                    onChange={(event) => updateField('titleEn', event.target.value)}
                    maxLength={350}
                    placeholder="Nhập tên tiếng Anh nếu có"
                  />
                </div>

                <div className="space-y-2">
                  <label htmlFor="topic-description" className="text-sm font-medium text-slate-700">
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
                  <label id="topic-capacity-label" className="text-sm font-medium text-slate-700">
                    Số sinh viên tối đa
                  </label>
                  <Select<number>
                    aria-labelledby="topic-capacity-label"
                    value={values.studentNumber}
                    options={[
                      { value: 1, label: '1 sinh viên' },
                      { value: 2, label: '2 sinh viên' },
                    ]}
                    disabled={busy || !editable}
                    onChange={(value: number) => updateField('studentNumber', value === 1 ? 1 : 2)}
                  />
                </div>
              </fieldset>

              {(formError || saveMutation.isError) && (
                <p role="alert" className="text-sm text-rose-600">
                  {formError || getTopicErrorMessage(saveMutation.error)}
                </p>
              )}

              {editable && (
                <div className="flex flex-wrap gap-3 border-t border-slate-100 pt-5">
                  <Button
                    type="submit"
                    name="action"
                    value="draft"
                    variant="outline"
                    leftIcon={<Save />}
                    disabled={busy}
                    isLoading={saveMutation.isPending && !saveMutation.variables?.submitImmediately}
                  >
                    {selectedTopic?.status === 'NeedRevision' ? 'Lưu chỉnh sửa' : 'Lưu nháp'}
                  </Button>
                  <Button
                    type="submit"
                    name="action"
                    value="submit"
                    leftIcon={<Send />}
                    disabled={busy}
                    isLoading={
                      saveMutation.isPending && saveMutation.variables?.submitImmediately === true
                    }
                  >
                    {selectedTopic?.status === 'NeedRevision'
                      ? 'Gửi duyệt lại'
                      : 'Gửi Khoa thẩm định'}
                  </Button>
                </div>
              )}
            </form>
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
              <p className="text-amber-700">Chưa có đợt KLTN. Vui lòng liên hệ đơn vị quản lý.</p>
            )}
          </CardContent>
        </Card>
      </div>

      <Card className="overflow-hidden">
        <CardHeader>
          <CardTitle className="text-base">Đề tài của tôi</CardTitle>
        </CardHeader>

        {topicsQuery.isPending ? (
          <p role="status" className="px-6 pb-6 text-sm text-slate-500">
            Đang tải danh sách đề tài...
          </p>
        ) : topicsQuery.isError ? (
          <div role="alert" className="space-y-3 px-6 pb-6">
            <p className="text-sm text-rose-600">{getTopicErrorMessage(topicsQuery.error)}</p>
            <Button type="button" variant="outline" onClick={() => void topicsQuery.refetch()}>
              Thử lại
            </Button>
          </div>
        ) : !topicsQuery.data?.items.length ? (
          <p className="px-6 pb-6 text-sm text-slate-500">
            Chưa có đề tài. Điền nội dung phía trên để tạo đề xuất đầu tiên.
          </p>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[640px] text-left text-sm">
                <thead className="border-y border-slate-100 bg-slate-50 text-xs text-slate-500">
                  <tr>
                    <th className="px-6 py-3 font-medium">Đề tài</th>
                    <th className="px-4 py-3 font-medium">Chỉ tiêu</th>
                    <th className="px-4 py-3 font-medium">Trạng thái</th>
                    <th className="px-6 py-3 text-right font-medium">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {topicsQuery.data.items.map((topic) => (
                    <tr
                      key={topic.id}
                      className={
                        selectedTopic?.id === topic.id ? 'bg-blue-50/60' : 'hover:bg-slate-50/60'
                      }
                    >
                      <td className="px-6 py-4">
                        <p className="font-medium text-slate-800">{topic.titleVi}</p>
                        <p className="mt-1 text-xs text-slate-500">
                          {new Date(topic.createdAt).toLocaleDateString('vi-VN')}
                        </p>
                      </td>
                      <td className="px-4 py-4">{topic.studentNumber ?? '—'} SV</td>
                      <td className="px-4 py-4">
                        <Badge variant={STATUS_VARIANTS[topic.status]}>
                          {TOPIC_STATUS_LABELS[topic.status]}
                        </Badge>
                      </td>
                      <td className="px-6 py-4 text-right">
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          disabled={busy}
                          onClick={() => openTopic(topic)}
                        >
                          {canEditTopic(topic.status) ? 'Chỉnh sửa' : 'Xem'}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <TablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
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
