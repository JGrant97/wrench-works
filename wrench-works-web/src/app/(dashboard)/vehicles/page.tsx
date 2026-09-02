"use client";

import { useState } from "react";
import Link from "next/link";
import { useApiQuery } from "@/hooks/use-api";
import { Card, PageHeader, Spinner, EmptyState } from "@/components/ui";
import { ErrorState } from "@/components/data-state";
import { Car, Search } from "lucide-react";
import type { VehicleSearchResultDto as VehicleResult } from "@/api/generated/models";

/**
 * Vehicle lookup.
 *
 * This page used to search *customers* and link to their detail page, so someone
 * ringing up with a registration could not be looked up at all — you had to know the
 * customer first. It now searches vehicles directly by plate, VIN or description and
 * links straight to the vehicle.
 */


export default function VehiclesPage() {
  const [search, setSearch] = useState("");

  const { data: results, isLoading, error, mutate: reload } = useApiQuery<VehicleResult[]>(
    search.trim().length >= 2 ? "/api/vehicles/search" : null,
    { q: search.trim() }
  );

  return (
    <>
      <PageHeader title="Vehicles" description="Look up a vehicle by registration, VIN or description" />

      <Card className="mb-6 p-4">
        <div className="relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-surface-400" />
          <input
            type="text"
            placeholder="Search by registration, VIN, make or model…"
            className="w-full pl-9 pr-3 py-2 rounded-lg border border-surface-300 text-sm focus:outline-none focus:ring-2 focus:ring-brand-400"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            autoFocus
          />
        </div>
      </Card>

      {isLoading && <div className="flex justify-center py-10"><Spinner /></div>}

      {!isLoading && error && <ErrorState error={error} onRetry={() => reload()} />}

      {!isLoading && !error && search.trim().length < 2 && (
        <EmptyState
          icon={<Car size={48} />}
          title="Search for a vehicle"
          description="Type at least 2 characters — a registration, VIN, make or model"
        />
      )}

      {!isLoading && !error && results && results.length === 0 && search.trim().length >= 2 && (
        <EmptyState
          icon={<Car size={48} />}
          title="No vehicles found"
          description="Nothing matches that registration or description"
        />
      )}

      {results && results.length > 0 && (
        <div className="space-y-2">
          {results.map((v) => (
            <Link key={v.id} href={`/vehicles/${v.id}`}>
              <Card className="p-4 hover:border-brand-200 transition-colors cursor-pointer">
                <div className="flex items-center gap-3">
                  <Car size={18} className="text-surface-400 shrink-0" />
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-surface-900 truncate">{v.displayName}</p>
                    <p className="text-xs text-surface-500 truncate">
                      {[v.registration, v.customerName].filter(Boolean).join(" · ")}
                    </p>
                  </div>
                </div>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </>
  );
}
