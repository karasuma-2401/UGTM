import { useSearchParams } from 'react-router-dom';
import { FileText, Users } from 'lucide-react';
import { NavigationTabs, type TabItem } from '@/components/ui/navigation-tabs';
import { TopicProposePage } from './TopicProposePage';
import { TopicAdvisingPage } from './TopicAdvisingPage';
import { PageHeader } from '@/components/ui/page-header';
import {
  GROUP_STATUS_LABELS,
  type AdvisingFilters,
  type GroupScope,
} from '../types/advising.types';
import { TOPIC_STATUS_LABELS, type TopicFilters } from '../types/topic.types';

export function TopicGuidancePage() {
  const [params, setParams] = useSearchParams();
  const selected = params.get('tab');
  // Existing bookmarks continue to resolve to the unified group view.
  const isGroups = ['groups', 'registrations', 'advising'].includes(selected ?? '');
  const batchId = params.get('batchId') ?? '';
  const href = (tab: 'topics' | 'groups') => {
    const query = new URLSearchParams();
    if (tab === 'groups') query.set('tab', 'groups');
    if (batchId) query.set('batchId', batchId);
    return `/topics/advising${query.size ? `?${query}` : ''}`;
  };
  const tabs: TabItem[] = [
    { id: 'topics', label: 'Đề tài của tôi', icon: FileText, href: href('topics') },
    { id: 'groups', label: 'Nhóm & tiến độ', icon: Users, href: href('groups') },
  ];
  const navigation = (
    <NavigationTabs
      tabs={tabs}
      activeId={isGroups ? 'groups' : 'topics'}
      ariaLabel="Đề tài hướng dẫn"
      continuityKey="topics-guidance-tabs"
    />
  );
  const status = params.get('status') ?? '';
  const keyword = params.get('keyword') ?? '';
  function changeTopicFilters(filters: TopicFilters) {
    setParams(
      {
        ...(filters.batchId ? { batchId: filters.batchId } : {}),
        ...(filters.status ? { status: filters.status } : {}),
        ...(filters.keyword ? { keyword: filters.keyword } : {}),
      },
      { flushSync: true },
    );
  }
  if (!isGroups)
    return (
      <TopicProposePage
        pageTitle="Đề tài hướng dẫn"
        navigation={navigation}
        initialFilters={{
          batchId,
          keyword,
          status: Object.hasOwn(TOPIC_STATUS_LABELS, status)
            ? (status as TopicFilters['status'])
            : '',
        }}
        onFiltersChange={changeTopicFilters}
      />
    );
  const requestedScope = params.get('scope') ?? (selected === 'registrations' ? 'all' : 'active');
  const scope: GroupScope =
    status === 'Stopped'
      ? 'stopped'
      : status === 'Completed'
        ? 'completed'
        : ['active', 'all', 'completed', 'stopped'].includes(requestedScope)
          ? (requestedScope as GroupScope)
          : 'active';
  const confirmation = params.get('confirmation') ?? '';
  const filters: AdvisingFilters = {
    batchId,
    keyword,
    scope,
    status: Object.hasOwn(GROUP_STATUS_LABELS, status) ? (status as AdvisingFilters['status']) : '',
    confirmation: confirmation === 'pending' || confirmation === 'expired' ? confirmation : '',
  };
  return (
    <div className="space-y-4 pb-8">
      <PageHeader
        compact
        accent
        title="Đề tài hướng dẫn"
        description="Quản lý đề xuất, thành viên và tiến độ thực hiện khóa luận"
        navigation={navigation}
      />
      <TopicAdvisingPage
        initialFilters={filters}
        onFiltersChange={(next) =>
          setParams(
            {
              tab: 'groups',
              ...(next.batchId ? { batchId: next.batchId } : {}),
              ...(next.scope !== 'active' ? { scope: next.scope } : {}),
              ...(next.status ? { status: next.status } : {}),
              ...(next.confirmation ? { confirmation: next.confirmation } : {}),
              ...(next.keyword ? { keyword: next.keyword } : {}),
            },
            { flushSync: true },
          )
        }
      />
    </div>
  );
}
