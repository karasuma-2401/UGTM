import { OperationToast } from '@/shared/notifications';
import React, { useMemo } from 'react';
import { RotateCcw, RefreshCw } from 'lucide-react';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { UserFormDialog } from '@/features/identity/components/users/UserFormDialog';
import { userApi, getUserErrorMessage } from '@/features/identity/services/userApi';
import type { UserFormValues } from '@/features/identity/types/user.types';
import {
  RosterConfigCard,
  RosterGuideCard,
  RosterPreviewTable,
} from '../components/roster';
import { useSchoolRoster } from '../hooks/roster';

export const SchoolRosterPage: React.FC = () => {
  const { data, roster } = useSchoolRoster();
  const [isSingleSaving, setIsSingleSaving] = React.useState(false);
  const [singleError, setSingleError] = React.useState<string | null>(null);

  const activeErrorMessage = roster.errorMessage || data.loadError;

  const handleDismissError = () => {
    roster.setErrorMessage(null);
    data.setLoadError(null);
  };

  const handleRetryLoad = () => {
    roster.setErrorMessage(null);
    void data.reloadData();
  };

  const handleSingleSave = async (values: UserFormValues) => {
    setIsSingleSaving(true);
    setSingleError(null);
    try {
      const created = await userApi.createUser(values);
      roster.setNotice(
        `Đã tạo thành công tài khoản ${created.fullName} (${created.userCode || created.email}) ở trạng thái Chờ kích hoạt.`,
      );
      roster.setIsSingleAddOpen(false);
      await data.refreshUsers();
    } catch (err) {
      setSingleError(getUserErrorMessage(err));
    } finally {
      setIsSingleSaving(false);
    }
  };

  const batchOptions = useMemo(
    () =>
      data.batches.map((b) => {
        let statusBadge = 'Bản nháp';
        if (b.status === 'Open') statusBadge = 'Đang hoạt động';
        else if (b.status === 'Closed') statusBadge = 'Đã đóng';
        else if (b.status === 'Archived') statusBadge = 'Đã lưu trữ';

        return {
          value: b.id,
          label: `${b.batchName} (Học kỳ ${b.semester} - Năm học ${b.academicYear}) [${statusBadge}]`,
        };
      }),
    [data.batches],
  );

  const batchLabel = data.selectedBatch
    ? `${data.selectedBatch.batchName} - Học kỳ ${data.selectedBatch.semester} (Năm học ${data.selectedBatch.academicYear})`
    : undefined;

  return (
    <div className="space-y-6 pb-12 animate-in fade-in-50 duration-300">
      <PageHeader
        title="Danh sách Sinh viên & Giảng viên"
        description="Nhập và đối chiếu danh sách sinh viên đủ điều kiện khóa luận và giảng viên hướng dẫn theo từng đợt đào tạo"
      >
        <div className="w-full sm:w-80 md:w-96">
          <Select
            options={batchOptions}
            value={data.selectedBatchId}
            onChange={(val) => data.setSelectedBatchId(String(val ?? ''))}
            disabled={data.isLoadingBatches || data.batches.length === 0}
            placeholder={data.isLoadingBatches ? 'Đang tải đợt khóa luận...' : 'Chưa có đợt khóa luận'}
            size="lg"
            triggerClassName="h-10 rounded-xl border-slate-200/90 bg-white text-xs font-semibold text-slate-800 shadow-2xs hover:border-slate-300 transition-all select-none"
          />
        </div>

        <Button
          type="button"
          variant="outline"
          size="icon"
          onClick={handleRetryLoad}
          disabled={data.isLoadingBatches}
          className="size-10 rounded-xl border-slate-200 bg-white text-slate-500 hover:text-slate-900 shadow-2xs active:scale-[0.98] transition-all"
          title="Tải lại dữ liệu"
        >
          <RefreshCw className={`size-4 ${data.isLoadingBatches ? 'animate-spin' : ''}`} />
        </Button>
      </PageHeader>

      <OperationToast message={roster.notice} onConsumed={() => roster.setNotice(null)} />

      {activeErrorMessage && (
        <InlineAlert
          onDismiss={handleDismissError}
          action={
            data.loadError ? (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleRetryLoad}
                disabled={data.isLoadingBatches}
                className="h-8 text-xs border-rose-300 bg-white text-rose-700 hover:bg-rose-100 cursor-pointer"
              >
                <RotateCcw className="size-3.5 mr-1.5" />
                Thử lại
              </Button>
            ) : undefined
          }
        >
          {activeErrorMessage}
        </InlineAlert>
      )}

      {/* Row 1: Config & Upload Card (8 cols) + Bento Guidelines Card (4 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
        <div className="lg:col-span-8">
          <RosterConfigCard
            activeRole={roster.activeRole}
            onSwitchRole={roster.handleSwitchRole}
            batches={data.batches}
            selectedBatchId={data.selectedBatchId}
            onSelectBatchId={data.setSelectedBatchId}
            isLoadingBatches={data.isLoadingBatches}
            fileInfo={roster.fileInfo}
            onSelectFile={roster.handleSelectFile}
            onDownloadTemplate={roster.handleDownloadTemplate}
            isDownloading={roster.isDownloading}
            onOpenSingleAdd={() => {
              setSingleError(null);
              roster.setIsSingleAddOpen(true);
            }}
          />
        </div>

        <div className="lg:col-span-4">
          <RosterGuideCard />
        </div>
      </div>

      {/* Row 2: Preview & Validation Table */}
      <RosterPreviewTable
        rows={roster.validatedRows}
        summary={roster.summary}
        isSubmitting={roster.isSubmitting}
        onSelectNewFile={roster.handleSelectFile}
        onSubmit={roster.handleSubmitImport}
        batchLabel={batchLabel}
        roleLabel={roster.activeRole === 'Student' ? 'sinh viên' : 'giảng viên'}
      />

      {roster.isSingleAddOpen && (
        <UserFormDialog
          accountGroup={roster.activeRole === 'Student' ? 'Student' : 'Staff'}
          user={null}
          isSaving={isSingleSaving}
          error={singleError}
          onSave={handleSingleSave}
          onClose={() => roster.setIsSingleAddOpen(false)}
        />
      )}
    </div>
  );
};
