import React, { useRef } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { FileText, UploadCloud, RefreshCw, Trash2 } from 'lucide-react';
import { cn } from '@/lib/utils';

interface SyllabusUploadCardProps {
  selectedFile: File | null;
  onFileSelect: (file: File | null) => void;
  disabled?: boolean;
}

export function SyllabusUploadCard({
  selectedFile,
  onFileSelect,
  disabled = false,
}: SyllabusUploadCardProps) {
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
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-base font-semibold text-slate-900">Tệp đề cương</h3>
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between text-xs text-slate-500">
          <span className="font-medium text-slate-700">Tải lên</span>
          <span>PDF / DOC / DOCX - tối đa 20 MB</span>
        </div>

        <input
          ref={fileInputRef}
          type="file"
          accept=".pdf,.doc,.docx"
          className="hidden"
          onChange={handleFileChange}
          disabled={disabled}
        />

        {selectedFile ? (
          <div className="p-4 rounded-lg border border-slate-200 bg-slate-50/70 space-y-3">
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
              'border-2 border-dashed border-slate-200 rounded-xl p-8 text-center cursor-pointer transition-colors',
              'hover:border-primary/50 hover:bg-slate-50/50',
              disabled && 'opacity-60 cursor-not-allowed hover:border-slate-200 hover:bg-transparent',
            )}
          >
            <div className="flex flex-col items-center justify-center gap-2">
              <div className="p-3 rounded-full bg-slate-100 text-slate-500">
                <UploadCloud className="size-6 text-slate-600" />
              </div>
              <p className="text-sm font-medium text-slate-800">
                Kéo thả tệp đề cương vào đây, hoặc{' '}
                <span className="text-primary hover:underline">chọn từ máy tính</span>
              </p>
              <p className="text-xs text-slate-400">
                Chấp nhận tệp .pdf, .doc, .docx (Dung lượng không quá 20 MB)
              </p>
            </div>
          </div>
        )}
      </div>
    </Card>
  );
}