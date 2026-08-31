"use client";

import { useState } from "react";
import { useApi } from "@/hooks/use-api";
import { Button, Modal, Input } from "@/components/ui";
import { formatCurrency } from "@/lib/utils";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";

export function AddPartModal({ jobId, onClose, onAdded }: { jobId: string; onClose: () => void; onAdded: () => void }) {
  const [search, setSearch] = useState("");
  const [selectedItem, setSelectedItem] = useState<{ id: string; name: string; sellPrice: number; quantityOnHand: number } | null>(null);
  const [quantity, setQuantity] = useState("1");
  const [loading, setLoading] = useState(false);

  const { data: items } = useApi<{ items: { id: string; name: string; sellPrice: number; quantityOnHand: number }[] }>(
    search.length >= 2 ? `/api/inventory/items?search=${encodeURIComponent(search)}` : null
  );

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    setLoading(true);
    try {
      await fetcher.post(`/api/jobs/${jobId}/parts`, {
        inventoryItemId: selectedItem.id,
        quantity: parseFloat(quantity),
        unitPrice: selectedItem.sellPrice,
      });
      toast.success("Part added");
      onAdded();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Add Part">
      <form onSubmit={handleSubmit} className="space-y-4">
        {!selectedItem ? (
          <div>
            <Input id="partSearch" label="Search parts" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search by name or SKU..." />
            {items?.items && items.items.length > 0 && (
              <div className="mt-2 border border-surface-200 rounded-lg max-h-48 overflow-y-auto">
                {items.items.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    className="w-full text-left px-3 py-2 text-sm hover:bg-surface-50 flex justify-between"
                    onClick={() => setSelectedItem(item)}
                  >
                    <span>{item.name}</span>
                    <span className="text-surface-400">{formatCurrency(item.sellPrice)} · {item.quantityOnHand} in stock</span>
                  </button>
                ))}
              </div>
            )}
          </div>
        ) : (
          <>
            <div className="p-3 bg-surface-50 rounded-lg flex justify-between items-center">
              <div>
                <p className="font-medium text-sm">{selectedItem.name}</p>
                <p className="text-xs text-surface-500">{formatCurrency(selectedItem.sellPrice)} each · {selectedItem.quantityOnHand} in stock</p>
              </div>
              <button type="button" onClick={() => setSelectedItem(null)} className="text-xs text-brand-600 hover:underline">Change</button>
            </div>
            <Input id="qty" label="Quantity" type="number" step="1" min="1" required value={quantity} onChange={(e) => setQuantity(e.target.value)} />
          </>
        )}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading} disabled={!selectedItem}>Add Part</Button>
        </div>
      </form>
    </Modal>
  );
}
