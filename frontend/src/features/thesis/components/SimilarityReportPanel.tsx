import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import {
  AlertCircle,
  AlertTriangle,
  ArrowUpDown,
  CheckCircle2,
  Clock,
  Copy,
  FileSearch,
  Info,
  Loader2,
  RefreshCw,
  RotateCw,
  Search,
  ShieldCheck,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import { useAuth } from '@/shared/auth';
import { similarityApi } from '../services/similarityApi';
import type {
  SimilarityFinding,
  SimilarityReceipt,
  SimilarityReport,
  SimilarityResult,
} from '../types/similarity.types';
import {
  formatRerankerScore,
  formatSimilarityPercent,
  getSimilarityScanDisplay,
  normalizeSimilarityPercent,
} from '../utils/similarity';

interface SimilarityReportPanelProps {
  reportId: string;
  versionNo?: number;
  embeddedInModal?: boolean;
}

type FindingFilterKey = 'ALL' | 'ReviewRequired' | 'Uncited' | 'Suspected' | 'Cited';
type FindingSortKey = 'score' | 'page';

const pendingStatuses = new Set([
  'PendingSubmission',
  'Queued',
  'Processing',
  'WaitingForCompute',
]);

const retryStatuses = new Set(['Completed', 'Failed', 'ComputeUnavailable']);

const citationDisplay: Record<
  string,
  { label: string; tone: StatusTone; cardTint: string }
> = {
  Cited: {
    label: 'Có trích dẫn',
    tone: 'success',
    cardTint: 'border-slate-200/80 bg-white',
  },
  Uncited: {
    label: 'Thiếu trích dẫn',
    tone: 'error',
    cardTint: 'border-rose-200/80 bg-rose-50/25',
  },
  SuspectedMisattribution: {
    label: 'Nghi ngờ sai nguồn',
    tone: 'warning',
    cardTint: 'border-amber-200/90 bg-amber-50/25',
  },
};

function isReceipt(data: SimilarityResult): data is SimilarityReceipt {
  return 'notice' in data;
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
      subtitle: 'Lưu trữ và SHA-256',
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
    <div className="rounded-xl border border-slate-200/80 bg-white px-4 py-3.5 space-y-3">
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

export function SimilarityReportPanel({
  reportId,
  versionNo,
  embeddedInModal = false,
}: SimilarityReportPanelProps) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [copiedChecksum, setCopiedChecksum] = useState(false);
  const [confirmRescanOpen, setConfirmRescanOpen] = useState(false);

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
      setConfirmRescanOpen(false);
      queryClient.setQueryData(
        ['similarity-report', user?.id, reportId, versionNo ?? 'latest'],
        data,
      );
      queryClient.invalidateQueries({ queryKey: ['similarity-report', user?.id, reportId] });
      queryClient.invalidateQueries({ queryKey: ['my-council-similarity-reports'] });
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

  const frameClass = embeddedInModal
    ? 'space-y-4'
    : 'bg-white rounded-2xl border border-slate-200/80 shadow-xs overflow-hidden fluent-panel';

  if (query.isPending) {
    return (
      <div className={cn(frameClass, !embeddedInModal && 'p-5 sm:p-6 space-y-4')}>
        <Skeleton className="h-20 w-full rounded-xl" />
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <Skeleton className="h-22 w-full rounded-xl" />
          <Skeleton className="h-22 w-full rounded-xl" />
          <Skeleton className="h-22 w-full rounded-xl" />
          <Skeleton className="h-22 w-full rounded-xl" />
        </div>
        <Skeleton className="h-36 w-full rounded-xl" />
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
        ? 'Chi tiết tương đồng chỉ hiển thị cho Giảng viên hướng dẫn, Khoa hoặc thành viên Hội đồng được phân công.'
        : 'Báo cáo chưa có bản ghi đối chiếu từ dịch vụ AI. Hệ thống sẽ hiển thị kết quả ngay khi lượt quét hoàn tất.';

    return (
      <div
        className={cn(
          embeddedInModal
            ? 'rounded-2xl border border-slate-200/80 bg-white p-5 sm:p-6 shadow-2xs'
            : 'bg-white rounded-2xl border border-slate-200/80 shadow-xs p-5 sm:p-6',
        )}
      >
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex items-start gap-3 text-sm text-slate-700">
            <div className="size-9 rounded-xl bg-slate-100 text-slate-500 flex items-center justify-center shrink-0">
              <FileSearch className="size-4.5" />
            </div>
            <div>
              <p className="font-semibold text-slate-900">{errorTitle}</p>
              <p className="text-xs text-slate-500 mt-1 max-w-xl leading-relaxed">
                {errorDescription}
              </p>
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
  const scanDisplay = getSimilarityScanDisplay(report.status);

  const statusBannerStyle =
    report.status === 'Completed'
      ? 'bg-white border-slate-200/80 text-slate-900'
      : isErrorStatus(report.status)
        ? 'bg-rose-50/70 border-rose-200/80 text-rose-950'
        : report.status === 'WaitingForCompute'
          ? 'bg-amber-50/70 border-amber-200/80 text-amber-950'
          : 'bg-blue-50/60 border-blue-200/80 text-slate-900';

  const bodyContent = (
    <div className={cn(embeddedInModal ? 'space-y-4' : 'p-5 sm:p-6 space-y-4')}>
      {embeddedInModal && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            {(completedText || createdText) && (
              <span className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-500 tabular-nums">
                <Clock className="size-3.5 text-slate-400" />
                {completedText ? `Hoàn tất ${completedText}` : `Tiếp nhận ${createdText}`}
              </span>
            )}
          </div>
          <div className="flex items-center gap-2">
            {!isReceipt(report) && report.canRescan && retryStatuses.has(report.status) && (
              <Button
                variant="outline"
                size="sm"
                className="h-8 rounded-lg bg-white text-xs"
                disabled={retryMutation.isPending}
                onClick={() => setConfirmRescanOpen(true)}
              >
                <RotateCw className="size-3.5 mr-1.5" />
                Yêu cầu quét lại
              </Button>
            )}
            <Button
              variant="outline"
              size="sm"
              className="h-8 rounded-lg bg-white text-xs"
              onClick={() => void query.refetch()}
              disabled={query.isFetching}
            >
              <RefreshCw
                className={cn('size-3.5 mr-1.5', query.isFetching && 'animate-spin')}
              />
              Làm mới
            </Button>
          </div>
        </div>
      )}

      <PipelineStepper status={report.status} />

      <div className={`rounded-xl border px-4 py-3 text-sm ${statusBannerStyle}`}>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <span className="font-semibold">{scanDisplay.label}</span>
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
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 rounded-xl border border-slate-200/80 bg-white p-3.5 text-xs">
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
                    className="font-mono text-[11px] text-slate-700 bg-slate-50 border border-slate-200/80 rounded-md px-2 py-0.5 tabular-nums"
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
  );

  return (
    <>
      <section aria-label="Kiểm tra tương đồng học thuật" className={frameClass}>
        {!embeddedInModal && (
          <div className="px-5 sm:px-6 py-4 border-b border-slate-100 flex flex-wrap items-center justify-between gap-3">
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h3 className="text-base font-semibold text-slate-900">Kiểm tra tương đồng</h3>
                <StatusBadge tone={scanDisplay.tone}>{scanDisplay.label}</StatusBadge>
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
                  onClick={() => setConfirmRescanOpen(true)}
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
                <RefreshCw
                  className={`size-3.5 mr-1.5 ${query.isFetching ? 'animate-spin' : ''}`}
                />
                Làm mới
              </Button>
            </div>
          </div>
        )}

        {bodyContent}
      </section>

      <ConfirmDialog
        open={confirmRescanOpen}
        onClose={() => {
          if (!retryMutation.isPending) setConfirmRescanOpen(false);
        }}
        onConfirm={() => retryMutation.mutate()}
        isConfirming={retryMutation.isPending}
        type="brand"
        title="Yêu cầu quét lại tương đồng"
        confirmText="Gửi yêu cầu quét lại"
        cancelText="Hủy"
        description={
          <div className="space-y-2 text-sm text-slate-600">
            <p>
              Hệ thống sẽ xếp hàng chạy lại phân tích tương đồng AI cho báo cáo phiên bản{' '}
              <strong>v{report.versionNo}</strong>.
            </p>
            <div className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">
              Kết quả mới sẽ tự động cập nhật sau khi worker đối chiếu ngữ nghĩa hoàn tất.
            </div>
          </div>
        }
      />
    </>
  );
}

function AuthorizedReportView({ report }: { report: SimilarityReport }) {
  const [findingFilter, setFindingFilter] = useState<FindingFilterKey>('ALL');
  const [findingKeyword, setFindingKeyword] = useState('');
  const [sortBy, setSortBy] = useState<FindingSortKey>('score');

  const summary = report.summary;
  const coveragePercent = summary ? normalizeSimilarityPercent(summary.similarityCoverage) : 0;
  const coverageBarTone =
    coveragePercent >= 30
      ? 'bg-rose-500'
      : coveragePercent >= 15
        ? 'bg-amber-500'
        : 'bg-emerald-500';
  const coverageTextTone =
    coveragePercent >= 30
      ? 'text-rose-700'
      : coveragePercent >= 15
        ? 'text-amber-700'
        : 'text-emerald-700';

  const maxScore = summary?.maxRerankerScore ?? 0;
  const maxScoreHint =
    maxScore >= 0.8 ? 'Độ khớp cao' : maxScore >= 0.55 ? 'Độ khớp trung bình' : 'Độ khớp thấp';

  const counts = useMemo(() => {
    let reviewRequired = 0;
    let uncited = 0;
    let suspected = 0;
    let cited = 0;
    for (const f of report.findings) {
      if (f.reviewRequired) reviewRequired++;
      if (f.citationStatus === 'Uncited') uncited++;
      else if (f.citationStatus === 'SuspectedMisattribution' || f.potentialMisattribution)
        suspected++;
      else if (f.citationStatus === 'Cited') cited++;
    }
    return {
      total: report.findings.length,
      reviewRequired,
      uncited,
      suspected,
      cited,
    };
  }, [report.findings]);

  const visibleFindings = useMemo(() => {
    const trimmed = findingKeyword.trim().toLowerCase();
    const filtered = report.findings.filter((f) => {
      if (findingFilter === 'ReviewRequired' && !f.reviewRequired) return false;
      if (findingFilter === 'Uncited' && f.citationStatus !== 'Uncited') return false;
      if (
        findingFilter === 'Suspected' &&
        f.citationStatus !== 'SuspectedMisattribution' &&
        !f.potentialMisattribution
      ) {
        return false;
      }
      if (findingFilter === 'Cited' && f.citationStatus !== 'Cited') return false;
      if (!trimmed) return true;
      return [
        f.submissionText,
        f.sourceText,
        f.sourceFilename ?? '',
        f.sourceKey ?? '',
        f.citationExplanation ?? '',
      ]
        .join(' ')
        .toLowerCase()
        .includes(trimmed);
    });

    return [...filtered].sort((a, b) => {
      if (sortBy === 'page') {
        const pageA = a.submissionPage ?? Number.MAX_SAFE_INTEGER;
        const pageB = b.submissionPage ?? Number.MAX_SAFE_INTEGER;
        if (pageA !== pageB) return pageA - pageB;
        return (a.submissionChunkNo ?? 0) - (b.submissionChunkNo ?? 0);
      }
      return (b.rerankerScore ?? 0) - (a.rerankerScore ?? 0);
    });
  }, [report.findings, findingFilter, findingKeyword, sortBy]);

  const filterChips: { key: FindingFilterKey; label: string; count: number }[] = [
    { key: 'ALL', label: 'Tất cả', count: counts.total },
    { key: 'ReviewRequired', label: 'Cần GV rà soát', count: counts.reviewRequired },
    { key: 'Uncited', label: 'Thiếu trích dẫn', count: counts.uncited },
    { key: 'Suspected', label: 'Nghi sai nguồn', count: counts.suspected },
    { key: 'Cited', label: 'Có trích dẫn', count: counts.cited },
  ];

  return (
    <div className="space-y-4">
      {summary && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          <div className="rounded-xl border border-slate-200/80 bg-white px-4 py-3 space-y-2 shadow-2xs">
            <div className="flex items-center justify-between gap-2">
              <span className="text-xs font-medium text-slate-500">Phạm vi tương đồng</span>
              <span className={cn('text-base font-bold tabular-nums', coverageTextTone)}>
                {formatSimilarityPercent(summary.similarityCoverage)}
              </span>
            </div>
            <div className="h-1.5 w-full rounded-full bg-slate-100 overflow-hidden">
              <div
                className={cn('h-full rounded-full transition-all duration-300', coverageBarTone)}
                style={{ width: `${Math.min(100, Math.max(2, coveragePercent))}%` }}
              />
            </div>
            <p className="text-[11px] text-slate-500 tabular-nums">
              {(summary.matchedCharacters ?? 0).toLocaleString('vi-VN')} /{' '}
              {(summary.scannedCharacters ?? 0).toLocaleString('vi-VN')} ký tự trùng khớp
            </p>
          </div>

          <div className="rounded-xl border border-slate-200/80 bg-white px-4 py-3 flex flex-col justify-between shadow-2xs">
            <span className="text-xs font-medium text-slate-500">Điểm đối chiếu cao nhất</span>
            <div className="mt-1 flex items-baseline justify-between gap-2">
              <span className="text-lg font-bold text-slate-900 tabular-nums">
                {formatRerankerScore(maxScore)}
              </span>
              <span className="text-[11px] font-medium text-slate-500">{maxScoreHint}</span>
            </div>
          </div>

          <div className="rounded-xl border border-slate-200/80 bg-white px-4 py-3 flex flex-col justify-between shadow-2xs">
            <span className="text-xs font-medium text-slate-500">Phân đoạn đã quét</span>
            <div className="mt-1 flex items-baseline justify-between gap-2">
              <span className="text-lg font-bold text-slate-900 tabular-nums">
                {(summary.scannedChunks ?? 0).toLocaleString('vi-VN')}
              </span>
              <span className="text-[11px] text-slate-500 tabular-nums">
                {report.findings.length} đoạn đối chiếu
              </span>
            </div>
          </div>

          <div className="rounded-xl border border-slate-200/80 bg-white px-4 py-3 space-y-1.5 shadow-2xs">
            <span className="text-xs font-medium text-slate-500">Phân loại trích dẫn</span>
            <div className="flex flex-wrap gap-1.5">
              {(summary.uncitedFindings ?? 0) > 0 && (
                <span className="inline-flex items-center rounded-md border border-rose-200/80 bg-rose-50 px-2 py-0.5 text-[11px] font-semibold text-rose-700 tabular-nums">
                  {summary.uncitedFindings} thiếu trích dẫn
                </span>
              )}
              {(summary.suspectedFindings ?? 0) > 0 && (
                <span className="inline-flex items-center rounded-md border border-amber-200/80 bg-amber-50 px-2 py-0.5 text-[11px] font-semibold text-amber-800 tabular-nums">
                  {summary.suspectedFindings} nghi sai nguồn
                </span>
              )}
              <span className="inline-flex items-center rounded-md border border-emerald-200/80 bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700 tabular-nums">
                {summary.citedFindings ?? 0} có trích dẫn
              </span>
            </div>
          </div>
        </div>
      )}

      {report.findings.length > 0 && (
        <div className="space-y-3">
          <div className="rounded-xl border border-slate-200/80 bg-white p-3.5 space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-2.5">
              <h4 className="text-xs font-semibold text-slate-700">
                Các đoạn văn bản đối chiếu ({visibleFindings.length}/{report.findings.length})
              </h4>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-8 rounded-lg bg-white px-2.5 text-xs"
                  onClick={() => setSortBy((prev) => (prev === 'score' ? 'page' : 'score'))}
                >
                  <ArrowUpDown className="size-3.5 mr-1.5 text-slate-500" />
                  {sortBy === 'score' ? 'Sắp xếp: Điểm cao nhất' : 'Sắp xếp: Theo trang'}
                </Button>
              </div>
            </div>

            <div className="flex flex-col gap-2.5 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex flex-wrap items-center gap-1.5">
                {filterChips.map((chip) => {
                  const isActive = findingFilter === chip.key;
                  return (
                    <button
                      key={chip.key}
                      type="button"
                      onClick={() => setFindingFilter(chip.key)}
                      aria-pressed={isActive}
                      className={cn(
                        'inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 text-xs font-medium transition-colors cursor-pointer select-none',
                        isActive
                          ? 'border-primary/40 bg-primary/8 text-primary'
                          : 'border-slate-200/80 bg-slate-50/70 text-slate-600 hover:bg-slate-100',
                      )}
                    >
                      <span>{chip.label}</span>
                      <span
                        className={cn(
                          'rounded-md px-1.5 py-0 text-[10px] font-semibold tabular-nums',
                          isActive ? 'bg-primary/15 text-primary' : 'bg-white text-slate-500',
                        )}
                      >
                        {chip.count}
                      </span>
                    </button>
                  );
                })}
              </div>

              <div className="relative w-full lg:w-72">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 size-3.5 text-slate-400" />
                <Input
                  type="search"
                  value={findingKeyword}
                  onChange={(e) => setFindingKeyword(e.target.value)}
                  placeholder="Tìm nội dung hoặc tài liệu nguồn..."
                  aria-label="Tìm trong các đoạn đối chiếu"
                  className="h-8 pl-8 pr-7 text-xs bg-white"
                />
                {findingKeyword && (
                  <button
                    type="button"
                    aria-label="Xóa từ khóa tìm đoạn đối chiếu"
                    onClick={() => setFindingKeyword('')}
                    className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 cursor-pointer"
                  >
                    <X className="size-3.5" />
                  </button>
                )}
              </div>
            </div>
          </div>

          {visibleFindings.length === 0 ? (
            <div className="rounded-xl border border-slate-200/80 bg-white px-4 py-6 text-center space-y-2">
              <p className="text-sm font-medium text-slate-700">
                Không có đoạn đối chiếu nào khớp bộ lọc hiện tại
              </p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  setFindingFilter('ALL');
                  setFindingKeyword('');
                }}
              >
                Hiện tất cả đoạn đối chiếu
              </Button>
            </div>
          ) : (
            <div className="space-y-3">
              {visibleFindings.map((finding, idx) => (
                <FindingCard
                  key={finding.passageGroupId ?? `${finding.submissionChunkNo ?? idx}-${idx}`}
                  finding={finding}
                />
              ))}
            </div>
          )}
        </div>
      )}

      {report.status === 'Completed' && report.findings.length === 0 && (
        <div className="rounded-xl border border-emerald-200/80 bg-emerald-50/70 px-4 py-3 text-sm text-emerald-800 flex items-center gap-2.5">
          <CheckCircle2 className="size-4 text-emerald-600 shrink-0" />
          <span>Không ghi nhận đoạn văn bản trùng lặp đáng kể cần rà soát.</span>
        </div>
      )}

      {report.disclaimer && (
        <p className="text-xs text-slate-500 pt-2 border-t border-slate-200/70 leading-relaxed">
          {report.disclaimer}
        </p>
      )}
    </div>
  );
}

function FindingCard({ finding }: { finding: SimilarityFinding }) {
  const citationMeta = finding.citationStatus
    ? (citationDisplay[finding.citationStatus] ?? {
        label: finding.citationStatus,
        tone: 'neutral' as StatusTone,
        cardTint: 'border-slate-200/80 bg-white',
      })
    : {
        label: 'Chưa phân loại',
        tone: 'neutral' as StatusTone,
        cardTint: 'border-slate-200/80 bg-white',
      };

  return (
    <article className="rounded-xl border border-slate-200/80 bg-white p-4 space-y-3 text-sm shadow-2xs">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <StatusBadge tone={citationMeta.tone}>{citationMeta.label}</StatusBadge>
          {finding.rerankerScore != null && (
            <span className="inline-flex items-center rounded-md border border-slate-200/80 bg-slate-50 px-2 py-0.5 text-xs font-medium text-slate-700 tabular-nums">
              Độ tương đồng: {formatRerankerScore(finding.rerankerScore)}
            </span>
          )}
          {finding.classifierScore != null && (
            <span className="text-[11px] text-slate-500 tabular-nums">
              Tin cậy phân loại: {Math.round(finding.classifierScore * 100)}%
            </span>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-1.5">
          {finding.potentialMisattribution && (
            <span className="inline-flex items-center gap-1 rounded-full border border-rose-200/80 bg-rose-50 px-2.5 py-0.5 text-[11px] font-medium text-rose-700">
              <AlertTriangle className="size-3" />
              Nghi sai nguồn trích dẫn
            </span>
          )}
          {finding.reviewRequired && (
            <span className="inline-flex items-center rounded-full border border-amber-200/80 bg-amber-50 px-2.5 py-0.5 text-[11px] font-medium text-amber-800">
              Cần GV rà soát
            </span>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <div className={cn('rounded-xl border p-3.5 space-y-1.5', citationMeta.cardTint)}>
          <div className="flex items-center justify-between gap-2 text-[11px] font-semibold text-slate-500">
            <span>Đoạn văn trong bài nộp</span>
            <span className="tabular-nums font-medium">
              {[
                finding.submissionPage ? `Trang ${finding.submissionPage}` : null,
                finding.submissionChunkNo != null ? `Đoạn #${finding.submissionChunkNo}` : null,
              ]
                .filter(Boolean)
                .join(' · ') || 'Toàn văn'}
            </span>
          </div>
          <p className="text-xs text-slate-800 leading-relaxed whitespace-pre-wrap">
            {finding.submissionText}
          </p>
        </div>

        <div className="rounded-xl border border-slate-200/80 bg-slate-50/60 p-3.5 space-y-1.5">
          <div className="flex items-center justify-between gap-2 text-[11px] font-semibold text-slate-500">
            <span className="truncate" title={finding.sourceFilename || finding.sourceKey || undefined}>
              Nguồn: {finding.sourceFilename || finding.sourceKey || 'Kho luận văn'}
            </span>
            {finding.sourcePage && (
              <span className="shrink-0 tabular-nums font-medium">
                Trang {finding.sourcePage}
              </span>
            )}
          </div>
          <p className="text-xs text-slate-800 leading-relaxed whitespace-pre-wrap">
            {finding.sourceText}
          </p>
        </div>
      </div>

      {finding.citationExplanation && (
        <div className="flex items-start gap-2 rounded-lg bg-slate-50/80 px-3 py-2 text-xs text-slate-600 leading-relaxed">
          <Info className="size-3.5 text-slate-400 shrink-0 mt-0.5" />
          <p>{finding.citationExplanation}</p>
        </div>
      )}
    </article>
  );
}
