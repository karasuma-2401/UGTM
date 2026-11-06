import { useRef } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { FileText, UploadCloud, CheckCircle2 } from 'lucide-react';
import type { CouncilDraftItemDto } from '../../types/councilApproval.types';

interface CouncilApprovalDecisionCardProps {
  selectedCouncil: CouncilDraftItemDto | null;
  selectedFile: File | null;
  onFileSelect: (file: File | null) => void;
  feedback: string;
  onFeedbackChange: (val: string) => void;
  decisionNumber: string;
  onDecisionNumberChange: (val: string) => void;
  onApprove: () => void;
  isApproving: boolean;
  canApprove: boolean;
}

export function CouncilApprovalDecisionCard({
  selectedCouncil,
  selectedFile,
  onFileSelect,
  feedback,
  onFeedbackChange,
  decisionNumber,
  onDecisionNumberChange,
  onApprove,
  isApproving,
  canApprove,
}: CouncilApprovalDecisionCardProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);

  const isAlreadyApproved = selectedCouncil?.status === 'Active';
  const displayFileName =
    selectedFile?.name || selectedCouncil?.decisionFileUrl || null;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (file.type !== 'application/pdf') {
        alert('Vui lòng chỉ tải lên tệp định dạng PDF.');
        return;
      }
      if (file.size > 20 * 1024 * 1024) {
        alert('Kích thước tệp vượt quá 20MB.');
        return;
      }
      onFileSelect(file);
    }
  };

  const handleClearFile = () => {
    onFileSelect(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const canSubmit =
    Boolean(selectedCouncil) &&
    !isAlreadyApproved &&
    canApprove &&
    !isApproving;

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-6">
      <h2 className="text-base font-bold text-slate-900">
        Quyết định & nhận xét
      </h2>

      {!selectedCouncil ? (
        <div className="py-8 text-center text-xs text-slate-400">
          Vui lòng chọn một dự thảo Hội đồng từ danh sách để thẩm định.
        </div>
      ) : (
        <div className="space-y-5">
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <span className="text-xs font-semibold text-slate-700">
                Tệp quyết định
              </span>
              <span className="text-[11px] text-slate-400">PDF ≤20 MB</span>
            </div>

            <input
              ref={fileInputRef}
              type="file"
              accept="application/pdf"
              onChange={handleFileChange}
              className="hidden"
            />

            {displayFileName ? (
              <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl flex items-center justify-between gap-3">
                <div className="flex items-center gap-3 min-w-0">
                  <div className="size-10 rounded-lg bg-blue-100 text-blue-600 flex items-center justify-center shrink-0">
                    <FileText className="size-5" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-xs font-semibold text-slate-800 truncate">
                      {displayFileName}
                    </p>
                    <p className="text-[11px] text-slate-400">
                      {isAlreadyApproved
                        ? 'Đã ban hành quyết định'
                        : selectedFile
                          ? 'Tệp đã chọn · Chưa gửi'
                          : 'Tệp mẫu dự thảo'}
                    </p>
                  </div>
                </div>

                {!isAlreadyApproved && (
                  <div className="flex items-center gap-2 shrink-0">
                    <button
                      type="button"
                      onClick={() => fileInputRef.current?.click()}
                      className="px-2.5 py-1 text-xs font-medium text-slate-700 bg-white border border-slate-200 rounded-md hover:bg-slate-50 cursor-pointer"
                    >
                      Thay tệp
                    </button>
                    <button
                      type="button"
                      onClick={handleClearFile}
                      className="px-2.5 py-1 text-xs font-medium text-rose-600 bg-white border border-rose-200 rounded-md hover:bg-rose-50 cursor-pointer"
                    >
                      Xóa tệp
                    </button>
                  </div>
                )}
              </div>
            ) : (
              <button
                type="button"
                onClick={() => fileInputRef.current?.click()}
                disabled={isAlreadyApproved}
                className="w-full p-6 border-2 border-dashed border-slate-200 rounded-xl flex flex-col items-center justify-center gap-2 text-slate-500 hover:border-blue-400 hover:text-blue-600 transition-colors cursor-pointer bg-slate-50/50"
              >
                <UploadCloud className="size-6 text-slate-400" />
                <span className="text-xs font-medium">
                  Nhấp để tải lên tệp quyết định PDF (tối đa 20MB)
                </span>
              </button>
            )}
          </div>

          <div>
            <label
              htmlFor="decision-number-input"
              className="block text-xs font-semibold text-slate-700 mb-1.5"
            >
              Số quyết định
            </label>
            <input
              id="decision-number-input"
              type="text"
              value={decisionNumber}
              onChange={(e) => onDecisionNumberChange(e.target.value)}
              disabled={isAlreadyApproved}
              placeholder="VD: QD-HD01 hoặc 123/QĐ-ĐHCNTT"
              className="w-full h-10 px-3 py-2 bg-slate-50/50 border border-slate-200 rounded-lg text-xs text-slate-800 focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:bg-white disabled:bg-slate-100 disabled:cursor-not-allowed"
            />
          </div>

          <div>
            <label
              htmlFor="feedback-textarea"
              className="block text-xs font-semibold text-slate-700 mb-1.5"
            >
              Nhận xét
            </label>
            <textarea
              id="feedback-textarea"
              rows={3}
              value={feedback}
              onChange={(e) => onFeedbackChange(e.target.value)}
              disabled={isAlreadyApproved}
              placeholder="Nhập nhận xét hoặc lưu ý cho Khoa..."
              className="w-full p-3 bg-slate-50/50 border border-slate-200 rounded-lg text-xs text-slate-800 focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:bg-white resize-none disabled:bg-slate-100 disabled:cursor-not-allowed"
            />
          </div>

          {isAlreadyApproved ? (
            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-lg flex items-center gap-2 text-xs text-emerald-800 font-medium">
              <CheckCircle2 className="size-4 text-emerald-600 shrink-0" />
              <span>Hội đồng này đã được phê duyệt quyết định thành lập.</span>
            </div>
          ) : (
            <div className="flex items-center gap-3 pt-2">
              <Button
                type="button"
                onClick={onApprove}
                disabled={!canSubmit}
                isLoading={isApproving}
                className="px-6 h-10 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg text-xs transition-colors shadow-xs"
              >
                Phê duyệt
              </Button>
            </div>
          )}
        </div>
      )}
    </Card>
  );
}
