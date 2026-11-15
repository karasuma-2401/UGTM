import { ArrowLeft } from 'lucide-react';

export interface GradingBackLinkProps {
  label?: string;
  onClick: () => void;
}

/** Top-left back link displayed above PageHeader when inside a grading sheet. */
export function GradingBackLink({
  label = 'Quay lại danh sách nhóm',
  onClick,
}: GradingBackLinkProps) {
  return (
    <div className="-mb-2">
      <button
        type="button"
        onClick={onClick}
        className="inline-flex cursor-pointer items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-sm font-medium text-slate-600 transition hover:bg-white hover:text-slate-900 hover:shadow-2xs hover:ring-1 hover:ring-slate-200/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500/40"
      >
        <ArrowLeft className="size-4" aria-hidden="true" />
        <span>{label}</span>
      </button>
    </div>
  );
}
