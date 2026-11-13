import { ClipboardCheck, MessageSquareText, Users } from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { stripRichText } from '@/components/ui/rich-text-content';
import { Skeleton } from '@/components/ui/skeleton';
import type { Topic, TopicBatch } from '../types/topic.types';
import { TopicStatusBadge } from './TopicStatusBadge';

interface TopicReviewTableProps {
  topics: Topic[];
  batches: TopicBatch[];
  advisorNames: Record<string, string>;
  advisorsLoading?: boolean;
  batchesLoading?: boolean;
  selected: Topic[];
  selectedRowId?: string | null;
  onSelectRow?: (id: string) => void;
  onActiveRowChange?: (id: string | null) => void;
  pageNumber: number;
  pageSize: number;
  busy: boolean;
  onSelect: (topics: Topic[]) => void;
  onOpen: (topic: Topic) => void;
}

function formatDate(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('vi-VN');
}

export function TopicReviewTable({
  topics,
  batches,
  advisorNames,
  advisorsLoading = false,
  batchesLoading = false,
  selected,
  selectedRowId,
  onSelectRow,
  onActiveRowChange,
  pageNumber,
  pageSize,
  busy,
  onSelect,
  onOpen,
}: TopicReviewTableProps) {
  const batchById = new Map(batches.map((batch) => [batch.id, batch.batchName]));
  const selectedIds = new Set(selected.map((topic) => topic.id));
  const selectable = topics.filter((topic) => topic.status === 'Approved');
  const selectedSelectableCount = selectable.filter((topic) => selectedIds.has(topic.id)).length;
  const allSelected = selectable.length > 0 && selectedSelectableCount === selectable.length;
  const someSelected = selectedSelectableCount > 0 && !allSelected;

  function setActiveRow(nextId: string | null) {
    if (onActiveRowChange) {
      onActiveRowChange(nextId);
      return;
    }
    if (!onSelectRow) return;
    if (nextId && selectedRowId !== nextId) {
      onSelectRow(nextId);
    } else if (!nextId && selectedRowId) {
      onSelectRow(selectedRowId);
    }
  }

  function toggleOne(topic: Topic, checked: boolean) {
    if (!checked) {
      const nextSelected = selected.filter((item) => item.id !== topic.id);
      onSelect(nextSelected);
      if (selectedRowId === topic.id) {
        const pageIds = new Set(topics.map((item) => item.id));
        const remainingOnPage = nextSelected.filter((item) => pageIds.has(item.id));
        setActiveRow(remainingOnPage[remainingOnPage.length - 1]?.id ?? null);
      }
      return;
    }
    if (topic.status !== 'Approved') return;
    onSelect(selectedIds.has(topic.id) ? selected : [...selected, topic]);
    setActiveRow(topic.id);
  }

  function toggleAll(checked: boolean) {
    const pageIds = new Set(topics.map((topic) => topic.id));
    if (!checked) {
      onSelect(selected.filter((topic) => !pageIds.has(topic.id)));
      if (selectedRowId && pageIds.has(selectedRowId)) {
        setActiveRow(null);
      }
      return;
    }
    const existing = new Set(selected.map((topic) => topic.id));
    onSelect([...selected, ...selectable.filter((topic) => !existing.has(topic.id))]);
    if (!selectedRowId || !selectable.some((topic) => topic.id === selectedRowId)) {
      setActiveRow(selectable[0]?.id ?? null);
    }
  }

  function renderAdvisor(advisorId: string) {
    const name = advisorNames[advisorId];
    if (name) {
      return (
        <div className="flex min-w-0 items-center gap-2.5">
          <Avatar name={name} className="size-7 text-[10px] ring-0" />
          <span className="truncate text-xs font-medium text-slate-700" title={name}>
            {name}
          </span>
        </div>
      );
    }
    if (advisorsLoading) {
      return (
        <div className="flex items-center gap-2.5">
          <Skeleton className="size-7 shrink-0 rounded-full" />
          <Skeleton className="h-4 w-28 max-w-full" />
        </div>
      );
    }
    return (
      <span className="text-xs text-slate-400" title={advisorId}>
        Chưa rõ GVHD
      </span>
    );
  }

  function renderBatch(batchId: string) {
    const name = batchById.get(batchId);
    if (name) {
      return (
        <span className="line-clamp-1 text-xs font-medium text-slate-600" title={name}>
          {name}
        </span>
      );
    }
    if (batchesLoading) {
      return <Skeleton className="h-4 w-28 max-w-full" />;
    }
    return (
      <span className="text-xs text-slate-400" title={batchId}>
        Chưa rõ đợt
      </span>
    );
  }

  function renderAction(topic: Topic) {
    if (topic.status === 'Submitted') {
      return (
        <Button
          type="button"
          variant="outline"
          size="sm"
          aria-label={`Thẩm định ${topic.titleVi}`}
          leftIcon={<ClipboardCheck className="size-3.5" />}
          onClick={() => onOpen(topic)}
          className="h-8 rounded-lg border-blue-200 bg-blue-50/70 px-3 text-xs font-medium text-primary shadow-2xs hover:border-blue-300 hover:bg-blue-100/70 hover:text-blue-700"
        >
          Thẩm định
        </Button>
      );
    }
    return (
      <Button
        type="button"
        variant="outline"
        size="sm"
        aria-label={`Xem ${topic.titleVi}`}
        onClick={() => onOpen(topic)}
        className="h-8 rounded-lg border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-2xs hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900"
      >
        Xem
      </Button>
    );
  }

  const columns: DataTableColumn<Topic>[] = [
    {
      id: 'selection',
      widthClassName: 'w-12',
      align: 'center',
      headerClassName: 'px-3 py-3.5 text-center',
      cellClassName: 'px-3 py-3.5 text-center',
      skeleton: { variant: 'checkbox', label: 'Chọn' },
      header: (
        <Checkbox
          aria-label="Chọn tất cả đề tài đã duyệt trên trang"
          checked={allSelected ? true : someSelected ? 'indeterminate' : false}
          disabled={busy || !selectable.length}
          title={
            !selectable.length
              ? 'Không có đề tài đã duyệt (Chờ công bố) trên trang này'
              : `Chọn tất cả ${selectable.length} đề tài đã duyệt trên trang`
          }
          onCheckedChange={(value) => toggleAll(value === true)}
        />
      ),
      cell: (topic) => {
        const checked = selectedIds.has(topic.id);
        const isApprovable = topic.status === 'Approved';
        return (
          <Checkbox
            aria-label={`Chọn đề tài ${topic.titleVi}`}
            checked={checked}
            disabled={busy || !isApprovable}
            title={
              isApprovable
                ? 'Chọn đề tài để công bố'
                : 'Chỉ chọn công bố các đề tài đã được duyệt'
            }
            onCheckedChange={(value) => toggleOne(topic, value === true)}
          />
        );
      },
    },
    {
      id: 'number',
      header: '#',
      widthClassName: 'w-12',
      align: 'center',
      headerClassName: 'px-2 py-3.5 text-center',
      cellClassName: 'px-2 py-3.5 text-center',
      skeleton: { variant: 'number' },
      cell: (_, index) => (
        <span className="text-xs font-medium tabular-nums text-slate-400">
          {(pageNumber - 1) * pageSize + index + 1}
        </span>
      ),
    },
    {
      id: 'title',
      header: 'Đề tài',
      headerClassName: 'px-4 py-3.5',
      cellClassName: 'px-4 py-3.5',
      skeleton: { variant: 'topic' },
      cell: (topic) => (
        <div className="min-w-0 space-y-1">
          <p
            className="line-clamp-2 text-[13px] font-semibold leading-snug text-slate-900"
            title={topic.titleVi}
          >
            {topic.titleVi}
          </p>
          {(topic.titleEn || topic.reviewNote?.trim()) && (
            <div className="flex flex-wrap items-center gap-2">
              {topic.titleEn && (
                <p className="line-clamp-1 min-w-0 flex-1 text-xs text-slate-500" title={topic.titleEn}>
                  {topic.titleEn}
                </p>
              )}
              {topic.reviewNote?.trim() && (
                <span
                  title={stripRichText(topic.reviewNote)}
                  className="inline-flex shrink-0 items-center gap-1 rounded-md bg-amber-50 px-1.5 py-0.5 text-[10px] font-medium text-amber-700 ring-1 ring-inset ring-amber-200/80"
                >
                  <MessageSquareText className="size-3" aria-hidden="true" />
                  Có góp ý
                </span>
              )}
            </div>
          )}
        </div>
      ),
    },
    {
      id: 'advisor',
      header: 'Giảng viên hướng dẫn',
      widthClassName: 'w-48',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-3.5',
      skeleton: { variant: 'person' },
      cell: (topic) => renderAdvisor(topic.advisorId),
    },
    {
      id: 'batch',
      header: 'Đợt KLTN',
      widthClassName: 'w-40',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-3.5',
      cell: (topic) => renderBatch(topic.batchId),
    },
    {
      id: 'studentNumber',
      header: 'Chỉ tiêu',
      widthClassName: 'w-20',
      align: 'center',
      headerClassName: 'px-3 py-3.5 whitespace-nowrap',
      cellClassName: 'px-3 py-3.5 text-center',
      skeleton: { variant: 'number' },
      cell: (topic) => (
        <span
          title={
            topic.studentNumber !== null
              ? `Tối đa ${topic.studentNumber} sinh viên`
              : undefined
          }
          className="inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border border-slate-200/80 bg-slate-50 px-2.5 py-0.5 text-xs font-medium tabular-nums text-slate-700"
        >
          <Users className="size-3.5 text-slate-400" aria-hidden="true" />
          {topic.studentNumber ?? '—'}
        </span>
      ),
    },
    {
      id: 'status',
      header: 'Trạng thái',
      widthClassName: 'w-36',
      headerClassName: 'px-3 py-3.5 whitespace-nowrap',
      cellClassName: 'px-3 py-3.5',
      skeleton: { variant: 'badge' },
      cell: (topic) => <TopicStatusBadge status={topic.status} />,
    },
    {
      id: 'createdAt',
      header: 'Ngày tạo',
      widthClassName: 'w-28',
      headerClassName: 'px-3 py-3.5 whitespace-nowrap',
      cellClassName: 'px-3 py-3.5 whitespace-nowrap text-xs font-medium tabular-nums text-slate-500',
      skeleton: { variant: 'text' },
      cell: (topic) => formatDate(topic.createdAt),
    },
    {
      id: 'actions',
      header: 'Thao tác',
      widthClassName: 'w-32',
      align: 'right',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-3.5 text-right',
      skeleton: { variant: 'actions', actionCount: 1 },
      cell: renderAction,
    },
  ];

  return (
    <DataTable
      data={topics}
      columns={columns}
      getRowId={(topic) => topic.id}
      ariaLabel="Danh sách đề tài thẩm định"
      selectedRowId={selectedRowId}
      onRowSelect={onSelectRow ? (id) => onSelectRow(id) : undefined}
      onRowOpen={onOpen}
      isRowSelected={(topic) => selectedIds.has(topic.id) || selectedRowId === topic.id}
      busy={busy}
      tableClassName="min-w-[960px]"
      rowClassName="h-[68px]"
      renderMobileItem={(topic, index) => {
        const checked = selectedIds.has(topic.id);
        const isApprovable = topic.status === 'Approved';
        return (
          <>
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-center gap-2.5">
                {isApprovable ? (
                  <Checkbox
                    aria-label={`Chọn đề tài ${topic.titleVi}`}
                    checked={checked}
                    disabled={busy}
                    onCheckedChange={(value) => toggleOne(topic, value === true)}
                  />
                ) : (
                  <span className="text-xs font-semibold tabular-nums text-slate-400">
                    #{(pageNumber - 1) * pageSize + index + 1}
                  </span>
                )}
                <TopicStatusBadge status={topic.status} />
              </div>
              <span className="text-xs font-medium tabular-nums text-slate-500">
                {formatDate(topic.createdAt)}
              </span>
            </div>

            <div className="space-y-1">
              <p className="text-sm font-semibold leading-snug text-slate-900">{topic.titleVi}</p>
              {topic.titleEn && <p className="text-xs text-slate-500">{topic.titleEn}</p>}
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 pt-1">
              <div className="min-w-0 space-y-1">
                {renderAdvisor(topic.advisorId)}
                <div className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
                  {renderBatch(topic.batchId)}
                  <span aria-hidden="true">·</span>
                  <span className="tabular-nums">
                    {topic.studentNumber !== null ? `${topic.studentNumber} sinh viên` : '—'}
                  </span>
                </div>
              </div>
              {renderAction(topic)}
            </div>
          </>
        );
      }}
    />
  );
}
