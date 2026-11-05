import React, { useRef } from 'react';
import { FileText, Upload, RefreshCw, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

interface FinalReportUploadCardProps {
  selectedFile: File | null;
  productNote: string;
  onProductNoteChange: (val: string) => void;
  onFileSelect: (file: File) => void;
  onRemoveFile: () => void;
  disabled?: boolean;
  latestFileName?: string;
}

export const FinalReportUploadCard: React.FC<FinalReportUploadCardProps> = ({
  selectedFile,
  productNote,
  onProductNoteChange,
  onFileSelect,
  onRemoveFile,
  disabled = false,
  latestFileName,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleInputChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      onFileSelect(file);
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const displayName = selectedFile?.name || latestFileName || 'Chưa chọn tệp báo cáo';
  const displayStatus = selectedFile
    ? 'Tệp đã chọn · Chưa gửi'
    : latestFileName
      ? 'Tệp phiên bản hiện tại'
      : 'Vui lòng chọn tệp báo cáo để nộp';

  return (
    <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs space-y-6">
      <h2 className="text-base font-bold text-slate-800">Báo cáo toàn văn</h2>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold text-slate-700 uppercase tracking-wider">
              Tệp báo cáo
            </span>
            <span className="text-xs text-slate-400 font-medium">PDF / DOCX ≤20 MB</span>
          </div>

          <input
            type="file"
            ref={fileInputRef}
            onChange={handleInputChange}
            accept=".pdf,.doc,.docx"
            className="hidden"
            disabled={disabled}
          />

          <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/70 hover:bg-slate-50 transition-colors space-y-4">
            <div className="flex items-start gap-3">
              <div className="p-2.5 rounded-lg bg-blue-50 text-blue-600 shrink-0 mt-0.5">
                <FileText className="size-5" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-slate-800 truncate" title={displayName}>
                  {displayName}
                </p>
                <p className="text-xs text-slate-500 mt-0.5">{displayStatus}</p>
              </div>
            </div>

            <div className="flex items-center gap-2 pt-1">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => fileInputRef.current?.click()}
                disabled={disabled}
                className="h-8 text-xs font-semibold bg-white hover:bg-slate-50 border-slate-200 text-slate-700"
              >
                {selectedFile || latestFileName ? (
                  <>
                    <RefreshCw className="size-3.5 mr-1.5" />
                    Thay tệp
                  </>
                ) : (
                  <>
                    <Upload className="size-3.5 mr-1.5" />
                    Chọn tệp
                  </>
                )}
              </Button>

              {selectedFile && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={onRemoveFile}
                  disabled={disabled}
                  className="h-8 text-xs font-semibold text-rose-600 hover:text-rose-700 hover:bg-rose-50"
                >
                  <Trash2 className="size-3.5 mr-1.5" />
                  Xóa tệp
                </Button>
              )}
            </div>
          </div>
        </div>

        <div className="space-y-2">
          <label
            htmlFor="product-note-input"
            className="block text-xs font-bold text-slate-700 uppercase tracking-wider"
          >
            Sản phẩm
          </label>
          <div className="space-y-1.5">
            <Input
              id="product-note-input"
              value={productNote}
              onChange={(e) => onProductNoteChange(e.target.value)}
              placeholder="Kho mã nguồn và kết quả thực nghiệm..."
              disabled={disabled}
              className="h-10 text-sm bg-white"
            />
            <p className="text-xs text-slate-400">
              Liên kết kho mã nguồn, bài báo hoặc minh chứng sản phẩm đính kèm đề tài.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};