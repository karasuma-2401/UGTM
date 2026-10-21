import type { ComponentType } from 'react';
import {
  LayoutDashboard,
  ShieldCheck,
  SlidersHorizontal,
  ScrollText,
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

/**
 * Cấu hình Navigation phân nhóm theo RBAC:
 * - Section Quản trị hệ thống: Cố định cho Admin
 * - Section Nghiệp vụ khóa luận: Lọc động theo RBAC permissions
 */
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
      // Các module nghiệp vụ khi bật tính năng sẽ được khai báo kèm permission tương ứng ở đây
    ],
  },
];

