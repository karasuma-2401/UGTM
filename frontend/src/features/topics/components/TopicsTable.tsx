import { useId, useRef } from 'react';
import { Copy, MoreHorizontal, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Skeleton } from '@/components/ui/skeleton';
import { DataTable, type DataTableColumn, type DataTableProps } from '@/components/ui/data-table';
import type { TableSortDirection } from '@/components/ui/table';
import { canEditTopic, type Topic, type TopicBatch } from '../types/topic.types';
import { TopicStatusBadge } from './TopicStatusBadge';

interface TopicActionMenuProps {
  topic: Topic;
  busy: boolean;
  onOpen: (topic: Topic) => void;
  onCopy: (topic: Topic) => void;
  onSubmit: (topic: Topic) => void;
}

function TopicActionMenu({ topic, busy, onOpen, onCopy, onSubmit }: TopicActionMenuProps) {
  const id = useId();
  const popup = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const canEdit = canEditTopic(topic.status);

  const closeAndRun = (action: (topic: Topic) => void) => {
    popup.current?.hidePopover?.();
    action(topic);
  };

  return (
    <div className="flex items-center justify-center gap-1.5">
      <Button
        type="button"
        variant="outline"
        size="sm"
        className="h-8 rounded-md border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-sm hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900"
        disabled={busy}
        onClick={() => onOpen(topic)}
        aria-label={`Xem chi tiết đề tài ${topic.titleVi}`}
      >
        Xem
      </Button>
      <button
        ref={trigger}
        type="button"
        disabled={busy}
        popoverTarget={id}
        aria-label={`Thao tác với ${topic.titleVi}`}
        className="flex size-8 items-center justify-center rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 focus-visible:outline-primary disabled:opacity-40 transition-colors cursor-pointer"
      >
        <MoreHorizontal className="size-4" />
      </button>
      <div
        ref={popup}
        id={id}
        popover="auto"
        aria-label={`Thao tác với ${topic.titleVi}`}
        className="fixed inset-auto m-0 w-44 rounded-xl border border-slate-200/90 bg-white p-1.5 text-left shadow-lg z-50 animate-in fade-in-50 zoom-in-95 duration-150"
        onToggle={(event) => {
          if ((event as unknown as { newState?: string }).newState !== 'open') return;
          const rect = trigger.current?.getBoundingClientRect();
          const el = popup.current;
          if (!rect || !el) return;
          el.style.left = Math.max(8, Math.min(rect.right - 176, window.innerWidth - 184)) + 'px';
          el.style.top =
            Math.max(
              8,
              rect.bottom + el.offsetHeight + 8 > window.innerHeight
                ? rect.top - el.offsetHeight - 4
                : rect.bottom + 4,
            ) + 'px';
          el.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
        }}
      >
        <button
          type="button"
          disabled={busy}
          onClick={() => closeAndRun(onCopy)}
          className="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 hover:text-slate-900 focus-visible:outline-primary transition-colors cursor-pointer"
          aria-label={`Nhân bản đề tài ${topic.titleVi}`}
        >
          <Copy className="size-3.5 text-slate-500" />
          <span>Nhân bản</span>
        </button>
        {canEdit && (
          <button
            type="button"
            disabled={busy}
            onClick={() => closeAndRun(onSubmit)}
            className="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-xs font-medium text-emerald-700 hover:bg-emerald-50 focus-visible:outline-primary transition-colors cursor-pointer"
            aria-label={`Gửi duyệt đề tài ${topic.titleVi}`}
          >
            <Send className="size-3.5 text-emerald-600" />
            <span>Gửi duyệt</span>
          </button>
        )}
      </div>
    </div>
  );
}

interface TopicsTableProps extends Pick<
  DataTableProps<Topic>,
  'loading' | 'error' | 'empty' | 'pagination'
> {
  topics: Topic[];
  batches: TopicBatch[];
  batchesLoading?: boolean;
  pageNumber: number;
  pageSize: number;
  busy: boolean;
  onOpen: (topic: Topic) => void;
  onCopy: (topic: Topic) => void;
  onSubmit: (topic: Topic) => void;
  selectedRowId?: string | null;
  onSelectRow?: (id: string) => void;
  selectedIds?: string[];
  onToggleSelect?: (id: string) => void;
  onSelectAll?: (ids: string[]) => void;
  sortField?: 'title' | 'studentNumber' | 'createdAt' | null;
  sortOrder?: TableSortDirection;
  onSortChange?: (field: 'title' | 'studentNumber' | 'createdAt') => void;
}

export function TopicsTable({
  topics,
  batches,
  batchesLoading = false,
  pageNumber,
  pageSize,
  busy,
  onOpen,
  onCopy,
  onSubmit,
  selectedRowId,
  onSelectRow,
  selectedIds,
  onToggleSelect,
  onSelectAll,
  sortField,
  sortOrder,
  onSortChange,
  loading,
  error,
  empty,
  pagination,
}: TopicsTableProps) {
  const batchNames = new Map(batches.map((batch) => [batch.id, batch.batchName]));

  const showCheckbox = Boolean(selectedIds && onToggleSelect);
  const allSelected =
    showCheckbox && topics.length > 0 && topics.every((topic) => selectedIds!.includes(topic.id));
  const actions = (topic: Topic) => (
    <TopicActionMenu
      topic={topic}
      busy={busy}
      onOpen={onOpen}
      onCopy={onCopy}
      onSubmit={onSubmit}
    />
  );
  const columns: DataTableColumn<Topic, 'title' | 'studentNumber' | 'createdAt'>[] = [
    {
      id: 'number',
      header: 'STT',
      widthClassName: 'w-14',
      align: 'center',
      headerClassName: 'px-3 py-3.5',
      cellClassName: 'px-3 py-3',
      skeleton: { variant: 'number' },
      cell: (_, index) => (
        <span className="text-xs tabular-nums text-slate-400 font-medium">
          {(pageNumber - 1) * pageSize + index + 1}
        </span>
      ),
    },
    ...(showCheckbox
      ? [
          {
            id: 'selection',
            widthClassName: 'w-12',
            align: 'center' as const,
            skeleton: { variant: 'checkbox' as const, label: 'Chọn' },
            header: (
              <Checkbox
                aria-label="Chọn tất cả đề tài trên trang này"
                checked={allSelected ? true : selectedIds?.length ? 'indeterminate' : false}
                disabled={busy}
                onCheckedChange={(checked) =>
                  onSelectAll?.(checked ? topics.map((topic) => topic.id) : [])
                }
              />
            ),
            cell: (topic: Topic) => (
              <Checkbox
                checked={selectedIds!.includes(topic.id)}
                aria-label={`Chọn đề tài ${topic.titleVi}`}
                disabled={busy}
                onCheckedChange={() => onToggleSelect?.(topic.id)}
              />
            ),
          },
        ]
      : []),
    {
      id: 'title',
      header: 'Đề tài',
      sortKey: 'title',
      skeleton: { variant: 'topic' },
      cell: (topic) => (
        <div className="space-y-1 min-w-0">
          <span
            className="line-clamp-2 block text-sm font-semibold text-slate-900 leading-snug"
            title={topic.titleVi}
          >
            {topic.titleVi}
          </span>
          {topic.titleEn ? (
            <span
              className="truncate block text-xs text-slate-500 font-normal"
              title={topic.titleEn}
            >
              {topic.titleEn}
            </span>
          ) : (
            <span className="block text-xs text-slate-400 italic">Chưa có tên tiếng Anh</span>
          )}
        </div>
      ),
    },
    {
      id: 'batch',
      header: 'Đợt khóa luận',
      widthClassName: 'w-36',
      cellClassName: 'px-4 py-3 text-xs text-slate-600',
      cell: (topic) =>
        batchNames.get(topic.batchId) ||
        (batchesLoading ? <Skeleton className="h-4 w-28 max-w-full" /> : '—'),
    },
    {
      id: 'capacity',
      header: 'Chỉ tiêu',
      widthClassName: 'w-28',
      sortKey: 'studentNumber',
      cellClassName: 'px-4 py-3 text-xs font-medium text-slate-700 tabular-nums',
      cell: (topic) => (topic.studentNumber ? `${topic.studentNumber} sinh viên` : '—'),
    },
    {
      id: 'createdAt',
      header: 'Ngày tạo',
      widthClassName: 'w-32',
      sortKey: 'createdAt',
      cellClassName: 'px-4 py-3 text-xs text-slate-500 tabular-nums',
      cell: (topic) => new Date(topic.createdAt).toLocaleDateString('vi-VN'),
    },
    {
      id: 'status',
      header: 'Trạng thái',
      widthClassName: 'w-36',
      skeleton: { variant: 'badge' },
      cell: (topic) => <TopicStatusBadge status={topic.status} />,
    },
    {
      id: 'actions',
      header: 'Thao tác',
      widthClassName: 'w-32',
      align: 'center',
      cellClassName: 'px-3 py-3',
      skeleton: { variant: 'actions', actionCount: 2 },
      cell: actions,
    },
  ];
  const styledColumns = columns.map((column) => ({
    headerClassName: 'px-4 py-3.5',
    cellClassName: 'px-4 py-3',
    ...column,
  }));

  return (
    <DataTable
      data={topics}
      columns={styledColumns}
      getRowId={(topic) => topic.id}
      ariaLabel="Danh sách đề tài"
      selectedRowId={selectedRowId}
      onRowSelect={onSelectRow}
      onRowOpen={onOpen}
      isRowSelected={
        selectedIds
          ? (topic) => selectedIds.includes(topic.id) || selectedRowId === topic.id
          : undefined
      }
      busy={busy}
      loading={loading}
      error={error}
      empty={empty}
      pagination={pagination}
      tableClassName="min-w-[960px]"
      rowClassName="h-[68px]"
      sort={sortField && sortOrder ? { field: sortField, direction: sortOrder } : undefined}
      onSortChange={onSortChange}
      renderMobileItem={(topic) => (
        <>
          <TopicStatusBadge status={topic.status} />
          <p className="text-sm font-semibold leading-6 text-slate-900">{topic.titleVi}</p>
          {topic.titleEn && <p className="text-sm text-slate-500">{topic.titleEn}</p>}
          <p className="text-xs text-slate-500">
            {batchNames.get(topic.batchId) ||
              (batchesLoading ? 'Đang tải tên đợt…' : 'Chưa có tên đợt')}
          </p>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-xs text-slate-600">
              Chỉ tiêu: {topic.studentNumber ?? '—'} sinh viên ·{' '}
              {new Date(topic.createdAt).toLocaleDateString('vi-VN')}
            </p>
            {actions(topic)}
          </div>
        </>
      )}
    />
  );
}
