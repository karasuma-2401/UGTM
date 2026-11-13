import { Card } from '@/components/ui/card';
import type { CouncilDraftItemDto } from '../../types/councilApproval.types';

interface CouncilApprovalTableProps {
  items: CouncilDraftItemDto[];
  selectedCouncilId: string | null;
  onSelectCouncilId: (id: string) => void;
  isLoading?: boolean;
}

const STATUS_BADGE_MAP: Record<
  CouncilDraftItemDto['status'],
  { label: string; className: string }
> = {
  Draft: {
    label: 'Chờ phê duyệt',
    className: 'text-slate-700 bg-slate-100 border-slate-200',
  },
  Active: {
    label: 'Đã phê duyệt',
    className: 'text-emerald-700 bg-emerald-50 border-emerald-200/60',
  },
  Completed: {
    label: 'Đã hoàn thành',
    className: 'text-blue-700 bg-blue-50 border-blue-200/60',
  },
};

export function CouncilApprovalTable({
  items,
  selectedCouncilId,
  onSelectCouncilId,
  isLoading = false,
}: CouncilApprovalTableProps) {
  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-bold text-slate-900">Dự thảo</h2>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left text-xs">
          <thead>
            <tr className="border-b border-slate-100 text-slate-400 font-medium">
              <th className="pb-3 font-medium">Hội đồng / Khoa</th>
              <th className="pb-3 font-medium">Nhóm / Lịch dự thảo</th>
              <th className="pb-3 font-medium">Trạng thái</th>
              <th className="pb-3 font-medium text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading ? (
              <tr>
                <td colSpan={4} className="py-8 text-center text-slate-400">
                  Đang tải danh sách dự thảo Hội đồng...
                </td>
              </tr>
            ) : items.length === 0 ? (
              <tr>
                <td colSpan={4} className="py-8 text-center text-slate-400">
                  Không có dự thảo nào phù hợp với bộ lọc.
                </td>
              </tr>
            ) : (
              items.map((item) => {
                const isSelected = item.id === selectedCouncilId;
                const badge = STATUS_BADGE_MAP[item.status] || STATUS_BADGE_MAP.Draft;

                return (
                  <tr
                    key={item.id}
                    onClick={() => onSelectCouncilId(item.id)}
                    className={`cursor-pointer transition-colors ${
                      isSelected ? 'bg-blue-50/60' : 'hover:bg-slate-50/60'
                    }`}
                  >
                    <td className="py-3.5 pr-4">
                      <div className="font-semibold text-slate-800">
                        {item.councilCode} · {item.facultyName}
                      </div>
                    </td>

                    <td className="py-3.5 pr-4 text-slate-600">
                      {item.groupCode} · {item.room} · {item.timeSlot}
                    </td>

                    <td className="py-3.5 pr-4">
                      <span
                        className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border ${badge.className}`}
                      >
                        {item.statusLabel || badge.label}
                      </span>
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
                            onSelectCouncilId(item.id);
                          }}
                          className="text-xs text-slate-600 hover:text-blue-600 font-medium cursor-pointer"
                        >
                          Xem dự thảo
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