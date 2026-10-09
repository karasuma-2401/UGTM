import { ArrowRight } from 'lucide-react';
import type { ReactNode } from 'react';

interface AuthSubmitButtonProps {
  children: ReactNode;
  disabled?: boolean;
}

export function AuthSubmitButton({ children, disabled }: AuthSubmitButtonProps) {
  return (
    <button className="auth-submit" type="submit" disabled={disabled}>
      {children}
      <ArrowRight size={16} />
    </button>
  );
}
