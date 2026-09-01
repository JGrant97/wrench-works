"use client";

import { useMemo } from "react";
import { useAuth } from "@/hooks/use-auth";
import { currencySymbol, formatCurrency, toCurrencyCode } from "@/lib/currency";

/**
 * The business's currency, for client components.
 *
 * Returns a bound `format` so call sites stay `format(total)` rather than threading the
 * code through every component that happens to render a number. Reuse this instead of
 * importing `formatCurrency` directly — a bare call falls back to GBP, which is exactly
 * the bug this replaces.
 *
 *   const { format, symbol } = useCurrency();
 *   <p>{format(job.grandTotal)}</p>
 *   <Input label={`Rate (${symbol}/hr)`} />
 */
export function useCurrency() {
  const { user } = useAuth();
  const currency = toCurrencyCode(user?.currency);

  return useMemo(
    () => ({
      currency,
      symbol: currencySymbol(currency),
      format: (amount: number) => formatCurrency(amount, currency),
    }),
    [currency]
  );
}
