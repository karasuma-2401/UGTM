import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { FileText, ExternalLink, Download } from 'lucide-react';
import type { SubmissionFileDto } from '../../types/syllabus.types';

interface AdvisorMilestoneSubmissionViewCardProps {
  versionNo: number;
  groupCode?: string;
  file?: SubmissionFileDto | null;
  gitUrl?: string | null;
  studentNote?: string | null;
  onDownloadFile: (fileId: string) => void;
  isDownloading?: boolean;
}

export function AdvisorMilestoneSubmissionViewCard({
  versionNo,
  groupCode = 'Nhóm KLTN',
  file,
  gitUrl,
  studentNote,
  onDownloadFile,
  isDownloading = false,
}: AdvisorMilestoneSubmissionViewCardProps) {
  const formatFileSize = (bytes?: number): string => {
    if (!bytes) return '';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-base font-semibold text-slate-900">
          Sản phẩm v{versionNo}
        </h3>
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
            <tr className="hover:bg-slate-50/70 transition-colors">
              <td className="py-3.5 px-4 font-medium text-slate-900">
                <div className="flex items-center gap-2.5">
                  <div className="p-1.5 rounded bg-blue-50 text-primary border border-blue-100 shrink-0">
                    <FileText className="size-4" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-xs font-semibold text-slate-800 truncate max-w-[260px]">
                      {file?.fileName || 'Chưa có tệp báo cáo'}
                    </p>
                    {file?.fileSize && (
                      <p className="text-[11px] text-slate-400">
                        {formatFileSize(file.fileSize)}
                      </p>
                    )}
                  </div>
                </div>
              </td>
              <td className="py-3.5 px-4 text-slate-600 text-xs">
                {groupCode}
              </td>
              <td className="py-3.5 px-4">
                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-50 text-emerald-700 border border-emerald-200/60">
                  {file ? 'Đã nộp' : 'Chưa có dữ liệu'}
                </span>
              </td>
              <td className="py-3.5 px-4 text-right">
                {file ? (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="h-8 text-xs font-semibold text-primary hover:text-primary hover:bg-blue-50"
                    onClick={() => onDownloadFile(file.id)}
                    disabled={isDownloading}
                  >
                    <Download className="size-3.5 mr-1.5" />
                    Xem tệp
                  </Button>
                ) : (
                  <span className="text-xs text-slate-400 italic">Không có</span>
                )}
              </td>
            </tr>

            <tr className="hover:bg-slate-50/70 transition-colors">
              <td className="py-3.5 px-4 font-medium text-slate-900">
                <div className="flex items-center gap-2.5">
                  <div className="p-1.5 rounded bg-slate-100 text-slate-600 border border-slate-200 shrink-0">
                    <ExternalLink className="size-4" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-xs font-semibold text-slate-800">
                      Kho mã nguồn
                    </p>
                    {gitUrl && (
                      <p className="text-[11px] text-slate-400 truncate max-w-[260px]">
                        {gitUrl}
                      </p>
                    )}
                  </div>
                </div>
              </td>
              <td className="py-3.5 px-4 text-slate-600 text-xs">
                {groupCode}
              </td>
              <td className="py-3.5 px-4">
                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-700 border border-blue-200/60">
                  {gitUrl ? 'Đã cập nhật' : 'Chưa có dữ liệu'}
                </span>
              </td>
              <td className="py-3.5 px-4 text-right">
                {gitUrl ? (
                  <Button
                    asChild
                    variant="ghost"
                    size="sm"
                    className="h-8 text-xs font-semibold text-primary hover:text-primary hover:bg-blue-50"
                  >
                    <a
                      href={gitUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      <ExternalLink className="size-3.5 mr-1.5" />
                      Mở link
                    </a>
                  </Button>
                ) : (
                  <span className="text-xs text-slate-400 italic">Không có</span>
                )}
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      {studentNote && (
        <div className="p-3 rounded-lg bg-slate-50 border border-slate-200 text-xs text-slate-700 space-y-1">
          <p className="font-semibold text-slate-800">Ghi chú của sinh viên:</p>
          <p className="leading-relaxed">{studentNote}</p>
        </div>
      )}

      <div className="pt-1">
        <span className="text-xs text-slate-500">2 mục trong danh sách</span>
      </div>
    </Card>
  );
}

