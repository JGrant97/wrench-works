import Axios, { AxiosRequestConfig } from "axios";
import { cookies } from "next/headers";

const API_BASE_URL = process.env.API_BASE_URL ?? "http://localhost:5000";

/**
 * Server-side only Axios instance that talks to the .NET backend.
 * Used exclusively inside Next.js Route Handlers via Orval-generated functions.
 * Never imported on the client side.
 */
const serverAxios = Axios.create({
  baseURL: API_BASE_URL,
  headers: { "Content-Type": "application/json" },
  timeout: 30_000,
});

/**
 * Orval custom instance (mutator).
 * Called by every Orval-generated function. Reads the JWT from the
 * httpOnly cookie and forwards it to the backend.
 */
export const apiClient = async <T>(config: AxiosRequestConfig): Promise<T> => {
  const cookieStore = await cookies();
  const token = cookieStore.get("ww_token")?.value;

  const response = await serverAxios({
    ...config,
    headers: {
      ...config.headers,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });

  return response.data;
};

export default apiClient;
