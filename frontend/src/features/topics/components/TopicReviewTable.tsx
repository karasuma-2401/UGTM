import { ClipboardCheck, Eye, FileText } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { InfoCell } from '@/components/ui/info-cell';
import type { Topic, TopicBatch } from '../types/topic.types';
import { TopicStatusBadge } from './TopicStatusBadge';

interface TopicReviewTableProps {
  topics: Topic[];
  batches: TopicBatch[];
  advisorNames: Record<string, string>;
  selected: Topic[];
  pageNumber: number;
  pageSize: number;
  busy: boolean;
  onSelect: (topics: Topic[]) => void;
  onOpen: (topic: Topic) => void;
}

export function TopicReviewTable({
  topics,
  batches,
  advisorNames,
  selected,
  pageNumber,
  pageSize,
  busy,
  onSelect,
  onOpen,
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
    <table className="w-full min-w-[1180px] table-fixed text-left text-sm">
      <colgroup>
        <col className="w-14" />
        <col className="w-12" />
        <col />
        <col className="w-48" />
        <col className="w-40" />
        <col className="w-20" />
        <col className="w-44" />
        <col className="w-36" />
      </colgroup>
      <thead className="sticky top-0 z-10 h-14 border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-600">
        <tr>
          <th scope="col" className="px-3 py-4 text-center font-bold">
            STT
          </th>
          <th scope="col" className="px-4">
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
          </th>
          {[
            'Đề tài',
            'Giảng viên hướng dẫn',
            'Đợt KLTN',
            'Chỉ tiêu',
            'Trạng thái',
            'Thao tác',
          ].map((label) => (
            <th key={label} scope="col" className="px-4 py-4 font-bold">
              {label}
            </th>
          ))}
        </tr>
      </thead>
      <tbody className="divide-y divide-slate-100">
        {topics.map((topic, index) => {
          const checked = selected.some((item) => item.id === topic.id);
          return (
            <tr
              key={topic.id}
              className={`h-[88px] transition-colors ${checked ? 'bg-blue-50/60' : 'hover:bg-blue-50/30'}`}
            >
              <td className="px-3 py-4 text-center text-xs tabular-nums text-slate-400">
                {(pageNumber - 1) * pageSize + index + 1}
              </td>
              <td className="px-4 py-4">
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
              </td>
              <td className="px-4 py-4">
                <InfoCell
                  leading={
                    <span
                      aria-hidden="true"
                      className="flex size-9 items-center justify-center rounded-lg bg-blue-50 text-blue-600 ring-1 ring-inset ring-blue-100"
                    >
                      <FileText className="size-4" />
                    </span>
                  }
                  title={
                    <span className="line-clamp-2" title={topic.titleVi}>
                      {topic.titleVi}
                    </span>
                  }
                  titleClassName="block text-sm leading-5"
                  description={topic.titleEn || 'Chưa có tên tiếng Anh'}
                  descriptionClassName="truncate text-[11px]"
                />
              </td>
              <td className="break-words px-4 py-4 text-xs text-slate-600">
                {advisorNames[topic.advisorId] || (
                  <span title={topic.advisorId}>Mã GVHD: {topic.advisorId}</span>
                )}
              </td>
              <td className="px-4 py-4 text-xs text-slate-500">
                {batches.find((batch) => batch.id === topic.batchId)?.batchName || (
                  <span title={topic.batchId}>Mã đợt: {topic.batchId}</span>
                )}
              </td>
              <td className="px-4 py-4 text-xs text-slate-500">{topic.studentNumber ?? '—'} SV</td>
              <td className="px-4 py-4">
                <TopicStatusBadge status={topic.status} />
              </td>
              <td className="px-4 py-4">
                <Button
                  type="button"
                  variant={topic.status === 'Submitted' ? 'default' : 'soft'}
                  size="sm"
                  className="h-9 cursor-pointer"
                  leftIcon={topic.status === 'Submitted' ? <ClipboardCheck /> : <Eye />}
                  disabled={busy}
                  onClick={() => onOpen(topic)}
                  aria-label={`${topic.status === 'Submitted' ? 'Thẩm định' : 'Xem chi tiết'} đề tài ${topic.titleVi}`}
                >
                  {topic.status === 'Submitted' ? 'Thẩm định' : 'Chi tiết'}
                </Button>
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
