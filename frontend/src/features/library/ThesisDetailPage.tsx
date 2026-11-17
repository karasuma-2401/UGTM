import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, BookOpen } from 'lucide-react';
import { useAuth } from '@/shared/auth';
import { getThesis, LibraryError } from './libraryApi';
import { ThesisDocumentAccess } from './ThesisDocumentAccess';

export function ThesisDetailPage() {
  const { slug = '' } = useParams();
  const { user, isAuthenticated } = useAuth();
  const query = useQuery({
    queryKey: ['public-thesis', slug],
    queryFn: ({ signal }) => getThesis(slug, signal),
    networkMode: 'always',
    retry: false,
  });
  const thesis = query.data;
  useEffect(() => {
    document.title = thesis ? `${thesis.title} · UGTM` : 'Chi tiết khóa luận · UGTM';
  }, [thesis]);
  return (
    <div className="public-container thesis-detail-page">
      <Link to="/library" className="public-back">
        <ArrowLeft size={16} />
        Kho khóa luận
      </Link>
      {query.isPending ? (
        <div className="library-state" role="status">
          Đang tải thông tin khóa luận…
        </div>
      ) : query.isError ? (
        <div className="library-state" role="alert">
          <BookOpen size={30} />
          <h1>
            {query.error instanceof LibraryError && query.error.status === 404
              ? 'Không tìm thấy khóa luận'
              : 'Chưa tải được khóa luận'}
          </h1>
          <p>{query.error.message}</p>
          <button
            className="public-button public-button-outline"
            onClick={() => void query.refetch()}
          >
            Thử lại
          </button>
        </div>
      ) : (
        thesis && (
          <>
            <div className="thesis-detail-heading">
              <p>
                {thesis.department} <span>·</span> {thesis.academicYear} <span>·</span>{' '}
                {thesis.semester}
              </p>
              <h1>{thesis.title}</h1>
            </div>
            <div className="thesis-detail-grid">
              <article>
                <h2>Tóm tắt nghiên cứu</h2>
                <p className="thesis-full-abstract">{thesis.abstract}</p>
                <h2>Từ khóa</h2>
                <div className="thesis-keywords">
                  {thesis.keywords.map((keyword) => (
                    <span key={keyword}>{keyword}</span>
                  ))}
                </div>
              </article>
              <aside className="thesis-document-panel">
                <h2>Thông tin khóa luận</h2>
                <dl>
                  <dt>Sinh viên thực hiện</dt>
                  <dd>{thesis.authors.join(', ')}</dd>
                  <dt>Giảng viên hướng dẫn</dt>
                  <dd>{thesis.advisor}</dd>
                  <dt>Năm học · Học kỳ / Đợt</dt>
                  <dd>
                    {thesis.academicYear} · {thesis.semester}
                  </dd>
                </dl>
                <ThesisDocumentAccess
                  key={`${slug}:${isAuthenticated}:${user?.id ?? ''}`}
                  slug={slug}
                  hasDocument={thesis.hasDocument}
                />
              </aside>
            </div>
          </>
        )
      )}
    </div>
  );
}
