import { useEffect, useState } from 'react';
import { Select, Space, Typography } from 'antd';
import { usePropertySearch } from '../../api/properties';
import { usePropertyRpus } from '../../api/rpus';
import { useTaxDeclarationsByRpu } from '../../api/taxDeclarations';
import { unitTypesFor, type SwornStatementItemKind } from '../../lib/types';

export interface PickedUnit {
  propertyId: string;
  rpuId: string;
  /** The unit's approved Tax Declaration, if it has one. */
  taxDeclarationId: string | null;
  tdNumber: string | null;
}

/**
 * Picks a unit of a registered property in one city/municipality: find the
 * property by PIN, lot, title or survey number, then choose the unit.
 */
export function UnitPicker({ municipalityId, kind, onChange }: {
  municipalityId: string;
  kind: SwornStatementItemKind;
  onChange: (unit: PickedUnit | null) => void;
}) {
  const [term, setTerm] = useState('');
  const [propertyId, setPropertyId] = useState<string>();
  const [rpuId, setRpuId] = useState<string>();
  const { data: properties, isFetching } = usePropertySearch({ searchTerm: term || undefined, municipalityId, pageSize: 10 });
  const { data: rpus = [] } = usePropertyRpus(propertyId ?? '');
  const { data: tds = [] } = useTaxDeclarationsByRpu(rpuId);
  const approved = tds.find((t) => t.status === 'Approved');
  const fitting = rpus.filter((r) => unitTypesFor[kind].includes(r.rpuType));

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Select aria-label="Property" showSearch filterOption={false} onSearch={setTerm} loading={isFetching} value={propertyId}
        placeholder="Search the property by PIN, lot, title or survey no."
        onChange={(v) => { setPropertyId(v); setRpuId(undefined); onChange(null); }}
        options={(properties?.items ?? []).map((p) => ({
          value: p.id, label: `${p.propertyIdentificationNumber} — ${[p.street, p.barangayName].filter(Boolean).join(', ')}`,
        }))} />
      {propertyId && (
        <Select aria-label="RPU" value={rpuId} placeholder={fitting.length ? 'Choose the RPU' : `No ${unitTypesFor[kind].join(' or ')} unit on this property`}
          disabled={fitting.length === 0}
          onChange={(v) => { setRpuId(v); onChange({ propertyId, rpuId: v, taxDeclarationId: null, tdNumber: null }); }}
          options={fitting.map((r) => ({ value: r.id, label: `${r.rpuNumber} (${r.rpuType})` }))} />
      )}
      {rpuId && propertyId && (
        <UnitTd approved={approved ? { id: approved.id, number: approved.taxDeclarationNumber } : null}
          onResolved={(td) => onChange({ propertyId, rpuId, taxDeclarationId: td?.id ?? null, tdNumber: td?.number ?? null })} />
      )}
    </Space>
  );
}

/** Reports the unit's approved TD once loaded, and shows it. */
function UnitTd({ approved, onResolved }: { approved: { id: string; number: string } | null; onResolved: (td: { id: string; number: string } | null) => void }) {
  const id = approved?.id ?? null;
  const number = approved?.number ?? null;
  useEffect(() => {
    onResolved(id && number ? { id, number } : null);
    // Report only when the TD changes, not on every parent render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, number]);
  return approved
    ? <Typography.Text type="secondary">Approved Tax Declaration: <b>{approved.number}</b></Typography.Text>
    : <Typography.Text type="warning">This unit has no approved Tax Declaration.</Typography.Text>;
}
