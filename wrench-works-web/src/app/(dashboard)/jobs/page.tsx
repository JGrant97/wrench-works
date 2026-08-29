"use client";

import { useState } from "react";
import Link from "next/link";
import { useApiQuery } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, Badge, Card, PageHeader, Spinner, EmptyState, Input, Select } from "@/components/ui";
import { cn, formatDate, JOB_STATUS_COLORS, formatCurrency } from "@/lib/utils";
import { Plus, Briefcase, Search } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { useRouter } from "next/navigation";

interface Job {
  id: string;
  title: string;
  status: string;
  priority: string;
  customerName: string;
  vehicleDisplay: string;
  scheduledStartUtc: string | null;
  laborTotal: number;
  partsTotal: number;
  createdAtUtc: string;
}

interface JobListResponse {
  items: Job[];
  totalCount: number;
  page: number;
  pageSize: number;
}

const STATUS_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: "Draft", label: "Draft" },
  { value: "Scheduled", label: "Scheduled" },
  { value: "InProgress", label: "In Progress" },
  { value: "WaitingParts", label: "Waiting Parts" },
  { value: "Completed", label: "Completed" },
  { value: "Invoiced", label: "Invoiced" },
  { value: "Closed", label: "Closed" },
];

export default function JobsPage() {
  const canCreate = usePermission("jobs.create");
  const router = useRouter();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);

  const { data, isLoading } = useApiQuery<JobListResponse>("/api/jobs", {
    search: search || undefined,
    status: status || undefined,
    page: String(page),
    pageSize: "20",
  });

  const jobs = data?.items ?? [];
  const totalPages = data ? Math.ceil(data.totalCount / data.pageSize) : 1;

  return (
    <>
      <PageHeader
        title="Jobs"
        description="Manage work orders and track progress"
        actions={
          canCreate ? (
            <Button onClick={() => router.push("/jobs/new")}>
              <Plus size={16} /> New Job
            </Button>
          ) : undefined
        }
      />

      <Card className="mb-6 p-4">
        <div className="flex gap-4">
          <div className="flex-1 relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-surface-400" />
            <input
              type="text"
              placeholder="Search jobs..."
              className="w-full pl-9 pr-3 py-2 rounded-lg border border-surface-300 text-sm focus:outline-none focus:ring-2 focus:ring-brand-400"
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            />
          </div>
          <Select
            options={STATUS_OPTIONS}
            value={status}
            onChange={(e) => { setStatus(e.target.value); setPage(1); }}
            className="w-48"
          />
        </div>
      </Card>

      {isLoading ? (
        <div className="flex justify-center py-20"><Spinner /></div>
      ) : jobs.length === 0 ? (
        <EmptyState
          icon={<Briefcase size={48} />}
          title="No jobs found"
          description={search || status ? "Try adjusting your filters" : "Create your first job to get started"}
        />
      ) : (
        <>
          <div className="space-y-2">
            {jobs.map((job) => (
              <Link key={job.id} href={`/jobs/${job.id}`}>
                <Card className="p-4 hover:border-brand-200 transition-colors cursor-pointer">
                  <div className="flex items-center justify-between">
                    <div className="min-w-0">
                      <div className="flex items-center gap-3">
                        <p className="font-medium text-surface-900 truncate">{job.title}</p>
                        <Badge className={JOB_STATUS_COLORS[job.status] ?? "bg-surface-100"}>{job.status}</Badge>
                      </div>
                      <p className="text-sm text-surface-500 mt-1">
                        {job.customerName} · {job.vehicleDisplay}
                        {job.scheduledStartUtc && ` · ${formatDate(job.scheduledStartUtc)}`}
                      </p>
                    </div>
                    <div className="text-right flex-shrink-0 ml-4">
                      <p className="font-semibold text-surface-900">
                        {formatCurrency(job.laborTotal + job.partsTotal)}
                      </p>
                      <p className="text-xs text-surface-400">{formatDate(job.createdAtUtc)}</p>
                    </div>
                  </div>
                </Card>
              </Link>
            ))}
          </div>

          {totalPages > 1 && (
            <div className="flex justify-center gap-2 mt-6">
              <Button variant="ghost" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
              <span className="flex items-center text-sm text-surface-500">Page {page} of {totalPages}</span>
              <Button variant="ghost" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
            </div>
          )}
        </>
      )}
    </>
  );
}
