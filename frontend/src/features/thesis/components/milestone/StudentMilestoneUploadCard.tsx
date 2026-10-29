import { useRef } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { FileText, UploadCloud, RefreshCw, Trash2, Lock, GitBranch } from 'lucide-react';
import { cn } from '@/lib/utils';

interface StudentMilestoneUploadCardProps {
  versionNo: number;
  selectedFile: File | null;
  gitUrl: string;
  studentNote: string;
  onFileSelect: (file: File | null) => void;
  onGitUrlChange: (url: string) => void;
  onNoteChange: (note: string) => void;
  disabled?: boolean;
  disabledReason?: string | null;
}

export function StudentMilestoneUploadCard({
  versionNo,
  selectedFile,
  gitUrl,
  studentNote,
  onFileSelect,
  onGitUrlChange,
  onNoteChange,
  disabled = false,
  disabledReason,
}: StudentMilestoneUploadCardProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      onFileSelect(e.target.files[0]);
    }
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    if (disabled) return;
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      onFileSelect(e.dataTransfer.files[0]);
    }
  };

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-5">
      <div className="flex items-center justify-between">
        <h3 className="text-base font-semibold text-slate-900">
          Sản phẩm v{versionNo}
        </h3>
      </div>

      <input
        ref={fileInputRef}
        type="file"
        accept=".pdf,.doc,.docx"
        className="hidden"
        onChange={handleFileChange}
        disabled={disabled}
      />

      <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
        <div className="space-y-2">
          <div className="flex items-center justify-between text-xs text-slate-500">
            <span className="font-medium text-slate-700">Tệp báo cáo</span>
            <span>Tối đa 20 MB</span>
          </div>

          {selectedFile ? (
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/70 space-y-3">
              <div className="flex items-start gap-3">
                <div className="p-2.5 rounded-lg bg-blue-50 text-primary shrink-0 border border-blue-100">
                  <FileText className="size-6" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-slate-900 truncate">
                    {selectedFile.name}
                  </p>
                  <div className="flex items-center gap-2 mt-0.5">
                    <span className="text-xs text-slate-500">
                      {formatFileSize(selectedFile.size)}
                    </span>
                    <span className="text-slate-300">•</span>
                    <span className="text-xs font-medium text-amber-600">
                      Tệp đã chọn - Chưa gửi
                    </span>
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2 pt-1 border-t border-slate-200/60">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-8 text-xs font-medium text-slate-700 bg-white"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={disabled}
                >
                  <RefreshCw className="size-3.5 mr-1.5" />
                  Thay tệp
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="h-8 text-xs font-medium text-rose-600 hover:text-rose-700 hover:bg-rose-50"
                  onClick={() => {
                    onFileSelect(null);
                    if (fileInputRef.current) fileInputRef.current.value = '';
                  }}
                  disabled={disabled}
                >
                  <Trash2 className="size-3.5 mr-1.5" />
                  Xóa tệp
                </Button>
              </div>
            </div>
          ) : (
            <div
              onDrop={handleDrop}
              onDragOver={handleDragOver}
              onClick={() => !disabled && fileInputRef.current?.click()}
              className={cn(
                'border-2 border-dashed border-slate-200 rounded-xl p-6 text-center cursor-pointer transition-colors',
                'hover:border-primary/50 hover:bg-slate-50/50',
                disabled && 'opacity-70 cursor-not-allowed hover:border-slate-200 hover:bg-slate-50/30'
              )}
            >
              <div className="flex flex-col items-center justify-center gap-1.5">
                <div
                  className={cn(
                    'p-2.5 rounded-full',
                    disabled ? 'bg-slate-100 text-slate-400' : 'bg-slate-100 text-slate-500'
                  )}
                >
                  {disabled ? (
                    <Lock className="size-5 text-slate-400" />
                  ) : (
                    <UploadCloud className="size-5 text-slate-600" />
                  )}
                </div>
                <p className="text-xs font-medium text-slate-800">
                  {disabled ? (
                    'Khu vực nộp báo cáo tạm khóa'
                  ) : (
                    <>
                      Kéo thả tệp hoặc{' '}
                      <span className="text-primary hover:underline">chọn tệp</span>
                    </>
                  )}
                </p>
                <p className="text-[11px] text-slate-400">
                  {disabled && disabledReason
                    ? disabledReason
                    : 'PDF, DOC, DOCX tối đa 20 MB'}
                </p>
              </div>
            </div>
          )}
        </div>

        <div className="space-y-2">
          <label htmlFor="git-url" className="text-xs font-medium text-slate-700 block">
            Mã nguồn / sản phẩm
          </label>
          <div className="relative">
            <Input
              id="git-url"
              placeholder="https://github.com/..."
              value={gitUrl}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => onGitUrlChange(e.target.value)}
              disabled={disabled}
              className="h-10 text-sm bg-white pl-9"
            />
            <GitBranch className="size-4 text-slate-400 absolute left-3 top-3" />
          </div>
          <p className="text-[11px] text-slate-400">
            Đường dẫn kho lưu trữ mã nguồn GitHub, GitLab hoặc liên kết demo
          </p>
        </div>
      </div>

      <div className="space-y-1.5 pt-1">
        <label htmlFor="student-note" className="text-xs font-medium text-slate-700 block">
          Ghi chú
        </label>
        <Input
          id="student-note"
          placeholder="Mô tả tóm tắt kết quả hoặc nội dung cập nhật của phiên bản này..."
          value={studentNote}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) => onNoteChange(e.target.value)}
          disabled={disabled}
          className="h-10 text-sm bg-white"
        />
      </div>
    </Card>
  );
}