import { Eye, Copy, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { SelectableTableRow } from '@/components/ui/selectable-table-row';
import { canEditTopic, type Topic, type TopicBatch } from '../types/topic.types';
import { TopicStatusBadge } from './TopicStatusBadge';

interface TopicsTableProps {
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
}: TopicsTableProps) {
  const batchNames = new Map(batches.map((batch) => [batch.id, batch.batchName]));
  return (
    <table className="w-full min-w-[1200px] table-fixed text-left text-sm">
      <colgroup>
        <col className="w-14" />
        <col />
        <col className="w-40" />
        <col className="w-24" />
        <col className="w-32" />
        <col className="w-44" />
        <col className="w-72" />
      </colgroup>
      <thead className="sticky top-0 z-10 h-12 border-b border-slate-200/80 bg-slate-50/90 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
        <tr>
          <th scope="col" className="px-3 py-3.5 text-center font-bold">
            STT
          </th>
          {['Đề tài', 'Đợt khóa luận', 'Chỉ tiêu', 'Ngày tạo', 'Trạng thái', 'Thao tác'].map(
            (heading) => (
              <th key={heading} scope="col" className="whitespace-nowrap px-4 py-3.5 font-bold">
                {heading}
              </th>
            ),
          )}
        </tr>
      </thead>
      <tbody className="divide-y divide-slate-100">
        {topics.map((topic, index) => (
          <SelectableTableRow
            key={topic.id}
            className="h-[88px]"
            selected={selectedRowId === topic.id}
            onSelect={onSelectRow ? () => onSelectRow(topic.id) : undefined}
            disabled={busy}
          >
            <td className="px-3 py-4 text-center text-xs tabular-nums text-slate-400">
              {(pageNumber - 1) * pageSize + index + 1}
            </td>
            <td className="px-4 py-4">
              <div className="space-y-1 min-w-0">
                <span
                  className="line-clamp-2 block text-sm font-semibold text-slate-900 leading-snug hover:text-primary transition-colors"
                  title={topic.titleVi}
                >
                  {topic.titleVi}
                </span>
                {topic.titleEn ? (
                  <span
                    className="truncate block text-[11px] text-slate-500 font-normal"
                    title={topic.titleEn}
                  >
                    {topic.titleEn}
                  </span>
                ) : (
                  <span className="block text-[11px] text-slate-400 italic">
                    Chưa có tên tiếng Anh
                  </span>
                )}
              </div>
            </td>
            <td className="px-4 py-4 text-xs text-slate-600">
              {batchNames.get(topic.batchId) ||
                (batchesLoading ? <Skeleton className="h-4 w-28 max-w-full" /> : '—')}
            </td>
            <td className="px-4 py-4 text-xs font-medium text-slate-600">
              {topic.studentNumber ? `${topic.studentNumber} sinh viên` : '—'}
            </td>
            <td className="px-4 py-4 text-xs text-slate-500">
              {new Date(topic.createdAt).toLocaleDateString('vi-VN')}
            </td>
            <td className="px-4 py-4">
              <TopicStatusBadge status={topic.status} />
            </td>
            <td className="px-4 py-4">
              <div className="flex flex-wrap items-center gap-2">
                <Button
                  type="button"
                  variant="soft"
                  size="sm"
                  leftIcon={<Eye />}
                  className="h-9 cursor-pointer"
                  disabled={busy}
                  onClick={() => onOpen(topic)}
                  aria-label={`Xem chi tiết đề tài ${topic.titleVi}`}
                >
                  Chi tiết
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-9"
                  leftIcon={<Copy />}
                  disabled={busy}
                  onClick={() => onCopy(topic)}
                  aria-label={`Nhân bản đề tài ${topic.titleVi}`}
                >
                  Nhân bản
                </Button>
                {canEditTopic(topic.status) && (
                  <Button
                    type="button"
                    variant="successOutline"
                    size="sm"
                    className="h-9"
                    leftIcon={<Send />}
                    disabled={busy}
                    onClick={() => onSubmit(topic)}
                    aria-label={`Gửi duyệt đề tài ${topic.titleVi}`}
                  >
                    Gửi duyệt
                  </Button>
                )}
              </div>
            </td>
          </SelectableTableRow>
        ))}
      </tbody>
    </table>
  );
}
