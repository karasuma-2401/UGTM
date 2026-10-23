import { Link, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { FileText, Users, GraduationCap } from 'lucide-react';
import { TopicProposePage } from './TopicProposePage';
import { TopicAdvisingPage } from './TopicAdvisingPage';
import { PageHeader } from '@/components/ui/page-header';

const tabs = [
  { id: 'topics', label: 'Đề tài của tôi', icon: FileText },
  { id: 'registrations', label: 'Nhóm đăng ký', icon: Users },
  { id: 'advising', label: 'Đang hướng dẫn', icon: GraduationCap },
] as const;

export function TopicGuidancePage() {
  const [params, setParams] = useSearchParams();
  const selected = params.get('tab');
  const tab = selected === 'registrations' || selected === 'advising' ? selected : 'topics';
  const status =
    tab === 'registrations' && params.get('status') === 'PendingEmail' ? 'PendingEmail' : '';
  const navigation = (
    <nav
      className="flex gap-1 overflow-x-auto border-b border-slate-200"
      aria-label="Đề tài hướng dẫn"
    >
      {tabs.map((item) => (
        <Button
          key={item.id}
          asChild
          variant="ghost"
          className={`h-12 shrink-0 rounded-none border-b-2 px-4 shadow-none transition-colors ${tab === item.id ? 'border-primary bg-primary/5 text-primary hover:bg-primary/5 hover:text-primary' : 'border-transparent text-slate-500 hover:bg-slate-100/60 hover:text-slate-800'}`}
        >
          <Link
            to={item.id === 'topics' ? '/topics/advising' : `/topics/advising?tab=${item.id}`}
            aria-current={tab === item.id ? 'page' : undefined}
          >
            <item.icon className="size-4" aria-hidden="true" />
            {item.label}
          </Link>
        </Button>
      ))}
    </nav>
  );

  // Router links also pass through the proposal editor's unsaved-changes blocker.
  if (tab === 'topics')
    return <TopicProposePage pageTitle="Đề tài hướng dẫn" navigation={navigation} />;

  return (
    <div className="min-w-0 space-y-5">
      <PageHeader icon={<FileText />} title="Đề tài hướng dẫn" navigation={navigation}>
        <Badge variant="secondary" className="font-normal">
          Dữ liệu nhóm mẫu
        </Badge>
      </PageHeader>
      <TopicAdvisingPage
        key={`${tab}:${status}`}
        embedded
        initialTab={tab}
        initialStatus={status}
        initialBatchId={params.get('batchId') ?? '2026-1'}
        onQuickFilter={(next, filter, batchId) =>
          setParams(
            next === 'topics' ? {} : { tab: next, batchId, ...(filter ? { status: filter } : {}) },
          )
        }
      />
    </div>
  );
}
