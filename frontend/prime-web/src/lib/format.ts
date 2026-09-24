const money = new Intl.NumberFormat('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Amount with 2 decimals and thousands separators, no currency symbol (e.g. "1,234.50"). */
export const formatMoney = (value: number) => money.format(value);
