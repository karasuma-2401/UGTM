import { RichTextContent } from '@/components/ui/rich-text-content';

interface TopicReviewNoteProps {
  note?: string | null;
}

export function TopicReviewNote({ note }: TopicReviewNoteProps) {
  if (!note?.trim()) return null;
  return (
    <section
      aria-label="Góp ý thẩm định"
      className="rounded-xl border border-tone-warning-border bg-tone-warning-bg p-4"
    >
      <h3 className="text-sm font-semibold text-tone-warning-strong">Góp ý từ Khoa</h3>
      <RichTextContent
        content={note}
        className="mt-2 text-sm leading-6 text-ink-secondary"
      />
    </section>
  );
}
