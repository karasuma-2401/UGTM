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
  ShieldCheck,
  GraduationCap,
  UserCheck,
  Layers,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select, Combobox } from '@/components/ui/select';
import { ProgressBar } from '@/components/ui/progress';
import { RadioGroup, RadioButton } from '@/components/ui/radio';
import { PasswordField } from '@/features/auth/components/PasswordField';
import { PasswordFields } from '@/features/auth/components/PasswordFields';
import { PasswordStrength } from '@/features/auth/components/PasswordStrength';

export function UiTestPage() {
  const [globalLoading, setGlobalLoading] = useState(false);
  const [btnLoading, setBtnLoading] = useState(false);
  const [testPassword, setTestPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [interactiveInput, setInteractiveInput] = useState('');
  const [simulateError, setSimulateError] = useState(false);

  const [pwdSingle, setPwdSingle] = useState('MatKhau@2026!');
  const [pwdPair1, setPwdPair1] = useState('');
  const [pwdPair2, setPwdPair2] = useState('');
  const [pwdDisabled, setPwdDisabled] = useState(false);
  const [pwdShowRules, setPwdShowRules] = useState(true);

  // Select / Combobox states
  const [selectedMajor, setSelectedMajor] = useState<string>('se');
  const [selectedAdvisor, setSelectedAdvisor] = useState<string>('');
  const [selectedTechs, setSelectedTechs] = useState<string[]>(['react', 'dotnet']);
  const [selectDisabled, setSelectDisabled] = useState(false);
  const [selectError, setSelectError] = useState(false);

  // Progress Bar state
  const [progressValue, setProgressValue] = useState<number>(88);

  // Radio Button states
  const [defenseDecision, setDefenseDecision] = useState<string>('approve');
  const [roleOption, setRoleOption] = useState<string>('student');
  const [radioDisabled, setRadioDisabled] = useState(false);

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
            B. Chấm trạng thái chuẩn Mockup (Status Dot - dot=true)
          </h3>
          <div className="flex flex-wrap items-center gap-2.5">
            <Badge variant="success" dot>
              On track
            </Badge>
            <Badge variant="warning" dot>
              Warning
            </Badge>
            <Badge variant="error" dot>
              Error
            </Badge>
            <Badge variant="active" dot>
              Active
            </Badge>
            <Badge variant="info" dot>
              Info
            </Badge>
            <Badge variant="inactive" dot>
              Inactive
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

      {/* ========================================================================= */}
      {/* 4. PASSWORD COMPONENTS TESTS (PasswordField, PasswordStrength, PasswordFields) */}
      {/* ========================================================================= */}
      <section className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-100 pb-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <span>4. Bộ ba Password Components</span>
              <Badge variant="success">Phase 2: Hoàn thiện</Badge>
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Kiểm tra PasswordField (toggle mắt, icons, disabled), PasswordStrength (live rules
              checklist), và PasswordFields (cặp mật khẩu đồng bộ).
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Button
              size="sm"
              variant={pwdDisabled ? 'destructive' : 'outline'}
              onClick={() => setPwdDisabled((prev) => !prev)}
            >
              {pwdDisabled ? 'Mở khóa (Enable)' : 'Khóa ô nhập (Disabled)'}
            </Button>
            <Button
              size="sm"
              variant={pwdShowRules ? 'default' : 'outline'}
              onClick={() => setPwdShowRules((prev) => !prev)}
            >
              {pwdShowRules ? 'Ẩn Checklist Rules' : 'Hiện Checklist Rules'}
            </Button>
          </div>
        </div>

        {/* 4.1 PasswordField Standalone Variations */}
        <div className="space-y-3">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            A. Các biến thể PasswordField Đơn lẻ
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {/* Standard with toggle */}
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <span className="text-xs font-semibold text-slate-700 block">
                1. Mặc định (Toggle Eye + Icon Khóa)
              </span>
              <PasswordField
                id="test-pwd-1"
                label="Mật khẩu tài khoản"
                value={pwdSingle}
                onChange={setPwdSingle}
                disabled={pwdDisabled}
                helperText="Bấm vào biểu tượng mắt để bật/tắt hiển thị."
              />
            </div>

            {/* Custom Icon & Right Action */}
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <span className="text-xs font-semibold text-slate-700 block">
                2. Custom Icon & Right Action
              </span>
              <PasswordField
                id="test-pwd-2"
                label="Mã bảo mật cấp 2"
                value={pwdSingle}
                onChange={setPwdSingle}
                disabled={pwdDisabled}
                leftIcon={<ShieldCheck size={16} className="text-emerald-500" />}
                rightAction={
                  <button
                    type="button"
                    onClick={() => setPwdSingle('Reset@2026!')}
                    className="text-xs text-primary hover:underline font-medium cursor-pointer"
                  >
                    Tạo ngẫu nhiên
                  </button>
                }
                placeholder="Nhập mã bảo mật..."
              />
            </div>

            {/* Error & No Left Icon */}
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <span className="text-xs font-semibold text-slate-700 block">
                3. Không có Left Icon & Báo Lỗi
              </span>
              <PasswordField
                id="test-pwd-3"
                label="Mật khẩu cũ"
                value="123"
                onChange={() => {}}
                disabled={pwdDisabled}
                leftIcon={null}
                error="Mật khẩu cũ không chính xác."
              />
            </div>
          </div>
        </div>

        {/* 4.2 PasswordStrength Live Testing */}
        <div className="space-y-3 pt-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            B. Đánh giá độ mạnh mật khẩu (PasswordStrength)
          </h3>
          <div className="p-5 rounded-xl border border-slate-200 bg-slate-50/50 space-y-3 max-w-2xl">
            <div className="flex items-center justify-between text-xs text-slate-600">
              <span>
                Đang đánh giá chuỗi:{' '}
                <code className="bg-white px-2 py-0.5 rounded border border-slate-200 font-mono text-primary font-semibold">
                  {pwdSingle || '(trống)'}
                </code>
              </span>
            </div>
            <PasswordStrength password={pwdSingle} showRules={pwdShowRules} />
          </div>
        </div>

        {/* 4.3 PasswordFields Synchronized Pair */}
        <div className="space-y-3 pt-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            C. Bộ đôi thiết lập & xác nhận mật khẩu (PasswordFields)
          </h3>
          <div className="p-5 rounded-xl border border-slate-200 bg-slate-50/50 space-y-4 max-w-xl">
            <p className="text-xs text-slate-500">
              Bao gồm tự động kiểm tra trùng khớp (autoValidateMismatch), đồng bộ trạng thái
              disabled, và tích hợp PasswordStrength ở giữa.
            </p>
            <PasswordFields
              password={pwdPair1}
              confirmationPassword={pwdPair2}
              onPasswordChange={setPwdPair1}
              onConfirmationChange={setPwdPair2}
              disabled={pwdDisabled}
              showRules={pwdShowRules}
              autoValidateMismatch={true}
              passwordPlaceholder="Thiết lập mật khẩu từ 12 ký tự..."
              confirmationPlaceholder="Nhập lại chính xác mật khẩu trên..."
            />
          </div>
        </div>
      </section>

      {/* ========================================================================= */}
      {/* 5. DROPDOWN BOX & COMBOBOX (Select, Combobox) */}
      {/* ========================================================================= */}
      <section className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-100 pb-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <span>5. Dropdown Box & Combo Box</span>
              <Badge variant="info">Mới hoàn thành</Badge>
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Hỗ trợ Select cơ bản, Combobox tìm kiếm (Searchable), Multi-select nhiều mục,
              Clearable, Icon slot và các trạng thái động.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Button
              size="sm"
              variant={selectDisabled ? 'destructive' : 'outline'}
              onClick={() => setSelectDisabled((prev) => !prev)}
            >
              {selectDisabled ? 'Mở khóa (Enable)' : 'Khóa ô chọn (Disabled)'}
            </Button>
            <Button
              size="sm"
              variant={selectError ? 'destructive' : 'outline'}
              onClick={() => setSelectError((prev) => !prev)}
            >
              {selectError ? 'Tắt báo lỗi' : 'Bật mô phỏng lỗi'}
            </Button>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {/* 5.1 Basic Select with Icon & Description */}
          <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-3">
            <div>
              <span className="text-xs font-semibold text-slate-700 block">
                1. Dropdown Chọn Ngành (Select cơ bản)
              </span>
              <span className="text-[11px] text-slate-500">
                Kèm mô tả mã ngành, leftIcon và nút xóa lựa chọn (clearable).
              </span>
            </div>

            <Select
              placeholder="Chọn chuyên ngành..."
              value={selectedMajor}
              onChange={(val) => setSelectedMajor(val)}
              disabled={selectDisabled}
              error={selectError ? 'Vui lòng chọn chuyên ngành phù hợp.' : undefined}
              helperText={!selectError ? 'Chuyên ngành đăng ký thực hiện KLTN' : undefined}
              leftIcon={<GraduationCap className="text-primary" />}
              clearable
              options={[
                { value: 'cs', label: 'Khoa học Máy tính', description: 'Mã ngành: 7480101' },
                { value: 'se', label: 'Kỹ thuật Phần mềm', description: 'Mã ngành: 7480103' },
                { value: 'is', label: 'Hệ thống Thông tin', description: 'Mã ngành: 7480104' },
                {
                  value: 'cn',
                  label: 'Mạng máy tính & An toàn TT',
                  description: 'Mã ngành: 7480102',
                },
                { value: 'ce', label: 'Kỹ thuật Máy tính', description: 'Mã ngành: 7480106' },
              ]}
            />

            <div className="text-xs text-slate-500 bg-white p-2 rounded border border-slate-200 font-mono">
              Giá trị đã chọn:{' '}
              <strong className="text-primary">{selectedMajor || '(chưa chọn)'}</strong>
            </div>
          </div>

          {/* 5.2 Searchable Combobox with Badges & Slots */}
          <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-3">
            <div>
              <span className="text-xs font-semibold text-slate-700 block">
                2. Searchable Combobox (Tìm kiếm GVHD)
              </span>
              <span className="text-[11px] text-slate-500">
                Có ô tìm kiếm trực tiếp, phụ đề bộ môn, badge số slot, lọc nhanh.
              </span>
            </div>

            <Combobox
              placeholder="Tìm và chọn Giảng viên..."
              searchPlaceholder="Gõ tên giảng viên hoặc bộ môn..."
              value={selectedAdvisor}
              onChange={(val) => setSelectedAdvisor(val)}
              disabled={selectDisabled}
              leftIcon={<UserCheck className="text-primary" />}
              clearable
              options={[
                {
                  value: 'gv01',
                  label: 'PGS. TS. Trần Minh Nam',
                  description: 'Bộ môn Kỹ thuật Phần mềm',
                  badge: <Badge variant="info">Trưởng BM</Badge>,
                },
                {
                  value: 'gv02',
                  label: 'TS. Nguyễn Thị Bích Loan',
                  description: 'Bộ môn Hệ thống Thông tin',
                  badge: <Badge variant="secondary">12 đề tài</Badge>,
                },
                {
                  value: 'gv03',
                  label: 'TS. Lê Hoàng Khang',
                  description: 'Khoa học Dữ liệu & AI',
                  badge: <Badge variant="success">Còn slot</Badge>,
                },
                {
                  value: 'gv04',
                  label: 'ThS. Đỗ Thanh Bình',
                  description: 'Bộ môn Mạng & Bảo mật',
                  badge: <Badge variant="warning">Đầy slot</Badge>,
                  disabled: true,
                },
                {
                  value: 'gv05',
                  label: 'TS. Phạm Quốc Hùng',
                  description: 'Hệ thống Nhúng & IoT',
                  badge: <Badge variant="success">Còn slot</Badge>,
                },
              ]}
            />

            <div className="text-xs text-slate-500 bg-white p-2 rounded border border-slate-200 font-mono">
              GVHD: <strong className="text-primary">{selectedAdvisor || '(chưa chọn)'}</strong>
            </div>
          </div>

          {/* 5.3 Multi-Select with Tags count */}
          <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-3">
            <div>
              <span className="text-xs font-semibold text-slate-700 block">
                3. Multi-Select (Chọn nhiều công nghệ)
              </span>
              <span className="text-[11px] text-slate-500">
                Chọn nhiều mục cùng lúc, hiển thị tag đếm số lượng (+N).
              </span>
            </div>

            <Select
              placeholder="Chọn các công nghệ sử dụng..."
              multiple
              clearable
              value={selectedTechs}
              onChange={(val) => setSelectedTechs(val)}
              disabled={selectDisabled}
              leftIcon={<Layers className="text-primary" />}
              options={[
                { value: 'react', label: 'React 19 / TypeScript' },
                { value: 'dotnet', label: '.NET 9 / C#' },
                { value: 'python', label: 'Python / FastAPI' },
                { value: 'postgres', label: 'PostgreSQL' },
                { value: 'docker', label: 'Docker / Kubernetes' },
                { value: 'tailwind', label: 'Tailwind CSS' },
              ]}
            />

            <div className="text-xs text-slate-500 bg-white p-2 rounded border border-slate-200 font-mono">
              Đã chọn:{' '}
              <strong className="text-primary">{selectedTechs.join(', ') || '(trống)'}</strong>
            </div>
          </div>
        </div>

        {/* 5.4 Size Variations & Custom Styling */}
        <div className="space-y-3 pt-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            B. Các kích thước (Size: sm / default / lg)
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-end">
            <div>
              <span className="text-xs text-slate-500 block mb-1">Small (h-8, text-xs):</span>
              <Select
                size="sm"
                placeholder="Chọn đợt bảo vệ (sm)..."
                options={['Đợt 1 - Tháng 6/2026', 'Đợt 2 - Tháng 12/2026']}
              />
            </div>
            <div>
              <span className="text-xs text-slate-500 block mb-1">Default (h-9, text-sm):</span>
              <Select
                size="default"
                placeholder="Chọn đợt bảo vệ (default)..."
                options={['Đợt 1 - Tháng 6/2026', 'Đợt 2 - Tháng 12/2026']}
              />
            </div>
            <div>
              <span className="text-xs text-slate-500 block mb-1">Large (h-10, text-base):</span>
              <Select
                size="lg"
                placeholder="Chọn đợt bảo vệ (lg)..."
                options={['Đợt 1 - Tháng 6/2026', 'Đợt 2 - Tháng 12/2026']}
              />
            </div>
          </div>
        </div>
      </section>

      {/* ========================================================================= */}
      {/* 6. PROGRESS BAR COMPONENT */}
      {/* ========================================================================= */}
      <section className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-100 pb-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <span>6. Progress Bar (Thanh tiến trình)</span>
              <Badge variant="success">Khớp 100% Mockup</Badge>
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Tiêu đề bên trái, phần trăm bên phải, thanh bo tròn pill-shape bên dưới. Hỗ trợ tùy
              chỉnh màu sắc, kích thước và định dạng số.
            </p>
          </div>

          {/* Quick preset percentage buttons */}
          <div className="flex flex-wrap items-center gap-1.5">
            {[0, 25, 50, 75, 88, 100].map((val) => (
              <Button
                key={val}
                size="sm"
                variant={progressValue === val ? 'default' : 'outline'}
                onClick={() => setProgressValue(val)}
                className="h-8 px-2.5 text-xs font-mono"
              >
                {val}%
              </Button>
            ))}
          </div>
        </div>

        {/* 6.1 Exact Mockup Implementation */}
        <div className="space-y-3">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            A. Giao diện mẫu theo thiết kế yêu cầu (Ảnh minh họa)
          </h3>
          <div className="p-6 rounded-2xl border border-slate-200 bg-slate-50/50 max-w-xl space-y-4">
            <ProgressBar label="Tiến độ thẩm định" value={progressValue} />

            <div className="pt-2 flex items-center gap-3">
              <span className="text-xs text-slate-500 shrink-0">Kéo trượt thử nghiệm:</span>
              <input
                type="range"
                min="0"
                max="100"
                step="0.5"
                value={progressValue}
                onChange={(e) => setProgressValue(parseFloat(e.target.value))}
                className="w-full accent-primary cursor-pointer"
              />
              <span className="text-xs font-mono font-semibold text-primary w-12 text-right shrink-0">
                {progressValue}%
              </span>
            </div>
          </div>
        </div>

        {/* 6.2 Color Variants */}
        <div className="space-y-3 pt-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            B. Các biến thể màu sắc (Color Variants)
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-5 max-w-4xl">
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <ProgressBar
                label="Primary (Mặc định - Màu thương hiệu)"
                value={progressValue}
                variant="default"
              />
            </div>

            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <ProgressBar
                label="Success (Hoàn thành điều kiện)"
                value={progressValue}
                variant="success"
              />
            </div>

            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <ProgressBar
                label="Warning (Sắp hết hạn thẩm định)"
                value={progressValue}
                variant="warning"
              />
            </div>

            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <ProgressBar
                label="Destructive (Cảnh báo đạo văn / vi phạm)"
                value={progressValue}
                variant="destructive"
              />
            </div>

            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <ProgressBar
                label="Gradient (Dải màu hiện đại)"
                value={progressValue}
                variant="gradient"
              />
            </div>

            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <ProgressBar
                label="Custom Hex Color (#8b5cf6 - Tím)"
                value={progressValue}
                indicatorColor="#8b5cf6"
              />
            </div>
          </div>
        </div>

        {/* 6.3 Size Variations */}
        <div className="space-y-3 pt-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            C. Các kích thước độ dày (Sizes: xs, sm, default, lg, xl)
          </h3>
          <div className="space-y-4 max-w-2xl bg-slate-50/50 p-5 rounded-2xl border border-slate-200">
            <ProgressBar label="Extra Small (size='xs' - 4px)" size="xs" value={progressValue} />
            <ProgressBar label="Small (size='sm' - 6px)" size="sm" value={progressValue} />
            <ProgressBar
              label="Default (size='default' - 8px)"
              size="default"
              value={progressValue}
            />
            <ProgressBar label="Large (size='lg' - 12px)" size="lg" value={progressValue} />
            <ProgressBar label="Extra Large (size='xl' - 16px)" size="xl" value={progressValue} />
          </div>
        </div>

        {/* 6.4 Custom Text Formats & Subtitle */}
        <div className="space-y-3 pt-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            D. Tùy biến định dạng text & mô tả phụ đề
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-5 max-w-4xl">
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50">
              <ProgressBar
                label="Chỉ tiêu đề tài đã duyệt"
                value={progressValue}
                valueText={`${Math.round((progressValue / 100) * 50)} / 50 đề tài`}
                description="Tổng số đề tài đã thông qua thẩm định Hội đồng đợt 1."
              />
            </div>

            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50">
              <ProgressBar
                label="Điểm tích lũy rèn luyện"
                value={progressValue}
                valueFormat={(_, val) => `${val.toFixed(1)} / 100 đ`}
                variant="info"
                description="Điểm đánh giá chuẩn đầu ra sinh viên tốt nghiệp."
              />
            </div>
          </div>
        </div>
      </section>

      {/* ========================================================================= */}
      {/* 7. RADIO BUTTON & RADIO GROUP */}
      {/* ========================================================================= */}
      <section className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-100 pb-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <span>7. Radio Button & Radio Group</span>
              <Badge variant="success">Khớp 100% Mockup</Badge>
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Logic gom nhóm ràng buộc (chọn 1 trong nhiều), viền đổi màu xanh khi được chọn (mặc
              định --color-success), hỗ trợ icon và subtitle tùy chọn bên phải.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Button
              size="sm"
              variant={radioDisabled ? 'destructive' : 'outline'}
              onClick={() => setRadioDisabled((prev) => !prev)}
            >
              {radioDisabled ? 'Mở khóa (Enable)' : 'Khóa nhóm (Disabled)'}
            </Button>
          </div>
        </div>

        {/* 7.1 Exact Mockup Implementation */}
        <div className="space-y-3">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            A. Giao diện mẫu theo ảnh minh họa (Thẩm định Khóa luận)
          </h3>
          <div className="p-6 rounded-2xl border border-slate-200 bg-slate-50/50 max-w-2xl space-y-4">
            <p className="text-xs text-slate-500">
              Khi được chọn, ô sẽ được tô viền màu{' '}
              <code className="bg-white px-1.5 py-0.5 rounded border border-slate-200 text-emerald-600 font-mono">
                --color-success
              </code>{' '}
              (mặc định), nền nhạt và nút tròn màu primary.
            </p>

            <RadioGroup
              value={defenseDecision}
              onChange={setDefenseDecision}
              disabled={radioDisabled}
            >
              <RadioButton
                value="approve"
                label="Đồng ý cho bảo vệ trước Hội đồng"
                icon={<span className="size-2 rounded-full bg-emerald-500 shrink-0" />}
                rightText="Đạt yêu cầu"
              />
              <RadioButton
                value="conditional"
                label="Bảo vệ có điều kiện (Yêu cầu chỉnh sửa)"
                icon={<span className="size-2 rounded-full bg-amber-500 shrink-0" />}
                rightText="Bổ sung báo cáo"
              />
              <RadioButton
                value="reject"
                label="Không đồng ý cho bảo vệ"
                icon={<span className="size-2 rounded-full bg-rose-500 shrink-0" />}
                rightText="Không đạt"
              />
            </RadioGroup>

            <div className="text-xs text-slate-500 bg-white p-2.5 rounded-lg border border-slate-200 font-mono">
              Quyết định đang chọn:{' '}
              <strong className="text-emerald-700 font-semibold">{defenseDecision}</strong>
            </div>
          </div>
        </div>

        {/* 7.2 Minimal / No Subtitle & Horizontal Layout */}
        <div className="space-y-3 pt-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            B. Không có subtitle phải (Chỉ nút tròn + title) & Hàng ngang
          </h3>
          <div className="p-5 rounded-2xl border border-slate-200 bg-slate-50/50 max-w-2xl space-y-3">
            <span className="text-xs font-medium text-slate-600 block">Chọn vai trò tham gia:</span>
            <RadioGroup
              orientation="horizontal"
              value={roleOption}
              onChange={setRoleOption}
              disabled={radioDisabled}
            >
              <RadioButton value="student" label="Sinh viên thực hiện" />
              <RadioButton value="advisor" label="Giảng viên hướng dẫn" />
              <RadioButton value="reviewer" label="Cán bộ phản biện" />
            </RadioGroup>

            <div className="text-xs text-slate-500 bg-white p-2.5 rounded-lg border border-slate-200 font-mono">
              Vai trò: <strong className="text-primary font-semibold">{roleOption}</strong>
            </div>
          </div>
        </div>

        {/* 7.3 Shorthand Options Array & Custom Colors */}
        <div className="space-y-3 pt-2">
          <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            C. Khởi tạo qua mảng `options=[...]` & Tùy biến màu viền
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-5 max-w-4xl">
            {/* Blue primary border variant */}
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <span className="text-xs font-medium text-slate-600 block">
                Variant Primary (Viền xanh dương):
              </span>
              <RadioGroup
                variant="primary"
                defaultValue="opt1"
                options={[
                  { value: 'opt1', label: 'Phương thức nộp trực tuyến', rightText: 'Khuyên dùng' },
                  { value: 'opt2', label: 'Nộp trực tiếp tại Văn phòng Khoa' },
                ]}
              />
            </div>

            {/* Custom purple border */}
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/50 space-y-2">
              <span className="text-xs font-medium text-slate-600 block">
                Custom Border Color (#8b5cf6 - Viền tím):
              </span>
              <RadioGroup
                selectedBorderColor="#8b5cf6"
                selectedBgColor="#f5f3ff"
                defaultValue="thesis"
                options={[
                  { value: 'thesis', label: 'Khóa luận Tốt nghiệp (10 TC)' },
                  { value: 'project', label: 'Chuyên đề Tốt nghiệp (6 TC)' },
                ]}
              />
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
