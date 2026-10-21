export interface AcademicBatch {
  id: string;
  batchName: string;
  academicYear: string;
  semester: number;
  startDate: string;
  endDate: string;
  timeZoneId: string;
  status: 'Draft' | 'Open' | 'Closed' | 'Archived';
}

export interface AcademicBatchTimeline {
  batchId: string;
  batchName: string;
  currentLocalDate: string;
  timeZoneId: string;
  stages: {
    id: string;
    stageCode: string;
    stageName: string;
    startDate: string;
    endDate: string;
    deadlineAtUtc: string;
  }[];
}
