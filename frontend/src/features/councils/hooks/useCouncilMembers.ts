import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import axios from 'axios';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { notify } from '@/shared/notifications';
import { councilApprovalApi } from '../services/councilApprovalApi';
import {
  councilMembersApi,
  memberError,
  type CouncilCandidate,
  type CouncilRole,
} from '../services/councilMembersApi';
import type { CouncilDraftItemDto } from '../types/councilApproval.types';
import {
  applyRosterToCandidates,
  applyRosterToCouncil,
  markCouncilSubmitted,
  mergeAddedMembers,
  roleLabel,
  rosterFromResponse,
  type RosterMember,
} from '../utils/councilMemberWorkspace';

/** Cùng khóa với màn hình PĐT phê duyệt để hai màn hình dùng chung một bản cache. */
export const councilDraftsKey = (userId: string | undefined, batchId: string) =>
  ['council-drafts', userId, batchId] as const;
export const councilCandidatesKey = (councilId: string) =>
  ['council-member-candidates', councilId] as const;

const SYNC_DELAY_MS = 2500;

// Mặc định của ứng dụng thử lại 3 lần (~7 giây) trước khi báo lỗi; ở đây chỉ thử lại một lần để lỗi hiện sớm.
function retryOnceOnServerError(failureCount: number, error: unknown): boolean {
  const status = axios.isAxiosError(error) ? error.response?.status : undefined;
  if (status && status >= 400 && status < 500) return false;
  return failureCount < 1;
}

export function useCouncilDrafts(userId: string | undefined, batchId: string, enabled: boolean) {
  return useQuery({
    queryKey: councilDraftsKey(userId, batchId),
    queryFn: ({ signal }) => councilApprovalApi.getCouncilDrafts({ batchId }, signal),
    enabled: Boolean(userId) && enabled,
    staleTime: 30_000,
    retry: retryOnceOnServerError,
  });
}

export function useCouncilCandidates(councilId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: councilCandidatesKey(councilId ?? ''),
    // Tải cả danh sách một lần và lọc tại máy: gõ từ khóa không tạo thêm request.
    queryFn: ({ signal }) => councilMembersApi.candidates(councilId!, '', signal),
    enabled: Boolean(councilId) && enabled,
    staleTime: 30_000,
    retry: retryOnceOnServerError,
  });
}

export type MemberAction =
  | { type: 'add'; userId: string; fullName: string; role: CouncilRole; isReviewer?: boolean }
  | { type: 'remove'; userId: string; fullName: string }
  | { type: 'role'; userId: string; fullName: string; role: CouncilRole }
  | { type: 'submit'; notes: string };

export interface MemberActionRequest {
  council: CouncilDraftItemDto;
  roster: RosterMember[];
  action: MemberAction;
}

/** Lỗi hiển thị ngay tại vùng người dùng vừa thao tác: thêm (mục 3), sửa/gỡ (mục 2) hoặc gửi PĐT. */
export type ErrorGroup = 'add' | 'roster' | 'submit';
export const errorKeyFor = (councilId: string, group: ErrorGroup) => `${councilId}:${group}`;
const errorGroupOf = (action: MemberAction): ErrorGroup =>
  action.type === 'submit' ? 'submit' : action.type === 'add' ? 'add' : 'roster';

/** Khóa hành động đang chạy, dùng để hiện trạng thái đúng nút bị bấm. */
export function actionKey(action: MemberAction): string {
  return action.type === 'submit' ? 'submit' : `${action.type}:${action.userId}`;
}

interface ActionResult {
  roster: RosterMember[];
}

function mutationFn({ council, roster, action }: MemberActionRequest): Promise<ActionResult> {
  switch (action.type) {
    case 'add':
      return councilMembersApi
        .add(council.id, { userId: action.userId, role: action.role })
        .then((added) => ({
          roster: mergeAddedMembers(roster, added).map((member) =>
            member.userId === action.userId ? { ...member, isReviewer: Boolean(action.isReviewer) } : member,
          ),
        }));
    case 'remove':
      return councilMembersApi
        .remove(council.id, action.userId)
        .then((remaining) => ({ roster: rosterFromResponse(remaining, roster) }));
    case 'role':
      return councilMembersApi
        .replace(
          council.id,
          roster.map((member) => ({
            userId: member.userId,
            role: member.userId === action.userId ? action.role : member.role,
          })),
        )
        .then((next) => ({ roster: rosterFromResponse(next, roster) }));
    case 'submit':
      return councilMembersApi.submit(council.id, action.notes).then(() => ({ roster }));
  }
}

function successMessage({ council, action }: MemberActionRequest): string {
  const withdrawn = council.isSubmittedToAcademicAffairs
    ? ' Hồ sơ đã được rút lại khỏi PĐT, Khoa cần gửi lại sau khi chỉnh xong.'
    : '';
  switch (action.type) {
    case 'add':
      return `Đã thêm ${action.fullName} làm ${roleLabel(action.role)}.${withdrawn}`;
    case 'remove':
      return `Đã gỡ ${action.fullName} khỏi ${council.councilCode}.${withdrawn}`;
    case 'role':
      return `Đã đổi vai trò của ${action.fullName} thành ${roleLabel(action.role)}.${withdrawn}`;
    case 'submit':
      return `Đã gửi hồ sơ ${council.councilCode} lên PĐT thẩm định.`;
  }
}

/**
 * Thao tác phân công/gửi hồ sơ cho từng hội đồng.
 * - Mỗi hội đồng chỉ chạy một thao tác tại một thời điểm; hội đồng khác không bị khóa.
 * - Cache được cập nhật từ phản hồi API (không lạc quan), sau đó đồng bộ nền một lần
 *   khi đã yên (gộp nhiều thao tác liên tiếp), không chặn giao diện.
 */
export function useCouncilMemberActions() {
  const queryClient = useQueryClient();
  const inFlight = useRef(new Set<string>());
  const timers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const [pending, setPending] = useState<Record<string, string>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    const active = timers.current;
    return () => {
      active.forEach((timer) => clearTimeout(timer));
      active.clear();
    };
  }, []);

  const scheduleSync = useCallback(
    (councilId: string) => {
      void queryClient.invalidateQueries({ queryKey: ['council-drafts'], refetchType: 'none' });
      void queryClient.invalidateQueries({
        queryKey: councilCandidatesKey(councilId),
        refetchType: 'none',
      });
      const existing = timers.current.get(councilId);
      if (existing) clearTimeout(existing);
      timers.current.set(
        councilId,
        setTimeout(() => {
          timers.current.delete(councilId);
          if (inFlight.current.size > 0) return;
          void queryClient.invalidateQueries({ queryKey: ['council-drafts'], refetchType: 'active' });
          void queryClient.invalidateQueries({
            queryKey: councilCandidatesKey(councilId),
            refetchType: 'active',
          });
        }, SYNC_DELAY_MS),
      );
    },
    [queryClient],
  );

  const mutation = useMutation({
    mutationFn,
    onMutate: async (request) => {
      const councilId = request.council.id;
      // Hiện trạng thái đang xử lý ngay tại nút được bấm, trước mọi bước bất đồng bộ.
      setErrors((current) => ({ ...current, [errorKeyFor(councilId, errorGroupOf(request.action))]: '' }));
      setPending((current) => ({ ...current, [councilId]: actionKey(request.action) }));
      // Một lần đồng bộ nền đang bay có thể ghi đè dữ liệu mới hơn: hủy trước.
      await Promise.all([
        queryClient.cancelQueries({ queryKey: ['council-drafts'] }),
        queryClient.cancelQueries({ queryKey: councilCandidatesKey(councilId) }),
      ]);
    },
    onSuccess: ({ roster }, request) => {
      const councilId = request.council.id;
      if (request.action.type === 'submit') {
        const submittedAt = new Date().toISOString();
        queryClient.setQueriesData<CouncilDraftItemDto[]>({ queryKey: ['council-drafts'] }, (old) =>
          old?.map((item) =>
            item.id === councilId
              ? markCouncilSubmitted(item, request.action.type === 'submit' ? request.action.notes : '', submittedAt)
              : item,
          ),
        );
      } else {
        queryClient.setQueriesData<CouncilDraftItemDto[]>({ queryKey: ['council-drafts'] }, (old) =>
          old?.map((item) => (item.id === councilId ? applyRosterToCouncil(item, roster) : item)),
        );
        queryClient.setQueryData<CouncilCandidate[]>(councilCandidatesKey(councilId), (old) =>
          old ? applyRosterToCandidates(old, roster) : old,
        );
      }
      notify.success(successMessage(request));
      scheduleSync(councilId);
    },
    onError: (error, request) => {
      if (axios.isCancel(error)) return;
      const councilId = request.council.id;
      setErrors((current) => ({
        ...current,
        [errorKeyFor(councilId, errorGroupOf(request.action))]: memberError(error),
      }));
      // Dữ liệu có thể đã đổi ở phía server (404/409/mất mạng giữa chừng): đồng bộ lại.
      const status = axios.isAxiosError(error) ? error.response?.status : undefined;
      if (status === 404 || status === 409 || (axios.isAxiosError(error) && !error.response)) {
        scheduleSync(councilId);
      }
    },
    onSettled: (_data, _error, request) => {
      setPending((current) => {
        const next = { ...current };
        delete next[request.council.id];
        return next;
      });
    },
  });
  const { mutateAsync } = mutation;

  /** Trả về true khi server xác nhận thành công. */
  const run = useCallback(
    async (request: MemberActionRequest): Promise<boolean> => {
      const councilId = request.council.id;
      if (inFlight.current.has(councilId)) return false;
      inFlight.current.add(councilId);
      try {
        await mutateAsync(request);
        return true;
      } catch {
        return false;
      } finally {
        inFlight.current.delete(councilId);
      }
    },
    [mutateAsync],
  );

  const clearErrors = useCallback((councilId: string) => {
    setErrors((current) => {
      const keys = (['add', 'roster', 'submit'] as const).map((group) => errorKeyFor(councilId, group));
      if (!keys.some((key) => current[key])) return current;
      const next = { ...current };
      keys.forEach((key) => delete next[key]);
      return next;
    });
  }, []);

  return useMemo(
    () => ({ run, pending, errors, clearErrors }),
    [run, pending, errors, clearErrors],
  );
}
