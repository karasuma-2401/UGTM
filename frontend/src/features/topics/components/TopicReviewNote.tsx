interface TopicReviewNoteProps {
  note?: string | null;
}

export function TopicReviewNote({ note }: TopicReviewNoteProps) {
  if (!note?.trim()) return null;
  return (
    <section
      aria-label="Góp ý thẩm định"
      className="rounded-xl border border-amber-200 bg-amber-50/60 p-4"
    >
      <h3 className="text-sm font-semibold text-amber-900">Góp ý từ Khoa</h3>
      <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-6 text-slate-700">
        {note}
      </p>
    </section>
  );
}
