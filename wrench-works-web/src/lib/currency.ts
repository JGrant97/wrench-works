/**
 * Currency for the whole app.
 *
 * The business picks one on /settings/general and every amount follows it. Before this,
 * `formatCurrency` defaulted to GBP and nothing ever passed anything else, so a US
 * workshop's totals were rendered in pounds.
 *
 * The chosen code travels in the readable `ww_user` cookie alongside permissions and
 * features. That is what lets client components (via `useCurrency`) and server components
 * (via `getCurrency` in currency-server.ts) format the same way without either doing an
 * extra fetch.
 *
 * Deliberately NOT a module-level mutable "current currency". Server components share
 * module scope across requests, so one tenant's currency could bleed into another
 * tenant's render — the same class of cross-tenant leak the query filters exist to stop.
 */

export const SUPPORTED_CURRENCIES = [
  { code: "GBP", symbol: "£", label: "British Pound (GBP)" },
  { code: "USD", symbol: "$", label: "US Dollar (USD)" },
  { code: "EUR", symbol: "€", label: "Euro (EUR)" },
] as const;

export type CurrencyCode = (typeof SUPPORTED_CURRENCIES)[number]["code"];

export const DEFAULT_CURRENCY: CurrencyCode = "GBP";

/**
 * Narrows whatever arrived from the cookie or the API. An unrecognised code falls back
 * rather than throwing: a currency the client doesn't know about should not be able to
 * blank out every price on the page.
 */
export function toCurrencyCode(value: string | null | undefined): CurrencyCode {
  const match = SUPPORTED_CURRENCIES.find(
    (c) => c.code === value?.toUpperCase()
  );
  return match?.code ?? DEFAULT_CURRENCY;
}

export function currencySymbol(currency: string | null | undefined): string {
  const code = toCurrencyCode(currency);
  return SUPPORTED_CURRENCIES.find((c) => c.code === code)!.symbol;
}

/**
 * Locale is tied to the currency rather than the browser, so a US workshop reads $1,234.56
 * and a euro workshop reads the grouping its customers expect. Using the browser locale
 * instead would render the same invoice differently on two machines in the same workshop.
 */
const LOCALES: Record<CurrencyCode, string> = {
  GBP: "en-GB",
  USD: "en-US",
  EUR: "de-DE",
};

export function formatCurrency(amount: number, currency?: string | null) {
  const code = toCurrencyCode(currency);
  return new Intl.NumberFormat(LOCALES[code], { style: "currency", currency: code }).format(amount);
}
