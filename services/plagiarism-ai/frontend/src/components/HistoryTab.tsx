import React, { useState, useEffect } from 'react';
import { History, RefreshCw, ArrowUpRight, CheckCircle2, Clock, XCircle, FileText } from 'lucide-react';
import { JobRecord } from '../types';
import { fetchJobList } from '../api/client';

interface HistoryTabProps {
  onViewReport: (jobId: string) => void;
  showToast: (msg: string, type: 'success' | 'error' | 'info' | 'warning') => void;
}

export const HistoryTab: React.FC<HistoryTabProps> = ({ onViewReport, showToast }) => {
  const [jobs, setJobs] = useState<JobRecord[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [filterType, setFilterType] = useState<string>('all');

  const loadJobs = async () => {
    setIsLoading(true);
    try {
      const data = await fetchJobList(50);
      setJobs(data);
    } catch (err: any) {
      showToast(err.message || 'Lỗi nạp lịch sử tác vụ', 'error');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadJobs();
    const interval = setInterval(loadJobs, 10000);
    return () => clearInterval(interval);
  }, []);

  const filteredJobs = jobs.filter((job) => {
    if (filterType === 'all') return true;
    return job.job_type === filterType;
  });

  return (
    <div className="space-y-6">
      {/* Header bar */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 sm:p-7 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="flex items-center space-x-2">
            <History className="w-5 h-5 text-[#2054A5]" />
            <h1 className="text-xl font-extrabold text-slate-900">Lịch Sử Quét & Nạp Tài Liệu</h1>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Theo dõi toàn bộ các lượt quét bài và nạp nguồn đã lưu trong cơ sở dữ liệu.
          </p>
        </div>

        <div className="flex items-center space-x-3">
          <select
            value={filterType}
            onChange={(e) => setFilterType(e.target.value)}
            className="px-3 py-2 rounded-xl border border-slate-200 text-xs font-semibold text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
          >
            <option value="all">Tất cả tác vụ ({jobs.length})</option>
            <option value="scan">Chỉ bài quét (Scan)</option>
            <option value="index">Chỉ nạp nguồn (Index)</option>
          </select>

          <button
            onClick={loadJobs}
            disabled={isLoading}
            className="inline-flex items-center space-x-2 px-3.5 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold transition-all disabled:opacity-50"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            <span>Làm mới danh sách</span>
          </button>
        </div>
      </div>

      {/* Jobs Table */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200 text-slate-500 font-bold uppercase text-[10px] tracking-wider">
                <th className="py-3.5 px-5">Loại Tác Vụ</th>
                <th className="py-3.5 px-5">Mã Tài Liệu</th>
                <th className="py-3.5 px-5">Tên File</th>
                <th className="py-3.5 px-5">Trạng Thái</th>
                <th className="py-3.5 px-5">Thời Gian</th>
                <th className="py-3.5 px-5 text-right">Thao Tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 font-sans">
              {filteredJobs.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-12 text-center text-slate-400">
                    Chưa có lịch sử tác vụ nào.
                  </td>
                </tr>
              ) : (
                filteredJobs.map((job) => {
                  const isCompleted = job.status === 'completed';
                  const isFailed = job.status === 'failed';
                  const isWaitingForCompute = job.status === 'waiting_for_compute';
                  const isCancelled = job.status === 'cancelled';
                  const isProcessing = job.status === 'processing' || job.status === 'queued';

                  return (
                    <tr key={job.job_id} className="hover:bg-slate-50/70 transition-colors">
                      <td className="py-3 px-5">
                        <span
                          className={`inline-block px-2.5 py-0.5 text-[10px] font-bold rounded-full uppercase ${
                            job.job_type === 'scan'
                              ? 'bg-blue-100 text-blue-800'
                              : 'bg-emerald-100 text-emerald-800'
                          }`}
                        >
                          {job.job_type === 'scan' ? 'Quét Bài' : 'Nạp Nguồn'}
                        </span>
                      </td>

                      <td className="py-3 px-5 font-mono font-bold text-slate-800">
                        {job.document_key} <span className="text-slate-400 font-normal">(v{job.version})</span>
                      </td>

                      <td className="py-3 px-5 text-slate-700 flex items-center space-x-1.5 font-medium truncate max-w-xs">
                        <FileText className="w-4 h-4 text-slate-400 flex-shrink-0" />
                        <span className="truncate">{job.filename}</span>
                      </td>

                      <td className="py-3 px-5">
                        <span
                          className={`inline-flex items-center space-x-1 px-2.5 py-0.5 text-[10px] font-bold rounded-full ${
                            isCompleted
                              ? 'bg-emerald-100 text-emerald-800'
                              : isFailed
                              ? 'bg-red-100 text-red-800'
                              : isWaitingForCompute
                              ? 'bg-amber-100 text-amber-800 animate-pulse'
                              : isCancelled
                              ? 'bg-slate-200 text-slate-700'
                              : 'bg-blue-100 text-blue-800 animate-pulse'
                          }`}
                        >
                          {isCompleted ? (
                            <CheckCircle2 className="w-3 h-3 text-emerald-600 mr-0.5" />
                          ) : isFailed ? (
                            <XCircle className="w-3 h-3 text-red-600 mr-0.5" />
                          ) : isWaitingForCompute ? (
                            <Clock className="w-3 h-3 text-amber-600 mr-0.5" />
                          ) : (
                            <Clock className="w-3 h-3 text-blue-600 mr-0.5" />
                          )}
                          <span>
                            {isCompleted
                              ? 'completed'
                              : isFailed
                              ? `failed ${job.error_code ? `(${job.error_code})` : ''}`
                              : isWaitingForCompute
                              ? `Chờ GPU (${job.attempt || 1}/${job.max_attempts || 5})`
                              : isCancelled
                              ? 'Đã huỷ'
                              : job.status}
                          </span>
                        </span>
                      </td>

                      <td className="py-3 px-5 text-slate-500 font-mono text-[11px]">
                        {job.created_at ? new Date(job.created_at).toLocaleString('vi-VN') : '---'}
                      </td>

                      <td className="py-3 px-5 text-right">
                        {job.job_type === 'scan' && isCompleted ? (
                          <button
                            onClick={() => onViewReport(job.job_id)}
                            className="inline-flex items-center space-x-1 px-3 py-1 rounded-lg bg-blue-50 hover:bg-blue-100 text-blue-700 text-xs font-bold transition-all"
                          >
                            <span>Xem Báo Cáo</span>
                            <ArrowUpRight className="w-3 h-3" />
                          </button>
                        ) : isWaitingForCompute ? (
                          <span className="text-[11px] text-amber-600 font-medium">
                            Đang retry...
                          </span>
                        ) : isProcessing ? (
                          <span className="text-[11px] text-blue-600 font-medium animate-pulse">
                            Đang xử lý...
                          </span>
                        ) : (
                          <span className="text-slate-400 text-xs">---</span>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
