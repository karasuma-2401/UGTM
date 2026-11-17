import { useMemo } from 'react';
import { syllabusReviewApi } from '../services/syllabusReviewApi';
import type { ReviewGroupItemDto } from '../types/review.types';
import {
  useReviewGateWorkflow,
  type ReviewGateApiAdapter,
} from './useReviewGateWorkflow';

export function useSyllabusReview(knownGroupId?: string) {
  const adapter = useMemo<ReviewGateApiAdapter<ReviewGroupItemDto>>(
    () => ({
      queryKeyPrefix: 'syllabus-review-queue',
      historyQueryKeyPrefix: 'syllabus-history',
      listErrorFallback: 'Không thể tải danh sách hồ sơ đề cương.',
      getReviewQueue: (query, signal) => syllabusReviewApi.getReviewQueue(query, signal),
      getHistory: (groupId, signal) => syllabusReviewApi.getSyllabusHistory(groupId, signal),
      getDownloadUrl: (fileId, signal) => syllabusReviewApi.getDownloadUrl(fileId, signal),
      submitReview: (groupId, payload) => syllabusReviewApi.reviewSyllabus(groupId, payload),
    }),
    [],
  );

  return useReviewGateWorkflow({
    gateKind: 'syllabus',
    knownGroupId,
    adapter,
  });
}
