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
    <div className="bg-white border border-slate-200 rounded-xl shadow-xs p-4 sm:p-5 flex flex-wrap items-center justify-between gap-3">
      <div className="flex items-center gap-2 flex-wrap">
        <Button
          type="button"
          variant="outline"
          className={cn(
            'rounded-lg h-10 px-3.5 text-xs sm:text-sm font-medium transition-colors',
            calculationMethod === 'IntegerCoefficient'
              ? 'border-primary bg-primary/10 text-primary font-semibold'
              : 'text-slate-700 hover:bg-slate-50',
          )}
          onClick={() => onSwitchCalculationMethod('IntegerCoefficient')}
          disabled={isReadOnly}
          leftIcon={
            <Sliders
              className={cn(
                'size-4',
                calculationMethod === 'IntegerCoefficient'
                  ? 'text-primary'
                  : 'text-slate-500',
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
              ? 'border-primary bg-primary/10 text-primary font-semibold'
              : 'text-slate-700 hover:bg-slate-50',
          )}
          onClick={() => onSwitchCalculationMethod('Percentage')}
          disabled={isReadOnly}
          leftIcon={
            <Percent
              className={cn(
                'size-4',
                calculationMethod === 'Percentage'
                  ? 'text-primary'
                  : 'text-slate-500',
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
        className="rounded-lg h-10 px-3.5 text-xs sm:text-sm font-medium hover:bg-slate-50 text-slate-700"
        onClick={onOpenAddComponentModal}
        disabled={isReadOnly}
        leftIcon={<Plus className="size-4 text-slate-500" />}
      >
        Thêm đầu điểm
      </Button>
    </div>
  );
};