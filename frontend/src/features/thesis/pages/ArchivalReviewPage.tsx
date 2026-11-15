import React, { useMemo } from 'react';
import {
  CheckCircle2,
  ClipboardCheck,
  Download,
  FileText,
  History,
  Lock,
  RefreshCw,
  RotateCcw,
  Search,
  X,
} from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { ArchivalHistoryDialog } from '../components/archival';
import { useArchivalReview } from '../hooks/useArchivalReview';
import type { ArchivalFilterKey, ArchivalReviewRow } from '../types/archival.types';
import { ARCHIVAL_REASON_MAX_LENGTH, getArchivalStatusDisplay } from '../utils/archival';
import { getThesisErrorMessage } from '../utils/thesisErrors';

const FILTER_OPTIONS: { value: ArchivalFilterKey; label: string }[] = [
  { value: 'AdvisorConfirmed', label: 'Chờ nghiệm thu' },
  { value: 'ALL', label: 'Tất cả trạng thái' },
  { value: 'Submitted', label: 'Chờ GVHD xác nhận' },
  { value: 'NeedRevision', label: 'Cần bổ sung' },
  { value: 'ArchiveConfirmed', label: 'Đã nghiệm thu' },
];

export const ArchivalReviewPage: React.FC = () => {
  const {
    permissions,
    batches,
    selectedBatchId,
    isLoadingBatches,
    filterKey,
    keyword,
    pageNumber,
    pageSize,
    totalCount,
    stats,
    allRowsCount,
    rows,
    selectedRow,
    submissionDetail,
    isLoadingDetail,
    detailError,
    academicDetail,
    packagesQuery,
    isLoadingList,
    isFetchingList,
    listError,
    facultyComment,
    commentError,
    pendingAction,
    isConfirming,
    isHistoryOpen,
    downloadingKey,
    actions,
  } = useArchivalReview();

  const batchOptions = useMemo(
    () =>
      batches.map((batch) => ({
        value: batch.id,
        label: `${batch.batchName} (${batch.academicYear} - HK${batch.semester})`,
      })),
    [batches],
  );

  const selectedBatchLabel = useMemo(() => {
    const current = batches.find((batch) => batch.id === selectedBatchId);
    return current ? `${current.batchName} (${current.academicYear} - HK${current.semester})` : '—';
  }, [batches, selectedBatchId]);

  const statItems: StatisticItem[] = useMemo(
    () => [
      {
        key: 'ALL',
        label: 'Tất cả hồ sơ',
        value: stats.all,
        tone: 'muted',
        active: filterKey === 'ALL',
        onClick: () => actions.changeFilter('ALL'),
      },
      {
        key: 'AdvisorConfirmed',
        label: 'Chờ nghiệm thu',
        value: stats.advisorConfirmed,
        tone: 'primary',
        active: filterKey === 'AdvisorConfirmed',
        onClick: () => actions.changeFilter('AdvisorConfirmed'),
      },
      {
        key: 'Submitted',
        label: 'Chờ GVHD xác nhận',
        value: stats.submitted,
        tone: 'warning',
        active: filterKey === 'Submitted',
        onClick: () => actions.changeFilter('Submitted'),
      },
      {
        key: 'NeedRevision',
        label: 'Cần bổ sung',
        value: stats.needRevision,
        tone: 'danger',
        active: filterKey === 'NeedRevision',
        onClick: () => actions.changeFilter('NeedRevision'),
      },
      {
        key: 'ArchiveConfirmed',
        label: 'Đã nghiệm thu',
        value: stats.archiveConfirmed,
        tone: 'success',
        active: filterKey === 'ArchiveConfirmed',
        onClick: () => actions.changeFilter('ArchiveConfirmed'),
      },
    ],
    [stats, filterKey, actions],
  );

  const columns: DataTableColumn<ArchivalReviewRow>[] = useMemo(
    () => [
      {
        id: 'groupCode',
        header: 'Nhóm',
        widthClassName: 'w-24',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <span className="font-semibold text-primary tabular-nums">{row.groupCode}</span>
        ),
        skeleton: { variant: 'text' },
      },
      {
        id: 'topicAndStudents',
        header: 'Đề tài & Sinh viên',
        cell: (row) => (
          <div className="min-w-0 space-y-0.5">
            <p className="font-semibold text-slate-900 line-clamp-1">{row.topicTitleVi}</p>
            <p className="text-xs text-slate-500 line-clamp-1">
              {row.studentNames.length > 0
                ? row.studentNames
                    .map((name, index) =>
                      row.studentCodes[index] ? `${name} (${row.studentCodes[index]})` : name,
                    )
                    .join(' - ')
                : 'Chưa có thông tin sinh viên'}
            </p>
          </div>
        ),
        skeleton: { variant: 'topic' },
      },
      {
        id: 'advisorName',
        header: 'GVHD',
        widthClassName: 'w-48',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <span className="text-xs font-medium text-slate-700 line-clamp-1">
            {row.advisorName || '—'}
          </span>
        ),
        skeleton: { variant: 'person' },
      },
      {
        id: 'version',
        header: 'Phiên bản',
        widthClassName: 'w-44',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <div className="text-xs space-y-0.5">
            <span className="font-semibold text-slate-800 tabular-nums">
              v{row.currentVersionNo}
            </span>
            <p className="text-slate-500">
              {getArchivalStatusDisplay(row.status).shortStepNote}
            </p>
          </div>
        ),
        skeleton: { variant: 'text' },
      },
      {
        id: 'status',
        header: 'Trạng thái',
        widthClassName: 'w-40',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => <StatusBadge tone={row.statusTone}>{row.statusLabel}</StatusBadge>,
        skeleton: { variant: 'badge' },
      },
      {
        id: 'actions',
        header: 'Thao tác',
        align: 'right',
        widthClassName: 'w-32',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => {
          const isSelected = selectedRow?.submissionId === row.submissionId;
          return (
            <div className="flex justify-end" onClick={(e) => e.stopPropagation()}>
              {isSelected ? (
                <span className="inline-flex items-center rounded-md bg-blue-50 px-2.5 py-1 text-xs font-semibold text-primary">
                  Đang chọn
                </span>
              ) : (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-8 rounded-md px-3 text-xs font-medium bg-white"
                  onClick={() => actions.selectRow(row.submissionId)}
                >
                  Xem hồ sơ
                </Button>
              )}
            </div>
          );
        },
        skeleton: { variant: 'actions', actionCount: 1 },
      },
    ],
    [selectedRow?.submissionId, actions],
  );

  if (!permissions.canRead) {
    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <InlineAlert tone="warning" title="Không có quyền truy cập">
          Tài khoản hiện tại không có quyền xem danh sách hồ sơ lưu chiểu của Khoa.
        </InlineAlert>
      </div>
    );
  }

  const canDecideSelected =
    permissions.canManage &&
    selectedRow?.status === 'AdvisorConfirmed' &&
    Boolean(submissionDetail) &&
    !isLoadingDetail;

  const pdfFile = submissionDetail?.files.find((f) => f.fileRole === 'ThesisPdf') ?? null;
  const docxFile = submissionDetail?.files.find((f) => f.fileRole === 'ThesisDocx') ?? null;

  const detailStudents =
    academicDetail?.students.map((student) => ({
      id: student.studentId,
      name: student.fullName,
      code: student.studentCode,
      userCode: student.studentCode,
    })) ??
    (selectedRow?.studentNames ?? []).map((name, index) => ({
      id: `${selectedRow?.groupId ?? 'row'}-${index}`,
      name,
      code: selectedRow?.studentCodes[index] ?? '',
      userCode: selectedRow?.studentCodes[index] ?? '',
    }));

  const detailAdvisorName = academicDetail?.advisorName || selectedRow?.advisorName || '—';
  const detailAdvisorItems =
    detailAdvisorName !== '—'
      ? [{ id: academicDetail?.advisorId || detailAdvisorName, name: detailAdvisorName }]
      : [];

  return (
    <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
      <PageHeader
        title="Nghiệm thu lưu chiểu"
        description="Chỉ hồ sơ GVHD đã xác nhận bản sửa mới đủ điều kiện nghiệm thu."
        icon={<ClipboardCheck />}
        actionsClassName="w-full sm:w-auto flex-1 justify-between items-center"
      >
        <div className="w-full sm:w-80 md:w-[400px]">
          <Select
            aria-label="Chọn đợt khóa luận tốt nghiệp"
            value={selectedBatchId}
            disabled={isLoadingBatches || batches.length === 0}
            placeholder={isLoadingBatches ? 'Đang tải đợt KLTN...' : 'Chọn đợt KLTN'}
            options={batchOptions}
            onChange={(value) => actions.selectBatch(value)}
            triggerClassName="h-10 rounded-xl border-slate-200/90 bg-white text-xs font-semibold text-slate-800 shadow-2xs hover:border-slate-300"
            contentClassName="w-max min-w-full sm:min-w-[400px] max-w-[90vw] left-0 right-auto p-1.5 rounded-xl border-slate-200/90 shadow-lg"
            optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-slate-700 hover:bg-slate-50"
          />
        </div>

        <div className="flex items-center gap-2.5 ml-auto">
          <StatusBadge tone="info">{stats.all} hồ sơ lưu chiểu</StatusBadge>
          <Button
            type="button"
            variant="outline"
            size="icon"
            aria-label="Làm mới danh sách hồ sơ lưu chiểu"
            title="Làm mới"
            disabled={!selectedBatchId || isFetchingList}
            onClick={() => actions.refresh()}
            className="size-10 rounded-xl border-slate-200 bg-white text-slate-600 hover:text-slate-900 shadow-2xs"
          >
            <RefreshCw className={`size-4 ${isFetchingList ? 'animate-spin' : ''}`} />
          </Button>
        </div>
      </PageHeader>

      <StatisticsBar
        items={statItems}
        loading={isLoadingList && allRowsCount === 0}
        className="grid-flow-row sm:grid-flow-col"
      />

      {listError && (
        <InlineAlert
          title="Không tải được danh sách hồ sơ lưu chiểu"
          action={
            <Button
              type="button"
              variant="outline"
              size="sm"
              leftIcon={<RefreshCw className="size-3.5" />}
              onClick={() => actions.refresh()}
            >
              Thử lại
            </Button>
          }
        >
          {listError}
        </InlineAlert>
      )}

      <section
        aria-label="Danh sách hồ sơ lưu chiểu"
        className="w-full min-w-0 rounded-2xl border border-slate-200/80 bg-white shadow-xs overflow-hidden"
      >
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 px-5 py-4">
          <div>
            <h2 className="text-base font-semibold text-slate-900">Checklist</h2>
            <p className="text-xs text-slate-500 mt-0.5 tabular-nums">
              {stats.all} hồ sơ - {stats.advisorConfirmed} chờ nghiệm thu - {stats.archiveConfirmed}{' '}
              đã nghiệm thu
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2 w-full sm:w-auto">
            <div className="w-full sm:w-72">
              <Input
                type="search"
                value={keyword}
                onChange={(e) => actions.changeKeyword(e.target.value)}
                placeholder="Tìm mã nhóm, tên đề tài, sinh viên, GVHD..."
                aria-label="Tìm kiếm hồ sơ lưu chiểu"
                leftIcon={<Search className="size-4 text-slate-400" />}
                rightIcon={
                  keyword ? (
                    <button
                      type="button"
                      aria-label="Xóa từ khóa tìm kiếm"
                      onClick={() => actions.changeKeyword('')}
                      className="text-slate-400 hover:text-slate-700"
                    >
                      <X className="size-3.5" />
                    </button>
                  ) : undefined
                }
              />
            </div>

            <div className="w-full sm:w-48">
              <Select
                aria-label="Lọc trạng thái hồ sơ lưu chiểu"
                value={filterKey}
                options={FILTER_OPTIONS}
                onChange={(value) => actions.changeFilter(value as ArchivalFilterKey)}
                triggerClassName="h-9 rounded-lg border-slate-200 bg-white text-xs font-medium text-slate-700"
                contentClassName="w-max min-w-full right-0 left-auto p-1 rounded-xl shadow-lg"
                optionClassName="text-xs py-1.5 px-2.5 rounded-lg"
              />
            </div>

            {(filterKey !== 'AdvisorConfirmed' || keyword.trim().length > 0) && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="h-9 rounded-lg text-xs"
                leftIcon={<RotateCcw className="size-3.5" />}
                onClick={actions.resetFilters}
              >
                Đặt lại
              </Button>
            )}
          </div>
        </div>

        <DataTable
          ariaLabel="Danh sách hồ sơ lưu chiểu cần nghiệm thu"
          data={rows}
          columns={columns}
          getRowId={(row) => row.submissionId}
          selectedRowId={selectedRow?.submissionId ?? null}
          onRowSelect={(id) => actions.selectRow(id)}
          onRowOpen={(row) => actions.selectRow(row.submissionId)}
          loading={isLoadingList && allRowsCount === 0}
          busy={isFetchingList && allRowsCount > 0}
          pagination={{
            pageNumber,
            pageSize,
            totalCount,
            onPageChange: actions.changePage,
            onPageSizeChange: actions.changePageSize,
            itemLabel: 'hồ sơ',
          }}
          empty={{
            title:
              allRowsCount === 0
                ? 'Chưa có hồ sơ lưu chiểu nào trong đợt'
                : 'Không có hồ sơ phù hợp bộ lọc',
            description:
              allRowsCount === 0
                ? 'Sinh viên cần nộp hồ sơ lưu chiểu sau khi có biên bản bảo vệ đã khóa.'
                : 'Thử chuyển sang "Tất cả trạng thái" hoặc xóa từ khóa tìm kiếm.',
            action:
              filterKey !== 'ALL' || keyword ? (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    actions.changeFilter('ALL');
                    actions.changeKeyword('');
                  }}
                >
                  Xóa bộ lọc
                </Button>
              ) : undefined,
          }}
        />
      </section>

      {selectedRow && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
          <div className="lg:col-span-7 xl:col-span-8 space-y-5">
            <Card>
              <CardHeader className="pb-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <CardTitle className="text-base font-bold text-slate-900">
                    Kết quả nghiệm thu - {selectedRow.groupCode}
                  </CardTitle>
                  <StatusBadge tone={selectedRow.statusTone}>{selectedRow.statusLabel}</StatusBadge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                {isLoadingDetail ? (
                  <div className="space-y-3">
                    <Skeleton className="h-24 w-full rounded-xl" />
                    <Skeleton className="h-24 w-full rounded-xl" />
                  </div>
                ) : detailError ? (
                  <InlineAlert
                    title="Không tải được chi tiết hồ sơ"
                    action={
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        leftIcon={<RefreshCw className="size-3.5" />}
                        onClick={actions.refetchDetail}
                      >
                        Thử lại
                      </Button>
                    }
                  >
                    {detailError}
                  </InlineAlert>
                ) : (
                  submissionDetail && (
                    <>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                        <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3 space-y-1">
                          <p className="font-semibold text-slate-600">Tóm tắt tiếng Việt</p>
                          <p className="text-slate-800 leading-relaxed whitespace-pre-line line-clamp-4">
                            {submissionDetail.abstractVi || '—'}
                          </p>
                        </div>
                        <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3 space-y-1">
                          <p className="font-semibold text-slate-600">Abstract tiếng Anh</p>
                          <p className="text-slate-800 leading-relaxed whitespace-pre-line line-clamp-4">
                            {submissionDetail.abstractEn || '—'}
                          </p>
                        </div>
                        <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3 space-y-1">
                          <p className="font-semibold text-slate-600">Từ khóa tiếng Việt</p>
                          <p className="text-slate-800">{submissionDetail.keywordsVi || '—'}</p>
                        </div>
                        <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3 space-y-1">
                          <p className="font-semibold text-slate-600">Keywords tiếng Anh</p>
                          <p className="text-slate-800">{submissionDetail.keywordsEn || '—'}</p>
                        </div>
                      </div>

                      {submissionDetail.facultyFeedback && (
                        <InlineAlert tone="warning" title="Phản hồi chỉnh sửa gần nhất">
                          {submissionDetail.facultyFeedback}
                        </InlineAlert>
                      )}

                      {selectedRow.status !== 'AdvisorConfirmed' && (
                        <InlineAlert
                          tone={selectedRow.status === 'ArchiveConfirmed' ? 'success' : 'info'}
                          title={
                            selectedRow.status === 'ArchiveConfirmed'
                              ? 'Hồ sơ đã hoàn thành lưu chiểu'
                              : selectedRow.status === 'Submitted'
                                ? 'Đang chờ GVHD xác nhận bản chỉnh sửa'
                                : 'Đang chờ sinh viên nộp lại bản sửa'
                          }
                        >
                          {selectedRow.status === 'ArchiveConfirmed'
                            ? 'Hồ sơ đã được nghiệm thu và khóa vĩnh viễn.'
                            : selectedRow.status === 'Submitted'
                              ? 'Khoa chỉ có thể nghiệm thu hoặc yêu cầu sửa sau khi GVHD hoàn tất bước xác nhận.'
                              : 'Sinh viên cần cập nhật và nộp lại phiên bản mới trước khi xét duyệt tiếp.'}
                        </InlineAlert>
                      )}

                      <div className="space-y-1.5">
                        <div className="flex items-center justify-between text-xs">
                          <label
                            htmlFor="faculty-archival-comment"
                            className="font-semibold text-slate-800"
                          >
                            Nhận xét Khoa{' '}
                            <span className="font-normal text-slate-500">
                              (bắt buộc khi yêu cầu sửa)
                            </span>
                          </label>
                          <span className="text-slate-400 tabular-nums">
                            {facultyComment.length}/{ARCHIVAL_REASON_MAX_LENGTH}
                          </span>
                        </div>
                        <textarea
                          id="faculty-archival-comment"
                          rows={3}
                          maxLength={ARCHIVAL_REASON_MAX_LENGTH}
                          disabled={!canDecideSelected || isConfirming}
                          value={facultyComment}
                          onChange={(e) => actions.setFacultyComment(e.target.value)}
                          placeholder="Hồ sơ đầy đủ tệp và metadata Việt / Anh, hoặc ghi rõ nội dung cần chỉnh sửa..."
                          className={`w-full rounded-xl border bg-white px-3 py-2 text-sm text-slate-800 placeholder:text-slate-400 focus:outline-none focus:ring-2 disabled:bg-slate-50 disabled:text-slate-500 ${
                            commentError
                              ? 'border-rose-400 focus:border-rose-500 focus:ring-rose-200'
                              : 'border-slate-200 focus:border-primary focus:ring-primary/20'
                          }`}
                        />
                        {commentError && (
                          <p className="text-xs font-medium text-rose-600">{commentError}</p>
                        )}
                      </div>

                      <div className="flex flex-wrap items-center gap-3 pt-1">
                        <Button
                          type="button"
                          disabled={!canDecideSelected || isConfirming}
                          leftIcon={<CheckCircle2 className="size-4" />}
                          onClick={() => actions.openDecisionConfirm('FacultyConfirm')}
                        >
                          Hoàn thành lưu chiểu
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          disabled={!canDecideSelected || isConfirming}
                          onClick={() => actions.openDecisionConfirm('RequestRevision')}
                        >
                          Yêu cầu sửa
                        </Button>
                      </div>
                    </>
                  )
                )}
              </CardContent>
            </Card>
          </div>

          <div className="lg:col-span-5 xl:col-span-4">
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base font-bold text-slate-900">
                  Nghiệm thu {selectedRow.groupCode}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4 text-sm">
                <div className="space-y-1">
                  <p className="text-xs font-medium text-slate-500">Hồ sơ</p>
                  <p className="text-xs font-semibold text-primary tabular-nums">
                    {academicDetail?.topicCode || 'KLTN'} - {selectedRow.groupCode} - v
                    {selectedRow.currentVersionNo}
                  </p>
                  <p className="text-sm font-semibold text-slate-900 leading-snug">
                    {selectedRow.topicTitleVi}
                  </p>
                  {selectedRow.topicTitleEn && (
                    <p className="text-xs text-slate-500 leading-snug">
                      {selectedRow.topicTitleEn}
                    </p>
                  )}
                </div>

                <div className="space-y-1.5 pt-2 border-t border-slate-100">
                  <p className="text-xs font-medium text-slate-500">Sinh viên</p>
                  {detailStudents.length > 0 ? (
                    <div className="flex items-start gap-2.5">
                      <AvatarGroup items={detailStudents} max={3} size="sm" ariaLabel="Sinh viên" />
                      <div className="min-w-0 space-y-1 text-xs text-slate-700 leading-relaxed">
                        {detailStudents.map((student) => (
                          <div key={student.id} className="truncate">
                            <span className="font-medium text-slate-800">{student.name}</span>
                            {student.code ? (
                              <span className="text-slate-500 tabular-nums"> - {student.code}</span>
                            ) : null}
                          </div>
                        ))}
                      </div>
                    </div>
                  ) : (
                    <p className="text-xs text-slate-500">Đang cập nhật sinh viên</p>
                  )}
                </div>

                <div className="space-y-1.5 pt-2 border-t border-slate-100">
                  <p className="text-xs font-medium text-slate-500">GVHD</p>
                  {detailAdvisorItems.length > 0 ? (
                    <div className="flex items-center gap-2.5">
                      <AvatarGroup
                        items={detailAdvisorItems}
                        max={1}
                        size="sm"
                        ariaLabel="Giảng viên hướng dẫn"
                      />
                      <span className="min-w-0 truncate text-xs font-medium text-slate-800">
                        {detailAdvisorName}
                      </span>
                    </div>
                  ) : (
                    <p className="text-xs text-slate-500">{detailAdvisorName}</p>
                  )}
                </div>

                <div className="space-y-1 pt-2 border-t border-slate-100">
                  <p className="text-xs font-medium text-slate-500">Đợt</p>
                  <p className="text-xs font-medium text-slate-800">{selectedBatchLabel}</p>
                </div>

                <div className="space-y-1.5 pt-2 border-t border-slate-100">
                  <p className="text-xs font-semibold text-slate-800">Lưu ý xử lý</p>
                  <ul className="space-y-1 text-xs text-slate-600 list-disc pl-4">
                    <li>
                      {selectedRow.advisorConfirmedAt
                        ? `GVHD đã xác nhận bản sửa (${formatDateTimeVi(selectedRow.advisorConfirmedAt)})`
                        : 'Chờ GVHD xác nhận bản sửa trước khi Khoa duyệt'}
                    </li>
                    <li>
                      {pdfFile && docxFile
                        ? 'Có đủ PDF và DOCX hợp lệ'
                        : 'Kiểm tra tệp PDF và DOCX đính kèm'}
                    </li>
                    <li>
                      {submissionDetail?.abstractVi &&
                      submissionDetail?.abstractEn &&
                      submissionDetail?.keywordsVi &&
                      submissionDetail?.keywordsEn
                        ? 'Đủ tóm tắt và từ khóa Việt / Anh'
                        : 'Đang kiểm tra metadata Việt / Anh'}
                    </li>
                  </ul>
                </div>

                <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-slate-100">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!pdfFile || downloadingKey === `${selectedRow.currentVersionNo}-ThesisPdf`}
                    leftIcon={<Download className="size-3.5" />}
                    onClick={() =>
                      pdfFile &&
                      void actions.downloadFile(
                        selectedRow.currentVersionNo,
                        'ThesisPdf',
                        pdfFile.fileName,
                      )
                    }
                  >
                    Tải PDF
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!docxFile || downloadingKey === `${selectedRow.currentVersionNo}-ThesisDocx`}
                    leftIcon={<FileText className="size-3.5" />}
                    onClick={() =>
                      docxFile &&
                      void actions.downloadFile(
                        selectedRow.currentVersionNo,
                        'ThesisDocx',
                        docxFile.fileName,
                      )
                    }
                  >
                    Tải DOCX
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    leftIcon={<History className="size-3.5" />}
                    onClick={actions.openHistory}
                  >
                    Lịch sử
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={pendingAction !== null}
        onClose={actions.closeDecisionConfirm}
        onConfirm={actions.confirmDecision}
        isConfirming={isConfirming}
        type={pendingAction === 'RequestRevision' ? 'warning' : 'brand'}
        title={
          pendingAction === 'RequestRevision'
            ? `Yêu cầu chỉnh sửa hồ sơ ${selectedRow?.groupCode ?? ''}`
            : `Xác nhận nghiệm thu lưu chiểu ${selectedRow?.groupCode ?? ''}`
        }
        confirmText={
          pendingAction === 'RequestRevision' ? 'Gửi yêu cầu sửa' : 'Hoàn thành lưu chiểu'
        }
        cancelText="Hủy"
        description={
          pendingAction === 'RequestRevision' ? (
            <div className="space-y-2 text-sm text-slate-600">
              <p>
                Hồ sơ nhóm <strong>{selectedRow?.groupCode}</strong> sẽ chuyển về trạng thái{' '}
                <strong>Cần bổ sung</strong> để sinh viên chỉnh sửa và nộp lại.
              </p>
              <div className="rounded-xl border border-amber-200 bg-amber-50/70 px-3 py-2 text-xs text-amber-900">
                Lý do: {facultyComment.trim()}
              </div>
            </div>
          ) : (
            <div className="space-y-2 text-sm text-slate-600">
              <p>
                Xác nhận nghiệm thu hồ sơ lưu chiểu phiên bản{' '}
                <strong>v{selectedRow?.currentVersionNo}</strong> của nhóm{' '}
                <strong>{selectedRow?.groupCode}</strong>.
              </p>
              <div className="flex items-start gap-2 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">
                <Lock className="size-3.5 shrink-0 mt-0.5 text-slate-500" />
                <span>
                  Sau khi xác nhận hoàn thành, hồ sơ lưu chiểu sẽ được khóa để phục vụ tổng hợp và
                  công bố điểm.
                </span>
              </div>
            </div>
          )
        }
      />

      <ArchivalHistoryDialog
        open={isHistoryOpen}
        onClose={actions.closeHistory}
        title={`Lịch sử lưu chiểu - ${selectedRow?.groupCode ?? ''}`}
        packages={packagesQuery.data ?? []}
        isLoading={packagesQuery.isLoading}
        error={
          packagesQuery.isError
            ? getThesisErrorMessage(packagesQuery.error, 'Không tải được lịch sử hồ sơ.')
            : null
        }
        onRetry={() => void packagesQuery.refetch()}
        onDownloadFile={(version, role, fileName) =>
          void actions.downloadFile(version, role, fileName)
        }
        downloadingKey={downloadingKey}
      />
    </div>
  );
};
