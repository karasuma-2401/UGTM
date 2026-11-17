import React from 'react';
import { Button } from '@/components/ui/button';
import { Plus, Sliders, Percent } from 'lucide-react';
import type { ScoreCalculationMethod } from '../../types/scoreConfig.types';
import { cn } from '@/lib/utils';

export interface ScoreConfigActionBarProps {
  calculationMethod: ScoreCalculationMethod;
  onSwitchCalculationMethod: (method: ScoreCalculationMethod) => void;
  onOpenAddComponentModal: () => void;
  isReadOnly?: boolean;
}

export const ScoreConfigActionBar: React.FC<ScoreConfigActionBarProps> = ({
  calculationMethod,
  onSwitchCalculationMethod,
  onOpenAddComponentModal,
  isReadOnly = false,
}) => {
  return (
    <div className="bg-surface-card border border-line rounded-xl shadow-xs p-4 sm:p-5 flex flex-wrap items-center justify-between gap-3">
      <div className="flex items-center gap-2 flex-wrap">
        <Button
          type="button"
          variant="outline"
          className={cn(
            'rounded-lg h-10 px-3.5 text-xs sm:text-sm font-medium transition-colors',
            calculationMethod === 'IntegerCoefficient'
              ? 'border-primary bg-primary/10 text-primary font-semibold dark:bg-primary/15'
              : 'text-ink-secondary hover:bg-surface-hover',
          )}
          onClick={() => onSwitchCalculationMethod('IntegerCoefficient')}
          disabled={isReadOnly}
          leftIcon={
            <Sliders
              className={cn(
                'size-4',
                calculationMethod === 'IntegerCoefficient'
                  ? 'text-primary'
                  : 'text-ink-muted',
              )}
            />
          }
        >
          Hệ số nguyên
        </Button>

        <Button
          type="button"
          variant="outline"
          className={cn(
            'rounded-lg h-10 px-3.5 text-xs sm:text-sm font-medium transition-colors',
            calculationMethod === 'Percentage'
              ? 'border-primary bg-primary/10 text-primary font-semibold dark:bg-primary/15'
              : 'text-ink-secondary hover:bg-surface-hover',
          )}
          onClick={() => onSwitchCalculationMethod('Percentage')}
          disabled={isReadOnly}
          leftIcon={
            <Percent
              className={cn(
                'size-4',
                calculationMethod === 'Percentage'
                  ? 'text-primary'
                  : 'text-ink-muted',
              )}
            />
          }
        >
          Tỷ lệ phần trăm
        </Button>
      </div>

      <Button
        type="button"
        variant="outline"
        className="rounded-lg h-10 px-3.5 text-xs sm:text-sm font-medium hover:bg-surface-hover text-ink-secondary"
        onClick={onOpenAddComponentModal}
        disabled={isReadOnly}
        leftIcon={<Plus className="size-4 text-ink-muted" />}
      >
        Thêm đầu điểm
      </Button>
    </div>
  );
};