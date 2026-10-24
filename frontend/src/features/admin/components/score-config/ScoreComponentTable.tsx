import React from 'react';
import { Trash2, ShieldAlert } from 'lucide-react';
import type {
  ScoreComponentInput,
  ScoreCalculationMethod,
} from '../../types/scoreConfig.types';
import { Badge } from '@/components/ui/badge';

export interface ScoreComponentTableProps {
  components: ScoreComponentInput[];
  calculationMethod: ScoreCalculationMethod;
  onChangeComponents: (components: ScoreComponentInput[]) => void;
  isReadOnly?: boolean;
}

export const ScoreComponentTable: React.FC<ScoreComponentTableProps> = ({
  components,
  calculationMethod,
  onChangeComponents,
  isReadOnly = false,
}) => {
  const totalWeight = components.reduce(
    (sum, c) => sum + (Number(c.weight) > 0 ? Number(c.weight) : 0),
    0,
  );

  const handleWeightChange = (index: number, newWeightStr: string) => {
    const val = parseFloat(newWeightStr);
    const updated = components.map((c, i) => {
      if (i !== index) return c;
      return {
        ...c,
        weight: isNaN(val) ? 0 : val,
      };
    });
    onChangeComponents(updated);
  };

  const handleRemoveComponent = (index: number) => {
    const updated = components.filter((_, i) => i !== index);
    const reindexed = updated.map((c, i) => ({
      ...c,
      orderIndex: i + 1,
    }));
    onChangeComponents(reindexed);
  };

  const getConditionText = (item: ScoreComponentInput): string => {
    const parts: string[] = [];
    if (item.minimumScore !== null) {
      parts.push(`Đạt sàn ≥ ${item.minimumScore}`);
    }
    if (item.requiresApproval) {
      parts.push('Cần duyệt');
    }
    if (item.applicability === 'ReviewerOutsideCouncil') {
      parts.push('GVPB kiêm UV không chấm');
    } else if (item.applicability === 'ReviewerInCouncil') {
      parts.push('GVPB trong HĐ');
    }

    if (parts.length > 0) {
      return parts.join(' + ');
    }
    return item.isRequired ? 'Bắt buộc khi áp dụng' : 'Tùy chọn';
  };

  const getCategoryBadge = (item: ScoreComponentInput) => {
    if (item.source === 'Advisor') {
      return (
        <Badge
          variant="info"
          className="whitespace-nowrap shrink-0 px-3 py-1 font-medium text-xs shadow-2xs"
        >
          Nguồn riêng
        </Badge>
      );
    }
    if (item.source === 'Reviewer') {
      return (
        <Badge
          variant="warning"
          className="whitespace-nowrap shrink-0 px-3 py-1 font-medium text-xs shadow-2xs"
        >
          Bắt buộc
        </Badge>
      );
    }
    if (item.source.startsWith('Council')) {
      return (
        <Badge
          variant="secondary"
          className="whitespace-nowrap shrink-0 px-3 py-1 font-medium text-xs shadow-2xs"
        >
          Điểm HĐ
        </Badge>
      );
    }
    return (
      <Badge
        variant="default"
        className="whitespace-nowrap shrink-0 px-3 py-1 font-medium text-xs shadow-2xs"
      >
        Tùy biến
      </Badge>
    );
  };

  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-xs overflow-hidden space-y-0">
      <div className="p-4 sm:p-5 border-b border-slate-100 flex items-center justify-between">
        <div>
          <h3 className="text-base font-bold text-slate-900 tracking-tight">
            Bộ hệ số mẫu
          </h3>
          <p className="text-xs text-slate-500 mt-0.5">
            Danh sách các thành phần điểm và điều kiện áp dụng cho từng nguồn chấm.
          </p>
        </div>
        <span className="text-xs font-medium text-slate-500">
          {components.length} mục trong danh sách
        </span>
      </div>

      <div className="overflow-x-auto min-h-[220px]">
        <table className="w-full text-left text-sm border-collapse min-w-[640px]">
          <thead>
            <tr className="border-b border-slate-100 bg-slate-50/60 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
              <th scope="col" className="px-5 py-3.5 w-[28%]">
                Nguồn / thành viên
              </th>
              <th scope="col" className="px-5 py-3.5 w-[28%]">
                Điểm / trọng số
              </th>
              <th scope="col" className="px-5 py-3.5 w-[24%]">
                Điều kiện
              </th>
              <th scope="col" className="px-5 py-3.5 w-[20%] text-right whitespace-nowrap">
                Phân loại
              </th>
            </tr>
          </thead>

          <tbody className="divide-y divide-slate-100">
            {components.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-5 py-12 text-center text-slate-400 text-sm">
                  Chưa có đầu điểm nào trong cấu hình. Bấm "Thêm đầu điểm" để bắt đầu.
                </td>
              </tr>
            ) : (
              components.map((item, index) => {
                const isCustom =
                  item.source === 'Custom' || item.componentCode.startsWith('CUSTOM');

                // Tính tỷ lệ phần trăm quy đổi động
                const percentEquiv =
                  totalWeight > 0
                    ? Math.round(((Number(item.weight) / totalWeight) * 100) * 100) / 100
                    : 0;

                return (
                  <tr
                    key={item.componentCode || index}
                    className="hover:bg-slate-50/50 transition-colors"
                  >
                    {/* Cột 1: Nguồn / thành viên */}
                    <td className="px-5 py-4">
                      <div className="font-semibold text-slate-800 text-sm">
                        {item.componentName}
                      </div>
                      <div className="text-[11px] text-slate-400 font-mono mt-0.5">
                        {item.componentCode}
                      </div>
                    </td>

                    {/* Cột 2: Điểm / Trọng số */}
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-2 flex-wrap">
                        {isReadOnly ? (
                          <div className="flex items-baseline gap-1.5">
                            <span className="font-semibold text-slate-800 text-sm">
                              {item.weight}
                            </span>
                            <span className="text-xs text-slate-500 font-medium">
                              {calculationMethod === 'Percentage' ? '%' : 'hệ số'}
                            </span>
                            {calculationMethod === 'IntegerCoefficient' && (
                              <span className="text-[11px] text-slate-400 font-medium">
                                (≈ {percentEquiv}%)
                              </span>
                            )}
                          </div>
                        ) : (
                          <div className="flex items-center gap-2">
                            <input
                              type="number"
                              step={calculationMethod === 'Percentage' ? '0.01' : '1'}
                              min="0"
                              max={calculationMethod === 'Percentage' ? '100' : '99'}
                              value={item.weight === 0 ? '' : item.weight}
                              onChange={(e) => handleWeightChange(index, e.target.value)}
                              placeholder="0"
                              className="w-20 px-2.5 py-1 text-sm font-semibold text-slate-800 border border-slate-200 rounded-lg text-center bg-white focus:outline-none focus:ring-1 focus:ring-primary shadow-2xs"
                            />
                            <span className="text-xs text-slate-500 font-medium whitespace-nowrap">
                              {calculationMethod === 'Percentage' ? '%' : 'hệ số'}
                            </span>
                            {calculationMethod === 'IntegerCoefficient' && (
                              <span className="text-[11px] text-slate-400 font-medium whitespace-nowrap">
                                (≈ {percentEquiv}%)
                              </span>
                            )}
                          </div>
                        )}
                      </div>
                    </td>

                    {/* Cột 3: Điều kiện */}
                    <td className="px-5 py-4 text-xs text-slate-600">
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <span>{getConditionText(item)}</span>
                        {item.minimumScore !== null && (
                          <span
                            title="Có áp dụng điểm sàn"
                            className="text-amber-500 inline-flex items-center"
                          >
                            <ShieldAlert className="size-3.5" />
                          </span>
                        )}
                      </div>
                    </td>

                    {/* Cột 4: Phân loại & thao tác (Tăng width và không để wrap chữ) */}
                    <td className="px-5 py-4 text-right whitespace-nowrap">
                      <div className="inline-flex items-center justify-end gap-2.5 shrink-0">
                        {getCategoryBadge(item)}

                        {isCustom && !isReadOnly && (
                          <button
                            type="button"
                            onClick={() => handleRemoveComponent(index)}
                            title="Xóa đầu điểm tùy biến"
                            className="p-1 rounded text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-colors focus:outline-none"
                          >
                            <Trash2 className="size-4" />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      <div className="p-3.5 sm:px-5 border-t border-slate-100 bg-slate-50/40 text-xs text-slate-500 flex items-center justify-between">
        <span>{components.length} mục trong danh sách</span>
        <span className="text-[11px] text-slate-400">
          {calculationMethod === 'IntegerCoefficient'
            ? 'Hệ số nguyên tự động quy đổi tỷ lệ phần trăm tương ứng'
            : 'Tổng tỷ lệ phần trăm của các đầu điểm cần bằng đúng 100%'}
        </span>
      </div>
    </div>
  );
};