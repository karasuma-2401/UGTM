import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '@/lib/utils';
import {
  checkPasswordCriteria,
  isStrongPassword,
  type PasswordRuleStatus,
} from '../utils/passwordValidation';

export type { PasswordRuleStatus };

export interface PasswordStrengthProps extends HTMLAttributes<HTMLDivElement> {
  password: string;
  strongLabel?: ReactNode;
  weakLabel?: ReactNode;
  showRules?: boolean;
  hideMessage?: boolean;
  validator?: (password: string) => boolean;
}

export function PasswordStrength({
  password,
  strongLabel,
  weakLabel,
  showRules = false,
  hideMessage = false,
  validator,
  className,
  ...props
}: PasswordStrengthProps) {
  const isStrong = validator ? validator(password) : isStrongPassword(password);
  const criteria = showRules ? checkPasswordCriteria(password) : [];

  return (
    <div className={cn('auth-strength', isStrong && 'is-strong', className)} {...props}>
      <div />
      {!hideMessage && (
        <p>
          {isStrong
            ? (strongLabel ?? '✓ Mật khẩu mạnh')
            : (weakLabel ??
              'Tối thiểu 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt; không chứa khoảng trắng.')}
        </p>
      )}
      {showRules && (
        <ul className="mt-2.5 grid grid-cols-1 sm:grid-cols-2 gap-1.5 text-[11px] leading-tight transition-all">
          {criteria.map((rule) => (
            <li
              key={rule.id}
              className={cn(
                'flex items-center gap-1.5 transition-colors',
                rule.passed ? 'text-emerald-600 font-medium' : 'text-slate-400',
              )}
            >
              <span
                className={cn(
                  'flex items-center justify-center w-3.5 h-3.5 rounded-full text-[9px] shrink-0',
                  rule.passed
                    ? 'bg-emerald-100 text-emerald-700 font-bold'
                    : 'bg-slate-100 text-slate-400',
                )}
              >
                {rule.passed ? '✓' : '•'}
              </span>
              <span>{rule.label}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
