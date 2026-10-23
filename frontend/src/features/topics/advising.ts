import type { AdvisingData, AdvisingTopic, AdvisingRegistration } from './types/advising.types';

export function searchableText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .trim();
}

export function matchesAdvisingSearch(
  topic: AdvisingTopic,
  groups: AdvisingRegistration[],
  keyword: string,
): boolean {
  const text = searchableText(
    [
      topic.code,
      topic.title,
      topic.titleEn,
      ...groups.flatMap((group) => [
        group.code,
        group.groupCode,
        ...group.students.flatMap((s) => [s.name, s.code, s.email]),
      ]),
    ].join(' '),
  );
  return searchableText(keyword)
    .split(/\s+/)
    .every((word) => text.includes(word));
}

export function getTopicRegistrations(data: AdvisingData, topicId: string) {
  return data.registrations.filter((group) => group.topicId === topicId);
}

export function getConfirmedStudentCount(groups: AdvisingRegistration[]) {
  return groups
    .filter((group) => group.status === 'Confirmed')
    .reduce((total, group) => total + group.students.length, 0);
}
