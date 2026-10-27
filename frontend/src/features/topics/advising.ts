import type { AdvisingTab, TrackingTopic } from './types/advising.types';

export function searchableText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .trim();
}

export function isAdvisingGroup(topic: TrackingTopic): boolean {
  return Boolean(topic.group && !['Completed', 'Stopped'].includes(topic.group.status));
}

export function getConfirmationStatistics(topics: TrackingTopic[] | undefined) {
  if (!topics) return { pending: null, confirmed: null };
  const active = topics.filter(isAdvisingGroup);
  const sum = (field: 'confirmedStudentsCount' | 'pendingConfirmationsCount') =>
    active.every((topic) => typeof topic[field] === 'number')
      ? active.reduce((total, topic) => total + topic[field]!, 0)
      : null;
  return { pending: sum('pendingConfirmationsCount'), confirmed: sum('confirmedStudentsCount') };
}

export function getAdvisingRows(
  topics: TrackingTopic[],
  tab: AdvisingTab,
  keyword: string,
  status: string,
): TrackingTopic[] {
  const words = searchableText(keyword).split(/\s+/).filter(Boolean);
  return topics
    .filter((topic) => {
      if (tab !== 'topics' && !topic.group) return false;
      if (tab === 'advising' && !isAdvisingGroup(topic)) return false;
      if (status && (tab === 'topics' ? topic.status : topic.group?.status) !== status)
        return false;
      const text = searchableText(
        [
          topic.id,
          topic.titleVi,
          topic.titleEn,
          topic.group?.groupId,
          ...(topic.group?.students.flatMap((student) => [
            student.fullName,
            student.userCode,
            student.email,
          ]) ?? []),
        ].join(' '),
      );
      return words.every((word) => text.includes(word));
    })
    .sort((a, b) => {
      const dateA = tab === 'topics' ? a.createdAt : a.group!.createdAt;
      const dateB = tab === 'topics' ? b.createdAt : b.group!.createdAt;
      return Date.parse(dateB) - Date.parse(dateA) || a.id.localeCompare(b.id);
    });
}
