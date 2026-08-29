# Wrench Works Web

Next.js frontend for the Wrench Works workshop management platform.

## Architecture

```
Browser → Next.js Route Handlers (/api/*) → .NET Backend API
          (same-origin proxy)               (separate server)
```

**Key design decisions:**

1. **Orval client generation** — The OpenAPI spec from the backend generates a typed Axios client (`src/api/generated/`). This client is used **server-side only** inside Next.js Route Handlers.

2. **Next.js API routes as a proxy** — The browser never talks to the backend directly. All requests go to `/api/*` on the same origin, which forward to the backend. This gives us:
   - httpOnly cookies for JWT storage (XSS-safe)
   - No CORS complexity in production
   - A single origin for the browser

3. **Catch-all proxy** — `src/app/api/[...path]/route.ts` forwards any `/api/*` request that doesn't have a dedicated handler. Auth routes (`/api/auth/*`) have custom handlers that manage cookie sessions.

4. **SWR for data fetching** — Client components use `useApi()` / `useApiQuery()` hooks that call the proxy routes via SWR for caching and revalidation.

## Project Structure

```
src/
├── app/
│   ├── api/                    # Next.js Route Handlers (server-side proxy)
│   │   ├── auth/               # Custom auth routes (login sets cookies)
│   │   │   ├── login/          # POST → backend + set httpOnly cookie
│   │   │   ├── logout/         # POST → clear cookies
│   │   │   ├── register/       # POST → backend
│   │   │   ├── refresh/        # POST → refresh JWT + rotate cookie
│   │   │   ├── verify-email/   # POST → backend
│   │   │   └── me/             # GET → backend /api/users/me
│   │   └── [...path]/          # Catch-all: proxy everything else to backend
│   ├── (auth)/                 # Auth pages (login, register, verify)
│   │   └── layout.tsx          # Centered card layout
│   ├── (dashboard)/            # Authenticated pages
│   │   ├── layout.tsx          # Sidebar + auth guard
│   │   ├── calendar/           # Weekly booking view
│   │   ├── jobs/               # Job list, detail, create
│   │   ├── customers/          # Customer list + detail
│   │   ├── vehicles/           # Vehicle search + detail
│   │   ├── inventory/          # Stock management
│   │   └── settings/           # General, zones, users, billing
│   ├── layout.tsx              # Root layout
│   └── page.tsx                # Redirect based on auth
├── api/
│   └── generated/              # Orval output (committed, typed contract)
├── components/
│   ├── ui/                     # Shared UI components
│   └── settings-nav.tsx        # Settings sidebar nav
├── hooks/
│   ├── use-auth.ts             # Zustand auth store + login/logout
│   ├── use-api.ts              # SWR hooks for data fetching
│   └── use-permission.ts       # Permission check hooks
└── lib/
    ├── api-client.ts           # Orval mutator (server-side, reads cookie)
    ├── fetcher.ts              # Client-side fetch (same-origin /api/*)
    ├── proxy.ts                # Proxy + backendFetch helpers
    ├── session.ts              # Cookie management (httpOnly JWT)
    └── utils.ts                # Formatting, classnames, color maps
```

## Getting Started

### Prerequisites

- [Node.js 22+](https://nodejs.org/)
- Backend API running at `http://localhost:5000` (see wrench-works-api)

### Setup

```bash
# Install dependencies
npm install

# Copy environment file
cp .env.local.example .env.local

# Generate API client from backend OpenAPI spec (backend must be running)
npm run generate-api

# Start development server
npm run dev
```

The app will be available at [http://localhost:3000](http://localhost:3000).

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `API_BASE_URL` | `http://localhost:5000` | Backend API URL (server-side only) |
| `SESSION_SECRET` | — | Cookie encryption secret |

**Note:** There is no `NEXT_PUBLIC_API_URL`. The browser never knows about the backend — all requests go through the Next.js proxy.

## Generating the API Client

The Orval-generated client lives in `src/api/generated/` and is committed to the repo. To regenerate after backend changes:

```bash
# Backend must be running and serving OpenAPI spec
npm run generate-api
```

This reads from `http://localhost:5000/openapi/v1.json` and generates:
- Typed request/response models in `src/api/generated/models/`
- Axios functions split by tag in `src/api/generated/`

The generated functions use the `apiClient` mutator (`src/lib/api-client.ts`) which reads the JWT from the httpOnly cookie and forwards it to the backend.

## Auth Flow

1. User submits login form → client `POST /api/auth/login`
2. Next.js route handler → forwards to backend, receives JWT
3. Route handler → sets `ww_token` (httpOnly) and `ww_user` (readable) cookies
4. Client reads `ww_user` cookie to hydrate Zustand store (user info, permissions)
5. All subsequent API calls include the `ww_token` cookie automatically
6. Route handlers read the cookie and attach `Authorization: Bearer <token>` to backend requests

## Docker

```bash
docker build -t wrench-works-web .
docker run -p 3000:3000 -e API_BASE_URL=http://api:8080 wrench-works-web
```

Or with the backend docker-compose:

```yaml
# In the backend's docker-compose.yml, add:
web:
  build: ../wrench-works-web
  ports:
    - "3000:3000"
  environment:
    API_BASE_URL: http://api:8080
  depends_on:
    - api
```

## Tech Stack

- **Next.js 15** (App Router)
- **React 19**
- **TypeScript** (strict mode)
- **Tailwind CSS** with custom brand palette
- **SWR** for data fetching + caching
- **Zustand** for auth state
- **Orval** for API client generation
- **Radix UI** for accessible primitives (Dialog, Select, etc.)
- **react-hot-toast** for notifications
- **lucide-react** for icons
- **date-fns** for date formatting
