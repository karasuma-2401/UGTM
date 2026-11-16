import { Fragment } from 'react';
import { cn } from '@/lib/utils';
import type { TitleDiffSegment } from '../renameReview';

export function TitleDiffText({
  segments,
  variant,
  subtle = false,
}: {
  segments: TitleDiffSegment[];
  variant: 'old' | 'new';
  subtle?: boolean;
}) {
  return (
    <>
      {segments.map((segment, index) => {
        const prefix = index > 0 ? ' ' : '';
        if (segment.kind === 'removed' && variant === 'old') {
          return (
            <Fragment key={`${segment.kind}-${index}`}>
              {prefix}
              <mark
                className={cn(
                  'rounded px-1 py-0.5 line-through decoration-rose-400/75',
                  subtle
                    ? 'bg-tone-danger-bg text-tone-danger-text/85'
                    : 'bg-tone-danger-bg/95 text-tone-danger-text',
                )}
              >
                {segment.text}
              </mark>
            </Fragment>
          );
        }
        if (segment.kind === 'added' && variant === 'new') {
          return (
            <Fragment key={`${segment.kind}-${index}`}>
              {prefix}
              <mark
                className={cn(
                  'rounded px-1 py-0.5 font-semibold',
                  subtle
                    ? 'bg-tone-info-bg text-tone-info-text'
                    : 'bg-tone-info-bg/95 text-tone-info-strong',
                )}
              >
                {segment.text}
              </mark>
            </Fragment>
          );
        }
        return (
          <Fragment key={`${segment.kind}-${index}`}>
            {prefix}
            {segment.text}
          </Fragment>
        );
      })}
    </>
  );
}
