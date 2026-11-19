import React, { useState, useEffect, useRef } from 'react';
import { Library, Upload, Plus, RefreshCw, FileText, CheckCircle2, FileUp } from 'lucide-react';
import { CorpusDocument } from '../types';
import { fetchCorpusDocuments, submitCorpusDocument, submitQuickIndex } from '../api/client';

interface CorpusTabProps {
  showToast: (msg: string, type: 'success' | 'error' | 'info' | 'warning') => void;
}

export const CorpusTab: React.FC<CorpusTabProps> = ({ showToast }) => {
  const [documents, setDocuments] = useState<CorpusDocument[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isQuickIndexing, setIsQuickIndexing] = useState(false);
  const [isUploading, setIsUploading] = useState(false);

  // Upload modal/form states
  const [sourceKey, setSourceKey] = useState('');
  const [version, setVersion] = useState(1);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const loadDocuments = async () => {
    setIsLoading(true);
    try {
      const data = await fetchCorpusDocuments('uit_thesis');
      setDocuments(data);
    } catch (err: any) {
      showToast(err.message || 'Lỗi nạp danh sách kho', 'error');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadDocuments();
  }, []);

  const handleQuickIndex = async () => {
    setIsQuickIndexing(true);
    try {
      const res = await submitQuickIndex();
      showToast(`Đã xếp hàng index tài liệu nguồn (${res.source_id})!`, 'success');
      setTimeout(() => loadDocuments(), 3000);
    } catch (err: any) {
      showToast(err.message || 'Lỗi nạp nguồn', 'error');
    } finally {
      setIsQuickIndexing(false);
    }
  };

  const handleUploadSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile) {
      showToast('Vui lòng chọn file Word hoặc PDF để nạp!', 'warning');
      return;
    }
    if (!sourceKey.trim()) {
      showToast('Vui lòng nhập Mã Nguồn (Source Key)!', 'warning');
      return;
    }

    const formData = new FormData();
    formData.append('corpus_id', 'uit_thesis');
    formData.append('source_key', sourceKey.trim());
    formData.append('version', version.toString());
    formData.append('file', selectedFile);

    setIsUploading(true);
    try {
      const res = await submitCorpusDocument(formData);
      showToast(`Đã nạp tài liệu thành công (Job: ${res.job_id.substring(0, 8)}...)`, 'success');
      setSelectedFile(null);
      setSourceKey('');
      if (fileInputRef.current) fileInputRef.current.value = '';
      setTimeout(() => loadDocuments(), 3000);
    } catch (err: any) {
      showToast(err.message || 'Lỗi nạp tài liệu', 'error');
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Top Banner */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 sm:p-7 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="flex items-center space-x-2">
            <Library className="w-5 h-5 text-[#2054A5]" />
            <h1 className="text-xl font-extrabold text-slate-900">Kho Tài Liệu Nguồn Tham Chiếu (Corpus)</h1>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Mã kho hiện tại: <strong className="font-mono text-slate-700">uit_thesis</strong> | Tổng cộng:{' '}
            <strong className="text-blue-700 font-bold">{documents.length}</strong> tài liệu đã kích hoạt
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2.5">
          <button
            onClick={handleQuickIndex}
            disabled={isQuickIndexing}
            className="inline-flex items-center space-x-2 px-3.5 py-2 rounded-xl bg-orange-50 hover:bg-orange-100 text-[#FA7921] text-xs font-bold border border-orange-200 transition-all disabled:opacity-50"
          >
            {isQuickIndexing ? (
              <RefreshCw className="w-3.5 h-3.5 animate-spin" />
            ) : (
              <Plus className="w-3.5 h-3.5" />
            )}
            <span>Nạp nhanh bài mẫu nguồn (source_doc.docx)</span>
          </button>

          <button
            onClick={loadDocuments}
            disabled={isLoading}
            className="inline-flex items-center space-x-2 px-3.5 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold transition-all disabled:opacity-50"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            <span>Làm mới danh sách</span>
          </button>
        </div>
      </div>

      {/* Upload Form to add to Corpus */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 sm:p-7 space-y-4">
        <h2 className="text-sm font-bold text-slate-800 flex items-center space-x-2">
          <FileUp className="w-4 h-4 text-[#2054A5]" />
          <span>Thêm Khóa Luận Mới vào Kho Tham Chiếu</span>
        </h2>

        <form onSubmit={handleUploadSubmit} className="grid grid-cols-1 sm:grid-cols-12 gap-3 items-end">
          <div className="sm:col-span-4">
            <label className="block text-xs font-semibold text-slate-700 mb-1">Mã Nguồn (Source Key)</label>
            <input
              type="text"
              value={sourceKey}
              onChange={(e) => setSourceKey(e.target.value)}
              placeholder="VD: kltn_2026_se505"
              required
              className="w-full text-xs font-mono px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
            />
          </div>

          <div className="sm:col-span-2">
            <label className="block text-xs font-semibold text-slate-700 mb-1">Phiên bản</label>
            <input
              type="number"
              min="1"
              value={version}
              onChange={(e) => setVersion(parseInt(e.target.value) || 1)}
              required
              className="w-full text-xs px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
            />
          </div>

          <div className="sm:col-span-4">
            <label className="block text-xs font-semibold text-slate-700 mb-1">File văn bản (.docx, .pdf)</label>
            <input
              ref={fileInputRef}
              type="file"
              accept=".docx,.pdf"
              onChange={(e) => setSelectedFile(e.target.files?.[0] || null)}
              required
              className="w-full text-xs px-3 py-1.5 rounded-xl border border-slate-200 focus:outline-none file:mr-3 file:py-1 file:px-2.5 file:rounded-lg file:border-0 file:text-xs file:font-semibold file:bg-blue-50 file:text-[#2054A5]"
            />
          </div>

          <div className="sm:col-span-2">
            <button
              type="submit"
              disabled={isUploading}
              className="w-full py-2.5 px-4 rounded-xl bg-[#2054A5] hover:bg-[#184285] text-white text-xs font-bold transition-all disabled:opacity-50 flex items-center justify-center space-x-1"
            >
              {isUploading ? <RefreshCw className="w-3.5 h-3.5 animate-spin mr-1" /> : <Upload className="w-3.5 h-3.5 mr-1" />}
              <span>{isUploading ? 'Đang nạp...' : 'Nạp vào kho'}</span>
            </button>
          </div>
        </form>
      </div>

      {/* Documents Table */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200 text-slate-500 font-bold uppercase text-[10px] tracking-wider">
                <th className="py-3.5 px-5">Mã Nguồn</th>
                <th className="py-3.5 px-5">Tên Tài Liệu</th>
                <th className="py-3.5 px-5">Phiên Bản</th>
                <th className="py-3.5 px-5">Số Đoạn (Chunks)</th>
                <th className="py-3.5 px-5">Thời Gian Nạp</th>
                <th className="py-3.5 px-5">Trạng Thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 font-sans">
              {documents.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-12 text-center text-slate-400">
                    Kho dữ liệu hiện đang trống. Bấm nút "Nạp nhanh bài mẫu nguồn" để thêm tài liệu đối chứng.
                  </td>
                </tr>
              ) : (
                documents.map((doc, idx) => (
                  <tr key={idx} className="hover:bg-slate-50/70 transition-colors">
                    <td className="py-3 px-5 font-mono font-bold text-blue-700">{doc.source_key}</td>
                    <td className="py-3 px-5 text-slate-800 font-medium flex items-center space-x-2">
                      <FileText className="w-4 h-4 text-slate-400 flex-shrink-0" />
                      <span>{doc.filename}</span>
                    </td>
                    <td className="py-3 px-5 text-slate-500 font-mono">v{doc.version}</td>
                    <td className="py-3 px-5 font-bold text-slate-700">{doc.chunk_count} đoạn</td>
                    <td className="py-3 px-5 text-slate-500">
                      {doc.indexed_at ? new Date(doc.indexed_at).toLocaleString('vi-VN') : '---'}
                    </td>
                    <td className="py-3 px-5">
                      <span
                        className={`inline-flex items-center space-x-1 px-2.5 py-0.5 text-[10px] font-bold rounded-full ${
                          doc.is_active
                            ? 'bg-emerald-100 text-emerald-800'
                            : 'bg-slate-100 text-slate-600'
                        }`}
                      >
                        {doc.is_active && <CheckCircle2 className="w-3 h-3 text-emerald-600 mr-0.5" />}
                        <span>{doc.is_active ? 'Đang hoạt động' : 'Chưa kích hoạt'}</span>
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

