import { Select } from 'antd';
import { useAllMunicipalities, useProvinces } from '../api/referenceData';
import { useCurrentUser } from '../api/offices';

/**
 * The provincial consolidated view's municipality filter (docs/analysis/province-wide-operation.md §3.7).
 * A municipal user is offered only the municipalities their office covers; empty means all of them.
 */
export function MunicipalityFilter({ value, onChange, placeholder = 'All municipalities', width = 280 }: {
  value: string | undefined; onChange: (id: string | undefined) => void; placeholder?: string; width?: number | string;
}) {
  const municipalities = useAllMunicipalities();
  const provinces = useProvinces();
  const me = useCurrentUser();
  const allowed = me.data?.municipalityIds;
  const provinceName = new Map((provinces.data ?? []).map((p) => [p.id, p.name]));
  const options = (municipalities.data ?? [])
    .filter((m) => !allowed || allowed.includes(m.id))
    .map((m) => ({ value: m.id, label: `${m.name} — ${provinceName.get(m.provinceId) ?? ''}` }))
    .sort((a, b) => a.label.localeCompare(b.label));
  return (
    <Select allowClear showSearch optionFilterProp="label" placeholder={placeholder} aria-label="Municipality"
      loading={municipalities.isLoading} options={options} value={value} onChange={(v) => onChange(v ?? undefined)}
      style={{ width, maxWidth: '100%' }} />
  );
}
