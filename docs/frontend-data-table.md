# DataTable

`frontend/src/components/ui/data-table.tsx` ghép lại bộ `Table`, `SelectableTableRow`, `SelectionIndicator`, `TableSkeleton`, `TableEmpty` và `TablePagination`. Hai bảng Đề tài của tôi và Nhóm & tiến độ là các consumer hiện tại.

## Ví dụ

```tsx
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';

type Student = { id: string; name: string; code: string };
type SortField = 'name';
const columns: DataTableColumn<Student, SortField>[] = [
  {
    id: 'name', header: 'Sinh viên', sortKey: 'name',
    skeleton: { variant: 'person' },
    cell: (student) => <strong>{student.name}</strong>,
  },
  {
    id: 'code', header: 'MSSV', widthClassName: 'w-36',
    cell: (student) => student.code,
  },
];

<DataTable<Student, SortField>
  ariaLabel="Danh sách sinh viên"
  data={page.items}
  columns={columns}
  getRowId={(student) => student.id}
  selectedRowId={selectedId}
  onRowSelect={setSelectedId}
  onRowOpen={openDetail}
  sort={{ field: sortBy, direction: sortOrder }}
  onSortChange={changeSort}
  loading={query.isPending}
  busy={query.isFetching}
  error={query.isError ? {
    message: getErrorMessage(query.error),
    onRetry: () => void query.refetch(),
  } : null}
  empty={{ title: 'Chưa có sinh viên' }}
  pagination={{
    pageNumber: page.pageNumber, pageSize: page.pageSize,
    totalCount: page.totalCount, itemLabel: 'sinh viên',
    onPageChange: changePage, onPageSizeChange: changePageSize,
  }}
  renderMobileItem={(student) => <>
    <p>{student.name} · {student.code}</p>
    <button disabled={query.isFetching} onClick={() => openDetail(student)}>Xem</button>
  </>}
/>
```

## Quy ước

- `data` là trang dữ liệu đã được xử lý. Component không gọi API và không tự lọc/sort/slice. `changeSort` cập nhật query key/tham số API, về trang 1 và bỏ chọn dòng. Backend sắp xếp toàn bộ tập kết quả trước khi phân trang, có khóa phụ ổn định.
- `onRowSelect(id, row)` và `selectedRowId` điều khiển chọn dòng. Bấm một lần hoặc Enter/Space khi focus dòng chỉ chọn; nhấp đúp nội dung gọi `onRowOpen(row)`. Nút/link/checkbox/vùng nhập liệu giữ hành vi riêng. Luôn cung cấp nút Xem cho mở chi tiết bằng bàn phím/mobile.
- Khi có `onRowSelect`, bảng dùng thanh chọn trượt 360ms và hỗ trợ `prefers-reduced-motion`. Không thêm thanh chọn riêng tại page.
- `loading` hiện skeleton theo cột. `busy` giữ nội dung, làm mờ và khóa chọn/mở dòng, sort, phân trang. Page phải truyền `disabled` tương ứng cho các nút tùy chỉnh trong `cell`/`renderMobileItem`.
- `error` có ưu tiên cao nhất, che dữ liệu cache/phân trang; retry tùy chọn. Trang rỗng vẫn có thể giữ phân trang để quay lại trang trước; dùng `empty.action` để bổ sung hành động khôi phục.
- Có `renderMobileItem`: dưới breakpoint `lg` dùng danh sách card. Không có: bảng cuộn ngang khi cần. Nội dung và các nút mobile do page cung cấp.
- Cột hỗ trợ `align`, `widthClassName`, `headerClassName`, `cellClassName`, `title`, `skeleton`. `cell` hiển thị tùy ý avatar, badge, menu; header nhận `ReactNode`, có thể chứa checkbox. `isRowSelected` hỗ trợ consumer cũ có chọn nhiều; bảng không tự quản lý tập ID đã chọn.
- `TableRow` giữ API/ref cũ và ủy quyền cho `SelectableTableRow`. Bố cục đặc biệt vẫn có thể dùng trực tiếp các primitive; ưu tiên `DataTable` cho danh sách thông thường.

Các page khác được chuyển dần khi có nhu cầu; không thay đổi quyền hoặc nghiệp vụ chỉ để dùng component.
