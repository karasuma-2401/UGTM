import type { ComponentType } from 'react';
import {
  LayoutDashboard,
  ShieldCheck,
  SlidersHorizontal,
  ScrollText,
  ClipboardCheck,
  BookOpen,
  CalendarRange,
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
        label: 'Đề tài hướng dẫn',
        path: '/topics/advising',
        icon: BookOpen,
        roles: ['Lecturer', 'Admin'],
      },
      {
        label: 'Thẩm định đề tài',
        path: '/topics/review',
        icon: ClipboardCheck,
        roles: ['Faculty', 'Admin'],
      },
    ],
  },
];
