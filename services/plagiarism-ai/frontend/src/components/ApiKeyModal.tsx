import React, { useState } from 'react';
import { Key, X } from 'lucide-react';
import { getApiKey, setApiKey } from '../api/client';

interface ApiKeyModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSaved: () => void;
}

export const ApiKeyModal: React.FC<ApiKeyModalProps> = ({ isOpen, onClose, onSaved }) => {
  const [key, setKey] = useState(getApiKey());

  if (!isOpen) return null;

  const handleSave = () => {
    setApiKey(key.trim());
    onSaved();
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="bg-white rounded-3xl max-w-md w-full p-6 shadow-2xl border border-slate-200 space-y-4 animate-in fade-in zoom-in-95 duration-200">
        <div className="flex items-center justify-between pb-2 border-b border-slate-100">
          <div className="flex items-center space-x-2">
            <div className="w-8 h-8 rounded-lg bg-blue-50 text-[#2054A5] flex items-center justify-center">
              <Key className="w-4 h-4" />
            </div>
            <h3 className="text-sm font-bold text-slate-900">Cài đặt Khóa API</h3>
          </div>
          <button onClick={onClose} className="p-1 text-slate-400 hover:text-slate-600 rounded-lg">
            <X className="w-5 h-5" />
          </button>
        </div>

        <p className="text-xs text-slate-500 leading-relaxed">
          Khóa này được đính kèm vào header <code className="bg-slate-100 text-slate-800 px-1 py-0.5 rounded font-mono text-[11px]">X-API-Key</code> trong mỗi yêu cầu gửi lên hệ thống.
        </p>

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">X-API-Key</label>
          <input
            type="text"
            value={key}
            onChange={(e) => setKey(e.target.value)}
            className="w-full text-xs font-mono px-3 py-2.5 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
          />
        </div>

        <div className="flex justify-end space-x-2 pt-2">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-100 rounded-xl transition-colors"
          >
            Đóng
          </button>
          <button
            onClick={handleSave}
            className="px-5 py-2 text-xs font-bold text-white bg-[#2054A5] hover:bg-[#184285] rounded-xl shadow-md transition-all"
          >
            Lưu thay đổi
          </button>
        </div>
      </div>
    </div>
  );
};

