import React from 'react';
import { useAuth } from '@/shared/auth';
import { AcademicResultsPage } from './AcademicResultsPage';
import { StudentResultsPage } from './StudentResultsPage';

export const ResultsEntryPage: React.FC = () => {
  const { hasRole } = useAuth();

  if (hasRole(['Faculty', 'AcademicAffairs', 'Admin'])) {
    return <AcademicResultsPage />;
  }

  return <StudentResultsPage />;
};
