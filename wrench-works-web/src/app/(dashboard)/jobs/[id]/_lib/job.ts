/**
 * Job detail types and the status-transition table.
 *
 * Split out of page.tsx (533 lines holding the page and three modals). The transition
 * table lives here rather than in the page because it is the UI's mirror of the server's
 * ValidTransitions in JobEndpoints.cs — if one changes, so must the other, and it is
 * easier to notice that when it is not buried in a render tree.
 */
/** Tax as charged on this line. See docs/tax.md — the rate is snapshotted, not looked up. */
interface TaxedLine {
  /** A fraction: 0.2 is 20%. Zero means the line was not taxed. */
  taxRatePercent: number;
  taxAmount: number;
}

export interface LaborLine extends TaxedLine {
  id: string;
  description: string;
  hours: number;
  rate: number;
  total: number;
}

export interface PartLine extends TaxedLine {
  id: string;
  inventoryItemId: string;
  itemName: string;
  sku: string | null;
  quantity: number;
  unitPrice: number;
  total: number;
}

export interface JobDetail {
  id: string;
  title: string;
  status: string;
  priority: string;
  customerId: string;
  customerName: string;
  vehicleId: string;
  vehicleDisplay: string;
  internalNotes: string | null;
  customerNotes: string | null;
  scheduledStartUtc: string | null;
  scheduledEndUtc: string | null;
  createdAtUtc: string;
  laborLines: LaborLine[];
  partLines: PartLine[];
  laborTotal: number;
  partsTotal: number;
  grandTotal: number;
  // Net of tax. With tax-inclusive pricing the labour/parts totals already contain tax,
  // which is why the server states subTotal rather than leaving it to be derived.
  subTotal: number;
  taxTotal: number;
  taxLabel: string;
  pricesIncludeTax: boolean;
  customerIsTaxExempt: boolean;
  taxBreakdown: TaxLine[];
}

export interface TaxComponentLine {
  name: string;
  ratePercent: number;
}

export interface TaxLine {
  name: string;
  ratePercent: number;
  amount: number;
  components: TaxComponentLine[];
}

export const STATUS_TRANSITIONS: Record<string, { value: string; label: string }[]> = {
  Draft: [
    { value: "Scheduled", label: "Schedule" },
    { value: "Closed", label: "Close" },
  ],
  Scheduled: [
    { value: "InProgress", label: "Start Work" },
    { value: "Closed", label: "Close" },
  ],
  InProgress: [
    { value: "WaitingParts", label: "Waiting Parts" },
    { value: "Completed", label: "Complete" },
  ],
  WaitingParts: [
    { value: "InProgress", label: "Resume Work" },
  ],
  Completed: [
    { value: "Invoiced", label: "Mark Invoiced" },
  ],
  Invoiced: [
    { value: "Closed", label: "Close" },
  ],
};
