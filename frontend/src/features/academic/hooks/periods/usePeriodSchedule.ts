import { useState, useMemo, useCallback } from 'react';
import type { KltnBatchDetailDto } from '../../types/period.types';
import {
  DEFAULT_STAGE_WEEKS,
  getTodayDateString,
  computeStageWeeksFromDetail,
  calculateWeeksFromDateDiff,
  computeStagesFromWeeks,
} from '../../utils/periodDates';

export function usePeriodSchedule() {
  const [academicYear, setAcademicYear] = useState<string>('2026-2027');
  const [semester, setSemester] = useState<number>(1);
  const [batchName, setBatchName] = useState<string>('Đợt 1');
  const [startDate, setStartDate] = useState<string>('');
  const [stageWeeks, setStageWeeks] = useState<number[]>([...DEFAULT_STAGE_WEEKS]);

  const computedStages = useMemo(
    () => computeStagesFromWeeks(startDate, stageWeeks),
    [startDate, stageWeeks],
  );

  const syllabusDeadline = computedStages[1]?.endDate || '';
  const midtermDeadline = computedStages[3]?.endDate || '';
  const finalEvaluationDeadline = computedStages[4]?.endDate || '';
  const defenseDeadline = computedStages[5]?.endDate || '';

  const totalWeeks = useMemo(
    () => stageWeeks.reduce((acc, w) => acc + w, 0),
    [stageWeeks],
  );

  const handleStageWeeksChange = useCallback((index: number, newWeeks: number) => {
    setStageWeeks((prev) => {
      const next = [...prev];
      next[index] = Math.max(1, newWeeks);
      return next;
    });
  }, []);

  const handleSyllabusDeadlineChange = useCallback(
    (val: string) => {
      if (!val || !/^\d{4}-\d{2}-\d{2}$/.test(val) || !computedStages[1]) return;
      const newWeeks = calculateWeeksFromDateDiff(computedStages[1].startDate, val);
      handleStageWeeksChange(1, newWeeks);
    },
    [computedStages, handleStageWeeksChange],
  );

  const handleMidtermDeadlineChange = useCallback(
    (val: string) => {
      if (!val || !/^\d{4}-\d{2}-\d{2}$/.test(val) || !computedStages[3]) return;
      const newWeeks = calculateWeeksFromDateDiff(computedStages[3].startDate, val);
      handleStageWeeksChange(3, newWeeks);
    },
    [computedStages, handleStageWeeksChange],
  );

  const handleFinalEvaluationDeadlineChange = useCallback(
    (val: string) => {
      if (!val || !/^\d{4}-\d{2}-\d{2}$/.test(val) || !computedStages[4]) return;
      const newWeeks = calculateWeeksFromDateDiff(computedStages[4].startDate, val);
      handleStageWeeksChange(4, newWeeks);
    },
    [computedStages, handleStageWeeksChange],
  );

  const handleDefenseDeadlineChange = useCallback(
    (val: string) => {
      if (!val || !/^\d{4}-\d{2}-\d{2}$/.test(val) || !computedStages[5]) return;
      const newWeeks = calculateWeeksFromDateDiff(computedStages[5].startDate, val);
      handleStageWeeksChange(5, newWeeks);
    },
    [computedStages, handleStageWeeksChange],
  );

  const handleResetDefaultWeeks = useCallback(() => {
    setStageWeeks([...DEFAULT_STAGE_WEEKS]);
  }, []);

  const resetForm = useCallback(() => {
    setStartDate(getTodayDateString());
    setAcademicYear('2026-2027');
    setSemester(1);
    setBatchName('Đợt 1');
    setStageWeeks([...DEFAULT_STAGE_WEEKS]);
  }, []);

  const populateFromDetail = useCallback((detail: KltnBatchDetailDto) => {
    setAcademicYear(detail.academicYear);
    setSemester(detail.semester);
    setBatchName(detail.batchName);
    setStartDate(detail.startDate);
    setStageWeeks(computeStageWeeksFromDetail(detail.stages));
  }, []);

  return {
    academicYear,
    setAcademicYear,
    semester,
    setSemester,
    batchName,
    setBatchName,
    startDate,
    setStartDate,
    stageWeeks,
    setStageWeeks,
    computedStages,
    totalWeeks,
    syllabusDeadline,
    midtermDeadline,
    finalEvaluationDeadline,
    defenseDeadline,
    handleStageWeeksChange,
    handleSyllabusDeadlineChange,
    handleMidtermDeadlineChange,
    handleFinalEvaluationDeadlineChange,
    handleDefenseDeadlineChange,
    handleResetDefaultWeeks,
    resetForm,
    populateFromDetail,
  };
}