import type { PagedResult } from '@/shared/api/types';

export type AuditOutcome = 'Success' | 'Denied' | 'Failed' | (string & {});

export interface AuditLogItem {
  id: number;
  actorUserId: string | null;
  actorEmail: string | null;
  actorFullName: string | null;
  actorSessionId: string | null;
  targetType: string;
  targetId: string;
  action: string;
  outcome: AuditOutcome;
  reasonCode: string | null;
  requestId: string;
  metadata: Record<string, any> | null;
  timestamp: string; // ISO 8601 UTC
}

export type AuditLogSortField = 'timestamp' | 'actor' | 'action' | 'outcome';

export interface AuditLogQueryParams {
  actorUserId?: string;
  actorSessionId?: string;
  action?: string;
  targetType?: string;
  targetId?: string;
  requestId?: string;
  searchTerm?: string;
  outcome?: string;
  fromUtc?: string;
  toUtc?: string;
  pageNumber?: number;
  pageSize?: number;
  sortBy?: AuditLogSortField;
  sortOrder?: 'asc' | 'desc';
}

export type AuditTimePreset = 'all' | 'today' | '7d' | '30d' | 'custom';

export interface AuditLogFilterState {
  searchTerm: string;
  action: string;
  outcome: string;
  fromUtc: string;
  toUtc: string;
  timePreset: AuditTimePreset;
  academicPeriod?: string;
  requestId?: string;
  targetId?: string;
  actorSessionId?: string;
}

export interface AuditLogDiffData {
  fieldTitle: string;
  beforeValue: string;
  afterValue: string;
  reason?: string;
  hasDiff: boolean;
}

export type PagedAuditLogResult = PagedResult<AuditLogItem>;

