import { useLocation, useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { useAuth } from '@/shared/auth';

export interface ErrorPageProps {
  code?: number | string;
  title?: string;
  description?: string;
  rollbackText?: string;
  loginText?: string;
  onRollback?: () => void;
  onLogout?: () => void;
  variant?: 'contained' | 'full';
  className?: string;
}

const DEFAULT_ERRORS: Record<string | number, { title: string; description: string }> = {
  403: {
    title: 'Bạn không có quyền truy cập',
    description: 'Trang hoặc hồ sơ này nằm ngoài phạm vi được cấp cho tài khoản của bạn.',
  },
  404: {
    title: 'Không tìm thấy trang',
    description: 'Đường dẫn bạn yêu cầu không tồn tại hoặc đã được chuyển sang địa chỉ khác.',
  },
  500: {
    title: 'Lỗi hệ thống máy chủ',
    description: 'Đã có sự cố xảy ra trong quá trình xử lý yêu cầu. Vui lòng thử lại sau.',
  },
};

export function ErrorPage({
  code: propCode,
  title: propTitle,
  description: propDescription,
  rollbackText = 'Quay lại khu vực của bạn',
  loginText = 'Về đăng nhập',
  onRollback,
  onLogout,
  variant = 'contained',
  className,
}: ErrorPageProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const { logout } = useAuth();

  const state = (location.state as Partial<ErrorPageProps> | undefined) || {};

  const code = propCode ?? state.code ?? 403;
  const defaultInfo = DEFAULT_ERRORS[code] || DEFAULT_ERRORS[403];
  const title = propTitle ?? state.title ?? defaultInfo.title;
  const description = propDescription ?? state.description ?? defaultInfo.description;

  const handleRollback = () => {
    if (onRollback) {
      onRollback();
      return;
    }

    if (window.history.length > 1) {
      navigate(-1);
    } else {
      navigate('/', { replace: true });
    }
  };

  const handleLogout = async () => {
    if (onLogout) {
      onLogout();
      return;
    }
    try {
      await logout();
    } finally {
      navigate('/login', { replace: true });
    }
  };

  return (
    <div
      className={cn(
        'w-full flex flex-col items-center justify-center p-6 select-none animate-in fade-in duration-200',
        variant === 'full' ? 'min-h-screen bg-white' : 'min-h-[60vh]',
        className,
      )}
    >
      <div className="max-w-lg w-full flex flex-col items-start text-left px-4">
        <h1 className="text-7xl sm:text-8xl font-bold tracking-tight text-[#1d4ed8]">
          {code}
        </h1>

        <h2 className="mt-5 text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">
          {title}
        </h2>

        <p className="mt-3 text-sm sm:text-base text-slate-500 font-normal leading-relaxed">
          {description}
        </p>

        <div className="mt-8 flex flex-wrap items-center gap-3">
          <Button
            type="button"
            onClick={handleRollback}
            className="bg-[#1d4ed8] hover:bg-[#1e40af] text-white font-medium px-5 py-2.5 rounded-lg shadow-sm transition-all"
          >
            {rollbackText}
          </Button>

          <Button
            type="button"
            variant="outline"
            onClick={handleLogout}
            className="bg-white hover:bg-slate-50 text-slate-700 border-slate-200 font-medium px-5 py-2.5 rounded-lg shadow-sm transition-all"
          >
            {loginText}
          </Button>
        </div>
      </div>
    </div>
  );
}
