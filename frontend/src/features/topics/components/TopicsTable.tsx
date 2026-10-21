import { Eye, FileText } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { InfoCell } from '@/components/ui/info-cell';
import type { Topic, TopicBatch } from '../types/topic.types';
import { TopicStatusBadge } from './TopicStatusBadge';

interface TopicsTableProps {
  topics: Topic[];
  batches: TopicBatch[];
  pageNumber: number;
  pageSize: number;
  busy: boolean;
  onOpen: (topic: Topic) => void;
}

export function TopicsTable({
  topics,
  batches,
  pageNumber,
  pageSize,
  busy,
  onOpen,
}: TopicsTableProps) {
  const batchNames = new Map(batches.map((batch) => [batch.id, batch.batchName]));
  return (
    <table className="w-full min-w-[1040px] table-fixed text-left text-sm">
      <colgroup>
        <col className="w-14" />
        <col />
        <col className="w-40" />
        <col className="w-24" />
        <col className="w-32" />
        <col className="w-44" />
        <col className="w-32" />
      </colgroup>
      <thead className="sticky top-0 z-10 h-14 border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-400">
        <tr>
          <th scope="col" className="px-3 py-4 text-center font-medium">
            STT
          </th>
          {['Đề tài', 'Đợt KLTN', 'Chỉ tiêu', 'Ngày tạo', 'Trạng thái', 'Thao tác'].map(
            (heading) => (
              <th key={heading} scope="col" className="whitespace-nowrap px-4 py-4 font-medium">
                {heading}
              </th>
            ),
          )}
        </tr>
      </thead>
      <tbody className="divide-y divide-slate-100">
        {topics.map((topic, index) => (
          <tr key={topic.id} className="h-[88px] transition-colors hover:bg-blue-50/30">
            <td className="px-3 py-4 text-center text-xs tabular-nums text-slate-400">
              {(pageNumber - 1) * pageSize + index + 1}
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
            <td className="px-4 py-4 text-xs text-slate-500">
              {batchNames.get(topic.batchId) || '—'}
            </td>
            <td className="px-4 py-4 text-xs text-slate-500">{topic.studentNumber ?? '—'} SV</td>
            <td className="px-4 py-4 text-xs text-slate-500">
              {new Date(topic.createdAt).toLocaleDateString('vi-VN')}
            </td>
            <td className="px-4 py-4">
              <TopicStatusBadge status={topic.status} />
            </td>
            <td className="px-4 py-4">
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
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
