import { Col, Form, Row, Select } from 'antd';
import { useBarangays, useMunicipalities, useProvinces } from '../api/referenceData';

/**
 * Cascading Province → Municipality → Barangay selects, wired as three
 * Form.Item fields with the given names. Must be rendered inside an antd
 * <Form>. Selecting a province resets municipality/barangay, and selecting
 * a municipality resets barangay — mirrors the real PSGC hierarchy
 * (docs/DOMAIN-MODEL.md §3 geography).
 */
export function LocationSelect({
  provinceFieldName,
  municipalityFieldName,
  barangayFieldName,
  provinceId,
  municipalityId,
  onProvinceChange,
  onMunicipalityChange,
  required = true,
}: {
  provinceFieldName: string;
  municipalityFieldName: string;
  barangayFieldName: string;
  provinceId: string | undefined;
  municipalityId: string | undefined;
  onProvinceChange: (id: string | undefined) => void;
  onMunicipalityChange: (id: string | undefined) => void;
  required?: boolean;
}) {
  const { data: provinces, isLoading: loadingProvinces } = useProvinces();
  const { data: municipalities, isLoading: loadingMunicipalities } = useMunicipalities(provinceId);
  const { data: barangays, isLoading: loadingBarangays } = useBarangays(municipalityId);

  return (
    <Row gutter={16}>
      <Col span={8}>
        <Form.Item name={provinceFieldName} label="Province" rules={required ? [{ required: true, message: 'Province is required' }] : []}>
          <Select
            placeholder="Select province"
            loading={loadingProvinces}
            options={provinces?.map((p) => ({ value: p.id, label: p.name }))}
            onChange={onProvinceChange}
            showSearch
            optionFilterProp="label"
          />
        </Form.Item>
      </Col>
      <Col span={8}>
        <Form.Item name={municipalityFieldName} label="City/Municipality" rules={required ? [{ required: true, message: 'Municipality is required' }] : []}>
          <Select
            placeholder={provinceId ? 'Select municipality' : 'Select a province first'}
            loading={loadingMunicipalities}
            disabled={!provinceId}
            options={municipalities?.map((m) => ({ value: m.id, label: m.name }))}
            onChange={onMunicipalityChange}
            showSearch
            optionFilterProp="label"
          />
        </Form.Item>
      </Col>
      <Col span={8}>
        <Form.Item name={barangayFieldName} label="Barangay" rules={required ? [{ required: true, message: 'Barangay is required' }] : []}>
          <Select
            placeholder={municipalityId ? 'Select barangay' : 'Select a municipality first'}
            loading={loadingBarangays}
            disabled={!municipalityId}
            options={barangays?.map((b) => ({ value: b.id, label: b.name }))}
            showSearch
            optionFilterProp="label"
          />
        </Form.Item>
      </Col>
    </Row>
  );
}
