import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Spin, Typography } from 'antd';
import { useParams } from 'react-router-dom';
import { httpClient } from '../../shared/api/httpClient';
import { modules } from '../../shared/config/modules';

interface PingResponse {
  module: string;
  status: string;
}

export function ModulePingPage() {
  const { modulePath } = useParams<{ modulePath: string }>();
  const module = modules.find((m) => m.path === modulePath);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['module-ping', module?.key],
    queryFn: async () => {
      const response = await httpClient.get<PingResponse>(
        `/api/modules/${module!.key}/ping`,
      );
      return response.data;
    },
    enabled: Boolean(module),
  });

  if (!module) {
    return <Alert type="error" message="Không tìm thấy module" />;
  }

  return (
    <Card title={module.label}>
      {isLoading && <Spin />}
      {isError && (
        <Alert
          type="error"
          message="Không gọi được backend"
          description={error instanceof Error ? error.message : String(error)}
        />
      )}
      {data && (
        <Typography.Paragraph>
          Backend module <b>{data.module}</b> đang phản hồi:{' '}
          <Typography.Text code>{data.status}</Typography.Text>
        </Typography.Paragraph>
      )}
    </Card>
  );
}
