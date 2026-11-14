import { useState } from 'react';
import { AlertCircle, ArrowLeft, CheckCircle2, Clock3, FilePenLine, FileText, Info, Layers, ListFilter, Search, SquarePen, UsersRound, X } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { NavigationTabs } from '@/components/ui/navigation-tabs';
import { Button } from '@/components/ui/button';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select } from '@/components/ui/select';

type Role = 'advisor' | 'reviewer';
type Scores = Record<string, Record<string, string>>;
type Group = (typeof groups)[Role][number];
type GroupStatus = 'all' | 'empty' | 'progress' | 'complete';
type GroupSort = 'default' | 'title-asc' | 'title-desc' | 'student-asc' | 'student-desc' | 'progress-asc' | 'progress-desc';
type SortColumn = 'topic' | 'students' | 'status';

const groups = {
  advisor: [
    { id: 'hd1', title: 'Xây dựng hệ thống quản lý khóa luận tốt nghiệp', students: [
      { id: 'sv1', name: 'Nguyễn Minh Anh', code: '21520001', initials: 'MA' },
      { id: 'sv2', name: 'Trần Hoàng Nam', code: '21520002', initials: 'HN' },
    ] },
    { id: 'hd2', title: 'Ứng dụng hỗ trợ quản lý lịch học', students: [
      { id: 'sv3', name: 'Lê Ngọc Mai', code: '21520003', initials: 'NM' },
      { id: 'sv4', name: 'Phạm Quốc Bảo', code: '21520004', initials: 'QB' },
    ] },
    { id: 'hd3', title: 'Nền tảng quản lý phòng thí nghiệm', students: [
      { id: 'sv9', name: 'Nguyễn Thùy Dương', code: '21520009', initials: 'TD' },
      { id: 'sv10', name: 'Trần Quốc Khánh', code: '21520010', initials: 'QK' },
    ] },
  ],
  reviewer: [
    { id: 'pb1', title: 'Nền tảng hỗ trợ tìm kiếm tài liệu học tập', students: [
      { id: 'sv5', name: 'Võ Thảo Nguyên', code: '21520005', initials: 'TN' },
      { id: 'sv6', name: 'Đặng Gia Huy', code: '21520006', initials: 'GH' },
    ] },
    { id: 'pb2', title: 'Hệ thống quản lý hoạt động tình nguyện', students: [
      { id: 'sv7', name: 'Bùi Khánh Linh', code: '21520007', initials: 'KL' },
      { id: 'sv8', name: 'Phan Đức Anh', code: '21520008', initials: 'ĐA' },
    ] },
    { id: 'pb3', title: 'Ứng dụng quản lý chi tiêu cá nhân', students: [
      { id: 'sv11', name: 'Phạm Gia Hân', code: '21520011', initials: 'GH' },
      { id: 'sv12', name: 'Lê Minh Quân', code: '21520012', initials: 'MQ' },
    ] },
  ],
};

const sections = [
  { name: 'Ý nghĩa khoa học, giá trị thực tiễn', max: 3, criteria: [
    { id: '1.1', name: 'Tính mới và độ phức tạp', max: 1.5, elo: 'LO2' },
    { id: '1.2', name: 'Tính thực tiễn và đóng góp', max: 1.5, elo: 'LO3' },
  ] },
  { name: 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', max: 3, criteria: [
    { id: '2.1', name: 'Khả năng tìm hiểu, phân tích yêu cầu và thiết kế hệ thống', max: 1, elo: 'LO3' },
    { id: '2.2', name: 'Khả năng hiện thực hóa, kiểm thử và vận hành hệ thống, đánh giá giải pháp', max: 1, elo: 'LO4' },
    { id: '2.3', name: 'Học hỏi và cập nhật kiến thức, công nghệ mới, khả năng học tập suốt đời', max: 1, elo: 'LO3' },
  ] },
  { name: 'Trình bày kết quả', max: 3, criteria: [
    { id: '3.1', name: 'Nội dung trình bày cuốn báo và sản phẩm (viết)', max: 1, elo: 'LO6, LO8' },
    { id: '3.2', name: 'Kỹ năng trình bày (nói)', max: 1, elo: 'LO6' },
    { id: '3.3', name: 'Trả lời các câu hỏi', max: 1, elo: 'LO8' },
  ] },
  { name: 'Kỹ năng mềm', max: 1, criteria: [
    { id: '4.1', name: 'Lập kế hoạch và quản lý thời gian, công việc của cá nhân hoặc đội nhóm', max: 0.5, elo: 'LO5, LO7' },
    { id: '4.2', name: 'Kỹ năng tìm hiểu, đọc hiểu tài liệu ngoại ngữ và khả năng tư duy, giải quyết vấn đề', max: 0.5, elo: 'LO4, LO6' },
  ] },
];
const criteria = sections.flatMap((section) => section.criteria);
const initialScores: Scores = {
  sv1: { '1.1': '1.25', '1.2': '1.25', '2.1': '0.75', '2.2': '0.75', '2.3': '0.75' },
  sv2: { '1.1': '1.25', '1.2': '1', '2.1': '0.75' },
};
const completedScores = Object.fromEntries(criteria.map((item) => [item.id, String(item.max)]));
const seededScores: Record<string, Scores> = {
  hd1: initialScores,
  hd3: { sv9: completedScores, sv10: completedScores },
  pb1: { sv5: { '1.1': '1.25', '1.2': '1', '2.1': '0.75' } },
  pb3: { sv11: completedScores, sv12: completedScores },
};

function validScore(value: string | undefined, max: number) {
  if (!value?.trim()) return false;
  const number = Number(value.replace(',', '.'));
  return Number.isFinite(number) && number >= 0 && number <= max;
}

export function LecturerGradingPage() {
  const [role, setRole] = useState<Role>('advisor');
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<GroupStatus>('all');
  const [sortBy, setSortBy] = useState<GroupSort>('default');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [activeGroupId, setActiveGroupId] = useState<string | null>(null);
  const [pendingGroupId, setPendingGroupId] = useState<string | null>(null);
  const [scoresByGroup, setScoresByGroup] = useState<Record<string, Scores>>(seededScores);
  const [comments, setComments] = useState<Record<string, string>>({});
  const group = groups[role].find((item) => item.id === activeGroupId) ?? groups[role][0];
  const pendingGroup = groups[role].find((item) => item.id === pendingGroupId);
  const students = group.students;
  const scores = scoresByGroup[group.id] ?? {};
  const searchText = search.trim().toLocaleLowerCase('vi');
  const groupProgress = (item: Group) => item.students.reduce(
    (total, student) => total + criteria.filter((criterion) => validScore(scoresByGroup[item.id]?.[student.id]?.[criterion.id], criterion.max)).length, 0,
  );
  const statusOf = (item: Group): Exclude<GroupStatus, 'all'> => {
    const filled = groupProgress(item);
    if (filled === 0) return 'empty';
    if (filled === criteria.length * item.students.length) return 'complete';
    return 'progress';
  };
  const visibleGroups = groups[role].filter((item) =>
    (statusFilter === 'all' || statusOf(item) === statusFilter) &&
    [item.title, ...item.students.flatMap((student) => [student.name, student.code])]
      .some((value) => value.toLocaleLowerCase('vi').includes(searchText)),
  ).sort((a, b) => {
    switch (sortBy) {
      case 'title-asc': return a.title.localeCompare(b.title, 'vi');
      case 'title-desc': return b.title.localeCompare(a.title, 'vi');
      case 'student-asc': return a.students[0].name.localeCompare(b.students[0].name, 'vi');
      case 'student-desc': return b.students[0].name.localeCompare(a.students[0].name, 'vi');
      case 'progress-asc': return groupProgress(a) - groupProgress(b);
      case 'progress-desc': return groupProgress(b) - groupProgress(a);
      default: return 0;
    }
  });
  const groupCounts = groups[role].reduce((counts, item) => {
    const filled = groupProgress(item);
    if (filled === 0) counts.empty += 1;
    else if (filled === criteria.length * item.students.length) counts.complete += 1;
    else counts.inProgress += 1;
    return counts;
  }, { empty: 0, inProgress: 0, complete: 0 });
  const currentPage = Math.min(pageNumber, Math.max(1, Math.ceil(visibleGroups.length / pageSize)));
  const pageGroups = visibleGroups.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const chooseStatus = (status: GroupStatus) => { setStatusFilter(status); setPageNumber(1); };
  const activeSort: { field: SortColumn; direction: 'asc' | 'desc' } | undefined = sortBy === 'default' ? undefined : {
    field: sortBy.startsWith('title') ? 'topic' : sortBy.startsWith('student') ? 'students' : 'status',
    direction: sortBy.endsWith('asc') ? 'asc' : 'desc',
  };
  function sortColumn(field: SortColumn) {
    const prefix = field === 'topic' ? 'title' : field === 'students' ? 'student' : 'progress';
    setSortBy(`${prefix}-${activeSort?.field === field && activeSort.direction === 'asc' ? 'desc' : 'asc'}` as GroupSort);
    setPageNumber(1);
  }
  const groupColumns: DataTableColumn<Group, SortColumn>[] = [
    { id: 'number', header: 'STT', widthClassName: 'w-[6%]', headerClassName: 'whitespace-nowrap', cell: (_item, index) => <span className="tabular-nums text-slate-500">{(currentPage - 1) * pageSize + index + 1}</span> },
    { id: 'topic', header: 'Đề tài', sortKey: 'topic', widthClassName: 'w-[34%]', cell: (item) => <span className="block min-w-0"><span className="block font-semibold leading-relaxed text-slate-900">{item.title}</span><span className="mt-1 block text-xs text-slate-500">Đợt KLTN mẫu</span></span> },
    { id: 'students', header: 'Sinh viên', sortKey: 'students', widthClassName: 'w-[30%]', cell: (item) => <span className="space-y-1 text-sm text-slate-700">{item.students.map((student) => <span key={student.id} className="block">{student.name} <span className="text-slate-500">· {student.code}</span></span>)}</span> },
    { id: 'status', header: 'Trạng thái', sortKey: 'status', widthClassName: 'w-[16%]', headerClassName: 'whitespace-nowrap', cell: (item) => { const status = statusOf(item); const filled = groupProgress(item); return <Badge dot rounded="full" variant={status === 'empty' ? 'warning' : status === 'progress' ? 'info' : 'success'} className="max-w-full whitespace-nowrap">{status === 'empty' ? 'Chưa nhập điểm' : status === 'progress' ? `Đang chấm · ${filled}/${criteria.length * item.students.length}` : 'Đã nhập đủ'}</Badge>; } },
    { id: 'action', header: 'Thao tác', widthClassName: 'w-[14%]', headerClassName: 'whitespace-nowrap', align: 'right', cell: (item) => <Button type="button" variant="outline" size="sm" onClick={() => setPendingGroupId(item.id)} className="gap-1.5 rounded-lg border-blue-200 text-blue-700 hover:bg-blue-50"><SquarePen className="size-4" aria-hidden="true" />Chấm điểm</Button> },
  ];
  const progress = students.map((student) => {
    const completed = criteria.filter((item) => validScore(scores[student.id]?.[item.id], item.max));
    return { ...student, count: completed.length, total: completed.reduce((sum, item) => sum + Number(scores[student.id][item.id].replace(',', '.')), 0) };
  });

  function updateScore(studentId: string, criterionId: string, value: string) {
    setScoresByGroup((previous) => ({ ...previous, [group.id]: { ...previous[group.id], [studentId]: { ...previous[group.id]?.[studentId], [criterionId]: value } } }));
  }

  return (
    <div className="space-y-5 pb-10">
      <PageHeader title={activeGroupId ? 'Phiếu chấm điểm' : 'Chấm điểm khóa luận'} description={activeGroupId ? 'Điểm được nhập riêng cho từng sinh viên trong nhóm.' : 'Chọn nhóm được phân công để bắt đầu chấm điểm.'} navigation={!activeGroupId ? <NavigationTabs tabs={[{ id: 'advisor', label: 'Hướng dẫn', count: groups.advisor.length }, { id: 'reviewer', label: 'Phản biện', count: groups.reviewer.length }]} activeId={role} onSelect={(id) => { if (id === 'advisor' || id === 'reviewer') setRole(id); }} ariaLabel="Chọn loại phân công chấm điểm" continuityKey="lecturer-grading-role-tabs" /> : undefined}>
        {activeGroupId && <Button type="button" variant="outline" onClick={() => setActiveGroupId(null)} className="gap-2 rounded-lg"><ArrowLeft className="size-4" aria-hidden="true" />Danh sách nhóm</Button>}
      </PageHeader>

      <div role="status" className="flex items-start gap-3 rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-900">
        <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
        <p><strong>Bản xem trước · Dữ liệu mẫu.</strong> Điểm và nhận xét chỉ tồn tại trong phiên này, chưa được lưu hoặc gửi đến hệ thống.</p>
      </div>

      {!activeGroupId && <section className="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm" aria-label="Chọn nhóm chấm điểm">
        <ListCardHeader title="Nhóm được phân công" icon={<SquarePen />} description={`${role === 'advisor' ? 'Hướng dẫn' : 'Phản biện'} · Đợt KLTN mẫu`} className="lg:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_auto]">
          <StatisticsBar items={[
            { key: 'total', label: 'Tổng nhóm', value: groups[role].length, icon: <Layers />, tone: 'muted', active: statusFilter === 'all', onClick: () => chooseStatus('all') },
            { key: 'empty', label: 'Chưa nhập điểm', value: groupCounts.empty, icon: <Clock3 />, tone: 'warning', active: statusFilter === 'empty', onClick: () => chooseStatus('empty') },
            { key: 'progress', label: 'Đang chấm', value: groupCounts.inProgress, icon: <FilePenLine />, tone: 'primary', active: statusFilter === 'progress', onClick: () => chooseStatus('progress') },
            { key: 'complete', label: 'Đã nhập đủ', value: groupCounts.complete, icon: <CheckCircle2 />, tone: 'success', active: statusFilter === 'complete', onClick: () => chooseStatus('complete') },
          ]} />
        </ListCardHeader>
        <div className="flex flex-wrap items-center gap-3 border-b border-slate-100 bg-slate-50/45 px-5 py-3.5">
          <div className="relative min-w-[220px] flex-1"><Input id="grading-group-search" aria-label="Tìm nhóm theo đề tài, tên hoặc mã sinh viên" leftIcon={<Search />} value={search} onChange={(event) => { setSearch(event.target.value); setPageNumber(1); }} placeholder="Tìm theo đề tài, tên hoặc MSSV..." className="pr-9" />{search && <button type="button" aria-label="Xóa từ khóa tìm kiếm" onClick={() => { setSearch(''); setPageNumber(1); }} className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded p-1 text-slate-500 hover:bg-slate-100"><X className="size-3.5" /></button>}</div>
          <div className="w-full sm:w-48"><Select<GroupStatus> aria-label="Lọc theo trạng thái chấm" leftIcon={<ListFilter className="size-4" />} value={statusFilter} onChange={(value: GroupStatus) => chooseStatus(value)} options={[{ value: 'all', label: 'Tất cả trạng thái' }, { value: 'empty', label: 'Chưa nhập điểm' }, { value: 'progress', label: 'Đang chấm' }, { value: 'complete', label: 'Đã nhập đủ' }]} /></div>
          <div className="w-full sm:w-52"><Select<GroupSort> aria-label="Sắp xếp nhóm" value={sortBy} onChange={(value: GroupSort) => { setSortBy(value); setPageNumber(1); }} options={[{ value: 'default', label: 'Thứ tự mặc định' }, { value: 'title-asc', label: 'Đề tài A–Z' }, { value: 'title-desc', label: 'Đề tài Z–A' }, { value: 'student-asc', label: 'Sinh viên A–Z' }, { value: 'student-desc', label: 'Sinh viên Z–A' }, { value: 'progress-asc', label: 'Ít điểm đã nhập' }, { value: 'progress-desc', label: 'Nhiều điểm đã nhập' }]} /></div>
        </div>
        <DataTable data={pageGroups} columns={groupColumns} getRowId={(item) => item.id} ariaLabel={`Danh sách nhóm ${role === 'advisor' ? 'hướng dẫn' : 'phản biện'}`} tableClassName="min-w-[1120px]" sort={activeSort} onSortChange={sortColumn} empty={{ title: 'Không có nhóm phù hợp', description: 'Hãy thử từ khóa hoặc trạng thái khác.' }} pagination={{ pageNumber: currentPage, pageSize, totalCount: visibleGroups.length, onPageChange: setPageNumber, onPageSizeChange: (size) => { setPageSize(size); setPageNumber(1); }, itemLabel: 'nhóm' }} renderMobileItem={(item) => <div className="space-y-3"><div><p className="font-semibold text-slate-900">{item.title}</p><p className="mt-1 text-xs text-slate-600">{item.students.map((student) => `${student.name} · ${student.code}`).join(' / ')}</p></div><div className="flex flex-wrap items-center justify-between gap-2">{groupColumns[3].cell(item, 0)}{groupColumns[4].cell(item, 0)}</div></div>} />
      </section>}

      <ConfirmDialog open={!!pendingGroup} onClose={() => setPendingGroupId(null)} onConfirm={() => { setActiveGroupId(pendingGroupId); setPendingGroupId(null); window.scrollTo({ top: 0 }); }} title="Xác nhận nhóm chấm điểm" confirmText="Vào phiếu chấm" cancelText="Quay lại" description={pendingGroup && <div className="space-y-3"><p>Kiểm tra thông tin trước khi mở phiếu chấm {role === 'advisor' ? 'hướng dẫn' : 'phản biện'}:</p><div className="rounded-xl border border-slate-200 bg-slate-50 p-4"><p className="font-semibold text-slate-900">{pendingGroup.title}</p><p className="mt-2 text-xs font-medium text-slate-500">THÀNH VIÊN</p><ul className="mt-1 space-y-1">{pendingGroup.students.map((student) => <li key={student.id}>{student.name} · {student.code}</li>)}</ul></div><p>Điểm được nhập riêng cho từng sinh viên.</p></div>} />

      {activeGroupId && <><section className="rounded-2xl border border-blue-200 bg-blue-50/60 px-5 py-5 sm:px-6"><div className="flex flex-wrap items-start justify-between gap-4"><div><div className="flex flex-wrap items-center gap-2"><span className="rounded-md bg-white px-2 py-1 text-xs font-semibold text-blue-700 ring-1 ring-blue-200">{role === 'advisor' ? 'Giảng viên hướng dẫn' : 'Giảng viên phản biện'}</span><span className="text-xs text-slate-600">Đợt KLTN mẫu · Báo cáo cuối kỳ</span></div><h2 className="mt-3 text-lg font-semibold text-slate-900">{group.title}</h2><div className="mt-3 flex flex-wrap gap-x-5 gap-y-1">{students.map((student) => <span key={student.id} className="text-sm text-slate-700">{student.name} <span className="text-slate-500">· {student.code}</span></span>)}</div></div><span className="rounded-full bg-white px-3 py-1.5 text-xs font-medium text-slate-600 ring-1 ring-slate-200">Phiếu mẫu</span></div></section>
      <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_290px]">
        <section className="min-w-0 overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm" aria-labelledby="rubric-title">
          <div className="flex flex-wrap items-end justify-between gap-3 border-b border-slate-200 px-5 py-5 sm:px-6">
            <div><div className="flex flex-wrap items-center gap-2"><h2 id="rubric-title" className="text-base font-semibold text-slate-900">Bảng điểm chi tiết</h2><span className="rounded-md bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700">{role === 'advisor' ? 'Hướng dẫn' : 'Phản biện'}</span></div><p className="mt-1 text-sm text-slate-600">{group.title}</p><p className="mt-1 text-xs text-slate-500">Nhập điểm riêng cho từng sinh viên theo thang điểm của mỗi tiêu chí.</p></div>
            <span className="text-xs font-medium text-slate-500">{criteria.length} tiêu chí · Thang điểm 10</span>
          </div>
          {sections.map((section, index) => (
            <div key={section.name}>
              <div className="flex items-center justify-between gap-3 border-b border-slate-100 bg-slate-50/70 px-5 py-3 sm:px-6"><h3 className="text-sm font-semibold text-slate-800">{index + 1}. {section.name}</h3><span className="shrink-0 text-xs font-medium text-slate-500">{section.max.toFixed(1)} điểm</span></div>
              {section.criteria.map((item) => <div key={item.id} className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 border-b border-slate-100 px-5 py-4 last:border-b-0 sm:px-6">
                <div><p className="text-sm leading-relaxed text-slate-800"><span className="mr-2 font-medium text-slate-500">{item.id}</span>{item.name}</p><p className="mt-1 text-xs text-slate-500">{item.elo} <span aria-hidden="true">·</span> Tối đa {item.max.toFixed(1)} điểm</p></div>
                <div className="flex flex-wrap gap-3">{students.map((student) => {
                  const value = scores[student.id]?.[item.id] ?? '';
                  const invalid = value !== '' && !validScore(value, item.max);
                  const id = `${role}-${student.id}-${item.id}`;
                  return <div key={student.id} className="w-36 max-w-full"><label htmlFor={id} className="mb-1.5 flex items-center gap-1.5 text-xs font-medium text-slate-600"><span className="flex size-5 shrink-0 items-center justify-center rounded-full bg-blue-50 text-[10px] font-semibold text-blue-700">{student.initials}</span><span className="truncate">{student.name}</span><span className="sr-only"> · {item.name}</span></label><div className="relative"><input id={id} type="text" inputMode="decimal" value={value} onChange={(event) => updateScore(student.id, item.id, event.target.value)} placeholder="Nhập điểm" aria-invalid={invalid} aria-describedby={invalid ? `${id}-error` : undefined} className={`h-10 w-full rounded-lg border bg-white pl-3 pr-11 text-sm font-medium tabular-nums text-slate-900 outline-none placeholder:text-slate-400 focus:ring-2 ${invalid ? 'border-red-500 focus:ring-red-100' : 'border-slate-300 focus:border-blue-600 focus:ring-blue-100'}`} /><span className="pointer-events-none absolute right-3 top-2.5 text-sm text-slate-500">/ {item.max}</span></div>{invalid && <p id={`${id}-error`} className="mt-1 text-xs text-red-700">Nhập từ 0 đến {item.max}.</p>}</div>;
                })}</div>
              </div>)}
            </div>
          ))}
          <div className="p-5 sm:p-6"><label htmlFor="grading-comment" className="text-sm font-semibold text-slate-900">Nhận xét chung</label><p className="mt-1 text-sm text-slate-500">Ghi lại nhận xét cho phiếu {role === 'advisor' ? 'hướng dẫn' : 'phản biện'}.</p><textarea id="grading-comment" value={comments[group.id] ?? ''} onChange={(event) => setComments({ ...comments, [group.id]: event.target.value })} maxLength={2000} rows={4} placeholder="Nhập nhận xét về kết quả thực hiện khóa luận..." className="mt-3 w-full rounded-lg border border-slate-300 p-3 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus:border-blue-600 focus:ring-2 focus:ring-blue-100" /><p className="mt-1 text-right text-xs text-slate-500">{(comments[group.id] ?? '').length}/2000</p></div>
        </section>

        <aside className="space-y-4 xl:sticky xl:top-5" aria-label="Tổng quan điểm">
          <section className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-sm"><div className="flex items-center gap-2"><UsersRound className="size-4 text-blue-600" aria-hidden="true" /><h2 className="font-semibold text-slate-900">Tiến độ chấm</h2></div><p className="mt-1 text-sm text-slate-500">Điểm tạm tính theo từng sinh viên.</p>
            <div className="mt-4 divide-y divide-slate-100">{progress.map((student) => <div key={student.id} className="py-4 first:pt-0 last:pb-0"><div className="flex items-center gap-3"><span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-blue-50 text-xs font-semibold text-blue-700">{student.initials}</span><div className="min-w-0 flex-1"><p className="truncate text-sm font-semibold text-slate-900">{student.name}</p><p className="text-xs text-slate-500">{student.code}</p></div><span className="text-lg font-semibold tabular-nums text-slate-900">{student.total.toFixed(2)}<span className="text-xs font-normal text-slate-500"> / 10</span></span></div><div className="mt-3 flex items-center justify-between text-xs text-slate-600"><span>{student.count}/{criteria.length} tiêu chí</span><span>{student.count === criteria.length ? 'Đã nhập đủ' : 'Chưa hoàn tất'}</span></div><div className="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-100"><div className="h-full rounded-full bg-blue-600 transition-[width]" style={{ width: `${student.count / criteria.length * 100}%` }} /></div></div>)}</div>
          </section>
          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600"><div className="flex items-center gap-2 font-semibold text-slate-800"><FileText className="size-4" aria-hidden="true" /> Lưu ý khi chấm</div><p className="mt-2 leading-relaxed">Điểm tổng được cộng từ các tiêu chí đã nhập. Các ô trống chưa được tính là điểm 0.</p></div>
          <div className="flex items-start gap-2 px-1 text-xs leading-relaxed text-slate-500">{progress.every((item) => item.count === criteria.length) ? <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-600" aria-hidden="true" /> : <AlertCircle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />}<span>Đây là bản xem trước. Chức năng lưu và chốt điểm sẽ được bổ sung sau khi backend hỗ trợ.</span></div>
        </aside>
      </div></>}
    </div>
  );
}
