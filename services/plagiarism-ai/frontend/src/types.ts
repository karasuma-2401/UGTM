export interface ClientConfig {
  api_key: string;
  default_corpus: string;
  embedding_model: string;
  reranker_model: string;
  classifier_enabled: boolean;
  max_upload_mb: number;
  remote_gpu_url?: string;
  remote_gpu_enabled: boolean;
  storage_backend?: string;
  inference_mode?: string;
}

export type JobStatus =
  | 'queued'
  | 'processing'
  | 'waiting_for_compute'
  | 'completed'
  | 'failed'
  | 'cancelled';

export interface JobRecord {
  job_id: string;
  job_type: 'scan' | 'index';
  corpus_id: string;
  document_key: string;
  version: number;
  filename: string;
  status: JobStatus;
  error_code?: string;
  attempt?: number;
  max_attempts?: number;
  retry_after?: string;
  last_heartbeat?: string;
  worker_id?: string;
  checkpoint?: {
    phase?: string;
    chunks_count?: number;
    candidates_count?: number;
    [key: string]: any;
  };
  enqueued_at?: string;
  reconciled_at?: string;
  created_at?: string;
  updated_at?: string;
}

export interface AlignedSpan {
  query_start: number;
  query_end: number;
  source_start: number;
  source_end: number;
  query_text?: string;
  source_text?: string;
  match_type?: string;
  length?: number;
}

export interface CitationContext {
  status: 'present' | 'absent' | 'uncertain' | 'cited' | 'uncited' | 'incorrect' | 'not_applicable';
  citation_type?: string;
  markers?: string[];
  raw_markers?: string[];
  cited_works?: string[];
  potential_misattribution?: boolean;
  confidence?: number;
  explanation?: string;
}

export interface FindingMatch {
  finding_id?: string;
  submission_chunk_no: number;
  submission_page?: number;
  submission_start?: number;
  submission_end?: number;
  submission_start_offset?: number;
  submission_end_offset?: number;
  submission_text: string;
  source_id?: string | number;
  source_key: string;
  source_version: number;
  source_filename?: string;
  document_family?: string;
  source_chunk_no: number;
  source_page?: number;
  source_start?: number;
  source_end?: number;
  source_start_offset?: number;
  source_end_offset?: number;
  source_text: string;
  dense_score?: number;
  lexical_score?: number;
  reranker_score?: number;
  rerank_score?: number;
  classifier_score?: number;
  decision_source?: 'heuristic' | 'custom_classifier' | 'near_exact_override' | string;
  citation_status?: 'present' | 'absent' | 'uncertain' | 'cited' | 'uncited' | string;
  citation_context?: CitationContext;
  feature_values?: Record<string, any>;
  review_required?: boolean;
  aligned_spans?: AlignedSpan[];
  passage_group_id?: string;
}

export interface PassageGroup {
  group_id?: string;
  passage_group_id?: string;
  source_key: string;
  source_version: number;
  source_filename?: string;
  document_family?: string;
  submission_start_chunk?: number;
  submission_end_chunk?: number;
  findings_count?: number;
  highest_rerank_score?: number;
  max_reranker_score?: number;
  mean_rerank_score?: number;
  review_required?: boolean;
  findings?: FindingMatch[];
}

export interface ReportSummary {
  scanned_chunks?: number;
  scanned_characters?: number;
  matched_characters?: number;
  similarity_coverage?: number;
  max_reranker_score?: number;
  flagged_pairs?: number;
  findings_returned?: number;
  total_findings?: number;
  matched_sources?: number;
  passage_groups?: number;
  cited_findings?: number;
  uncited_findings?: number;
  uncertain_findings?: number;
  review_recommended?: boolean;
  detector_mode?: string;
  interpretation?: string;
}

export interface ReportProvenance {
  parser_version?: string;
  preprocessing_profile?: string;
  preprocessing_config_hash?: string;
  embedding_model?: string;
  embedding_revision?: string;
  reranker_model?: string;
  reranker_revision?: string;
  classifier_version?: string;
  detector_mode?: string;
  corpus_snapshot_id?: string;
}

export interface ReportData {
  schema_version?: number | string;
  report_id?: string;
  job_id?: string;
  corpus_id?: string;
  submission_id?: string;
  submission_version?: number;
  created_at?: string;
  provenance?: ReportProvenance;
  summary?: ReportSummary;
  passage_groups?: PassageGroup[];
  findings?: FindingMatch[];
  pagination?: {
    page: number;
    page_size: number;
    total_pages: number;
    total_findings: number;
    has_next: boolean;
    has_prev: boolean;
  };
  disclaimer?: string;

  // Legacy Schema v2 fallbacks
  version?: number;
  filename?: string;
  total_chunks?: number;
  flagged_chunks_count?: number;
  max_rerank_score?: number;
  mean_rerank_score?: number;
  detector_mode?: string;
  classifier_version?: string;
  isolated_findings?: FindingMatch[];
  matched_sources_count?: number;
}

export type ReviewDecision =
  | 'pending'
  | 'confirmed_plagiarism'
  | 'acceptable_citation'
  | 'common_boilerplate'
  | 'needs_investigation';

export interface ReviewRecord {
  id: string;
  target_id: string;
  target_type: 'group' | 'finding';
  decision: ReviewDecision;
  reviewer_name: string;
  notes: string;
  timestamp: string;
}

export interface CorpusDocument {
  source_key: string;
  version: number;
  filename: string;
  is_active: boolean;
  chunk_count: number;
  indexed_at?: string;
}

export type ActiveTab = 'scan' | 'corpus' | 'report' | 'history' | 'system';
