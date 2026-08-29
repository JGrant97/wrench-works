"use client";

import { useFeature } from "@/hooks/use-feature";
import { Button, Card } from "@/components/ui";
import { Lock } from "lucide-react";
import { useRouter } from "next/navigation";

interface FeatureGateProps {
  feature: string;
  featureName: string;
  children: React.ReactNode;
}

export function FeatureGate({ feature, featureName, children }: FeatureGateProps) {
  const enabled = useFeature(feature);
  const router = useRouter();

  if (enabled) return <>{children}</>;

  return (
    <div className="flex items-center justify-center py-20">
      <Card className="max-w-md text-center p-8">
        <div className="mx-auto w-12 h-12 rounded-full bg-surface-100 flex items-center justify-center mb-4">
          <Lock className="w-6 h-6 text-surface-400" />
        </div>
        <h2 className="text-lg font-semibold text-surface-900 font-display">
          {featureName} is not available on your plan
        </h2>
        <p className="text-sm text-surface-500 mt-2">
          Upgrade your subscription to access {featureName.toLowerCase()}.
        </p>
        <Button className="mt-5" onClick={() => router.push("/settings/billing")}>
          View Plans
        </Button>
      </Card>
    </div>
  );
}
