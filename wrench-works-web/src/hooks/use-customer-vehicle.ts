"use client";

import { useApiQuery } from "@/hooks/use-api";

/**
 * The customer-then-vehicle pairing every booking and job creation flow needs: search for
 * a customer, then choose one of their vehicles.
 *
 * Extracted because New Booking and New Job each had their own copy — same two queries,
 * same 2-character threshold, and their own inline declarations of the response shapes.
 * Duplicated response types are how four display bugs reached the browser with TypeScript
 * perfectly happy (see docs/app-flow.md), so one declaration is worth more here than the
 * few lines saved.
 *
 * Both queries are conditional: passing null as the SWR key is what stops them firing
 * until there is something to fetch.
 */

export interface CustomerSearchResult {
  id: string;
  name: string;
  phone?: string;
}

export interface CustomerVehicle {
  id: string;
  displayName: string;
  registration?: string;
}

export function useCustomerVehicle(
  search: string,
  customerId: string,
  /**
   * New Job stops searching once a customer is chosen; New Booking keeps the list open so
   * the choice can be changed without clearing the field first. The two flows genuinely
   * differ, so it is a parameter rather than a behaviour imposed on both.
   */
  { keepSearchingAfterSelect = true }: { keepSearchingAfterSelect?: boolean } = {}
) {
  const searching = search.length >= 2 && (keepSearchingAfterSelect || !customerId);

  const { data: customers } = useApiQuery<CustomerSearchResult[]>(
    searching ? "/api/customers/search" : null,
    { q: search }
  );

  const { data: customerDetail } = useApiQuery<{ vehicles: CustomerVehicle[] }>(
    customerId ? `/api/customers/${customerId}` : null
  );

  return {
    customers,
    vehicles: customerDetail?.vehicles ?? [],
    /**
     * True only once the customer's record has actually loaded. Distinguishes "this
     * customer has no vehicles" from "the vehicles have not arrived yet" — the callers
     * use it to decide whether to show the add-a-vehicle-first warning.
     */
    customerLoaded: Boolean(customerDetail),
  };
}
