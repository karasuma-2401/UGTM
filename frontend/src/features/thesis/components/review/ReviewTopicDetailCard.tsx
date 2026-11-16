import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Download, CheckCircle2 } from 'lucide-react';
import { SyllabusStatusBadge } from '../submission/SyllabusStatusBadge';
import type { ReviewGroupItemDto } from '../../types/review.types';
import type { ReviewMidtermGroupItemDto } from '../../types/midterm.types';

interface ReviewTopicDetailCardProps {
  selectedGroup: ReviewGroupItemDto | ReviewMidtermGroupItemDto | null;
  onDownloadFile: (fileId: string) => void;
  disabled?: boolean;
  hasDownloadedFile?: boolean;
  gateTitle?: string;
}

export function ReviewTopicDetailCard({
  selectedGroup,
  onDownloadFile,
  disabled = false,
  hasDownloadedFile = false,
  gateTitle = 'Đề cương',
}: ReviewTopicDetailCardProps) {
  if (!selectedGroup) {
    return (
      <Card className="p-6 bg-surface-card border-line shadow-xs rounded-xl text-center text-ink-subtle text-xs py-12">
        Chọn một nhóm trong danh sách để xem chi tiết hồ sơ.
      </Card>
    );
  }

  const {
    groupCode,
    topicCode,
    topicTitleVi,
    studentNames,
    advisorName,
    batchName,
    latestFileName,
    latestVersionNo,
    fileId,
    submittedAtUtc,
    currentStatus,
  } = selectedGroup;

  return (
    <Card className="p-6 bg-surface-card border-line shadow-xs rounded-xl space-y-4">
      <div className="flex items-center justify-between border-b border-line-subtle pb-3">
        <h3 className="text-base font-bold text-ink-primary">
          {gateTitle} {groupCode}
        </h3>
        <SyllabusStatusBadge status={currentStatus as any} className="text-[11px]" />
      </div>

      <div className="space-y-1">
        <p className="text-xs font-medium text-ink-subtle uppercase tracking-wider">Hồ sơ</p>
        <p className="text-sm font-semibold text-ink-heading">
          {topicCode} - {groupCode}
        </p>
        {topicTitleVi && topicTitleVi !== 'Chưa có dữ liệu' && (
          <p className="text-xs text-ink-body line-clamp-2 mt-0.5">{topicTitleVi}</p>
        )}
      </div>

      <Separator className="bg-surface-muted" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-ink-subtle uppercase tracking-wider">Sinh viên</p>
        <p className="text-xs text-ink-secondary">{studentNames}</p>
      </div>

      <Separator className="bg-surface-muted" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-ink-subtle uppercase tracking-wider">GVHD</p>
        <p className="text-xs text-ink-secondary">{advisorName}</p>
      </div>

      <Separator className="bg-surface-muted" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-ink-subtle uppercase tracking-wider">Đợt</p>
        <p className="text-xs text-ink-secondary">{batchName}</p>
      </div>

      <Separator className="bg-surface-muted" />

      <div className="space-y-2.5">
        <div className="flex items-center justify-between">
          <p className="text-xs font-medium text-ink-subtle uppercase tracking-wider">
            Tài liệu đối chiếu
          </p>
          {hasDownloadedFile ? (
            <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-tone-success-icon bg-tone-success-bg px-2 py-0.5 rounded-full border border-tone-success-border">
              <CheckCircle2 className="size-3" />
              Đã tải
            </span>
          ) : (
            <span className="text-[11px] font-medium text-tone-warning-icon bg-tone-warning-bg px-2 py-0.5 rounded-full border border-tone-warning-border">
              Chưa tải
            </span>
          )}
        </div>

        {fileId ? (
          <Button
            type="button"
            onClick={() => onDownloadFile(fileId)}
            className="w-full h-10 gap-2 bg-gradient-to-r from-[#2054A5] to-[#FA7921] text-white font-medium text-xs rounded-lg shadow-sm shadow-[#2054A5]/25 hover:opacity-90 active:scale-[0.99] transition-all"
          >
            <Download className="size-4 shrink-0" />
            <span className="truncate">Tải {gateTitle.toLowerCase()} ({latestFileName})</span>
          </Button>
        ) : (
          <div className="text-xs text-ink-subtle italic bg-surface-subtle p-2.5 rounded-lg border border-line-subtle text-center">
            Chưa có tệp nộp
          </div>
        )}

        <div className="flex items-center justify-between text-xs text-ink-muted pt-0.5">
          <span>Phiên bản:</span>
          <span className="text-ink-secondary font-medium">
            v{latestVersionNo} {submittedAtUtc ? `- ${new Date(submittedAtUtc).toLocaleDateString('vi-VN')}` : ''}
          </span>
        </div>
      </div>

      <Separator className="bg-surface-muted" />

      {disabled && (
        <div className="p-2.5 rounded-lg bg-surface-subtle border border-line text-ink-muted text-[11px] text-center">
          Hồ sơ đã hoàn tất thẩm định và được khóa chỉnh sửa.
        </div>
      )}

      <div className="space-y-1.5">
        <p className="text-xs font-medium text-ink-subtle uppercase tracking-wider">Kết luận</p>
        <div className="text-xs text-ink-body leading-relaxed space-y-1 bg-surface-subtle p-3 rounded-lg">
          <p>
            <strong className="text-tone-success-text">Đạt:</strong> Mở giai đoạn tiếp theo.
          </p>
          <p>
            <strong className="text-tone-warning-text">Cần sửa:</strong> Cho nộp lại trước hạn chốt đóng cổng.
          </p>
          <p>
            <strong className="text-tone-danger-text">Không đạt:</strong> Dừng KLTN và khóa bước sau.
          </p>
        </div>
      </div>
    </Card>
  );
}
