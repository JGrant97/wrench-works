"use client";

import { useState } from "react";
import { Archive, ArchiveRestore, Trash2 } from "lucide-react";
import toast from "react-hot-toast";
import { Button, Modal } from "@/components/ui";
import { ApiError, fetcher } from "@/lib/fetcher";

/**
 * Delete / Archive / Restore, for any record that supports them.
 *
 * The server decides which is allowed, not this component: DELETE removes the row only
 * when nothing references it and otherwise returns 409 with a message naming what blocks
 * it ("This customer has 2 vehicles, 3 jobs…"). Rather than pre-empt that with a guess,
 * the refusal is shown as-is and the archive route offered in the same breath — the user
 * finds out why and what to do about it in one step.
 *
 * `resource` is the API segment: "customers", "vehicles", "jobs", "inventory/items".
 */

type Stage = "closed" | "confirm-delete" | "blocked" | "confirm-archive";

export function RecordActions({
  resource,
  id,
  label,
  archived = false,
  allowArchive = true,
  canManage,
  onChanged,
  afterDelete,
}: {
  resource: string;
  id: string;
  /** Singular, lower case — "customer", "vehicle". Used in the confirmation copy. */
  label: string;
  archived?: boolean;
  /**
   * False for records with no archive endpoint. Zones are the case: they model retirement
   * as `IsActive` on the edit form, so offering Archive here would call a route that does
   * not exist and 404.
   */
  allowArchive?: boolean;
  canManage: boolean;
  /** Called after archive or restore, to refresh whatever is on screen. */
  onChanged: () => void;
  /** Called after a successful delete. The record is gone, so usually a navigation. */
  afterDelete: () => void;
}) {
  const [stage, setStage] = useState<Stage>("closed");
  const [blockedReason, setBlockedReason] = useState("");
  const [busy, setBusy] = useState(false);

  if (!canManage) return null;

  const close = () => { setStage("closed"); setBlockedReason(""); };

  const handleDelete = async () => {
    setBusy(true);
    try {
      await fetcher.delete(`/api/${resource}/${id}`);
      toast.success(`${label[0].toUpperCase()}${label.slice(1)} deleted`);
      close();
      afterDelete();
    } catch (err) {
      // 409 means "this has history" — the one case where archiving is the right answer.
      if (err instanceof ApiError && err.status === 409) {
        setBlockedReason(err.message);
        setStage("blocked");
      } else {
        toast.error(err instanceof Error ? err.message : "Could not delete");
      }
    } finally {
      setBusy(false);
    }
  };

  const handleArchive = async (restore: boolean) => {
    setBusy(true);
    try {
      await fetcher.post(`/api/${resource}/${id}/${restore ? "unarchive" : "archive"}`, {});
      toast.success(restore ? "Restored" : "Archived");
      close();
      onChanged();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not update");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      {archived ? (
        <Button variant="secondary" size="sm" onClick={() => handleArchive(true)} loading={busy}>
          <ArchiveRestore size={14} /> Restore
        </Button>
      ) : (
        <>
          {allowArchive && (
            <Button variant="ghost" size="sm" onClick={() => setStage("confirm-archive")}>
              <Archive size={14} /> Archive
            </Button>
          )}
          <Button variant="ghost" size="sm" onClick={() => setStage("confirm-delete")}>
            <Trash2 size={14} /> Delete
          </Button>
        </>
      )}

      <Modal
        open={stage === "confirm-delete"}
        onClose={close}
        title={`Delete this ${label}?`}
      >
        <p className="text-sm text-surface-600">
          This permanently removes the {label}. It only succeeds if nothing else refers to
          it — otherwise you&apos;ll be offered archiving instead.
        </p>
        <div className="flex justify-end gap-2 pt-4">
          <Button variant="ghost" onClick={close}>Cancel</Button>
          <Button variant="danger" onClick={handleDelete} loading={busy}>Delete</Button>
        </div>
      </Modal>

      <Modal
        open={stage === "blocked"}
        onClose={close}
        title={`This ${label} has history`}
      >
        {/* The server's own words: it names the counts that block the delete. */}
        <p className="text-sm text-surface-600">{blockedReason}</p>
        <div className="flex justify-end gap-2 pt-4">
          <Button variant="ghost" onClick={close}>Cancel</Button>
          {allowArchive && (
            <Button onClick={() => handleArchive(false)} loading={busy}>
              <Archive size={14} /> Archive instead
            </Button>
          )}
        </div>
      </Modal>

      <Modal
        open={stage === "confirm-archive"}
        onClose={close}
        title={`Archive this ${label}?`}
      >
        <p className="text-sm text-surface-600">
          It will be hidden from lists and pickers. Existing jobs, bookings and history
          keep working, and you can restore it at any time.
        </p>
        <div className="flex justify-end gap-2 pt-4">
          <Button variant="ghost" onClick={close}>Cancel</Button>
          <Button onClick={() => handleArchive(false)} loading={busy}>Archive</Button>
        </div>
      </Modal>
    </>
  );
}
