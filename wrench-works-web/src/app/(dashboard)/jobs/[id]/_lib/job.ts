
// Re-exported from the generated client so these names stay stable for the
// components that import them, while the shapes come from the API contract.
import type { LaborLineDto as LaborLine, PartLineDto as PartLine, JobDetailDto as JobDetail, TaxComponentLineDto as TaxComponentLine, TaxLineDto as TaxLine } from "@/api/generated/models";
export type { LaborLine, PartLine, JobDetail, TaxComponentLine, TaxLine };

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
