import { useState } from 'react';
import {
  Search,
  Mail,
  Lock,
  Eye,
  EyeOff,
  Plus,
  Download,
  Sparkles,
  ArrowRight,
  ChevronRight,
  Trash2,
  Clock,
  CheckCircle2,
  AlertTriangle,
  ExternalLink,
  Send,
  Save,
  RefreshCw,
  Info,
  Check,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';

export function UiTestPage() {
  // Interactive test states
  const [globalLoading, setGlobalLoading] = useState(false);
  const [btnLoading, setBtnLoading] = useState(false);
  const [testPassword, setTestPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [interactiveInput, setInteractiveInput] = useState('');
  const [simulateError, setSimulateError] = useState(false);

  const handleSimulateSubmit = () => {
    setBtnLoading(true);
    setTimeout(() => {
      setBtnLoading(false);
    }, 2500);
  };

  return (
    <div className="space-y-10 pb-16">
      {/* Page Header */}
      <div className="border-b border-slate-200 pb-5">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold text-primary uppercase tracking-wider">
              <span>Hệ thống Component Core</span>
              <span>•</span>
              <span>Giai đoạn 1</span>
            </div>
            <h1 className="text-2xl font-bold text-slate-900 mt-1">
              UI Playground & Component Testing
            </h1>
            <p className="text-sm text-slate-500 mt-1">
              Trang kiểm thử trực quan các tính năng động của Button, Input và Badge.
            </p>
          </div>

          <div className="flex items-center gap-3 bg-slate-100 p-2 rounded-xl">
            <span className="text-xs font-medium text-slate-600 pl-1">Mô phỏng Loading:</span>
            <Button
              size="sm"
              variant={globalLoading ? 'destructive' : 'default'}
              onClick={() => setGlobalLoading((prev) => !prev)}
              leftIcon={<RefreshCw className={globalLoading ? 'animate-spin' : ''} />}
            >
              {globalLoading ? 'Tắt Loading' : 'Bật Loading toàn cục'}
            </Button>
          </div>
        </div>
      </div>

      {/* ========================================================================= */}
      {/* 1. BUTTON COMPONENT TESTS */}
      {/* ========================================================================= */}
      <section className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm space-y-6">
        <div className="flex items-center justify-between border-b border-slate-100 pb-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <span>1. Button Component</span>
              <Badge variant="info">Đầy đủ Variants & Icons</Badge>
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Kiểm tra icon linh hoạt (leftIcon, rightIcon, hoặc không icon), kích thước và trạng
              thái isLoading
            </p>
          </div>
        </div>

        {/* 1.1 Variants */}
        <div className="space-y-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            A. Các biến thể màu sắc (Variants)
          </h3>
          <div className="flex flex-wrap items-center gap-3">
            <Button variant="default">Default (Primary)</Button>
            <Button variant="secondary">Secondary</Button>
            <Button variant="outline">Outline</Button>
            <Button variant="destructive">Destructive</Button>
            <Button variant="ghost">Ghost</Button>
            <Button variant="link">Link Style</Button>
          </div>
        </div>

        {/* 1.2 Sizes */}
        <div className="space-y-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            B. Kích thước (Sizes)
          </h3>
          <div className="flex flex-wrap items-center gap-3">
            <Button size="sm">Small (h-8)</Button>
            <Button size="default">Default (h-9)</Button>
            <Button size="lg">Large (h-10)</Button>
            <Button size="icon" variant="outline" aria-label="Icon only">
              <Sparkles />
            </Button>
          </div>
        </div>

        {/* 1.3 Icon Placements (Optional test) */}
        <div className="space-y-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            C. Vị trí Icon (leftIcon / rightIcon / Không icon)
          </h3>
          <div className="flex flex-wrap items-center gap-3">
            <Button leftIcon={<Plus />}>Chỉ có leftIcon</Button>
            <Button rightIcon={<ArrowRight />}>Chỉ có rightIcon</Button>
            <Button leftIcon={<Download />} rightIcon={<ChevronRight />} variant="secondary">
              Có cả 2 Icon
            </Button>
            <Button variant="outline">Hoàn toàn không có Icon</Button>
            <Button variant="destructive" leftIcon={<Trash2 />}>
              Xóa đề tài
            </Button>
          </div>
        </div>

        {/* 1.4 Loading State */}
        <div className="space-y-3 bg-slate-50 p-4 rounded-xl border border-slate-150">
          <h3 className="text-xs font-bold text-slate-700 uppercase tracking-wider flex items-center justify-between">
            <span>D. Kiểm tra trạng thái đang tải (isLoading)</span>
            <span className="text-[11px] font-normal text-slate-500">
              {btnLoading ? '⏳ Đang giả lập tải trong 2.5s...' : 'Sẵn sàng'}
            </span>
          </h3>
          <div className="flex flex-wrap items-center gap-3">
            <Button
              onClick={handleSimulateSubmit}
              isLoading={btnLoading || globalLoading}
              leftIcon={<Save />}
              loadingText="Đang lưu dữ liệu..."
            >
              Bấm thử để kích hoạt Loading
            </Button>

            <Button variant="outline" isLoading={globalLoading} leftIcon={<Send />}>
              Loading giữ nguyên chữ
            </Button>

            <Button variant="destructive" isLoading={globalLoading} leftIcon={<Trash2 />}>
              Xóa có loading
            </Button>

            <Button disabled leftIcon={<Lock />}>
              Nút bị Disabled cố định
            </Button>
          </div>
        </div>
      </section>

      {/* ========================================================================= */}
      {/* 2. INPUT COMPONENT TESTS */}
      {/* ========================================================================= */}
      <section className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm space-y-6">
        <div className="flex items-center justify-between border-b border-slate-100 pb-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <span>2. Input Component</span>
              <Badge variant="info">Icon Prefix/Suffix & Validation</Badge>
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Hỗ trợ leftIcon, rightIcon tùy chọn, text hướng dẫn helperText và trạng thái báo lỗi
              error
            </p>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {/* 2.1 Basic & No Icon */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-700">A. Input cơ bản (Không Icon)</label>
            <Input placeholder="Nhập tên đề tài khóa luận..." />
          </div>

          {/* 2.2 Left Icon only */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-700">
              B. Có Left Icon (Kính lúp tìm kiếm)
            </label>
            <Input
              leftIcon={<Search />}
              placeholder="Tìm kiếm sinh viên, giảng viên theo mã hoặc tên..."
            />
          </div>

          {/* 2.3 Right Icon only */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-700">C. Có Right Icon tương tác</label>
            <Input
              placeholder="Nhập địa chỉ email cá nhân..."
              rightIcon={<Mail />}
              defaultValue="nguyenvana@gmail.com"
            />
          </div>

          {/* 2.4 Interactive Password toggle */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-700">
              D. Cả Left Icon và Right Icon (Ô mật khẩu có nút mắt)
            </label>
            <Input
              type={showPassword ? 'text' : 'password'}
              value={testPassword}
              onChange={(e) => setTestPassword(e.target.value)}
              placeholder="Nhập mật khẩu kiểm thử..."
              leftIcon={<Lock />}
              rightIcon={
                <button
                  type="button"
                  tabIndex={-1}
                  onClick={() => setShowPassword((prev) => !prev)}
                  className="hover:text-slate-800 transition-colors cursor-pointer"
                >
                  {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              }
            />
          </div>

          {/* 2.5 Helper text */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-700">E. Input có Helper Text</label>
            <Input
              placeholder="24521186"
              helperText="Mã số sinh viên định danh gồm 8 chữ số do phòng Đào tạo cấp."
            />
          </div>

          {/* 2.6 Disabled state */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-700">
              F. Input Disabled (Khóa nhập)
            </label>
            <Input
              disabled
              leftIcon={<Lock />}
              defaultValue="Dữ liệu chỉ đọc, không thể chỉnh sửa"
            />
          </div>
        </div>

        {/* 2.7 Interactive Error Validation Test */}
        <div className="bg-slate-50 p-4 rounded-xl border border-slate-150 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-xs font-bold text-slate-700 uppercase tracking-wider">
              G. Trải nghiệm tương tác trạng thái Lỗi (Validation Error)
            </h3>
            <Button size="sm" variant="outline" onClick={() => setSimulateError((prev) => !prev)}>
              {simulateError ? 'Tắt cờ lỗi giả lập' : 'Bật cờ lỗi giả lập'}
            </Button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <Input
                value={interactiveInput}
                onChange={(e) => setInteractiveInput(e.target.value)}
                placeholder="Gõ ít hơn 6 ký tự để kích hoạt lỗi..."
                leftIcon={<Info />}
                error={
                  simulateError
                    ? 'Lỗi giả lập: Dữ liệu này không hợp lệ trong hệ thống!'
                    : interactiveInput.length > 0 && interactiveInput.length < 6
                      ? 'Mã đề tài quá ngắn (cần tối thiểu 6 ký tự)'
                      : undefined
                }
              />
            </div>
            <div className="text-xs text-slate-500 flex items-center">
              💡 Hãy thử gõ vào ô trên: khi độ dài từ 1 đến 5 ký tự, component sẽ tự động chuyển
              viền đỏ và hiển thị thông báo lỗi bên dưới. Khi đủ từ 6 ký tự trở lên, lỗi sẽ tự biến
              mất.
            </div>
          </div>
        </div>
      </section>

      {/* ========================================================================= */}
      {/* 3. BADGE COMPONENT TESTS */}
      {/* ========================================================================= */}
      <section className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm space-y-6">
        <div className="flex items-center justify-between border-b border-slate-100 pb-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <span>3. Badge Component</span>
              <Badge variant="success" dot>
                Trạng thái trực quan
              </Badge>
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Hỗ trợ đầy đủ các màu sắc nghiệp vụ, chấm trạng thái dot và icon đi kèm
            </p>
          </div>
        </div>

        {/* 3.1 All Variants */}
        <div className="space-y-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            A. Các biến thể màu sắc (Variants)
          </h3>
          <div className="flex flex-wrap items-center gap-2.5">
            <Badge variant="default">Default</Badge>
            <Badge variant="secondary">Secondary</Badge>
            <Badge variant="success">Success (Thành công)</Badge>
            <Badge variant="warning">Warning (Cảnh báo)</Badge>
            <Badge variant="info">Info (Thông tin mới)</Badge>
            <Badge variant="destructive">Destructive (Từ chối)</Badge>
            <Badge variant="outline">Outline</Badge>
          </div>
        </div>

        {/* 3.2 Badges with Status Dot */}
        <div className="space-y-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            B. Chấm trạng thái (Status Dot - dot=true)
          </h3>
          <div className="flex flex-wrap items-center gap-2.5">
            <Badge variant="success" dot>
              Đang mở đợt KLTN
            </Badge>
            <Badge variant="warning" dot>
              Chờ thẩm định đề cương
            </Badge>
            <Badge variant="info" dot>
              Đã phân công GVHD
            </Badge>
            <Badge variant="destructive" dot>
              Quá hạn nộp báo cáo
            </Badge>
            <Badge variant="secondary" dot>
              Đã lưu chiểu thư viện
            </Badge>
          </div>
        </div>

        {/* 3.3 Badges with Icons */}
        <div className="space-y-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            C. Badge kết hợp Icon (leftIcon / rightIcon)
          </h3>
          <div className="flex flex-wrap items-center gap-2.5">
            <Badge variant="info" leftIcon={<Clock />}>
              Còn 3 ngày nữa
            </Badge>
            <Badge variant="success" leftIcon={<CheckCircle2 />}>
              Đã được duyệt Cổng 1
            </Badge>
            <Badge variant="warning" leftIcon={<AlertTriangle />}>
              Cần chỉnh sửa đề cương
            </Badge>
            <Badge variant="outline" rightIcon={<ExternalLink />}>
              Xem chi tiết đề tài
            </Badge>
            <Badge variant="default" leftIcon={<Check />}>
              Đã hoàn tất 100%
            </Badge>
          </div>
        </div>
      </section>
    </div>
  );
}
