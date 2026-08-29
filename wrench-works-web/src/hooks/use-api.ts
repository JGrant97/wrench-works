"use client";

import useSWR, { SWRConfiguration } from "swr";
import useSWRMutation from "swr/mutation";
import { swrFetcher, fetcher } from "@/lib/fetcher";

/**
 * SWR-based data fetching hook. Calls the Next.js proxy routes.
 *
 *   const { data, isLoading } = useApi<Customer[]>('/api/customers');
 *   const { data } = useApi<JobDetail>(`/api/jobs/${id}`);
 */
export function useApi<T>(key: string | null, config?: SWRConfiguration<T>) {
  return useSWR<T>(key, swrFetcher, {
    revalidateOnFocus: false,
    ...config,
  });
}

/**
 * SWR-based data fetching with query params.
 *
 *   const { data } = useApiQuery<PaginatedList>('/api/customers', { page: '1', search: 'john' });
 */
export function useApiQuery<T>(
  basePath: string,
  params?: Record<string, string | undefined>,
  config?: SWRConfiguration<T>
) {
  const filtered = params
    ? Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== ""))
    : {};
  const qs = Object.keys(filtered).length
    ? `?${new URLSearchParams(filtered as Record<string, string>).toString()}`
    : "";

  return useSWR<T>(`${basePath}${qs}`, swrFetcher, {
    revalidateOnFocus: false,
    ...config,
  });
}

/**
 * Mutation hook for POST/PUT/PATCH/DELETE.
 *
 *   const { trigger, isMutating } = useMutation<Customer>('/api/customers', 'POST');
 *   await trigger({ name: 'John', phone: '...' });
 */
export function useMutation<TData = unknown, TBody = unknown>(
  url: string,
  method: "POST" | "PUT" | "PATCH" | "DELETE" = "POST"
) {
  return useSWRMutation<TData, Error, string, TBody>(
    url,
    async (_key: string, { arg }: { arg: TBody }) => {
      switch (method) {
        case "POST":
          return fetcher.post<TData>(url, arg);
        case "PUT":
          return fetcher.put<TData>(url, arg);
        case "PATCH":
          return fetcher.patch<TData>(url, arg);
        case "DELETE":
          return fetcher.delete<TData>(url);
      }
    }
  );
}
