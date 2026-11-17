import type { SystemRoleInfo } from '../types/rolePermission.types';

export const SYSTEM_ROLES: SystemRoleInfo[] = [
  {
    id: 1,
    roleName: 'Admin',
    title: 'Quản trị viên',
    description: 'Toàn quyền cấu hình, quản trị người dùng và giám sát toàn bộ hoạt động hệ thống.',
    badge: 'Toàn quyền',
  },
  {
    id: 2,
    roleName: 'Faculty',
    title: 'Ban chủ nhiệm Khoa',
    description: 'Phê duyệt kế hoạch đợt KLTN, thẩm định đề tài và phân công giảng viên hướng dẫn.',
    badge: 'Quản lý',
  },
  {
    id: 3,
    roleName: 'AcademicAffairs',
    title: 'Phòng Đào tạo',
    description: 'Kiểm tra điều kiện sinh viên, quản lý danh sách đủ điều kiện và công nhận tốt nghiệp.',
    badge: 'Nghiệp vụ',
  },
  {
    id: 4,
    roleName: 'Lecturer',
    title: 'Giảng viên',
    description: 'Đề xuất đề tài, hướng dẫn sinh viên, đánh giá tiến độ và tham gia chấm phản biện.',
    badge: 'Chuyên môn',
  },
  {
    id: 5,
    roleName: 'Student',
    title: 'Sinh viên',
    description: 'Đăng ký đề tài, nộp báo cáo đề cương, cập nhật tiến độ và bảo vệ khóa luận tốt nghiệp.',
    badge: 'Học vụ',
  },
  {
    id: 6,
    roleName: 'CouncilMember',
    title: 'Thành viên Hội đồng',
    description: 'Đánh giá điểm phản biện, tham gia hội đồng chấm khóa luận và ghi nhận kết quả bảo vệ.',
    badge: 'Hội đồng',
  },
];

