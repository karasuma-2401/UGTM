import { useRef } from 'react';
import {
  CheckCircle2,
  ExternalLink,
  FileText,
  ShieldAlert,
  UploadCloud,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DatePicker } from '@/components/ui/calendar';
import { Input } from '@/components/ui/input';
import type {
  CouncilDraftItemDto,
  CouncilScheduleResponseDto,
} from '../../types/councilApproval.types';
import {
  formatViDate,
  formatViDateTime,
  type CouncilReadinessResult,
} from '../../utils/councilApprovalReadiness';

interface CouncilApprovalDecisionCardProps {
  selectedCouncil: CouncilDraftItemDto | null;
  readiness: CouncilReadinessResult | null;
  schedule?: CouncilScheduleResponseDto;
  selectedFile: File | null;
  onFileSelect: (file: File | null) => void;
  decisionFileUrl: string;
  onDecisionFileUrlChange: (val: string) => void;
  fileError?: string | null;
  decisionNumber: string;
  onDecisionNumberChange: (val: string) => void;
  signedDate: string;
  onSignedDateChange: (val: string) => void;
  onRequestApprove: () => void;
  isApproving: boolean;
  canApprove: boolean;
}

function isSafeHttpUrl(value?: string | null): boolean {
  if (!value) return false;
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

export function CouncilApprovalDecisionCard({
  selectedCouncil,
  readiness,
  schedule,
  selectedFile,
  onFileSelect,
  decisionFileUrl,
  onDecisionFileUrlChange,
  fileError,
  decisionNumber,
  onDecisionNumberChange,
  signedDate,
  onSignedDateChange,
  onRequestApprove,
  isApproving,
  canApprove,
}: CouncilApprovalDecisionCardProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);

  if (!selectedCouncil) {
    return null;
  }

  const isAlreadyApproved =
    selectedCouncil.status === 'Active' || selectedCouncil.status === 'Completed';

  if (isAlreadyApproved) {
    const fileValue = selectedCouncil.decisionFileUrl;
    const displayDecisionNumber =
      selectedCouncil.decisionNumber || schedule?.decisionNumber || 'Chưa cập nhật';
    const displaySignedDate = selectedCouncil.signedDate || schedule?.signedDate;
    const hasLink = isSafeHttpUrl(fileValue);

    return (
      <div className="p-5 bg-surface-subtle/30 space-y-3">
        <div className="flex items-center gap-2 text-xs font-semibold text-tone-success-text">
          <CheckCircle2 className="size-4 text-tone-success-icon shrink-0" />
          <span>
            {selectedCouncil.status === 'Completed'
              ? 'Quyết định đã ban hành · Đã hoàn thành bảo vệ'
              : 'Quyết định thành lập Hội đồng đã được ban hành'}
          </span>
        </div>

        <dl className="grid grid-cols-2 gap-3 rounded-xl border border-tone-success-border bg-tone-success-bg/40 p-3.5 text-xs">
          <div>
            <dt className="text-[11px] text-ink-muted">Số quyết định</dt>
            <dd className="font-semibold text-ink-heading tabular-nums mt-0.5">
              {displayDecisionNumber}
            </dd>
          </div>
          <div>
            <dt className="text-[11px] text-ink-muted">Ngày ký</dt>
            <dd className="font-semibold text-ink-heading tabular-nums mt-0.5">
              {formatViDate(displaySignedDate)}
            </dd>
          </div>
          {selectedCouncil.approvedAt && (
            <div className="col-span-2">
              <dt className="text-[11px] text-ink-muted">Thời điểm phê duyệt</dt>
              <dd className="font-medium text-ink-body tabular-nums mt-0.5">
                {formatViDateTime(selectedCouncil.approvedAt)}
              </dd>
            </div>
          )}
          <div className="col-span-2">
            <dt className="text-[11px] text-ink-muted">Tệp quyết định</dt>
            <dd className="mt-0.5">
              {fileValue ? (
                hasLink ? (
                  <a
                    href={fileValue}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1.5 font-medium text-primary hover:underline break-all"
                  >
                    <FileText className="size-3.5 shrink-0" />
                    <span>{fileValue}</span>
                    <ExternalLink className="size-3 shrink-0" />
                  </a>
                ) : (
                  <span className="inline-flex items-center gap-1.5 font-medium text-ink-heading break-all">
                    <FileText className="size-3.5 text-tone-info-icon shrink-0" />
                    {fileValue}
                  </span>
                )
              ) : (
                <span className="text-ink-subtle">Chưa đính kèm tệp quyết định</span>
              )}
            </dd>
          </div>
        </dl>
      </div>
    );
  }

  const isReady = Boolean(readiness?.isReady);
  const canSubmit = canApprove && isReady && Boolean(decisionNumber.trim()) && !fileError && !isApproving;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] ?? null;
    onFileSelect(file);
  };

  const handleClearFile = () => {
    onFileSelect(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  return (
    <div className="p-5 space-y-4">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-ink-heading">
          Ban hành quyết định thành lập
        </h3>
        <span className="text-[11px] text-ink-subtle">Bắt buộc có *</span>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-1 2xl:grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <label
            htmlFor="decision-number-input"
            className="block text-xs font-medium text-ink-secondary"
          >
            Số quyết định <span className="text-tone-danger-icon">*</span>
          </label>
          <Input
            id="decision-number-input"
            type="text"
            value={decisionNumber}
            onChange={(e) => onDecisionNumberChange(e.target.value)}
            maxLength={100}
            placeholder="VD: 123/QĐ-ĐHCNTT"
            className="h-9 text-xs rounded-xl bg-surface-subtle/50 border-line-input tabular-nums focus-visible:bg-surface-card"
          />
        </div>

        <div className="space-y-1.5">
          <label
            htmlFor="decision-signed-date"
            className="block text-xs font-medium text-ink-secondary"
          >
            Ngày ký quyết định
          </label>
          <DatePicker
            id="decision-signed-date"
            value={signedDate}
            onValueChange={onSignedDateChange}
            aria-label="Ngày ký quyết định"
            className="h-9 text-xs rounded-xl"
          />
        </div>
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between gap-2">
          <label
            htmlFor="decision-file-url-input"
            className="text-xs font-medium text-ink-secondary"
          >
            Tệp / Đường dẫn quyết định (PDF)
          </label>
          <span className="text-[11px] text-ink-subtle">PDF ≤ 20 MB hoặc URL</span>
        </div>

        <input
          ref={fileInputRef}
          type="file"
          accept="application/pdf"
          onChange={handleFileChange}
          className="hidden"
        />

        {selectedFile ? (
          <div className="flex items-center justify-between gap-2.5 rounded-xl border border-line bg-surface-subtle/60 px-3 py-2.5">
            <div className="flex items-center gap-2.5 min-w-0">
              <div className="size-8 rounded-lg bg-tone-info-bg text-tone-info-icon flex items-center justify-center shrink-0">
                <FileText className="size-4" />
              </div>
              <div className="min-w-0">
                <p className="text-xs font-medium text-ink-heading truncate">
                  {selectedFile.name}
                </p>
                <p className="text-[11px] text-ink-muted tabular-nums">
                  {(selectedFile.size / (1024 * 1024)).toFixed(2)} MB · Lưu tên tệp tham chiếu
                </p>
              </div>
            </div>
            <div className="flex items-center gap-1.5 shrink-0">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => fileInputRef.current?.click()}
                className="h-7 px-2.5 text-[11px]"
              >
                Đổi tệp
              </Button>
              <button
                type="button"
                onClick={handleClearFile}
                aria-label="Xóa tệp đã chọn"
                className="p-1 rounded-md text-ink-subtle hover:bg-surface-muted hover:text-tone-danger-icon cursor-pointer"
              >
                <X className="size-3.5" />
              </button>
            </div>
          </div>
        ) : (
          <div className="flex items-center gap-2">
            <Input
              id="decision-file-url-input"
              type="text"
              value={decisionFileUrl}
              onChange={(e) => onDecisionFileUrlChange(e.target.value)}
              maxLength={500}
              placeholder="Nhập URL tệp PDF hoặc chọn tệp từ máy..."
              className="h-9 text-xs rounded-xl bg-surface-subtle/50 border-line-input flex-1 min-w-0 focus-visible:bg-surface-card"
            />
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => fileInputRef.current?.click()}
              className="h-9 px-3 text-xs rounded-xl shrink-0"
            >
              <UploadCloud className="size-3.5 mr-1.5" />
              Chọn PDF
            </Button>
          </div>
        )}

        {fileError && (
          <p role="alert" className="text-xs text-tone-danger-text">
            {fileError}
          </p>
        )}
      </div>

      {!canApprove && (
        <div className="rounded-xl border border-tone-warning-border bg-tone-warning-bg/70 px-3 py-2.5 flex items-start gap-2 text-xs text-tone-warning-strong">
          <ShieldAlert className="size-4 text-tone-warning-icon shrink-0 mt-0.5" />
          <span>
            Chỉ tài khoản Phòng Đào tạo hoặc Quản trị viên mới có quyền phê duyệt quyết định thành lập Hội đồng.
          </span>
        </div>
      )}

      {canApprove && !isReady && readiness && readiness.blockingReasons.length > 0 && (
        <div className="rounded-xl border border-tone-warning-border bg-tone-warning-bg/60 px-3 py-2.5 text-xs text-tone-warning-strong">
          <span className="font-medium">Chưa đủ điều kiện phê duyệt:</span>{' '}
          {readiness.blockingReasons[0]}
        </div>
      )}

      <div className="pt-1 flex items-center justify-end">
        <Button
          type="button"
          onClick={onRequestApprove}
          disabled={!canSubmit}
          isLoading={isApproving}
          loadingText="Đang phê duyệt..."
          className="w-full sm:w-auto h-10 px-5 rounded-xl bg-brand-solid hover:bg-brand-solid-hover text-white font-semibold text-xs shadow-xs"
        >
          Phê duyệt hội đồng
        </Button>
      </div>
    </div>
  );
}
