import React, { useState, useEffect, useRef } from 'react';
import { Upload, FileText, PlayCircle, Download, X, Scan, RefreshCw, AlertCircle, ArrowRight, Zap, CheckCircle2, ShieldCheck, HelpCircle } from 'lucide-react';
import { ClientConfig, JobRecord } from '../types';
import { submitScanDocument, submitQuickScan, fetchJobStatus } from '../api/client';

interface ScanTabProps {
  config: ClientConfig | null;
  onViewReport: (jobId: string) => void;
  showToast: (msg: string, type: 'success' | 'error' | 'info' | 'warning') => void;
}

export const ScanTab: React.FC<ScanTabProps> = ({ config, onViewReport, showToast }) => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [corpusId, setCorpusId] = useState('uit_thesis');
  const [submissionId, setSubmissionId] = useState('');
  const [version, setVersion] = useState(1);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isQuickScanning, setIsQuickScanning] = useState(false);

  // Tracker state
  const [activeJobId, setActiveJobId] = useState<string | null>(null);
  const [trackingSubmissionId, setTrackingSubmissionId] = useState<string>('');
  const [jobStatus, setJobStatus] = useState<JobRecord['status']>('queued');
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [currentStep, setCurrentStep] = useState(1);

  const fileInputRef = useRef<HTMLInputElement>(null);

  const generateRandomSubmissionId = () => {
    const rand = Math.random().toString(16).substring(2, 8);
    return `kltn_nop_${rand}`;
  };

  useEffect(() => {
    setSubmissionId(generateRandomSubmissionId());
  }, []);

  // Timer loop for tracking
  useEffect(() => {
    if (!activeJobId || jobStatus === 'completed' || jobStatus === 'failed') return;

    const timer = setInterval(() => {
      setElapsedSeconds((prev) => prev + 1);
    }, 1000);

    return () => clearInterval(timer);
  }, [activeJobId, jobStatus]);

  // Stepper progress simulation based on elapsed seconds
  useEffect(() => {
    if (jobStatus === 'completed') {
      setCurrentStep(5);
      return;
    }
    if (elapsedSeconds < 4) setCurrentStep(1);
    else if (elapsedSeconds < 10) setCurrentStep(2);
    else if (elapsedSeconds < 25) setCurrentStep(3);
    else if (elapsedSeconds < 45) setCurrentStep(4);
    else setCurrentStep(4);
  }, [elapsedSeconds, jobStatus]);

  // Polling loop for active job
  useEffect(() => {
    if (!activeJobId || jobStatus === 'completed' || jobStatus === 'failed') return;

    const poll = setInterval(async () => {
      try {
        const job = await fetchJobStatus(activeJobId);
        setJobStatus(job.status);
        if (job.status === 'completed') {
          showToast('Quá trình quét hoàn tất thành công!', 'success');
          clearInterval(poll);
        } else if (job.status === 'failed') {
          showToast(`Tác vụ thất bại: ${job.error_code || 'Lỗi không xác định'}`, 'error');
          clearInterval(poll);
        }
      } catch (err) {
        console.warn('Poll error:', err);
      }
    }, 2500);

    return () => clearInterval(poll);
  }, [activeJobId, jobStatus, showToast]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (file.size > (config?.max_upload_mb || 20) * 1024 * 1024) {
        showToast(`Dung lượng file vượt quá giới hạn ${config?.max_upload_mb || 20}MB`, 'error');
        return;
      }
      setSelectedFile(file);
    }
  };

  const handleClearFile = (e: React.MouseEvent) => {
    e.stopPropagation();
    setSelectedFile(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const handleSubmitScan = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile) {
      showToast('Vui lòng chọn file Word (.docx) hoặc PDF (.pdf) cần quét!', 'warning');
      return;
    }

    const formData = new FormData();
    formData.append('corpus_id', corpusId);
    formData.append('submission_id', submissionId);
    formData.append('version', version.toString());
    formData.append('file', selectedFile);

    setIsSubmitting(true);
    try {
      const res = await submitScanDocument(formData);
      showToast(`Đã gửi yêu cầu quét thành công! Job ID: ${res.job_id.substring(0, 8)}...`, 'success');
      setActiveJobId(res.job_id);
      setTrackingSubmissionId(submissionId);
      setJobStatus('queued');
      setElapsedSeconds(0);
      setCurrentStep(1);
    } catch (err: any) {
      showToast(err.message || 'Lỗi nộp bài', 'error');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleQuickScan = async () => {
    setIsQuickScanning(true);
    try {
      const res = await submitQuickScan();
      showToast(`Bắt đầu quét mẫu: suspect_doc.docx (Mã: ${res.submission_id})`, 'success');
      setActiveJobId(res.job_id);
      setTrackingSubmissionId(res.submission_id);
      setJobStatus('queued');
      setElapsedSeconds(0);
      setCurrentStep(1);
    } catch (err: any) {
      showToast(err.message || 'Lỗi Quick Scan', 'error');
    } finally {
      setIsQuickScanning(false);
    }
  };

  const steps = [
    'Tải lên và trích xuất cấu trúc văn bản (.docx/.pdf)',
    'Cắt đoạn (Chunking) & mã hóa ngữ nghĩa (BGE-M3)',
    'Truy xuất ứng viên đối chứng từ PostgreSQL pgvector',
    'Tái chấm điểm chuyên sâu BGE Cross-Encoder Reranker',
    'Ghép cụm đoạn văn (Passage Alignment) & Kết xuất Báo cáo',
  ];

  return (
    <div className="space-y-6">
      {/* Top Header & Quick Actions Bar */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-2">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">
            Thẩm định Đạo văn Luận văn Tốt nghiệp
          </h1>
          <p className="text-xs text-slate-500 mt-1 max-w-2xl">
            Đối chiếu toàn văn với kho luận văn lưu chiểu bằng mô hình học sâu đa ngữ nghĩa <strong className="text-slate-700 font-semibold">BGE-M3</strong> kết hợp <strong className="text-slate-700 font-semibold">Cross-Encoder Reranker</strong> và <strong className="text-slate-700 font-semibold">Custom Classifier</strong>.
          </p>
        </div>

        {/* Quick Action Buttons */}
        <div className="flex items-center space-x-2.5 flex-shrink-0">
          <button
            onClick={handleQuickScan}
            disabled={isQuickScanning}
            className="inline-flex items-center space-x-1.5 px-3.5 py-2 rounded-xl bg-orange-50 hover:bg-orange-100 text-[#FA7921] border border-orange-200/80 text-xs font-bold shadow-xs transition-all active:scale-[0.98] disabled:opacity-50"
          >
            {isQuickScanning ? (
              <RefreshCw className="w-3.5 h-3.5 animate-spin" />
            ) : (
              <PlayCircle className="w-3.5 h-3.5" />
            )}
            <span>Thử nghiệm 1-Click (suspect_doc.docx)</span>
          </button>

          <a
            href="/static/samples/suspect_doc.docx"
            download
            className="inline-flex items-center space-x-1.5 px-3 py-2 rounded-xl bg-white hover:bg-slate-50 text-slate-600 border border-slate-200 text-xs font-semibold shadow-xs transition-all"
          >
            <Download className="w-3.5 h-3.5" />
            <span>Tải file test mẫu</span>
          </a>
        </div>
      </div>

      {/* 3 Metric Pills */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <div className="p-3.5 rounded-2xl bg-white border border-slate-200/80 shadow-xs flex items-center space-x-3">
          <div className="w-8 h-8 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center flex-shrink-0">
            <Zap className="w-4 h-4" />
          </div>
          <div>
            <p className="text-[10px] uppercase font-bold text-slate-400">Tăng tốc tính toán</p>
            <p className="text-xs font-bold text-slate-800">
              {config?.remote_gpu_enabled ? 'GPU Tesla T4 (Cloud Colab)' : 'CPU Multithread Engine'}
            </p>
          </div>
        </div>

        <div className="p-3.5 rounded-2xl bg-white border border-slate-200/80 shadow-xs flex items-center space-x-3">
          <div className="w-8 h-8 rounded-xl bg-blue-50 text-[#2054A5] flex items-center justify-center flex-shrink-0">
            <ShieldCheck className="w-4 h-4" />
          </div>
          <div>
            <p className="text-[10px] uppercase font-bold text-slate-400">Kho lưu chiểu</p>
            <p className="text-xs font-bold text-slate-800">
              uit_thesis <span className="text-[10px] font-normal text-slate-500">(1024-dim Vector)</span>
            </p>
          </div>
        </div>

        <div className="p-3.5 rounded-2xl bg-white border border-slate-200/80 shadow-xs flex items-center space-x-3">
          <div className="w-8 h-8 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center flex-shrink-0">
            <CheckCircle2 className="w-4 h-4" />
          </div>
          <div>
            <p className="text-[10px] uppercase font-bold text-slate-400">Bộ phân loại ML</p>
            <p className="text-xs font-bold text-slate-800">
              {config?.classifier_enabled ? 'Active Classifier (Giai đoạn B)' : 'Heuristic Rules (Giai đoạn A)'}
            </p>
          </div>
        </div>
      </div>

      {/* Main Grid: Upload Form (7 cols) + Status Tracker/Guide (5 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left: Upload Form Card */}
        <div className="lg:col-span-7 bg-white rounded-3xl border border-slate-200/90 shadow-xs p-6 space-y-5">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <h2 className="text-sm font-bold text-slate-800 flex items-center space-x-2">
              <Upload className="w-4 h-4 text-[#2054A5]" />
              <span>Nộp Tài Liệu Cần Kiểm Tra</span>
            </h2>
            <span className="text-[10px] font-mono font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-600">
              Hỗ trợ: .docx, .pdf
            </span>
          </div>

          <form onSubmit={handleSubmitScan} className="space-y-4">
            {/* File Dropzone */}
            <div
              onClick={() => fileInputRef.current?.click()}
              className={`border-2 border-dashed rounded-2xl p-6 text-center cursor-pointer transition-all ${
                selectedFile
                  ? 'border-blue-400 bg-blue-50/20'
                  : 'border-slate-200 hover:border-[#2054A5] hover:bg-slate-50/50'
              }`}
            >
              <input
                ref={fileInputRef}
                type="file"
                accept=".docx,.pdf"
                onChange={handleFileChange}
                className="hidden"
              />

              {!selectedFile ? (
                <div className="space-y-2 py-2">
                  <div className="w-11 h-11 mx-auto rounded-xl bg-slate-100 text-slate-500 flex items-center justify-center">
                    <Upload className="w-5 h-5 text-[#2054A5]" />
                  </div>
                  <div>
                    <p className="text-xs font-bold text-slate-800">
                      Nhấn để tải tài liệu lên <span className="text-slate-400 font-normal">hoặc kéo thả vào đây</span>
                    </p>
                    <p className="text-[11px] text-slate-400 mt-0.5">
                      Dung lượng tối đa {config?.max_upload_mb || 20}MB (Tự động bóc tách phân trang)
                    </p>
                  </div>
                </div>
              ) : (
                <div className="flex items-center justify-between p-3 bg-white rounded-xl border border-blue-200 shadow-xs">
                  <div className="flex items-center space-x-3 truncate">
                    <div className="w-9 h-9 rounded-lg bg-blue-50 text-[#2054A5] flex items-center justify-center flex-shrink-0">
                      <FileText className="w-5 h-5" />
                    </div>
                    <div className="truncate text-left">
                      <p className="text-xs font-semibold text-slate-800 truncate">{selectedFile.name}</p>
                      <p className="text-[10px] text-slate-400 font-mono">{(selectedFile.size / 1024).toFixed(1)} KB</p>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={handleClearFile}
                    className="p-1.5 text-slate-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>
              )}
            </div>

            {/* Metadata Inputs */}
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Mã Kho (Corpus)</label>
                <input
                  type="text"
                  value={corpusId}
                  onChange={(e) => setCorpusId(e.target.value)}
                  required
                  className="w-full text-xs font-mono px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
                />
              </div>

              <div>
                <div className="flex items-center justify-between mb-1">
                  <label className="text-xs font-semibold text-slate-700">Mã Bài nộp</label>
                  <button
                    type="button"
                    onClick={() => setSubmissionId(generateRandomSubmissionId())}
                    className="text-[10px] text-blue-600 hover:underline font-medium"
                  >
                    Tạo mới
                  </button>
                </div>
                <input
                  type="text"
                  value={submissionId}
                  onChange={(e) => setSubmissionId(e.target.value)}
                  required
                  className="w-full text-xs font-mono px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Phiên bản (Version)</label>
                <input
                  type="number"
                  min="1"
                  value={version}
                  onChange={(e) => setVersion(parseInt(e.target.value) || 1)}
                  required
                  className="w-full text-xs px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
                />
              </div>
            </div>

            {/* Submit Button */}
            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full flex items-center justify-center space-x-2 py-3 rounded-xl bg-[#2054A5] hover:bg-[#184285] text-white font-bold text-xs shadow-md shadow-blue-900/10 transition-all active:scale-[0.99] disabled:opacity-50"
            >
              {isSubmitting ? (
                <>
                  <RefreshCw className="w-4 h-4 animate-spin mr-1" />
                  <span>Đang tải lên và xếp hàng...</span>
                </>
              ) : (
                <>
                  <Scan className="w-4 h-4 mr-1" />
                  <span>Tiến hành Thẩm định Đạo văn</span>
                </>
              )}
            </button>
          </form>
        </div>

        {/* Right: Real-time Tracker OR Academic Standards Guide */}
        <div className="lg:col-span-5 bg-white rounded-3xl border border-slate-200/90 shadow-xs p-6 space-y-5">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <h2 className="text-sm font-bold text-slate-800">
              {activeJobId ? 'Tiến Trình Thời Gian Thực' : 'Quy Chuẩn Thẩm Định Luận Văn'}
            </h2>
            <span
              className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                jobStatus === 'completed'
                  ? 'bg-emerald-100 text-emerald-800'
                  : jobStatus === 'failed'
                  ? 'bg-red-100 text-red-800'
                  : jobStatus === 'waiting_for_compute'
                  ? 'bg-amber-100 text-amber-800 animate-pulse'
                  : jobStatus === 'cancelled'
                  ? 'bg-slate-200 text-slate-700'
                  : activeJobId
                  ? 'bg-blue-100 text-blue-800 animate-pulse'
                  : 'bg-slate-100 text-slate-600'
              }`}
            >
              {jobStatus === 'completed'
                ? 'Hoàn thành'
                : jobStatus === 'failed'
                ? 'Thất bại'
                : jobStatus === 'waiting_for_compute'
                ? 'Chờ GPU kết nối'
                : jobStatus === 'cancelled'
                ? 'Đã huỷ'
                : activeJobId
                ? 'Đang xử lý'
                : 'Sẵn sàng'}
            </span>
          </div>

          {!activeJobId ? (
            /* Academic Guidance when Idle */
            <div className="space-y-4 text-xs">
              <div className="space-y-2.5">
                <p className="font-semibold text-slate-700">Khung thẩm định tỷ lệ tương đồng đề xuất:</p>

                <div className="p-3 rounded-xl bg-emerald-50/70 border border-emerald-200/80 flex items-start space-x-2.5">
                  <span className="w-2 h-2 rounded-full bg-emerald-500 mt-1 flex-shrink-0" />
                  <div>
                    <strong className="text-emerald-950 font-bold">&lt; 15% — Đạt yêu cầu nguyên bản</strong>
                    <p className="text-[11px] text-emerald-800 mt-0.5 leading-relaxed">
                      Nội dung độc lập, trích dẫn chuẩn quy định, đủ điều kiện bảo vệ trước hội đồng.
                    </p>
                  </div>
                </div>

                <div className="p-3 rounded-xl bg-amber-50/70 border border-amber-200/80 flex items-start space-x-2.5">
                  <span className="w-2 h-2 rounded-full bg-amber-500 mt-1 flex-shrink-0" />
                  <div>
                    <strong className="text-amber-950 font-bold">15% – 25% — Cần rà soát trích dẫn</strong>
                    <p className="text-[11px] text-amber-800 mt-0.5 leading-relaxed">
                      GVHD thẩm định ngữ cảnh các đoạn trích dẫn lý thuyết và tổng quan tài liệu.
                    </p>
                  </div>
                </div>

                <div className="p-3 rounded-xl bg-red-50/70 border border-red-200/80 flex items-start space-x-2.5">
                  <span className="w-2 h-2 rounded-full bg-red-500 mt-1 flex-shrink-0" />
                  <div>
                    <strong className="text-red-950 font-bold">&gt; 25% — Trùng lặp cao (Cảnh báo)</strong>
                    <p className="text-[11px] text-red-800 mt-0.5 leading-relaxed">
                      Phát hiện sao chép nguyên văn hoặc diễn giải ý tưởng diện rộng, yêu cầu giải trình.
                    </p>
                  </div>
                </div>
              </div>

              <div className="pt-3 border-t border-slate-100 text-[11px] text-slate-400 flex items-center space-x-1.5">
                <HelpCircle className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
                <span>Hệ thống phân tích từng đoạn 90 từ trắng với độ chồng lấn 20 từ.</span>
              </div>
            </div>
          ) : (
            /* Active Job Real-Time Stepper */
            <div className="space-y-4">
              <div className="bg-slate-50 border border-slate-100 rounded-2xl p-3 text-xs space-y-1">
                <div className="flex items-center justify-between">
                  <span className="text-slate-500">Job ID:</span>
                  <span className="font-mono font-bold text-slate-800 text-[11px]">{activeJobId}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-slate-500">Mã bài nộp:</span>
                  <span className="font-mono font-semibold text-blue-700 text-[11px]">{trackingSubmissionId}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-slate-500">Thời gian chạy:</span>
                  <span className="font-mono font-bold text-slate-800">{elapsedSeconds}s</span>
                </div>
              </div>

              {/* Progress Bar */}
              <div className="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                <div
                  className="bg-[#2054A5] h-full rounded-full transition-all duration-500"
                  style={{
                    width: jobStatus === 'completed' ? '100%' : `${Math.min(currentStep * 20, 95)}%`,
                  }}
                />
              </div>

              {/* Stepper items */}
              <div className="space-y-3 pt-1">
                {steps.map((text, idx) => {
                  const stepNum = idx + 1;
                  const isDone = jobStatus === 'completed' || currentStep > stepNum;
                  const isCurrent = jobStatus !== 'completed' && currentStep === stepNum;

                  return (
                    <div key={idx} className="flex items-start space-x-3 text-xs">
                      <div
                        className={`w-5 h-5 rounded-full flex items-center justify-center flex-shrink-0 text-[10px] font-bold transition-all ${
                          isDone
                            ? 'bg-emerald-500 text-white'
                            : isCurrent
                            ? 'bg-[#2054A5] text-white animate-pulse'
                            : 'bg-slate-100 text-slate-400'
                        }`}
                      >
                        {isDone ? '✓' : stepNum}
                      </div>
                      <span
                        className={`leading-relaxed pt-0.5 ${
                          isDone
                            ? 'text-slate-700 font-medium'
                            : isCurrent
                            ? 'text-slate-900 font-bold'
                            : 'text-slate-400'
                        }`}
                      >
                        {text}
                      </span>
                    </div>
                  );
                })}
              </div>

              {/* Action button when completed */}
              {jobStatus === 'completed' && (
                <div className="pt-2">
                  <button
                    onClick={() => onViewReport(activeJobId)}
                    className="w-full flex items-center justify-center space-x-2 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs shadow-sm transition-all"
                  >
                    <span>Xem Báo cáo Chi tiết</span>
                    <ArrowRight className="w-4 h-4" />
                  </button>
                </div>
              )}

              {jobStatus === 'failed' && (
                <div className="p-3 bg-red-50 border border-red-200 rounded-xl flex items-center space-x-2 text-xs text-red-700">
                  <AlertCircle className="w-4 h-4 flex-shrink-0" />
                  <span>Tác vụ thất bại. Vui lòng kiểm tra lại file hoặc log.</span>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
