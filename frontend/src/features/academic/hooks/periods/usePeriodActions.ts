import { useState, useCallback } from 'react';
import type {
  KltnBatchDetailDto,
  ReviewGateConfigurationDto,
  UpdateStageScheduleItem,
  UpsertReviewGateConfigurationRequest,
} from '../../types/period.types';
import { periodApi, getPeriodErrorMessage } from '../../services/periodApi';
import { toUtcDeadlineIso } from '../../utils/periodDates';

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
  changeReason?: string;
  onBatchCreated?: (createdBatchId: string) => Promise<void> | void;
  onPartialSuccess?: (
    targetBatchId: string,
    isNew: boolean,
    failedGates: FailedGateUpdate[],
  ) => Promise<void> | void;
  onSuccess: (targetBatchId: string, message: string, isNew: boolean) => Promise<void>;
  onError: (msg: string) => void;
}

interface OpenBatchParams {
  selectedBatchId?: string;
  currentBatch: KltnBatchDetailDto | null;
  isLoadingDetail?: boolean;
  onSuccess: (opened: KltnBatchDetailDto) => Promise<void>;
  onError: (msg: string) => void;
}

interface GateTimeInput {
  initial: string;
  faculty: string;
  final: string;
}

interface SaveGatesParams {
  selectedBatchId?: string;
  currentBatch: KltnBatchDetailDto | null;
  isLoadingDetail?: boolean;
  syllabus: GateTimeInput;
  midterm: GateTimeInput;
  onSyllabusDeadlineChange: (val: string) => void;
  onMidtermDeadlineChange: (val: string) => void;
  onSuccess: (gates: ReviewGateConfigurationDto[]) => void;
  onError: (msg: string) => void;
}

interface RetryFailedGatesParams {
  selectedBatchId?: string;
  onSuccess: (targetBatchId: string, message: string) => Promise<void> | void;
  onError: (msg: string) => void;
}

function buildAutoGatePayload(deadlineDate: string): UpsertReviewGateConfigurationRequest {
  const finalUtc = toUtcDeadlineIso(deadlineDate, '23:59:00');
  const [y, m, d] = deadlineDate.split('-').map(Number);
  const facultyDate = new Date(y, m - 1, d - 2, 23, 59, 0, 0);
  const initialDate = new Date(y, m - 1, d - 5, 23, 59, 0, 0);
  return {
    initialSubmissionDeadlineUtc: initialDate.toISOString(),
    facultyResponseDeadlineUtc: facultyDate.toISOString(),
    finalClosureDeadlineUtc: finalUtc,
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
    startDate,
    batchName,
    academicYear,
    semester,
    computedStages,
    syllabusDeadline,
    midtermDeadline,
    finalEvaluationDeadline,
    defenseDeadline,
    changeReason,
    onBatchCreated,
    onPartialSuccess,
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
        isNew = true;
        const createdBatch = await periodApi.createBatch({
          batchName: batchName.trim(),
          academicYear: academicYear.trim(),
          semester,
          startDate,
        });

        createdBatchId = createdBatch.id;
        targetBatchId = createdBatch.id;
        await onBatchCreated?.(createdBatch.id);

        await periodApi.updateSchedule(targetBatchId, {
          stages: stagesPayload,
        });

        successMsg = 'Khởi tạo đợt KLTN và cấu hình lịch trình giai đoạn thành công!';
      } else if (targetBatchId) {
        await periodApi.updateSchedule(targetBatchId, {
          batchName: batchName.trim(),
          academicYear: academicYear.trim(),
          semester,
          startDate,
          stages: stagesPayload,
          changeReason: changeReason || undefined,
        });

        successMsg = 'Lưu kế hoạch đợt và deadline thành công!';
      }

      if (targetBatchId) {
        const gatesToUpdate: Array<{
          gateType: 'Syllabus' | 'Midterm';
          label: string;
          deadlineDate: string;
          payload: UpsertReviewGateConfigurationRequest;
        }> = [];

        if (syllabusDeadline) {
          gatesToUpdate.push({
            gateType: 'Syllabus',
            label: 'Cổng 1 (Đề cương)',
            deadlineDate: syllabusDeadline,
            payload: buildAutoGatePayload(syllabusDeadline),
          });
        }

        if (midtermDeadline) {
          gatesToUpdate.push({
            gateType: 'Midterm',
            label: 'Cổng 2 (Giữa kỳ)',
            deadlineDate: midtermDeadline,
            payload: buildAutoGatePayload(midtermDeadline),
          });
        }

        const gateFailures: FailedGateUpdate[] = [];

        for (const gate of gatesToUpdate) {
          try {
            await periodApi.upsertReviewGateConfiguration(
              targetBatchId,
              gate.gateType,
              gate.payload,
            );
          } catch (gateErr) {
            gateFailures.push({
              batchId: targetBatchId,
              gateType: gate.gateType,
              label: gate.label,
              deadlineDate: gate.deadlineDate,
              payload: gate.payload,
              errorMessage: getPeriodErrorMessage(gateErr),
            });
          }
        }

        if (gateFailures.length > 0) {
          setFailedGateUpdates(gateFailures);
          await onPartialSuccess?.(targetBatchId, isNew, gateFailures);
          const failedSummary = gateFailures
            .map((f) => `${f.label}: ${f.errorMessage}`)
            .join('; ');
          onError(
            `Đã lưu lịch trình giai đoạn của đợt, nhưng cập nhật hạn chót cổng thẩm định thất bại (${failedSummary}). Vui lòng thử lại phần bị lỗi.`,
          );
          return;
        }

        await onSuccess(targetBatchId, successMsg, isNew);
      }
    } catch (err) {
      const baseMsg = getPeriodErrorMessage(err);
      if (createdBatchId) {
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

  const handleSaveGates = async ({
    selectedBatchId,
    currentBatch,
    isLoadingDetail = false,
    syllabus,
    midterm,
    onSyllabusDeadlineChange,
    onMidtermDeadlineChange,
    onSuccess,
    onError,
  }: SaveGatesParams) => {
    if (!currentBatch) {
      onError('Vui lòng lưu thông tin đợt trước khi cấu hình cổng thẩm định.');
      return;
    }

    if (
      isLoadingDetail ||
      (selectedBatchId && selectedBatchId !== 'new' && currentBatch.id !== selectedBatchId)
    ) {
      onError(
        'Dữ liệu đợt khóa luận đang chọn chưa tải xong hoặc không khớp. Vui lòng đợi tải hoàn tất rồi thử lại.',
      );
      return;
    }

    const gateFailures: FailedGateUpdate[] = [];

    if (syllabus.final) {
      const payload: UpsertReviewGateConfigurationRequest = {
        initialSubmissionDeadlineUtc: toUtcDeadlineIso(syllabus.initial, '23:59:00'),
        facultyResponseDeadlineUtc: toUtcDeadlineIso(syllabus.faculty, '23:59:00'),
        finalClosureDeadlineUtc: toUtcDeadlineIso(syllabus.final, '23:59:00'),
      };
      try {
        await periodApi.upsertReviewGateConfiguration(currentBatch.id, 'Syllabus', payload);
        onSyllabusDeadlineChange(syllabus.final);
      } catch (err) {
        gateFailures.push({
          batchId: currentBatch.id,
          gateType: 'Syllabus',
          label: 'Cổng 1 (Đề cương)',
          deadlineDate: syllabus.final,
          payload,
          errorMessage: getPeriodErrorMessage(err),
        });
      }
    }

    if (midterm.final) {
      const payload: UpsertReviewGateConfigurationRequest = {
        initialSubmissionDeadlineUtc: toUtcDeadlineIso(midterm.initial, '23:59:00'),
        facultyResponseDeadlineUtc: toUtcDeadlineIso(midterm.faculty, '23:59:00'),
        finalClosureDeadlineUtc: toUtcDeadlineIso(midterm.final, '23:59:00'),
      };
      try {
        await periodApi.upsertReviewGateConfiguration(currentBatch.id, 'Midterm', payload);
        onMidtermDeadlineChange(midterm.final);
      } catch (err) {
        gateFailures.push({
          batchId: currentBatch.id,
          gateType: 'Midterm',
          label: 'Cổng 2 (Giữa kỳ)',
          deadlineDate: midterm.final,
          payload,
          errorMessage: getPeriodErrorMessage(err),
        });
      }
    }

    try {
      const updatedGates = await periodApi.getReviewGateConfigurations(currentBatch.id);
      if (gateFailures.length > 0) {
        setFailedGateUpdates(gateFailures);
        const failedSummary = gateFailures
          .map((f) => `${f.label}: ${f.errorMessage}`)
          .join('; ');
        onError(
          `Chưa thể lưu đầy đủ cấu hình cổng thẩm định (${failedSummary}). Vui lòng thử lại phần bị lỗi.`,
        );
        return;
      }
      setFailedGateUpdates([]);
      onSuccess(updatedGates);
    } catch (err) {
      if (gateFailures.length > 0) {
        setFailedGateUpdates(gateFailures);
        const failedSummary = gateFailures
          .map((f) => `${f.label}: ${f.errorMessage}`)
          .join('; ');
        onError(
          `Cập nhật cổng thẩm định thất bại (${failedSummary}). Vui lòng thử lại.`,
        );
      } else {
        onError(getPeriodErrorMessage(err));
      }
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
    handleSaveGates,
  };
}