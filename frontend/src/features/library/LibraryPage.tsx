import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { ThesisSearch } from './ThesisSearch';

export function LibraryPage() {
  useEffect(() => {
    document.title = 'Kho khóa luận tốt nghiệp · UGTM';
  }, []);
  return (
    <div className="public-container library-page">
      <Link className="public-back" to="/welcome">
        <ArrowLeft size={16} />
        Trang chủ
      </Link>
      <div className="library-page-heading">
        <h1>
          Kho tri thức
          <br />
          <span>qua từng thế hệ.</span>
        </h1>
        <p>
          Tra cứu khóa luận tốt nghiệp đã được công bố.
          <br />
          Tìm một hướng đi, tiếp nối một ý tưởng.
        </p>
      </div>
      <ThesisSearch />
    </div>
  );
}
