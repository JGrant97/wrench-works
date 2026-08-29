"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { Button, Input } from "@/components/ui";
import toast from "react-hot-toast";

export default function RegisterPage() {
  const { register, isLoading } = useAuth();
  const router = useRouter();
  const [form, setForm] = useState({
    businessName: "",
    ownerName: "",
    email: "",
    password: "",
  });

  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await register(form);
      toast.success("Registration successful! Check your email.");
      router.push("/verify-email");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Registration failed";
      toast.error(message);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <div className="text-center mb-2">
        <h1 className="text-xl font-semibold text-surface-900 font-display">
          Create your workshop
        </h1>
        <p className="text-sm text-surface-500 mt-1">Start your 14-day free trial</p>
      </div>

      <Input
        id="businessName"
        label="Workshop name"
        required
        value={form.businessName}
        onChange={update("businessName")}
        placeholder="Smith's Auto Repairs"
      />

      <Input
        id="ownerName"
        label="Your name"
        required
        value={form.ownerName}
        onChange={update("ownerName")}
        placeholder="John Smith"
      />

      <Input
        id="email"
        label="Email"
        type="email"
        required
        value={form.email}
        onChange={update("email")}
        placeholder="john@smithsauto.co.uk"
        autoComplete="email"
      />

      <Input
        id="password"
        label="Password"
        type="password"
        required
        minLength={8}
        value={form.password}
        onChange={update("password")}
        placeholder="Min. 8 characters"
        autoComplete="new-password"
      />

      <Button type="submit" loading={isLoading} className="w-full">
        Create account
      </Button>

      <p className="text-center text-sm text-surface-500">
        Already have an account?{" "}
        <Link href="/login" className="text-brand-600 hover:text-brand-700 font-medium">
          Sign in
        </Link>
      </p>
    </form>
  );
}
