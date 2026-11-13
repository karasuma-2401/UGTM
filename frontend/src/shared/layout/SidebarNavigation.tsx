import { useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import type { NavSectionConfig } from './navigationConfig';

interface SidebarNavigationProps {
  sections: NavSectionConfig[];
  pathname: string;
  activePath?: string;
  collapsed: boolean;
  collapsible: boolean;
  userId?: string;
  onNavigate: () => void;
}

export function SidebarNavigation({
  sections,
  pathname,
  activePath,
  collapsed,
  collapsible,
  userId,
  onNavigate,
}: SidebarNavigationProps) {
  const storageKey = `ugtm:sidebar_groups:${userId ?? 'anonymous'}`;
  const activeSectionId = sections.find((section) =>
    section.items.some((item) => item.path === activePath),
  )?.id;
  const [groupState, setGroupState] = useState(() => {
    let saved: Record<string, boolean> = {};
    try {
      const value: unknown = JSON.parse(localStorage.getItem(storageKey) ?? '{}');
      if (value && typeof value === 'object' && !Array.isArray(value)) {
        saved = Object.fromEntries(
          Object.entries(value).filter(([, open]) => typeof open === 'boolean'),
        );
      }
    } catch {
      // Navigation still works when browser storage is blocked or invalid.
    }
    const initialGroup = activeSectionId ?? sections.find((section) => section.title)?.id;
    return { pathname, values: initialGroup ? { ...saved, [initialGroup]: true } : saved };
  });
  const openGroups =
    groupState.pathname !== pathname && activeSectionId
      ? { ...groupState.values, [activeSectionId]: true }
      : groupState.values;
  // Reveal the destination before painting, while still allowing manual collapse there.
  if (groupState.pathname !== pathname) {
    setGroupState({ pathname, values: openGroups });
  }

  useEffect(() => {
    if (!collapsible) return;
    try {
      localStorage.setItem(storageKey, JSON.stringify(groupState.values));
    } catch {
      // Persisting a display preference is optional; keep the current session usable.
    }
  }, [collapsible, groupState.values, storageKey]);

  return (
    <nav
      aria-label="Điều hướng chính"
      className={cn(
        'min-h-0 flex-1 space-y-3 overflow-y-auto overscroll-contain px-3 py-3',
        collapsed && 'lg:px-2',
      )}
    >
      {sections.length === 0 && (
        <p className="px-2 py-4 text-sm text-slate-600">
          Không có phân hệ nào khả dụng cho tài khoản này
        </p>
      )}
      {sections.map((section) => {
        const canToggle = collapsible && Boolean(section.title);
        const isOpen = !canToggle || Boolean(openGroups[section.id]);
        const sectionActive = section.id === activeSectionId;
        const panelId = `sidebar-group-${section.id}`;
        return (
          <div key={section.id}>
            {section.title &&
              (canToggle ? (
                <button
                  type="button"
                  aria-expanded={isOpen}
                  aria-controls={panelId}
                  onClick={() =>
                    setGroupState({ pathname, values: { ...openGroups, [section.id]: !isOpen } })
                  }
                  className={cn(
                    'mb-1 flex min-h-11 w-full items-center justify-between gap-2 rounded-md px-2 text-left text-xs font-medium transition-colors hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary lg:min-h-8',
                    sectionActive ? 'text-primary' : 'text-slate-600',
                    collapsed && 'lg:hidden',
                  )}
                >
                  {section.title}
                  <ChevronRight
                    aria-hidden="true"
                    className={cn(
                      'size-3.5 shrink-0 transition-transform duration-150 motion-reduce:transition-none',
                      isOpen && 'rotate-90',
                    )}
                  />
                </button>
              ) : (
                <h2
                  className={cn(
                    'mb-1 px-2 py-1 text-xs font-medium text-slate-600',
                    collapsed && 'lg:hidden',
                  )}
                >
                  {section.title}
                </h2>
              ))}
            <div
              id={panelId}
              className={cn('space-y-0.5', !isOpen && (collapsed ? 'hidden lg:block' : 'hidden'))}
            >
              {section.items.map((item) => {
                const Icon = item.icon;
                const isActive = item.path === activePath;
                return (
                  <NavLink
                    key={item.path}
                    to={item.path}
                    end={item.path === '/' || !isActive}
                    aria-current={isActive ? 'page' : false}
                    aria-label={item.label}
                    title={item.label}
                    onClick={onNavigate}
                    className={cn(
                      'group flex min-h-11 items-center gap-2.5 rounded-md px-2.5 py-2 text-[length:var(--font-size-md)] leading-5 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary lg:min-h-9',
                      collapsed && 'lg:justify-center lg:px-0',
                      isActive
                        ? 'bg-primary/10 font-semibold text-primary'
                        : 'font-normal text-slate-700 hover:bg-slate-100 hover:text-slate-950',
                    )}
                  >
                    <Icon
                      className={cn(
                        'size-4 shrink-0 stroke-[1.75]',
                        isActive ? 'text-primary' : 'text-slate-500 group-hover:text-slate-700',
                      )}
                    />
                    <span className={cn('min-w-0 flex-1', collapsed && 'lg:hidden')}>
                      {item.label}
                    </span>
                    {item.badge && (
                      <Badge
                        variant="secondary"
                        className={cn('shrink-0 text-xs', collapsed && 'lg:hidden')}
                      >
                        {item.badge}
                      </Badge>
                    )}
                  </NavLink>
                );
              })}
            </div>
          </div>
        );
      })}
    </nav>
  );
}
