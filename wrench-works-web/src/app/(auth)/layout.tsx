import { Wrench } from "lucide-react";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-surface-50 px-4">
      <div className="w-full max-w-md">
        <div className="flex items-center justify-center gap-3 mb-8">
          <div className="w-10 h-10 rounded-xl bg-brand-500 flex items-center justify-center">
            <Wrench className="w-5 h-5 text-white" />
          </div>
          <span className="text-2xl font-bold font-display text-surface-900">
            Wrench Works
          </span>
        </div>
        <div className="rounded-2xl border border-surface-200 bg-surface-0 p-8 shadow-sm">
          {children}
        </div>
      </div>
    </div>
  );
}
