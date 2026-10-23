import { cn } from '@/lib/utils';
import { Skeleton } from './skeleton';

interface SkeletonColumn {
    label: string;
    width?: string;
}

interface TableSkeletonProps {
    columns: SkeletonColumn[];
    rows?: number;
    className?: string;
}

export function TableSkeleton({
    columns,
    rows = 6,
    className,
}: TableSkeletonProps) {
    return (
        <div role="status">
        <span className="sr-only">Đang tải danh sách...</span>

        <table
            aria-hidden="true"
            className={cn(
            'w-full table-fixed text-left text-sm',
            className,
            )}
        >
            <colgroup>
            {columns.map((column, index) => (
                <col key={index} className={column.width} />
            ))}
            </colgroup>

            <thead className="sticky top-0 z-10 h-14 border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-400">
            <tr>
                {columns.map((column, index) => (
                <th key={index} className="px-4 py-4 font-medium">
                    {column.label}
                </th>
                ))}
            </tr>
            </thead>

            <tbody className="divide-y divide-slate-100">
            {Array.from({ length: rows }, (_, rowIndex) => (
                <tr key={rowIndex} className="h-[88px]">
                {columns.map((_, columnIndex) => (
                    <td key={columnIndex} className="px-4 py-4">
                    <Skeleton
                        className={cn(
                        'h-4 max-w-full',
                        columnIndex === 0 ? 'w-5' : 'w-24',
                        )}
                    />

                    {columnIndex === 1 && (
                        <Skeleton className="mt-2 h-3 w-36 max-w-full" />
                    )}
                    </td>
                ))}
                </tr>
            ))}
            </tbody>
        </table>
        </div>
    );
}