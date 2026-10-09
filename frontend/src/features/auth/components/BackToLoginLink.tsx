import { ArrowLeft } from 'lucide-react';
import { Link } from 'react-router-dom';

export function BackToLoginLink() {
  return (
    <Link className="auth-back" to="/login">
      <ArrowLeft size={14} />
      Quay lại Đăng nhập
    </Link>
  );
}
