import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import {
  ArrowRight,
  ArrowUpRight,
  BookOpen,
  RotateCcw,
  Search,
  SlidersHorizontal,
} from 'lucide-react';
import { findTheses, readSearch, searchParams } from './libraryApi';

function SearchForm({
  initialValue,
  onSearch,
}: {
  initialValue: string;
  onSearch: (value: string) => void;
}) {
  const [draft, setDraft] = useState(initialValue);
  function submit(event: FormEvent) {
    event.preventDefault();
    onSearch(draft.trim());
  }
  return (
    <form onSubmit={submit} role="search" className="thesis-search-form">
      <label className="public-sr-only" htmlFor="thesis-search-input">
        Tìm khóa luận theo tên, từ khóa, tác giả hoặc giảng viên
      </label>
      <Search size={22} aria-hidden="true" />
      <input
        id="thesis-search-input"
        name="q"
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        maxLength={200}
        placeholder="Tên khóa luận, từ khóa, tác giả, giảng viên…"
      />
      <button type="submit" className="public-button public-button-blue">
        Tìm kiếm
        <ArrowRight size={18} />
      </button>
    </form>
  );
}

export function ThesisSearch({ preview = false }: { preview?: boolean }) {
  const [params, setParams] = useSearchParams();
  const search = readSearch(params);
  const [filterOpen, setFilterOpen] = useState(!preview);
  const pageSize = preview ? 4 : 12;
  const query = useQuery({
    queryKey: ['public-theses', search, pageSize],
    queryFn: ({ signal }) => findTheses(search, pageSize, signal),
    networkMode: 'always',
    retry: false,
    staleTime: 60_000,
  });
  function apply(values: Partial<typeof search>) {
    setParams(searchParams({ ...search, ...values, page: values.page ?? 1 }, pageSize), {
      preventScrollReset: true,
    });
  }
  const activeFilters = Boolean(search.q || search.year || search.semester || search.department);
  const data = query.data;
  const pageOutOfRange = Boolean(
    data && data.totalCount > 0 && search.page > Math.ceil(data.totalCount / pageSize),
  );
  return (
    <div className="thesis-search">
      <SearchForm key={search.q} initialValue={search.q} onSearch={(q) => apply({ q })} />
      <div className="thesis-toolbar">
        <span>Khám phá theo năm học, học kỳ và lĩnh vực nghiên cứu.</span>
        <button
          type="button"
          className="public-text-button"
          aria-expanded={filterOpen}
          aria-controls="thesis-filters"
          onClick={() => setFilterOpen(!filterOpen)}
        >
          <SlidersHorizontal size={16} />
          Bộ lọc
          {activeFilters && (
            <span className="filter-active-dot" aria-label="Có bộ lọc đang áp dụng" />
          )}
        </button>
      </div>
      {filterOpen && (
        <div id="thesis-filters" className="thesis-filters">
          <label>
            Năm học
            <select value={search.year} onChange={(e) => apply({ year: e.target.value })}>
              <option value="">Tất cả năm học</option>
              {data?.years.map((year) => (
                <option key={year}>{year}</option>
              ))}
            </select>
          </label>
          <label>
            Học kỳ / Đợt
            <select value={search.semester} onChange={(e) => apply({ semester: e.target.value })}>
              <option value="">Tất cả học kỳ / đợt</option>
              {data?.semesters.map((semester) => (
                <option key={semester}>{semester}</option>
              ))}
            </select>
          </label>
          <label>
            Khoa / Ngành
            <select
              value={search.department}
              onChange={(e) => apply({ department: e.target.value })}
            >
              <option value="">Tất cả khoa / ngành</option>
              {data?.departments.map((department) => (
                <option key={department}>{department}</option>
              ))}
            </select>
          </label>
          <label>
            Sắp xếp
            <select value={search.sort} onChange={(e) => apply({ sort: e.target.value })}>
              <option value="newest">Mới công bố</option>
              <option value="title">Tên A–Z</option>
            </select>
          </label>
        </div>
      )}
      <div className="thesis-result-status" role="status" aria-live="polite">
        {data && (
          <span>
            {data.totalCount.toLocaleString('vi-VN')} khóa luận
            {activeFilters ? ' phù hợp' : ' đã công bố'}
          </span>
        )}
        {activeFilters && (
          <button
            type="button"
            className="public-text-button"
            onClick={() => {
              setParams({}, { preventScrollReset: true });
            }}
          >
            <RotateCcw size={14} />
            Xóa bộ lọc
          </button>
        )}
      </div>
      {query.isPending ? (
        <div className="thesis-skeletons" role="status" aria-label="Đang tải khóa luận">
          {[0, 1].map((i) => (
            <div className="thesis-skeleton" key={i}>
              <i />
              <i />
              <i />
            </div>
          ))}
        </div>
      ) : query.isError ? (
        <div className="library-state" role="alert">
          <BookOpen size={28} />
          <h3>Chưa kết nối được kho khóa luận</h3>
          <p>Dữ liệu hiện chưa tải được. Bạn có thể thử lại sau ít phút.</p>
          <button
            className="public-button public-button-outline"
            type="button"
            onClick={() => void query.refetch()}
          >
            Thử lại
            <RotateCcw size={16} />
          </button>
        </div>
      ) : pageOutOfRange ? (
        <div className="library-state">
          <BookOpen size={28} />
          <h3>Trang kết quả không còn tồn tại</h3>
          <p>Danh sách khóa luận đã thay đổi. Quay về trang đầu để xem các kết quả hiện có.</p>
          <button
            type="button"
            className="public-button public-button-outline"
            onClick={() => apply({ page: 1 })}
          >
            Về trang đầu
            <ArrowRight size={16} />
          </button>
        </div>
      ) : data?.items.length === 0 ? (
        <div className="library-state">
          <BookOpen size={28} />
          <h3>
            {activeFilters ? 'Chưa tìm thấy khóa luận phù hợp' : 'Kho tri thức đang được cập nhật'}
          </h3>
          <p>
            {activeFilters
              ? 'Thử từ khóa ngắn hơn hoặc bỏ bớt bộ lọc để mở rộng kết quả.'
              : 'Các khóa luận được phép công bố sẽ xuất hiện tại đây, theo từng năm học và học kỳ.'}
          </p>
          {activeFilters && (
            <button
              type="button"
              className="public-button public-button-outline"
              onClick={() => {
                setParams({});
              }}
            >
              Xem tất cả khóa luận
              <ArrowRight size={16} />
            </button>
          )}
        </div>
      ) : (
        <div className="thesis-list">
          {data?.items.map((thesis) => (
            <article className="thesis-result" key={thesis.slug}>
              <div className="thesis-result-year">
                <span>{thesis.academicYear}</span>
                <small>{thesis.semester}</small>
              </div>
              <div className="thesis-result-body">
                <span className="thesis-department">{thesis.department}</span>
                <h3>
                  <Link to={`/library/${thesis.slug}`}>{thesis.title}</Link>
                </h3>
                <p className="thesis-result-abstract">{thesis.abstract}</p>
                <p className="thesis-result-people">
                  {thesis.authors.join(', ')}
                  <span>GVHD: {thesis.advisor}</span>
                </p>
                <div className="thesis-keywords">
                  {thesis.keywords.slice(0, 4).map((keyword) => (
                    <span key={keyword}>{keyword}</span>
                  ))}
                </div>
              </div>
              <Link
                to={`/library/${thesis.slug}`}
                className="thesis-result-link"
                aria-label={`Xem khóa luận: ${thesis.title}`}
              >
                <ArrowUpRight size={23} />
              </Link>
            </article>
          ))}
        </div>
      )}
      {!preview && data && data.totalCount > pageSize && (
        <nav className="library-pagination" aria-label="Phân trang khóa luận">
          <button
            type="button"
            className="public-button public-button-outline"
            disabled={search.page === 1 || query.isFetching}
            onClick={() => apply({ page: search.page - 1 })}
          >
            Trang trước
          </button>
          <span>
            Trang {search.page} / {Math.ceil(data.totalCount / pageSize)}
          </span>
          <button
            type="button"
            className="public-button public-button-outline"
            disabled={search.page * pageSize >= data.totalCount || query.isFetching}
            onClick={() => apply({ page: search.page + 1 })}
          >
            Trang sau
          </button>
        </nav>
      )}
      {preview && (
        <div className="library-preview-footer">
          <p>
            <BookOpen size={16} />
            Thông tin công khai. Đăng nhập để đọc tài liệu được cấp quyền.
          </p>
          <Link
            to={`/library?${searchParams({ ...search, page: 1 })}`}
            className="public-text-link"
          >
            Xem toàn bộ kho khóa luận
            <ArrowRight size={18} />
          </Link>
        </div>
      )}
    </div>
  );
}
