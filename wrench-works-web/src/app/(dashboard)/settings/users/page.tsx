"use client";

import { useState } from "react";
import { useApi } from "@/hooks/use-api";
import { Button, Badge, Card, Modal, Input, Select, PageHeader, Spinner } from "@/components/ui";
import { SettingsNav } from "@/components/settings-nav";
import { Plus, UserPlus } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";
import { ErrorState } from "@/components/data-state";

interface BusinessUserDto {
  id: string;
  userId: string;
  name: string;
  email: string;
  status: string;
  roles: string[];
}

interface RoleDto {
  id: string;
  name: string;
}

export default function SettingsUsersPage() {
  const { data: users, isLoading, error } = useApi<BusinessUserDto[]>("/api/users");
  const [showInvite, setShowInvite] = useState(false);

  return (
    <>
      <PageHeader title="Settings" />
      <div className="flex gap-8">
        <SettingsNav />
        <div className="flex-1 max-w-2xl">
          <Card>
            <div className="flex items-center justify-between mb-5">
              <h2 className="font-semibold text-surface-800">Team Members</h2>
              <Button size="sm" onClick={() => setShowInvite(true)}><UserPlus size={14} /> Invite</Button>
            </div>
            {isLoading ? (
              <Spinner />
            ) : error ? (
              <ErrorState error={error} onRetry={() => mutate("/api/users")} compact />
            ) : !users || users.length === 0 ? (
              <p className="text-sm text-surface-400">No team members</p>
            ) : (
              <div className="space-y-2">
                {users.map((u) => (
                  <div key={u.id} className="flex items-center justify-between p-3 rounded-lg bg-surface-50">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-full bg-brand-100 dark:bg-brand-950/40 flex items-center justify-center text-brand-700 dark:text-brand-400 text-sm font-bold">
                        {u.name.charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <p className="text-sm font-medium text-surface-900">{u.name}</p>
                        <p className="text-xs text-surface-500">{u.email}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      {u.roles.map((r) => (
                        <Badge key={r} className="bg-surface-200 text-surface-700">{r}</Badge>
                      ))}
                      <Badge className={u.status === "Active" ? "bg-green-100 text-green-700 dark:bg-green-950/40 dark:text-green-300" : "bg-yellow-100 text-yellow-700 dark:bg-yellow-950/40 dark:text-yellow-300"}>
                        {u.status}
                      </Badge>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </div>
      </div>

      {showInvite && (
        <InviteModal
          onClose={() => setShowInvite(false)}
          onInvited={() => { setShowInvite(false); mutate("/api/users"); }}
        />
      )}
    </>
  );
}

function InviteModal({ onClose, onInvited }: { onClose: () => void; onInvited: () => void }) {
  const [form, setForm] = useState({ email: "", name: "", roleName: "Technician" });
  const [loading, setLoading] = useState(false);

  const roles = [
    { value: "Admin", label: "Admin" },
    { value: "Advisor", label: "Advisor" },
    { value: "Technician", label: "Technician" },
    { value: "Inventory", label: "Inventory" },
    { value: "ReadOnly", label: "Read Only" },
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await fetcher.post("/api/users/invite", form);
      toast.success("Invitation sent");
      onInvited();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to invite");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Invite Team Member">
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="name" label="Name" required value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="Jane Doe" />
        <Input id="email" label="Email" type="email" required value={form.email} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} placeholder="jane@workshop.com" />
        <Select id="role" label="Role" value={form.roleName} onChange={(e) => setForm((f) => ({ ...f, roleName: e.target.value }))} options={roles} />
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Send Invite</Button>
        </div>
      </form>
    </Modal>
  );
}
