import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import { buildDownloadRequestConfig, type DownloadUrlOptions } from '../utils/archival';
import type {
  ArchivalFileRole,
  FinalSubmissionConfirmationDto,
  FinalSubmissionDownloadDto,
  FinalSubmissionDto,
  FinalSubmissionEligibilityDto,
  FinalSubmissionPackageDto,
  FinalSubmissionPageDto,
  FinalSubmissionReviewAction,
  FinalSubmissionSummaryDto,
} from '../types/archival.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const archivalApi = {
  async getMySubmission(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<FinalSubmissionDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<FinalSubmissionDto>>(
        '/api/thesis/final-submissions/my-group',
        { params: { groupId }, signal },
      );
      return unwrap(response.data);
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 404) {
        return null;
      }
      throw error;
    }
  },

  async getEligibility(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<FinalSubmissionEligibilityDto> {
    const response = await httpClient.get<ApiResponse<FinalSubmissionEligibilityDto>>(
      '/api/thesis/final-submissions/submission-eligibility',
      { params: { groupId }, signal },
    );
    return unwrap(response.data);
  },

  async submitFinalSubmission(
    payload: {
      groupId: string;
      expectedSubmissionVersion: number | null;
      abstractVi: string;
      abstractEn: string;
      keywordsVi: string;
      keywordsEn: string;
      pdfFile: File;
      docxFile: File;
    },
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<FinalSubmissionDto> {
    const formData = new FormData();
    formData.append('groupId', payload.groupId);
    if (payload.expectedSubmissionVersion !== null) {
      formData.append('expectedSubmissionVersion', String(payload.expectedSubmissionVersion));
    }
    formData.append('abstractVi', payload.abstractVi.trim());
    formData.append('abstractEn', payload.abstractEn.trim());
    formData.append('keywordsVi', payload.keywordsVi.trim());
    formData.append('keywordsEn', payload.keywordsEn.trim());
    formData.append('pdfFile', payload.pdfFile);
    formData.append('docxFile', payload.docxFile);

    const response = await httpClient.post<ApiResponse<FinalSubmissionDto>>(
      '/api/thesis/final-submissions',
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
          'Idempotency-Key': idempotencyKey,
        },
      },
    );
    return unwrap(response.data);
  },

  async getDetail(submissionId: string, signal?: AbortSignal): Promise<FinalSubmissionDto> {
    const response = await httpClient.get<ApiResponse<FinalSubmissionDto>>(
      `/api/thesis/final-submissions/${submissionId}`,
      { signal },
    );
    return unwrap(response.data);
  },

  async getPackages(
    submissionId: string,
    signal?: AbortSignal,
  ): Promise<FinalSubmissionPackageDto[]> {
    const response = await httpClient.get<ApiResponse<FinalSubmissionPackageDto[]>>(
      `/api/thesis/final-submissions/${submissionId}/packages`,
      { signal },
    );
    return unwrap(response.data);
  },

  async getDownloadUrl(
    submissionId: string,
    packageVersion: number,
    fileRole: ArchivalFileRole | string,
    optionsOrSignal?: DownloadUrlOptions | AbortSignal,
  ): Promise<FinalSubmissionDownloadDto> {
    const response = await httpClient.get<ApiResponse<FinalSubmissionDownloadDto>>(
      `/api/thesis/final-submissions/${submissionId}/packages/${packageVersion}/files/${fileRole}/download`,
      buildDownloadRequestConfig(optionsOrSignal),
    );
    return unwrap(response.data);
  },

  async getAllSubmissionsByBatch(
    batchId: string,
    signal?: AbortSignal,
  ): Promise<FinalSubmissionSummaryDto[]> {
    const items: FinalSubmissionSummaryDto[] = [];
    let pageNumber = 1;
    const pageSize = 100;

    while (true) {
      const response = await httpClient.get<ApiResponse<FinalSubmissionPageDto>>(
        '/api/thesis/final-submissions',
        {
          params: { batchId, pageNumber, pageSize },
          signal,
        },
      );
      const page = unwrap(response.data);
      items.push(...page.items);
      if (items.length >= page.totalCount || page.items.length < pageSize) {
        return items;
      }
      pageNumber += 1;
    }
  },

  async confirmArchive(
    submissionId: string,
    payload: {
      action: FinalSubmissionReviewAction;
      expectedSubmissionVersion: number;
      expectedPackageVersion: number;
      reason: string | null;
    },
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<FinalSubmissionConfirmationDto> {
    const response = await httpClient.put<ApiResponse<FinalSubmissionConfirmationDto>>(
      `/api/thesis/final-submissions/${submissionId}/confirm-archive`,
      payload,
      {
        headers: {
          'Idempotency-Key': idempotencyKey,
        },
      },
    );
    return unwrap(response.data);
  },
};
