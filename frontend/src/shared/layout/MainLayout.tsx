import { Layout, Menu, Typography } from 'antd';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { modules } from '../config/modules';

const { Header, Sider, Content } = Layout;

const menuItems = [
  { key: '/', label: <Link to="/">Tổng quan</Link> },
  ...modules.map((module) => ({
    key: `/modules/${module.path}`,
    label: <Link to={`/modules/${module.path}`}>{module.label}</Link>,
  })),
];

export function MainLayout() {
  const location = useLocation();

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider width={240} breakpoint="lg" collapsedWidth={0}>
        <Typography.Title level={4} style={{ color: '#fff', margin: 16 }}>
          UGTM
        </Typography.Title>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          items={menuItems}
        />
      </Sider>
      <Layout>
        <Header style={{ background: '#fff', paddingInline: 24 }}>
          <Typography.Text strong>Quản lý Khóa luận Tốt nghiệp</Typography.Text>
        </Header>
        <Content style={{ margin: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
