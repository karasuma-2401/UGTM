import { useCallback, useMemo, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Plus, RefreshCw } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { PageHeader } from '@/components/ui/page-header';
import { Card } from '@/components/ui/card';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { getCurrentBatches } from '@/shared/academic-periods/currentBatches';
import { useAuth } from '@/shared/auth';
import { CouncilCandidatePicker } from '../components/council-members/CouncilCandidatePicker';
import { CouncilCreateCard } from '../components/council-members/CouncilCreateCard';
import { CouncilPickerPanel, type PhaseFilter } from '../components/council-members/CouncilPickerPanel';
import { CouncilRosterCard } from '../components/council-members/CouncilRosterCard';
import { CouncilSetupCard } from '../components/council-members/CouncilSetupCard';
import { nativeFieldClass } from '../components/council-members/memberFieldStyles';
import { CouncilSummaryCard } from '../components/council-members/CouncilSummaryCard';
import {
  councilCandidatesKey,
  errorKeyFor,
  useCouncilCandidates,
  useCouncilDrafts,
  useCouncilMemberActions,
} from '../hooks/useCouncilMembers';
import { memberError, type CouncilRole } from '../services/councilMembersApi';
import { councilScheduleKey } from '../services/councilPlanningApi';
import { councilPhase, rosterFromCouncil, type RosterMember } from '../utils/councilMemberWorkspace';

export function CouncilMembersPage() {
  const { user, hasRole } = useAuth();
  const queryClient = useQueryClient();
  const batchesQuery = useAcademicBatches();
  const [batchOverride, setBatchOverride] = useState<string | null>(null);
  const [keyword, setKeyword] = useState('');
  const [phaseFilter, setPhaseFilter] = useState<PhaseFilter>('all');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [notesByCouncil, setNotesByCouncil] = useState<Record<string, string>>({});
  const [candidateKeywords, setCandidateKeywords] = useState<Record<string, string>>({});
  const [removeTarget, setRemoveTarget] = useState<{ councilId: string; member: RosterMember } | null>(null);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [creating, setCreating] = useState(false);
  const [memberPickerRole, setMemberPickerRole] = useState<CouncilRole | null>(null);
  const rosterHeadingRef = useRef<HTMLHeadingElement>(null);

  const batches = batchesQuery.data;
  // Mặc định lọc theo đợt đang diễn ra: tránh tải mọi hội đồng của mọi đợt.
  const defaultBatchId = useMemo(() => getCurrentBatches(batches ?? [])[0]?.id ?? 'all', [batches]);
  const batchId = batchOverride ?? defaultBatchId;
  const batchesLoading = batchesQuery.isPending && batchesQuery.fetchStatus === 'fetching';
  const batchOptions = useMemo(
    () => (batches ?? []).map((batch) => ({ ...batch, label: `${batch.batchName} · ${batch.academicYear}` })),
    [batches],
  );

  const draftsQuery = useCouncilDrafts(user?.id, batchId, !batchesLoading);
  const councils = useMemo(() => draftsQuery.data ?? [], [draftsQuery.data]);
  const visibleCouncils = useMemo(() => {
    const needle = keyword.trim().toLocaleLowerCase('vi');
    return councils.filter((item) => {
      if (phaseFilter !== 'all' && councilPhase(item) !== phaseFilter) return false;
      if (!needle) return true;
      return [item.councilCode, item.facultyName, item.room, ...item.groupCodes].some((value) =>
        value.toLocaleLowerCase('vi').includes(needle),
      );
    });
  }, [councils, keyword, phaseFilter]);
  const phaseCounts = useMemo(() => ({
    preparing: councils.filter((item) => councilPhase(item) === 'preparing').length,
    submitted: councils.filter((item) => councilPhase(item) === 'submitted').length,
    approved: councils.filter((item) => councilPhase(item) === 'approved').length,
  }), [councils]);
  // Giữ hội đồng đang chọn nếu còn trong bộ lọc; tránh hiển thị chi tiết của một dòng đã bị ẩn.
  const selectedCouncil =
    visibleCouncils.find((item) => item.id === selectedId) ?? visibleCouncils[0] ?? null;
  const roster = useMemo(() => (selectedCouncil ? rosterFromCouncil(selectedCouncil) : []), [selectedCouncil]);
  const editable = selectedCouncil?.status === 'Draft';
  const candidatesQuery = useCouncilCandidates(selectedCouncil?.id, Boolean(editable));
  const actions = useCouncilMemberActions();

  const councilId = selectedCouncil?.id ?? '';
  const pendingKey = actions.pending[councilId];
  const rosterError = actions.errors[errorKeyFor(councilId, 'roster')] ?? '';
  const addError = actions.errors[errorKeyFor(councilId, 'add')] ?? '';
  const submitError = actions.errors[errorKeyFor(councilId, 'submit')] ?? '';
  const notes = notesByCouncil[councilId] ?? '';
  const candidateKeyword = candidateKeywords[councilId] ?? '';

  const openMemberPicker = useCallback(() => {
    const nextRole: CouncilRole = !roster.some((member) => member.role === 'Chairman')
      ? 'Chairman'
      : !roster.some((member) => member.role === 'Secretary') ? 'Secretary' : 'Member';
    setMemberPickerRole(nextRole);
  }, [roster]);

  const addMember = useCallback(
    async (
      candidate: { userId: string; fullName: string; isReviewerForCouncilGroups?: boolean },
      role: CouncilRole,
    ) => {
      if (!selectedCouncil) return;
      const ok = await actions.run({
        council: selectedCouncil,
        roster,
        action: {
          type: 'add',
          userId: candidate.userId,
          fullName: candidate.fullName,
          role,
          isReviewer: candidate.isReviewerForCouncilGroups,
        },
      });
      if (ok) {
        setCandidateKeywords((current) => ({ ...current, [selectedCouncil.id]: '' }));
        setMemberPickerRole(null);
      }
    },
    [actions, roster, selectedCouncil],
  );

  const changeRole = useCallback(
    (member: RosterMember, role: CouncilRole) => {
      if (!selectedCouncil || role === member.role) return;
      void actions.run({
        council: selectedCouncil,
        roster,
        action: { type: 'role', userId: member.userId, fullName: member.fullName, role },
      });
    },
    [actions, roster, selectedCouncil],
  );

  const removeMember = async () => {
    const target = removeTarget;
    const council = councils.find((item) => item.id === target?.councilId);
    if (!target || !council) return;
    const ok = await actions.run({
      council,
      roster: rosterFromCouncil(council),
      action: { type: 'remove', userId: target.member.userId, fullName: target.member.fullName },
    });
    setRemoveTarget(null);
    if (ok) rosterHeadingRef.current?.focus();
  };

  const submitCouncil = async () => {
    if (!selectedCouncil) return;
    const ok = await actions.run({ council: selectedCouncil, roster, action: { type: 'submit', notes } });
    setConfirmSubmit(false);
    if (ok) setNotesByCouncil((current) => ({ ...current, [selectedCouncil.id]: '' }));
  };

  const refresh = () => {
    void draftsQuery.refetch();
    if (selectedCouncil) void queryClient.invalidateQueries({ queryKey: councilScheduleKey(selectedCouncil.id) });
    if (selectedCouncil && editable) {
      void queryClient.invalidateQueries({ queryKey: councilCandidatesKey(selectedCouncil.id) });
    }
  };

  const removeCouncil = removeTarget ? councils.find((item) => item.id === removeTarget.councilId) : undefined;
  const isSubmittingThis = pendingKey === 'submit';

  return (
    <div className="mx-auto max-w-[1440px] space-y-5 px-4 py-6 sm:px-6">
      <PageHeader
        title="Lập hội đồng bảo vệ"
        description={
          <span className="block max-w-[68ch]">
            Khoa tạo hội đồng, xếp lịch và nhóm bảo vệ, phân công giảng viên rồi gửi hồ sơ lên PĐT. PĐT phê duyệt ở màn hình riêng.
            {hasRole('Admin') && (
              <>
                {' '}
                <Link to="/councils/approval" className="font-medium text-primary underline-offset-4 hover:underline">
                  Mở màn hình Phê duyệt hội đồng
                </Link>
              </>
            )}
          </span>
        }
        actionsClassName="ml-auto justify-end"
        accent
      >
        <Button type="button" size="sm" onClick={() => setCreating(true)} disabled={creating || batchesLoading || !batchOptions.length} aria-expanded={creating}>
          <Plus className="size-4" aria-hidden="true" />
          Tạo hội đồng
        </Button>
        <Button type="button" variant="outline" size="sm" isLoading={draftsQuery.isFetching && !draftsQuery.isLoading} loadingText="Đang làm mới…" onClick={refresh}>
          <RefreshCw className="size-4" aria-hidden="true" />
          Làm mới dữ liệu
        </Button>
      </PageHeader>

      {creating && (
        <CouncilCreateCard
          batches={batchOptions}
          initialBatchId={batchId}
          onCancel={() => setCreating(false)}
          onCreated={async (id, createdBatchId) => {
            setBatchOverride(createdBatchId);
            setPhaseFilter('all');
            setKeyword('');
            setSelectedId(id);
            setCreating(false);
            await queryClient.invalidateQueries({ queryKey: ['council-drafts'] });
          }}
        />
      )}

      <StatisticsBar
        loading={draftsQuery.isLoading || batchesLoading}
        className="grid-flow-row grid-cols-2 sm:grid-flow-col sm:grid-cols-none"
        items={[
          { key: 'all', label: 'Tất cả hội đồng', value: councils.length, tone: 'muted', active: phaseFilter === 'all', onClick: () => setPhaseFilter('all') },
          { key: 'preparing', label: 'Đang chuẩn bị', value: phaseCounts.preparing, tone: 'warning', active: phaseFilter === 'preparing', onClick: () => setPhaseFilter('preparing') },
          { key: 'submitted', label: 'Đã gửi PĐT', value: phaseCounts.submitted, tone: 'primary', active: phaseFilter === 'submitted', onClick: () => setPhaseFilter('submitted') },
          { key: 'approved', label: 'Đã duyệt / hoàn thành', value: phaseCounts.approved, tone: 'success', active: phaseFilter === 'approved', onClick: () => setPhaseFilter('approved') },
        ]}
      />

      <div className="grid gap-5 lg:grid-cols-[minmax(320px,400px)_minmax(0,1fr)] lg:items-start">
        <CouncilPickerPanel
          councils={visibleCouncils}
          totalCount={councils.length}
          selectedId={selectedCouncil?.id ?? null}
          onSelect={(id) => {
            actions.clearErrors(id);
            setSelectedId(id);
          }}
          batchId={batchId}
          onBatchChange={(id) => setBatchOverride(id)}
          batchOptions={batchOptions}
          keyword={keyword}
          onKeywordChange={setKeyword}
          phaseFilter={phaseFilter}
          onPhaseFilterChange={setPhaseFilter}
          isLoading={draftsQuery.isLoading || batchesLoading}
          error={draftsQuery.isError ? memberError(draftsQuery.error) : null}
          onRetry={() => void draftsQuery.refetch()}
        />

        <div className="min-w-0 space-y-5">
          {!selectedCouncil ? (
            <Card className="p-8 text-center text-sm text-ink-muted">
              {draftsQuery.isLoading || batchesLoading
                ? 'Đang tải hội đồng…'
                : 'Chọn một hội đồng ở danh sách bên trái để xem và phân công thành viên.'}
            </Card>
          ) : (
            <>
              <CouncilSetupCard
                key={selectedCouncil.id}
                council={selectedCouncil}
                batch={batches?.find((item) => item.id === selectedCouncil.batchId)}
                onUpdated={async () => {
                  await Promise.all([
                    queryClient.invalidateQueries({ queryKey: ['council-drafts'] }),
                    queryClient.invalidateQueries({ queryKey: councilScheduleKey(selectedCouncil.id) }),
                    queryClient.invalidateQueries({ queryKey: councilCandidatesKey(selectedCouncil.id) }),
                  ]);
                }}
              />
              <CouncilRosterCard
                ref={rosterHeadingRef}
                councilCode={selectedCouncil.councilCode}
                roster={roster}
                candidates={candidatesQuery.data}
                candidatesError={candidatesQuery.isError}
                editable={Boolean(editable)}
                submitted={councilPhase(selectedCouncil) === 'submitted'}
                pendingKey={pendingKey}
                error={rosterError}
                onChangeRole={changeRole}
                onRemove={(member) => setRemoveTarget({ councilId: selectedCouncil.id, member })}
                onAdd={openMemberPicker}
              />
              <CouncilSummaryCard
                council={selectedCouncil}
                roster={roster}
                isAdminActing={hasRole('Admin')}
                submitting={isSubmittingThis}
                busyElsewhere={Boolean(pendingKey) && !isSubmittingThis}
                submitError={submitError}
                onSubmit={() => setConfirmSubmit(true)}
              />
            </>
          )}
        </div>
      </div>

      {editable && selectedCouncil && memberPickerRole && (
        <CouncilCandidatePicker
          key={selectedCouncil.id}
          councilCode={selectedCouncil.councilCode}
          candidates={candidatesQuery.data}
          isLoading={candidatesQuery.isLoading}
          error={candidatesQuery.isError ? memberError(candidatesQuery.error) : null}
          onRetry={() => void candidatesQuery.refetch()}
          roster={roster}
          keyword={candidateKeyword}
          onKeywordChange={(value) => setCandidateKeywords((current) => ({ ...current, [selectedCouncil.id]: value }))}
          role={memberPickerRole}
          onRoleChange={setMemberPickerRole}
          pendingKey={pendingKey}
          actionError={addError}
          onAdd={(candidate, role) => void addMember(candidate, role)}
          onClose={() => { if (!pendingKey) setMemberPickerRole(null); }}
        />
      )}

      <ConfirmDialog
        open={Boolean(removeTarget)}
        onClose={() => !pendingKey && setRemoveTarget(null)}
        onConfirm={() => void removeMember()}
        type="warning"
        title={`Gỡ ${removeTarget?.member.fullName ?? 'thành viên'} khỏi ${removeCouncil?.councilCode ?? 'hội đồng'}?`}
        description={
          removeCouncil?.isSubmittedToAcademicAffairs
            ? 'Hồ sơ đã gửi PĐT sẽ bị rút lại; Khoa cần gửi lại sau khi chỉnh xong. Bạn vẫn có thể thêm lại giảng viên này.'
            : 'Giảng viên sẽ được gỡ khỏi hội đồng này. Bạn vẫn có thể thêm lại sau.'
        }
        confirmText="Gỡ khỏi hội đồng"
        cancelText="Quay lại"
        isConfirming={Boolean(removeTarget) && pendingKey === `remove:${removeTarget?.member.userId}`}
      />
      <ConfirmDialog
        open={confirmSubmit}
        onClose={() => !isSubmittingThis && setConfirmSubmit(false)}
        onConfirm={() => void submitCouncil()}
        title={`Gửi ${selectedCouncil?.councilCode ?? 'hội đồng'} lên PĐT?`}
        description={
          <div className="space-y-3">
            <p>
              PĐT sẽ nhận hồ sơ để kiểm tra và phê duyệt. Sau khi gửi, nếu Khoa sửa thành viên thì hồ sơ bị rút lại và
              phải gửi lại.
            </p>
            <div className="space-y-1.5">
              <label htmlFor="submission-notes" className="text-sm font-medium text-ink-secondary">
                Ghi chú cho PĐT (không bắt buộc)
              </label>
              <textarea
                id="submission-notes"
                maxLength={500}
                rows={3}
                value={notes}
                disabled={isSubmittingThis}
                onChange={(event) =>
                  selectedCouncil &&
                  setNotesByCouncil((current) => ({ ...current, [selectedCouncil.id]: event.target.value }))
                }
                className={`${nativeFieldClass} h-auto py-2`}
                placeholder="Thông tin cần PĐT lưu ý"
              />
              <p className="text-right text-xs text-ink-muted">{notes.length}/500</p>
            </div>
          </div>
        }
        confirmText="Gửi thẩm định"
        cancelText="Quay lại"
        isConfirming={isSubmittingThis}
      />
    </div>
  );
}
