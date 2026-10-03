import { Tag, Tooltip } from 'antd';
import type { Taxability } from '../lib/types';

const color: Record<Taxability, string> = { Taxable: 'default', Exempt: 'green', PartlyExempt: 'cyan' };
const label: Record<Taxability, string> = { Taxable: 'Taxable', Exempt: 'Exempt', PartlyExempt: 'Partly exempt' };

/**
 * Taxable, exempt or (a TD) partly exempt (docs/analysis/assessment-listing-exemptions.md §4.1), with the exemption's
 * legal basis or the reason the line is as it is in the tooltip.
 */
export function TaxabilityTag({ value, legalBasis, note }: { value: Taxability; legalBasis?: string | null; note?: string | null }) {
  const tip = [legalBasis, note].filter(Boolean).join(' — ');
  const tag = <Tag color={color[value]}>{label[value]}</Tag>;
  return tip ? <Tooltip title={tip}>{tag}</Tooltip> : tag;
}
