import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { MilestoneStatusBadge } from './MilestoneStatusBadge';
import { Download, ExternalLink, MessageSquare, Send } from 'lucide-react';
import type { ProgressReportVersionDto } from '../../types/milestone.types';

interface StudentMilestoneHistoryTableProps {
  versions: ProgressReportVersionDto[];
  nextVersionNo: number;
  selectedFile: File | null;
  groupCode?: string;
  canSubmit: boolean;
  isSubmitting: boolean;
  onSubmit: () => void;
  onDownload: (fileId: string) => void;
}

export function StudentMilestoneHistoryTable({
  versions,
  nextVersionNo,
  selectedFile,
  groupCode = 'Nhóm KLTN',
  canSubmit,
  isSubmitting,
  onSubmit,
  onDownload,
}: StudentMilestoneHistoryTableProps) {
  const [expandedVersionNo, setExpandedVersionNo] = useState<number | null>(null);

  const hasDraftRow = Boolean(selectedFile && canSubmit);
  const totalItems = versions.length + (hasDraftRow ? 1 : 0);

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-base font-semibold text-slate-900">Phiên bản</h3>
      </div>

      <div className="overflow-x-auto border border-slate-200/80 rounded-lg">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-slate-500 font-medium text-xs border-b border-slate-200/80">
            <tr>
              <th className="py-3 px-4">Nội dung</th>
              <th className="py-3 px-4">Thông tin</th>
              <th className="py-3 px-4">Trạng thái</th>
              <th className="py-3 px-4 text-right">Chi tiết</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {hasDraftRow && (
              <tr className="bg-blue-50/30">
                <td className="py-3 px-4 font-medium text-slate-900">
                  <span className="text-primary font-semibold">
                    v{nextVersionNo}
                  </span>
                </td>
                <td className="py-3 px-4 text-slate-600">{groupCode}</td>
                <td className="py-3 px-4">
                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
                    Sẵn sàng gửi
                  </span>
                </td>
                <td className="py-3 px-4 text-right text-xs text-slate-400 italic">
                  Bản mới
                </td>
              </tr>
            )}

            {versions.map((ver) => {
              const isExpanded = expandedVersionNo === ver.versionNo;
              const hasFeedback = Boolean(ver.review?.feedback);

              return (
                <tr key={ver.versionNo} className="hover:bg-slate-50/70 transition-colors">
                  <td className="py-3.5 px-4 font-medium text-slate-900">
                    <span className="font-semibold text-slate-700">
                      v{ver.versionNo}
                    </span>
                    {ver.file && (
                      <p className="text-xs text-slate-500 mt-0.5 truncate max-w-[200px]">
                        {ver.file.fileName}
                      </p>
                    )}
                    {hasFeedback && isExpanded && (
                      <div className="mt-2 p-2.5 rounded-lg bg-amber-50 border border-amber-200 text-xs text-amber-900 space-y-1">
                        <div className="font-semibold flex items-center gap-1.5 text-amber-800">
                          <MessageSquare className="size-3.5" />
                          Nhận xét của GVHD:
                        </div>
                        <p>{ver.review!.feedback}</p>
                      </div>
                    )}
                  </td>
                  <td className="py-3.5 px-4 text-slate-600 text-xs">
                    {ver.review?.reviewedBy ? 'GVHD' : groupCode}
                  </td>
                  <td className="py-3.5 px-4">
                    <MilestoneStatusBadge status={ver.status} versionNo={ver.versionNo} />
                  </td>
                  <td className="py-3.5 px-4 text-right">
                    <div className="flex items-center justify-end gap-1">
                      {hasFeedback && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="h-8 text-xs text-slate-600 hover:text-slate-900"
                          onClick={() =>
                            setExpandedVersionNo(isExpanded ? null : ver.versionNo)
                          }
                        >
                          <MessageSquare className="size-3.5 mr-1" />
                          {isExpanded ? 'Ẩn nhận xét' : 'Xem nhận xét'}
                        </Button>
                      )}
                      {ver.file && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="h-8 text-xs text-primary hover:text-primary hover:bg-blue-50"
                          onClick={() => onDownload(ver.file!.id)}
                        >
                          <Download className="size-3.5 mr-1" />
                          Tải tệp
                        </Button>
                      )}
                      {ver.gitUrl && (
                        <a
                          href={ver.gitUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="inline-flex items-center h-8 px-2 text-xs text-slate-500 hover:text-slate-900 hover:bg-slate-100 rounded-md transition-colors"
                        >
                          <ExternalLink className="size-3.5" />
                        </a>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}

            {totalItems === 0 && (
              <tr>
                <td colSpan={4} className="py-8 text-center text-slate-400 text-xs">
                  Chưa có phiên bản nào được nộp cho mốc này. Vui lòng chọn tệp và nộp sản phẩm.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between pt-2">
        <span className="text-xs text-slate-500">
          {totalItems} mục trong danh sách
        </span>

        <Button
          type="button"
          onClick={onSubmit}
          disabled={!selectedFile || !canSubmit || isSubmitting}
          className="bg-primary hover:bg-primary/90 text-white font-medium px-5 h-10 shadow-xs"
        >
          {isSubmitting ? (
            <span className="flex items-center gap-2">
              <span className="size-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              Đang gửi...
            </span>
          ) : (
            <span className="flex items-center gap-1.5">
              <Send className="size-4" />
              Nộp sản phẩm v{nextVersionNo}
            </span>
          )}
        </Button>
      </div>
    </Card>
  );
}