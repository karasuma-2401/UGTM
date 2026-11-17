import React, { useState, useEffect } from 'react';
import {
  Download,
  RefreshCw,
  CheckCircle2,
  Split,
  Library,
  FileWarning,
  Search,
  AlertTriangle,
  BookmarkCheck,
  BookmarkX,
  ShieldAlert,
  Save,
  Clock,
  UserCheck,
  Info,
  ChevronDown,
  ChevronUp,
} from 'lucide-react';
import {
  AlignedSpan,
  CitationContext,
  FindingMatch,
  PassageGroup,
  ReportData,
  ReviewDecision,
  ReviewRecord,
} from '../types';

interface ReportTabProps {
  report: ReportData | null;
  isLoading: boolean;
  onRefreshLatest: () => void;
  showToast: (msg: string, type: 'success' | 'error' | 'info' | 'warning') => void;
}

/**
 * Renders text with highlight marks based on AlignedSpans if available.
 */
function HighlightedText({
  text,
  spans,
  isSource = false,
}: {
  text: string;
  spans?: AlignedSpan[];
  isSource?: boolean;
}) {
  if (!text) return <span className="text-slate-400 italic">Trống</span>;
  if (!spans || spans.length === 0) {
    return <span>{text}</span>;
  }

  // Extract matching snippets
  const snippets = spans
    .map((s) => (isSource ? s.source_text : s.query_text))
    .filter((s): s is string => Boolean(s && s.length >= 6));

  if (snippets.length === 0) {
    return <span>{text}</span>;
  }

  // Find occurrences of snippets and highlight
  let segments: Array<{ text: string; highlighted: boolean }> = [{ text, highlighted: false }];

  for (const snippet of snippets) {
    const nextSegments: typeof segments = [];
    for (const seg of segments) {
      if (seg.highlighted) {
        nextSegments.push(seg);
        continue;
      }
      const idx = seg.text.toLowerCase().indexOf(snippet.toLowerCase());
      if (idx !== -1) {
        const before = seg.text.substring(0, idx);
        const match = seg.text.substring(idx, idx + snippet.length);
        const after = seg.text.substring(idx + snippet.length);
        if (before) nextSegments.push({ text: before, highlighted: false });
        nextSegments.push({ text: match, highlighted: true });
        if (after) nextSegments.push({ text: after, highlighted: false });
      } else {
        nextSegments.push(seg);
      }
    }
    segments = nextSegments;
  }

  return (
    <span>
      {segments.map((seg, i) =>
        seg.highlighted ? (
          <mark
            key={i}
            className={`${
              isSource
                ? 'bg-blue-150 text-blue-950 font-medium'
                : 'bg-amber-200 text-amber-950 font-medium'
            } px-1 py-0.5 rounded transition-colors`}
          >
            {seg.text}
          </mark>
        ) : (
          <span key={i}>{seg.text}</span>
        )
      )}
    </span>
  );
}

export const ReportTab: React.FC<ReportTabProps> = ({
  report,
  isLoading,
  onRefreshLatest,
  showToast,
}) => {
  const [filterSource, setFilterSource] = useState<string>('all');
  const [filterCitation, setFilterCitation] = useState<string>('all');
  const [minScore, setMinScore] = useState<number>(0);
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [expandedExplanation, setExpandedExplanation] = useState<Record<string, boolean>>({});

  // Review state stored per report
  const [reviews, setReviews] = useState<Record<string, { decision: ReviewDecision; notes: string }>>({});
  const [reviewHistory, setReviewHistory] = useState<ReviewRecord[]>([]);
  const [reviewerName, setReviewerName] = useState<string>('Thẩm định viên');

  // Load reviews from localStorage
  const reportKey = report?.report_id || report?.job_id || 'active_report';
  useEffect(() => {
    if (!report) return;
    try {
      const stored = localStorage.getItem(`ugtm_reviews_${reportKey}`);
      if (stored) {
        const parsed = JSON.parse(stored);
        setReviews(parsed.reviews || {});
        setReviewHistory(parsed.history || []);
      }
    } catch {
      // ignore JSON parse errors
    }
  }, [reportKey, report]);

  const handleSaveReview = (targetId: string, targetType: 'group' | 'finding') => {
    const cur = reviews[targetId] || { decision: 'pending', notes: '' };
    if (cur.decision === 'pending') {
      showToast('Vui lòng chọn quyết định thẩm định trước khi lưu!', 'warning');
      return;
    }

    const newRecord: ReviewRecord = {
      id: Math.random().toString(36).substring(2, 9),
      target_id: targetId,
      target_type: targetType,
      decision: cur.decision,
      reviewer_name: reviewerName || 'Thẩm định viên',
      notes: cur.notes,
      timestamp: new Date().toISOString(),
    };

    const updatedHistory = [newRecord, ...reviewHistory];
    setReviewHistory(updatedHistory);

    try {
      localStorage.setItem(
        `ugtm_reviews_${reportKey}`,
        JSON.stringify({
          reviews,
          history: updatedHistory,
        })
      );
      showToast(`Đã lưu quyết định thẩm định cho [${targetId}]!`, 'success');
    } catch {
      showToast('Không thể lưu vào localStorage', 'error');
    }
  };

  const handleDownloadJson = () => {
    if (!report) {
      showToast('Chưa có báo cáo để xuất!', 'warning');
      return;
    }
    const blob = new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `UGTM_Report_${report.submission_id || report.job_id || 'scan'}_v3.json`;
    a.click();
    URL.revokeObjectURL(url);
    showToast('Đã tải xuống file báo cáo JSON Schema v3!', 'success');
  };

  if (isLoading) {
    return (
      <div className="bg-white rounded-3xl border border-slate-200 p-16 text-center space-y-4">
        <RefreshCw className="w-10 h-10 animate-spin text-[#2054A5] mx-auto" />
        <p className="text-sm font-semibold text-slate-700">Đang nạp dữ liệu báo cáo thẩm định...</p>
      </div>
    );
  }

  if (!report) {
    return (
      <div className="bg-white rounded-3xl border border-slate-200 p-16 text-center space-y-4">
        <div className="w-14 h-14 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto text-slate-400">
          <Split className="w-7 h-7" />
        </div>
        <div>
          <h3 className="text-base font-bold text-slate-800">Chưa có báo cáo phân tích nào được mở</h3>
          <p className="text-xs text-slate-500 max-w-md mx-auto mt-1">
            Vui lòng thực hiện một tác vụ quét trong tab <strong>"Quét bài nộp"</strong> hoặc chọn một tác vụ đã hoàn thành trong tab <strong>"Lịch sử tác vụ"</strong> để đối chiếu.
          </p>
        </div>
        <button
          onClick={onRefreshLatest}
          className="inline-flex items-center space-x-2 px-4 py-2 bg-blue-50 text-[#2054A5] hover:bg-blue-100 rounded-xl text-xs font-bold transition-colors"
        >
          <RefreshCw className="w-3.5 h-3.5" />
          <span>Tải Báo Cáo Gần Nhất</span>
        </button>
      </div>
    );
  }

  // --------------------------------------------------------------------------
  // Normalize Schema v3 vs Schema v2 fields (Prevent any field from showing 0 wrongly)
  // --------------------------------------------------------------------------
  const summary = report.summary || {};
  const prov = report.provenance || {};

  const similarityCoverage =
    summary.similarity_coverage !== undefined
      ? summary.similarity_coverage
      : (report as any).similarity_coverage !== undefined
      ? (report as any).similarity_coverage
      : 0.0;

  const coveragePercent = (similarityCoverage * 100).toFixed(2);

  const maxRerankerScore =
    summary.max_reranker_score !== undefined
      ? summary.max_reranker_score
      : report.max_rerank_score !== undefined
      ? report.max_rerank_score
      : 0.0;

  const maxScorePercent = Math.round(maxRerankerScore * 100);

  const scannedChunks = summary.scanned_chunks ?? report.total_chunks ?? 0;
  const scannedChars = summary.scanned_characters ?? 0;
  const matchedChars = summary.matched_characters ?? 0;
  const matchedSources = summary.matched_sources ?? report.matched_sources_count ?? 0;
  const totalFindings = summary.total_findings ?? report.findings?.length ?? 0;
  const passageGroupsCount = summary.passage_groups ?? report.passage_groups?.length ?? 0;

  const citedFindings = summary.cited_findings ?? 0;
  const uncitedFindings = summary.uncited_findings ?? 0;
  const uncertainFindings = summary.uncertain_findings ?? 0;

  const submissionDocKey = report.submission_id || (report as any).submission?.document_key || report.job_id || 'scan';
  const submissionFilename = report.filename || (report as any).submission?.filename || `${submissionDocKey}.docx`;
  const submissionVersion = report.submission_version ?? report.version ?? 1;

  const detectorMode = prov.detector_mode || report.detector_mode || 'heuristic';
  const classifierVersion = prov.classifier_version || report.classifier_version;

  // Build groups list (from report.passage_groups or group report.findings)
  let allGroups: PassageGroup[] = report.passage_groups || [];
  if (allGroups.length === 0 && report.findings && report.findings.length > 0) {
    const groupedMap = new Map<string, FindingMatch[]>();
    for (const f of report.findings) {
      const gid = f.passage_group_id || 'PG-0001';
      if (!groupedMap.has(gid)) groupedMap.set(gid, []);
      groupedMap.get(gid)!.push(f);
    }
    allGroups = Array.from(groupedMap.entries()).map(([gid, fList]) => ({
      group_id: gid,
      passage_group_id: gid,
      source_key: fList[0]?.source_key || 'unknown',
      source_version: fList[0]?.source_version || 1,
      source_filename: fList[0]?.source_filename,
      findings_count: fList.length,
      highest_rerank_score: Math.max(...fList.map((x) => x.reranker_score ?? x.rerank_score ?? 0)),
      findings: fList,
    }));
  }

  // Unique sources
  const uniqueSources = Array.from(
    new Set(allGroups.map((g) => g.source_key).filter(Boolean))
  );

  // Filter groups
  const filteredGroups = allGroups.filter((group) => {
    if (filterSource !== 'all' && group.source_key !== filterSource) return false;

    const groupScore = Math.round(
      (group.highest_rerank_score ?? group.max_reranker_score ?? 0) * 100
    );
    if (groupScore < minScore) return false;

    if (filterCitation !== 'all') {
      const hasCitMatch = group.findings?.some((f) => {
        const st = f.citation_status || f.citation_context?.status || 'absent';
        if (filterCitation === 'cited') return st === 'present' || st === 'cited';
        if (filterCitation === 'uncited') return st === 'absent' || st === 'uncited';
        if (filterCitation === 'uncertain') return st === 'uncertain';
        return true;
      });
      if (!hasCitMatch) return false;
    }

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      const hasMatch = group.findings?.some(
        (f) =>
          f.submission_text?.toLowerCase().includes(q) ||
          f.source_text?.toLowerCase().includes(q) ||
          f.source_key?.toLowerCase().includes(q) ||
          f.source_filename?.toLowerCase().includes(q)
      );
      if (!hasMatch) return false;
    }

    return true;
  });

  const isCleanNoMatch = allGroups.length === 0 && similarityCoverage === 0;

  return (
    <div className="space-y-6">
      {/* Top Header & Provenance */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-xs p-6 sm:p-7 space-y-6">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-5 border-b border-slate-100">
          <div>
            <div className="flex items-center space-x-2">
              <span className="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-blue-100 text-blue-800">
                Báo Cáo Thẩm Định Schema v3
              </span>
              <h1 className="text-xl font-extrabold text-slate-900 font-mono">{submissionDocKey}</h1>
            </div>
            <p className="text-xs text-slate-500 mt-1">
              File: <strong className="text-slate-700">{submissionFilename}</strong> (v{submissionVersion}) | Kho:{' '}
              <strong className="text-slate-700">{report.corpus_id || 'uit_thesis'}</strong> | Chế độ AI:{' '}
              <span className="font-semibold text-slate-700 font-mono">{detectorMode}</span>
              {classifierVersion && (
                <span className="ml-1 px-1.5 py-0.5 rounded text-[10px] bg-purple-100 text-purple-700 font-mono">
                  calibrated-{classifierVersion.substring(0, 8)}
                </span>
              )}
            </p>
          </div>

          <div className="flex items-center space-x-2">
            <button
              onClick={handleDownloadJson}
              className="inline-flex items-center space-x-2 px-3.5 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold transition-all"
            >
              <Download className="w-3.5 h-3.5" />
              <span>Xuất JSON v3</span>
            </button>
            <button
              onClick={onRefreshLatest}
              className="inline-flex items-center space-x-2 px-4 py-2 rounded-xl bg-[#2054A5] hover:bg-[#184285] text-white text-xs font-bold shadow-xs transition-all"
            >
              <RefreshCw className="w-3.5 h-3.5" />
              <span>Tải Mới Nhất</span>
            </button>
          </div>
        </div>

        {/* 4 Standardized Metric Cards */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {/* 1. True Similarity Coverage Card */}
          <div
            className={`p-4 rounded-2xl border transition-all ${
              similarityCoverage === 0
                ? 'bg-emerald-50/70 border-emerald-200'
                : similarityCoverage >= 0.25
                ? 'bg-red-50/70 border-red-200'
                : similarityCoverage >= 0.15
                ? 'bg-amber-50/70 border-amber-200'
                : 'bg-cyan-50/70 border-cyan-200'
            }`}
          >
            <p className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
              Độ Phủ Tương Đồng (Coverage)
            </p>
            <div className="flex items-baseline space-x-2 mt-1">
              <span
                className={`text-2xl sm:text-3xl font-extrabold ${
                  similarityCoverage === 0
                    ? 'text-emerald-700'
                    : similarityCoverage >= 0.25
                    ? 'text-red-700'
                    : similarityCoverage >= 0.15
                    ? 'text-amber-700'
                    : 'text-cyan-700'
                }`}
              >
                {coveragePercent}%
              </span>
            </div>
            <p className="text-[11px] font-medium text-slate-600 mt-1">
              {matchedChars.toLocaleString()} / {scannedChars > 0 ? scannedChars.toLocaleString() : '---'} ký tự không chồng lấn
            </p>
          </div>

          {/* 2. Peak Semantic Similarity Card */}
          <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200/80">
            <p className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
              Tương Đồng Ngữ Nghĩa Cực Đại
            </p>
            <p className="text-2xl sm:text-3xl font-extrabold text-slate-800 mt-1">
              {maxScorePercent}% <span className="text-xs font-mono font-normal text-slate-400">({maxRerankerScore.toFixed(4)})</span>
            </p>
            <p className="text-[11px] text-slate-500 mt-1">Cross-Encoder BGE-Reranker-v2</p>
          </div>

          {/* 3. Flagged Groups and Findings */}
          <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200/80">
            <p className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
              Nhóm Đoạn & Cặp Nghi Vấn
            </p>
            <p className="text-2xl sm:text-3xl font-extrabold text-slate-800 mt-1">
              {passageGroupsCount}{' '}
              <span className="text-xs font-normal text-slate-400">nhóm / {totalFindings} cặp</span>
            </p>
            <p className="text-[11px] text-slate-500 mt-1">{matchedSources} tài liệu nguồn tham chiếu</p>
          </div>

          {/* 4. Citation Transparency Breakdown */}
          <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200/80">
            <p className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
              Minh Bạch Trích Dẫn
            </p>
            <div className="flex items-center space-x-2 mt-1.5">
              <span className="px-2 py-0.5 rounded text-xs font-bold bg-emerald-100 text-emerald-800">
                {citedFindings} Có trích dẫn
              </span>
              <span className="px-2 py-0.5 rounded text-xs font-bold bg-red-100 text-red-800">
                {uncitedFindings} Thiếu
              </span>
            </div>
            <p className="text-[11px] text-slate-500 mt-1">
              {uncertainFindings > 0 ? `${uncertainFindings} đoạn cần rà soát thêm` : `${scannedChunks} đoạn đã phân tích`}
            </p>
          </div>
        </div>
      </div>

      {/* Filter and Search Bar (Active when there are violations) */}
      {!isCleanNoMatch && allGroups.length > 0 && (
        <div className="bg-white rounded-2xl border border-slate-200 p-4 flex flex-wrap items-center justify-between gap-3 text-xs">
          <div className="flex flex-wrap items-center gap-3 flex-1">
            {/* Search Input */}
            <div className="relative min-w-[200px] flex-1 max-w-sm">
              <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Tìm từ khóa trong nội dung bài nộp / nguồn..."
                className="w-full pl-9 pr-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500"
              />
            </div>

            {/* Source Selector */}
            <select
              value={filterSource}
              onChange={(e) => setFilterSource(e.target.value)}
              className="px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 text-slate-700 font-medium"
            >
              <option value="all">Tất cả tài liệu nguồn ({uniqueSources.length})</option>
              {uniqueSources.map((s) => (
                <option key={s} value={s}>
                  Nguồn: {s}
                </option>
              ))}
            </select>

            {/* Citation Filter */}
            <select
              value={filterCitation}
              onChange={(e) => setFilterCitation(e.target.value)}
              className="px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 text-slate-700 font-medium"
            >
              <option value="all">Tất cả trạng thái trích dẫn</option>
              <option value="uncited">Chỉ đoạn thiếu trích dẫn</option>
              <option value="cited">Chỉ đoạn có trích dẫn</option>
              <option value="uncertain">Chỉ đoạn nghi vấn / thứ cấp</option>
            </select>

            {/* Threshold Slider */}
            <div className="flex items-center space-x-2 text-slate-600">
              <span>Ngưỡng &gt;=</span>
              <input
                type="range"
                min="0"
                max="90"
                step="5"
                value={minScore}
                onChange={(e) => setMinScore(parseInt(e.target.value))}
                className="w-24 accent-[#2054A5]"
              />
              <span className="font-mono font-bold text-[#2054A5]">{minScore}%</span>
            </div>
          </div>

          <span className="text-slate-500 text-[11px]">
            Hiển thị <strong>{filteredGroups.length}</strong> / {allGroups.length} nhóm đoạn
          </span>
        </div>
      )}

      {/* Side-by-Side Findings Section */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-bold text-slate-800 flex items-center space-x-2">
            <Split className="w-4 h-4 text-[#2054A5]" />
            <span>Chi tiết Đối chiếu Từng Đoạn Vi Phạm (Side-by-Side Evidence Workbench)</span>
          </h2>
          <span className="text-xs text-slate-500 font-medium">
            <span className="inline-block w-2.5 h-2.5 rounded-full bg-amber-400 mr-1" />
            Màu cam: Bài nộp nghi vấn | <span className="inline-block w-2.5 h-2.5 rounded-full bg-blue-500 mr-1" />
            Màu xanh: Văn bản gốc đối chứng
          </span>
        </div>

        {/* Clean No-Match View (Strict Academic Compliance) */}
        {isCleanNoMatch && (
          <div className="bg-white rounded-3xl border border-slate-200 p-12 text-center space-y-4">
            <div className="w-16 h-16 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center mx-auto shadow-xs">
              <CheckCircle2 className="w-10 h-10 stroke-[2.2]" />
            </div>
            <div className="space-y-1">
              <h3 className="text-base font-bold text-slate-800">
                Không phát hiện đoạn văn nào vượt ngưỡng tương đồng trong kho tham chiếu hiện tại
              </h3>
              <p className="text-xs text-slate-500 max-w-xl mx-auto leading-relaxed">
                Tài liệu đã được đối chiếu toàn văn với toàn bộ luận văn trong kho tham chiếu (<strong>{report.corpus_id || 'uit_thesis'}</strong>) bằng mô hình BGE-M3 và Cross-Encoder nhưng không có đoạn nào vượt ngưỡng nghi vấn.
              </p>
            </div>
            <div className="max-w-md mx-auto p-3.5 bg-slate-50 rounded-2xl border border-slate-200 text-left flex items-start space-x-2.5 text-[11px] text-slate-600">
              <Info className="w-4 h-4 text-blue-600 flex-shrink-0 mt-0.5" />
              <span>
                <strong>Lưu ý quy chế học thuật:</strong> Kết quả này phản ánh phạm vi đối soát tự động với các tài liệu đang có trong cơ sở dữ liệu hiện tại, không thay thế chứng nhận độc lập về tính nguyên bản tuyệt đối của văn bản.
              </span>
            </div>
          </div>
        )}

        {/* Group Cards List */}
        {filteredGroups.map((group, gIdx) => {
          const groupId = group.passage_group_id || group.group_id || `PG-${gIdx + 1}`;
          const groupScore = group.highest_rerank_score ?? group.max_reranker_score ?? 0;
          const groupScorePercent = Math.round(groupScore * 100);
          const currentReview = reviews[groupId] || { decision: 'pending', notes: '' };

          return (
            <div
              key={groupId}
              className="bg-white rounded-3xl border border-slate-200 overflow-hidden shadow-xs hover:shadow-md transition-shadow"
            >
              {/* Group Header Bar */}
              <div className="bg-slate-50/80 px-6 py-3.5 border-b border-slate-200/80 flex flex-wrap items-center justify-between gap-2 text-xs">
                <div className="flex items-center space-x-2.5">
                  <span className="w-6 h-6 rounded-lg bg-red-600 text-white font-mono font-bold text-[11px] flex items-center justify-center">
                    #{gIdx + 1}
                  </span>
                  <span className="font-bold text-slate-800 font-mono">Nhóm: {groupId}</span>
                  <span className="text-slate-400">|</span>
                  <span className="text-slate-600">
                    Nguồn: <strong className="font-mono text-blue-700">{group.source_filename || group.source_key}</strong> (v{group.source_version})
                  </span>
                  {group.document_family && (
                    <span className="text-[10px] text-slate-400 font-mono">({group.document_family})</span>
                  )}
                </div>

                <div className="flex items-center space-x-2">
                  <span className="px-2.5 py-1 rounded-full text-xs font-extrabold bg-red-50 text-red-700 border border-red-200">
                    Rerank Score: {groupScore.toFixed(4)} ({groupScorePercent}%)
                  </span>
                  {currentReview.decision !== 'pending' && (
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-200 flex items-center space-x-1">
                      <UserCheck className="w-3 h-3" />
                      <span>Đã thẩm định</span>
                    </span>
                  )}
                </div>
              </div>

              {/* Sub-findings list inside group */}
              <div className="divide-y divide-slate-100">
                {group.findings?.map((finding: FindingMatch, fIdx: number) => {
                  const rerankScore = finding.reranker_score ?? finding.rerank_score ?? 0;
                  const rerankPercent = Math.round(rerankScore * 100);
                  const citStatus = finding.citation_status || finding.citation_context?.status || 'absent';
                  const citCtx: CitationContext = finding.citation_context || { status: 'absent' };
                  const fKey = `${groupId}_${fIdx}`;
                  const isExp = expandedExplanation[fKey] || false;

                  return (
                    <div key={finding.finding_id || fIdx} className="p-5 space-y-4">
                      {/* Side-by-side text columns */}
                      <div className="grid grid-cols-1 md:grid-cols-12 text-xs divide-y md:divide-y-0 md:divide-x divide-slate-200 border border-slate-200/80 rounded-2xl overflow-hidden">
                        {/* Left: Suspect Submission Text */}
                        <div className="md:col-span-5 p-4 bg-amber-50/20 space-y-2">
                          <div className="flex items-center justify-between text-amber-950 font-semibold border-b border-amber-200/50 pb-1.5">
                            <span className="flex items-center space-x-1.5">
                              <FileWarning className="w-3.5 h-3.5 text-amber-600" />
                              <span>Nội dung bài nộp nghi vấn</span>
                            </span>
                            <span className="text-[10px] text-amber-700 font-mono">
                              Đoạn #{finding.submission_chunk_no}{' '}
                              {finding.submission_page ? `(Trang ${finding.submission_page})` : ''}
                            </span>
                          </div>
                          <div className="p-3 bg-white rounded-xl border border-amber-200/80 text-slate-800 leading-relaxed font-sans shadow-2xs max-h-56 overflow-y-auto">
                            <HighlightedText
                              text={finding.submission_text}
                              spans={finding.aligned_spans}
                              isSource={false}
                            />
                          </div>
                        </div>

                        {/* Center: AI Evidence Metrics Pillar */}
                        <div className="md:col-span-2 p-4 bg-slate-50/80 flex flex-col justify-center items-center text-center space-y-2.5">
                          <div className="w-full">
                            <p className="text-[10px] text-slate-500 font-bold uppercase tracking-wider">BGE Cross-Encoder</p>
                            <p className="text-base font-extrabold text-red-600 mt-0.5">{rerankPercent}%</p>
                            <div className="w-full bg-slate-200 rounded-full h-1.5 mt-1 overflow-hidden">
                              <div className="bg-red-500 h-full rounded-full" style={{ width: `${rerankPercent}%` }} />
                            </div>
                          </div>

                          <div className="w-full pt-2 border-t border-slate-200/70 text-[10px] space-y-1 text-slate-600">
                            <div className="flex justify-between">
                              <span className="text-slate-500">Vector Cosine:</span>
                              <span className="font-mono font-bold text-slate-800">
                                {finding.dense_score !== undefined ? finding.dense_score.toFixed(4) : '---'}
                              </span>
                            </div>
                            <div className="flex justify-between">
                              <span className="text-slate-500">Trigram Lexical:</span>
                              <span className="font-mono font-bold text-slate-800">
                                {finding.lexical_score !== undefined ? finding.lexical_score.toFixed(4) : '---'}
                              </span>
                            </div>
                            {finding.classifier_score !== undefined && (
                              <div className="flex justify-between">
                                <span className="text-slate-500">Classifier:</span>
                                <span className="font-mono font-bold text-purple-700">
                                  {(finding.classifier_score * 100).toFixed(1)}%
                                </span>
                              </div>
                            )}
                          </div>

                          <span className="px-2 py-0.5 rounded text-[9px] font-mono font-semibold bg-slate-200 text-slate-700">
                            {finding.decision_source || 'custom_classifier'}
                          </span>
                        </div>

                        {/* Right: Reference Source Text */}
                        <div className="md:col-span-5 p-4 bg-blue-50/20 space-y-2">
                          <div className="flex items-center justify-between text-blue-950 font-semibold border-b border-blue-200/50 pb-1.5">
                            <span className="flex items-center space-x-1.5">
                              <Library className="w-3.5 h-3.5 text-blue-600" />
                              <span>Văn bản gốc trong kho tham chiếu</span>
                            </span>
                            <span className="text-[10px] text-blue-700 font-mono">
                              Đoạn #{finding.source_chunk_no}{' '}
                              {finding.source_page ? `(Trang ${finding.source_page})` : ''}
                            </span>
                          </div>
                          <div className="p-3 bg-white rounded-xl border border-blue-200/80 text-slate-800 leading-relaxed font-sans shadow-2xs max-h-56 overflow-y-auto">
                            <HighlightedText
                              text={finding.source_text}
                              spans={finding.aligned_spans}
                              isSource={true}
                            />
                          </div>
                        </div>
                      </div>

                      {/* Citation Context Banner */}
                      <div
                        className={`p-3 rounded-xl border flex flex-col sm:flex-row sm:items-center justify-between gap-2 text-xs ${
                          citStatus === 'present' || citStatus === 'cited'
                            ? 'bg-emerald-50/80 border-emerald-200 text-emerald-900'
                            : citStatus === 'uncertain'
                            ? 'bg-amber-50/80 border-amber-200 text-amber-900'
                            : 'bg-red-50/80 border-red-200 text-red-900'
                        }`}
                      >
                        <div className="flex items-center space-x-2">
                          {citStatus === 'present' || citStatus === 'cited' ? (
                            <BookmarkCheck className="w-4 h-4 text-emerald-600 flex-shrink-0" />
                          ) : citStatus === 'uncertain' ? (
                            <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0" />
                          ) : (
                            <BookmarkX className="w-4 h-4 text-red-600 flex-shrink-0" />
                          )}
                          <span className="font-bold">
                            {citStatus === 'present' || citStatus === 'cited'
                              ? `Đã phát hiện trích dẫn: ${citCtx.markers?.join(', ') || '[Nguồn trích dẫn]'}`
                              : citStatus === 'uncertain'
                              ? 'Cần rà soát trích dẫn / Dẫn nguồn thứ cấp'
                              : 'Chưa phát hiện trích dẫn nguồn trong đoạn này'}
                          </span>
                          {citCtx.potential_misattribution && (
                            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-200 text-amber-950 flex items-center space-x-1">
                              <ShieldAlert className="w-3 h-3" />
                              <span>Nghi vấn dẫn nguồn thứ cấp</span>
                            </span>
                          )}
                        </div>

                        {citCtx.explanation && (
                          <button
                            onClick={() => setExpandedExplanation((prev) => ({ ...prev, [fKey]: !isExp }))}
                            className="inline-flex items-center space-x-1 text-[11px] font-semibold text-slate-700 hover:text-slate-900 transition-colors"
                          >
                            <span>Chi tiết trích dẫn</span>
                            {isExp ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
                          </button>
                        )}
                      </div>

                      {/* Expanded Citation Explanation */}
                      {isExp && citCtx.explanation && (
                        <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 text-xs text-slate-700 space-y-1">
                          <p className="font-semibold text-slate-800">Giải trình nhận diện bối cảnh trích dẫn:</p>
                          <p className="leading-relaxed">{citCtx.explanation}</p>
                          {citCtx.cited_works && citCtx.cited_works.length > 0 && (
                            <p className="text-[11px] text-slate-500 font-mono">
                              Công trình được trích: {citCtx.cited_works.join(', ')}
                            </p>
                          )}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>

              {/* Reviewer Action Box for the Passage Group */}
              <div className="bg-slate-50/90 p-4 border-t border-slate-200 space-y-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center space-x-2">
                    <UserCheck className="w-4 h-4 text-[#2054A5]" />
                    <span className="text-xs font-bold text-slate-800">Thẩm Định Viên Đánh Giá Đoạn:</span>
                    <input
                      type="text"
                      value={reviewerName}
                      onChange={(e) => setReviewerName(e.target.value)}
                      placeholder="Tên thẩm định viên..."
                      className="px-2 py-1 text-[11px] rounded-lg border border-slate-300 bg-white text-slate-700 w-32"
                    />
                  </div>

                  <div className="flex items-center space-x-2 text-xs">
                    <select
                      value={currentReview.decision}
                      onChange={(e) =>
                        setReviews((prev) => ({
                          ...prev,
                          [groupId]: { ...currentReview, decision: e.target.value as ReviewDecision },
                        }))
                      }
                      className="px-3 py-1.5 rounded-xl border border-slate-300 font-semibold text-slate-800 bg-white focus:outline-none focus:ring-2 focus:ring-blue-500/20"
                    >
                      <option value="pending">-- Chưa thẩm định --</option>
                      <option value="confirmed_plagiarism">🔴 Xác nhận đạo văn (Cần giải trình)</option>
                      <option value="acceptable_citation">🟢 Trích dẫn hợp lệ (Chấp nhận)</option>
                      <option value="common_boilerplate">⚪ Thuật ngữ / Mẫu chung (Bỏ qua)</option>
                      <option value="needs_investigation">🟡 Cần hội đồng xem xét thêm</option>
                    </select>

                    <button
                      onClick={() => handleSaveReview(groupId, 'group')}
                      className="inline-flex items-center space-x-1.5 px-3.5 py-1.5 rounded-xl bg-[#2054A5] hover:bg-[#184285] text-white text-xs font-bold shadow-2xs transition-all"
                    >
                      <Save className="w-3.5 h-3.5" />
                      <span>Lưu Đánh Giá</span>
                    </button>
                  </div>
                </div>

                <textarea
                  rows={2}
                  value={currentReview.notes}
                  onChange={(e) =>
                    setReviews((prev) => ({
                      ...prev,
                      [groupId]: { ...currentReview, notes: e.target.value },
                    }))
                  }
                  placeholder="Ghi chú thẩm định của giảng viên / hội đồng (ví dụ: 'Sinh viên đã dẫn nguồn IEEE ở cuối bài, đoạn 2 là định nghĩa cơ bản')..."
                  className="w-full p-2.5 text-xs bg-white rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 placeholder:text-slate-400"
                />
              </div>
            </div>
          );
        })}
      </div>

      {/* Review History Audit Trail (Dữ liệu review được lưu có lịch sử) */}
      {reviewHistory.length > 0 && (
        <div className="bg-white rounded-3xl border border-slate-200 p-6 space-y-4 shadow-xs">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <h3 className="text-sm font-bold text-slate-800 flex items-center space-x-2">
              <Clock className="w-4 h-4 text-purple-600" />
              <span>Nhật Ký Thẩm Định Của Hội Đồng (Review Audit Trail)</span>
            </h3>
            <span className="text-xs text-slate-500 font-medium font-mono">
              {reviewHistory.length} mục đã ghi nhận
            </span>
          </div>

          <div className="divide-y divide-slate-100">
            {reviewHistory.map((rec) => (
              <div key={rec.id} className="py-3 flex flex-col sm:flex-row sm:items-center justify-between gap-2 text-xs">
                <div className="space-y-0.5">
                  <div className="flex items-center space-x-2">
                    <span className="font-mono font-bold text-blue-700">[{rec.target_id}]</span>
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                        rec.decision === 'confirmed_plagiarism'
                          ? 'bg-red-100 text-red-800'
                          : rec.decision === 'acceptable_citation'
                          ? 'bg-emerald-100 text-emerald-800'
                          : rec.decision === 'common_boilerplate'
                          ? 'bg-slate-200 text-slate-700'
                          : 'bg-amber-100 text-amber-800'
                      }`}
                    >
                      {rec.decision === 'confirmed_plagiarism'
                        ? 'Xác nhận đạo văn'
                        : rec.decision === 'acceptable_citation'
                        ? 'Trích dẫn hợp lệ'
                        : rec.decision === 'common_boilerplate'
                        ? 'Thuật ngữ chung'
                        : 'Cần xem xét thêm'}
                    </span>
                    <span className="text-slate-500 text-[11px]">Bởi: <strong>{rec.reviewer_name}</strong></span>
                  </div>
                  {rec.notes && <p className="text-slate-700 italic">"{rec.notes}"</p>}
                </div>
                <span className="text-slate-400 font-mono text-[11px]">
                  {new Date(rec.timestamp).toLocaleString('vi-VN')}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Academic Disclaimer Box */}
      <div className="bg-amber-50/80 border border-amber-200 rounded-2xl p-4 sm:p-5 flex items-start space-x-3.5 text-xs text-amber-900 shadow-xs">
        <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
        <div className="space-y-1">
          <strong className="font-bold text-amber-950">Lưu ý nghiệp vụ học thuật & quy chế:</strong>
          <p className="leading-relaxed text-amber-800">
            {report.disclaimer ||
              'Các chỉ số tương đồng và đoạn văn trùng lặp là bằng chứng kỹ thuật phục vụ hội đồng chấm và giảng viên hướng dẫn thẩm định ngữ cảnh, trích dẫn; hệ thống không tự động đưa ra kết luận kỷ luật đạo văn.'}
          </p>
        </div>
      </div>
    </div>
  );
};
