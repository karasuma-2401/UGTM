import { useState, useCallback } from 'react';
import type {
  KltnBatchDetailDto,
  UpdateStageScheduleItem,
  UpsertReviewGateConfigurationRequest,
} from '../../types/period.types';
import { periodApi, getPeriodErrorMessage } from '../../services/periodApi';
import {
  type GateTimeInput,
  computeDefaultGateSchedule,
  toUtcDeadlineIso,
} from '../../utils/periodDates';

export interface FailedGateUpdate {
  batchId: string;
  gateType: 'Syllabus' | 'Midterm';
  label: string;
  deadlineDate: string;
  payload: UpsertReviewGateConfigurationRequest;
  errorMessage: string;
}

interface SavePeriodParams {
  selectedBatchId: string;
  currentBatch: KltnBatchDetailDto | null;
  isLoadingDetail?: boolean;
  canManageBatch?: boolean;
  canManageReviewGate?: boolean;
  startDate: string;
  batchName: string;
  academicYear: string;
  semester: number;
  computedStages: Array<{
    orderIndex: number;
    stageCode: string;
    stageName: string;
    startDate: string;
    endDate: string;
  }>;
  syllabusDeadline: string;
  midtermDeadline: string;
  finalEvaluationDeadline: string;
  defenseDeadline: string;
  syllabusGate?: GateTimeInput;
  midtermGate?: GateTimeInput;
  changeReason?: string;
  onBatchCreated?: (createdBatchId: string) => Promise<void> | void;
  onSuccess: (targetBatchId: string, message: string, isNew: boolean) => Promise<void>;
  onError: (msg: string) => void;
}

interface OpenBatchParams {
  selectedBatchId?: string;
  currentBatch: KltnBatchDetailDto | null;
  otherOpenBatchName?: string | null;
  isLoadingDetail?: boolean;
  onSuccess: (opened: KltnBatchDetailDto) => Promise<void>;
  onError: (msg: string) => void;
}

interface RetryFailedGatesParams {
  selectedBatchId?: string;
  onSuccess: (targetBatchId: string, message: string) => Promise<void> | void;
  onError: (msg: string) => void;
}

function validateGateInput(label: string, gate: GateTimeInput): string | null {
  const hasAny = Boolean(gate.initial || gate.faculty || gate.final);
  if (!hasAny) return null;
  if (!gate.initial || !gate.faculty || !gate.final) {
    return `${label}: Vui lòng nhập đầy đủ 3 mốc (SV nộp, Khoa/GV phản hồi, Hạn chót đóng cổng).`;
  }
  if (gate.initial >= gate.faculty || gate.faculty >= gate.final) {
    return `${label}: Hạn sinh viên nộp < Hạn Khoa/GV phản hồi < Hạn chót đóng cổng.`;
  }
  return null;
}

function toGatePayload(gate: GateTimeInput): UpsertReviewGateConfigurationRequest {
  return {
    initialSubmissionDeadlineUtc: toUtcDeadlineIso(gate.initial, '23:59:00'),
    facultyResponseDeadlineUtc: toUtcDeadlineIso(gate.faculty, '23:59:00'),
    finalClosureDeadlineUtc: toUtcDeadlineIso(gate.final, '23:59:00'),
  };
}

export function usePeriodActions() {
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [isOpenLoading, setIsOpenLoading] = useState<boolean>(false);
  const [failedGateUpdates, setFailedGateUpdates] = useState<FailedGateUpdate[]>([]);

  const clearFailedGateUpdates = useCallback(() => {
    setFailedGateUpdates([]);
  }, []);

  const executeSave = async ({
    selectedBatchId,
    currentBatch,
    isLoadingDetail = false,
    canManageBatch = true,
    canManageReviewGate = true,
    startDate,
    batchName,
    academicYear,
    semester,
    computedStages,
    syllabusDeadline,
    midtermDeadline,
    finalEvaluationDeadline,
    defenseDeadline,
    syllabusGate,
    midtermGate,
    changeReason,
    onBatchCreated,
    onSuccess,
    onError,
  }: SavePeriodParams) => {
    if (selectedBatchId !== 'new') {
      if (isLoadingDetail || !currentBatch || currentBatch.id !== selectedBatchId) {
        onError(
          'Dữ liệu đợt khóa luận đang chọn chưa tải xong hoặc không khớp với đợt hiện tại. Vui lòng đợi tải hoàn tất rồi thử lại.',
        );
        return;
      }
    }

    if (!startDate) {
      onError('Vui lòng chọn ngày bắt đầu của đợt KLTN.');
      return;
    }

    if (!batchName.trim()) {
      onError('Vui lòng nhập tên đợt.');
      return;
    }

    if (!academicYear.trim()) {
      onError('Vui lòng nhập năm học (vd: 2026-2027).');
      return;
    }

    const effectiveSyllabusGate = syllabusGate ?? computeDefaultGateSchedule(syllabusDeadline);
    const effectiveMidtermGate = midtermGate ?? computeDefaultGateSchedule(midtermDeadline);

    if (canManageReviewGate) {
      const syllabusError = validateGateInput('Cổng 1 (Đề cương)', effectiveSyllabusGate);
      if (syllabusError) {
        onError(syllabusError);
        return;
      }
      const midtermError = validateGateInput('Cổng 2 (Giữa kỳ)', effectiveMidtermGate);
      if (midtermError) {
        onError(midtermError);
        return;
      }
    }

    setIsSaving(true);
    setFailedGateUpdates([]);

    let createdBatchId: string | null = null;
    try {
      const stagesPayload: UpdateStageScheduleItem[] = computedStages.map((cs) => {
        let deadlineDate = cs.endDate;
        if (cs.orderIndex === 2 && syllabusDeadline) {
          deadlineDate = syllabusDeadline;
        } else if (cs.orderIndex === 4 && midtermDeadline) {
          deadlineDate = midtermDeadline;
        } else if (cs.orderIndex === 5 && finalEvaluationDeadline) {
          deadlineDate = finalEvaluationDeadline;
        } else if (cs.orderIndex === 6 && defenseDeadline) {
          deadlineDate = defenseDeadline;
        }

        return {
          orderIndex: cs.orderIndex,
          stageCode: cs.stageCode,
          stageName: cs.stageName,
          startDate: cs.startDate,
          endDate: cs.endDate,
          deadlineAtUtc: toUtcDeadlineIso(deadlineDate, '23:59:00'),
        };
      });

      let targetBatchId = selectedBatchId === 'new' ? undefined : selectedBatchId;
      let isNew = false;
      let successMsg = '';

      if (selectedBatchId === 'new') {
        if (!canManageBatch) {
          onError('Bạn không có quyền khởi tạo đợt KLTN mới.');
          return;
        }
        isNew = true;
        const createdBatch = await periodApi.createBatch({
          batchName: batchName.trim(),
          academicYear: academicYear.trim(),
          semester,
          startDate,
        });

        createdBatchId = createdBatch.id;
        targetBatchId = createdBatch.id;

        await periodApi.updateSchedule(targetBatchId, {
          stages: stagesPayload,
        });

        successMsg = canManageReviewGate
          ? 'Khởi tạo đợt KLTN, lịch trình và cấu hình cổng thẩm định thành công!'
          : 'Khởi tạo đợt KLTN và cấu hình lịch trình giai đoạn thành công!';
      } else if (targetBatchId) {
        if (canManageBatch) {
          await periodApi.updateSchedule(targetBatchId, {
            batchName: batchName.trim(),
            academicYear: academicYear.trim(),
            semester,
            startDate,
            stages: stagesPayload,
            changeReason: changeReason || undefined,
          });
        }

        successMsg = canManageReviewGate
          ? 'Lưu kế hoạch đợt, deadline và cổng thẩm định thành công!'
          : 'Lưu kế hoạch đợt và deadline thành công!';
      }

      if (targetBatchId && canManageReviewGate) {
        const gatesToSave: Array<{
          gateType: 'Syllabus' | 'Midterm';
          label: string;
          input: GateTimeInput;
        }> = [
          { gateType: 'Syllabus', label: 'Cổng 1 (Đề cương)', input: effectiveSyllabusGate },
          { gateType: 'Midterm', label: 'Cổng 2 (Giữa kỳ)', input: effectiveMidtermGate },
        ];

        const gateFailures: FailedGateUpdate[] = [];
        for (const gate of gatesToSave) {
          if (!gate.input.initial || !gate.input.faculty || !gate.input.final) continue;
          const payload = toGatePayload(gate.input);
          try {
            await periodApi.upsertReviewGateConfiguration(targetBatchId, gate.gateType, payload);
          } catch (err) {
            gateFailures.push({
              batchId: targetBatchId,
              gateType: gate.gateType,
              label: gate.label,
              deadlineDate: gate.input.final,
              payload,
              errorMessage: getPeriodErrorMessage(err),
            });
          }
        }

        if (gateFailures.length > 0) {
          if (createdBatchId) {
            await onBatchCreated?.(createdBatchId);
          }
          setFailedGateUpdates(gateFailures);
          const failedSummary = gateFailures
            .map((f) => `${f.label}: ${f.errorMessage}`)
            .join('; ');
          onError(
            `Đã lưu kế hoạch đợt nhưng chưa cập nhật được cổng thẩm định (${failedSummary}). Vui lòng thử lại phần bị lỗi.`,
          );
          return;
        }
      }

      if (targetBatchId) {
        await onSuccess(targetBatchId, successMsg, isNew);
      }
    } catch (err) {
      const baseMsg = getPeriodErrorMessage(err);
      if (createdBatchId) {
        await onBatchCreated?.(createdBatchId);
        onError(
          `Đã khởi tạo đợt KLTN nhưng cập nhật lịch trình giai đoạn thất bại: ${baseMsg}`,
        );
      } else {
        onError(baseMsg);
      }
    } finally {
      setIsSaving(false);
    }
  };

  const retryFailedGates = async ({
    selectedBatchId,
    onSuccess,
    onError,
  }: RetryFailedGatesParams) => {
    if (failedGateUpdates.length === 0) return;

    const targetBatchId = failedGateUpdates[0].batchId;
    if (selectedBatchId && selectedBatchId !== 'new' && selectedBatchId !== targetBatchId) {
      onError('Đợt khóa luận đang chọn đã thay đổi. Không thể thử lại cổng của đợt khác.');
      return;
    }

    setIsSaving(true);
    try {
      const remainingFailures: FailedGateUpdate[] = [];
      for (const item of failedGateUpdates) {
        try {
          await periodApi.upsertReviewGateConfiguration(item.batchId, item.gateType, item.payload);
        } catch (err) {
          remainingFailures.push({
            ...item,
            errorMessage: getPeriodErrorMessage(err),
          });
        }
      }

      setFailedGateUpdates(remainingFailures);

      if (remainingFailures.length > 0) {
        const failedSummary = remainingFailures
          .map((f) => `${f.label}: ${f.errorMessage}`)
          .join('; ');
        onError(
          `Một số cổng thẩm định vẫn chưa cập nhật được (${failedSummary}). Vui lòng kiểm tra và thử lại.`,
        );
        return;
      }

      await onSuccess(
        targetBatchId,
        'Đã cập nhật thành công hạn chót các cổng thẩm định bị lỗi trước đó!',
      );
    } finally {
      setIsSaving(false);
    }
  };

  const handleOpenBatch = async ({
    selectedBatchId,
    currentBatch,
    otherOpenBatchName = null,
    isLoadingDetail = false,
    onSuccess,
    onError,
  }: OpenBatchParams) => {
    if (!currentBatch) return;
    if (
      isLoadingDetail ||
      (selectedBatchId && selectedBatchId !== 'new' && currentBatch.id !== selectedBatchId)
    ) {
      onError(
        'Dữ liệu đợt khóa luận đang chọn chưa tải xong hoặc không khớp. Vui lòng đợi tải hoàn tất rồi thử lại.',
      );
      return;
    }

    if (otherOpenBatchName) {
      onError(
        `Hiện đã có đợt "${otherOpenBatchName}" đang mở. Vui lòng đóng đợt hiện tại trước khi mở đợt mới.`,
      );
      return;
    }

    setIsOpenLoading(true);
    try {
      const opened = await periodApi.openBatch(currentBatch.id);
      await onSuccess(opened);
    } catch (err) {
      onError(getPeriodErrorMessage(err));
    } finally {
      setIsOpenLoading(false);
    }
  };

  return {
    isSaving,
    isOpenLoading,
    failedGateUpdates,
    clearFailedGateUpdates,
    executeSave,
    retryFailedGates,
    handleOpenBatch,
  };
}