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
    <Card className="p-6 bg-surface-card border-line shadow-xs rounded-xl space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-bold text-ink-primary">Hồ sơ</h2>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left text-xs">
          <thead>
            <tr className="border-b border-line-subtle text-ink-subtle font-medium">
              <th className="pb-3 font-medium">Nhóm / đề tài</th>
              <th className="pb-3 font-medium">GVHD</th>
              <th className="pb-3 font-medium">Trạng thái</th>
              <th className="pb-3 font-medium text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line-subtle">
            {isLoading ? (
              <tr>
                <td colSpan={4} className="py-8 text-center text-ink-subtle">
                  Đang tải danh sách hồ sơ...
                </td>
              </tr>
            ) : items.length === 0 ? (
              <tr>
                <td colSpan={4} className="py-8 text-center text-ink-subtle">
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
                      isSelected
                        ? 'bg-tone-info-bg'
                        : 'hover:bg-surface-hover/60'
                    }`}
                  >
                    <td className="py-3.5 pr-4">
                      <div className="font-semibold text-ink-heading">
                        {groupLabel} · {item.topicTitleVi}
                      </div>
                    </td>

                    <td className="py-3.5 pr-4 text-ink-body">
                      {item.advisorFullName || 'Chưa xác định'}
                    </td>

                    <td className="py-3.5 pr-4">
                      {item.hasActiveAssignment ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium text-tone-success-text bg-tone-success-bg border border-tone-success-border">
                          Đã phân công
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium text-ink-secondary bg-surface-muted border border-line-input">
                          Chờ phân công
                        </span>
                      )}
                    </td>

                    <td className="py-3.5 pl-4 text-right">
                      {isSelected ? (
                        <span className="text-xs font-semibold text-tone-info-icon">
                          Đang chọn
                        </span>
                      ) : (
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            onSelectGroupId(item.groupId);
                          }}
                          className="text-xs text-ink-body hover:text-tone-info-icon font-medium cursor-pointer"
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

      <div className="pt-2 text-xs text-ink-subtle">
        {items.length} hồ sơ hiển thị
      </div>
    </Card>
  );
}