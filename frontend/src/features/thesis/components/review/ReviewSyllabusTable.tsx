import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Download } from 'lucide-react';
import { SyllabusStatusBadge } from '../submission/SyllabusStatusBadge';
import type { ReviewGroupItemDto } from '../../types/review.types';

interface ReviewSyllabusTableProps {
  items: ReviewGroupItemDto[];
  selectedGroupId: string | null;
  onSelectGroup: (group: ReviewGroupItemDto) => void;
  onDownloadFile: (fileId: string, groupId?: string) => void;
  isLoading?: boolean;
}

export function ReviewSyllabusTable({
  items,
  selectedGroupId,
  onSelectGroup,
  onDownloadFile,
  isLoading = false,
}: ReviewSyllabusTableProps) {
  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-4">
      <h2 className="text-base font-bold text-slate-900">Hồ sơ</h2>

      <div className="overflow-x-auto">
        <table className="w-full text-left text-xs">
          <thead>
            <tr className="border-b border-slate-100 text-slate-400 font-medium">
              <th className="pb-3 font-medium">Nhóm sinh viên</th>
              <th className="pb-3 font-medium">Đề cương mới nhất</th>
              <th className="pb-3 font-medium">Trạng thái</th>
              <th className="pb-3 font-medium text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading ? (
              <tr>
                <td colSpan={4} className="py-8 text-center text-slate-400">
                  Đang tải danh sách hồ sơ...
                </td>
              </tr>
            ) : items.length === 0 ? (
              <tr>
                <td colSpan={4} className="py-8 text-center text-slate-400">
                  Không có hồ sơ nào phù hợp với bộ lọc.
                </td>
              </tr>
            ) : (
              items.map((item) => {
                const isSelected = item.groupId === selectedGroupId;
                return (
                  <tr
                    key={item.groupId}
                    className={`transition-colors ${
                      isSelected ? 'bg-blue-50/50' : 'hover:bg-slate-50/50'
                    }`}
                  >
                    <td className="py-3.5 pr-4">
                      <div className="font-semibold text-slate-800">
                        {item.groupCode} - {item.studentNames}
                      </div>
                      <div className="text-[11px] text-slate-500 line-clamp-1 mt-0.5">
                        {item.topicTitleVi}
                      </div>
                    </td>

                    <td className="py-3.5 pr-4">
                      {item.fileId ? (
                        <Button
                          type="button"
                          size="sm"
                          onClick={() => onDownloadFile(item.fileId!, item.groupId)}
                          className="h-8 px-3 gap-1.5 text-xs font-medium bg-gradient-to-r from-[#2054A5] to-[#FA7921] text-white hover:opacity-90 shadow-sm shadow-[#2054A5]/20 rounded-lg transition-all"
                          title={item.latestFileName}
                        >
                          <Download className="size-3.5 shrink-0" />
                          <span className="max-w-[130px] truncate">{item.latestFileName}</span>
                        </Button>
                      ) : (
                        <span className="text-slate-400 italic">Chưa có tệp</span>
                      )}
                    </td>

                    <td className="py-3.5 pr-4">
                      <SyllabusStatusBadge status={item.currentStatus} className="text-[11px]" />
                    </td>

                    <td className="py-3.5 text-right">
                      {isSelected ? (
                        <Button
                          type="button"
                          size="sm"
                          className="h-8 px-3 text-xs font-medium bg-[#2054A5] text-white hover:bg-[#1a4485]"
                        >
                          Đang chọn
                        </Button>
                      ) : (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => onSelectGroup(item)}
                          className="h-8 px-3 text-xs font-medium border-slate-200 text-slate-700 hover:bg-slate-50"
                        >
                          Xem hồ sơ
                        </Button>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      <div className="text-xs text-slate-400 pt-2 border-t border-slate-100">
        {items.length} hồ sơ hiển thị
      </div>
    </Card>
  );
}
