import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { FileSearch } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/shared/auth';
import { SimilarityReportPanel } from '../components/SimilarityReportPanel';
import { similarityApi } from '../services/similarityApi';

export function CouncilSimilarityReportsPage() {
  const { user } = useAuth();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const query = useQuery({
    queryKey: ['my-council-similarity-reports', user?.id],
    queryFn: ({ signal }) => similarityApi.getMyCouncilReports(signal),
    enabled: Boolean(user?.id),
    retry: false,
    gcTime: 0,
    refetchOnMount: 'always',
  });
  const selected = !query.isError
    ? query.data?.find((item) => item.finalReportId === selectedId)
    : undefined;

  return (
    <main className="mx-auto max-w-7xl space-y-6 px-4 py-6 sm:px-6">
      <PageHeader
        title="Báo cáo tương đồng được phân công"
        description="Các báo cáo thuộc nhóm đề tài trong hội đồng của bạn và đã đến giai đoạn thẩm định."
        icon={<FileSearch />}
      />
      <section
        className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs"
        aria-label="Danh sách báo cáo"
      >
        {query.isPending && (
          <p role="status" className="text-sm text-slate-500">
            Đang tải danh sách…
          </p>
        )}
        {query.isError && (
          <div role="alert" className="space-y-2 text-sm text-rose-700">
            <p>Không tải được danh sách báo cáo được phân công.</p>
            <Button type="button" variant="outline" size="sm" onClick={() => void query.refetch()}>
              Thử lại
            </Button>
          </div>
        )}
        {!query.isError && query.data?.length === 0 && (
          <p className="text-sm text-slate-500">
            Chưa có báo cáo nào được phân công và đến giai đoạn thẩm định.
          </p>
        )}
        {!query.isError && query.data && query.data.length > 0 && (
          <ul className="divide-y divide-slate-100">
            {query.data.map((item) => (
              <li
                key={item.finalReportId}
                className="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-0 last:pb-0"
              >
                <div>
                  <p className="font-semibold text-slate-800">{item.topicTitle}</p>
                  <p className="mt-1 text-xs text-slate-500">Báo cáo phiên bản v{item.versionNo}</p>
                </div>
                <Button
                  type="button"
                  variant={selectedId === item.finalReportId ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setSelectedId(item.finalReportId)}
                >
                  {selectedId === item.finalReportId ? 'Đang xem' : 'Xem kết quả'}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>
      {selected && (
        <SimilarityReportPanel reportId={selected.finalReportId} versionNo={selected.versionNo} />
      )}
    </main>
  );
}
