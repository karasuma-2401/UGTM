import { Card } from '@/components/ui/card';
import type { CouncilDraftItemDto } from '../../types/councilApproval.types';

interface CouncilApprovalDetailCardProps {
  selectedCouncil: CouncilDraftItemDto | null;
  attachedFileName?: string | null;
}

export function CouncilApprovalDetailCard({
  selectedCouncil,
  attachedFileName,
}: CouncilApprovalDetailCardProps) {
  const displayDecisionFile =
    attachedFileName || selectedCouncil?.decisionFileUrl || 'Chưa đính kèm';

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-6">
      <h2 className="text-base font-bold text-slate-900">
        Dự thảo đang chọn
      </h2>

      {!selectedCouncil ? (
        <div className="py-8 text-center text-xs text-slate-400">
          Chưa chọn dự thảo nào.
        </div>
      ) : (
        <div className="space-y-5 text-xs">
          <div>
            <span className="block text-slate-400 font-medium mb-1">
              Dự thảo
            </span>
            <p className="text-slate-800 font-semibold">
              {selectedCouncil.councilCode} · {selectedCouncil.facultyName}
            </p>
          </div>

          <div>
            <span className="block text-slate-400 font-medium mb-1">
              Nhóm
            </span>
            <p className="text-slate-800 font-medium">
              {selectedCouncil.groupCode} · {selectedCouncil.groupConditionLabel}
            </p>
          </div>

          <div>
            <span className="block text-slate-400 font-medium mb-1">
              Thành viên
            </span>
            <p className="text-slate-800 font-medium">
              {selectedCouncil.membersSummary}
            </p>
            {selectedCouncil.chairmanName && (
              <p className="text-[11px] text-slate-500 mt-0.5">
                Chủ tịch: {selectedCouncil.chairmanName}
              </p>
            )}
            {selectedCouncil.secretaryName && (
              <p className="text-[11px] text-slate-500">
                Thư ký: {selectedCouncil.secretaryName}
              </p>
            )}
          </div>

          <div>
            <span className="block text-slate-400 font-medium mb-1">
              Lịch
            </span>
            <p className="text-slate-800 font-medium">
              {selectedCouncil.room} · {selectedCouncil.timeSlot}
            </p>
          </div>

          <div>
            <span className="block text-slate-400 font-medium mb-1">
              Quyết định
            </span>
            <p className="text-slate-800 font-medium">
              {displayDecisionFile}
            </p>
          </div>

          <div className="pt-2 border-t border-slate-100">
            <span className="block text-slate-400 font-medium mb-2">
              Lưu ý xử lý
            </span>
            <ul className="space-y-1.5 text-slate-600">
              <li className="flex items-center gap-1.5">
                <span className="size-1 rounded-full bg-slate-400 shrink-0" />
                <span>Thành viên đúng phân công</span>
              </li>
              <li className="flex items-center gap-1.5">
                <span className="size-1 rounded-full bg-slate-400 shrink-0" />
                <span>Không có GVHD chấm đề tài mình</span>
              </li>
              <li className="flex items-center gap-1.5">
                <span className="size-1 rounded-full bg-slate-400 shrink-0" />
                <span>Đính kèm quyết định phê duyệt</span>
              </li>
            </ul>
          </div>
        </div>
      )}
    </Card>
  );
}