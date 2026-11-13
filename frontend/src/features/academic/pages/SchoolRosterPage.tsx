import { OperationToast } from '@/shared/notifications';
import React from 'react';
import { UsersRound, RotateCcw } from 'lucide-react';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
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

  const batchLabel = data.selectedBatch
    ? `${data.selectedBatch.batchName} - HK${data.selectedBatch.semester} ${data.selectedBatch.academicYear}`
    : undefined;

  return (
    <div className="space-y-6 pb-12 max-w-7xl mx-auto min-w-0">
      <PageHeader
        title="Danh sách nhà trường"
        icon={<UsersRound />}
      >
      </PageHeader>

      <OperationToast message={roster.notice} onConsumed={() => roster.setNotice(null)} />

      {activeErrorMessage && (
        <InlineAlert onDismiss={handleDismissError} action={data.loadError ? (
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
            ) : undefined}>
          {activeErrorMessage}
        </InlineAlert>
      )}

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

