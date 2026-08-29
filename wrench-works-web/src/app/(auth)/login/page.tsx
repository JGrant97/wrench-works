"use client";

import { useState } from "react";
import Link from "next/link";
import { useAuth } from "@/hooks/use-auth";
import { Button, Input } from "@/components/ui";
import toast from "react-hot-toast";

export default function LoginPage() {
  const { login, isLoading } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await login(email, password);
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message : "Login failed";
      toast.error(message);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <div className="text-center mb-2">
        <h1 className="text-xl font-semibold text-surface-900 font-display">
          Welcome back
        </h1>
        <p className="text-sm text-surface-500 mt-1">
          Sign in to your workshop
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
        autoComplete="email"
      />

      <Input
        id="password"
        label="Password"
        type="password"
        required
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        placeholder="Your password"
        autoComplete="current-password"
      />

      <Button type="submit" loading={isLoading} className="w-full">
        Sign in
      </Button>

      <p className="text-center text-sm text-surface-500">
        Don&apos;t have an account?{" "}
        <Link href="/register" className="text-brand-600 hover:text-brand-700 font-medium">
          Register
        </Link>
      </p>
    </form>
  );
}
