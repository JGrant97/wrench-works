import { cookies } from "next/headers";
import { type CurrencyCode, toCurrencyCode } from "@/lib/currency";

/**
 * The business's currency, for server components.
 *
 * Separate file from currency.ts because it imports `next/headers`, which cannot be
 * pulled into a client bundle — importing it from a shared module would break every
 * client component that formats money.
 *
 * Reads the same `ww_user` cookie the client reads, so a server-rendered page and a
 * client-rendered one cannot disagree about what currency the workshop uses. The cookie
 * is user-controlled, but the only thing at stake here is which symbol is drawn — nothing
 * is authorised on it, and an unrecognised value falls back to GBP.
 */
export async function getCurrency(): Promise<CurrencyCode> {
  const raw = (await cookies()).get("ww_user")?.value;
  if (!raw) return toCurrencyCode(null);

  try {
    const parsed = JSON.parse(raw) as { currency?: string };
    return toCurrencyCode(parsed.currency);
  } catch {
    // A malformed cookie must degrade to the default, not throw and blank the page.
    return toCurrencyCode(null);
  }
}
