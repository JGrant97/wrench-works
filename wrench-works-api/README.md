# Wrench Works API

Backend API for the Wrench Works workshop management SaaS platform.

## Tech Stack

- **.NET 10** (Preview) — Minimal APIs
- **Entity Framework Core 10** — PostgreSQL via Npgsql
- **Vertical Slice Architecture** — each feature is self-contained
- **JWT Authentication** with permission-based authorization
- **FluentValidation** for request validation
- **Multi-tenant** — single database with BusinessId query filters

## Project Structure

```
src/
├── WrenchWorks.Api/           # HTTP layer — endpoints, middleware, auth
│   ├── Auth/                  # JWT service, CurrentUserService, permission handler
│   ├── Features/              # Vertical slices (one folder per feature)
│   │   ├── Auth/              # Register, Login, VerifyEmail, RefreshToken
│   │   ├── Business/          # Business settings
│   │   ├── Billing/           # Stripe checkout, webhooks, subscription
│   │   ├── Calendar/          # Bookings with conflict detection
│   │   ├── Customers/         # CRUD + search
│   │   ├── Inventory/         # Items, categories, stock adjustments
│   │   ├── Jobs/              # Full lifecycle, parts, labor lines
│   │   ├── Messaging/         # Email/SMS sending, retry
│   │   ├── Users/             # Invite, list, me endpoint
│   │   ├── Vehicles/          # CRUD + service history
│   │   └── Zones/             # Workshop bays CRUD
│   └── Middleware/            # Global error handling
├── WrenchWorks.Domain/        # Entities (no dependencies)
│   └── Entities/
└── WrenchWorks.Infrastructure/# DbContext, EF configs, external services
    ├── Persistence/
    ├── Services/
    └── Stripe/
tests/
└── WrenchWorks.Tests/         # Integration tests with Testcontainers
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Preview)
- [Docker](https://docs.docker.com/get-docker/) (for PostgreSQL)

### Option 1 — Docker Compose (recommended)

```bash
docker compose up --build
```

This starts PostgreSQL and the API. The API will be available at `http://localhost:5000`.

Scalar docs: [http://localhost:5000/scalar/v1](http://localhost:5000/scalar/v1)

### Option 2 — Local Development

1. Start PostgreSQL:

```bash
docker compose up postgres -d
```

2. Run the API:

```bash
cd src/WrenchWorks.Api
dotnet run
```

The API auto-applies EF Core migrations and seeds permissions on startup.

### Running Tests

```bash
dotnet test
```

Tests use [Testcontainers](https://testcontainers.com/) to spin up a real PostgreSQL instance — Docker must be running.

## EF Core Migrations

Generate a new migration after changing entities:

```bash
cd src/WrenchWorks.Api
dotnet ef migrations add <MigrationName> -p ../WrenchWorks.Infrastructure -s .
```

Apply manually (also happens on app start):

```bash
dotnet ef database update -p ../WrenchWorks.Infrastructure -s .
```

## API Overview

### Auth (anonymous)
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/register` | Register business + owner |
| POST | `/api/auth/login` | Login, returns JWT |
| POST | `/api/auth/verify-email` | Verify email token |
| POST | `/api/auth/refresh` | Refresh JWT (authenticated) |

### Business
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/business` | authenticated |
| PUT | `/api/business` | `settings.manage` |

### Calendar
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/calendar/bookings?from=&to=` | `calendar.view` |
| POST | `/api/calendar/bookings` | `calendar.edit` |
| PUT | `/api/calendar/bookings/{id}/move` | `calendar.edit` |
| DELETE | `/api/calendar/bookings/{id}` | `calendar.edit` |

### Jobs
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/jobs` | `jobs.view` |
| GET | `/api/jobs/{id}` | `jobs.view` |
| POST | `/api/jobs` | `jobs.create` |
| PATCH | `/api/jobs/{id}/status` | `jobs.edit` |
| POST | `/api/jobs/{id}/parts` | `jobs.edit` |
| POST | `/api/jobs/{id}/labor` | `jobs.edit` |

### Customers
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/customers` | `customers.view` |
| GET | `/api/customers/{id}` | `customers.view` |
| POST | `/api/customers` | `customers.manage` |
| PUT | `/api/customers/{id}` | `customers.manage` |
| GET | `/api/customers/search?q=` | `customers.view` |

### Vehicles
| Method | Route | Permission |
|--------|-------|------------|
| POST | `/api/vehicles` | `vehicles.manage` |
| GET | `/api/vehicles/{id}` | `vehicles.view` |
| GET | `/api/vehicles/{id}/history` | `vehicles.view` |

### Inventory
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/inventory/categories` | `inventory.view` |
| POST | `/api/inventory/categories` | `inventory.manage` |
| GET | `/api/inventory/items` | `inventory.view` |
| POST | `/api/inventory/items` | `inventory.manage` |
| POST | `/api/inventory/items/{id}/adjust` | `inventory.manage` |

### Users
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/users` | `users.manage` |
| POST | `/api/users/invite` | `users.manage` |
| GET | `/api/users/me` | authenticated |

### Messaging
| Method | Route | Permission |
|--------|-------|------------|
| POST | `/api/messaging/send` | `messaging.send` |
| GET | `/api/messaging` | `messaging.view` |
| POST | `/api/messaging/{id}/retry` | `messaging.send` |

### Billing
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/billing/subscription` | authenticated |
| POST | `/api/billing/checkout` | `billing.manage` |
| POST | `/api/billing/portal` | `billing.manage` |
| POST | `/api/billing/webhook` | anonymous |

## Default Roles & Permissions

On registration, 5 system roles are seeded for the business:

- **Admin** — all permissions
- **Advisor** — calendar, jobs, customers, vehicles, messaging
- **Technician** — calendar (view), jobs (view/edit), inventory (view), vehicles/customers (view)
- **Inventory** — inventory management, jobs (view)
- **ReadOnly** — view-only across all modules

## Multi-Tenancy

All business-scoped entities inherit `BusinessScopedEntity` and are automatically filtered by the current user's `business_id` JWT claim via EF Core global query filters. This ensures complete data isolation between businesses.

## Configuration

Key settings in `appsettings.json`:

- `ConnectionStrings:DefaultConnection` — PostgreSQL connection string
- `Jwt:Key` — signing key (min 32 chars, change in production!)
- `Jwt:Issuer` / `Jwt:Audience` — token validation
- `Stripe:SecretKey` / `Stripe:WebhookSecret` — Stripe integration (stub for now)
- `Cors:Origins` — allowed frontend origins

## TODO

- [ ] Create initial EF Core migration
- [ ] Implement real Stripe webhook handling
- [ ] Add real email/SMS providers (SendGrid, Twilio)
- [ ] Add rate limiting middleware
- [ ] Add request logging middleware
- [ ] Add password reset flow
