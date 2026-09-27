import { Link } from 'react-router-dom';
import {
  BookOpen,
  Clock,
  GraduationCap,
  Sparkles,
  Users,
  ArrowRight,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { modules } from '@/shared/config/modules';

export function DashboardPage() {
  const stats = [
    {
      title: 'Đề tài đăng ký',
      value: '128',
      change: '+12% so với kỳ trước',
      icon: BookOpen,
      color: 'text-blue-600 bg-blue-50',
    },
    {
      title: 'Chờ thẩm định đề cương',
      value: '34',
      change: 'Cần xử lý trong tuần này',
      icon: Clock,
      color: 'text-amber-600 bg-amber-50',
    },
    {
      title: 'Giảng viên hướng dẫn',
      value: '42',
      change: '100% đã nhận đề tài',
      icon: Users,
      color: 'text-emerald-600 bg-emerald-50',
    },
    {
      title: 'Sinh viên đủ điều kiện',
      value: '215',
      change: 'Đợt 1 HK2 (2025-2026)',
      icon: GraduationCap,
      color: 'text-indigo-600 bg-indigo-50',
    },
  ];

  return (
    <div className="space-y-6">
      {/* Banner chào mừng */}
      <div className="relative overflow-hidden rounded-2xl bg-gradient-to-r from-blue-700 via-indigo-700 to-sky-700 p-6 sm:p-8 text-white shadow-md">
        <div className="relative z-10 max-w-2xl space-y-2">
          <Badge className="bg-white/20 text-white hover:bg-white/30 backdrop-blur-sm border-white/20">
            <Sparkles className="size-3.5 mr-1" />
            Học kỳ 2 (2025 - 2026) • Đợt 1
          </Badge>
          <h1 className="text-2xl sm:text-3xl font-bold tracking-tight">
            Cổng Quản Lý Khóa Luận Tốt Nghiệp (UGTM)
          </h1>
          <p className="text-blue-100 text-sm sm:text-base leading-relaxed">
            Hệ thống quản lý toàn diện quy trình làm khóa luận từ đăng ký đề
            tài, thẩm định đề cương, theo dõi tiến độ đến thành lập hội đồng và
            tổng hợp điểm bảo vệ.
          </p>
        </div>
      </div>

      {/* 4 Thẻ Thống kê nhanh */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {stats.map((stat) => {
          const Icon = stat.icon;
          return (
            <Card key={stat.title} className="border-slate-200 shadow-sm">
              <CardContent className="p-5 flex items-center justify-between">
                <div>
                  <p className="text-xs font-medium text-slate-500">{stat.title}</p>
                  <p className="text-2xl font-bold text-slate-900 mt-1">
                    {stat.value}
                  </p>
                  <p className="text-[11px] text-slate-500 mt-1">{stat.change}</p>
                </div>
                <div className={`p-3 rounded-xl ${stat.color}`}>
                  <Icon className="size-5" />
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      {/* Danh sách 9 Phân hệ nghiệp vụ */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-lg font-bold text-slate-900">
              Phân Hệ Nghiệp Vụ Cốt Lõi
            </h2>
            <p className="text-xs text-slate-500">
              {modules.length} phân hệ vi mô sẵn sàng kết nối API
            </p>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {modules.map((module) => (
            <Link
              key={module.path}
              to={`/modules/${module.path}`}
              className="group"
            >
              <Card className="h-full border-slate-200 transition-all hover:border-primary/50 hover:shadow-md group-hover:-translate-y-0.5">
                <CardHeader className="p-5">
                  <div className="flex items-center justify-between">
                    <Badge variant="outline" className="text-[11px] text-slate-600">
                      Module: {module.key}
                    </Badge>
                    <ArrowRight className="size-4 text-slate-400 group-hover:text-primary group-hover:translate-x-1 transition-all" />
                  </div>
                  <CardTitle className="text-base text-slate-900 mt-3 group-hover:text-primary transition-colors">
                    {module.label}
                  </CardTitle>
                  <CardDescription className="text-xs text-slate-500 line-clamp-2">
                    Kiểm tra trạng thái kết nối backend và các thao tác nghiệp vụ
                    thuộc phân hệ {module.label}.
                  </CardDescription>
                </CardHeader>
              </Card>
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
