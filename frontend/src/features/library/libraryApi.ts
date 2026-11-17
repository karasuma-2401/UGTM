import { API_BASE_URL } from '@/shared/api/config';
import { httpClient } from '@/shared/api/httpClient';
import type { ApiResponse } from '@/shared/api/types';

export interface PublicThesis {
  slug: string;
  title: string;
  abstract: string;
  authors: string[];
  advisor: string;
  department: string;
  academicYear: string;
  semester: string;
  keywords: string[];
  publishedAt: string;
  hasDocument: boolean;
}
export interface ThesisSearch {
  q: string;
  year: string;
  semester: string;
  department: string;
  sort: string;
  page: number;
}
export interface PublicThesisPage {
  items: PublicThesis[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  years: string[];
  semesters: string[];
  departments: string[];
}
export class LibraryError extends Error {
  status: number;
  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}
export function readSearch(params: URLSearchParams): ThesisSearch {
  const page = Number(params.get('page'));
  return {
    q: params.get('q') ?? '',
    year: params.get('year') ?? '',
    semester: params.get('semester') ?? '',
    department: params.get('department') ?? '',
    sort: params.get('sort') === 'title' ? 'title' : 'newest',
    page: Number.isSafeInteger(page) && page > 0 && page <= 1_000_000 ? page : 1,
  };
}
export function searchParams(search: ThesisSearch, pageSize = 12) {
  const params = new URLSearchParams();
  for (const key of ['q', 'year', 'semester', 'department'] as const)
    if (search[key]) params.set(key, search[key]);
  params.set('sort', search.sort);
  params.set('page', String(search.page));
  params.set('pageSize', String(pageSize));
  return params;
}
async function getPublic<T>(path: string, signal?: AbortSignal): Promise<T> {
  // Public discovery must not trigger the private client's token refresh/logout flow.
  const response = await fetch(`${API_BASE_URL}/api/library/theses${path}`, {
    signal: AbortSignal.any([...(signal ? [signal] : []), AbortSignal.timeout(15_000)]),
    credentials: 'omit',
  });
  if (!response.ok)
    throw new LibraryError(
      response.status === 404
        ? 'Không tìm thấy khóa luận này hoặc hồ sơ đã ngừng công bố.'
        : 'Chưa tải được kho khóa luận. Bạn hãy thử lại sau.',
      response.status,
    );
  const envelope = (await response.json()) as ApiResponse<T>;
  if (!envelope.success || !envelope.data)
    throw new LibraryError(envelope.error?.message ?? 'Chưa tải được dữ liệu.', response.status);
  return envelope.data;
}
export function findTheses(search: ThesisSearch, pageSize: number, signal?: AbortSignal) {
  return getPublic<PublicThesisPage>(`?${searchParams(search, pageSize)}`, signal);
}
export function getThesis(slug: string, signal?: AbortSignal) {
  return getPublic<PublicThesis>(`/${encodeURIComponent(slug)}`, signal);
}
export async function getThesisDocument(slug: string) {
  const { data } = await httpClient.get<ApiResponse<{ downloadUrl: string; expiresAt: string }>>(
    `/api/library/theses/${encodeURIComponent(slug)}/document`,
  );
  if (!data.success || !data.data)
    throw new Error(data.error?.message ?? 'Không thể cấp quyền đọc tài liệu.');
  const url = new URL(data.data.downloadUrl);
  if (
    url.protocol !== 'https:' &&
    !(
      import.meta.env.DEV &&
      url.protocol === 'http:' &&
      ['localhost', '127.0.0.1'].includes(url.hostname)
    )
  )
    throw new Error('Địa chỉ tài liệu không hợp lệ.');
  return data.data;
}
