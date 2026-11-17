import { useState, useMemo, useCallback } from 'react';
import type { KltnBatchDetailDto, ReviewGateConfigurationDto } from '../../types/period.types';
import {
  DEFAULT_STAGE_WEEKS,
  type GateTimeInput,
  computeDefaultGateSchedule,
  toLocalDateInput,
  getTodayDateString,
  computeStageWeeksFromDetail,
  calculateWeeksFromDateDiff,
  computeStagesFromWeeks,
} from '../../utils/periodDates';

function toGateInputFromDto(gate?: ReviewGateConfigurationDto | null): GateTimeInput | null {
  if (!gate) return null;
  const initial = toLocalDateInput(gate.initialSubmissionDeadlineUtc);
  const faculty = toLocalDateInput(gate.facultyResponseDeadlineUtc);
  const final = toLocalDateInput(gate.finalClosureDeadlineUtc);
  if (!initial || !faculty || !final) return null;
  return { initial, faculty, final };
}

export function usePeriodSchedule() {
  const [academicYear, setAcademicYear] = useState<string>('2026-2027');
  const [semester, setSemester] = useState<number>(1);
  const [batchName, setBatchName] = useState<string>('Đợt 1');
  const [startDate, setStartDateState] = useState<string>('');
  const [stageWeeks, setStageWeeks] = useState<number[]>([...DEFAULT_STAGE_WEEKS]);
  const [customSyllabusGate, setCustomSyllabusGate] = useState<GateTimeInput | null>(null);
  const [customMidtermGate, setCustomMidtermGate] = useState<GateTimeInput | null>(null);

  const computedStages = useMemo(
    () => computeStagesFromWeeks(startDate, stageWeeks),
    [startDate, stageWeeks],
  );

  const syllabusDeadline = computedStages[1]?.endDate || '';
  const midtermDeadline = computedStages[3]?.endDate || '';
  const finalEvaluationDeadline = computedStages[4]?.endDate || '';
  const defenseDeadline = computedStages[5]?.endDate || '';

  const syllabusGateDraft = useMemo(
    () => customSyllabusGate ?? computeDefaultGateSchedule(syllabusDeadline),
    [customSyllabusGate, syllabusDeadline],
  );
  const midtermGateDraft = useMemo(
    () => customMidtermGate ?? computeDefaultGateSchedule(midtermDeadline),
    [customMidtermGate, midtermDeadline],
  );

  const totalWeeks = useMemo(
    () => stageWeeks.reduce((acc, w) => acc + w, 0),
    [stageWeeks],
  );

  const setStartDate = useCallback((nextStartDate: string) => {
    setStartDateState(nextStartDate);
    setCustomSyllabusGate(null);
    setCustomMidtermGate(null);
  }, []);

  const handleStageWeeksChange = useCallback((index: number, newWeeks: number) => {
    setStageWeeks((prev) => {
      const next = [...prev];
      next[index] = Math.max(1, newWeeks);
      return next;
    });
    if (index <= 1) {
      setCustomSyllabusGate(null);
    }
    if (index <= 3) {
      setCustomMidtermGate(null);
    }
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
    setCustomSyllabusGate(null);
    setCustomMidtermGate(null);
  }, []);

  const resetForm = useCallback(() => {
    setStartDateState(getTodayDateString());
    setAcademicYear('2026-2027');
    setSemester(1);
    setBatchName('Đợt 1');
    setStageWeeks([...DEFAULT_STAGE_WEEKS]);
    setCustomSyllabusGate(null);
    setCustomMidtermGate(null);
  }, []);

  const populateFromDetail = useCallback(
    (detail: KltnBatchDetailDto, gates?: ReviewGateConfigurationDto[]) => {
      setAcademicYear(detail.academicYear);
      setSemester(detail.semester);
      setBatchName(detail.batchName);
      setStartDateState(detail.startDate);
      setStageWeeks(computeStageWeeksFromDetail(detail.stages));
      setCustomSyllabusGate(
        toGateInputFromDto(gates?.find((gate) => gate.gateType === 'Syllabus')),
      );
      setCustomMidtermGate(
        toGateInputFromDto(gates?.find((gate) => gate.gateType === 'Midterm')),
      );
    },
    [],
  );

  const setGateDrafts = useCallback(
    (syllabus: GateTimeInput, midterm: GateTimeInput) => {
      setCustomSyllabusGate(syllabus.initial && syllabus.faculty && syllabus.final ? syllabus : null);
      setCustomMidtermGate(midterm.initial && midterm.faculty && midterm.final ? midterm : null);
    },
    [],
  );

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
    syllabusGateDraft,
    midtermGateDraft,
    setGateDrafts,
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