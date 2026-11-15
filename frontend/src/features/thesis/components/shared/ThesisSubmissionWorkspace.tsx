import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, BookOpen, RefreshCw } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Skeleton } from '@/components/ui/skeleton';
import { AvatarGroup } from '@/components/ui/avatar-group';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';
import { classifyThesisError } from '../../utils/thesisErrors';
import { useThesisJourneySummary, type JourneyGateSummary } from '../../hooks/useThesisJourneySummary';
import { ThesisSubmissionNavigation } from '../navigation';

interface ThesisSubmissionWorkspaceProps {
  activeTab: JourneyGateSummary['id'];
  statusQuery: {
    data?: {
      hasRegistered?: boolean;
      groupId?: string | null;
      topicTitleVi?: string | null;
    } | null;
    isLoading: boolean;
    isError: boolean;
    error: unknown;
    refetch: () => void;
  };
  groupDetail?: StudentGroupDetailDto | null;
  groupDetailLoading?: boolean;
  headerBadge?: ReactNode;
  actionError?: string | null;
  onDismissError?: () => void;
  actionSuccess?: string | null;
  onDismissSuccess?: () => void;
  sidebar?: ReactNode;
  children: ReactNode;
}

export function ThesisSubmissionWorkspace({
  activeTab,
  statusQuery,
  groupDetail,
  groupDetailLoading = false,
  headerBadge,
  actionError,
  onDismissError,
  actionSuccess,
  onDismissSuccess,
  sidebar,
  children,
}: ThesisSubmissionWorkspaceProps) {
  const statusData = statusQuery.data;
  const groupId = statusData?.groupId;
  const hasGroup = Boolean(statusData?.hasRegistered && groupId);

  const journey = useThesisJourneySummary(groupId);

  const advisorName = groupDetail?.advisor?.fullName || null;
  const topicTitle = groupDetail?.topicTitleVi || statusData?.topicTitleVi || null;
  const groupCode = groupDetail?.groupCode || null;
  const batchName = groupDetail?.batchName || null;

  const subtitleText = (() => {
    if (statusQuery.isLoading || (hasGroup && groupDetailLoading && !topicTitle)) {
      return null;
    }
    if (!hasGroup) {
      return 'Quản lý tiến độ thực hiện và nộp các cổng đánh giá khóa luận tốt nghiệp';
    }
    const parts = [groupCode, topicTitle, advisorName ? `GVHD: ${advisorName}` : null].filter(Boolean);
    return parts.length > 0
      ? parts.join(' · ')
      : 'Quản lý tiến độ thực hiện và nộp các cổng đánh giá khóa luận tốt nghiệp';
  })();

  const hasMetaItems = Boolean(
    (groupDetail?.members && groupDetail.members.length > 0) || batchName || headerBadge,
  );

  const headerDescription = (() => {
    if (statusQuery.isLoading || (hasGroup && groupDetailLoading && !topicTitle)) {
      return <Skeleton className="h-4 w-72 mt-0.5" />;
    }
    if (!hasGroup || !hasMetaItems) {
      return subtitleText;
    }
    return (
      <div className="space-y-2">
        <p>{subtitleText}</p>
        <div className="flex flex-wrap items-center gap-2">
          {groupDetail?.members && groupDetail.members.length > 0 && (
            <AvatarGroup
              size="sm"
              max={3}
              className="space-x-0 gap-1"
              items={groupDetail.members.map((m) => ({
                id: m.studentId,
                name: m.fullName || 'Sinh viên',
                userCode: m.userCode,
              }))}
            />
          )}
          {batchName && (
            <span className="inline-flex items-center px-2.5 py-0.5 rounded-md text-xs font-medium bg-slate-100/80 text-slate-600 border border-slate-200/70">
              {batchName}
            </span>
          )}
          {headerBadge}
        </div>
      </div>
    );
  })();

  if (statusQuery.isLoading) {
    return (
      <div className="space-y-5 animate-fade-in">
        <PageHeader
          compact
          accent
          title="Báo cáo & tiến độ"
          description={<Skeleton className="h-4 w-72 mt-0.5" />}
          navigation={<ThesisSubmissionNavigation activeId={activeTab} />}
        />
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
          <div className="lg:col-span-8 space-y-5">
            <Skeleton className="h-56 w-full rounded-2xl" />
            <Skeleton className="h-44 w-full rounded-2xl" />
          </div>
          <div className="lg:col-span-4">
            <Skeleton className="h-80 w-full rounded-2xl" />
          </div>
        </div>
      </div>
    );
  }

  if (statusQuery.isError) {
    const kind = classifyThesisError(statusQuery.error);
    return (
      <div className="space-y-5 animate-fade-in">
        <PageHeader
          compact
          accent
          title="Báo cáo & tiến độ"
          description="Quản lý tiến độ thực hiện và nộp các cổng đánh giá khóa luận tốt nghiệp"
          navigation={<ThesisSubmissionNavigation activeId={activeTab} />}
        />
        <InlineAlert
          tone={kind === 'forbidden' ? 'warning' : 'error'}
          title={
            kind === 'forbidden'
              ? 'Không đủ quyền truy cập khu vực nộp bài'
              : 'Không thể tải thông tin hồ sơ sinh viên'
          }
          action={
            kind !== 'forbidden' ? (
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() => statusQuery.refetch()}
              >
                <RefreshCw className="size-3.5 mr-1.5" />
                Thử lại
              </Button>
            ) : undefined
          }
        >
          {kind === 'forbidden'
            ? 'Vui lòng kiểm tra lại tài khoản đăng nhập có vai trò Sinh viên hoặc liên hệ Văn phòng Khoa.'
            : 'Đã xảy ra lỗi kết nối khi tải thông tin đề tài và nhóm thực hiện. Vui lòng thử lại.'}
        </InlineAlert>
      </div>
    );
  }

  if (!hasGroup) {
    return (
      <div className="space-y-5 animate-fade-in">
        <PageHeader
          compact
          accent
          title="Báo cáo & tiến độ"
          description="Quản lý tiến độ thực hiện và nộp các cổng đánh giá khóa luận tốt nghiệp"
          navigation={<ThesisSubmissionNavigation activeId={activeTab} />}
        />
        <Card className="p-10 bg-white border-slate-200/80 shadow-xs rounded-2xl text-center">
          <div className="max-w-md mx-auto space-y-4">
            <div className="size-12 rounded-2xl bg-slate-100 text-slate-500 flex items-center justify-center mx-auto">
              <AlertCircle className="size-6" />
            </div>
            <div className="space-y-1.5">
              <h2 className="text-base font-bold text-slate-900">Chưa tham gia nhóm đề tài</h2>
              <p className="text-sm text-slate-500 leading-relaxed">
                Bạn cần đăng ký đề tài và xác nhận nhóm thực hiện trước khi nộp báo cáo tiến độ hoặc các cổng thẩm định.
              </p>
            </div>
            <Button asChild size="sm">
              <Link to="/topics/student">
                <BookOpen className="size-4 mr-1.5" />
                Đi tới Đề tài & đăng ký
              </Link>
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-5 animate-fade-in">
      <PageHeader
        compact
        accent
        title="Báo cáo & tiến độ"
        description={headerDescription}
        navigation={<ThesisSubmissionNavigation activeId={activeTab} gates={journey.gates} />}
      />

      {actionError && (
        <InlineAlert tone="error" onDismiss={onDismissError}>
          {actionError}
        </InlineAlert>
      )}

      {actionSuccess && (
        <InlineAlert tone="success" onDismiss={onDismissSuccess}>
          {actionSuccess}
        </InlineAlert>
      )}

      <div className="fluent-tab-enter space-y-5">
        {sidebar ? (
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
            <div className="lg:col-span-8 space-y-5 min-w-0">{children}</div>
            <aside className="lg:col-span-4 lg:sticky lg:top-6 space-y-5 min-w-0">
              {sidebar}
            </aside>
          </div>
        ) : (
          <div className="space-y-5 min-w-0">{children}</div>
        )}
      </div>
    </div>
  );
}
