import { RichTextEditor } from '@/components/ui/rich-text-editor';
import {
  SubmissionBlockComposer,
  SubmissionDropzoneCard,
  type ProductLinkDraft,
} from '../shared';

interface StudentMilestoneUploadCardProps {
  versionNo: number;
  selectedFile: File | null;
  productLinks: ProductLinkDraft[];
  selectedImages: File[];
  studentNote: string;
  uploadProgress?: number | null;
  onFileSelect: (file: File | null) => void;
  onProductLinksChange: (links: ProductLinkDraft[]) => void;
  onImagesChange: (files: File[]) => void;
  onNoteChange: (note: string) => void;
  onSubmit: () => void;
  isSubmitting?: boolean;
  canSubmit?: boolean;
  disabled?: boolean;
  lockedTitle?: string;
  disabledReason?: string | null;
}

export function StudentMilestoneUploadCard({
  versionNo,
  selectedFile,
  productLinks,
  selectedImages,
  studentNote,
  uploadProgress = null,
  onFileSelect,
  onProductLinksChange,
  onImagesChange,
  onNoteChange,
  onSubmit,
  isSubmitting = false,
  canSubmit = true,
  disabled = false,
  lockedTitle = 'Khu vực nộp sản phẩm tạm khóa',
  disabledReason,
}: StudentMilestoneUploadCardProps) {
  return (
    <SubmissionDropzoneCard
      title={`Nộp sản phẩm phiên bản v${versionNo}`}
      subtitle="1 tệp báo cáo chính (PDF, DOC, DOCX · ≤ 20 MB), tối đa 5 liên kết và 5 ảnh minh chứng"
      selectedFile={selectedFile}
      onFileSelect={onFileSelect}
      disabled={disabled}
      lockedTitle={lockedTitle}
      lockedDescription={disabledReason}
      compactWhenLocked
      submitLabel={`Nộp sản phẩm v${versionNo}`}
      submitLoadingLabel="Đang nộp sản phẩm…"
      canSubmit={canSubmit}
      isSubmitting={isSubmitting}
      uploadProgress={uploadProgress}
      onSubmit={onSubmit}
      extraFields={
        <div className="divide-y divide-slate-200/75 [&>*]:py-5 [&>*:first-child]:pt-0 [&>*:last-child]:pb-0">
          <SubmissionBlockComposer
            productLinks={productLinks}
            onProductLinksChange={onProductLinksChange}
            images={selectedImages}
            onImagesChange={onImagesChange}
            linksRequired
            disabled={disabled}
          />

          <div className="space-y-2">
            <div>
              <label
                id="student-note-label"
                htmlFor="student-note"
                className="text-xs font-semibold text-slate-800 block"
              >
                Ghi chú phiên bản (tuỳ chọn)
              </label>
              <p className="text-[11px] text-slate-500 mt-0.5">
                Hiển thị trực tiếp để GVHD nắm bối cảnh khi đánh giá.
              </p>
            </div>
            <RichTextEditor
              id="student-note"
              value={studentNote}
              onChange={onNoteChange}
              disabled={disabled}
              ariaLabelledBy="student-note-label"
              placeholder="Tóm tắt đầu mục đã hoàn thành hoặc điểm đã chỉnh sửa..."
              minHeightClassName="min-h-[112px]"
              expandable
              expandedTitle="Ghi chú phiên bản"
              expandedSubtitle="Nội dung này sẽ hiển thị trực tiếp cho GVHD khi đánh giá sản phẩm."
            />
          </div>
        </div>
      }
    />
  );
}
