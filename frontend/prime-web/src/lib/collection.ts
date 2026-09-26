import type { PaymentStatus } from './types';

/** Tag colours for payment statuses (docs/analysis/collection.md). */
export const paymentStatusColor: Record<PaymentStatus, string> = { Posted: 'green', Voided: 'red', Reversed: 'volcano' };
