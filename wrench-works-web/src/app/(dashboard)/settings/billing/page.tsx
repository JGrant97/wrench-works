"use client";

import { useState } from "react";
import { useApi } from "@/hooks/use-api";
import { Button, Badge, Card, PageHeader, Spinner } from "@/components/ui";
import { SettingsNav } from "@/components/settings-nav";
import { formatDate, cn } from "@/lib/utils";
import { CreditCard, ExternalLink, Check } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";

interface Subscription {
  plan: string;
  status: string;
  currentPeriodEnd: string | null;
  userLimit: number;
  zoneLimit: number;
  inventoryEnabled: boolean;
  messagingEnabled: boolean;
}

const PLAN_COLORS: Record<string, string> = {
  Trial: "bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300",
  Starter: "bg-green-100 text-green-700 dark:bg-green-950/40 dark:text-green-300",
  Professional: "bg-purple-100 text-purple-700 dark:bg-purple-950/40 dark:text-purple-300",
  Enterprise: "bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300",
};

const PLANS = [
  {
    name: "Starter", price: "£29/mo",
    features: ["3 users", "2 zones", "Inventory", "Calendar & jobs"],
  },
  {
    name: "Professional", price: "£59/mo", popular: true,
    features: ["10 users", "10 zones", "Inventory", "Messaging (email/SMS)", "Priority support"],
  },
  {
    name: "Enterprise", price: "£99/mo",
    features: ["Unlimited users", "Unlimited zones", "All features", "Dedicated support", "API access"],
  },
];

export default function SettingsBillingPage() {
  const { data: sub, isLoading } = useApi<Subscription>("/api/billing/subscription");
  const [portalLoading, setPortalLoading] = useState(false);
  const [upgradeLoading, setUpgradeLoading] = useState<string | null>(null);

  const openPortal = async () => {
    setPortalLoading(true);
    try {
      const data = await fetcher.post<{ url: string }>("/api/billing/portal");
      if (data.url) window.open(data.url, "_blank");
      else toast.error("Billing portal not available yet — Stripe integration required");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to open billing portal");
    } finally {
      setPortalLoading(false);
    }
  };

  const upgradePlan = async (plan: string) => {
    setUpgradeLoading(plan);
    try {
      const data = await fetcher.post<{ url: string }>("/api/billing/checkout", {
        plan,
        successUrl: `${window.location.origin}/settings/billing?upgraded=true`,
        cancelUrl: `${window.location.origin}/settings/billing`,
      });
      if (data.url) window.location.href = data.url;
      else toast.error("Checkout not available yet — Stripe integration required");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to start checkout");
    } finally {
      setUpgradeLoading(null);
    }
  };

  if (isLoading) return <div className="flex justify-center py-20"><Spinner /></div>;

  return (
    <>
      <PageHeader title="Settings" />
      <div className="flex gap-8">
        <SettingsNav />
        <div className="flex-1 max-w-3xl space-y-6">
          {/* Current plan */}
          <Card>
            <h2 className="font-semibold text-surface-800 mb-5">Current Plan</h2>
            {sub ? (
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <div className="flex items-center gap-2">
                      <p className="text-lg font-bold text-surface-900">{sub.plan}</p>
                      <Badge className={PLAN_COLORS[sub.plan] ?? "bg-surface-100"}>{sub.status}</Badge>
                    </div>
                    {sub.currentPeriodEnd && (
                      <p className="text-sm text-surface-500 mt-1">
                        Renews {formatDate(sub.currentPeriodEnd)}
                      </p>
                    )}
                  </div>
                  <Button variant="secondary" onClick={openPortal} loading={portalLoading}>
                    <ExternalLink size={14} /> Manage Billing
                  </Button>
                </div>

                <div className="grid grid-cols-4 gap-4 pt-4 border-t border-surface-100">
                  <div>
                    <p className="text-xs text-surface-400">Users</p>
                    <p className="text-sm font-medium text-surface-900">{sub.userLimit >= 999 ? "Unlimited" : sub.userLimit}</p>
                  </div>
                  <div>
                    <p className="text-xs text-surface-400">Zones</p>
                    <p className="text-sm font-medium text-surface-900">{sub.zoneLimit >= 999 ? "Unlimited" : sub.zoneLimit}</p>
                  </div>
                  <div>
                    <p className="text-xs text-surface-400">Inventory</p>
                    <p className="text-sm font-medium text-surface-900">{sub.inventoryEnabled ? "Enabled" : "Disabled"}</p>
                  </div>
                  <div>
                    <p className="text-xs text-surface-400">Messaging</p>
                    <p className="text-sm font-medium text-surface-900">{sub.messagingEnabled ? "Enabled" : "Disabled"}</p>
                  </div>
                </div>
              </div>
            ) : (
              <p className="text-sm text-surface-400">No subscription information available</p>
            )}
          </Card>

          {/* Upgrade plans */}
          <div>
            <h2 className="font-semibold text-surface-800 mb-4">Upgrade Plan</h2>
            <div className="grid grid-cols-3 gap-4">
              {PLANS.map((plan) => {
                const isCurrent = sub?.plan === plan.name;
                return (
                  <Card
                    key={plan.name}
                    className={cn(
                      "relative p-5",
                      plan.popular && "ring-2 ring-brand-500",
                      isCurrent && "bg-surface-50"
                    )}
                  >
                    {plan.popular && (
                      <div className="absolute -top-3 left-1/2 -translate-x-1/2 bg-brand-500 text-white text-[10px] font-bold uppercase tracking-wider px-2.5 py-0.5 rounded-full">
                        Popular
                      </div>
                    )}
                    <h3 className="font-bold text-surface-900 text-lg">{plan.name}</h3>
                    <p className="text-2xl font-bold text-surface-900 mt-1">{plan.price}</p>
                    <ul className="mt-4 space-y-2">
                      {plan.features.map((f) => (
                        <li key={f} className="flex items-center gap-2 text-sm text-surface-600">
                          <Check size={14} className="text-green-500 shrink-0" /> {f}
                        </li>
                      ))}
                    </ul>
                    <div className="mt-5">
                      {isCurrent ? (
                        <Button variant="secondary" className="w-full" disabled>
                          Current Plan
                        </Button>
                      ) : (
                        <Button
                          variant={plan.popular ? "primary" : "secondary"}
                          className="w-full"
                          loading={upgradeLoading === plan.name}
                          onClick={() => upgradePlan(plan.name)}
                        >
                          Upgrade to {plan.name}
                        </Button>
                      )}
                    </div>
                  </Card>
                );
              })}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
