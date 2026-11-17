import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { notify } from '@/shared/notifications';
import { archivalApi } from '../services/archivalApi';
import { finalReportApi } from '../services/finalReportApi';
import { syllabusApi } from '../services/syllabusApi';
import type { ArchivalMetadataInput } from '../types/archival.types';
import {
  triggerBrowserDownload,
  validateArchivalFile,
  validateArchivalMetadata,
} from '../utils/archival';
import { getThesisErrorMessage } from '../utils/thesisErrors';

const STALE_TIME_MS = 60_000;

export function useStudentArchival() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [pdfFile, setPdfFile] = useState<File | null>(null);
  const [docxFile, setDocxFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [draftMetadata, setDraftMetadata] = useState<ArchivalMetadataInput | null>(null);
  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [downloadingKey, setDownloadingKey] = useState<string | null>(null);
  const [previewingKey, setPreviewingKey] = useState<string | null>(null);
  const localPdfUrlRef = useRef<string | null>(null);
  const [pdfPreviewState, setPdfPreviewState] = useState<{
    open: boolean;
    url: string;
    fileName?: string;
    fileSize?: number;
  }>({ open: false, url: '' });

  useEffect(() => {
    return () => {
      if (localPdfUrlRef.current) {
        URL.revokeObjectURL(localPdfUrlRef.current);
        localPdfUrlRef.current = null;
      }
    };
  }, []);

  const statusQuery = useQuery({
    queryKey: ['student-registration-status', user?.id],
    queryFn: ({ signal }) => syllabusApi.getMyStatus(signal),
    enabled: Boolean(user?.id),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const groupId = statusQuery.data?.groupId ?? null;
  const isConfirmedMember =
    Boolean(statusQuery.data?.hasRegistered) &&
    Boolean(groupId) &&
    statusQuery.data?.confirmationStatus === 'Confirmed';

  const currentBatch = useMemo(
    () =>
      batchesQuery.data?.find(
        (batch) => batch.id === statusQuery.data?.batchId || batch.status === 'Open',
      ),
    [batchesQuery.data, statusQuery.data?.batchId],
  );

  const groupDetailQuery = useQuery({
    queryKey: ['student-group-detail', user?.id, groupId, currentBatch?.id],
    queryFn: ({ signal }) =>
      syllabusApi.getGroupDetail(
        groupId!,
        {
          topicId: statusQuery.data?.topicId ?? undefined,
          topicTitle: statusQuery.data?.topicTitleVi ?? undefined,
          currentUser: user,
          batchName: currentBatch?.batchName,
        },
        signal,
      ),
    enabled: Boolean(user?.id && groupId && isConfirmedMember),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const eligibilityQuery = useQuery({
    queryKey: ['student-archival-eligibility', user?.id, groupId],
    queryFn: ({ signal }) => archivalApi.getEligibility(groupId!, signal),
    enabled: Boolean(user?.id && groupId && isConfirmedMember),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const submissionQuery = useQuery({
    queryKey: ['student-final-submission', user?.id, groupId],
    queryFn: ({ signal }) => archivalApi.getMySubmission(groupId!, signal),
    enabled: Boolean(user?.id && groupId && isConfirmedMember),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
    retry: false,
  });

  const feedbackQuery = useQuery({
    queryKey: ['student-archival-feedback', user?.id, groupId],
    queryFn: ({ signal }) => finalReportApi.getReviewerFeedback(groupId!, signal),
    enabled: Boolean(user?.id && groupId && isConfirmedMember),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
    retry: false,
  });

  const submissionId = submissionQuery.data?.submissionId ?? null;
  const rawSubmissionStatus =
    submissionQuery.data?.status ??
    eligibilityQuery.data?.currentSubmissionStatus ??
    'NotSubmitted';

  const packagesQuery = useQuery({
    queryKey: ['student-archival-packages', submissionId],
    queryFn: ({ signal }) => archivalApi.getPackages(submissionId!, signal),
    enabled: Boolean(
      submissionId && (isHistoryOpen || rawSubmissionStatus === 'NeedRevision'),
    ),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const serverMetadata: ArchivalMetadataInput = useMemo(
    () => ({
      abstractVi: submissionQuery.data?.abstractVi ?? '',
      abstractEn: submissionQuery.data?.abstractEn ?? '',
      keywordsVi: submissionQuery.data?.keywordsVi ?? '',
      keywordsEn: submissionQuery.data?.keywordsEn ?? '',
    }),
    [submissionQuery.data],
  );

  const metadata = draftMetadata ?? serverMetadata;
  const metadataValidation = useMemo(() => validateArchivalMetadata(metadata), [metadata]);

  const submissionStatus = rawSubmissionStatus;

  const isLockedStatus =
    submissionStatus === 'AdvisorConfirmed' || submissionStatus === 'ArchiveConfirmed';
  const canEdit = Boolean(eligibilityQuery.data?.canSubmit) && !isLockedStatus;
  const canSubmit =
    canEdit && Boolean(pdfFile) && Boolean(docxFile) && metadataValidation.valid;

  const hasUnsavedChanges = useMemo(() => {
    if (!canEdit) return false;
    if (pdfFile || docxFile) return true;
    if (!draftMetadata) return false;
    return (
      draftMetadata.abstractVi !== serverMetadata.abstractVi ||
      draftMetadata.abstractEn !== serverMetadata.abstractEn ||
      draftMetadata.keywordsVi !== serverMetadata.keywordsVi ||
      draftMetadata.keywordsEn !== serverMetadata.keywordsEn
    );
  }, [canEdit, docxFile, draftMetadata, pdfFile, serverMetadata]);

  const latestRevisionReview = useMemo(() => {
    const packages = packagesQuery.data ?? [];
    if (packages.length === 0) return null;
    const latestPkg = [...packages].sort((a, b) => b.versionNo - a.versionNo)[0];
    return (
      [...(latestPkg.reviews ?? [])]
        .sort((a, b) => Date.parse(b.decidedAtUtc) - Date.parse(a.decidedAtUtc))
        .find((review) => review.action === 'RequestRevision') ?? null
    );
  }, [packagesQuery.data]);

  const updateMetadataField = (field: keyof ArchivalMetadataInput, value: string) => {
    setDraftMetadata((prev) => ({
      ...(prev ?? serverMetadata),
      [field]: value,
    }));
  };

  const selectPdfFile = (file: File) => {
    const err = validateArchivalFile(file, '.pdf');
    if (err) {
      setFileError(err);
      notify.error(err);
      return;
    }
    setFileError(null);
    setPdfFile(file);
  };

  const selectDocxFile = (file: File) => {
    const err = validateArchivalFile(file, '.docx');
    if (err) {
      setFileError(err);
      notify.error(err);
      return;
    }
    setFileError(null);
    setDocxFile(file);
  };

  const submitMutation = useMutation({
    mutationFn: async () => {
      if (!groupId || !pdfFile || !docxFile) {
        throw new Error('Cần chọn đầy đủ cả tệp PDF và DOCX trước khi nộp.');
      }
      return archivalApi.submitFinalSubmission({
        groupId,
        expectedSubmissionVersion:
          submissionQuery.data?.submissionVersion ??
          eligibilityQuery.data?.submissionVersion ??
          null,
        abstractVi: metadata.abstractVi,
        abstractEn: metadata.abstractEn,
        keywordsVi: metadata.keywordsVi,
        keywordsEn: metadata.keywordsEn,
        pdfFile,
        docxFile,
      });
    },
    onSuccess: async (saved) => {
      setPdfFile(null);
      setDocxFile(null);
      setFileError(null);
      setDraftMetadata(null);
      notify.success(`Đã nộp hồ sơ lưu chiểu phiên bản v${saved.currentVersionNo}.`);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['student-final-submission', user?.id, groupId] }),
        queryClient.invalidateQueries({
          queryKey: ['student-archival-eligibility', user?.id, groupId],
        }),
        queryClient.invalidateQueries({
          queryKey: ['student-archival-packages', saved.submissionId],
        }),
      ]);
    },
    onError: (error) => {
      notify.error(getThesisErrorMessage(error, 'Nộp hồ sơ lưu chiểu chưa thành công.'));
    },
  });

  const handleDownloadFile = async (
    packageVersion: number,
    fileRole: string,
    fileName: string,
  ) => {
    if (!submissionId) return;
    const key = `${packageVersion}-${fileRole}`;
    setDownloadingKey(key);
    try {
      const download = await archivalApi.getDownloadUrl(submissionId, packageVersion, fileRole);
      triggerBrowserDownload(download.downloadUrl, download.fileName || fileName);
    } catch (error) {
      notify.error(getThesisErrorMessage(error, 'Không tạo được liên kết tải tệp.'));
    } finally {
      setDownloadingKey(null);
    }
  };

  const handlePreviewLocalPdf = () => {
    if (!pdfFile) return;
    if (localPdfUrlRef.current) {
      URL.revokeObjectURL(localPdfUrlRef.current);
    }
    const objectUrl = URL.createObjectURL(pdfFile);
    localPdfUrlRef.current = objectUrl;
    setPdfPreviewState({
      open: true,
      url: objectUrl,
      fileName: pdfFile.name,
      fileSize: pdfFile.size,
    });
  };

  const handlePreviewPackagePdf = async (
    packageVersion: number,
    fileName: string,
    fileSize?: number,
  ) => {
    if (!submissionId) return;
    const key = `${packageVersion}-ThesisPdf`;
    setPreviewingKey(key);
    try {
      const download = await archivalApi.getDownloadUrl(submissionId, packageVersion, 'ThesisPdf');
      setPdfPreviewState({
        open: true,
        url: download.downloadUrl,
        fileName: download.fileName || fileName,
        fileSize: download.fileSize ?? fileSize,
      });
    } catch (error) {
      notify.error(getThesisErrorMessage(error, 'Không tạo được liên kết xem trước tệp PDF.'));
    } finally {
      setPreviewingKey(null);
    }
  };

  const resetDraft = () => {
    setPdfFile(null);
    setDocxFile(null);
    setDraftMetadata(null);
    setFileError(null);
  };

  const refresh = async () => {
    if (!isConfirmedMember) {
      await statusQuery.refetch();
      return;
    }
    await Promise.all([
      groupDetailQuery.refetch(),
      eligibilityQuery.refetch(),
      submissionQuery.refetch(),
      feedbackQuery.refetch(),
    ]);
  };

  return {
    statusQuery,
    groupDetailQuery,
    eligibilityQuery,
    submissionQuery,
    feedbackQuery,
    packagesQuery,
    isConfirmedMember,
    submissionStatus,
    isLockedStatus,
    canEdit,
    canSubmit,
    hasUnsavedChanges,
    latestRevisionReview,
    pdfFile,
    docxFile,
    fileError,
    metadata,
    metadataValidation,
    isHistoryOpen,
    setIsHistoryOpen,
    downloadingKey,
    previewingKey,
    pdfPreviewState,
    closePdfPreview: () => setPdfPreviewState((prev) => ({ ...prev, open: false })),
    selectPdfFile,
    selectDocxFile,
    removePdfFile: () => setPdfFile(null),
    removeDocxFile: () => setDocxFile(null),
    updateMetadataField,
    resetDraft,
    submitArchival: () => submitMutation.mutateAsync(),
    isSubmitting: submitMutation.isPending,
    handleDownloadFile,
    handlePreviewLocalPdf,
    handlePreviewPackagePdf,
    refresh,
  };
}
