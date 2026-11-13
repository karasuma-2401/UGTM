import { useEffect, useId, useRef } from 'react';
import { InlineAlert } from './inline-alert';

export interface FormFieldError {
  fieldId: string;
  message: string;
}

export function FormErrorSummary({
  errors,
  submissionKey,
}: {
  errors: FormFieldError[];
  submissionKey: number;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const lastSubmission = useRef(0);
  useEffect(() => {
    if (submissionKey > lastSubmission.current && errors.length) ref.current?.focus();
    lastSubmission.current = submissionKey;
  }, [submissionKey, errors.length]);
  if (!errors.length) return null;
  return (
    <div
      ref={ref}
      tabIndex={-1}
      aria-labelledby={titleId}
      className="rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-600 focus-visible:ring-offset-2"
    >
      <InlineAlert title="Vui lòng kiểm tra lại thông tin">
        <span id={titleId} className="sr-only">
          Có {errors.length} trường cần kiểm tra
        </span>
        <ul className="list-disc space-y-1 pl-4">
          {errors.map(({ fieldId, message }) => (
            <li key={fieldId}>
              <a
                href={`#${fieldId}`}
                className="underline underline-offset-4"
                onClick={(event) => {
                  event.preventDefault();
                  document.getElementById(fieldId)?.focus();
                }}
              >
                {message}
              </a>
            </li>
          ))}
        </ul>
      </InlineAlert>
    </div>
  );
}
