import {
  type ScoreCalculationMethod,
  type ScoreComponentInput,
  UIT_INTEGER_PRESET_COMPONENTS,
} from '../types/scoreConfig.types';

export function gcd(a: number, b: number): number {
  a = Math.abs(Math.round(a));
  b = Math.abs(Math.round(b));
  while (b) {
    const t = b;
    b = a % b;
    a = t;
  }
  return a;
}

export function findArrayGcd(arr: number[]): number {
  return arr.reduce((acc, val) => gcd(acc, val), arr[0] || 1);
}

export function convertWeightsToPercentage(items: ScoreComponentInput[]): ScoreComponentInput[] {
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

export function convertWeightsToInteger(items: ScoreComponentInput[]): ScoreComponentInput[] {
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

export function isConfigDifferentFromDefault(
  method: ScoreCalculationMethod,
  reviewerMinScore: number | null,
  currentComponents: ScoreComponentInput[],
): boolean {
  if (method !== 'IntegerCoefficient') return true;
  if (reviewerMinScore !== 5.0) return true;
  if (currentComponents.length !== UIT_INTEGER_PRESET_COMPONENTS.length) return true;

  return currentComponents.some((comp, idx) => {
    const preset = UIT_INTEGER_PRESET_COMPONENTS[idx];
    if (!preset) return true;
    return (
      comp.componentCode !== preset.componentCode ||
      Number(comp.weight) !== Number(preset.weight) ||
      comp.minimumScore !== preset.minimumScore ||
      comp.applicability !== preset.applicability
    );
  });
}

export const OPTIONAL_COMPONENT_PRESETS: Record<string, Partial<ScoreComponentInput>> = {
  COUNCIL_CHAIRMAN: {
    componentCode: 'COUNCIL_CHAIRMAN',
    componentName: 'Chủ tịch hội đồng',
    source: 'CouncilChairman',
    weight: 1,
  },
  COUNCIL_SECRETARY: {
    componentCode: 'COUNCIL_SECRETARY',
    componentName: 'Thư ký hội đồng',
    source: 'CouncilSecretary',
    weight: 1,
  },
  COUNCIL_MEMBER: {
    componentCode: 'COUNCIL_MEMBER',
    componentName: 'Ủy viên hội đồng',
    source: 'CouncilMember',
    weight: 1,
    applicability: 'ReviewerOutsideCouncil',
  },
  COUNCIL_AVG: {
    componentCode: 'COUNCIL_AVG',
    componentName: 'Điểm trung bình hội đồng',
    source: 'CouncilAverage',
    weight: 2,
  },
};