import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  CalendarRange,
  Plus,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
} from 'lucide-react';
import { useAuth } from '@/shared/auth';
import { Button } from '@/components/ui/button';
import {
  PeriodStagesCard,
  PeriodInfoCard,
  PeriodDeadlinesCard,
  PeriodSummaryCard,
  PeriodActionBar,
  PeriodReviewGateDialog,
} from '../components/periods';
import { StudentImportDialog } from '../components/users/StudentImportDialog';
import { ScoreConfigCloneReasonDialog } from '../components/score-config/ScoreConfigCloneReasonDialog';
import {
  periodApi,
  getPeriodErrorMessage,
} from '../services/periodApi';
import {
  STANDARD_7_STAGES,
  type KltnBatchSummaryDto,
  type KltnBatchDetailDto,
  type ReviewGateConfigurationDto,
  type UpdateStageScheduleItem,
} from '../types/period.types';

const DEFAULT_STAGE_WEEKS: readonly number[] = [1, 1, 7, 1, 8, 2, 1];

function addDaysToDateString(baseDateStr: string, daysToAdd: number): string {
  try {
    const [y, m, d] = baseDateStr.split('-').map(Number);
    if (!y || !m || !d) return baseDateStr;
    const date = new Date(y, m - 1, d + daysToAdd);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  } catch {
    return baseDateStr;
  }
}

function toLocalDatetimeInput(utcStr?: string): string {
  if (!utcStr) return '';
  try {
    const d = new Date(utcStr);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    const yyyy = d.getFullYear();
    const mm = pad(d.getMonth() + 1);
    const dd = pad(d.getDate());
    const hh = pad(d.getHours());
    const min = pad(d.getMinutes());
    return `${yyyy}-${mm}-${dd}T${hh}:${min}`;
  } catch {
    return '';
  }
}

function toUtcIso(localDatetimeStr: string): string {
  if (!localDatetimeStr) return '';
  try {
    const d = new Date(localDatetimeStr);
    if (isNaN(d.getTime())) return '';
    return d.toISOString();
  } catch {
    return '';
  }
}

export const PeriodPage: React.FC = () => {
  const { hasPermission } = useAuth();
  const canManageBatch = hasPermission('academic.batches.manage');
  const canManageReviewGate = hasPermission('thesis.review-gate-config.manage');
  const canEdit = canManageBatch || canManageReviewGate;

  const [batches, setBatches] = useState<KltnBatchSummaryDto[]>([]);
  const [selectedBatchId, setSelectedBatchId] = useState<string>('new');
  const [isLoadingBatches, setIsLoadingBatches] = useState<boolean>(true);

  const [currentBatch, setCurrentBatch] = useState<KltnBatchDetailDto | null>(null);
  const [reviewGates, setReviewGates] = useState<ReviewGateConfigurationDto[]>([]);
  const [isLoadingDetail, setIsLoadingDetail] = useState<boolean>(false);

  const [academicYear, setAcademicYear] = useState<string>('2026-2027');
  const [semester, setSemester] = useState<number>(1);
  const [batchName, setBatchName] = useState<string>('Đợt 1');
  const [startDate, setStartDate] = useState<string>('');

  const [stageWeeks, setStageWeeks] = useState<number[]>([...DEFAULT_STAGE_WEEKS]);

  const [syllabusDeadline, setSyllabusDeadline] = useState<string>('');
  const [midtermDeadline, setMidtermDeadline] = useState<string>('');
  const [finalEvaluationDeadline, setFinalEvaluationDeadline] = useState<string>('');
  const [defenseDeadline, setDefenseDeadline] = useState<string>('');

  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [isOpenLoading, setIsOpenLoading] = useState<boolean>(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [isGateDetailsOpen, setIsGateDetailsOpen] = useState<boolean>(false);
  const [isImportOpen, setIsImportOpen] = useState<boolean>(false);
  const [isChangeReasonOpen, setIsChangeReasonOpen] = useState<boolean>(false);

  const syllabusGate = useMemo(
    () => reviewGates.find((g) => g.gateType === 'Syllabus') || null,
    [reviewGates],
  );

  const midtermGate = useMemo(
    () => reviewGates.find((g) => g.gateType === 'Midterm') || null,
    [reviewGates],
  );

  const computedStages = useMemo(() => {
    const result = [];
    let currentStart = startDate || new Date().toISOString().split('T')[0];
    let cumulativeWeek = 0;

    for (let index = 0; index < STANDARD_7_STAGES.length; index++) {
      const meta = STANDARD_7_STAGES[index];
      const weeks = stageWeeks[index] || 1;
      const startWeek = cumulativeWeek + 1;
      const endWeek = cumulativeWeek + weeks;
      cumulativeWeek = endWeek;

      const stageStartDate = currentStart;
      const stageEndDate = addDaysToDateString(stageStartDate, weeks * 7 - 1);
      currentStart = addDaysToDateString(stageEndDate, 1);

      const [y, m, d] = stageEndDate.split('-').map(Number);
      const localDeadline = new Date(y, m - 1, d, 23, 59, 0, 0);
      const defaultDeadlineUtc = localDeadline.toISOString();

      result.push({
        orderIndex: meta.orderIndex,
        stageCode: meta.stageCode,
        stageName: meta.stageName,
        shortName: meta.shortName,
        actor: meta.actor,
        weeks,
        startWeek,
        endWeek,
        weekLabel: weeks === 1 ? `Tuần ${startWeek}` : `Tuần ${startWeek}–${endWeek}`,
        startDate: stageStartDate,
        endDate: stageEndDate,
        defaultDeadlineUtc,
      });
    }

    return result;
  }, [startDate, stageWeeks]);

  const totalWeeks = useMemo(
    () => stageWeeks.reduce((acc, w) => acc + w, 0),
    [stageWeeks],
  );

  const handleStageWeeksChange = (index: number, newWeeks: number) => {
    setStageWeeks((prev) => {
      const next = [...prev];
      next[index] = Math.max(1, newWeeks);
      return next;
    });
  };

  const handleResetDefaultWeeks = () => {
    setStageWeeks([...DEFAULT_STAGE_WEEKS]);
  };

  const fetchBatches = useCallback(async (selectId?: string) => {
    setIsLoadingBatches(true);
    try {
      const result = await periodApi.getBatches({ pageSize: 50 });
      setBatches(result.items);

      if (selectId) {
        setSelectedBatchId(selectId);
      } else if (result.items.length > 0) {
        setSelectedBatchId(result.items[0].id);
      } else {
        setSelectedBatchId('new');
      }
    } catch (err) {
      setErrorMessage(getPeriodErrorMessage(err));
    } finally {
      setIsLoadingBatches(false);
    }
  }, []);

  useEffect(() => {
    fetchBatches();
  }, [fetchBatches]);

  const loadBatchDetail = useCallback(async (batchId: string) => {
    if (batchId === 'new') {
      setCurrentBatch(null);
      setReviewGates([]);
      const today = new Date();
      const pad = (n: number) => String(n).padStart(2, '0');
      const defaultStart = `${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`;
      setStartDate(defaultStart);
      setAcademicYear('2026-2027');
      setSemester(1);
      setBatchName('Đợt 1');
      setStageWeeks([...DEFAULT_STAGE_WEEKS]);

      const s2End = addDaysToDateString(defaultStart, 13);
      const s4End = addDaysToDateString(defaultStart, 69);
      const s5End = addDaysToDateString(defaultStart, 125);
      const s6End = addDaysToDateString(defaultStart, 139);

      setSyllabusDeadline(`${s2End}T23:59`);
      setMidtermDeadline(`${s4End}T23:59`);
      setFinalEvaluationDeadline(`${s5End}T23:59`);
      setDefenseDeadline(`${s6End}T23:59`);
      return;
    }

    setIsLoadingDetail(true);
    setErrorMessage(null);
    try {
      const [detail, gates] = await Promise.all([
        periodApi.getBatchById(batchId),
        periodApi.getReviewGateConfigurations(batchId),
      ]);
      setCurrentBatch(detail);
      setReviewGates(gates);

      setAcademicYear(detail.academicYear);
      setSemester(detail.semester);
      setBatchName(detail.batchName);
      setStartDate(detail.startDate);

      if (detail.stages && detail.stages.length === 7) {
        const sorted = [...detail.stages].sort((a, b) => a.orderIndex - b.orderIndex);
        const computed = sorted.map((s) => {
          const [sy, sm, sd] = s.startDate.split('-').map(Number);
          const [ey, em, ed] = s.endDate.split('-').map(Number);
          const start = new Date(sy, sm - 1, sd).getTime();
          const end = new Date(ey, em - 1, ed).getTime();
          const days = Math.round((end - start) / (1000 * 60 * 60 * 24)) + 1;
          return Math.max(1, Math.round(days / 7));
        });
        setStageWeeks(computed);
      } else {
        setStageWeeks([...DEFAULT_STAGE_WEEKS]);
      }

      const sylGate = gates.find((g) => g.gateType === 'Syllabus');
      const midGate = gates.find((g) => g.gateType === 'Midterm');

      const stage2 = detail.stages.find((s) => s.orderIndex === 2 || s.stageCode === 'REGISTRATION_AND_SYLLABUS');
      const stage4 = detail.stages.find((s) => s.orderIndex === 4 || s.stageCode === 'MIDTERM_GATE');
      const stage5 = detail.stages.find((s) => s.orderIndex === 5 || s.stageCode === 'FINAL_EVALUATION');
      const stage6 = detail.stages.find((s) => s.orderIndex === 6 || s.stageCode === 'DEFENSE');

      setSyllabusDeadline(
        toLocalDatetimeInput(sylGate?.finalClosureDeadlineUtc || stage2?.deadlineAtUtc),
      );
      setMidtermDeadline(
        toLocalDatetimeInput(midGate?.finalClosureDeadlineUtc || stage4?.deadlineAtUtc),
      );
      setFinalEvaluationDeadline(
        toLocalDatetimeInput(stage5?.deadlineAtUtc),
      );
      setDefenseDeadline(
        toLocalDatetimeInput(stage6?.deadlineAtUtc),
      );
    } catch (err) {
      setErrorMessage(getPeriodErrorMessage(err));
    } finally {
      setIsLoadingDetail(false);
    }
  }, []);

  useEffect(() => {
    if (selectedBatchId) {
      loadBatchDetail(selectedBatchId);
    }
  }, [selectedBatchId, loadBatchDetail]);

  const handleStartDateChange = (val: string) => {
    setStartDate(val);
  };

  const executeSave = async (reason?: string) => {
    setNotice(null);
    setErrorMessage(null);

    if (!startDate) {
      setErrorMessage('Vui lòng chọn ngày bắt đầu của đợt KLTN.');
      return;
    }

    if (!batchName.trim()) {
      setErrorMessage('Vui lòng nhập tên đợt.');
      return;
    }

    if (!academicYear.trim()) {
      setErrorMessage('Vui lòng nhập năm học (vd: 2026-2027).');
      return;
    }

    setIsSaving(true);
    try {
      if (selectedBatchId === 'new') {
        const createdBatch = await periodApi.createBatch({
          batchName: batchName.trim(),
          academicYear: academicYear.trim(),
          semester,
          startDate,
        });

        const stagesPayload: UpdateStageScheduleItem[] = computedStages.map((cs) => {
          let deadlineUtc = cs.defaultDeadlineUtc;
          if (cs.orderIndex === 2 && syllabusDeadline) {
            deadlineUtc = toUtcIso(syllabusDeadline);
          } else if (cs.orderIndex === 4 && midtermDeadline) {
            deadlineUtc = toUtcIso(midtermDeadline);
          } else if (cs.orderIndex === 5 && finalEvaluationDeadline) {
            deadlineUtc = toUtcIso(finalEvaluationDeadline);
          } else if (cs.orderIndex === 6 && defenseDeadline) {
            deadlineUtc = toUtcIso(defenseDeadline);
          }

          return {
            orderIndex: cs.orderIndex,
            stageCode: cs.stageCode,
            stageName: cs.stageName,
            startDate: cs.startDate,
            endDate: cs.endDate,
            deadlineAtUtc: deadlineUtc,
          };
        });

        await periodApi.updateSchedule(createdBatch.id, {
          stages: stagesPayload,
        });

        if (syllabusDeadline) {
          const finalUtc = toUtcIso(syllabusDeadline);
          const finalDate = new Date(syllabusDeadline);
          const facultyDate = new Date(finalDate.getTime() - 2 * 86400000);
          const initialDate = new Date(finalDate.getTime() - 5 * 86400000);
          try {
            await periodApi.upsertReviewGateConfiguration(createdBatch.id, 'Syllabus', {
              initialSubmissionDeadlineUtc: initialDate.toISOString(),
              facultyResponseDeadlineUtc: facultyDate.toISOString(),
              finalClosureDeadlineUtc: finalUtc,
            });
          } catch {
          }
        }

        if (midtermDeadline) {
          const finalUtc = toUtcIso(midtermDeadline);
          const finalDate = new Date(midtermDeadline);
          const facultyDate = new Date(finalDate.getTime() - 2 * 86400000);
          const initialDate = new Date(finalDate.getTime() - 5 * 86400000);
          try {
            await periodApi.upsertReviewGateConfiguration(createdBatch.id, 'Midterm', {
              initialSubmissionDeadlineUtc: initialDate.toISOString(),
              facultyResponseDeadlineUtc: facultyDate.toISOString(),
              finalClosureDeadlineUtc: finalUtc,
            });
          } catch {
          }
        }

        setNotice('Khởi tạo đợt KLTN và cấu hình lịch trình giai đoạn thành công!');
        await fetchBatches(createdBatch.id);
      } else if (currentBatch) {
        const stagesPayload: UpdateStageScheduleItem[] = computedStages.map((cs) => {
          let deadlineUtc = cs.defaultDeadlineUtc;
          if (cs.orderIndex === 2 && syllabusDeadline) {
            deadlineUtc = toUtcIso(syllabusDeadline);
          } else if (cs.orderIndex === 4 && midtermDeadline) {
            deadlineUtc = toUtcIso(midtermDeadline);
          } else if (cs.orderIndex === 5 && finalEvaluationDeadline) {
            deadlineUtc = toUtcIso(finalEvaluationDeadline);
          } else if (cs.orderIndex === 6 && defenseDeadline) {
            deadlineUtc = toUtcIso(defenseDeadline);
          }

          return {
            orderIndex: cs.orderIndex,
            stageCode: cs.stageCode,
            stageName: cs.stageName,
            startDate: cs.startDate,
            endDate: cs.endDate,
            deadlineAtUtc: deadlineUtc,
          };
        });

        const updated = await periodApi.updateSchedule(currentBatch.id, {
          stages: stagesPayload,
          changeReason: reason || undefined,
        });

        if (syllabusDeadline) {
          const finalUtc = toUtcIso(syllabusDeadline);
          const finalDate = new Date(syllabusDeadline);
          const facultyDate = new Date(finalDate.getTime() - 2 * 86400000);
          const initialDate = new Date(finalDate.getTime() - 5 * 86400000);
          try {
            await periodApi.upsertReviewGateConfiguration(currentBatch.id, 'Syllabus', {
              initialSubmissionDeadlineUtc: initialDate.toISOString(),
              facultyResponseDeadlineUtc: facultyDate.toISOString(),
              finalClosureDeadlineUtc: finalUtc,
            });
          } catch {
          }
        }

        if (midtermDeadline) {
          const finalUtc = toUtcIso(midtermDeadline);
          const finalDate = new Date(midtermDeadline);
          const facultyDate = new Date(finalDate.getTime() - 2 * 86400000);
          const initialDate = new Date(finalDate.getTime() - 5 * 86400000);
          try {
            await periodApi.upsertReviewGateConfiguration(currentBatch.id, 'Midterm', {
              initialSubmissionDeadlineUtc: initialDate.toISOString(),
              facultyResponseDeadlineUtc: facultyDate.toISOString(),
              finalClosureDeadlineUtc: finalUtc,
            });
          } catch {
          }
        }

        setCurrentBatch(updated);
        setNotice('Lưu kế hoạch đợt và deadline thành công!');
        await loadBatchDetail(currentBatch.id);
      }
    } catch (err) {
      setErrorMessage(getPeriodErrorMessage(err));
    } finally {
      setIsSaving(false);
    }
  };

  const handleSaveClick = () => {
    if (currentBatch && currentBatch.status === 'Open') {
      setIsChangeReasonOpen(true);
    } else {
      executeSave();
    }
  };

  const handleConfirmChangeReason = (reason: string) => {
    setIsChangeReasonOpen(false);
    executeSave(reason);
  };

  const handleOpenBatch = async () => {
    if (!currentBatch) return;
    setIsOpenLoading(true);
    setNotice(null);
    setErrorMessage(null);
    try {
      const opened = await periodApi.openBatch(currentBatch.id);
      setCurrentBatch(opened);
      setNotice('Đợt KLTN đã được mở vận hành thành công!');
      await fetchBatches(opened.id);
    } catch (err) {
      setErrorMessage(getPeriodErrorMessage(err));
    } finally {
      setIsOpenLoading(false);
    }
  };

  const handleSaveGates = async (
    syllabus: { initial: string; faculty: string; final: string },
    midterm: { initial: string; faculty: string; final: string },
  ) => {
    if (!currentBatch) {
      throw new Error('Vui lòng lưu thông tin đợt trước khi cấu hình cổng thẩm định.');
    }

    if (syllabus.final) {
      await periodApi.upsertReviewGateConfiguration(currentBatch.id, 'Syllabus', {
        initialSubmissionDeadlineUtc: toUtcIso(syllabus.initial),
        facultyResponseDeadlineUtc: toUtcIso(syllabus.faculty),
        finalClosureDeadlineUtc: toUtcIso(syllabus.final),
      });
      setSyllabusDeadline(syllabus.final);
    }

    if (midterm.final) {
      await periodApi.upsertReviewGateConfiguration(currentBatch.id, 'Midterm', {
        initialSubmissionDeadlineUtc: toUtcIso(midterm.initial),
        facultyResponseDeadlineUtc: toUtcIso(midterm.faculty),
        finalClosureDeadlineUtc: toUtcIso(midterm.final),
      });
      setMidtermDeadline(midterm.final);
    }

    const updatedGates = await periodApi.getReviewGateConfigurations(currentBatch.id);
    setReviewGates(updatedGates);
    setNotice('Cập nhật chi tiết cổng thẩm định thành công!');
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">
            Khóa luận / Phòng Đào tạo
          </div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">
              Khởi tạo đợt & deadline
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-[#2054A5] ring-1 ring-blue-500/20">
              <CalendarRange className="size-3.5" />
              Kế hoạch đợt
            </span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <select
            value={selectedBatchId}
            disabled={isLoadingBatches}
            onChange={(e) => setSelectedBatchId(e.target.value)}
            className="h-9 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-800 shadow-2xs focus:outline-hidden focus:ring-2 focus:ring-[#2054A5]"
          >
            {batches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.batchName} ({b.academicYear} - HK{b.semester}) — {b.status}
              </option>
            ))}
            <option value="new">+ Khởi tạo đợt mới...</option>
          </select>

          {canManageBatch && selectedBatchId !== 'new' && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setSelectedBatchId('new')}
              className="h-9 gap-1 text-xs font-medium"
            >
              <Plus className="size-3.5" />
              Đợt mới
            </Button>
          )}

          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={() => {
              if (selectedBatchId !== 'new') {
                loadBatchDetail(selectedBatchId);
              }
            }}
            disabled={isLoadingDetail}
            className="size-9 text-slate-500 hover:text-slate-800"
            title="Tải lại dữ liệu"
          >
            <RefreshCw className={`size-4 ${isLoadingDetail ? 'animate-spin' : ''}`} />
          </Button>
        </div>
      </div>

      {notice && (
        <div className="flex items-center gap-2.5 rounded-xl border border-emerald-200 bg-emerald-50/80 p-4 text-sm font-medium text-emerald-800 shadow-2xs">
          <CheckCircle2 className="size-5 shrink-0 text-emerald-600" />
          <span>{notice}</span>
        </div>
      )}

      {errorMessage && (
        <div className="flex items-center gap-2.5 rounded-xl border border-rose-200 bg-rose-50/80 p-4 text-sm font-medium text-rose-800 shadow-2xs">
          <AlertTriangle className="size-5 shrink-0 text-rose-600" />
          <span>{errorMessage}</span>
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2 space-y-6">
          <PeriodInfoCard
            academicYear={academicYear}
            semester={semester}
            batchName={batchName}
            startDate={startDate}
            canEdit={canEdit}
            onAcademicYearChange={setAcademicYear}
            onSemesterChange={setSemester}
            onBatchNameChange={setBatchName}
            onStartDateChange={handleStartDateChange}
          />

          <PeriodDeadlinesCard
            syllabusDeadline={syllabusDeadline}
            midtermDeadline={midtermDeadline}
            finalEvaluationDeadline={finalEvaluationDeadline}
            defenseDeadline={defenseDeadline}
            canEdit={canEdit}
            onSyllabusDeadlineChange={setSyllabusDeadline}
            onMidtermDeadlineChange={setMidtermDeadline}
            onFinalEvaluationDeadlineChange={setFinalEvaluationDeadline}
            onDefenseDeadlineChange={setDefenseDeadline}
            onOpenGateDetails={() => setIsGateDetailsOpen(true)}
          />
        </div>

        <div className="space-y-6">
          <PeriodSummaryCard
            academicYear={academicYear}
            semester={semester}
            batchName={batchName}
            status={currentBatch?.status || 'Draft'}
            totalWeeks={totalWeeks}
            canEdit={canEdit}
            onOpenBatch={handleOpenBatch}
            isOpenLoading={isOpenLoading}
          />
        </div>
      </div>

      <PeriodStagesCard
        computedStages={computedStages}
        canEdit={canEdit}
        onWeeksChange={handleStageWeeksChange}
        onResetDefaultWeeks={handleResetDefaultWeeks}
        totalWeeks={totalWeeks}
      />  

      <PeriodActionBar
        canEdit={canEdit}
        isSaving={isSaving}
        onSave={handleSaveClick}
        onImportUsers={() => setIsImportOpen(true)}
      />

      <PeriodReviewGateDialog
        open={isGateDetailsOpen}
        onClose={() => setIsGateDetailsOpen(false)}
        canEdit={canEdit}
        syllabusGate={syllabusGate}
        midtermGate={midtermGate}
        onSaveGates={handleSaveGates}
      />

      {isImportOpen && (
        <StudentImportDialog
          onClose={() => setIsImportOpen(false)}
          onImported={async (count: number) => {
            setIsImportOpen(false);
            setNotice(`Import thành công ${count} tài khoản sinh viên.`);
          }}
        />
      )}

      <ScoreConfigCloneReasonDialog
        open={isChangeReasonOpen}
        onClose={() => setIsChangeReasonOpen(false)}
        onConfirm={handleConfirmChangeReason}
      />
    </div>
  );
};
