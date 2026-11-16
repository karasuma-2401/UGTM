import { FileText } from 'lucide-react';

function getFileExtensionKey(fileName: string): 'pdf' | 'docx' | 'doc' | 'file' {
  const ext = fileName.trim().toLowerCase().split('.').pop();
  if (ext === 'pdf') return 'pdf';
  if (ext === 'docx') return 'docx';
  if (ext === 'doc') return 'doc';
  return 'file';
}

export interface ThesisFileTypeIconProps {
  fileName: string;
}

/**
 * Authored SVG file-type icon matching the student submission cards (PDF, DOCX/DOC, generic file).
 */
export function ThesisFileTypeIcon({ fileName }: ThesisFileTypeIconProps) {
  const fileType = getFileExtensionKey(fileName);

  if (fileType === 'pdf') {
    return (
      <div
        data-file-type="pdf"
        className="size-10 rounded-xl bg-white border border-rose-200/80 flex items-center justify-center shrink-0 shadow-2xs"
        aria-hidden="true"
      >
        <svg viewBox="0 0 28 28" className="size-6" fill="none">
          <path
            d="M7 3.5h9.2L21 8.3V23a1.5 1.5 0 0 1-1.5 1.5H7A1.5 1.5 0 0 1 5.5 23V5A1.5 1.5 0 0 1 7 3.5Z"
            className="fill-rose-50/70 stroke-rose-600/85"
            strokeWidth="1.5"
            strokeLinejoin="round"
          />
          <path
            d="M16 3.7V8.5h4.8"
            className="stroke-rose-600/75"
            strokeWidth="1.4"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <rect x="7" y="14" width="14" height="7" rx="2" className="fill-rose-600" />
          <text
            x="14"
            y="19.1"
            textAnchor="middle"
            className="fill-white"
            fontSize="5.2"
            fontWeight="700"
            letterSpacing="0.3"
            fontFamily="ui-sans-serif, system-ui, sans-serif"
          >
            PDF
          </text>
        </svg>
      </div>
    );
  }

  if (fileType === 'docx' || fileType === 'doc') {
    const label = fileType === 'docx' ? 'DOCX' : 'DOC';
    return (
      <div
        data-file-type={fileType}
        className="size-10 rounded-xl bg-white border border-blue-200/80 flex items-center justify-center shrink-0 shadow-2xs"
        aria-hidden="true"
      >
        <svg viewBox="0 0 28 28" className="size-6" fill="none">
          <path
            d="M7 3.5h9.2L21 8.3V23a1.5 1.5 0 0 1-1.5 1.5H7A1.5 1.5 0 0 1 5.5 23V5A1.5 1.5 0 0 1 7 3.5Z"
            className="fill-blue-50/80 stroke-[#2054a5]"
            strokeWidth="1.5"
            strokeLinejoin="round"
          />
          <path
            d="M16 3.7V8.5h4.8"
            className="stroke-[#2054a5]/80"
            strokeWidth="1.4"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <rect x="5.8" y="14" width="16.4" height="7" rx="2" className="fill-[#2054a5]" />
          <text
            x="14"
            y="19.1"
            textAnchor="middle"
            className="fill-white"
            fontSize={fileType === 'docx' ? '4.5' : '5.1'}
            fontWeight="700"
            letterSpacing="0.2"
            fontFamily="ui-sans-serif, system-ui, sans-serif"
          >
            {label}
          </text>
        </svg>
      </div>
    );
  }

  return (
    <div
      data-file-type="file"
      className="size-10 rounded-xl bg-white border border-slate-200/90 text-slate-600 flex items-center justify-center shrink-0 shadow-2xs"
      aria-hidden="true"
    >
      <FileText className="size-4.5" />
    </div>
  );
}
