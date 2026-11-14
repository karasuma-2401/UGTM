import { Fragment, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

export interface RichTextContentProps {
  content?: string | null;
  className?: string;
  emptyFallback?: ReactNode;
}

type BlockNode =
  | { kind: 'heading'; text: string }
  | { kind: 'paragraph'; text: string }
  | { kind: 'blockquote'; text: string }
  | { kind: 'bulletList'; items: string[] }
  | { kind: 'orderedList'; items: string[] };

const SAFE_URL_PROTOCOLS = new Set(['http:', 'https:', 'mailto:']);

function getSafeHref(rawHref: string): string | null {
  const trimmed = rawHref.trim();
  if (!trimmed) return null;
  try {
    const parsed = new URL(trimmed, 'https://example.invalid');
    if (!SAFE_URL_PROTOCOLS.has(parsed.protocol)) return null;
    if (trimmed.startsWith('mailto:') || /^https?:\/\//i.test(trimmed)) return trimmed;
    return null;
  } catch {
    return null;
  }
}

const INLINE_TOKEN_PATTERN =
  /(\[([^\]\n]+)\]\(([^)\s]+)\)|`([^`\n]+)`|\*\*([^*\n]+)\*\*|<u>([\s\S]+?)<\/u>|~~([^~\n]+)~~|\*([^*\n]+)\*|(?<!\w)_([^_\n]+)_(?!\w))/;

/** Render inline Markdown and safe `<u>` spans strictly as React elements without HTML injection. */
function renderInline(text: string, keyPrefix: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  let remaining = text;
  let tokenIndex = 0;

  while (remaining.length > 0) {
    const match = INLINE_TOKEN_PATTERN.exec(remaining);
    if (!match || match.index === undefined) {
      nodes.push(remaining);
      break;
    }
    if (match.index > 0) {
      nodes.push(remaining.slice(0, match.index));
    }

    const key = `${keyPrefix}-${tokenIndex++}`;
    const [full, , linkLabel, linkHref, codeText, boldText, underText, strikeText, starItal, underItal] =
      match;

    if (linkLabel !== undefined && linkHref !== undefined) {
      const safeHref = getSafeHref(linkHref);
      if (safeHref) {
        nodes.push(
          <a key={key} href={safeHref} target="_blank" rel="noopener noreferrer">
            {renderInline(linkLabel, key)}
          </a>,
        );
      } else {
        nodes.push(full);
      }
    } else if (codeText !== undefined) {
      nodes.push(<code key={key}>{codeText}</code>);
    } else if (boldText !== undefined) {
      nodes.push(
        <strong key={key} className="font-semibold text-slate-900">
          {renderInline(boldText, key)}
        </strong>,
      );
    } else if (underText !== undefined) {
      nodes.push(
        <u key={key} className="underline underline-offset-2">
          {renderInline(underText, key)}
        </u>,
      );
    } else if (strikeText !== undefined) {
      nodes.push(
        <s key={key} className="text-slate-400">
          {renderInline(strikeText, key)}
        </s>,
      );
    } else if (starItal !== undefined || underItal !== undefined) {
      nodes.push(<em key={key}>{renderInline(starItal ?? underItal ?? '', key)}</em>);
    } else {
      nodes.push(full);
    }

    remaining = remaining.slice(match.index + full.length);
  }

  return nodes;
}

const BULLET_LINE_RE = /^\s*[-*•]\s+(.*)$/;
const ORDERED_LINE_RE = /^\s*\d+\.\s+(.*)$/;
const QUOTE_LINE_RE = /^\s*>\s?(.*)$/;
const HEADING_LINE_RE = /^\s*#{1,6}\s+(.*)$/;

/** Parse lightweight Markdown blocks while preserving plain multi-line text faithfully. */
function parseBlocks(raw: string): BlockNode[] {
  const normalized = raw.replace(/\r\n?/g, '\n');
  const lines = normalized.split('\n');
  const blocks: BlockNode[] = [];
  let index = 0;

  while (index < lines.length) {
    const line = lines[index] ?? '';
    if (!line.trim()) {
      index += 1;
      continue;
    }

    if (HEADING_LINE_RE.test(line)) {
      const match = HEADING_LINE_RE.exec(line);
      blocks.push({ kind: 'heading', text: match?.[1]?.trim() ?? line.trim() });
      index += 1;
      continue;
    }

    if (BULLET_LINE_RE.test(line)) {
      const items: string[] = [];
      while (index < lines.length && BULLET_LINE_RE.test(lines[index] ?? '')) {
        const match = BULLET_LINE_RE.exec(lines[index] ?? '');
        items.push(match?.[1] ?? '');
        index += 1;
      }
      blocks.push({ kind: 'bulletList', items });
      continue;
    }

    if (ORDERED_LINE_RE.test(line)) {
      const items: string[] = [];
      while (index < lines.length && ORDERED_LINE_RE.test(lines[index] ?? '')) {
        const match = ORDERED_LINE_RE.exec(lines[index] ?? '');
        items.push(match?.[1] ?? '');
        index += 1;
      }
      blocks.push({ kind: 'orderedList', items });
      continue;
    }

    if (QUOTE_LINE_RE.test(line)) {
      const quoteLines: string[] = [];
      while (index < lines.length && QUOTE_LINE_RE.test(lines[index] ?? '')) {
        const match = QUOTE_LINE_RE.exec(lines[index] ?? '');
        quoteLines.push(match?.[1] ?? '');
        index += 1;
      }
      blocks.push({ kind: 'blockquote', text: quoteLines.join('\n') });
      continue;
    }

    const paragraphLines: string[] = [];
    while (index < lines.length) {
      const current = lines[index] ?? '';
      if (
        !current.trim() ||
        HEADING_LINE_RE.test(current) ||
        BULLET_LINE_RE.test(current) ||
        ORDERED_LINE_RE.test(current) ||
        QUOTE_LINE_RE.test(current)
      ) {
        break;
      }
      paragraphLines.push(current);
      index += 1;
    }
    blocks.push({ kind: 'paragraph', text: paragraphLines.join('\n') });
  }

  return blocks;
}

/** Convert rich-text Markdown/underline formatting into clean plain text for tooltips and summaries. */
export function stripRichText(content?: string | null): string {
  if (!content?.trim()) return '';
  return content
    .replace(/\r\n?/g, '\n')
    .replace(/\[([^\]\n]+)\]\(([^)\s]+)\)/g, '$1')
    .replace(/<u>([\s\S]+?)<\/u>/g, '$1')
    .replace(/\*\*([^*\n]+)\*\*/g, '$1')
    .replace(/~~([^~\n]+)~~/g, '$1')
    .replace(/`([^`\n]+)`/g, '$1')
    .replace(/\*([^*\n]+)\*/g, '$1')
    .replace(/(?<!\w)_([^_\n]+)_(?!\w)/g, '$1')
    .replace(/^\s*#{1,6}\s+/gm, '')
    .replace(/^\s*>\s?/gm, '')
    .replace(/^\s*[-*]\s+/gm, '• ')
    .trim();
}

export function RichTextContent({ content, className, emptyFallback = null }: RichTextContentProps) {
  if (!content?.trim()) return <>{emptyFallback}</>;
  const blocks = parseBlocks(content);
  if (blocks.length === 0) return <>{emptyFallback}</>;

  return (
    <div className={cn('rich-text-prose break-words', className)}>
      {blocks.map((block, index) => {
        const key = `block-${index}`;
        if (block.kind === 'heading') {
          return (
            <p key={key} className="font-semibold text-slate-900">
              {renderInline(block.text, key)}
            </p>
          );
        }
        if (block.kind === 'blockquote') {
          return (
            <blockquote key={key} className="whitespace-pre-wrap">
              {renderInline(block.text, key)}
            </blockquote>
          );
        }
        if (block.kind === 'bulletList') {
          return (
            <ul key={key}>
              {block.items.map((item, itemIndex) => (
                <li key={`${key}-li-${itemIndex}`}>
                  {renderInline(item, `${key}-li-${itemIndex}`)}
                </li>
              ))}
            </ul>
          );
        }
        if (block.kind === 'orderedList') {
          return (
            <ol key={key}>
              {block.items.map((item, itemIndex) => (
                <li key={`${key}-li-${itemIndex}`}>
                  {renderInline(item, `${key}-li-${itemIndex}`)}
                </li>
              ))}
            </ol>
          );
        }
        return (
          <p key={key} className="whitespace-pre-wrap">
            {block.text.split('\n').map((line, lineIndex, allLines) => (
              <Fragment key={`${key}-line-${lineIndex}`}>
                {renderInline(line, `${key}-line-${lineIndex}`)}
                {lineIndex < allLines.length - 1 && '\n'}
              </Fragment>
            ))}
          </p>
        );
      })}
    </div>
  );
}
