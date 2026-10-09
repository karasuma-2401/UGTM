export interface ModuleConfig {
  key: string;
  path: string;
  label: string;
}

export const modules: ModuleConfig[] = [
  { key: 'Identity', path: 'identity', label: 'Tài khoản & Phân quyền' },
  { key: 'AcademicPeriods', path: 'academic-periods', label: 'Đợt KLTN' },
  { key: 'TopicCatalog', path: 'topic-catalog', label: 'Danh mục đề tài' },
  { key: 'ThesisManagement', path: 'thesis-management', label: 'Hồ sơ & Tiến độ KLTN' },
  { key: 'DefenseCouncil', path: 'defense-council', label: 'Đánh giá, Hội đồng & Kết quả' },
  { key: 'Notifications', path: 'notifications', label: 'Thông báo' },
];
