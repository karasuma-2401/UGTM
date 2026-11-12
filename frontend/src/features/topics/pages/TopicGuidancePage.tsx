import { useSearchParams } from 'react-router-dom';
import { FileText, Users, GraduationCap } from 'lucide-react';
import { NavigationTabs, type TabItem } from '@/components/ui/navigation-tabs';
import { TopicProposePage } from './TopicProposePage';
import { TopicAdvisingPage } from './TopicAdvisingPage';
import { PageHeader } from '@/components/ui/page-header';

const tabs: TabItem[] = [
  { id: 'topics', label: 'Đề tài của tôi', icon: FileText, href: '/topics/advising' },
  { id: 'registrations', label: 'Nhóm đăng ký', icon: Users, href: '/topics/advising?tab=registrations' },
  { id: 'advising', label: 'Đang hướng dẫn', icon: GraduationCap, href: '/topics/advising?tab=advising' },
];

export function TopicGuidancePage() {
  const [params, setParams] = useSearchParams();
  const selected = params.get('tab');
  const tab = selected === 'registrations' || selected === 'advising' ? selected : 'topics';
  const status = params.get('status') ?? '';

  const navigation = (
    <NavigationTabs
      tabs={tabs}
      activeId={tab}
      ariaLabel="Đề tài hướng dẫn"
      continuityKey="topics-guidance-tabs"
    />
  );

  // Router links also pass through the proposal editor's unsaved-changes blocker.
  if (tab === 'topics')
    return <TopicProposePage pageTitle="Đề tài hướng dẫn" navigation={navigation} />;

  return (
    <div className="space-y-6 pb-12 animate-in fade-in-50 duration-300">
      <PageHeader
        compact
        accent
        title="Đề tài hướng dẫn"
        description="Quản lý danh sách đề tài đề xuất, theo dõi nhóm sinh viên đăng ký và tiến độ hướng dẫn khóa luận tốt nghiệp"
        navigation={navigation}
      />
      <TopicAdvisingPage
        key={`${tab}:${status}:${params.get('batchId') ?? ''}`}
        embedded
        initialTab={tab}
        initialStatus={status}
        initialBatchId={params.get('batchId') ?? ''}
        onQuickFilter={(next, filter, batchId) =>
          setParams(
            next === 'topics'
              ? {}
              : {
                  tab: next,
                  ...(batchId ? { batchId } : {}),
                  ...(filter ? { status: filter } : {}),
                },
          )
        }
      />
    </div>
  );
}
