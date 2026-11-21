import type { ComponentType } from 'react';
import {
  LayoutDashboard,
  ShieldCheck,
  SlidersHorizontal,
  ScrollText,
  ClipboardCheck,
  BookOpen,
  BookMarked,
  CalendarRange,
  UsersRound,
  Presentation,
  FileText,
  FileSearch,
  CalendarCheck,
  UserCheck,
  UserCog,
  FilePenLine,
  BadgeCheck,
  ListChecks,
  BarChart3,
  Award,
  Archive,
  PackageCheck,
  FolderCheck,
  FileCheck2,
  Gavel,
  Stamp,
  SquarePen,
} from 'lucide-react';

export interface NavItemConfig {
  label: string;
  path: string;
  activePaths?: string[];
  icon: ComponentType<{ className?: string }>;
  badge?: string;
  permission?: string | string[];
  permissions?: string | string[];
  role?: string | string[];
  roles?: string | string[];
  requireAllPermissions?: boolean;
}

export interface NavSectionConfig {
  id: string;
  title?: string;
  roles?: string | string[];
  permissions?: string | string[];
  items: NavItemConfig[];
}

export const navigationSections: NavSectionConfig[] = [
  {
    id: 'overview',
    roles: ['Admin'],
    items: [
      {
        label: 'Tổng quan',
        path: '/dashboard',
        icon: LayoutDashboard,
      },
    ],
  },
  {
    id: 'planning',
    title: 'Tổ chức và kế hoạch',
    items: [
      {
        label: 'Kế hoạch đợt',
        path: '/academic/periods',
        icon: CalendarRange,
        roles: ['Admin', 'AcademicAffairs'],
      },

      {
        label: 'Cấu hình điểm',
        path: '/admin/grade-config',
        icon: SlidersHorizontal,
        roles: ['Admin', 'AcademicAffairs'],
      },
      {
        label: 'Sinh viên và Giảng viên',
        path: '/academic/roster',
        icon: UsersRound,
        roles: ['Admin', 'AcademicAffairs'],
      },
    ],
  },
  {
    id: 'topics',
    title: 'Đề tài và hướng dẫn',
    items: [
      {
        label: 'Đề tài hướng dẫn',
        path: '/topics/advising',
        icon: BookOpen,
        roles: ['Lecturer', 'Admin'],
      },
      {
        label: 'Đề tài và đăng ký',
        path: '/topics/catalog',
        icon: BookMarked,
        roles: ['Student', 'Admin'],
      },
      {
        label: 'Xin đổi tên đề tài',
        path: '/topics/rename',
        icon: FilePenLine,
        roles: ['Student'],
      },
      {
        label: 'Thẩm định đề tài',
        path: '/topics/review',
        icon: ClipboardCheck,
        roles: ['Faculty', 'Admin'],
      },
      {
        label: 'Duyệt đổi tên đề tài',
        path: '/topics/rename/review',
        icon: BadgeCheck,
        roles: ['Lecturer', 'Faculty', 'AcademicAffairs', 'Admin'],
      },
    ],
  },
  {
    id: 'progress',
    title: 'Tiến độ và đánh giá',
    items: [
      {
        label: 'Tiến độ sinh viên',
        path: '/advisor/milestones',
        icon: CalendarCheck,
        roles: ['Lecturer', 'Admin'],
      },
      {
        label: 'Chấm điểm khóa luận',
        path: '/thesis/lecturer-grading',
        icon: SquarePen,
        roles: ['Lecturer', 'Admin'],
      },
      {
        label: 'Xác nhận lưu chiểu',
        path: '/advisor/archival',
        icon: PackageCheck,
        roles: ['Lecturer', 'Admin'],
      },
      {
        label: 'Báo cáo và tiến độ',
        path: '/thesis/milestones',
        activePaths: ['/thesis/proposal', '/thesis/midterm', '/thesis/final-report'],
        icon: FileText,
        roles: ['Student', 'Admin'],
      },
      {
        label: 'Thẩm định quy trình',
        path: '/thesis/review/syllabus',
        icon: ListChecks,
        roles: ['Faculty', 'Admin', 'AcademicAffairs'],
      },
      {
        label: 'Phân công giảng viên',
        path: '/councils/reviewer-assignments',
        icon: UserCheck,
        roles: ['Faculty', 'Admin', 'AcademicAffairs'],
      },
      {
        label: 'Báo cáo tương đồng',
        path: '/councils/similarity-reports',
        icon: FileSearch,
        roles: ['CouncilMember'],
      },
      {
        label: 'Chấm điểm Hội đồng',
        path: '/councils/grading',
        icon: Gavel,
        roles: ['CouncilMember', 'Admin'],
      },
      {
        label: 'Biên bản hội đồng',
        path: '/councils/minutes',
        icon: FileCheck2,
        roles: ['CouncilMember', 'Lecturer', 'Faculty', 'AcademicAffairs', 'Admin'],
      },
      {
        label: 'Lập hội đồng bảo vệ',
        path: '/councils/members',
        icon: Presentation,
        roles: ['Faculty', 'Admin'],
      },
      {
        label: 'Phê duyệt hội đồng',
        path: '/councils/approval',
        icon: Stamp,
        roles: ['AcademicAffairs', 'Admin'],
      },
      {
        label: 'Tổng hợp kết quả',
        path: '/councils/results',
        icon: BarChart3,
        roles: ['Faculty', 'AcademicAffairs', 'Admin'],
      },
      {
        label: 'Hồ sơ lưu chiểu',
        path: '/thesis/archival',
        icon: Archive,
        roles: ['Student'],
      },
      {
        label: 'Duyệt hồ sơ lưu chiểu',
        path: '/thesis/review/archival',
        icon: FolderCheck,
        roles: ['Faculty', 'Admin'],
      },
      {
        label: 'Kết quả',
        path: '/councils/results',
        icon: Award,
        roles: ['Student'],
      },
    ],
  },
  {
    id: 'admin',
    title: 'Hệ thống',
    roles: ['Admin', 'AcademicAffairs'],
    items: [
      {
        label: 'Tài khoản',
        path: '/modules/identity/students',
        activePaths: ['/modules/identity/staff'],
        icon: UserCog,
        permission: 'identity.users.read',
      },
      {
        label: 'Phân quyền',
        path: '/modules/identity/permissions',
        icon: ShieldCheck,
        permissions: ['identity.roles.manage', 'identity.permissions.read'],
        requireAllPermissions: true,
      },
      {
        label: 'Nhật ký hoạt động',
        path: '/admin/audit-logs',
        icon: ScrollText,
        roles: ['Admin'],
      },
    ],
  },
];
