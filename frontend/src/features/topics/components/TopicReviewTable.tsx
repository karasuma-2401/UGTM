import { ClipboardCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Skeleton } from '@/components/ui/skeleton';
import { SelectableTableRow } from '@/components/ui/selectable-table-row';
import { SelectionIndicator } from '@/components/ui/selection-indicator';
import {
  Table,
  TableHeader,
  TableHead,
  TableBody,
  TableCell,
} from '@/components/ui/table';
import type { Topic, TopicBatch } from '../types/topic.types';
import { TopicStatusBadge } from './TopicStatusBadge';

interface TopicReviewTableProps {
  topics: Topic[];
  batches: TopicBatch[];
  advisorNames: Record<string, string>;
  advisorsLoading?: boolean;
  batchesLoading?: boolean;
  selected: Topic[];
  pageNumber: number;
  pageSize: number;
  busy: boolean;
  onSelect: (topics: Topic[]) => void;
  onOpen: (topic: Topic) => void;
  selectedRowId?: string | null;
  onSelectRow?: (id: string) => void;
}

export function TopicReviewTable({
  topics,
  batches,
  advisorNames,
  advisorsLoading = false,
  batchesLoading = false,
  selected,
  pageNumber,
  pageSize,
  busy,
  onSelect,
  onOpen,
  selectedRowId,
  onSelectRow,
}: TopicReviewTableProps) {
  const selectedBatch = selected[0]?.batchId;
  const selectable = topics.filter(
    (topic) => topic.status === 'Approved' && (!selectedBatch || topic.batchId === selectedBatch),
  );
  const allSelected =
    selectable.length > 0 &&
    selectable.every((topic) => selected.some((item) => item.id === topic.id));
  const canSelectAll = new Set(selectable.map((topic) => topic.batchId)).size === 1;

  return (
    <div className="relative isolate">
      <SelectionIndicator
        activeKey={selectedRowId}
        selector='tr[aria-selected="true"]'
        variant="rail"
        className="z-20 rounded-r bg-primary"
      />
      <Table className="min-w-[1240px] table-fixed">
      <colgroup>
        <col className="w-14" />
        <col className="w-12" />
        <col />
        <col className="w-48" />
        <col className="w-40" />
        <col className="w-20" />
        <col className="w-44" />
        <col className="w-44" />
      </colgroup>
      <TableHeader>
        <tr>
          <TableHead align="center" className="w-14 px-3 py-3.5">
            STT
          </TableHead>
          <TableHead className="w-12 px-4 py-3.5">
            <Checkbox
              aria-label="Chọn tất cả đề tài đã duyệt cùng đợt trên trang này"
              checked={allSelected ? true : selected.length ? 'indeterminate' : false}
              disabled={busy || !canSelectAll}
              title={
                canSelectAll
                  ? 'Chọn đề tài đã duyệt trên trang này'
                  : 'Lọc theo đợt hoặc chọn một đề tài trước'
              }
              onCheckedChange={(checked) => onSelect(checked ? selectable : [])}
            />
          </TableHead>
          {['Đề tài', 'Giảng viên hướng dẫn', 'Đợt KLTN', 'Chỉ tiêu', 'Trạng thái', 'Thao tác'].map(
            (label) => (
              <TableHead
                key={label}
                align={label === 'Thao tác' ? 'center' : 'left'}
                className={label === 'Thao tác' ? 'px-4 py-3.5 whitespace-nowrap' : 'px-4 py-3.5'}
              >
                {label}
              </TableHead>
            ),
          )}
        </tr>
      </TableHeader>
      <TableBody>
        {topics.map((topic, index) => {
          const checked = selected.some((item) => item.id === topic.id);
          return (
            <SelectableTableRow
              key={topic.id}
              data-selection-item
              hideSelectionMarker
              title="Nhấp đúp để xem chi tiết"
              className="h-[88px]"
              selected={checked || selectedRowId === topic.id}
              onSelect={onSelectRow ? () => onSelectRow(topic.id) : undefined}
              onDoubleClick={(event) => {
                if (
                  (event.target as Element).closest(
                    'button, a, input, select, textarea, [role="button"]',
                  )
                )
                  return;
                onOpen(topic);
              }}
              disabled={busy}
            >
              <TableCell align="center" className="px-3 py-4">
                <span className="text-xs tabular-nums text-slate-400 font-medium">
                  {(pageNumber - 1) * pageSize + index + 1}
                </span>
              </TableCell>
              <TableCell className="px-4 py-4" onClick={(event) => event.stopPropagation()}>
                <Checkbox
                  aria-label={`Chọn công bố đề tài ${topic.titleVi}`}
                  checked={checked}
                  disabled={
                    busy ||
                    topic.status !== 'Approved' ||
                    Boolean(selectedBatch && selectedBatch !== topic.batchId)
                  }
                  onCheckedChange={(value) =>
                    onSelect(
                      value
                        ? [...selected, topic]
                        : selected.filter((item) => item.id !== topic.id),
                    )
                  }
                />
              </TableCell>
              <TableCell className="px-4 py-4">
                <div className="space-y-1 min-w-0">
                  <span
                    className="line-clamp-2 block text-sm font-semibold text-slate-900 leading-snug hover:text-primary transition-colors cursor-pointer"
                    onClick={() => onOpen(topic)}
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
                    <span className="block text-xs text-slate-400 italic">
                      Chưa có tên tiếng Anh
                    </span>
                  )}
                </div>
              </TableCell>
              <TableCell
                aria-busy={!advisorNames[topic.advisorId] && advisorsLoading}
                className="break-words px-4 py-4 text-xs text-slate-600"
              >
                {advisorNames[topic.advisorId] ||
                  (advisorsLoading ? (
                    <Skeleton className="h-4 w-32 max-w-full" />
                  ) : (
                    <span title={topic.advisorId}>Mã GVHD: {topic.advisorId}</span>
                  ))}
              </TableCell>
              <TableCell className="px-4 py-4 text-xs text-slate-500">
                {batches.find((batch) => batch.id === topic.batchId)?.batchName ||
                  (batchesLoading ? (
                    <Skeleton className="h-4 w-28 max-w-full" />
                  ) : (
                    <span title={topic.batchId}>Mã đợt: {topic.batchId}</span>
                  ))}
              </TableCell>
              <TableCell className="px-4 py-4 text-xs font-medium text-slate-700 tabular-nums">
                {topic.studentNumber ?? '—'} SV
              </TableCell>
              <TableCell className="px-4 py-4">
                <TopicStatusBadge status={topic.status} />
              </TableCell>
              <TableCell align="center" className="px-4 py-4">
                <Button
                  type="button"
                  variant={topic.status === 'Submitted' ? 'default' : 'outline'}
                  size="sm"
                  className={
                    topic.status === 'Submitted'
                      ? 'h-8.5 px-3 cursor-pointer rounded-xl text-xs font-semibold'
                      : 'h-8.5 px-4 rounded-lg border-slate-200 bg-white font-medium text-xs text-slate-700 shadow-2xs hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900 transition-all cursor-pointer'
                  }
                  leftIcon={topic.status === 'Submitted' ? <ClipboardCheck className="size-3.5" /> : undefined}
                  disabled={busy}
                  onClick={() => onOpen(topic)}
                  aria-label={`${topic.status === 'Submitted' ? 'Thẩm định' : 'Xem chi tiết'} đề tài ${topic.titleVi}`}
                >
                  {topic.status === 'Submitted' ? 'Thẩm định' : 'Xem'}
                </Button>
              </TableCell>
            </SelectableTableRow>
          );
        })}
      </TableBody>
    </Table>
    </div>
  );
}
