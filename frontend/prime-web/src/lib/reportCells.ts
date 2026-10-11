import type { ReportCell, ReportColumn } from '../api/reports';
import { formatMoney } from './format';

const area = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 4 });
const count = new Intl.NumberFormat('en-PH');
const rate = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 6 });

/** A report cell as shown on screen and in the print view (docs/analysis/reporting.md §4.1). */
export function formatCell(column: ReportColumn, value: ReportCell) {
  if (value === null || value === undefined || value === '') return '';
  if (typeof value === 'number') {
    if (column.type === 'Money') return formatMoney(value);
    if (column.type === 'Area') return area.format(value);
    if (column.type === 'Integer') return count.format(value);
    if (column.type === 'Percent') return `${rate.format(value)}%`;
  }
  return String(value);
}

export const numeric = (column: ReportColumn) => column.type === 'Money' || column.type === 'Area' || column.type === 'Integer' || column.type === 'Percent';
