import type { ReactNode } from 'react';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';
import { ThesisGroupDossierCard, type DossierMetaItem } from '../shared';

interface SyllabusTopicInfoCardProps {
  title?: string;
  groupDetail?: StudentGroupDetailDto | null;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
  topBanner?: ReactNode;
  metaItems?: DossierMetaItem[];
  rulesTitle?: string;
  rules?: string[];
}

export function SyllabusTopicInfoCard({
  title = 'Hồ sơ đề cương',
  groupDetail,
  isLoading = false,
  isError = false,
  onRetry,
  topBanner,
  metaItems,
  rulesTitle = 'Quy định thẩm định Cổng 1',
  rules,
}: SyllabusTopicInfoCardProps) {
  const defaultMetaItems: DossierMetaItem[] = metaItems ?? [
    {
      label: 'Đơn vị thẩm định',
      value: 'Văn phòng Khoa: Đạt / Yêu cầu chỉnh sửa / Không đạt. GVHD được xem đề cương của nhóm.',
    },
  ];

  const defaultRules = rules ?? [
    'Đúng tên đề tài và phạm vi đã đăng ký.',
    'Trình bày rõ mục tiêu, phạm vi và kế hoạch thực hiện theo mốc.',
    'Định dạng PDF, DOC hoặc DOCX dưới 20 MB.',
  ];

  return (
    <ThesisGroupDossierCard
      title={title}
      groupDetail={groupDetail}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      topBanner={topBanner}
      metaItems={defaultMetaItems}
      rulesTitle={rulesTitle}
      rules={defaultRules}
    />
  );
}
