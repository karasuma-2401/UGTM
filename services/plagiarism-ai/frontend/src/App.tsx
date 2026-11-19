import React, { useState, useEffect } from 'react';
import { Navbar } from './components/Navbar';
import { ScanTab } from './components/ScanTab';
import { ReportTab } from './components/ReportTab';
import { CorpusTab } from './components/CorpusTab';
import { HistoryTab } from './components/HistoryTab';
import { SystemTab } from './components/SystemTab';
import { ApiKeyModal } from './components/ApiKeyModal';
import { ActiveTab, ClientConfig, ReportData } from './types';
import { fetchClientConfig, fetchJobReport } from './api/client';
import { CheckCircle2, AlertCircle, Info, AlertTriangle } from 'lucide-react';

interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info' | 'warning';
}

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState<ActiveTab>('scan');
  const [config, setConfig] = useState<ClientConfig | null>(null);
  const [isApiKeyModalOpen, setIsApiKeyModalOpen] = useState(false);

  // Report state
  const [currentReport, setCurrentReport] = useState<ReportData | null>(null);
  const [reportLoading, setReportLoading] = useState(false);
  const [latestJobId, setLatestJobId] = useState<string | null>(null);

  // Toasts
  const [toasts, setToasts] = useState<Toast[]>([]);

  const showToast = (message: string, type: Toast['type'] = 'info') => {
    const id = Math.random().toString(36).substring(2, 9);
    setToasts((prev) => [...prev, { id, message, type }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 4000);
  };

  const loadConfig = async () => {
    try {
      const data = await fetchClientConfig();
      setConfig(data);
    } catch (err: any) {
      console.warn('Config load warning:', err);
    }
  };

  useEffect(() => {
    loadConfig();
  }, []);

  const handleViewReport = async (jobId: string) => {
    setLatestJobId(jobId);
    setReportLoading(true);
    setActiveTab('report');
    try {
      const data = await fetchJobReport(jobId);
      setCurrentReport(data);
      showToast('Đã tải báo cáo thẩm định chi tiết!', 'success');
    } catch (err: any) {
      showToast(err.message || 'Lỗi tải báo cáo', 'error');
    } finally {
      setReportLoading(false);
    }
  };

  const handleRefreshLatestReport = () => {
    if (latestJobId) {
      handleViewReport(latestJobId);
    } else {
      showToast('Chưa có tác vụ quét nào được thực thi trong phiên này.', 'info');
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-slate-50 font-sans selection:bg-orange-100 selection:text-orange-900">
      {/* Top Sticky Navigation */}
      <Navbar
        activeTab={activeTab}
        onTabChange={setActiveTab}
        config={config}
        onOpenApiKeyModal={() => setIsApiKeyModalOpen(true)}
      />

      {/* Main Content Viewport */}
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8 transition-all">
        {activeTab === 'scan' && (
          <ScanTab
            config={config}
            onViewReport={handleViewReport}
            showToast={showToast}
          />
        )}

        {activeTab === 'corpus' && (
          <CorpusTab showToast={showToast} />
        )}

        {activeTab === 'report' && (
          <ReportTab
            report={currentReport}
            isLoading={reportLoading}
            onRefreshLatest={handleRefreshLatestReport}
            showToast={showToast}
          />
        )}

        {activeTab === 'history' && (
          <HistoryTab
            onViewReport={handleViewReport}
            showToast={showToast}
          />
        )}

        {activeTab === 'system' && (
          <SystemTab
            config={config}
            showToast={showToast}
            onRefreshConfig={loadConfig}
          />
        )}
      </main>

      {/* Footer */}
      <footer className="mt-auto border-t border-slate-200 bg-white py-6 text-center text-xs text-slate-500">
        <div className="max-w-7xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-between gap-2">
          <p>© 2026 UGTM AI — Đồ án Thẩm định Đạo văn Luận văn Tốt nghiệp UIT (ĐHQG-HCM).</p>
          <div className="flex items-center space-x-3 text-slate-400">
            <span>FastAPI Backend</span>
            <span>•</span>
            <span>React + Tailwind + Vite</span>
            <span>•</span>
            <span>BGE-M3 Hybrid AI</span>
          </div>
        </div>
      </footer>

      {/* API Key Modal */}
      <ApiKeyModal
        isOpen={isApiKeyModalOpen}
        onClose={() => setIsApiKeyModalOpen(false)}
        onSaved={loadConfig}
      />

      {/* Toast Notifications */}
      <div className="fixed bottom-5 right-5 z-50 space-y-2 pointer-events-none">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={`pointer-events-auto flex items-center space-x-2.5 px-4 py-3 rounded-2xl shadow-xl text-xs font-semibold backdrop-blur-md border transition-all animate-in slide-in-from-bottom-3 duration-200 ${
              toast.type === 'success'
                ? 'bg-emerald-50/95 text-emerald-900 border-emerald-200'
                : toast.type === 'error'
                ? 'bg-red-50/95 text-red-900 border-red-200'
                : toast.type === 'warning'
                ? 'bg-amber-50/95 text-amber-900 border-amber-200'
                : 'bg-blue-50/95 text-blue-900 border-blue-200'
            }`}
          >
            {toast.type === 'success' && <CheckCircle2 className="w-4 h-4 text-emerald-600 flex-shrink-0" />}
            {toast.type === 'error' && <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />}
            {toast.type === 'warning' && <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0" />}
            {toast.type === 'info' && <Info className="w-4 h-4 text-blue-600 flex-shrink-0" />}
            <span>{toast.message}</span>
          </div>
        ))}
      </div>
    </div>
  );
};

export default App;

