import React, { useMemo } from 'react';
import { Link } from 'react-router-dom';
import {
  AlertCircle,
  BarChart3,
  BookOpen,
  FileText,
  Lock,
  Printer,
  RefreshCw,
  ShieldAlert,
} from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  formatDateTimeVi,
  formatScoreDecimal,
} from '../utils/scoreSummaryFormatting';
import { useStudentResults } from '../hooks/useStudentResults';
import {
  buildStudentConditionRows,
  formatScale4Score,
  replaceInterpunctWithHyphen,
} from '../utils/studentResults';
import { classifyThesisError, getThesisErrorMessage } from '@/features/thesis/utils/thesisErrors';

export const StudentResultsPage: React.FC = () => {
  const {
    statusQuery,
    groupDetailQuery,
    scoreQuery,
    finalSubmissionQuery,
    feedbackQuery,
    isConfirmedMember,
    refresh,
  } = useStudentResults();

  const scoreResult = scoreQuery.data;
  const isPublished = scoreResult?.kind === 'published';
  const summary = scoreResult?.kind === 'published' ? scoreResult.data : null;
  const groupDetail = groupDetailQuery.data;
  const isFetching =
    statusQuery.isFetching ||
    groupDetailQuery.isFetching ||
    scoreQuery.isFetching ||
    finalSubmissionQuery.isFetching;

  const conditionRows = useMemo(
    () => buildStudentConditionRows(scoreResult, finalSubmissionQuery.data),
    [scoreResult, finalSubmissionQuery.data],
  );

  const feedbackComment = feedbackQuery.data?.comment?.trim() || '';

  const handleOpenArchival = () => undefined;
  const handlePrintTranscript = () => undefined;

  if (statusQuery.isLoading || (isConfirmedMember && scoreQuery.isLoading && !scoreQuery.data)) {
    return (
      <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
        <Skeleton className="h-28 w-full rounded-2xl" />
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
          <div className="lg:col-span-8 space-y-5">
            <Skeleton className="h-56 w-full rounded-2xl" />
            <Skeleton className="h-44 w-full rounded-2xl" />
          </div>
          <div className="lg:col-span-4 space-y-5">
            <Skeleton className="h-72 w-full rounded-2xl" />
          </div>
        </div>
      </div>
    );
  }

  if (statusQuery.isError) {
    const errorKind = classifyThesisError(statusQuery.error);
    const message = getThesisErrorMessage(
      statusQuery.error,
      'Không thể tải trạng thái đăng ký đề tài của bạn.',
    );

    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <Card className="p-8 text-center space-y-4">
          <div className="size-12 mx-auto rounded-full bg-tone-danger-bg text-tone-danger-icon flex items-center justify-center">
            {errorKind === 'forbidden' ? (
              <ShieldAlert className="size-6 text-tone-warning-icon" />
            ) : (
              <AlertCircle className="size-6" />
            )}
          </div>
          <h2 className="text-lg font-bold text-ink-primary">
            {errorKind === 'forbidden'
              ? 'Không có quyền tra cứu kết quả'
              : 'Không tải được thông tin đề tài KLTN'}
          </h2>
          <p className="text-sm text-ink-muted max-w-md mx-auto">{message}</p>
          {errorKind !== 'forbidden' && (
            <Button
              type="button"
              variant="outline"
              leftIcon={<RefreshCw className="size-4" />}
              onClick={() => void statusQuery.refetch()}
            >
              Thử lại
            </Button>
          )}
        </Card>
      </div>
    );
  }

  if (!isConfirmedMember) {
    const isPendingConfirmation =
      Boolean(statusQuery.data?.hasRegistered) &&
      statusQuery.data?.confirmationStatus === 'Pending';

    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <Card className="p-8 text-center space-y-4">
          <div className="size-12 mx-auto rounded-full bg-tone-warning-bg text-tone-warning-icon flex items-center justify-center">
            <AlertCircle className="size-6" />
          </div>
          <h2 className="text-lg font-bold text-ink-primary">
            {isPendingConfirmation
              ? 'Bạn chưa xác nhận tham gia nhóm đề tài'
              : 'Chưa có thông tin nhóm đề tài KLTN'}
          </h2>
          <p className="text-sm text-ink-muted max-w-md mx-auto">
            {isPendingConfirmation
              ? 'Vui lòng hoàn tất xác nhận tham gia nhóm đề tài trước khi tra cứu điều kiện và kết quả khóa luận.'
              : 'Bạn chưa đăng ký đề tài nào trong đợt KLTN này. Vui lòng tra cứu và đăng ký đề tài để theo dõi tiến độ và kết quả.'}
          </p>
          <Button asChild>
            <Link to="/topics/catalog">
              <BookOpen className="size-4" />
              Xem danh mục đề tài
            </Link>
          </Button>
        </Card>
      </div>
    );
  }

  if (scoreQuery.isError) {
    const message = getThesisErrorMessage(
      scoreQuery.error,
      'Không thể tải dữ liệu bảng điểm của nhóm.',
    );
    return (
      <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
        <PageHeader
          title="Kết quả khóa luận"
          description="Tra cứu điều kiện mở điểm và bảng điểm chính thức theo nhóm."
          icon={<BarChart3 />}
        />
        <InlineAlert
          title="Không tải được kết quả"
          action={
            <Button
              type="button"
              variant="outline"
              size="sm"
              leftIcon={<RefreshCw className="size-3.5" />}
              onClick={() => void refresh()}
            >
              Thử lại
            </Button>
          }
        >
          {message}
        </InlineAlert>
      </div>
    );
  }

  const groupCode =
    groupDetail?.groupCode ||
    (statusQuery.data?.groupId
      ? `Nhóm ${statusQuery.data.groupId.slice(0, 4).toUpperCase()}`
      : 'Nhóm KLTN');
  const topicCode = groupDetail?.topicCode || 'KLTN';
  const topicTitle =
    groupDetail?.topicTitleVi ||
    summary?.topicTitle ||
    statusQuery.data?.topicTitleVi ||
    'Chưa có tên đề tài';
  const topicTitleEn = groupDetail?.topicTitleEn ?? null;
  const batchName = replaceInterpunctWithHyphen(groupDetail?.batchName) || 'Học kỳ hiện tại';

  const studentItems = (groupDetail?.members ?? [])
    .filter((member) => member.confirmationStatus === 'Confirmed')
    .map((member) => ({
      id: member.studentId,
      name: member.fullName,
      code: member.userCode,
      userCode: member.userCode,
    }));

  const advisorName = groupDetail?.advisor?.fullName || 'Chưa phân công';
  const advisorItems = groupDetail?.advisor?.fullName
    ? [
        {
          id: groupDetail.advisor.advisorId || advisorName,
          name: advisorName,
        },
      ]
    : [];

  return (
    <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
      <PageHeader
        title={isPublished ? 'Kết quả chính thức' : 'Kết quả chưa được mở'}
        description={
          isPublished && summary
            ? `${groupCode} - Công bố lúc ${formatDateTimeVi(summary.publishedAt)}`
            : `${groupCode} - Điều kiện tra cứu theo hồ sơ.`
        }
        icon={<BarChart3 />}
      >
        <StatusBadge tone={isPublished ? 'success' : 'neutral'}>
          {isPublished ? 'Đã công bố' : 'Chưa mở điểm'}
        </StatusBadge>
        <Button
          type="button"
          variant="outline"
          size="icon"
          aria-label="Làm mới kết quả"
          title="Làm mới"
          disabled={isFetching}
          onClick={() => void refresh()}
          className="size-9 rounded-xl border-line-input bg-surface-card text-ink-body hover:text-ink-primary"
        >
          <RefreshCw className={`size-4 ${isFetching ? 'animate-spin' : ''}`} />
        </Button>
      </PageHeader>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
        <div className="lg:col-span-8 space-y-5">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base font-bold text-ink-primary">
                Điều kiện
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="overflow-x-auto rounded-xl border border-line/80">
                <Table>
                  <TableHeader>
                    <TableRow className="bg-surface-subtle/90">
                      <TableHead>Bước / yêu cầu</TableHead>
                      <TableHead>Thông tin hồ sơ</TableHead>
                      <TableHead className="whitespace-nowrap">Trạng thái</TableHead>
                      <TableHead className="whitespace-nowrap">Tiếp theo</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {conditionRows.map((row) => (
                      <TableRow key={row.key}>
                        <TableCell className="font-semibold text-ink-primary">
                          {row.stepLabel}
                        </TableCell>
                        <TableCell className="text-ink-body">
                          {row.detailLabel}
                        </TableCell>
                        <TableCell>
                          <StatusBadge tone={row.statusTone}>{row.statusLabel}</StatusBadge>
                        </TableCell>
                        <TableCell className="text-ink-muted">
                          {row.nextLabel}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              <p className="text-xs text-ink-muted">
                {conditionRows.length} mục trong danh sách
              </p>
            </CardContent>
          </Card>

          {!isPublished && (
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base font-bold text-ink-primary">
                  Điểm đang khóa
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-start gap-3 rounded-xl border border-line/80 bg-surface-subtle/80 px-4 py-3.5 text-sm text-ink-secondary">
                  <Lock className="size-4 shrink-0 text-ink-muted mt-0.5" />
                  <span className="leading-relaxed">
                    {scoreResult?.kind === 'locked'
                      ? scoreResult.message
                      : 'Chưa hoàn thành lưu chiểu hoặc Khoa chưa công bố kết quả, chưa xem điểm hoặc bảng điểm.'}
                  </span>
                </div>
              </CardContent>
            </Card>
          )}

          {isPublished && summary && (
            <Card>
              <CardHeader className="pb-3 flex flex-row flex-wrap items-center justify-between gap-2 space-y-0">
                <CardTitle className="text-base font-bold text-ink-primary">
                  Nguồn điểm
                </CardTitle>
                {summary.calculationFormula && (
                  <span className="text-xs text-ink-muted tabular-nums">
                    Công thức:{' '}
                    <strong className="font-semibold text-ink-secondary">
                      {summary.calculationFormula}
                    </strong>
                  </span>
                )}
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="overflow-x-auto rounded-xl border border-line/80">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-surface-subtle/90">
                        <TableHead>Nguồn / thành viên</TableHead>
                        <TableHead className="text-right whitespace-nowrap">Điểm</TableHead>
                        <TableHead className="text-right whitespace-nowrap">Hệ số</TableHead>
                        <TableHead className="whitespace-nowrap">Trạng thái</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {summary.components.map((component, index) => (
                        <TableRow key={`${component.componentName}-${index}`}>
                          <TableCell className="font-semibold text-ink-primary">
                            {component.componentName}
                          </TableCell>
                          <TableCell className="text-right font-semibold text-ink-primary tabular-nums">
                            {formatScoreDecimal(component.score, 2)}
                          </TableCell>
                          <TableCell className="text-right text-ink-body tabular-nums">
                            Hệ số {component.weight}
                          </TableCell>
                          <TableCell>
                            <StatusBadge tone="success">Đã chấm</StatusBadge>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
                <p className="text-xs text-ink-muted">
                  {summary.components.length} mục trong danh sách
                </p>
              </CardContent>
            </Card>
          )}

          {feedbackComment && (
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base font-bold text-ink-primary">
                  Góp ý sau bảo vệ
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-start gap-3 rounded-xl border border-line/80 bg-surface-subtle/70 px-4 py-3.5 text-sm text-ink-secondary">
                  <FileText className="size-4 shrink-0 text-ink-muted mt-0.5" />
                  <p className="leading-relaxed whitespace-pre-line">{feedbackComment}</p>
                </div>
              </CardContent>
            </Card>
          )}

          {isPublished && (
            <Card>
              <CardContent className="p-4 sm:p-5 flex flex-wrap items-center gap-3">
                <Button
                  type="button"
                  leftIcon={<Printer className="size-4" />}
                  onClick={handlePrintTranscript}
                >
                  In bảng điểm
                </Button>
              </CardContent>
            </Card>
          )}
        </div>

        <div className="lg:col-span-4 space-y-5">
          {isPublished && summary && (
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base font-bold text-ink-primary">
                  Kết quả công bố
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="rounded-xl border border-line/80 bg-surface-subtle/70 px-3.5 py-3 space-y-1">
                    <p className="text-xs text-ink-muted">
                      Điểm tổng kết - Thang 10
                    </p>
                    <p className="text-3xl font-bold tracking-tight text-ink-primary tabular-nums">
                      {formatScoreDecimal(summary.finalScore, 2)}
                    </p>
                  </div>
                  <div className="rounded-xl border border-tone-info-border bg-tone-info-bg px-3.5 py-3 space-y-1">
                    <p className="text-xs text-ink-body">Quy đổi - Thang 4</p>
                    <p className="text-3xl font-bold tracking-tight text-primary tabular-nums">
                      {formatScale4Score(summary.finalScore)}
                    </p>
                  </div>
                </div>
                <p className="text-xs text-ink-muted pt-1 border-t border-line-subtle">
                  Bảng điểm do Khoa công bố
                </p>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base font-bold text-ink-primary">
                {isPublished ? 'Thông tin công bố' : 'Điều kiện xem điểm'}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 text-sm">
              <div className="space-y-1">
                <p className="text-xs font-medium text-ink-muted">Hồ sơ</p>
                <p className="text-xs font-semibold text-primary tabular-nums">
                  {topicCode} - {groupCode}
                </p>
                <p className="text-sm font-semibold text-ink-primary leading-snug">
                  {topicTitle}
                </p>
                {topicTitleEn && (
                  <p className="text-xs text-ink-muted leading-snug">
                    {topicTitleEn}
                  </p>
                )}
              </div>

              <div className="space-y-1.5 pt-2 border-t border-line-subtle">
                <p className="text-xs font-medium text-ink-muted">Sinh viên</p>
                {studentItems.length > 0 ? (
                  <div className="flex items-start gap-2.5">
                    <AvatarGroup items={studentItems} max={3} size="sm" ariaLabel="Sinh viên" />
                    <div className="min-w-0 space-y-1 text-xs text-ink-secondary leading-relaxed">
                      {studentItems.map((student) => (
                        <div key={student.id} className="truncate">
                          <span className="font-medium text-ink-heading">
                            {student.name}
                          </span>
                          {student.code ? (
                            <span className="text-ink-muted tabular-nums">
                              {' '}
                              - {student.code}
                            </span>
                          ) : null}
                        </div>
                      ))}
                    </div>
                  </div>
                ) : (
                  <p className="text-xs text-ink-muted">
                    Đang cập nhật thành viên
                  </p>
                )}
              </div>

              <div className="space-y-1.5 pt-2 border-t border-line-subtle">
                <p className="text-xs font-medium text-ink-muted">GVHD</p>
                {advisorItems.length > 0 ? (
                  <div className="flex items-center gap-2.5">
                    <AvatarGroup
                      items={advisorItems}
                      max={1}
                      size="sm"
                      ariaLabel="Giảng viên hướng dẫn"
                    />
                    <span className="min-w-0 truncate text-xs font-medium text-ink-heading">
                      {advisorName}
                    </span>
                  </div>
                ) : (
                  <p className="text-xs text-ink-muted">{advisorName}</p>
                )}
              </div>

              <div className="space-y-1 pt-2 border-t border-line-subtle">
                <p className="text-xs font-medium text-ink-muted">Đợt</p>
                <p className="text-xs font-medium text-ink-heading">{batchName}</p>
              </div>

              <div className="space-y-1.5 pt-2 border-t border-line-subtle">
                <p className="text-xs font-semibold text-ink-heading">
                  Lưu ý xử lý
                </p>
                <ul className="space-y-1 text-xs text-ink-body list-disc pl-4">
                  {isPublished ? (
                    <>
                      <li>Kết quả thuộc hồ sơ của bạn</li>
                      <li>Hồ sơ lưu chiểu đã được nghiệm thu</li>
                      <li>Bảng điểm theo cấu hình chính thức của đợt</li>
                    </>
                  ) : (
                    <>
                      <li>Khoa công bố kết quả</li>
                      <li>Khoa nghiệm thu hồ sơ lưu chiểu</li>
                    </>
                  )}
                </ul>
              </div>

              {!isPublished && (
                <div className="pt-2">
                  <Button type="button" className="w-full" onClick={handleOpenArchival}>
                    Xem hồ sơ lưu chiểu
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
};
