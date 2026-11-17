import React from 'react';
import { ShieldCheck, FileSearch, Library, BarChart3, History, Cpu, Key, Zap } from 'lucide-react';
import { ActiveTab, ClientConfig } from '../types';

interface NavbarProps {
  activeTab: ActiveTab;
  onTabChange: (tab: ActiveTab) => void;
  config: ClientConfig | null;
  onOpenApiKeyModal: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({
  activeTab,
  onTabChange,
  config,
  onOpenApiKeyModal,
}) => {
  const tabs = [
    { id: 'scan' as ActiveTab, label: 'Kiểm tra bài nộp', icon: FileSearch },
    { id: 'corpus' as ActiveTab, label: 'Kho tham chiếu', icon: Library },
    { id: 'report' as ActiveTab, label: 'Báo cáo thẩm định', icon: BarChart3 },
    { id: 'history' as ActiveTab, label: 'Lịch sử tác vụ', icon: History },
    { id: 'system' as ActiveTab, label: 'Hệ thống AI', icon: Cpu },
  ];

  return (
    <header className="sticky top-0 z-40 bg-white/95 backdrop-blur-md border-b border-slate-200/80 shadow-xs">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Logo Brand */}
          <div
            className="flex items-center space-x-3 cursor-pointer group"
            onClick={() => onTabChange('scan')}
          >
            <div className="w-9 h-9 rounded-xl bg-[#2054A5] text-white flex items-center justify-center shadow-md shadow-blue-900/10 transition-transform group-hover:scale-105">
              <ShieldCheck className="w-5 h-5 text-white stroke-[2.2]" />
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <span className="text-lg font-bold tracking-tight text-slate-900">
                  <span className="text-[#2054A5]">UGTM</span> <span className="text-[#FA7921]">AI</span>
                </span>
                <span className="px-2 py-0.5 text-[10px] font-mono font-semibold rounded-md bg-slate-100 text-slate-600 border border-slate-200">
                  v0.1
                </span>
              </div>
              <p className="text-[11px] text-slate-400 font-medium hidden sm:block">
                Hệ thống Thẩm định Đạo văn Luận văn Tốt nghiệp
              </p>
            </div>
          </div>

          {/* Nav Tabs */}
          <nav className="hidden md:flex items-center space-x-1 bg-slate-100/80 p-1 rounded-xl border border-slate-200/60">
            {tabs.map((tab) => {
              const Icon = tab.icon;
              const isActive = activeTab === tab.id;
              return (
                <button
                  key={tab.id}
                  onClick={() => onTabChange(tab.id)}
                  className={`flex items-center space-x-2 px-3.5 py-1.5 text-xs font-semibold rounded-lg transition-all ${
                    isActive
                      ? 'bg-white text-[#2054A5] shadow-xs font-bold'
                      : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/50'
                  }`}
                >
                  <Icon className={`w-3.5 h-3.5 ${isActive ? 'text-[#2054A5]' : 'text-slate-400'}`} />
                  <span>{tab.label}</span>
                </button>
              );
            })}
          </nav>

          {/* Right Status Indicators */}
          <div className="flex items-center space-x-2.5">
            {config?.remote_gpu_enabled ? (
              <div className="flex items-center space-x-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-purple-50 text-purple-700 border border-purple-200">
                <Zap className="w-3 h-3 text-purple-600 fill-purple-600 animate-pulse" />
                <span className="hidden sm:inline">GPU Tesla T4 (Online)</span>
                <span className="sm:hidden">T4 GPU</span>
              </div>
            ) : (
              <div className="hidden sm:flex items-center space-x-2 px-3 py-1 rounded-full text-xs font-medium bg-slate-100 text-slate-600 border border-slate-200">
                <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
                <span>Local CPU Ready</span>
              </div>
            )}

            <button
              onClick={onOpenApiKeyModal}
              className="p-2 text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg transition-colors border border-transparent hover:border-slate-200"
              title="Cài đặt API Key"
            >
              <Key className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>
    </header>
  );
};
