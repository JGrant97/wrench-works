import Link from "next/link";
import { AlertCircle, Briefcase, CalendarClock, Package, TrendingDown, TrendingUp, Users } from "lucide-react";
import { Badge, Card, PageHeader } from "@/components/ui";
import { JOB_STATUS_COLORS, statusLabel } from "@/lib/utils";
import { formatCurrency } from "@/lib/currency";
import { getCurrency } from "@/lib/currency-server";
import { getApiDashboard } from "@/api/generated/dashboard/dashboard";
import type { DashboardDto } from "@/api/generated/models";

/**
 * The opening screen.
 *
 * This is the first page written the way CLAUDE.md rules 3 and 4 describe: a server
 * component fetching through the Orval-generated client rather than a client component
 * calling the proxy. That is only worth doing because GET /api/dashboard declares a
 * response type — against a `void`-returning endpoint the generated client buys nothing.
 *
 * No "use client" anywhere: nothing here is interactive, so nothing needs to ship as JS.
 */

export const dynamic = "force-dynamic";

/**
 * The .NET 10 preview OpenAPI generator emits every numeric DTO field with a string
 * validation pattern, so Orval types them `number | string` rather than `number`.
 * Coerce once at the boundary instead of scattering casts through the render tree.
 * This applies to every page migrated to the generated client, not just this one.
 */
function num(value: number | string | null | undefined): number {
  return typeof value === "number" ? value : Number(value ?? 0);
}

function StatCard({
  label, value, hint, icon,
}: {
  label: string; value: string; hint?: React.ReactNode; icon: React.ReactNode;
}) {
  return (
    <Card>
      <div className="flex items-start justify-between">
        <div className="min-w-0">
          <p className="text-sm text-surface-500">{label}</p>
          <p className="mt-1 text-2xl font-bold text-surface-900 font-display">{value}</p>
          {hint && <div className="mt-1 text-xs text-surface-500">{hint}</div>}
        </div>
        <div className="text-surface-300 shrink-0 ml-3">{icon}</div>
      </div>
    </Card>
  );
}

/** Times render in the browser's timezone, consistent with the rest of the app. */
function timeRange(startUtc: string, endUtc: string) {
  const opts: Intl.DateTimeFormatOptions = { hour: "2-digit", minute: "2-digit" };
  return `${new Date(startUtc).toLocaleTimeString([], opts)} – ${new Date(endUtc).toLocaleTimeString([], opts)}`;
}

function RevenueTrend({ thisMonth, lastMonth }: { thisMonth: number; lastMonth: number }) {
  // No comparison to draw in month one, and dividing by zero would render "Infinity%".
  if (!lastMonth) return <span>No prior month to compare</span>;

  const change = Math.round(((thisMonth - lastMonth) / lastMonth) * 100);
  const up = change >= 0;

  return (
    <span className={up ? "text-green-600 dark:text-green-400" : "text-red-600 dark:text-red-400"}>
      {up ? <TrendingUp size={12} className="inline mr-1" /> : <TrendingDown size={12} className="inline mr-1" />}
      {up ? "+" : ""}{change}% vs last month
    </span>
  );
}

export default async function DashboardPage() {
  // Server component, so no hook: read the same cookie the client reads.
  const currency = await getCurrency();

  let data: DashboardDto;

  try {
    data = await getApiDashboard();
  } catch {
    // Server-rendered, so there is no SWR retry to lean on — the honest thing is to say
    // the load failed rather than render zeroes, which would read as a quiet workshop.
    return (
      <>
        <PageHeader title="Dashboard" />
        <Card>
          <div className="flex items-center gap-3 py-6 text-red-600" role="alert">
            <AlertCircle size={20} />
            <div>
              <p className="text-sm font-medium">Could not load the dashboard</p>
              <p className="text-sm text-surface-500">
                The backend did not respond. Reload the page to try again.
              </p>
            </div>
          </div>
        </Card>
      </>
    );
  }

  const bookings = data.todaysBookings ?? [];
  const activeJobs = data.activeJobs ?? [];
  const lowStock = data.lowStockItems ?? [];
  const byStatus = data.jobsByStatus ?? [];

  return (
    <>
      <PageHeader
        title="Dashboard"
        description={new Date().toLocaleDateString([], {
          weekday: "long", day: "numeric", month: "long", year: "numeric",
        })}
      />

      <div className="grid grid-cols-4 gap-4 mb-6">
        <StatCard
          label="Open jobs"
          value={String(num(data.openJobCount))}
          hint={
            num(data.unscheduledJobCount) > 0
              ? <Link href="/jobs" className="text-brand-600 hover:underline dark:text-brand-400">
                  {data.unscheduledJobCount} not scheduled
                </Link>
              : "All scheduled"
          }
          icon={<Briefcase size={22} />}
        />
        <StatCard
          label="Booked today"
          value={String(bookings.length)}
          hint={<Link href="/calendar" className="text-brand-600 hover:underline dark:text-brand-400">Open calendar</Link>}
          icon={<CalendarClock size={22} />}
        />
        <StatCard
          label="Revenue this month"
          value={formatCurrency(num(data.revenueThisMonth), currency)}
          hint={<RevenueTrend thisMonth={num(data.revenueThisMonth)} lastMonth={num(data.revenueLastMonth)} />}
          icon={<TrendingUp size={22} />}
        />
        <StatCard
          label="Customers"
          value={String(num(data.customerCount))}
          hint={`${num(data.vehicleCount)} vehicles`}
          icon={<Users size={22} />}
        />
      </div>

      <div className="grid grid-cols-2 gap-6">
        <Card>
          <h2 className="font-semibold text-surface-800 mb-4">Today&apos;s schedule</h2>
          {bookings.length === 0 ? (
            <p className="text-sm text-surface-400">Nothing booked in today</p>
          ) : (
            <div className="space-y-2">
              {bookings.map((b) => (
                <div key={b.id} className="flex items-center justify-between p-3 rounded-lg bg-surface-50">
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-surface-900 truncate">{b.title}</p>
                    <p className="text-xs text-surface-500 truncate">
                      {b.customerName}{b.vehicleDisplay ? ` · ${b.vehicleDisplay}` : ""} · {b.zoneName}
                    </p>
                  </div>
                  <div className="text-right shrink-0 ml-3">
                    <p className="text-sm font-medium text-surface-700">
                      {timeRange(b.startUtc, b.endUtc)}
                    </p>
                    {b.jobId && (
                      <Link href={`/jobs/${b.jobId}`} className="text-xs text-brand-600 hover:underline dark:text-brand-400">
                        View job
                      </Link>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>

        <Card>
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-semibold text-surface-800">Active jobs</h2>
            <Link href="/jobs" className="text-sm text-brand-600 hover:underline dark:text-brand-400">All jobs</Link>
          </div>
          {activeJobs.length === 0 ? (
            <p className="text-sm text-surface-400">No jobs in progress</p>
          ) : (
            <div className="space-y-2">
              {activeJobs.map((j) => (
                <Link key={j.id} href={`/jobs/${j.id}`}>
                  <div className="flex items-center justify-between p-3 rounded-lg bg-surface-50 hover:bg-surface-100 transition-colors">
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-surface-900 truncate">{j.title}</p>
                      <p className="text-xs text-surface-500 truncate">
                        {j.customerName}{j.vehicleDisplay ? ` · ${j.vehicleDisplay}` : ""}
                      </p>
                    </div>
                    <Badge className={JOB_STATUS_COLORS[j.status] ?? "bg-surface-100"}>
                      {statusLabel(j.status)}
                    </Badge>
                  </div>
                </Link>
              ))}
            </div>
          )}
        </Card>
      </div>

      <div className="grid grid-cols-2 gap-6 mt-6">
        <Card>
          <h2 className="font-semibold text-surface-800 mb-4">Jobs by status</h2>
          {byStatus.length === 0 ? (
            <p className="text-sm text-surface-400">No jobs yet</p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {byStatus.map((s) => (
                <Link key={s.status} href={`/jobs?status=${s.status}`}>
                  <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-surface-50 hover:bg-surface-100 transition-colors">
                    <Badge className={JOB_STATUS_COLORS[s.status] ?? "bg-surface-100"}>
                      {statusLabel(s.status)}
                    </Badge>
                    <span className="text-sm font-medium text-surface-700">{s.count}</span>
                  </div>
                </Link>
              ))}
            </div>
          )}
        </Card>

        {/* Absent rather than empty when inventory is off the plan — an empty "low stock"
            card would read as "nothing is running low", which is not what it means. */}
        {lowStock.length > 0 && (
          <Card>
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-semibold text-surface-800">Low stock</h2>
              <Link href="/inventory" className="text-sm text-brand-600 hover:underline dark:text-brand-400">Inventory</Link>
            </div>
            <div className="space-y-2">
              {lowStock.map((i) => (
                <div key={i.id} className="flex items-center justify-between p-3 rounded-lg bg-surface-50">
                  <div className="min-w-0 flex items-center gap-2">
                    <Package size={14} className="text-surface-400 shrink-0" />
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-surface-900 truncate">{i.name}</p>
                      {i.sku && <p className="text-xs text-surface-500">{i.sku}</p>}
                    </div>
                  </div>
                  <span className="text-sm font-medium text-amber-600 shrink-0 ml-3">
                    {i.stockOnHand} left · reorder at {i.reorderThreshold}
                  </span>
                </div>
              ))}
            </div>
          </Card>
        )}
      </div>
    </>
  );
}
