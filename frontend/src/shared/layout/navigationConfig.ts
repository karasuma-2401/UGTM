import type { ComponentType } from 'react';
import {
  LayoutDashboard,
  ShieldCheck,
  SlidersHorizontal,
  ScrollText,
  ClipboardCheck,
  BookOpen,
  CalendarRange,
  UsersRound,
  FileText,
} from 'lucide-react';

export interface NavItemConfig {
  label: string;
  path: string;
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
    id: 'admin',
    title: 'Quản trị hệ thống',
    roles: ['Admin'],
    items: [
      {
        label: 'Tổng quan',
        path: '/',
        icon: LayoutDashboard,
      },
      {
        label: 'Tài khoản & Phân quyền',
        path: '/modules/identity',
        icon: ShieldCheck,
      },
      {
        label: 'Cấu hình điểm',
        path: '/admin/grade-config',
        icon: SlidersHorizontal,
        roles: ['Admin'],
      },
      {
        label: 'Nhật ký hoạt động',
        path: '/admin/audit-logs',
        icon: ScrollText,
        roles: ['Admin'],
      },
    ],
  },
  {
    id: 'academic',
    title: 'Khóa luận',
    items: [
      {
        label: 'Kế hoạch đợt',
        path: '/academic/periods',
        icon: CalendarRange,
      },
      {
        label: 'Danh sách SV / GV',
        path: '/academic/roster',
        icon: UsersRound,
      },
      {
        label: 'Đề tài hướng dẫn',
        path: '/topics/advising',
        icon: BookOpen,
        roles: ['Lecturer', 'Admin'],
      },
      {
        label: 'Đề tài & đăng ký',
        path: '/topics/catalog',
        icon: BookOpen,
        roles: ['Student', 'Admin'],
      },
      {
        label: 'Báo cáo & tiến độ',
        path: '/thesis/proposal',
        icon: FileText,
        roles: ['Student', 'Admin'],
      },
      {
        label: 'Thẩm định đề tài',
        path: '/topics/review',
        icon: ClipboardCheck,
        roles: ['Faculty', 'Admin'],
      },
      {
        label: 'Thẩm định đề cương',
        path: '/thesis/review/syllabus',
        icon: ClipboardCheck,
        roles: ['Faculty', 'Admin', 'AcademicAffairs'],
      },
    ],
  },
];