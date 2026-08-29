"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button, Input } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";

export default function VerifyEmailPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [token, setToken] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await fetcher.post("/api/auth/verify-email", { email, token });
      toast.success("Email verified! You can now sign in.");
      router.push("/login");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Verification failed";
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <div className="text-center mb-2">
        <h1 className="text-xl font-semibold text-surface-900 font-display">Verify your email</h1>
        <p className="text-sm text-surface-500 mt-1">
          Enter the verification code we sent to your email
        </p>
      </div>

      <Input
        id="email"
        label="Email"
        type="email"
        required
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        placeholder="you@workshop.com"
      />

      <Input
        id="token"
        label="Verification code"
        required
        value={token}
        onChange={(e) => setToken(e.target.value)}
        placeholder="Paste the code from your email"
      />

      <Button type="submit" loading={loading} className="w-full">
        Verify email
      </Button>
    </form>
  );
}
