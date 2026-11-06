import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { AlertCircle, FileSearch, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/shared/auth';
import { similarityApi } from '../services/similarityApi';
import type { SimilarityReport, SimilarityResult } from '../types/similarity.types';

const pendingStatuses = new Set(['PendingSubmission', 'Queued', 'Processing', 'WaitingForCompute']);
const retryStatuses = new Set(['Completed', 'Failed', 'ComputeUnavailable']);
const labels: Record<string, string> = {
  PendingSubmission: 'Chờ gửi đến dịch vụ AI',
  Queued: 'Đang chờ xử lý',
  Processing: 'Đang phân tích',
  WaitingForCompute: 'Đang chờ tài nguyên tính toán',
  Completed: 'Đã hoàn tất',
  ComputeUnavailable: 'Không có tài nguyên tính toán',
  Failed: 'Xử lý thất bại',
  UnsupportedFormat: 'Định dạng chưa được hỗ trợ',
};

function errorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 403)
      return 'Bạn không có quyền xem báo cáo này hoặc báo cáo chưa đến giai đoạn được phép xem.';
    if (error.response?.status === 404) return 'Chưa có lượt kiểm tra cho phiên bản báo cáo này.';
    if (error.response?.status === 409) return 'Kết quả chưa sẵn sàng. Vui lòng kiểm tra lại sau.';
    const message = error.response?.data?.error?.message;
    if (typeof message === 'string' && message) return message;
  }
  return 'Không tải được kết quả. Vui lòng kiểm tra kết nối và thử lại.';
}

const percent = (value: number) =>
  `${(value * 100).toLocaleString('vi-VN', { maximumFractionDigits: 2 })}%`;
const score = (value: number | null) =>
  value == null ? '—' : value.toLocaleString('vi-VN', { maximumFractionDigits: 3 });

function isReport(result: SimilarityResult): result is SimilarityReport {
  return 'findings' in result;
}

export function SimilarityReportPanel({
  reportId,
  versionNo,
}: {
  reportId: string;
  versionNo?: number;
}) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const queryKey = ['similarity-report', user?.id, reportId, versionNo ?? 'latest'] as const;
  const query = useQuery({
    queryKey,
    queryFn: ({ signal }) => similarityApi.getReport(reportId, versionNo, signal),
    enabled: Boolean(user?.id && reportId),
    retry: false,
    gcTime: 0,
    refetchOnMount: 'always',
    refetchInterval: (state) =>
      state.state.data && pendingStatuses.has(state.state.data.status) ? 10000 : false,
  });
  const retry = useMutation({
    mutationFn: () => similarityApi.retry(reportId, versionNo),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey });
    },
  });

  return (
    <section
      className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs space-y-4"
      aria-label="Kiểm tra tương đồng"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="flex items-center gap-2 font-bold text-slate-900">
            <FileSearch className="size-4 text-blue-700" /> Kiểm tra tương đồng
          </h3>
          <p className="mt-1 text-xs text-slate-500">
            Kết quả đối chiếu cho báo cáo cuối kỳ
            {versionNo != null ? ` · phiên bản v${versionNo}` : ''}.
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void query.refetch()}
          disabled={query.isFetching}
        >
          <RefreshCw className="size-4 mr-1" /> Làm mới
        </Button>
      </div>

      {query.isPending && (
        <p role="status" className="text-sm text-slate-500">
          Đang tải kết quả kiểm tra…
        </p>
      )}
      {query.isError && (
        <p role="alert" className="flex gap-2 text-sm text-rose-700">
          <AlertCircle className="size-4 shrink-0" />
          {errorMessage(query.error)}
        </p>
      )}
      {!query.isError && query.data && (
        <div className="space-y-4">
          <div role="status" className="rounded-lg bg-slate-50 p-3 text-sm">
            <p className="font-semibold text-slate-800">
              {labels[query.data.status] ?? query.data.status}
            </p>
            <p className="mt-1 text-slate-600">
              {query.data.status === 'WaitingForCompute'
                ? 'Đang chờ tài nguyên GPU; tác vụ sẽ tiếp tục theo lịch xử lý.'
                : query.data.statusDescription}
            </p>
            {pendingStatuses.has(query.data.status) && (
              <p className="mt-1 text-xs text-slate-500">
                Trạng thái tự cập nhật trong khi trang đang mở.
              </p>
            )}
          </div>
          {!isReport(query.data) ? (
            <div className="space-y-2 text-sm text-slate-700">
              <p>
                <strong>Tệp đã nộp:</strong> {query.data.fileName}
              </p>
              <p>
                <strong>Dung lượng:</strong>{' '}
                {(query.data.fileSize / 1024 / 1024).toLocaleString('vi-VN', {
                  maximumFractionDigits: 2,
                })}{' '}
                MB
              </p>
              {query.data.checksumSha256 && (
                <p className="break-all text-xs">
                  <strong>SHA-256:</strong> {query.data.checksumSha256}
                </p>
              )}
              <p className="rounded-lg bg-blue-50 p-3 text-blue-900">{query.data.notice}</p>
            </div>
          ) : (
            <>
              {query.data.summary && (
                <div className="grid gap-3 sm:grid-cols-3">
                  <Metric
                    label="Phạm vi tương đồng"
                    value={percent(query.data.summary.similarityCoverage)}
                  />
                  <Metric
                    label="Điểm đối chiếu cao nhất"
                    value={score(query.data.summary.maxRerankerScore)}
                  />
                  <Metric label="Số đoạn đối chiếu" value={String(query.data.findings.length)} />
                </div>
              )}
              {query.data.status === 'Completed' && !query.data.summary && (
                <p className="text-sm text-amber-700">
                  Chưa có số liệu tổng hợp cho lượt kiểm tra này.
                </p>
              )}
              {query.data.findings.length > 0 && (
                <div className="space-y-3">
                  <h4 className="text-sm font-semibold text-slate-800">Các đoạn cần đối chiếu</h4>
                  {query.data.findings.map((finding, index) => (
                    <article
                      key={`${finding.passageGroupId ?? 'finding'}-${index}`}
                      className="rounded-lg border border-slate-200 p-4 text-sm space-y-3"
                    >
                      <div className="flex flex-wrap justify-between gap-2 text-xs text-slate-500">
                        <span>
                          Đoạn {index + 1}
                          {finding.submissionPage != null
                            ? ` · trang ${finding.submissionPage}`
                            : ''}
                        </span>
                        <span>Điểm đối chiếu: {score(finding.rerankerScore)}</span>
                      </div>
                      <div>
                        <p className="font-semibold text-slate-700">Bài nộp</p>
                        <p className="mt-1 whitespace-pre-wrap break-words text-slate-600">
                          {finding.submissionText || 'Không có văn bản.'}
                        </p>
                      </div>
                      <div>
                        <p className="font-semibold text-slate-700">
                          Nguồn: {finding.sourceFilename || finding.sourceKey || 'Chưa xác định'}
                          {finding.sourcePage != null ? ` · trang ${finding.sourcePage}` : ''}
                        </p>
                        <p className="mt-1 whitespace-pre-wrap break-words text-slate-600">
                          {finding.sourceText || 'Không có văn bản.'}
                        </p>
                      </div>
                      {finding.citationExplanation && (
                        <p className="text-xs text-slate-600">{finding.citationExplanation}</p>
                      )}
                      {finding.reviewRequired && (
                        <p className="text-xs font-medium text-amber-700">
                          Cần giảng viên xem xét ngữ cảnh.
                        </p>
                      )}
                    </article>
                  ))}
                </div>
              )}
              {query.data.status === 'Completed' &&
                query.data.findings.length === 0 &&
                query.data.summary && (
                  <p className="text-sm text-slate-600">
                    Không có đoạn trùng được ghi nhận trong báo cáo này.
                  </p>
                )}
              {query.data.disclaimer && (
                <p className="text-xs leading-relaxed text-slate-500">{query.data.disclaimer}</p>
              )}
              {query.data.canRescan && retryStatuses.has(query.data.status) && (
                <Button
                  type="button"
                  variant="outline"
                  disabled={retry.isPending}
                  onClick={() => {
                    if (
                      window.confirm(
                        'Yêu cầu quét lại có thể sử dụng tài nguyên GPU. Bạn muốn tiếp tục?',
                      )
                    )
                      retry.mutate();
                  }}
                >
                  {retry.isPending ? 'Đang yêu cầu…' : 'Yêu cầu quét lại'}
                </Button>
              )}
              {retry.isError && (
                <p role="alert" className="text-sm text-rose-700">
                  {errorMessage(retry.error)}
                </p>
              )}
            </>
          )}
        </div>
      )}
    </section>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-blue-50 p-3">
      <p className="text-xs text-slate-600">{label}</p>
      <p className="mt-1 text-lg font-bold text-slate-900">{value}</p>
    </div>
  );
}
