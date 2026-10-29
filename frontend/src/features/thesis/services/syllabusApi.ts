import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  SyllabusSubmissionDto,
  SyllabusSubmissionHistoryDto,
  SubmissionFileDownloadDto,
  StudentGroupDetailDto,
} from '../types/syllabus.types';
import type { StudentRegistrationStatus } from '@/features/topics/types/studentTopic.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const syllabusApi = {
  async getMyStatus(signal?: AbortSignal): Promise<StudentRegistrationStatus> {
    const response = await httpClient.get<ApiResponse<StudentRegistrationStatus>>(
      '/api/thesis/topic-registrations/my-status',
      { signal },
    );
    return unwrap(response.data);
  },

  async getSyllabusHistory(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<SyllabusSubmissionHistoryDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<SyllabusSubmissionHistoryDto>>(
        `/api/thesis/groups/${groupId}/submissions/Syllabus`,
        { signal },
      );
      return unwrap(response.data);
    } catch (err: unknown) {
      const axiosError = err as { response?: { status?: number } };
      if (axiosError?.response?.status === 404) {
        return null;
      }
      throw err;
    }
  },

  async submitSyllabus(
    groupId: string,
    file: File,
    signal?: AbortSignal,
  ): Promise<SyllabusSubmissionDto> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await httpClient.post<ApiResponse<SyllabusSubmissionDto>>(
      `/api/thesis/groups/${groupId}/syllabus/submissions`,
      formData,
      {
        headers: { 'Content-Type': 'multipart/form-data' },
        signal,
      },
    );
    return unwrap(response.data);
  },

  async getDownloadUrl(
    fileId: string,
    signal?: AbortSignal,
  ): Promise<SubmissionFileDownloadDto> {
    const response = await httpClient.get<ApiResponse<SubmissionFileDownloadDto>>(
      `/api/thesis/submission-files/${fileId}/download`,
      { signal },
    );
    return unwrap(response.data);
  },

  async getGroupDetail(
    groupId: string,
    fallbackInfo?: {
      topicId?: string;
      topicTitle?: string;
      currentUser?: { fullName?: string; userCode?: string | null; email?: string } | null;
      batchName?: string;
    },
    signal?: AbortSignal,
  ): Promise<StudentGroupDetailDto> {
    try {
      const response = await httpClient.get<ApiResponse<StudentGroupDetailDto>>(
        `/api/thesis/groups/${groupId}/detail`,
        { signal },
      );
      const data = unwrap(response.data);
      return {
        ...data,
        groupCode: data.groupCode || `Nhóm ${data.groupId.slice(0, 4).toUpperCase()}`,
        topicCode:
          data.topicCode ||
          (data.topicId ? `KLTN-${data.topicId.slice(0, 8).toUpperCase()}` : 'KLTN'),
        batchName: data.batchName || fallbackInfo?.batchName || 'Học kỳ hiện tại',
        members: (data.members || []).map((m) => ({
          studentId: m.studentId,
          userCode: m.userCode || 'Chưa cập nhật',
          fullName: m.fullName || 'Chưa cập nhật',
          email: m.email || '',
          confirmationStatus: m.confirmationStatus,
        })),
        advisor: {
          advisorId: data.advisor?.advisorId || '',
          fullName: data.advisor?.fullName || 'Chưa phân công',
          academicTitle: 'GV.',
        },
      };
    } catch {
      const members = fallbackInfo?.currentUser?.fullName
        ? [
            {
              studentId: 'me',
              userCode: fallbackInfo.currentUser.userCode || 'Chưa cập nhật',
              fullName: fallbackInfo.currentUser.fullName,
              email: fallbackInfo.currentUser.email || '',
              confirmationStatus: 'Confirmed',
            },
          ]
        : [];

      return {
        groupId,
        groupCode: `Nhóm ${groupId.slice(0, 4).toUpperCase()}`,
        topicId: fallbackInfo?.topicId || '',
        topicCode: fallbackInfo?.topicId
          ? `KLTN-${fallbackInfo.topicId.slice(0, 8).toUpperCase()}`
          : 'KLTN',
        topicTitleVi: fallbackInfo?.topicTitle || '',
        batchName: fallbackInfo?.batchName || 'Học kỳ hiện tại',
        advisor: {
          advisorId: '',
          fullName: 'Chưa phân công',
          academicTitle: 'GV.',
        },
        members,
      };
    }
  },
};
