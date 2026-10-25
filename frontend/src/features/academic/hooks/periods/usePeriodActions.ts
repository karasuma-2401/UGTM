import { useState } from 'react';
import type {
  KltnBatchDetailDto,
  ReviewGateConfigurationDto,
  UpdateStageScheduleItem,
} from '../../types/period.types';
import { periodApi, getPeriodErrorMessage } from '../../services/periodApi';
import { toUtcDeadlineIso } from '../../utils/periodDates';

interface SavePeriodParams {
  selectedBatchId: string;
  currentBatch: KltnBatchDetailDto | null;
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
  onSuccess: (targetBatchId: string, message: string, isNew: boolean) => Promise<void>;
  onError: (msg: string) => void;
}

interface OpenBatchParams {
  currentBatch: KltnBatchDetailDto | null;
  onSuccess: (opened: KltnBatchDetailDto) => Promise<void>;
  onError: (msg: string) => void;
}

interface GateTimeInput {
  initial: string;
  faculty: string;
  final: string;
}

interface SaveGatesParams {
  currentBatch: KltnBatchDetailDto | null;
  syllabus: GateTimeInput;
  midterm: GateTimeInput;
  onSyllabusDeadlineChange: (val: string) => void;
  onMidtermDeadlineChange: (val: string) => void;
  onSuccess: (gates: ReviewGateConfigurationDto[]) => void;
  onError: (msg: string) => void;
}

export function usePeriodActions() {
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [isOpenLoading, setIsOpenLoading] = useState<boolean>(false);

  const executeSave = async ({
    selectedBatchId,
    currentBatch,
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
    onSuccess,
    onError,
  }: SavePeriodParams) => {
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

      let targetBatchId = currentBatch?.id;
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

        targetBatchId = createdBatch.id;

        await periodApi.updateSchedule(targetBatchId, {
          stages: stagesPayload,
        });

        successMsg = 'Khởi tạo đợt KLTN và cấu hình lịch trình giai đoạn thành công!';
      } else if (targetBatchId) {
        await periodApi.updateSchedule(targetBatchId, {
          stages: stagesPayload,
          changeReason: changeReason || undefined,
        });

        successMsg = 'Lưu kế hoạch đợt và deadline thành công!';
      }

      if (targetBatchId) {
        if (syllabusDeadline) {
          const finalUtc = toUtcDeadlineIso(syllabusDeadline, '23:59:00');
          const [y, m, d] = syllabusDeadline.split('-').map(Number);
          const facultyDate = new Date(y, m - 1, d - 2, 23, 59, 0, 0);
          const initialDate = new Date(y, m - 1, d - 5, 23, 59, 0, 0);
          try {
            await periodApi.upsertReviewGateConfiguration(targetBatchId, 'Syllabus', {
              initialSubmissionDeadlineUtc: initialDate.toISOString(),
              facultyResponseDeadlineUtc: facultyDate.toISOString(),
              finalClosureDeadlineUtc: finalUtc,
            });
          } catch {}
        }

        if (midtermDeadline) {
          const finalUtc = toUtcDeadlineIso(midtermDeadline, '23:59:00');
          const [y, m, d] = midtermDeadline.split('-').map(Number);
          const facultyDate = new Date(y, m - 1, d - 2, 23, 59, 0, 0);
          const initialDate = new Date(y, m - 1, d - 5, 23, 59, 0, 0);
          try {
            await periodApi.upsertReviewGateConfiguration(targetBatchId, 'Midterm', {
              initialSubmissionDeadlineUtc: initialDate.toISOString(),
              facultyResponseDeadlineUtc: facultyDate.toISOString(),
              finalClosureDeadlineUtc: finalUtc,
            });
          } catch {}
        }

        await onSuccess(targetBatchId, successMsg, isNew);
      }
    } catch (err) {
      onError(getPeriodErrorMessage(err));
    } finally {
      setIsSaving(false);
    }
  };

  const handleOpenBatch = async ({ currentBatch, onSuccess, onError }: OpenBatchParams) => {
    if (!currentBatch) return;
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
    currentBatch,
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

    try {
      if (syllabus.final) {
        await periodApi.upsertReviewGateConfiguration(currentBatch.id, 'Syllabus', {
          initialSubmissionDeadlineUtc: toUtcDeadlineIso(syllabus.initial, '23:59:00'),
          facultyResponseDeadlineUtc: toUtcDeadlineIso(syllabus.faculty, '23:59:00'),
          finalClosureDeadlineUtc: toUtcDeadlineIso(syllabus.final, '23:59:00'),
        });
        onSyllabusDeadlineChange(syllabus.final);
      }

      if (midterm.final) {
        await periodApi.upsertReviewGateConfiguration(currentBatch.id, 'Midterm', {
          initialSubmissionDeadlineUtc: toUtcDeadlineIso(midterm.initial, '23:59:00'),
          facultyResponseDeadlineUtc: toUtcDeadlineIso(midterm.faculty, '23:59:00'),
          finalClosureDeadlineUtc: toUtcDeadlineIso(midterm.final, '23:59:00'),
        });
        onMidtermDeadlineChange(midterm.final);
      }

      const updatedGates = await periodApi.getReviewGateConfigurations(currentBatch.id);
      onSuccess(updatedGates);
    } catch (err) {
      onError(getPeriodErrorMessage(err));
    }
  };

  return {
    isSaving,
    isOpenLoading,
    executeSave,
    handleOpenBatch,
    handleSaveGates,
  };
}