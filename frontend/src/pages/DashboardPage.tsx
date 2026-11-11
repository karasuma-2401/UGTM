import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import {
  ArrowRight,
  ArrowUpRight,
  CalendarRange,
  CheckCircle2,
  Clock3,
  Layers,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { useAuth } from '@/shared/auth';
import { getIdentityAccess } from '@/shared/auth/identityAccess';
import { getCurrentBatches } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { navigationSections } from '@/shared/layout/navigationConfig';
import { modules } from '@/shared/config/modules';

const ROLE_LABELS: Record<string, string> = {
  Admin: 'Quản trị hệ thống',
  AcademicAffairs: 'Phòng Đào tạo / Giáo vụ',
  Faculty: 'Ban chủ nhiệm Khoa',
  Lecturer: 'Giảng viên hướng dẫn',
  CouncilMember: 'Hội đồng chấm KLTN',
  Student: 'Sinh viên thực hiện KLTN',
};

const WORKFLOW_DESCRIPTIONS: Record<string, string> = {
  '/modules/identity': 'Quản lý danh sách tài khoản sinh viên, giảng viên và phân quyền vai trò.',
  '/admin/grade-config': 'Thiết lập tỷ trọng điểm hướng dẫn, phản biện, hội đồng theo từng đợt.',
  '/admin/audit-logs': 'Tra cứu lịch sử thao tác và truy vết bảo mật trên toàn hệ thống.',
  '/academic/periods': 'Khởi tạo đợt khóa luận, cấu hình mốc thời gian và cổng nộp hồ sơ.',
  '/academic/roster': 'Import và đồng bộ danh sách sinh viên, giảng viên đủ điều kiện trong đợt.',
  '/topics/advising': 'Đề xuất đề tài mới, quản lý danh sách nhóm đăng ký và tiến độ hướng dẫn.',
  '/advisor/milestones': 'Thiết lập các mốc báo cáo tiến độ và nhận xét bài nộp của nhóm sinh viên.',
  '/topics/catalog': 'Tra cứu danh mục đề tài công bố, đăng ký cá nhân hoặc mời thành viên nhóm.',
  '/topics/rename': 'Gửi đơn đề nghị điều chỉnh tên đề tài khóa luận trong giai đoạn cho phép.',
  '/topics/rename/review': 'Xem xét và phê duyệt yêu cầu đổi tên đề tài theo cấp thẩm quyền.',
  '/thesis/proposal': 'Nộp đề cương, báo cáo giữa kỳ, mốc tiến độ và báo cáo cuối kỳ.',
  '/topics/review': 'Thẩm định nội dung đề tài do giảng viên đề xuất và công bố danh mục.',
  '/thesis/review/syllabus': 'Thẩm định hồ sơ đề cương và báo cáo giữa kỳ của các nhóm sinh viên.',
  '/councils/reviewer-assignments': 'Phân công giảng viên phản biện và thành lập hội đồng bảo vệ.',
  '/councils/similarity-reports': 'Kiểm tra báo cáo tương đồng và bằng chứng đối sánh văn bản.',
  '/councils/approval': 'Rà soát và phê duyệt danh sách hội đồng bảo vệ khóa luận chính thức.',
};

export function DashboardPage() {
  const { user, hasPermission, hasRole } = useAuth();
  const batchesQuery = useAcademicBatches();
  const canReadBatches = hasPermission('academic.batches.read');
  const currentBatches = useMemo(
    () => getCurrentBatches(batchesQuery.data ?? []),
    [batchesQuery.data],
  );
  const allBatches = batchesQuery.data ?? [];

  const accessibleWorkflows = useMemo(() => {
    return navigationSections
      .flatMap((section) => {
        if (section.roles && !hasRole(section.roles)) return [];
        if (section.permissions && !hasPermission(section.permissions)) return [];
        return section.items
          .filter((item) => {
            if (item.path === '/') return false;
            if (item.path === '/modules/identity') {
              return getIdentityAccess(hasPermission).canAccess;
            }
            const requiredPerms = item.permissions || item.permission;
            if (requiredPerms) {
              const perms = Array.isArray(requiredPerms) ? requiredPerms : [requiredPerms];
              if (perms.length > 0) {
                const ok = item.requireAllPermissions
                  ? perms.every((p) => hasPermission(p))
                  : hasPermission(perms);
                if (!ok) return false;
              }
            }
            const requiredRoles = item.roles || item.role;
            if (requiredRoles) {
              const roles = Array.isArray(requiredRoles) ? requiredRoles : [requiredRoles];
              if (roles.length > 0 && !hasRole(roles)) return false;
            }
            return true;
          })
          .map((item) => ({
            ...item,
            sectionTitle: section.title || 'Nghiệp vụ',
            description:
              WORKFLOW_DESCRIPTIONS[item.path] ||
              `Truy cập và thực hiện các tác vụ thuộc phân hệ ${item.label}.`,
          }));
      });
  }, [hasPermission, hasRole]);

  const roleBadges = useMemo(() => {
    if (!user?.roles || user.roles.length === 0) return ['Thành viên'];
    return user.roles.map((r) => ROLE_LABELS[r] || r);
  }, [user]);

  return (
    <div className="space-y-6">
      {/* Top Asymmetric Bento Grid: Hero Workspace (8 cols) + Live Period Status (4 cols) */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        {/* Primary Workspace Banner */}
        <Card className="relative overflow-hidden border-slate-200/80 bg-white p-6 sm:p-8 lg:col-span-8 flex flex-col justify-between">
          <div
            aria-hidden="true"
            className="pointer-events-none absolute -right-20 -top-20 size-72 rounded-full bg-blue-500/[0.06] blur-3xl"
          />
          <div
            aria-hidden="true"
            className="pointer-events-none absolute bottom-0 right-12 size-48 rounded-full bg-orange-400/[0.06] blur-2xl"
          />

          <div className="relative z-10 space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              {roleBadges.map((roleLabel) => (
                <StatusBadge key={roleLabel} tone="info">
                  {roleLabel}
                </StatusBadge>
              ))}
            </div>

            <div className="space-y-2 max-w-2xl">
              <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900">
                Xin chào, {user?.fullName || 'bạn'}
              </h1>
              <p className="text-sm sm:text-base text-slate-600 leading-relaxed">
                Hệ thống quản lý khóa luận tốt nghiệp UGTM. Chọn nhanh không gian làm việc bên dưới
                hoặc nhấn{' '}
                <kbd className="rounded-md border border-slate-200 bg-slate-50 px-1.5 py-0.5 font-mono text-xs font-medium text-slate-700">
                  Ctrl K
                </kbd>{' '}
                để chuyển nhanh tới bất kỳ phân hệ nào bạn được phân quyền.
              </p>
            </div>
          </div>

          {accessibleWorkflows.length > 0 && (
            <div className="relative z-10 mt-6 flex flex-wrap items-center gap-2.5 pt-4 border-t border-slate-100">
              {accessibleWorkflows.slice(0, 3).map((item, index) => {
                const Icon = item.icon;
                return (
                  <Button
                    key={item.path}
                    asChild
                    variant={index === 0 ? 'default' : 'outline'}
                    size="sm"
                    className="h-9 rounded-xl px-4"
                  >
                    <Link to={item.path}>
                      <Icon className="size-4" />
                      <span>{item.label}</span>
                      {index === 0 && <ArrowRight className="size-3.5 ml-0.5" />}
                    </Link>
                  </Button>
                );
              })}
            </div>
          )}
        </Card>

        {/* Live Academic Batch Status Bento Card */}
        <Card className="border-slate-200/80 bg-gradient-to-b from-slate-50/70 to-white p-6 lg:col-span-4 flex flex-col justify-between">
          <div className="space-y-4">
            <div className="flex items-center justify-between gap-2">
              <div className="flex items-center gap-2.5">
                <span className="flex size-9 items-center justify-center rounded-xl border border-blue-100 bg-blue-50/70 text-primary">
                  <CalendarRange className="size-4.5" />
                </span>
                <div>
                  <h2 className="text-sm font-semibold text-slate-900">Đợt khóa luận</h2>
                  <p className="text-xs text-slate-500">Trạng thái thời gian thực</p>
                </div>
              </div>
              {canReadBatches && !batchesQuery.isPending && !batchesQuery.isError && (
                <StatusBadge tone={currentBatches.length > 0 ? 'success' : 'neutral'}>
                  {currentBatches.length > 0 ? `${currentBatches.length} đang mở` : 'Chưa mở'}
                </StatusBadge>
              )}
            </div>

            {!canReadBatches ? (
              <div className="rounded-xl border border-slate-200/70 bg-white p-4 text-xs text-slate-600 leading-relaxed">
                Tài khoản hiện tại chưa được cấp quyền tra cứu danh sách đợt khóa luận.
              </div>
            ) : batchesQuery.isPending ? (
              <div className="space-y-2.5" role="status" aria-label="Đang tải đợt khóa luận">
                <Skeleton className="h-14 w-full rounded-xl" />
                <Skeleton className="h-10 w-full rounded-xl" />
              </div>
            ) : batchesQuery.isError ? (
              <div
                role="alert"
                className="rounded-xl border border-rose-200/80 bg-rose-50/60 p-3.5 space-y-2.5"
              >
                <p className="text-xs text-rose-700 font-medium">
                  Không tải được thông tin đợt khóa luận từ máy chủ.
                </p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  leftIcon={<RefreshCw className="size-3.5" />}
                  disabled={batchesQuery.isFetching}
                  onClick={() => void batchesQuery.refetch()}
                >
                  Thử lại
                </Button>
              </div>
            ) : currentBatches.length === 0 ? (
              <div className="rounded-xl border border-dashed border-slate-200 bg-white p-4 text-xs text-slate-500 leading-relaxed">
                Hiện không có đợt khóa luận nào đang trong thời gian mở hoạt động.
                {allBatches.length > 0 && (
                  <span className="block mt-1 font-medium text-slate-700">
                    Tổng số đợt đã cấu hình: {allBatches.length} đợt.
                  </span>
                )}
              </div>
            ) : (
              <div className="space-y-2">
                {currentBatches.slice(0, 2).map((batch) => (
                  <div
                    key={batch.id}
                    className="rounded-xl border border-slate-200/80 bg-white p-3.5 shadow-2xs"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <p className="text-sm font-semibold text-slate-900 leading-snug">
                        {batch.batchName}
                      </p>
                      <CheckCircle2 className="size-4 text-emerald-600 shrink-0 mt-0.5" />
                    </div>
                    <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                      <span className="rounded-md bg-slate-100 px-2 py-0.5 font-medium text-slate-700">
                        HK{batch.semester} • {batch.academicYear}
                      </span>
                      <span className="inline-flex items-center gap-1">
                        <Clock3 className="size-3.5 text-slate-400" />
                        {batch.startDate} → {batch.endDate}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-xs text-slate-500">
            <span>Phân hệ khả dụng</span>
            <span className="font-semibold text-slate-800 tabular-nums">
              {accessibleWorkflows.length} chức năng
            </span>
          </div>
        </Card>
      </div>

      {/* Role-Based Quick Workflows Bento Section */}
      {accessibleWorkflows.length > 0 && (
        <section className="space-y-3.5" aria-label="Không gian làm việc theo vai trò">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-lg font-bold tracking-tight text-slate-900">
                Không gian làm việc của bạn
              </h2>
              <p className="text-xs text-slate-500">
                Các chức năng nghiệp vụ được mở theo quyền của tài khoản hiện tại
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-3">
            {accessibleWorkflows.map((item) => {
              const Icon = item.icon;
              return (
                <Link key={item.path} to={item.path} className="group block">
                  <Card className="h-full border-slate-200/80 bg-white p-5 transition-all duration-150 hover:border-primary/40 hover:shadow-[0_8px_24px_-8px_rgba(37,99,235,0.12)] group-hover:-translate-y-0.5">
                    <div className="flex items-start justify-between gap-3">
                      <span className="flex size-10 items-center justify-center rounded-xl border border-slate-200/80 bg-slate-50/80 text-slate-700 transition-colors group-hover:border-blue-200 group-hover:bg-blue-50/70 group-hover:text-primary">
                        <Icon className="size-5" />
                      </span>
                      <span className="inline-flex items-center gap-1 rounded-full bg-slate-100/80 px-2.5 py-0.5 text-[11px] font-medium text-slate-600">
                        {item.sectionTitle}
                      </span>
                    </div>

                    <div className="mt-4 space-y-1">
                      <div className="flex items-center justify-between gap-2">
                        <h3 className="text-base font-semibold tracking-tight text-slate-900 group-hover:text-primary transition-colors">
                          {item.label}
                        </h3>
                        <ArrowUpRight className="size-4 text-slate-400 transition-transform group-hover:translate-x-0.5 group-hover:-translate-y-0.5 group-hover:text-primary shrink-0" />
                      </div>
                      <p className="text-xs text-slate-500 leading-relaxed line-clamp-2">
                        {item.description}
                      </p>
                    </div>
                  </Card>
                </Link>
              );
            })}
          </div>
        </section>
      )}

      {/* Modular Monolith Health / System Modules */}
      <section className="space-y-3.5" aria-label="Phân hệ kiến trúc hệ thống">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <span className="flex size-8 items-center justify-center rounded-lg bg-slate-100 text-slate-600">
              <Layers className="size-4" />
            </span>
            <div>
              <h2 className="text-base font-semibold tracking-tight text-slate-900">
                Kiến trúc Phân hệ (Modular Monolith)
              </h2>
              <p className="text-xs text-slate-500">
                {modules.length} phân hệ nghiệp vụ và kiểm tra kết nối dịch vụ
              </p>
            </div>
          </div>
          <Badge variant="secondary" rounded="full" className="text-[11px] font-medium">
            <ShieldCheck className="size-3.5 mr-1 text-emerald-600" />
            {modules.length} Modules
          </Badge>
        </div>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {modules.map((module) => (
            <Link key={module.path} to={`/modules/${module.path}`} className="group block">
              <Card className="h-full border-slate-200/80 bg-white px-4 py-3.5 transition-all duration-150 hover:border-slate-300 hover:bg-slate-50/40">
                <div className="flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold text-slate-800 group-hover:text-primary transition-colors truncate">
                        {module.label}
                      </span>
                      <span className="rounded-md bg-slate-100 px-1.5 py-0.5 font-mono text-[10px] text-slate-500">
                        {module.key}
                      </span>
                    </div>
                    <p className="mt-0.5 text-xs text-slate-500 truncate">
                      Kiểm tra kết nối API & trạng thái phân hệ
                    </p>
                  </div>
                  <ArrowRight className="size-4 text-slate-400 group-hover:text-primary group-hover:translate-x-0.5 transition-all shrink-0" />
                </div>
              </Card>
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}
