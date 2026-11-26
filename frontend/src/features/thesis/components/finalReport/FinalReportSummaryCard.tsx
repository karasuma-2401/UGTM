import type { StudentGroupDetailDto } from '../../types/syllabus.types';
import { ThesisGroupDossierCard } from '../shared';

interface FinalReportSummaryCardProps {
  postDefense?: boolean;
  groupDetail?: StudentGroupDetailDto | null;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
}

export function FinalReportSummaryCard({
  postDefense = false,
  groupDetail,
  isLoading = false,
  isError = false,
  onRetry,
}: FinalReportSummaryCardProps) {
  return (
    <ThesisGroupDossierCard
      title="Hồ sơ cuối kỳ"
      groupDetail={groupDetail}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      metaItems={postDefense ? [
        { label: 'Tiến độ hiện tại', value: 'Đã hoàn tất bảo vệ · Biên bản Hội đồng đã khóa.', accent: true },
        { label: 'Các bước tiếp theo', value: 'Nộp lưu chiểu → GVHD xác nhận → Khoa xác nhận → Theo dõi kết quả công bố.' },
      ] : [
        {
          label: 'Trình tự đánh giá',
          value: 'GVHD chấm và xác nhận chuyển phản biện → Khoa phân công GVPB → GVPB chấm và kết luận.',
        },
        {
          label: 'Thời gian thực hiện',
          value: 'Theo lịch phản biện · Tuần 11–18',
          accent: true,
        },
      ]}
      rulesTitle={postDefense ? 'Lưu ý sau bảo vệ' : 'Lưu ý nộp toàn văn'}
      rules={postDefense ? [
        'Hoàn thiện báo cáo theo yêu cầu của Hội đồng trước khi nộp lưu chiểu.',
        'Hồ sơ lưu chiểu nộp tại trang Lưu chiểu, gồm bản PDF và DOCX.',
        'Điểm chính thức được hiển thị khi kết quả khóa luận được công bố.',
      ] : [
        'Tài liệu toàn văn định dạng PDF, DOC hoặc DOCX (tối đa 20 MB).',
        'Hệ thống tự động kiểm tra tương đồng sau khi nộp phiên bản mới.',
        'Phiên bản mới nhất được giữ làm bản chính thức khi GVHD xác nhận chuyển phản biện.',
      ]}
    />
  );
}
