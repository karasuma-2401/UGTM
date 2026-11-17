import { ReviewGatePageShell } from '../components/review';
import { useMidtermReview } from '../hooks/useMidtermReview';

export function MidtermReviewPage() {
  const workflow = useMidtermReview();

  return (
    <ReviewGatePageShell
      title="Thẩm định quy trình"
      description="Kiểm tra tệp báo cáo giữa kỳ, đối chiếu tiến độ và thẩm định hồ sơ Cổng 2 của nhóm sinh viên."
      workflow={workflow}
    />
  );
}
