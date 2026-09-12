export interface ModuleConfig {
  key: string;
  path: string;
  label: string;
}

export const modules: ModuleConfig[] = [
  { key: 'Identity', path: 'identity', label: 'Tài khoản & Phân quyền' },
  { key: 'AcademicPeriods', path: 'academic-periods', label: 'Đợt KLTN' },
  { key: 'TopicCatalog', path: 'topic-catalog', label: 'Danh mục đề tài' },
  { key: 'ThesisManagement', path: 'thesis-management', label: 'Đăng ký & Đề cương' },
  { key: 'MidtermProgress', path: 'midterm-progress', label: 'Tiến độ giữa kỳ' },
  { key: 'FinalReportEvaluation', path: 'final-report-evaluation', label: 'Báo cáo cuối kỳ' },
  { key: 'DefenseCouncil', path: 'defense-council', label: 'Hội đồng bảo vệ' },
  { key: 'GradingConsolidation', path: 'grading-consolidation', label: 'Tổng hợp điểm' },
  { key: 'Notifications', path: 'notifications', label: 'Thông báo' },
];
