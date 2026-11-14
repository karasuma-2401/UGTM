import type { StudentGroupDetailDto } from '../../types/syllabus.types';
import { ThesisGroupDossierCard } from '../shared';

interface FinalReportSummaryCardProps {
  groupDetail?: StudentGroupDetailDto | null;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
}

export function FinalReportSummaryCard({
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
      metaItems={[
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
      rulesTitle="Lưu ý nộp toàn văn"
      rules={[
        'Tài liệu toàn văn định dạng PDF, DOC hoặc DOCX (tối đa 20 MB).',
        'Hệ thống tự động kiểm tra tương đồng sau khi nộp phiên bản mới.',
        'Phiên bản mới nhất được giữ làm bản chính thức khi GVHD xác nhận chuyển phản biện.',
      ]}
    />
  );
}
