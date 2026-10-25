import { useState, useMemo, useCallback } from 'react';
import {
  type ScoreCalculationMethod,
  type ScoreComponentInput,
  type ScoreConfigurationDto,
  UIT_INTEGER_PRESET_COMPONENTS,
} from '../types/scoreConfig.types';
import {
  convertWeightsToPercentage,
  convertWeightsToInteger,
  OPTIONAL_COMPONENT_PRESETS,
} from '../utils/scoreWeights';

export function useScoreConfigForm(
  onNotice: (msg: string) => void,
  onError: (msg: string) => void,
  onOpenCustomModal: () => void,
) {
  const [calculationMethod, setCalculationMethod] =
    useState<ScoreCalculationMethod>('IntegerCoefficient');
  const [reviewerMinimumScore, setReviewerMinimumScore] = useState<number | null>(5.0);
  const [components, setComponents] = useState<ScoreComponentInput[]>(
    UIT_INTEGER_PRESET_COMPONENTS,
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

  const populateFromConfig = useCallback((targetConfig: ScoreConfigurationDto | null) => {
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
  }, []);

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
      onNotice('Đã chuyển sang phương thức Tỷ lệ phần trăm (quy đổi tự động theo tỷ lệ hệ số hiện có).');
    } else {
      const converted = convertWeightsToInteger(components);
      setComponents(converted);
      setCalculationMethod('IntegerCoefficient');
      onNotice('Đã chuyển sang phương thức Hệ số nguyên.');
    }
  };

  const applyDefaultTemplate = () => {
    setCalculationMethod('IntegerCoefficient');
    setReviewerMinimumScore(5.0);
    setComponents(UIT_INTEGER_PRESET_COMPONENTS);
    onNotice('Đã nạp lại bộ hệ số mẫu chuẩn 5 thành viên của UIT.');
  };

  const handleAddOptionalComponent = (sourceCode: string) => {
    if (sourceCode === 'CUSTOM') {
      onOpenCustomModal();
      return;
    }

    const template = OPTIONAL_COMPONENT_PRESETS[sourceCode];
    if (!template) return;

    const alreadyExists = components.some((c) => c.componentCode === template.componentCode);
    if (alreadyExists) {
      onError(`Đầu điểm "${template.componentName}" đã có trong danh sách.`);
      return;
    }

    const weightVal = calculationMethod === 'Percentage'
      ? (sourceCode === 'COUNCIL_AVG' ? 30 : 15)
      : (template.weight || 1);

    const newComponent: ScoreComponentInput = {
      componentCode: template.componentCode!,
      componentName: template.componentName!,
      source: template.source!,
      applicability: template.applicability || 'Always',
      weight: weightVal,
      isRequired: true,
      minimumScore: null,
      requiresApproval: false,
      orderIndex: components.length + 1,
    };

    setComponents((prev) => [...prev, newComponent]);
    onNotice(`Đã thêm đầu điểm "${template.componentName}".`);
  };

  return {
    calculationMethod,
    setCalculationMethod,
    reviewerMinimumScore,
    setReviewerMinimumScore,
    components,
    setComponents,
    totalWeight,
    isValid,
    populateFromConfig,
    handleReviewerMinScoreChange,
    handleSwitchCalculationMethod,
    applyDefaultTemplate,
    handleAddOptionalComponent,
  };
}
