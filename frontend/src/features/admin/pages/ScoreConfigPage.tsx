import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  type ScoreCalculationMethod,
  type ScoreComponentInput,
  type ScoreConfigurationDto,
  type KltnBatchSummaryDto,
  UIT_INTEGER_PRESET_COMPONENTS,
} from '../types/scoreConfig.types';
import { scoreConfigApi, getScoreConfigErrorMessage } from '../services/scoreConfigApi';
import { ScoreConfigGeneralCard } from '../components/ScoreConfigGeneralCard';
import { ScoreComponentTable } from '../components/ScoreComponentTable';
import { ScoreConfigValidationCard } from '../components/ScoreConfigValidationCard';
import { ScoreConfigSummaryCard } from '../components/ScoreConfigSummaryCard';
import { ScoreConfigActionBar } from '../components/ScoreConfigActionBar';
import { ScoreComponentAddDialog } from '../components/ScoreComponentAddDialog';
import { ScoreConfigHistoryDialog } from '../components/ScoreConfigHistoryDialog';
import { ScoreConfigCloneReasonDialog } from '../components/ScoreConfigCloneReasonDialog';
import { History, Wand2 } from 'lucide-react';
import { Button } from '@/components/ui/button';

function gcd(a: number, b: number): number {
  a = Math.abs(Math.round(a));
  b = Math.abs(Math.round(b));
  while (b) {
    const t = b;
    b = a % b;
    a = t;
  }
  return a;
}

function findArrayGcd(arr: number[]): number {
  return arr.reduce((acc, val) => gcd(acc, val), arr[0] || 1);
}

function convertWeightsToPercentage(items: ScoreComponentInput[]): ScoreComponentInput[] {
  const total = items.reduce((sum, c) => sum + (Number(c.weight) > 0 ? Number(c.weight) : 0), 0);
  if (total <= 0) return items;

  let accumulated = 0;
  return items.map((c, index) => {
    if (index === items.length - 1) {
      const lastWeight = Math.round((100 - accumulated) * 100) / 100;
      return { ...c, weight: Math.max(0, lastWeight) };
    }
    const pct = Math.round(((Number(c.weight) / total) * 100) * 100) / 100;
    accumulated += pct;
    return { ...c, weight: pct };
  });
}

function convertWeightsToInteger(items: ScoreComponentInput[]): ScoreComponentInput[] {
  const allInteger = items.every((c) => Number.isInteger(Number(c.weight)));
  if (allInteger) {
    const weights = items.map((c) => Math.round(Number(c.weight)));
    const commonDivisor = findArrayGcd(weights);
    if (commonDivisor > 1) {
      return items.map((c) => ({
        ...c,
        weight: Math.round(Number(c.weight) / commonDivisor),
      }));
    }
    return items;
  }

  const positiveWeights = items.map((c) => Number(c.weight)).filter((w) => w > 0);
  const minPositive = positiveWeights.length > 0 ? Math.min(...positiveWeights) : 1;

  if (minPositive > 0) {
    const scaled = items.map((c) => Math.round(Number(c.weight) / minPositive));
    const scaledDivisor = findArrayGcd(scaled);
    return items.map((c, i) => ({
      ...c,
      weight: Math.max(1, Math.round(scaled[i] / scaledDivisor)),
    }));
  }

  return items.map((c) => ({
    ...c,
    weight: Math.max(1, Math.round(Number(c.weight))),
  }));
}

export const ScoreConfigPage: React.FC = () => {
  const [batches, setBatches] = useState<KltnBatchSummaryDto[]>([]);
  const [selectedBatchId, setSelectedBatchId] = useState<string>('');
  const [isLoadingBatches, setIsLoadingBatches] = useState<boolean>(true);

  const [currentConfig, setCurrentConfig] = useState<ScoreConfigurationDto | null>(null);
  const [historyConfigs, setHistoryConfigs] = useState<ScoreConfigurationDto[]>([]);
  const [isLoadingConfig, setIsLoadingConfig] = useState<boolean>(false);

  const [calculationMethod, setCalculationMethod] =
    useState<ScoreCalculationMethod>('IntegerCoefficient');
  const [reviewerMinimumScore, setReviewerMinimumScore] = useState<number | null>(5.0);
  const [components, setComponents] = useState<ScoreComponentInput[]>(
    UIT_INTEGER_PRESET_COMPONENTS,
  );

  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [isPublishing, setIsPublishing] = useState<boolean>(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [isAddModalOpen, setIsAddModalOpen] = useState<boolean>(false);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState<boolean>(false);
  const [isCloneReasonModalOpen, setIsCloneReasonModalOpen] = useState<boolean>(false);

  useEffect(() => {
    let isMounted = true;
    const fetchBatches = async () => {
      setIsLoadingBatches(true);
      try {
        const result = await scoreConfigApi.getBatches({ pageSize: 50 });
        if (isMounted) {
          setBatches(result.items);
          if (result.items.length > 0) {
            setSelectedBatchId(result.items[0].id);
          }
        }
      } catch (err) {
        if (isMounted) {
          setErrorMessage(getScoreConfigErrorMessage(err));
        }
      } finally {
        if (isMounted) {
          setIsLoadingBatches(false);
        }
      }
    };

    fetchBatches();
    return () => {
      isMounted = false;
    };
  }, []);

  const loadBatchConfig = useCallback(async (batchId: string) => {
    if (!batchId) return;
    setIsLoadingConfig(true);
    setErrorMessage(null);
    try {
      const history = await scoreConfigApi.getConfigurationHistory(batchId);
      setHistoryConfigs(history);

      const draftConfig = history.find((c) => c.status === 'Draft');
      let targetConfig = draftConfig || null;

      if (!targetConfig) {
        targetConfig = await scoreConfigApi.getActiveConfiguration(batchId);
      }

      setCurrentConfig(targetConfig);

      if (targetConfig) {
        setCalculationMethod(targetConfig.calculationMethod);
        const mappedComponents: ScoreComponentInput[] = targetConfig.components.map((c) => ({
          componentCode: c.componentCode,
          componentName: c.componentName,
          source: c.source,
          applicability: c.applicability,
          weight: c.weight,
          isRequired: c.isRequired,
          minimumScore: c.minimumScore,
          requiresApproval: c.requiresApproval,
          orderIndex: c.orderIndex,
        }));
        setComponents(mappedComponents);

        const reviewerComp = mappedComponents.find((c) => c.source === 'Reviewer');
        setReviewerMinimumScore(reviewerComp?.minimumScore ?? null);
      } else {
        setCalculationMethod('IntegerCoefficient');
        setReviewerMinimumScore(5.0);
        setComponents(UIT_INTEGER_PRESET_COMPONENTS);
      }
    } catch (err) {
      setErrorMessage(getScoreConfigErrorMessage(err));
    } finally {
      setIsLoadingConfig(false);
    }
  }, []);

  useEffect(() => {
    if (selectedBatchId) {
      loadBatchConfig(selectedBatchId);
    }
  }, [selectedBatchId, loadBatchConfig]);

  const currentBatch = useMemo(
    () => batches.find((b) => b.id === selectedBatchId) || null,
    [batches, selectedBatchId],
  );

  const totalWeight = useMemo(() => {
    const sum = components.reduce((acc, c) => acc + (Number(c.weight) || 0), 0);
    return Math.round(sum * 100) / 100;
  }, [components]);

  const isValid = useMemo(() => {
    if (components.length === 0) return false;
    if (calculationMethod === 'Percentage') {
      return totalWeight === 100;
    }
    return totalWeight > 0;
  }, [calculationMethod, totalWeight, components.length]);

  const handleReviewerMinScoreChange = (score: number | null) => {
    setReviewerMinimumScore(score);
    setComponents((prev) =>
      prev.map((c) => {
        if (c.source === 'Reviewer') {
          return { ...c, minimumScore: score };
        }
        return c;
      }),
    );
  };

  const handleSwitchCalculationMethod = (newMethod: ScoreCalculationMethod) => {
    if (newMethod === calculationMethod) return;

    if (newMethod === 'Percentage') {
      const converted = convertWeightsToPercentage(components);
      setComponents(converted);
      setCalculationMethod('Percentage');
      setNotice('Đã chuyển sang phương thức Tỷ lệ phần trăm (quy đổi tự động theo tỷ lệ hệ số hiện có).');
    } else {
      const converted = convertWeightsToInteger(components);
      setComponents(converted);
      setCalculationMethod('IntegerCoefficient');
      setNotice('Đã chuyển sang phương thức Hệ số nguyên.');
    }
  };

  const handleApplyDefaultTemplate = () => {
    setCalculationMethod('IntegerCoefficient');
    setReviewerMinimumScore(5.0);
    setComponents(UIT_INTEGER_PRESET_COMPONENTS);
    setNotice('Đã nạp lại bộ hệ số mẫu chuẩn 5 thành viên của UIT.');
  };

  const handleAddOptionalComponent = (sourceCode: string) => {
    if (sourceCode === 'CUSTOM') {
      setIsAddModalOpen(true);
      return;
    }

    const presetMap: Record<string, Partial<ScoreComponentInput>> = {
      COUNCIL_CHAIRMAN: {
        componentCode: 'COUNCIL_CHAIRMAN',
        componentName: 'Chủ tịch hội đồng',
        source: 'CouncilChairman',
        weight: calculationMethod === 'Percentage' ? 15 : 1,
      },
      COUNCIL_SECRETARY: {
        componentCode: 'COUNCIL_SECRETARY',
        componentName: 'Thư ký hội đồng',
        source: 'CouncilSecretary',
        weight: calculationMethod === 'Percentage' ? 15 : 1,
      },
      COUNCIL_MEMBER: {
        componentCode: 'COUNCIL_MEMBER',
        componentName: 'Ủy viên hội đồng',
        source: 'CouncilMember',
        weight: calculationMethod === 'Percentage' ? 15 : 1,
        applicability: 'ReviewerOutsideCouncil',
      },
      COUNCIL_AVG: {
        componentCode: 'COUNCIL_AVG',
        componentName: 'Điểm trung bình hội đồng',
        source: 'CouncilAverage',
        weight: calculationMethod === 'Percentage' ? 30 : 2,
      },
    };

    const template = presetMap[sourceCode];
    if (!template) return;

    const alreadyExists = components.some((c) => c.componentCode === template.componentCode);
    if (alreadyExists) {
      setErrorMessage(`Đầu điểm "${template.componentName}" đã có trong danh sách.`);
      return;
    }

    const newComponent: ScoreComponentInput = {
      componentCode: template.componentCode!,
      componentName: template.componentName!,
      source: template.source!,
      applicability: template.applicability || 'Always',
      weight: template.weight || 1,
      isRequired: true,
      minimumScore: null,
      requiresApproval: false,
      orderIndex: components.length + 1,
    };

    setComponents((prev) => [...prev, newComponent]);
    setNotice(`Đã thêm đầu điểm "${template.componentName}".`);
  };

  const executeSaveDraft = async (changeReasonText?: string | null) => {
    if (!selectedBatchId) return;
    setIsSaving(true);
    setErrorMessage(null);
    setNotice(null);

    try {
      if (currentConfig && currentConfig.status === 'Draft') {
        await scoreConfigApi.updateDraft(selectedBatchId, currentConfig.id, {
          calculationMethod,
          components,
          changeReason: changeReasonText || currentConfig.changeReason || null,
        });
        setNotice('Đã cập nhật bản nháp cấu hình điểm thành công.');
      } else {
        await scoreConfigApi.createDraft(selectedBatchId, {
          calculationMethod,
          components,
          changeReason: changeReasonText || null,
        });
        setNotice('Đã tạo bản nháp cấu hình điểm thành công.');
      }

      await loadBatchConfig(selectedBatchId);
    } catch (err) {
      setErrorMessage(getScoreConfigErrorMessage(err));
    } finally {
      setIsSaving(false);
    }
  };

  const handleSaveClick = () => {
    if (currentConfig && currentConfig.status === 'Active') {
      setIsCloneReasonModalOpen(true);
      return;
    }

    executeSaveDraft(null);
  };

  const handlePublish = async () => {
    if (!selectedBatchId || !currentConfig || currentConfig.status !== 'Draft') return;
    if (!isValid) {
      setErrorMessage('Cấu hình chưa hợp lệ. Vui lòng kiểm tra lại tổng trọng số / hệ số.');
      return;
    }

    setIsPublishing(true);
    setErrorMessage(null);
    setNotice(null);

    try {
      await scoreConfigApi.publishConfiguration(selectedBatchId, currentConfig.id);
      setNotice('Đã công bố (Publish) cấu hình điểm thành công. Cấu hình hiện đang có hiệu lực áp dụng.');
      await loadBatchConfig(selectedBatchId);
    } catch (err) {
      setErrorMessage(getScoreConfigErrorMessage(err));
    } finally {
      setIsPublishing(false);
    }
  };

  return (
    <div className="space-y-6 pb-12 max-w-7xl mx-auto">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 className="text-xl sm:text-2xl font-bold text-slate-900 tracking-tight">
            Cấu hình điểm theo đợt
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 mt-1">
            Thiết lập phương thức tính điểm, trọng số và điều kiện áp dụng cho từng đầu điểm.
          </p>
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={handleApplyDefaultTemplate}
            className="rounded-lg h-9 text-xs"
            leftIcon={<Wand2 className="size-3.5 text-primary" />}
          >
            Cấu hình đợt mẫu
          </Button>

          {historyConfigs.length > 0 && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setIsHistoryModalOpen(true)}
              className="rounded-lg h-9 text-xs"
              leftIcon={<History className="size-3.5 text-slate-500" />}
            >
              Lịch sử phiên bản ({historyConfigs.length})
            </Button>
          )}
        </div>
      </div>

      {notice && (
        <div
          role="status"
          className="rounded-xl bg-emerald-50 border border-emerald-200 p-4 text-xs sm:text-sm text-emerald-800 flex items-center justify-between"
        >
          <span>{notice}</span>
          <button
            type="button"
            onClick={() => setNotice(null)}
            className="text-emerald-600 hover:text-emerald-900 font-bold ml-3 text-xs"
          >
            Đóng
          </button>
        </div>
      )}

      {errorMessage && (
        <div
          role="alert"
          className="rounded-xl bg-rose-50 border border-rose-200 p-4 text-xs sm:text-sm text-rose-800 flex items-center justify-between"
        >
          <span>{errorMessage}</span>
          <button
            type="button"
            onClick={() => setErrorMessage(null)}
            className="text-rose-600 hover:text-rose-900 font-bold ml-3 text-xs"
          >
            Đóng
          </button>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8 space-y-6">
          <ScoreConfigGeneralCard
            batches={batches}
            selectedBatchId={selectedBatchId}
            onBatchChange={setSelectedBatchId}
            calculationMethod={calculationMethod}
            onCalculationMethodChange={handleSwitchCalculationMethod}
            reviewerMinimumScore={reviewerMinimumScore}
            onReviewerMinimumScoreChange={handleReviewerMinScoreChange}
            onAddOptionalComponent={handleAddOptionalComponent}
            isReadOnly={isLoadingConfig || isLoadingBatches}
          />

          <ScoreComponentTable
            components={components}
            calculationMethod={calculationMethod}
            onChangeComponents={setComponents}
            isReadOnly={isLoadingConfig || isLoadingBatches}
          />

          <ScoreConfigValidationCard
            calculationMethod={calculationMethod}
            totalWeight={totalWeight}
            componentsCount={components.length}
          />

          <ScoreConfigActionBar
            onSave={handleSaveClick}
            isSaving={isSaving}
            onPublish={handlePublish}
            isPublishing={isPublishing}
            canPublish={currentConfig?.status === 'Draft' && isValid}
            calculationMethod={calculationMethod}
            onSwitchCalculationMethod={handleSwitchCalculationMethod}
            onOpenAddComponentModal={() => setIsAddModalOpen(true)}
            isReadOnly={isLoadingConfig || isLoadingBatches}
            saveButtonText={
              currentConfig?.status === 'Draft'
                ? 'Lưu bản nháp'
                : currentConfig?.status === 'Active'
                ? 'Sửa cấu hình (Tạo Draft)'
                : 'Lưu cấu hình'
            }
          />
        </div>

        <div className="lg:col-span-4 sticky top-6">
          <ScoreConfigSummaryCard
            batch={currentBatch}
            calculationMethod={calculationMethod}
            totalWeight={totalWeight}
            reviewerMinimumScore={reviewerMinimumScore}
            versionNo={currentConfig?.versionNo}
            status={currentConfig?.status}
          />
        </div>
      </div>

      <ScoreComponentAddDialog
        open={isAddModalOpen}
        onClose={() => setIsAddModalOpen(false)}
        calculationMethod={calculationMethod}
        existingCodes={components.map((c) => c.componentCode)}
        nextOrderIndex={components.length + 1}
        onAdd={(newComp) => {
          setComponents((prev) => [...prev, newComp]);
          setNotice(`Đã thêm đầu điểm "${newComp.componentName}".`);
        }}
      />

      <ScoreConfigHistoryDialog
        open={isHistoryModalOpen}
        onClose={() => setIsHistoryModalOpen(false)}
        history={historyConfigs}
        onSelectVersion={(selected) => {
          setCalculationMethod(selected.calculationMethod);
          const mappedComponents: ScoreComponentInput[] = selected.components.map((c) => ({
            componentCode: c.componentCode,
            componentName: c.componentName,
            source: c.source,
            applicability: c.applicability,
            weight: c.weight,
            isRequired: c.isRequired,
            minimumScore: c.minimumScore,
            requiresApproval: c.requiresApproval,
            orderIndex: c.orderIndex,
          }));
          setComponents(mappedComponents);
          const reviewerComp = mappedComponents.find((c) => c.source === 'Reviewer');
          setReviewerMinimumScore(reviewerComp?.minimumScore ?? null);
          setNotice(`Đang xem thông số phiên bản ${selected.versionNo} (${selected.status}).`);
        }}
      />

      <ScoreConfigCloneReasonDialog
        open={isCloneReasonModalOpen}
        onClose={() => setIsCloneReasonModalOpen(false)}
        isSubmitting={isSaving}
        onConfirm={(reason) => {
          setIsCloneReasonModalOpen(false);
          executeSaveDraft(reason);
        }}
      />
    </div>
  );
};