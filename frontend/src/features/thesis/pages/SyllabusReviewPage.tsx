import { ReviewGatePageShell } from '../components/review';
import { useSyllabusReview } from '../hooks/useSyllabusReview';

export function SyllabusReviewPage() {
  const workflow = useSyllabusReview();

  return (
    <ReviewGatePageShell
      title="Thẩm định quy trình"
      description="Kiểm tra tệp đề cương chi tiết, lịch sử phiên bản và thẩm định hồ sơ Cổng 1 của nhóm sinh viên."
      workflow={workflow}
    />
  );
}
