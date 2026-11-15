import React, { useMemo } from 'react';
import {
  CheckCircle2,
  ClipboardCheck,
  Download,
  ExternalLink,
  FileSearch,
  FileText,
  History,
  Lock,
  RefreshCw,
  RotateCcw,
  Search,
} from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { cn } from '@/lib/utils';
import { ArchivalHistoryDialog } from '../components/archival';
import { useAdvisorArchival } from '../hooks/useAdvisorArchival';
import type { ArchivalFilterKey, ArchivalReviewRow } from '../types/archival.types';
import { ARCHIVAL_REASON_MAX_LENGTH } from '../utils/archival';
import { getThesisErrorMessage } from '../utils/thesisErrors';

const FILTER_OPTIONS: { value: ArchivalFilterKey; label: string }[] = [
  { value: 'Submitted', label: 'Chờ GVHD xác nhận' },
  { value: 'ALL', label: 'Tất cả trạng thái' },
  { value: 'NeedRevision', label: 'Cần bổ sung' },
  { value: 'AdvisorConfirmed', label: 'Chờ Khoa nghiệm thu' },
  { value: 'ArchiveConfirmed', label: 'Đã nghiệm thu' },
];

export const AdvisorArchivalPage: React.FC = () => {
  const {
    canAccess,
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
    groupDetail,
    packagesQuery,
    isLoadingList,
    isFetchingList,
    listError,
    decision,
    revisionReason,
    reasonError,
    pendingAction,
    isConfirming,
    isHistoryOpen,
    isMetadataOpen,
    downloadingKey,
    actions,
  } = useAdvisorArchival();

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
        key: 'Submitted',
        label: 'Chờ GVHD xác nhận',
        value: stats.submitted,
        tone: 'primary',
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
        key: 'AdvisorConfirmed',
        label: 'Chờ Khoa nghiệm thu',
        value: stats.advisorConfirmed,
        tone: 'warning',
        active: filterKey === 'AdvisorConfirmed',
        onClick: () => actions.changeFilter('AdvisorConfirmed'),
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
        cell: (row) => <span className="font-semibold text-primary">{row.groupCode}</span>,
        skeleton: { variant: 'text' },
      },
      {
        id: 'topicAndStudents',
        header: 'Đề tài & Sinh viên',
        cell: (row) => (
          <div className="min-w-0 space-y-0.5">
            <p className="truncate font-semibold text-slate-900" title={row.topicTitleVi}>
              {row.topicTitleVi}
            </p>
            <p className="truncate text-xs text-slate-500">
              {row.studentNames.length > 0
                ? row.studentNames
                    .map((name, idx) =>
                      row.studentCodes[idx] ? `${name} (${row.studentCodes[idx]})` : name,
                    )
                    .join(' - ')
                : 'Đang tải thông tin sinh viên...'}
            </p>
          </div>
        ),
        skeleton: { variant: 'topic' },
      },
      {
        id: 'version',
        header: 'Phiên bản',
        widthClassName: 'w-28',
        cell: (row) => (
          <span className="text-xs font-semibold text-slate-700">v{row.currentVersionNo}</span>
        ),
        skeleton: { variant: 'text' },
      },
      {
        id: 'status',
        header: 'Trạng thái',
        widthClassName: 'w-44',
        cell: (row) => <StatusBadge tone={row.statusTone}>{row.statusLabel}</StatusBadge>,
        skeleton: { variant: 'badge' },
      },
      {
        id: 'action',
        header: 'Thao tác',
        align: 'right',
        widthClassName: 'w-32',
        cell: (row) => {
          const isSelected = selectedRow?.submissionId === row.submissionId;
          return (
            <div className="flex justify-end" onClick={(event) => event.stopPropagation()}>
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

  if (!canAccess) {
    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <InlineAlert tone="warning" title="Không có quyền truy cập">
          Chức năng này chỉ dành cho Giảng viên hướng dẫn (Lecturer) hoặc Quản trị viên (Admin).
        </InlineAlert>
      </div>
    );
  }

  const pdfFile = submissionDetail?.files.find((f) => f.fileRole === 'ThesisPdf') ?? null;
  const docxFile = submissionDetail?.files.find((f) => f.fileRole === 'ThesisDocx') ?? null;
  const hasFullMetadata = Boolean(
    submissionDetail?.abstractVi &&
    submissionDetail?.abstractEn &&
    submissionDetail?.keywordsVi &&
    submissionDetail?.keywordsEn,
  );
  const canConfirmRow = selectedRow?.status === 'Submitted';

  const detailStudents =
    groupDetail && groupDetail.members.length > 0
      ? groupDetail.members.map((m) => ({
          id: m.studentId,
          name: m.fullName,
          code: m.userCode,
          userCode: m.userCode,
        }))
      : (selectedRow?.studentNames.map((name, idx) => ({
          id: `${selectedRow?.groupId ?? 'row'}-${idx}`,
          name,
          code: selectedRow?.studentCodes[idx] ?? '',
          userCode: selectedRow?.studentCodes[idx] ?? '',
        })) ?? []);

  const advisorDisplayName =
    groupDetail?.advisor?.fullName && groupDetail.advisor.fullName !== 'Chưa phân công'
      ? `${groupDetail.advisor.academicTitle || ''} ${groupDetail.advisor.fullName}`.trim()
      : selectedRow?.advisorName || 'Giảng viên hướng dẫn';

  const detailAdvisorItems =
    advisorDisplayName !== 'Giảng viên hướng dẫn' || groupDetail?.advisor?.advisorId
      ? [{ id: groupDetail?.advisor?.advisorId || advisorDisplayName, name: advisorDisplayName }]
      : [];

  const activeVersion = submissionDetail?.currentVersionNo ?? selectedRow?.currentVersionNo ?? 1;
  const topicCode = groupDetail?.topicCode || 'KLTN';

  return (
    <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
      <PageHeader
        title="Xác nhận lưu chiểu"
        description={
          selectedRow
            ? `GVHD của nhóm ${selectedRow.groupCode} - Trước Khoa nghiệm thu.`
            : 'Kiểm tra bản chỉnh sửa, PDF/DOCX và Metadata Việt/Anh trước khi chuyển Văn phòng Khoa nghiệm thu.'
        }
        icon={<ClipboardCheck />}
        actionsClassName="w-full sm:w-auto flex-1 justify-between items-center"
      >
        <div className="w-full sm:w-80 md:w-[400px]">
          <Select
            aria-label="Chọn đợt KLTN"
            value={selectedBatchId}
            disabled={isLoadingBatches || batchOptions.length === 0}
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
            <RefreshCw className={cn('size-4', isFetchingList && 'animate-spin')} />
          </Button>
        </div>
      </PageHeader>

      <StatisticsBar items={statItems} loading={isLoadingList && allRowsCount === 0} />

      <section className="space-y-4">
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex-1 min-w-[240px]">
            <Input
              value={keyword}
              onChange={(event) => actions.changeKeyword(event.target.value)}
              leftIcon={<Search className="size-4 text-slate-400" />}
              placeholder="Tìm mã nhóm, tên đề tài hoặc tên/MSSV sinh viên..."
              aria-label="Tìm kiếm hồ sơ lưu chiểu"
            />
          </div>
          <div className="w-56">
            <Select
              aria-label="Lọc trạng thái hồ sơ"
              value={filterKey}
              onChange={(value) => actions.changeFilter(value as ArchivalFilterKey)}
              options={FILTER_OPTIONS}
            />
          </div>
          <Button type="button" variant="outline" onClick={actions.resetFilters}>
            Đặt lại
          </Button>
        </div>

        {listError && (
          <InlineAlert
            tone="error"
            title="Không tải được danh sách hồ sơ"
            action={
              <Button type="button" variant="outline" size="sm" onClick={actions.refresh}>
                Thử lại
              </Button>
            }
          >
            {listError}
          </InlineAlert>
        )}

        <DataTable
          ariaLabel="Danh sách hồ sơ lưu chiểu của các nhóm hướng dẫn"
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
                ? 'Các nhóm bạn hướng dẫn sẽ xuất hiện tại đây sau khi nộp hồ sơ lưu chiểu.'
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
                  <div>
                    <CardTitle className="text-base font-bold text-slate-900">
                      Kiểm tra bản v{activeVersion}
                    </CardTitle>
                    <p className="text-xs text-slate-500 mt-0.5">
                      Nhóm {selectedRow.groupCode} - {selectedRow.topicTitleVi}
                    </p>
                  </div>
                  <StatusBadge tone={selectedRow.statusTone}>{selectedRow.statusLabel}</StatusBadge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                {detailError && (
                  <InlineAlert
                    tone="error"
                    title="Không tải được chi tiết hồ sơ"
                    action={
                      <Button type="button" variant="outline" size="sm" onClick={actions.refetchDetail}>
                        Thử lại
                      </Button>
                    }
                  >
                    {detailError}
                  </InlineAlert>
                )}

                {isLoadingDetail ? (
                  <Skeleton className="h-44 w-full rounded-xl" />
                ) : (
                  <div className="overflow-hidden rounded-xl border border-slate-200/80">
                    <table className="w-full text-sm">
                      <thead className="bg-slate-50/80 text-xs font-semibold text-slate-500 border-b border-slate-200/80">
                        <tr>
                          <th className="px-4 py-2.5 text-left">Nội dung</th>
                          <th className="px-4 py-2.5 text-left">Thông tin</th>
                          <th className="px-4 py-2.5 text-left">Trạng thái</th>
                          <th className="px-4 py-2.5 text-right">Chi tiết</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-200/70 bg-white">
                        <tr>
                          <td className="px-4 py-3 font-semibold text-slate-900">PDF + DOCX</td>
                          <td className="px-4 py-3 text-slate-600">Nhóm {selectedRow.groupCode}</td>
                          <td className="px-4 py-3 text-slate-700">
                            {pdfFile && docxFile ? 'Đã nộp' : 'Chưa đủ tệp'}
                          </td>
                          <td className="px-4 py-3 text-right">
                            {pdfFile ? (
                              <button
                                type="button"
                                onClick={() =>
                                  void actions.downloadFile(
                                    activeVersion,
                                    'ThesisPdf',
                                    pdfFile.fileName,
                                  )
                                }
                                disabled={downloadingKey !== null}
                                className="inline-flex items-center gap-1 text-xs font-semibold text-primary hover:underline cursor-pointer disabled:opacity-50"
                              >
                                Xem bản sửa
                                <ExternalLink className="size-3" />
                              </button>
                            ) : (
                              <span className="text-xs text-slate-400">—</span>
                            )}
                          </td>
                        </tr>
                        <tr>
                          <td className="px-4 py-3 font-semibold text-slate-900">
                            Metadata Việt/Anh
                          </td>
                          <td className="px-4 py-3 text-slate-600">Nhóm {selectedRow.groupCode}</td>
                          <td className="px-4 py-3 text-slate-700">
                            {hasFullMetadata ? 'Đủ trường' : 'Chưa đủ'}
                          </td>
                          <td className="px-4 py-3 text-right">
                            <button
                              type="button"
                              onClick={actions.openMetadata}
                              className="inline-flex items-center gap-1 text-xs font-semibold text-slate-700 hover:text-primary hover:underline cursor-pointer"
                            >
                              <FileSearch className="size-3.5" />
                              Đối chiếu
                            </button>
                          </td>
                        </tr>
                        <tr>
                          <td className="px-4 py-3 font-semibold text-slate-900">Góp ý HĐ</td>
                          <td className="px-4 py-3 text-slate-600">Biên bản</td>
                          <td className="px-4 py-3 text-slate-700">Đã khóa (hợp lệ)</td>
                          <td className="px-4 py-3 text-right text-xs text-slate-400">—</td>
                        </tr>
                      </tbody>
                    </table>
                    <div className="border-t border-slate-200/70 bg-slate-50/50 px-4 py-2 text-xs text-slate-500">
                      3 mục trong danh sách
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base font-bold text-slate-900">
                  Nhận xét & Xử lý
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {canConfirmRow ? (
                  <>
                    <div className="space-y-2">
                      <span className="block text-xs font-semibold text-slate-700">
                        Trạng thái xác nhận của GVHD
                      </span>
                      <div
                        className="flex flex-wrap items-center gap-2.5"
                        role="group"
                        aria-label="Chọn kết quả xác nhận"
                      >
                        <Button
                          type="button"
                          variant={decision === 'AdvisorConfirm' ? 'default' : 'outline'}
                          leftIcon={<CheckCircle2 className="size-4" />}
                          onClick={() => actions.selectDecision('AdvisorConfirm')}
                          disabled={isConfirming}
                        >
                          Đồng ý
                        </Button>
                        <Button
                          type="button"
                          variant={decision === 'RequestRevision' ? 'destructive' : 'outline'}
                          leftIcon={<RotateCcw className="size-4" />}
                          onClick={() => actions.selectDecision('RequestRevision')}
                          disabled={isConfirming}
                        >
                          Yêu cầu chỉnh sửa
                        </Button>
                      </div>
                    </div>

                    <div className="space-y-1.5">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <label
                          htmlFor="advisor-archival-reason"
                          className="text-xs font-semibold text-slate-700"
                        >
                          Lý do yêu cầu chỉnh sửa{' '}
                          <span className="font-normal text-slate-500">
                            (bắt buộc khi Yêu cầu chỉnh sửa; tự động khóa khi chọn Đồng ý)
                          </span>
                        </label>
                        {decision === 'RequestRevision' && (
                          <span className="text-[11px] text-slate-500">
                            {revisionReason.length}/{ARCHIVAL_REASON_MAX_LENGTH}
                          </span>
                        )}
                      </div>
                      <textarea
                        id="advisor-archival-reason"
                        rows={4}
                        value={decision === 'AdvisorConfirm' ? '' : revisionReason}
                        onChange={(event) => actions.setRevisionReason(event.target.value)}
                        disabled={decision === 'AdvisorConfirm' || isConfirming || isLoadingDetail}
                        placeholder={
                          decision === 'AdvisorConfirm'
                            ? 'Đã chọn Đồng ý — hệ thống không lưu nhận xét khi xác nhận bản sửa đạt yêu cầu.'
                            : 'Nhập nội dung sinh viên cần bổ sung hoặc chỉnh sửa lại trước khi chuyển Khoa...'
                        }
                        className={cn(
                          'w-full rounded-xl border border-slate-200 px-3.5 py-2.5 text-sm text-slate-900 placeholder:text-slate-400 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 transition-colors',
                          decision === 'AdvisorConfirm'
                            ? 'bg-slate-100/80 text-slate-500 cursor-not-allowed'
                            : 'bg-white',
                        )}
                      />
                      {reasonError && (
                        <p className="text-xs font-medium text-rose-600">{reasonError}</p>
                      )}
                    </div>

                    <div className="pt-2 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100">
                      <p className="text-xs text-slate-500">
                        {decision === 'AdvisorConfirm'
                          ? 'Xác nhận bản sửa đạt yêu cầu để chuyển hồ sơ sang Văn phòng Khoa nghiệm thu.'
                          : 'Trả hồ sơ về trạng thái Cần bổ sung để sinh viên cập nhật và nộp lại phiên bản mới.'}
                      </p>
                      <Button
                        type="button"
                        variant={decision === 'AdvisorConfirm' ? 'default' : 'destructive'}
                        onClick={actions.openDecisionConfirm}
                        disabled={isLoadingDetail || !submissionDetail || isConfirming}
                        isLoading={isConfirming}
                      >
                        {decision === 'AdvisorConfirm'
                          ? 'Xác nhận bản sửa'
                          : 'Gửi yêu cầu chỉnh sửa'}
                      </Button>
                    </div>
                  </>
                ) : (
                  <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-4 space-y-2">
                    <div className="flex items-center gap-2 text-sm font-semibold text-slate-900">
                      <Lock className="size-4 text-slate-500" />
                      <span>
                        {selectedRow.status === 'AdvisorConfirmed'
                          ? 'Bạn đã xác nhận bản chỉnh sửa này và chuyển sang Khoa.'
                          : selectedRow.status === 'ArchiveConfirmed'
                            ? 'Khoa đã hoàn thành nghiệm thu lưu chiểu.'
                            : 'Hồ sơ đang ở trạng thái Cần bổ sung — chờ sinh viên nộp lại bản mới.'}
                      </span>
                    </div>
                    {submissionDetail?.advisorConfirmedAt && (
                      <p className="text-xs text-slate-600">
                        Thời điểm GVHD xác nhận: {formatDateTimeVi(submissionDetail.advisorConfirmedAt)}
                      </p>
                    )}
                    {submissionDetail?.facultyFeedback && (
                      <p className="text-xs text-slate-700 whitespace-pre-wrap">
                        Nội dung yêu cầu sửa gần nhất: {submissionDetail.facultyFeedback}
                      </p>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>

          <div className="lg:col-span-5 xl:col-span-4 space-y-5">
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base font-bold text-slate-900">
                  Hồ sơ lưu chiểu v{activeVersion}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4 text-sm">
                <div className="space-y-1">
                  <p className="text-xs font-medium text-slate-500">Hồ sơ</p>
                  <p className="font-semibold text-slate-900">
                    {topicCode} - Nhóm {selectedRow.groupCode}
                  </p>
                  <p className="text-xs text-slate-600">{selectedRow.topicTitleVi}</p>
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
                        {advisorDisplayName}
                      </span>
                    </div>
                  ) : (
                    <p className="text-xs text-slate-500">{advisorDisplayName}</p>
                  )}
                </div>

                <div className="space-y-1 pt-2 border-t border-slate-100">
                  <p className="text-xs font-medium text-slate-500">Đợt</p>
                  <p className="text-xs font-medium text-slate-800">{selectedBatchLabel}</p>
                </div>

                <div className="space-y-1.5 pt-2 border-t border-slate-100">
                  <p className="text-xs font-semibold text-slate-800">Tài liệu đối chiếu</p>
                  <div className="space-y-1 text-xs text-slate-600">
                    <p className="truncate" title={pdfFile?.fileName}>
                      Bản PDF - {pdfFile?.fileName ?? 'Chưa có'}
                    </p>
                    <p className="truncate" title={docxFile?.fileName}>
                      Bản DOCX - {docxFile?.fileName ?? 'Chưa có'}
                    </p>
                    <p>Metadata - Tóm tắt và từ khóa Việt / Anh</p>
                  </div>
                </div>

                <div className="space-y-1.5 pt-2 border-t border-slate-100">
                  <p className="text-xs font-semibold text-slate-800">Lưu ý xử lý</p>
                  <ul className="space-y-1 text-xs text-slate-600 list-disc pl-4">
                    <li>Đối chiếu các góp ý sau bảo vệ</li>
                    <li>Kiểm tra PDF và DOCX cùng phiên bản</li>
                    <li>Xác nhận để chuyển Khoa nghiệm thu</li>
                  </ul>
                </div>

                <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-slate-100">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!pdfFile || downloadingKey === `${activeVersion}-ThesisPdf`}
                    isLoading={downloadingKey === `${activeVersion}-ThesisPdf`}
                    leftIcon={<Download className="size-3.5" />}
                    onClick={() =>
                      pdfFile &&
                      void actions.downloadFile(activeVersion, 'ThesisPdf', pdfFile.fileName)
                    }
                  >
                    Tải PDF
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!docxFile || downloadingKey === `${activeVersion}-ThesisDocx`}
                    isLoading={downloadingKey === `${activeVersion}-ThesisDocx`}
                    leftIcon={<FileText className="size-3.5" />}
                    onClick={() =>
                      docxFile &&
                      void actions.downloadFile(activeVersion, 'ThesisDocx', docxFile.fileName)
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

      <Dialog
        open={isMetadataOpen}
        onClose={actions.closeMetadata}
        closeOnBackdropClick
        title={`Đối chiếu Metadata Việt / Anh - Phiên bản v${activeVersion}`}
        className="max-w-3xl"
      >
        <div className="space-y-4">
          <div className="rounded-xl border border-slate-200/80 bg-slate-50/60 p-4 space-y-1.5">
            <p className="text-xs font-semibold text-slate-500">Tóm tắt tiếng Việt</p>
            <p className="text-sm text-slate-900 whitespace-pre-wrap leading-relaxed">
              {submissionDetail?.abstractVi || 'Chưa cập nhật'}
            </p>
          </div>
          <div className="rounded-xl border border-slate-200/80 bg-slate-50/60 p-4 space-y-1.5">
            <p className="text-xs font-semibold text-slate-500">Abstract tiếng Anh</p>
            <p className="text-sm text-slate-900 whitespace-pre-wrap leading-relaxed">
              {submissionDetail?.abstractEn || 'Chưa cập nhật'}
            </p>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="rounded-xl border border-slate-200/80 bg-slate-50/60 p-4 space-y-1.5">
              <p className="text-xs font-semibold text-slate-500">Từ khóa tiếng Việt</p>
              <p className="text-sm font-medium text-slate-900">
                {submissionDetail?.keywordsVi || 'Chưa cập nhật'}
              </p>
            </div>
            <div className="rounded-xl border border-slate-200/80 bg-slate-50/60 p-4 space-y-1.5">
              <p className="text-xs font-semibold text-slate-500">Keywords tiếng Anh</p>
              <p className="text-sm font-medium text-slate-900">
                {submissionDetail?.keywordsEn || 'Chưa cập nhật'}
              </p>
            </div>
          </div>
          <div className="flex justify-end pt-2">
            <Button type="button" variant="outline" onClick={actions.closeMetadata}>
              Đóng
            </Button>
          </div>
        </div>
      </Dialog>

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

      <ConfirmDialog
        open={pendingAction !== null}
        onClose={actions.closeDecisionConfirm}
        onConfirm={actions.confirmDecision}
        isConfirming={isConfirming}
        type={pendingAction === 'RequestRevision' ? 'warning' : 'brand'}
        title={
          pendingAction === 'AdvisorConfirm'
            ? `Xác nhận bản chỉnh sửa - Nhóm ${selectedRow?.groupCode ?? ''}`
            : `Gửi yêu cầu chỉnh sửa - Nhóm ${selectedRow?.groupCode ?? ''}`
        }
        confirmText={
          pendingAction === 'AdvisorConfirm' ? 'Xác nhận bản sửa' : 'Gửi yêu cầu sửa'
        }
        cancelText="Hủy"
        description={
          pendingAction === 'AdvisorConfirm' ? (
            <div className="space-y-2 text-sm text-slate-600">
              <p>
                Xác nhận phiên bản <strong>v{activeVersion}</strong> (PDF, DOCX và Metadata
                Việt/Anh) của nhóm <strong>{selectedRow?.groupCode}</strong> đã đạt yêu cầu.
              </p>
              <div className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">
                Sau khi xác nhận, hồ sơ sẽ được chuyển sang Văn phòng Khoa nghiệm thu lưu chiểu.
              </div>
            </div>
          ) : (
            <div className="space-y-2 text-sm text-slate-600">
              <p>
                Hồ sơ nhóm <strong>{selectedRow?.groupCode}</strong> sẽ chuyển về trạng thái{' '}
                <strong>Cần bổ sung</strong> để sinh viên chỉnh sửa và nộp lại.
              </p>
              <div className="rounded-xl border border-amber-200 bg-amber-50/70 px-3 py-2 text-xs text-amber-900">
                Lý do: {revisionReason.trim()}
              </div>
            </div>
          )
        }
      />
    </div>
  );
};
