import { ClientConfig, CorpusDocument, JobRecord, ReportData } from '../types';

let cachedApiKey: string = localStorage.getItem('ugtm_api_key') || '';

export function getApiKey(): string {
  return cachedApiKey;
}

export function setApiKey(key: string): void {
  cachedApiKey = key;
  localStorage.setItem('ugtm_api_key', key);
}

function authHeaders(extra: Record<string, string> = {}): Record<string, string> {
  const headers: Record<string, string> = { ...extra };
  if (cachedApiKey) {
    headers['X-API-Key'] = cachedApiKey;
  }
  return headers;
}

export async function fetchClientConfig(): Promise<ClientConfig> {
  const res = await fetch('/api/v1/client-config');
  if (!res.ok) throw new Error(`HTTP ${res.status}: Failed to load config`);
  const data: ClientConfig = await res.json();
  if (!cachedApiKey && data.api_key) {
    setApiKey(data.api_key);
  }
  return data;
}

export async function submitScanDocument(formData: FormData): Promise<{ job_id: string; status: string }> {
  const res = await fetch('/api/v1/scans', {
    method: 'POST',
    headers: authHeaders(),
    body: formData,
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || `Lỗi tải lên bài nộp (HTTP ${res.status})`);
  }
  return res.json();
}

export async function submitQuickScan(): Promise<{ job_id: string; submission_id: string }> {
  const res = await fetch('/api/v1/demo/quick-scan', {
    method: 'POST',
    headers: authHeaders(),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || `Lỗi khởi tạo Quick Scan (HTTP ${res.status})`);
  }
  return res.json();
}

export async function fetchJobStatus(jobId: string): Promise<JobRecord> {
  const res = await fetch(`/api/v1/jobs/${jobId}`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}: Không tìm thấy Job`);
  return res.json();
}

export async function fetchJobReport(jobId: string): Promise<ReportData> {
  const res = await fetch(`/api/v1/reports/${jobId}`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}: Chưa có báo cáo`);
  return res.json();
}

export async function fetchJobList(limit: number = 50): Promise<JobRecord[]> {
  const res = await fetch(`/api/v1/jobs?limit=${limit}`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}: Không thể tải lịch sử`);
  return res.json();
}

export async function fetchCorpusDocuments(corpusId: string = 'uit_thesis'): Promise<CorpusDocument[]> {
  const res = await fetch(`/api/v1/corpus/documents?corpus_id=${encodeURIComponent(corpusId)}`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}: Không thể tải danh sách kho`);
  return res.json();
}

export async function submitCorpusDocument(formData: FormData): Promise<{ job_id: string }> {
  const res = await fetch('/api/v1/corpus/documents', {
    method: 'POST',
    headers: authHeaders(),
    body: formData,
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || `Lỗi nạp tài liệu vào kho (HTTP ${res.status})`);
  }
  return res.json();
}

export async function submitQuickIndex(): Promise<{ job_id: string; source_id: string }> {
  const res = await fetch('/api/v1/demo/quick-index', {
    method: 'POST',
    headers: authHeaders(),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || `Lỗi nạp bài mẫu nguồn (HTTP ${res.status})`);
  }
  return res.json();
}

