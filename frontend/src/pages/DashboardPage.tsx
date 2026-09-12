import { Card, Typography } from 'antd';
import { modules } from '../shared/config/modules';

export function DashboardPage() {
  return (
    <Card>
      <Typography.Title level={3}>Tổng quan hệ thống</Typography.Title>
      <Typography.Paragraph>
        Hệ thống quản lý Khóa luận Tốt nghiệp gồm {modules.length} module nghiệp
        vụ, tương ứng với các module backend. Chọn một mục ở menu bên trái để
        xem chi tiết.
      </Typography.Paragraph>
    </Card>
  );
}
