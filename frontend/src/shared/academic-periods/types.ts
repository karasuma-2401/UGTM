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
