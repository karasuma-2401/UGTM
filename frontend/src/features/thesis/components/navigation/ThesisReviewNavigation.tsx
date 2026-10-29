import { NavLink } from 'react-router-dom';
import { FileText, ClipboardCheck } from 'lucide-react';
import { cn } from '@/lib/utils';

export function ThesisReviewNavigation() {
  const tabs = [
    {
      path: '/thesis/review/syllabus',
      label: 'Cổng 1 · Thẩm định đề cương',
      icon: FileText,
    },
    {
      path: '/thesis/review/midterm',
      label: 'Cổng 2 · Thẩm định báo cáo giữa kỳ',
      icon: ClipboardCheck,
    },
  ];

  return (
    <nav
      aria-label="Thẩm định các cổng khóa luận"
      className="flex gap-1 overflow-x-auto border-b border-slate-200"
    >
      {tabs.map(({ path, label, icon: Icon }) => (
        <NavLink
          key={path}
          to={path}
          className={({ isActive }) =>
            cn(
              'inline-flex h-12 shrink-0 items-center justify-center gap-2 border-b-2 px-4 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring',
              isActive
                ? 'border-primary bg-primary/5 text-primary hover:bg-primary/5 hover:text-primary'
                : 'border-transparent text-slate-500 hover:bg-slate-100/60 hover:text-slate-800'
            )
          }
        >
          <Icon className="size-4" aria-hidden="true" />
          {label}
        </NavLink>
      ))}
    </nav>
  );
}