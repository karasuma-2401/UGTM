import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Download, CheckCircle2 } from 'lucide-react';
import { SyllabusStatusBadge } from '../submission/SyllabusStatusBadge';
import type { ReviewGroupItemDto } from '../../types/review.types';

interface ReviewTopicDetailCardProps {
  selectedGroup: ReviewGroupItemDto | null;
  onDownloadFile: (fileId: string) => void;
  disabled?: boolean;
  hasDownloadedFile?: boolean;
}

export function ReviewTopicDetailCard({
  selectedGroup,
  onDownloadFile,
  disabled = false,
  hasDownloadedFile = false,
}: ReviewTopicDetailCardProps) {
  if (!selectedGroup) {
    return (
      <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl text-center text-slate-400 text-xs py-12">
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
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-4">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3">
        <h3 className="text-base font-bold text-slate-900">
          Đề cương {groupCode}
        </h3>
        <SyllabusStatusBadge status={currentStatus} className="text-[11px]" />
      </div>

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">Hồ sơ</p>
        <p className="text-sm font-semibold text-slate-800">
          {topicCode} · {groupCode}
        </p>
        {topicTitleVi && topicTitleVi !== 'Chưa có dữ liệu' && (
          <p className="text-xs text-slate-600 line-clamp-2 mt-0.5">{topicTitleVi}</p>
        )}
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">Sinh viên</p>
        <p className="text-xs text-slate-700">{studentNames}</p>
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">GVHD</p>
        <p className="text-xs text-slate-700">{advisorName}</p>
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">Đợt</p>
        <p className="text-xs text-slate-700">{batchName}</p>
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-2.5">
        <div className="flex items-center justify-between">
          <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">
            Tài liệu đối chiếu
          </p>
          {hasDownloadedFile ? (
            <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-200">
              <CheckCircle2 className="size-3" />
              Đã tải
            </span>
          ) : (
            <span className="text-[11px] font-medium text-amber-600 bg-amber-50 px-2 py-0.5 rounded-full border border-amber-200">
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
            <span className="truncate">Tải đề cương ({latestFileName})</span>
          </Button>
        ) : (
          <div className="text-xs text-slate-400 italic bg-slate-50 p-2.5 rounded-lg border border-slate-100 text-center">
            Chưa có tệp nộp
          </div>
        )}

        <div className="flex items-center justify-between text-xs text-slate-500 pt-0.5">
          <span>Phiên bản:</span>
          <span className="text-slate-700 font-medium">
            v{latestVersionNo} {submittedAtUtc ? `· ${new Date(submittedAtUtc).toLocaleDateString('vi-VN')}` : ''}
          </span>
        </div>
      </div>

      <Separator className="bg-slate-100" />

      {disabled && (
        <div className="p-2.5 rounded-lg bg-slate-50 border border-slate-200 text-slate-500 text-[11px] text-center">
          Hồ sơ đã hoàn tất thẩm định và được khóa chỉnh sửa.
        </div>
      )}

      <div className="space-y-1.5">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">Kết luận</p>
        <div className="text-xs text-slate-600 leading-relaxed space-y-1 bg-slate-50 p-3 rounded-lg">
          <p>
            <strong className="text-emerald-700">Đạt:</strong> Mở giai đoạn thực hiện.
          </p>
          <p>
            <strong className="text-amber-700">Cần sửa:</strong> Cho nộp lại trước hạn chốt đóng cổng.
          </p>
          <p>
            <strong className="text-rose-700">Không đạt:</strong> Dừng KLTN và khóa bước sau.
          </p>
        </div>
      </div>
    </Card>
  );
}
