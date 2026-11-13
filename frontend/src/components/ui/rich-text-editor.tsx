import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { EditorContent, useEditor, useEditorState } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { CharacterCount, Placeholder } from '@tiptap/extensions';
import { Markdown } from '@tiptap/markdown';
import {
  Bold,
  Italic,
  Link2,
  List,
  ListOrdered,
  Maximize2,
  Minimize2,
  Quote,
  Redo2,
  RemoveFormatting,
  Underline as UnderlineIcon,
  Undo2,
  Unlink,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from './button';
import { Dialog } from './dialog';

export interface RichTextEditorProps {
  id?: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  readOnly?: boolean;
  required?: boolean;
  invalid?: boolean;
  maxLength?: number;
  ariaLabel?: string;
  ariaLabelledBy?: string;
  ariaDescribedBy?: string;
  expandable?: boolean;
  expandedTitle?: string;
  expandedSubtitle?: string;
  toolbarExtra?: ReactNode;
  minHeightClassName?: string;
  className?: string;
  editorClassName?: string;
}

interface ToolbarButtonProps {
  active?: boolean;
  disabled?: boolean;
  label: string;
  title?: string;
  onClick: () => void;
  children: ReactNode;
}

function ToolbarButton({
  active = false,
  disabled = false,
  label,
  title,
  onClick,
  children,
}: ToolbarButtonProps) {
  return (
    <button
      type="button"
      aria-label={label}
      aria-pressed={active}
      title={title ?? label}
      disabled={disabled}
      onMouseDown={(event) => {
        // Keep current ProseMirror selection intact when toggling marks.
        event.preventDefault();
      }}
      onClick={onClick}
      className={cn(
        'inline-flex size-7 items-center justify-center rounded-md text-slate-600 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 disabled:cursor-not-allowed disabled:opacity-40',
        active
          ? 'bg-blue-50 text-primary ring-1 ring-inset ring-blue-200/80'
          : 'hover:bg-slate-100 hover:text-slate-900',
      )}
    >
      {children}
    </button>
  );
}

/** Normalize TipTap markdown output so empty documents emit an empty string. */
export function normalizeRichTextMarkdown(markdown: string, isEmpty: boolean): string {
  if (isEmpty) return '';
  return markdown.replace(/\u00a0/g, ' ').replace(/\n+$/g, '');
}

/** Validate and normalize a user-entered link URL, blocking unsafe schemes such as javascript:. */
export function normalizeSafeLinkUrl(raw: string): string | null {
  const trimmed = raw.trim();
  if (!trimmed) return null;
  const candidate =
    /^[a-zA-Z][a-zA-Z\d+\-.]*:/.test(trimmed) || trimmed.startsWith('/')
      ? trimmed
      : `https://${trimmed}`;
  try {
    const parsed = new URL(candidate, 'https://example.invalid');
    if (!['http:', 'https:', 'mailto:'].includes(parsed.protocol)) return null;
    if (candidate.startsWith('mailto:') || /^https?:\/\//i.test(candidate)) return candidate;
    return null;
  } catch {
    return null;
  }
}

export function RichTextEditor({
  id,
  value,
  onChange,
  placeholder = 'Nhập nội dung...',
  disabled = false,
  readOnly = false,
  required = false,
  invalid = false,
  maxLength,
  ariaLabel,
  ariaLabelledBy,
  ariaDescribedBy,
  expandable = true,
  expandedTitle = 'Trình soạn thảo chi tiết',
  expandedSubtitle,
  toolbarExtra,
  minHeightClassName = 'min-h-[168px]',
  className,
  editorClassName,
}: RichTextEditorProps) {
  const generatedId = useId();
  const editorId = id ?? generatedId;
  const isLocked = disabled || readOnly;

  const [expanded, setExpanded] = useState(false);
  const [linkBarOpen, setLinkBarOpen] = useState(false);
  const [linkDraft, setLinkDraft] = useState('');
  const [linkError, setLinkError] = useState('');

  const placeholderRef = useRef(placeholder);
  placeholderRef.current = placeholder;
  const lastSerializedRef = useRef(value);
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  const editor = useEditor(
    {
      extensions: [
        StarterKit.configure({
          heading: false,
          codeBlock: false,
          horizontalRule: false,
          link: {
            openOnClick: false,
            autolink: true,
            linkOnPaste: true,
            HTMLAttributes: {
              rel: 'noopener noreferrer nofollow',
              target: '_blank',
            },
          },
        }),
        Markdown,
        Placeholder.configure({
          placeholder: () => placeholderRef.current,
        }),
        CharacterCount.configure({
          limit: maxLength ?? null,
        }),
      ],
      content: value || '',
      contentType: 'markdown',
      editable: !isLocked,
      editorProps: {
        attributes: {
          id: editorId,
          role: 'textbox',
          'aria-multiline': 'true',
          class: cn('rich-text-prose outline-none', editorClassName),
        },
      },
      onUpdate: ({ editor: currentEditor }) => {
        const next = normalizeRichTextMarkdown(currentEditor.getMarkdown(), currentEditor.isEmpty);
        lastSerializedRef.current = next;
        onChangeRef.current(next);
      },
    },
    [],
  );

  useEffect(() => {
    if (!editor) return;
    editor.setEditable(!isLocked);
  }, [editor, isLocked]);

  useEffect(() => {
    if (!editor) return;
    editor.setOptions({
      editorProps: {
        attributes: {
          id: editorId,
          role: 'textbox',
          'aria-multiline': 'true',
          ...(ariaLabel ? { 'aria-label': ariaLabel } : {}),
          ...(ariaLabelledBy ? { 'aria-labelledby': ariaLabelledBy } : {}),
          ...(ariaDescribedBy ? { 'aria-describedby': ariaDescribedBy } : {}),
          ...(required ? { 'aria-required': 'true' } : {}),
          ...(invalid ? { 'aria-invalid': 'true' } : {}),
          ...(disabled ? { 'aria-disabled': 'true' } : {}),
          class: cn('rich-text-prose outline-none', editorClassName),
        },
      },
    });
  }, [
    editor,
    editorId,
    ariaLabel,
    ariaLabelledBy,
    ariaDescribedBy,
    required,
    invalid,
    disabled,
    editorClassName,
  ]);

  useEffect(() => {
    if (!editor) return;
    if (value === lastSerializedRef.current) return;
    lastSerializedRef.current = value;
    editor.commands.setContent(value || '', { emitUpdate: false, contentType: 'markdown' });
  }, [editor, value]);

  useEffect(() => {
    if (!editor) return;
    const frame = requestAnimationFrame(() => {
      if (expanded) editor.commands.focus();
    });
    return () => cancelAnimationFrame(frame);
  }, [editor, expanded]);

  const editorState = useEditorState({
    editor,
    selector: ({ editor: current }) => {
      if (!current) {
        return {
          bold: false,
          italic: false,
          underline: false,
          bulletList: false,
          orderedList: false,
          blockquote: false,
          link: false,
          canUndo: false,
          canRedo: false,
          words: 0,
        };
      }
      return {
        bold: current.isActive('bold'),
        italic: current.isActive('italic'),
        underline: current.isActive('underline'),
        bulletList: current.isActive('bulletList'),
        orderedList: current.isActive('orderedList'),
        blockquote: current.isActive('blockquote'),
        link: current.isActive('link'),
        canUndo: current.can().undo(),
        canRedo: current.can().redo(),
        words: current.storage.characterCount.words(),
      };
    },
  });

  const serializedLength = value.length;
  const isOverLimit = typeof maxLength === 'number' && serializedLength > maxLength;
  const isNearLimit =
    typeof maxLength === 'number' && !isOverLimit && serializedLength >= Math.floor(maxLength * 0.9);

  function openLinkEditor() {
    if (!editor || isLocked) return;
    const currentHref = String(editor.getAttributes('link').href ?? '');
    setLinkDraft(currentHref);
    setLinkError('');
    setLinkBarOpen((open) => !open);
  }

  function applyLink() {
    if (!editor || isLocked) return;
    const trimmed = linkDraft.trim();
    if (!trimmed) {
      editor.chain().focus().extendMarkRange('link').unsetLink().run();
      setLinkBarOpen(false);
      setLinkError('');
      return;
    }
    const safeUrl = normalizeSafeLinkUrl(trimmed);
    if (!safeUrl) {
      setLinkError('Liên kết không hợp lệ (chỉ hỗ trợ http, https hoặc mailto).');
      return;
    }
    editor.chain().focus().extendMarkRange('link').setLink({ href: safeUrl }).run();
    setLinkBarOpen(false);
    setLinkError('');
  }

  function renderSurface(isExpandedView: boolean) {
    return (
      <div
        className={cn(
          'flex min-h-0 flex-col overflow-hidden rounded-xl border bg-white shadow-2xs transition-colors',
          !isExpandedView && 'focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/20',
          invalid || isOverLimit
            ? 'border-rose-400 focus-within:border-rose-500 focus-within:ring-rose-500/20'
            : 'border-slate-200',
          disabled && 'bg-slate-100/80 opacity-70',
          isExpandedView ? 'min-h-0 flex-1' : cn(minHeightClassName, className),
        )}
      >
        <div
          role="toolbar"
          aria-label="Thanh công cụ định dạng"
          className="flex shrink-0 flex-wrap items-center gap-1 border-b border-slate-100 bg-slate-50/80 px-2.5 py-1.5"
        >
          <div className="flex items-center gap-0.5">
            <ToolbarButton
              active={Boolean(editorState?.bold)}
              disabled={isLocked}
              label="In đậm"
              title="In đậm (Ctrl+B)"
              onClick={() => editor?.chain().focus().toggleBold().run()}
            >
              <Bold className="size-3.5" strokeWidth={2.4} />
            </ToolbarButton>
            <ToolbarButton
              active={Boolean(editorState?.italic)}
              disabled={isLocked}
              label="In nghiêng"
              title="In nghiêng (Ctrl+I)"
              onClick={() => editor?.chain().focus().toggleItalic().run()}
            >
              <Italic className="size-3.5" />
            </ToolbarButton>
            <ToolbarButton
              active={Boolean(editorState?.underline)}
              disabled={isLocked}
              label="Gạch chân"
              title="Gạch chân (Ctrl+U)"
              onClick={() => editor?.chain().focus().toggleUnderline().run()}
            >
              <UnderlineIcon className="size-3.5" />
            </ToolbarButton>
          </div>

          <span className="mx-1 h-4 w-px bg-slate-200" aria-hidden="true" />

          <div className="flex items-center gap-0.5">
            <ToolbarButton
              active={Boolean(editorState?.bulletList)}
              disabled={isLocked}
              label="Danh sách gạch đầu dòng"
              title="Danh sách gạch đầu dòng"
              onClick={() => editor?.chain().focus().toggleBulletList().run()}
            >
              <List className="size-3.5" />
            </ToolbarButton>
            <ToolbarButton
              active={Boolean(editorState?.orderedList)}
              disabled={isLocked}
              label="Danh sách đánh số"
              title="Danh sách đánh số"
              onClick={() => editor?.chain().focus().toggleOrderedList().run()}
            >
              <ListOrdered className="size-3.5" />
            </ToolbarButton>
            <ToolbarButton
              active={Boolean(editorState?.blockquote)}
              disabled={isLocked}
              label="Trích dẫn"
              title="Trích dẫn"
              onClick={() => editor?.chain().focus().toggleBlockquote().run()}
            >
              <Quote className="size-3.5" />
            </ToolbarButton>
          </div>

          <span className="mx-1 h-4 w-px bg-slate-200" aria-hidden="true" />

          <div className="flex items-center gap-0.5">
            <ToolbarButton
              active={Boolean(editorState?.link) || linkBarOpen}
              disabled={isLocked}
              label="Chèn liên kết"
              title="Chèn hoặc sửa liên kết"
              onClick={openLinkEditor}
            >
              <Link2 className="size-3.5" />
            </ToolbarButton>
            <ToolbarButton
              disabled={isLocked}
              label="Xóa định dạng"
              title="Xóa định dạng vùng chọn"
              onClick={() => editor?.chain().focus().unsetAllMarks().clearNodes().run()}
            >
              <RemoveFormatting className="size-3.5" />
            </ToolbarButton>
          </div>

          <span className="mx-1 hidden h-4 w-px bg-slate-200 xl:inline-block" aria-hidden="true" />

          <div className="hidden items-center gap-0.5 xl:flex">
            <ToolbarButton
              disabled={isLocked || !editorState?.canUndo}
              label="Hoàn tác"
              title="Hoàn tác (Ctrl+Z)"
              onClick={() => editor?.chain().focus().undo().run()}
            >
              <Undo2 className="size-3.5" />
            </ToolbarButton>
            <ToolbarButton
              disabled={isLocked || !editorState?.canRedo}
              label="Làm lại"
              title="Làm lại (Ctrl+Y)"
              onClick={() => editor?.chain().focus().redo().run()}
            >
              <Redo2 className="size-3.5" />
            </ToolbarButton>
          </div>

          {(toolbarExtra || (expandable && !isExpandedView)) && (
            <div className="ml-auto flex items-center gap-1.5">
              {toolbarExtra}
              {expandable && !isExpandedView && (
                <ToolbarButton
                  disabled={disabled}
                  label="Mở rộng trình soạn thảo"
                  title="Mở rộng toàn khung"
                  onClick={() => setExpanded(true)}
                >
                  <Maximize2 className="size-3.5" />
                </ToolbarButton>
              )}
            </div>
          )}
        </div>

        {linkBarOpen && (
          <div className="shrink-0 space-y-1.5 border-b border-slate-100 bg-blue-50/40 px-3 py-2">
            <div className="flex flex-wrap items-center gap-2">
              <input
                type="url"
                value={linkDraft}
                autoFocus
                placeholder="https://example.com"
                onChange={(event) => {
                  setLinkDraft(event.target.value);
                  if (linkError) setLinkError('');
                }}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    applyLink();
                  } else if (event.key === 'Escape') {
                    event.preventDefault();
                    event.stopPropagation();
                    setLinkBarOpen(false);
                    editor?.commands.focus();
                  }
                }}
                className="h-7 min-w-0 flex-1 rounded-lg border border-slate-200 bg-white px-2.5 text-xs text-slate-800 placeholder:text-slate-400 focus-visible:border-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20"
              />
              <Button
                type="button"
                size="sm"
                onClick={applyLink}
                className="h-7 rounded-lg px-2.5 text-xs"
              >
                Áp dụng
              </Button>
              {editorState?.link && (
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  aria-label="Gỡ liên kết"
                  onClick={() => {
                    editor?.chain().focus().extendMarkRange('link').unsetLink().run();
                    setLinkBarOpen(false);
                  }}
                  className="h-7 rounded-lg px-2 text-xs text-rose-600 hover:bg-rose-50 hover:text-rose-700"
                >
                  <Unlink className="size-3.5" />
                </Button>
              )}
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => {
                  setLinkBarOpen(false);
                  editor?.commands.focus();
                }}
                className="h-7 rounded-lg px-2 text-xs text-slate-500"
              >
                Hủy
              </Button>
            </div>
            {linkError && <p className="text-[11px] font-medium text-rose-700">{linkError}</p>}
          </div>
        )}

        <div
          onClick={(event) => {
            if (isLocked || !editor) return;
            if ((event.target as HTMLElement).closest('.ProseMirror')) return;
            editor.commands.focus();
          }}
          className={cn(
            'rich-text-editor-content min-h-0 flex-1 cursor-text overflow-y-auto px-3.5 py-3',
            isExpandedView ? 'px-5 py-4 sm:px-6' : 'min-h-[140px]',
          )}
        >
          <EditorContent editor={editor} className="h-full min-h-full" />
        </div>

        <div className="flex shrink-0 items-center justify-between gap-2 border-t border-slate-100 bg-slate-50/50 px-3 py-1.5 text-[11px] text-slate-400">
          <span className="truncate">
            {isExpandedView
              ? 'Chế độ soạn thảo mở rộng · Hỗ trợ Ctrl+B, Ctrl+I, Ctrl+U'
              : 'Hỗ trợ định dạng văn bản'}
          </span>
          <span
            className={cn(
              'shrink-0 font-medium tabular-nums',
              isOverLimit
                ? 'text-rose-600'
                : isNearLimit
                  ? 'text-amber-600'
                  : 'text-slate-500',
            )}
          >
            {typeof maxLength === 'number'
              ? `${serializedLength}/${maxLength}`
              : `${editorState?.words ?? 0} từ`}
          </span>
        </div>
      </div>
    );
  }

  return (
    <>
      {!expanded && renderSurface(false)}

      {expanded && (
        <Dialog
          title={expandedTitle}
          hideHeader
          onClose={() => setExpanded(false)}
          className="flex h-[84dvh] max-h-[84dvh] w-[calc(100%-2rem)] max-w-4xl flex-col overflow-hidden rounded-2xl border border-slate-200/90 p-0 shadow-[0_24px_64px_-12px_rgba(15,23,42,0.22)]"
        >
          <div className="flex shrink-0 items-center justify-between gap-4 border-b border-slate-100 bg-white px-5 py-3.5 sm:px-6">
            <div className="min-w-0 space-y-0.5">
              <h3 className="text-sm font-semibold text-slate-900">{expandedTitle}</h3>
              {expandedSubtitle && (
                <p className="truncate text-xs text-slate-500" title={expandedSubtitle}>
                  {expandedSubtitle}
                </p>
              )}
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              leftIcon={<Minimize2 className="size-3.5" />}
              onClick={() => setExpanded(false)}
              className="h-8 shrink-0 rounded-xl border-slate-200 bg-white text-xs"
            >
              Thu gọn
            </Button>
          </div>
          <div className="flex min-h-0 flex-1 flex-col bg-slate-50/40 p-4 sm:p-5">
            {renderSurface(true)}
          </div>
        </Dialog>
      )}
    </>
  );
}
