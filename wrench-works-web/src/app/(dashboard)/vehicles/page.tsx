"use client";

import { useState } from "react";
import Link from "next/link";
import { useApiQuery } from "@/hooks/use-api";
import { Button, Card, PageHeader, Spinner, EmptyState } from "@/components/ui";
import { Car, Search } from "lucide-react";

/**
 * Vehicles don't have a dedicated list endpoint on the backend
 * (they're accessed via customer). This page searches customers
 * and links to their vehicle detail pages.
 */
export default function VehiclesPage() {
  const [search, setSearch] = useState("");

  const { data: results, isLoading } = useApiQuery<{ id: string; name: string; phone?: string }[]>(
    search.length >= 2 ? "/api/customers/search" : null,
    { q: search }
  );

  return (
    <>
      <PageHeader
        title="Vehicles"
        description="Search customers to find their vehicles"
      />

      <Card className="mb-6 p-4">
        <div className="relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-surface-400" />
          <input
            type="text"
            placeholder="Search customers by name or phone to find vehicles..."
            className="w-full pl-9 pr-3 py-2 rounded-lg border border-surface-300 text-sm focus:outline-none focus:ring-2 focus:ring-brand-400"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </Card>

      {isLoading && <div className="flex justify-center py-10"><Spinner /></div>}

      {!isLoading && search.length < 2 && (
        <EmptyState
          icon={<Car size={48} />}
          title="Search for a customer"
          description="Type at least 2 characters to search"
        />
      )}

      {results && results.length === 0 && (
        <EmptyState icon={<Car size={48} />} title="No customers found" />
      )}

      {results && results.length > 0 && (
        <div className="space-y-2">
          {results.map((c) => (
            <Link key={c.id} href={`/customers/${c.id}`}>
              <Card className="p-4 hover:border-brand-200 transition-colors cursor-pointer">
                <p className="font-medium text-surface-900">{c.name}</p>
                {c.phone && <p className="text-xs text-surface-500 mt-1">{c.phone}</p>}
              </Card>
            </Link>
          ))}
        </div>
      )}
    </>
  );
}
