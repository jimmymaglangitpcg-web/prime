import { Card, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { TaxpayerForm } from '../../components/TaxpayerForm';

export function TaxpayerRegisterPage() {
  const navigate = useNavigate();

  return (
    <Card style={{ maxWidth: 700 }}>
      <Typography.Title level={3}>Register Taxpayer</Typography.Title>
      <TaxpayerForm onSuccess={() => navigate('/taxpayers')} />
    </Card>
  );
}
