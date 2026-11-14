import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Copy,
  Loader2,
  RefreshCw,
  RotateCw,
  ShieldCheck,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/shared/auth';
import { similarityApi } from '../services/similarityApi';
import type {
  SimilarityFinding,
  SimilarityReceipt,
  SimilarityReport,
  SimilarityResult,
} from '../types/similarity.types';

interface SimilarityReportPanelProps {
  reportId: string;
  versionNo?: number;
}

const pendingStatuses = new Set([
  'PendingSubmission',
  'Queued',
  'Processing',
  'WaitingForCompute',
]);

const retryStatuses = new Set(['Completed', 'Failed', 'ComputeUnavailable']);

const statusLabels: Record<string, string> = {
  PendingSubmission: 'Đã tiếp nhận',
  Queued: 'Đang chờ xử lý',
  Processing: 'Đang phân tích AI',
  WaitingForCompute: 'Đang chờ GPU',
  ComputeUnavailable: 'GPU tạm gián đoạn',
  Completed: 'Đã hoàn tất',
  UnsupportedFormat: 'Định dạng chưa hỗ trợ',
  Failed: 'Xử lý thất bại',
};

const statusBadgeTone: Record<string, string> = {
  Completed: 'bg-emerald-50 text-emerald-700 border-emerald-200/80',
  PendingSubmission: 'bg-blue-50 text-[#2054a5] border-blue-200/80',
  Queued: 'bg-blue-50 text-[#2054a5] border-blue-200/80',
  Processing: 'bg-blue-50 text-[#2054a5] border-blue-200/80',
  WaitingForCompute: 'bg-amber-50 text-amber-800 border-amber-200/80',
  ComputeUnavailable: 'bg-amber-50 text-amber-800 border-amber-200/80',
  UnsupportedFormat: 'bg-rose-50 text-rose-700 border-rose-200/80',
  Failed: 'bg-rose-50 text-rose-700 border-rose-200/80',
};

const citationLabels: Record<string, string> = {
  Cited: 'Có trích dẫn',
  Uncited: 'Thiếu trích dẫn',
  SuspectedMisattribution: 'Nghi ngờ sai nguồn',
};

function isReceipt(data: SimilarityResult): data is SimilarityReceipt {
  return 'notice' in data;
}

function formatPercent(value: number) {
  const percent = value <= 1 ? value * 100 : value;
  return `${percent.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}%`;
}

function formatScore(value: number) {
  return value.toLocaleString('vi-VN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toLocaleString('vi-VN', { maximumFractionDigits: 2 })} MB`;
}

function formatUtc(iso?: string | null) {
  if (!iso) return null;
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}

function isErrorStatus(status: string) {
  return status === 'Failed' || status === 'ComputeUnavailable' || status === 'UnsupportedFormat';
}

function PipelineStepper({ status }: { status: string }) {
  const isDone = status === 'Completed';
  const isError = isErrorStatus(status);
  const isWaiting = status === 'WaitingForCompute';
  const isActiveAi = pendingStatuses.has(status) && !isWaiting;

  const steps: {
    label: string;
    subtitle: string;
    state: 'done' | 'active' | 'warning' | 'error' | 'idle';
  }[] = [
    {
      label: 'Tiếp nhận tệp',
      subtitle: 'Lưu trữ & SHA-256',
      state: 'done',
    },
    {
      label: isWaiting ? 'Chờ tài nguyên GPU' : 'Phân tích AI',
      subtitle: isDone
        ? 'Hoàn tất phân tích'
        : isError
          ? 'Phân tích gián đoạn'
          : isWaiting
            ? 'Đang xếp hàng xử lý'
            : 'Đối chiếu ngữ nghĩa',
      state: isDone ? 'done' : isError ? 'error' : isWaiting ? 'warning' : 'active',
    },
    {
      label: 'Hoàn tất đối soát',
      subtitle: isDone ? 'Đã cập nhật báo cáo' : 'Chờ kết quả tổng hợp',
      state: isDone ? 'done' : 'idle',
    },
  ];

  const firstConnectorTone =
    steps[1].state === 'done'
      ? 'bg-emerald-500/60'
      : steps[1].state === 'active'
        ? 'bg-blue-400/70'
        : steps[1].state === 'warning'
          ? 'bg-amber-400/70'
          : steps[1].state === 'error'
            ? 'bg-rose-400/70'
            : 'bg-slate-200/90';

  const secondConnectorTone = isDone ? 'bg-emerald-500/60' : 'bg-slate-200/90';

  return (
    <div className="rounded-xl border border-slate-200/75 bg-slate-50/55 px-4 py-3.5 space-y-3">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3 sm:gap-0">
        {steps.map((step, index) => {
          const leftSegmentTone =
            index === 1 ? firstConnectorTone : index === 2 ? secondConnectorTone : '';
          const rightSegmentTone =
            index === 0 ? firstConnectorTone : index === 1 ? secondConnectorTone : '';

          const nodeClasses =
            step.state === 'done'
              ? 'bg-emerald-50 border-emerald-200/90 text-emerald-600'
              : step.state === 'active'
                ? 'bg-blue-50 border-blue-300/90 text-[#2054a5] ring-4 ring-blue-100/70'
                : step.state === 'warning'
                  ? 'bg-amber-50 border-amber-300/90 text-amber-700 ring-4 ring-amber-100/70'
                  : step.state === 'error'
                    ? 'bg-rose-50 border-rose-300/90 text-rose-600 ring-4 ring-rose-100/70'
                    : 'bg-white border-slate-200/90 text-slate-400';

          const titleClasses =
            step.state === 'idle'
              ? 'text-slate-400'
              : step.state === 'active'
                ? 'text-[#2054a5]'
                : step.state === 'warning'
                  ? 'text-amber-800'
                  : step.state === 'error'
                    ? 'text-rose-700'
                    : 'text-slate-900';

          return (
            <div
              key={step.label}
              className="relative flex items-start gap-3 sm:flex-col sm:items-center sm:gap-1.5 sm:px-2 sm:text-center"
            >
              {index > 0 && (
                <span
                  aria-hidden="true"
                  className={`hidden sm:block absolute top-3.5 left-0 right-[calc(50%+18px)] h-[2px] rounded-full ${leftSegmentTone}`}
                />
              )}
              {index < steps.length - 1 && (
                <>
                  <span
                    aria-hidden="true"
                    className={`hidden sm:block absolute top-3.5 left-[calc(50%+18px)] right-0 h-[2px] rounded-full ${rightSegmentTone}`}
                  />
                  <span
                    aria-hidden="true"
                    className={`sm:hidden absolute left-3.5 top-8 bottom-[-10px] w-px ${rightSegmentTone}`}
                  />
                </>
              )}

              <span
                className={`relative z-10 inline-flex size-7 shrink-0 items-center justify-center rounded-full border text-xs font-semibold tabular-nums transition-colors ${nodeClasses}`}
              >
                {step.state === 'done' ? (
                  <CheckCircle2 className="size-4" />
                ) : step.state === 'active' ? (
                  <Loader2 className="size-3.5 animate-spin" />
                ) : step.state === 'warning' ? (
                  <Clock className="size-3.5" />
                ) : step.state === 'error' ? (
                  <AlertCircle className="size-3.5" />
                ) : (
                  index + 1
                )}
              </span>

              <div className="min-w-0 pt-0.5 sm:pt-0">
                <p className={`text-xs font-semibold leading-tight ${titleClasses}`}>
                  {step.label}
                </p>
                <p className="text-[11px] text-slate-500 mt-0.5 leading-snug">{step.subtitle}</p>
              </div>
            </div>
          );
        })}
      </div>

      {isActiveAi && (
        <div
          aria-hidden="true"
          className="h-1 w-full rounded-full bg-slate-200/80 fluent-ai-progress-track"
        />
      )}
    </div>
  );
}

export function SimilarityReportPanel({ reportId, versionNo }: SimilarityReportPanelProps) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [copiedChecksum, setCopiedChecksum] = useState(false);

  const query = useQuery({
    queryKey: ['similarity-report', user?.id, reportId, versionNo ?? 'latest'],
    queryFn: ({ signal }) => similarityApi.getReport(reportId, versionNo, signal),
    enabled: Boolean(user?.id && reportId),
    refetchInterval: (q) => {
      const status = q.state.data?.status;
      return status && pendingStatuses.has(status) ? 10000 : false;
    },
    retry: (failureCount, error) => {
      if (axios.isAxiosError(error) && error.response?.status && error.response.status < 500) {
        return false;
      }
      return failureCount < 1;
    },
  });

  const retryMutation = useMutation({
    mutationFn: () => similarityApi.retry(reportId, versionNo),
    onSuccess: (data) => {
      queryClient.setQueryData(
        ['similarity-report', user?.id, reportId, versionNo ?? 'latest'],
        data,
      );
      queryClient.invalidateQueries({ queryKey: ['similarity-report', user?.id, reportId] });
    },
  });

  const handleCopyChecksum = async (checksum: string) => {
    try {
      await navigator.clipboard?.writeText(checksum);
      setCopiedChecksum(true);
      window.setTimeout(() => setCopiedChecksum(false), 2000);
    } catch {
      // Clipboard API may be unavailable in restricted contexts.
    }
  };

  if (query.isPending) {
    return (
      <div className="bg-white rounded-2xl border border-slate-200/80 shadow-xs p-5 sm:p-6">
        <div className="flex items-center gap-2 text-sm text-slate-600">
          <Loader2 className="size-4 animate-spin text-[#2054a5]" />
          <span>Đang tải kết quả kiểm tra tương đồng...</span>
        </div>
      </div>
    );
  }

  if (query.isError) {
    const httpStatus = axios.isAxiosError(query.error) ? query.error.response?.status : undefined;
    const errorTitle =
      httpStatus === 403
        ? 'Bạn không có quyền xem báo cáo tương đồng này'
        : httpStatus === 404
          ? 'Chưa có lượt quét tương đồng cho phiên bản này'
          : 'Chưa có dữ liệu đối chiếu tương đồng';
    const errorDescription =
      httpStatus === 403
        ? 'Chi tiết tương đồng chỉ hiển thị cho Giảng viên hướng dẫn, Khoa hoặc Hội đồng theo quy định.'
        : 'Hệ thống sẽ tự động đối chiếu sau khi phiên bản báo cáo cuối kỳ được tiếp nhận.';

    return (
      <div className="bg-white rounded-2xl border border-slate-200/80 shadow-xs p-5 sm:p-6">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-2.5 text-sm text-slate-700">
            <AlertCircle className="size-4 text-slate-400 shrink-0 mt-0.5" />
            <div>
              <p className="font-semibold text-slate-900">{errorTitle}</p>
              <p className="text-xs text-slate-500 mt-1">{errorDescription}</p>
            </div>
          </div>
          <Button variant="outline" size="sm" onClick={() => void query.refetch()}>
            <RefreshCw className="size-3.5 mr-1.5" />
            Tải lại
          </Button>
        </div>
      </div>
    );
  }

  const report = query.data;
  const isPending = pendingStatuses.has(report.status);
  const completedText = formatUtc(report.completedAt);
  const createdText = formatUtc(isReceipt(report) ? report.submittedAt : report.createdAt);
  const badgeClass =
    statusBadgeTone[report.status] || 'bg-slate-100 text-slate-700 border-slate-200/80';

  const statusBannerStyle =
    report.status === 'Completed'
      ? 'bg-slate-50/70 border-slate-200/80 text-slate-900'
      : isErrorStatus(report.status)
        ? 'bg-rose-50/55 border-rose-200/80 text-rose-950'
        : report.status === 'WaitingForCompute'
          ? 'bg-amber-50/60 border-amber-200/80 text-amber-950'
          : 'bg-blue-50/50 border-blue-200/80 text-slate-900';

  return (
    <section
      aria-label="Kiểm tra tương đồng học thuật"
      className="bg-white rounded-2xl border border-slate-200/80 shadow-xs overflow-hidden fluent-panel"
    >
      <div className="px-5 sm:px-6 py-4 border-b border-slate-100 flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-base font-semibold text-slate-900">Kiểm tra tương đồng</h3>
            <span
              className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-semibold ${badgeClass}`}
            >
              {statusLabels[report.status] ?? report.status}
            </span>
          </div>
          <p className="text-xs text-slate-500 mt-0.5 tabular-nums">
            {versionNo
              ? `Kết quả đối chiếu cho báo cáo cuối kỳ · Phiên bản v${versionNo}`
              : 'Kết quả đối chiếu cho phiên bản báo cáo cuối kỳ mới nhất'}
          </p>
        </div>

        <div className="flex items-center gap-2">
          {!isReceipt(report) && report.canRescan && retryStatuses.has(report.status) && (
            <Button
              variant="outline"
              size="sm"
              disabled={retryMutation.isPending}
              onClick={() => {
                if (window.confirm('Yêu cầu hệ thống AI quét lại tương đồng cho phiên bản này?')) {
                  retryMutation.mutate();
                }
              }}
            >
              <RotateCw className="size-3.5 mr-1.5" />
              Yêu cầu quét lại
            </Button>
          )}
          <Button
            variant="outline"
            size="sm"
            onClick={() => void query.refetch()}
            disabled={query.isFetching}
          >
            <RefreshCw className={`size-3.5 mr-1.5 ${query.isFetching ? 'animate-spin' : ''}`} />
            Làm mới
          </Button>
        </div>
      </div>

      <div className="p-5 sm:p-6 space-y-4">
        <PipelineStepper status={report.status} />

        <div className={`rounded-xl border px-4 py-3 text-sm ${statusBannerStyle}`}>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="font-semibold">
              {statusLabels[report.status] ?? report.status}
            </span>
            {(completedText || createdText) && (
              <span className="inline-flex items-center gap-1 text-xs opacity-80 tabular-nums">
                <Clock className="size-3.5" />
                {completedText ? `Hoàn tất ${completedText}` : `Tiếp nhận ${createdText}`}
              </span>
            )}
          </div>
          <p className="text-xs sm:text-sm opacity-90 mt-1">{report.statusDescription}</p>
          {isPending && (
            <p className="text-xs opacity-75 mt-1">
              Trạng thái tự đồng bộ mỗi 10 giây khi trang đang mở, hoặc nhấn Làm mới để kiểm tra ngay.
            </p>
          )}
        </div>

        {isReceipt(report) ? (
          <div className="space-y-3">
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 rounded-xl border border-slate-200/80 bg-slate-50/55 p-3.5 text-xs">
              <div className="sm:col-span-2 min-w-0">
                <p className="text-slate-500">Tệp đã nộp</p>
                <p
                  className="font-semibold text-slate-900 mt-0.5 break-words [overflow-wrap:anywhere]"
                  title={report.fileName}
                >
                  {report.fileName}
                </p>
              </div>
              <div>
                <p className="text-slate-500">Dung lượng</p>
                <p className="font-semibold text-slate-900 mt-0.5 tabular-nums">
                  {formatSize(report.fileSize)}
                </p>
              </div>
              {report.checksumSha256 && (
                <div className="sm:col-span-3 pt-2.5 border-t border-slate-200/70 flex flex-wrap items-center justify-between gap-2">
                  <div className="min-w-0 flex items-center gap-1.5 flex-wrap">
                    <span className="text-slate-500">Biên nhận SHA-256:</span>
                    <code
                      title={report.checksumSha256}
                      className="font-mono text-[11px] text-slate-700 bg-white border border-slate-200/80 rounded-md px-2 py-0.5 tabular-nums"
                    >
                      {report.checksumSha256.length > 26
                        ? `${report.checksumSha256.slice(0, 12)}…${report.checksumSha256.slice(-10)}`
                        : report.checksumSha256}
                    </code>
                  </div>
                  <button
                    type="button"
                    onClick={() => void handleCopyChecksum(report.checksumSha256!)}
                    className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-[11px] font-medium text-slate-700 hover:bg-slate-50 transition-colors cursor-pointer shrink-0"
                  >
                    {copiedChecksum ? (
                      <>
                        <CheckCircle2 className="size-3.5 text-emerald-600" />
                        <span className="text-emerald-700">Đã sao chép</span>
                      </>
                    ) : (
                      <>
                        <Copy className="size-3 text-slate-500" />
                        <span>Sao chép</span>
                      </>
                    )}
                  </button>
                </div>
              )}
            </div>

            <div className="flex items-start gap-2.5 rounded-xl bg-slate-50/75 border border-slate-200/80 px-3.5 py-2.5 text-xs text-slate-600 leading-relaxed">
              <ShieldCheck className="size-4 text-slate-500 shrink-0 mt-0.5" />
              <p>{report.notice}</p>
            </div>
          </div>
        ) : (
          <AuthorizedReportView report={report} />
        )}
      </div>
    </section>
  );
}

function AuthorizedReportView({ report }: { report: SimilarityReport }) {
  return (
    <div className="space-y-4">
      {report.summary && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <Metric label="Phạm vi tương đồng" value={formatPercent(report.summary.similarityCoverage)} />
          <Metric
            label="Điểm đối chiếu cao nhất"
            value={formatScore(report.summary.maxRerankerScore)}
          />
          <Metric
            label="Phân đoạn đã quét"
            value={(report.summary.scannedChunks ?? 0).toLocaleString('vi-VN')}
          />
          <Metric
            label="Phát hiện"
            value={`${report.summary.citedFindings ?? 0} có trích dẫn · ${report.summary.uncitedFindings ?? 0} thiếu`}
          />
        </div>
      )}

      {report.findings.length > 0 && (
        <div className="space-y-3">
          <h4 className="text-xs font-semibold text-slate-600">
            Các đoạn cần đối chiếu ({report.findings.length})
          </h4>
          {report.findings.map((finding) => (
            <FindingCard key={finding.passageGroupId} finding={finding} />
          ))}
        </div>
      )}

      {report.status === 'Completed' && report.findings.length === 0 && (
        <p className="text-sm text-emerald-700 bg-emerald-50 border border-emerald-200/70 rounded-xl px-4 py-3">
          Không ghi nhận đoạn văn bản trùng lặp cần rà soát.
        </p>
      )}

      {report.disclaimer && (
        <p className="text-xs text-slate-500 pt-2 border-t border-slate-100">{report.disclaimer}</p>
      )}
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-slate-200/80 bg-slate-50/60 px-3.5 py-2.5">
      <div className="text-xs text-slate-500">{label}</div>
      <div className="text-sm font-semibold text-slate-900 mt-1 tabular-nums">{value}</div>
    </div>
  );
}

function FindingCard({ finding }: { finding: SimilarityFinding }) {
  const citationLabel = finding.citationStatus
    ? (citationLabels[finding.citationStatus] ?? finding.citationStatus)
    : 'Chưa phân loại';

  return (
    <div className="rounded-xl border border-slate-200/80 bg-slate-50/40 p-4 space-y-2.5 text-sm">
      <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500">
        <span>
          {citationLabel}
          {finding.rerankerScore != null && (
            <>
              {' · Điểm: '}
              <span className="font-semibold text-slate-700 tabular-nums">
                {formatScore(finding.rerankerScore)}
              </span>
            </>
          )}
        </span>
        {finding.reviewRequired && (
          <span className="px-2 py-0.5 rounded-full bg-amber-50 text-amber-800 border border-amber-200/80 font-medium">
            Cần GV rà soát
          </span>
        )}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <div className="rounded-lg bg-white border border-slate-200/70 p-3">
          <div className="text-xs text-slate-500 mb-1">
            Bài nộp{finding.submissionPage ? ` (Trang ${finding.submissionPage})` : ''}
          </div>
          <p className="text-xs text-slate-800 whitespace-pre-wrap">{finding.submissionText}</p>
        </div>
        <div className="rounded-lg bg-white border border-slate-200/70 p-3">
          <div className="text-xs text-slate-500 mb-1">
            Nguồn: {finding.sourceFilename || finding.sourceKey || 'Kho luận văn'}
            {finding.sourcePage ? ` (Trang ${finding.sourcePage})` : ''}
          </div>
          <p className="text-xs text-slate-800 whitespace-pre-wrap">{finding.sourceText}</p>
        </div>
      </div>

      {finding.citationExplanation && (
        <p className="text-xs text-slate-600">{finding.citationExplanation}</p>
      )}
    </div>
  );
}
