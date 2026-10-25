import { useState, type FormEvent } from 'react';
import type { AccountStatus, UsersQuery } from '../types/user.types';

export type UserFilters = Pick<UsersQuery, 'keyword' | 'role' | 'status'>;

export const INITIAL_FILTERS: UserFilters = {
  keyword: '',
  role: '',
  status: '',
};

export function useUserFilters() {
  const [draftFilters, setDraftFilters] = useState<UserFilters>(INITIAL_FILTERS);
  const [filters, setFilters] = useState<UserFilters>(INITIAL_FILTERS);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const quickFilter = (status: AccountStatus | '') => {
    const next = { ...INITIAL_FILTERS, status };
    setFilters(next);
    setDraftFilters(next);
    setPageNumber(1);
  };

  const handleApplyFilters = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFilters({
      ...draftFilters,
      keyword: draftFilters.keyword.trim(),
    });
    setPageNumber(1);
  };

  const clearFilters = () => {
    setDraftFilters({ ...INITIAL_FILTERS });
    setFilters({ ...INITIAL_FILTERS });
    setPageNumber(1);
  };

  const removeFilter = (key: keyof UserFilters) => {
    const next = { ...filters, [key]: '' };
    setDraftFilters(next);
    setFilters(next);
    setPageNumber(1);
  };

  const hasFilters = Boolean(
    filters.keyword ||
    filters.role ||
    filters.status ||
    draftFilters.keyword ||
    draftFilters.role ||
    draftFilters.status,
  );

  return {
    draftFilters,
    setDraftFilters,
    filters,
    setFilters,
    pageNumber,
    setPageNumber,
    pageSize,
    setPageSize,
    quickFilter,
    handleApplyFilters,
    clearFilters,
    removeFilter,
    hasFilters,
  };
}