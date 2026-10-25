import React from 'react';
import { UsersRound, CheckCircle2, AlertCircle } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
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
    ? `${data.selectedBatch.batchName} · HK${data.selectedBatch.semester} ${data.selectedBatch.academicYear}`
    : undefined;

  return (
    <div className="space-y-6 pb-12 max-w-7xl mx-auto min-w-0">
      <PageHeader
        title="Danh sách nhà trường"
        icon={<UsersRound />}
      >
      </PageHeader>

      {roster.notice && (
        <div
          role="status"
          className="rounded-xl bg-emerald-50 border border-emerald-200 p-4 text-xs sm:text-sm text-emerald-800 flex items-center justify-between shadow-xs transition-all animate-in fade-in"
        >
          <div className="flex items-center gap-2.5">
            <CheckCircle2 className="size-5 text-emerald-600 shrink-0" />
            <span className="font-medium">{roster.notice}</span>
          </div>
          <button
            type="button"
            onClick={() => roster.setNotice(null)}
            className="text-emerald-700 hover:text-emerald-950 font-bold ml-3 text-xs underline cursor-pointer"
          >
            Đóng
          </button>
        </div>
      )}

      {roster.errorMessage && (
        <div
          role="alert"
          className="rounded-xl bg-rose-50 border border-rose-200 p-4 text-xs sm:text-sm text-rose-800 flex items-center justify-between shadow-xs transition-all animate-in fade-in"
        >
          <div className="flex items-center gap-2.5">
            <AlertCircle className="size-5 text-rose-600 shrink-0" />
            <span className="font-medium">{roster.errorMessage}</span>
          </div>
          <button
            type="button"
            onClick={() => roster.setErrorMessage(null)}
            className="text-rose-700 hover:text-rose-950 font-bold ml-3 text-xs underline cursor-pointer"
          >
            Đóng
          </button>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
        <div className="lg:col-span-8">
          <RosterConfigCard
            activeRole={roster.activeRole}
            onSwitchRole={roster.handleSwitchRole}
            batches={data.batches}
            selectedBatchId={data.selectedBatchId}
            onSelectBatchId={data.setSelectedBatchId}
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

