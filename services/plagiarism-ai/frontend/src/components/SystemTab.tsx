import React, { useState } from 'react';
import { Cpu, Server, Key, Zap } from 'lucide-react';
import { ClientConfig } from '../types';
import { setApiKey } from '../api/client';

interface SystemTabProps {
  config: ClientConfig | null;
  showToast: (msg: string, type: 'success' | 'error' | 'info' | 'warning') => void;
  onRefreshConfig: () => void;
}

export const SystemTab: React.FC<SystemTabProps> = ({ config, showToast, onRefreshConfig }) => {
  const [apiKeyInput, setApiKeyInput] = useState(config?.api_key || '');

  const handleSaveKey = () => {
    if (!apiKeyInput.trim()) {
      showToast('Khóa API không được để trống!', 'warning');
      return;
    }
    setApiKey(apiKeyInput.trim());
    showToast('Đã lưu khóa API vào bộ nhớ trình duyệt!', 'success');
    onRefreshConfig();
  };

  return (
    <div className="space-y-6">
      {/* Top Header */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 sm:p-7">
        <div className="flex items-center space-x-2">
          <Cpu className="w-5 h-5 text-[#2054A5]" />
          <h1 className="text-xl font-extrabold text-slate-900">Thông Số & Cấu Hình Hệ Thống</h1>
        </div>
        <p className="text-xs text-slate-500 mt-1">
          Giám sát trạng thái vận hành của các mô hình học sâu và dịch vụ tăng tốc suy luận.
        </p>
      </div>

      {/* Two Column Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Left: AI Pipeline Architecture */}
        <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 space-y-4">
          <h2 className="text-sm font-bold text-slate-800 flex items-center space-x-2 pb-3 border-b border-slate-100">
            <Server className="w-4 h-4 text-[#2054A5]" />
            <span>Kiến Trúc Mô Hình AI (Hybrid 3 Tầng)</span>
          </h2>

          <div className="space-y-3 text-xs">
            <div className="flex items-center justify-between p-3 rounded-2xl bg-slate-50 border border-slate-100">
              <span className="text-slate-600 font-medium">Mô hình Mã hóa (Embedding):</span>
              <span className="font-mono font-bold text-slate-800">{config?.embedding_model || 'BAAI/bge-m3'}</span>
            </div>

            <div className="flex items-center justify-between p-3 rounded-2xl bg-slate-50 border border-slate-100">
              <span className="text-slate-600 font-medium">Mô hình Tái chấm điểm (Reranker):</span>
              <span className="font-mono font-bold text-slate-800">
                {config?.reranker_model || 'BAAI/bge-reranker-v2-m3'}
              </span>
            </div>

            <div className="flex items-center justify-between p-3 rounded-2xl bg-slate-50 border border-slate-100">
              <span className="text-slate-600 font-medium">Phân loại mở rộng (Classifier):</span>
              <span
                className={`font-mono font-bold px-2 py-0.5 rounded ${
                  config?.classifier_enabled
                    ? 'bg-purple-100 text-purple-800 border border-purple-200'
                    : 'bg-slate-200 text-slate-700'
                }`}
              >
                {config?.classifier_enabled ? 'Active Classifier (Giai đoạn B)' : 'Heuristic Rules (Giai đoạn A)'}
              </span>
            </div>

            <div className="flex items-center justify-between p-3 rounded-2xl bg-slate-50 border border-slate-100">
              <span className="text-slate-600 font-medium">Giới hạn tải lên tối đa:</span>
              <span className="font-mono font-bold text-slate-800">{config?.max_upload_mb || 20} MB</span>
            </div>
          </div>
        </div>

        {/* Right: Accelerator Status & API Key */}
        <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 space-y-4">
          <h2 className="text-sm font-bold text-slate-800 flex items-center space-x-2 pb-3 border-b border-slate-100">
            <Zap className="w-4 h-4 text-purple-600" />
            <span>Trạng Thái Tăng Tốc (Hardware Accelerator)</span>
          </h2>

          <div className="p-4 rounded-2xl border space-y-2 text-xs bg-slate-50 border-slate-100">
            <div className="flex items-center justify-between">
              <span className="font-medium text-slate-600">Môi trường tính toán:</span>
              <span
                className={`font-bold px-2.5 py-0.5 rounded-full flex items-center space-x-1 ${
                  config?.remote_gpu_enabled
                    ? 'bg-purple-100 text-purple-800'
                    : 'bg-slate-200 text-slate-700'
                }`}
              >
                {config?.remote_gpu_enabled && <Zap className="w-3 h-3 text-purple-600 fill-purple-600 mr-1" />}
                <span>{config?.remote_gpu_enabled ? 'Remote GPU (Google Colab T4)' : 'Local CPU Engine'}</span>
              </span>
            </div>

            {config?.remote_gpu_enabled && (
              <div className="pt-2 border-t border-slate-200/60 flex items-center justify-between text-[11px]">
                <span className="text-slate-500">Endpoint URL:</span>
                <span className="font-mono text-purple-700 font-bold truncate max-w-xs">{config.remote_gpu_url}</span>
              </div>
            )}
          </div>

          {/* API Key Box */}
          <div className="pt-2 space-y-2">
            <h3 className="text-xs font-bold text-slate-800 flex items-center space-x-1.5">
              <Key className="w-3.5 h-3.5 text-slate-500" />
              <span>Khóa Xác Thực API (X-API-Key)</span>
            </h3>
            <p className="text-[11px] text-slate-500">
              Đính kèm tự động vào mỗi yêu cầu HTTP gửi đến FastAPI backend.
            </p>
            <div className="flex items-center space-x-2">
              <input
                type="password"
                value={apiKeyInput}
                onChange={(e) => setApiKeyInput(e.target.value)}
                className="flex-1 text-xs font-mono px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
              />
              <button
                onClick={handleSaveKey}
                className="px-4 py-2 bg-[#2054A5] hover:bg-[#184285] text-white text-xs font-bold rounded-xl transition-all"
              >
                Lưu
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
