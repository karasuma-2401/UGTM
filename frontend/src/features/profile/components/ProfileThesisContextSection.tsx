import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowUpRight, BookOpen, UsersRound } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { advisingApi } from '@/features/topics/services/advisingApi';
import { studentTopicApi } from '@/features/topics/services/studentTopicApi';
import { topicApi } from '@/features/topics/services/topicApi';
import type { UserProfile } from '@/features/auth/types/auth.types';

interface ProfileThesisContextSectionProps {
  user: UserProfile;
}

export function ProfileThesisContextSection({ user }: ProfileThesisContextSectionProps) {
  const isStudent = user.roles.some((r) => r.toLowerCase() === 'student');
  const isLecturer = user.roles.some((r) => r.toLowerCase() === 'lecturer');
  const enabled = !user.mustChangePassword && (isStudent || isLecturer);

  const studentStatusQuery = useQuery({
    queryKey: ['profile-student-thesis-status', user.id],
    queryFn: ({ signal }) => studentTopicApi.getMyStatus(signal),
    enabled: enabled && isStudent,
    retry: false,
    staleTime: 60_000,
  });

  const studentGroupId = studentStatusQuery.data?.groupId;
  const studentGroupQuery = useQuery({
    queryKey: ['profile-student-thesis-group', studentGroupId],
    queryFn: ({ signal }) => studentTopicApi.getGroupDetail(studentGroupId!, signal),
    enabled: enabled && isStudent && Boolean(studentGroupId),
    retry: false,
    staleTime: 60_000,
  });

  const lecturerStatsQuery = useQuery({
    queryKey: ['profile-lecturer-topic-stats', user.id],
    queryFn: ({ signal }) => topicApi.getStatistics(user.id, undefined, signal),
    enabled: enabled && isLecturer && !isStudent,
    retry: false,
    staleTime: 60_000,
  });

  const lecturerGroupsQuery = useQuery({
    queryKey: ['profile-lecturer-advising-count', user.id],
    queryFn: ({ signal }) =>
      advisingApi.getGroups(
        {
          batchId: '',
          keyword: '',
          scope: 'active',
          status: '',
          confirmation: '',
          pageNumber: 1,
          pageSize: 1,
          sortBy: 'createdAt',
          sortOrder: 'desc',
        },
        signal,
      ),
    enabled: enabled && isLecturer && !isStudent,
    retry: false,
    staleTime: 60_000,
  });

  if (!enabled) return null;

  if (isStudent) {
    if (studentStatusQuery.isError) return null;

    return (
      <section className="p-5 space-y-3" aria-label="Ngữ cảnh khóa luận sinh viên">
        <div className="flex items-center justify-between gap-2">
          <h2 className="text-sm font-semibold text-ink-primary">Khóa luận hiện tại</h2>
          <Link
            to={studentStatusQuery.data?.hasRegistered ? '/thesis/milestones' : '/topics/catalog'}
            className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
          >
            <span>Chi tiết</span>
            <ArrowUpRight className="size-3.5" aria-hidden="true" />
          </Link>
        </div>

        {studentStatusQuery.isLoading ? (
          <Skeleton className="h-20 w-full rounded-xl" />
        ) : !studentStatusQuery.data?.hasRegistered ? (
          <div className="rounded-xl border border-dashed border-line-input bg-surface-subtle/50 p-3.5 space-y-2">
            <p className="text-xs text-ink-muted leading-relaxed">
              Bạn chưa đăng ký đề tài khóa luận trong đợt hiện tại.
            </p>
            <Link
              to="/topics/catalog"
              className="inline-flex items-center gap-1.5 text-xs font-semibold text-primary hover:underline"
            >
              <BookOpen className="size-3.5" aria-hidden="true" />
              Xem danh mục đề tài
            </Link>
          </div>
        ) : (
          <div className="rounded-xl border border-line-subtle bg-surface-subtle/60 p-3.5 space-y-2">
            <p className="text-xs font-semibold text-ink-primary leading-snug line-clamp-2">
              {studentGroupQuery.data?.topicTitleVi ||
                studentStatusQuery.data.topicTitleVi ||
                'Đề tài khóa luận'}
            </p>
            <div className="flex flex-wrap items-center gap-1.5">
              <Badge
                variant={
                  studentStatusQuery.data.confirmationStatus === 'Confirmed'
                    ? 'success'
                    : 'warning'
                }
                className="text-[11px]"
              >
                {studentStatusQuery.data.confirmationStatus === 'Confirmed'
                  ? 'Đã xác nhận nhóm'
                  : 'Chờ xác nhận nhóm'}
              </Badge>
              {studentGroupQuery.data?.batchName && (
                <Badge variant="outline" className="bg-surface-card text-[11px]">
                  {studentGroupQuery.data.batchName}
                </Badge>
              )}
            </div>
            {studentGroupQuery.data?.advisor?.fullName && (
              <p className="text-[11px] text-ink-muted">
                GVHD:{' '}
                <span className="font-medium text-ink-secondary">
                  {studentGroupQuery.data.advisor.fullName}
                </span>
              </p>
            )}
          </div>
        )}
      </section>
    );
  }

  if (lecturerStatsQuery.isError && lecturerGroupsQuery.isError) return null;

  const isLoadingLecturer = lecturerStatsQuery.isLoading || lecturerGroupsQuery.isLoading;
  const activeGroupsCount =
    lecturerGroupsQuery.data?.statistics.active ??
    lecturerGroupsQuery.data?.page.totalCount ??
    0;
  const totalTopics = lecturerStatsQuery.data?.total ?? 0;

  return (
    <section className="p-5 space-y-3" aria-label="Công tác hướng dẫn khóa luận">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-ink-primary">Công tác hướng dẫn</h2>
        <Link
          to="/topics/advising"
          className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
        >
          <span>Quản lý</span>
          <ArrowUpRight className="size-3.5" aria-hidden="true" />
        </Link>
      </div>

      {isLoadingLecturer ? (
        <Skeleton className="h-20 w-full rounded-xl" />
      ) : (
        <div className="grid grid-cols-2 gap-2.5">
          <Link
            to="/topics/advising"
            className="rounded-xl border border-line-subtle bg-surface-subtle/60 p-3 hover:border-line-strong transition-colors"
          >
            <div className="flex items-center gap-1.5 text-[11px] font-medium text-ink-muted">
              <BookOpen className="size-3.5 text-primary" aria-hidden="true" />
              <span>Đề tài đề xuất</span>
            </div>
            <p className="mt-1 text-lg font-bold tabular-nums text-ink-primary">{totalTopics}</p>
          </Link>

          <Link
            to="/topics/advising?tab=groups"
            className="rounded-xl border border-line-subtle bg-surface-subtle/60 p-3 hover:border-line-strong transition-colors"
          >
            <div className="flex items-center gap-1.5 text-[11px] font-medium text-ink-muted">
              <UsersRound className="size-3.5 text-tone-success-icon" aria-hidden="true" />
              <span>Nhóm hướng dẫn</span>
            </div>
            <p className="mt-1 text-lg font-bold tabular-nums text-ink-primary">
              {activeGroupsCount}
            </p>
          </Link>
        </div>
      )}
    </section>
  );
}
