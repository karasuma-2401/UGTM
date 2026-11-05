import { Card } from '@/components/ui/card';
import type { ReviewerAssignmentEligibilityItemDto } from '../../types/reviewerAssignment.types';

interface ReviewerAssignmentTableProps {
  items: ReviewerAssignmentEligibilityItemDto[];
  selectedGroupId: string | null;
  onSelectGroupId: (groupId: string) => void;
  isLoading?: boolean;
}

export function ReviewerAssignmentTable({
  items,
  selectedGroupId,
  onSelectGroupId,
  isLoading = false,
}: ReviewerAssignmentTableProps) {
  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-bold text-slate-900">Hồ sơ</h2>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left text-xs">
          <thead>
            <tr className="border-b border-slate-100 text-slate-400 font-medium">
              <th className="pb-3 font-medium">Nhóm / đề tài</th>
              <th className="pb-3 font-medium">GVHD</th>
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
              items.map((item, index) => {
                const isSelected = item.groupId === selectedGroupId;
                const groupLabel = `N${String(index + 1).padStart(2, '0')}`;

                return (
                  <tr
                    key={item.groupId}
                    onClick={() => onSelectGroupId(item.groupId)}
                    className={`cursor-pointer transition-colors ${
                      isSelected ? 'bg-blue-50/60' : 'hover:bg-slate-50/60'
                    }`}
                  >
                    <td className="py-3.5 pr-4">
                      <div className="font-semibold text-slate-800">
                        {groupLabel} · {item.topicTitleVi}
                      </div>
                    </td>

                    <td className="py-3.5 pr-4 text-slate-600">
                      {item.advisorFullName || 'Chưa xác định'}
                    </td>

                    <td className="py-3.5 pr-4">
                      {item.hasActiveAssignment ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium text-emerald-700 bg-emerald-50 border border-emerald-200/60">
                          Đã phân công
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium text-slate-700 bg-slate-100 border border-slate-200">
                          Chờ phân công
                        </span>
                      )}
                    </td>

                    <td className="py-3.5 pl-4 text-right">
                      {isSelected ? (
                        <span className="text-xs font-semibold text-blue-600">
                          Đang chọn
                        </span>
                      ) : (
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            onSelectGroupId(item.groupId);
                          }}
                          className="text-xs text-slate-600 hover:text-blue-600 font-medium cursor-pointer"
                        >
                          {item.hasActiveAssignment ? 'Xem phân công' : 'Xem hồ sơ'}
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      <div className="pt-2 text-xs text-slate-400">
        {items.length} hồ sơ hiển thị
      </div>
    </Card>
  );
}