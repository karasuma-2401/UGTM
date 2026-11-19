import { useMemo } from 'react';
import { midtermApi } from '../services/midtermApi';
import type { ReviewMidtermGroupItemDto } from '../types/midterm.types';
import {
  useReviewGateWorkflow,
  type ReviewGateApiAdapter,
} from './useReviewGateWorkflow';

export function useMidtermReview(knownGroupId?: string) {
  const adapter = useMemo<ReviewGateApiAdapter<ReviewMidtermGroupItemDto>>(
    () => ({
      queryKeyPrefix: 'midterm-review-queue',
      historyQueryKeyPrefix: 'midterm-history',
      listErrorFallback: 'Không thể tải danh sách hồ sơ báo cáo giữa kỳ.',
      getReviewQueue: (query, signal) => midtermApi.getMidtermReviewQueue(query, signal),
      getHistory: (groupId, signal) => midtermApi.getMidtermHistory(groupId, signal),
      getDownloadUrl: (fileId, optionsOrSignal) => midtermApi.getDownloadUrl(fileId, optionsOrSignal),
      submitReview: (groupId, payload) => midtermApi.reviewMidtermReport(groupId, payload),
    }),
    [],
  );

  return useReviewGateWorkflow({
    gateKind: 'midterm',
    knownGroupId,
    adapter,
  });
}
