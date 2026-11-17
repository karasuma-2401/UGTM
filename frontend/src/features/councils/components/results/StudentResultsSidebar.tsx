import React from 'react';
import { Link } from 'react-router-dom';
import { Archive, ExternalLink } from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { formatDateTimeVi } from '../../utils/scoreSummaryFormatting';

export interface StudentResultMemberItem {
  id: string;
  name: string;
  userCode?: string;
}

export interface StudentResultsSidebarProps {
  isPublished: boolean;
  topicCode: string;
  groupCode: string;
  topicTitle: string;
  topicTitleEn: string | null;
  batchName: string;
  publishedAt: string | null;
  archivedConfirmedAt: string | null;
  studentItems: readonly StudentResultMemberItem[];
  advisorName: string;
  advisorItems: readonly { id: string; name: string }[];
}

/**
 * Sticky 340px hairline-divided right rail displaying thesis dossier metadata,
 * confirmed student & advisor avatars, and quick navigation to archival submission.
 */
export const StudentResultsSidebar: React.FC<StudentResultsSidebarProps> = ({
  isPublished,
  topicCode,
  groupCode,
  topicTitle,
  topicTitleEn,
  batchName,
  publishedAt,
  archivedConfirmedAt,
  studentItems,
  advisorName,
  advisorItems,
}) => {
  return (
    <aside className="lg:sticky lg:top-6 space-y-4 print:hidden">
      <Card className="overflow-hidden border-line/80 shadow-[0_1px_2px_rgba(15,23,42,0.03)] divide-y divide-line-subtle">
        {/* Section 1: Topic & Batch Metadata */}
        <section className="p-5 space-y-3">
          <h2 className="text-sm font-semibold text-ink-primary">
            {isPublished ? 'Thông tin công bố' : 'Thông tin hồ sơ'}
          </h2>

          <div className="space-y-1">
            <p className="text-xs font-semibold text-primary tabular-nums">
              {topicCode} · {groupCode}
            </p>
            <p className="text-sm font-semibold text-ink-primary leading-snug">{topicTitle}</p>
            {topicTitleEn && (
              <p className="text-xs text-ink-muted leading-snug">{topicTitleEn}</p>
            )}
          </div>

          <div className="pt-2.5 border-t border-line/60 space-y-2 text-xs">
            <div className="flex items-center justify-between gap-3">
              <span className="text-ink-muted">Đợt khóa luận</span>
              <span className="font-medium text-ink-heading text-right">{batchName}</span>
            </div>
            {isPublished && publishedAt && (
              <div className="flex items-center justify-between gap-3">
                <span className="text-ink-muted">Công bố điểm</span>
                <span className="font-medium text-ink-heading tabular-nums text-right">
                  {formatDateTimeVi(publishedAt)}
                </span>
              </div>
            )}
            {archivedConfirmedAt && (
              <div className="flex items-center justify-between gap-3">
                <span className="text-ink-muted">Nghiệm thu lưu chiểu</span>
                <span className="font-medium text-ink-heading tabular-nums text-right">
                  {formatDateTimeVi(archivedConfirmedAt)}
                </span>
              </div>
            )}
          </div>
        </section>

        {/* Section 2: Group Members & Advisor */}
        <section className="p-5 space-y-3.5">
          <div className="space-y-2">
            <p className="text-xs font-medium text-ink-muted">
              Sinh viên thực hiện ({studentItems.length})
            </p>
            {studentItems.length > 0 ? (
              <div className="flex items-start gap-2.5">
                <AvatarGroup
                  items={[...studentItems]}
                  max={3}
                  size="sm"
                  ariaLabel="Sinh viên thực hiện"
                />
                <div className="min-w-0 space-y-1 text-xs text-ink-secondary leading-relaxed">
                  {studentItems.map((student) => (
                    <div key={student.id} className="truncate">
                      <span className="font-medium text-ink-heading">{student.name}</span>
                      {student.userCode ? (
                        <span className="text-ink-muted tabular-nums"> · {student.userCode}</span>
                      ) : null}
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              <p className="text-xs text-ink-muted">Đang cập nhật thành viên</p>
            )}
          </div>

          <div className="pt-3 border-t border-line/60 space-y-2">
            <p className="text-xs font-medium text-ink-muted">Giảng viên hướng dẫn</p>
            {advisorItems.length > 0 ? (
              <div className="flex items-center gap-2.5">
                <AvatarGroup
                  items={[...advisorItems]}
                  max={1}
                  size="sm"
                  ariaLabel="Giảng viên hướng dẫn"
                />
                <span className="min-w-0 truncate text-xs font-medium text-ink-heading">
                  {advisorName}
                </span>
              </div>
            ) : (
              <p className="text-xs text-ink-muted">{advisorName}</p>
            )}
          </div>
        </section>

        {/* Section 3: Rules & Quick Links */}
        <section className="p-5 space-y-3.5">
          <div className="space-y-1.5">
            <h2 className="text-xs font-semibold text-ink-heading">
              {isPublished ? 'Căn cứ công bố' : 'Điều kiện mở bảng điểm'}
            </h2>
            <ul className="space-y-1 text-xs text-ink-body list-disc pl-4 leading-relaxed">
              {isPublished ? (
                <>
                  <li>Điểm tổng hợp được tính theo cấu hình trọng số chính thức của đợt.</li>
                  <li>Hồ sơ lưu chiểu sau bảo vệ đã được Khoa nghiệm thu.</li>
                  <li>Điểm thành phần phản ánh đúng kết quả đánh giá dành cho bạn.</li>
                </>
              ) : (
                <>
                  <li>Nhóm hoàn tất nộp lưu chiểu và được Khoa nghiệm thu.</li>
                  <li>Khoa hoàn tất kiểm tra và công bố bảng điểm chính thức.</li>
                </>
              )}
            </ul>
          </div>

          <div className="flex flex-col gap-2 pt-1">
            <Button
              asChild
              variant={isPublished ? 'outline' : 'default'}
              size="sm"
              className="w-full justify-center"
            >
              <Link to="/thesis/archival">
                <Archive className="size-4" />
                {isPublished ? 'Xem hồ sơ lưu chiểu' : 'Đi tới Hồ sơ lưu chiểu'}
              </Link>
            </Button>
            <Button asChild variant="ghost" size="sm" className="w-full justify-center text-xs">
              <Link to="/thesis/final-report">
                Báo cáo & Góp ý bảo vệ
                <ExternalLink className="size-3.5" />
              </Link>
            </Button>
          </div>
        </section>
      </Card>
    </aside>
  );
};
