import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { TablePagination } from '@/components/ui/table-pagination';
import { Download, AlertCircle, ShieldAlert, RotateCcw, Inbox } from 'lucide-react';
import { SyllabusStatusBadge } from '../submission/SyllabusStatusBadge';
import type { ReviewGroupItemDto } from '../../types/review.types';
import type { ThesisErrorKind } from '../../utils/thesisErrors';

interface ReviewSyllabusTableProps {
  items: ReviewGroupItemDto[];
  selectedGroupId: string | null;
  onSelectGroup: (group: ReviewGroupItemDto) => void;
  onDownloadFile: (fileId: string, groupId?: string) => void;
  isLoading?: boolean;
  errorKind?: ThesisErrorKind | null;
  errorMessage?: string | null;
  onRetry?: () => void;
  pageNumber?: number;
  pageSize?: number;
  totalCount?: number;
  hasPreviousPage?: boolean;
  hasNextPage?: boolean;
  onPageChange?: (page: number) => void;
}

export function ReviewSyllabusTable({
  items,
  selectedGroupId,
  onSelectGroup,
  onDownloadFile,
  isLoading = false,
  errorKind = null,
  errorMessage = null,
  onRetry,
  pageNumber = 1,
  pageSize = 10,
  totalCount,
  hasPreviousPage,
  hasNextPage,
  onPageChange,
}: ReviewSyllabusTableProps) {
  const resolvedTotalCount = totalCount ?? items.length;

  return (
    <Card className="bg-white border-slate-200 shadow-xs rounded-xl overflow-hidden">
      <div className="p-6 space-y-4">
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
                  <td colSpan={4} className="py-10 text-center text-slate-400">
                    Đang tải danh sách hồ sơ...
                  </td>
                </tr>
              ) : errorKind === 'forbidden' ? (
                <tr>
                  <td colSpan={4} className="py-10 text-center">
                    <div className="max-w-md mx-auto space-y-2">
                      <ShieldAlert className="size-6 text-amber-600 mx-auto" />
                      <p className="text-sm font-semibold text-slate-800">
                        Không có quyền truy cập danh sách hồ sơ
                      </p>
                      <p className="text-xs text-slate-500">
                        {errorMessage ||
                          'Tài khoản của bạn không có quyền xem hoặc thẩm định danh sách đề cương.'}
                      </p>
                    </div>
                  </td>
                </tr>
              ) : errorKind ? (
                <tr>
                  <td colSpan={4} className="py-10 text-center">
                    <div className="max-w-md mx-auto space-y-3">
                      <AlertCircle className="size-6 text-rose-600 mx-auto" />
                      <div>
                        <p className="text-sm font-semibold text-slate-800">
                          Không tải được danh sách hồ sơ
                        </p>
                        <p className="text-xs text-slate-500 mt-0.5">
                          {errorMessage || 'Đã xảy ra lỗi khi tải dữ liệu từ máy chủ.'}
                        </p>
                      </div>
                      {onRetry && (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={onRetry}
                          className="h-8 text-xs border-slate-200"
                        >
                          <RotateCcw className="size-3.5 mr-1.5" />
                          Thử lại
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={4} className="py-10 text-center">
                    <div className="max-w-sm mx-auto space-y-1.5 text-slate-400">
                      <Inbox className="size-6 mx-auto text-slate-300" />
                      <p className="text-xs font-medium text-slate-500">
                        Không có dữ liệu hồ sơ phù hợp với bộ lọc.
                      </p>
                    </div>
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
      </div>

      {onPageChange ? (
        <TablePagination
          pageNumber={pageNumber}
          pageSize={pageSize}
          totalCount={resolvedTotalCount}
          hasPreviousPage={hasPreviousPage}
          hasNextPage={hasNextPage}
          isLoading={isLoading}
          itemLabel="hồ sơ"
          onPageChange={onPageChange}
        />
      ) : (
        <div className="px-6 py-3 text-xs text-slate-400 border-t border-slate-100">
          {items.length} hồ sơ hiển thị
        </div>
      )}
    </Card>
  );
}
