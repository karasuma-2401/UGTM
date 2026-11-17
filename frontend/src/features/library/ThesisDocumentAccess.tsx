import { useEffect, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { ArrowUpRight, BookOpen, Download, LockKeyhole } from 'lucide-react';
import axios from 'axios';
import { useAuth } from '@/shared/auth';
import { getThesisDocument } from './libraryApi';

// Keyed by publication/account in the detail page: a private URL cannot survive an account change.
export function ThesisDocumentAccess({
  slug,
  hasDocument,
}: {
  slug: string;
  hasDocument: boolean;
}) {
  const { isAuthenticated } = useAuth();
  const location = useLocation();
  const mounted = useRef(false);
  const [state, setState] = useState<{
    url?: string;
    expiresAt?: string;
    error?: string;
    loading?: boolean;
  }>({});
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  useEffect(() => {
    if (!state.expiresAt) return;
    const timer = setTimeout(
      () => setState({}),
      Math.max(0, new Date(state.expiresAt).getTime() - Date.now()),
    );
    return () => clearTimeout(timer);
  }, [state.expiresAt]);
  async function requestDocument() {
    setState({ loading: true });
    try {
      const result = await getThesisDocument(slug);
      if (mounted.current) setState({ url: result.downloadUrl, expiresAt: result.expiresAt });
    } catch (error) {
      if (mounted.current)
        setState({
          error:
            axios.isAxiosError(error) && error.response?.status === 403
              ? 'Tài khoản của bạn chưa được cấp quyền đọc tài liệu này.'
              : 'Chưa mở được tài liệu. Bạn hãy thử lại.',
        });
    }
  }
  return (
    <div className="thesis-document-action">
      <BookOpen size={26} />
      <h3>Tài liệu khóa luận</h3>
      {!hasDocument ? (
        <p>Tài liệu hiện chưa được mở để đọc trực tuyến.</p>
      ) : !isAuthenticated ? (
        <>
          <p>Đăng nhập tài khoản UGTM để đọc hoặc tải tài liệu được cấp quyền.</p>
          <Link to="/login" state={{ from: location }} className="public-button public-button-blue">
            <LockKeyhole size={16} />
            Đăng nhập để đọc
          </Link>
        </>
      ) : state.url ? (
        <>
          <p>Tài liệu đã sẵn sàng. Liên kết có thời hạn ngắn để bảo vệ hồ sơ.</p>
          <a
            href={state.url}
            target="_blank"
            rel="noreferrer"
            className="public-button public-button-blue"
          >
            Mở / tải PDF
            <ArrowUpRight size={17} />
          </a>
        </>
      ) : (
        <>
          <p>Tài liệu PDF dành cho tài khoản được cấp quyền.</p>
          <button
            disabled={state.loading}
            type="button"
            onClick={() => void requestDocument()}
            className="public-button public-button-blue"
          >
            <Download size={16} />
            {state.loading ? 'Đang mở tài liệu…' : 'Đọc / tải tài liệu'}
          </button>
        </>
      )}
      {state.error && (
        <p role="alert" className="library-error-text">
          {state.error}
        </p>
      )}
    </div>
  );
}
