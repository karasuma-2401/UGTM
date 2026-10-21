import type { Topic, TopicFormValues } from './types/topic.types';

export const EMPTY_PROPOSAL: TopicFormValues = {
  batchId: '',
  titleVi: '',
  titleEn: '',
  description: '',
  requirements: '',
  studentNumber: 2,
};
export type ProposalErrors = Partial<Record<keyof TopicFormValues, string>>;

export function topicToValues(topic: Topic): TopicFormValues {
  return {
    batchId: topic.batchId,
    titleVi: topic.titleVi,
    titleEn: topic.titleEn ?? '',
    description: topic.description ?? '',
    requirements: topic.requirements ?? '',
    studentNumber: topic.studentNumber === 1 ? 1 : 2,
  };
}

export function copyProposal(topic: Topic): TopicFormValues {
  return { ...topicToValues(topic), batchId: '' };
}

export function validateProposal(values: TopicFormValues, submitting: boolean): ProposalErrors {
  const errors: ProposalErrors = {};
  if (!values.batchId) errors.batchId = 'Vui lòng chọn đợt KLTN.';
  if (!values.titleVi.trim()) errors.titleVi = 'Vui lòng nhập tên đề tài tiếng Việt.';
  else if (values.titleVi.trim().length > 300)
    errors.titleVi = 'Tên tiếng Việt không được vượt quá 300 ký tự.';
  if (values.titleEn.trim().length > 350)
    errors.titleEn = 'Tên tiếng Anh không được vượt quá 350 ký tự.';
  else if (submitting && !values.titleEn.trim())
    errors.titleEn = 'Vui lòng bổ sung tên đề tài tiếng Anh.';
  if (submitting && !values.description.trim())
    errors.description = 'Vui lòng bổ sung mục tiêu và mô tả.';
  if (submitting && !values.requirements.trim())
    errors.requirements = 'Vui lòng bổ sung yêu cầu công nghệ và kiến thức.';
  if (values.studentNumber !== 1 && values.studentNumber !== 2)
    errors.studentNumber = 'Chỉ tiêu sinh viên chỉ được chọn 1 hoặc 2.';
  return errors;
}

export interface LocalProposal {
  version: 1;
  values: TopicFormValues;
  baseline: TopicFormValues;
  savedAt: string;
}

const draftKey = (userId: string, topicId: string | null) =>
  `ugtm:topic-proposal:v1:${encodeURIComponent(userId)}:${topicId ? `topic:${encodeURIComponent(topicId)}` : 'new'}`;

function isValues(value: unknown): value is TopicFormValues {
  if (!value || typeof value !== 'object') return false;
  const fields = value as Record<string, unknown>;
  return (
    ['batchId', 'titleVi', 'titleEn', 'description', 'requirements'].every(
      (key) => typeof fields[key] === 'string',
    ) &&
    (fields.studentNumber === 1 || fields.studentNumber === 2)
  );
}

export function readLocalProposal(
  userId: string,
  topicId: string | null,
  storage: Storage = localStorage,
): LocalProposal | null {
  const raw = storage.getItem(draftKey(userId, topicId));
  if (!raw) return null;
  try {
    const draft = JSON.parse(raw) as LocalProposal;
    return draft.version === 1 &&
      isValues(draft.values) &&
      isValues(draft.baseline) &&
      typeof draft.savedAt === 'string' &&
      Number.isFinite(Date.parse(draft.savedAt))
      ? draft
      : null;
  } catch {
    return null;
  }
}

export function saveLocalProposal(
  userId: string,
  topicId: string | null,
  values: TopicFormValues,
  baseline: TopicFormValues,
  storage: Storage = localStorage,
): LocalProposal {
  const draft: LocalProposal = { version: 1, values, baseline, savedAt: new Date().toISOString() };
  storage.setItem(draftKey(userId, topicId), JSON.stringify(draft));
  return draft;
}

export function removeLocalProposal(
  userId: string,
  topicId: string | null,
  storage: Storage = localStorage,
) {
  storage.removeItem(draftKey(userId, topicId));
}

export function sameProposal(a: TopicFormValues, b: TopicFormValues): boolean {
  return (Object.keys(EMPTY_PROPOSAL) as (keyof TopicFormValues)[]).every(
    (key) => a[key] === b[key],
  );
}
