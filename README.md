# Hotel Management — Backend

REST API for a multi-hotel management system: hotels, rooms, guests, reservations
(overnight and hourly short stays), walk-in check-in, housekeeping, inventory and reports.
The frontend lives in the separate `Hotel-Management-frontend` repository.

**Stack:** ASP.NET Core 9 · EF Core 9 + PostgreSQL · ASP.NET Identity + JWT · AutoMapper · FluentValidation · xUnit

## Running locally

Requirements: .NET 9 SDK and PostgreSQL (e.g. `docker run -e POSTGRES_PASSWORD=hotel1234 -p 5432:5432 postgres:17`).

```bash
dotnet run --launch-profile http        # http://localhost:5213, Swagger at /swagger
```

In Development the app reads `appsettings.Development.json` (local connection string and a
development-only JWT secret), applies migrations on startup and seeds demo data:

| Role        | Email                   | Password          |
|-------------|-------------------------|-------------------|
| SuperAdmin  | superadmin@hotel.com    | SuperAdmin123!    |
| Admin       | admin@hotel.com         | Admin123!         |
| Manager     | manager@hotel.com       | Manager123!       |
| Housekeeper | housekeeper@hotel.com   | Housekeeper123!   |
| Guest       | guest@hotel.com         | Guest123!         |

Point at another database without editing files:
`ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." dotnet run`

## Configuration

`appsettings.json` contains no secrets. Outside Development, set these as environment variables:

| Variable                               | Required | Notes |
|----------------------------------------|----------|-------|
| `ConnectionStrings__DefaultConnection` | yes*     | *or `DATABASE_URL` (`postgres://user:pass@host:port/db`, as provided by Railway) |
| `JwtSettings__Secret`                  | yes      | 32+ random characters; startup fails without it |
| `JwtSettings__ExpiryMinutes`           | no       | Token lifetime, default 60 |
| `Cors__AllowedOrigins__0`              | yes      | Frontend URL (add `__1`, `__2`, ... for more) |
| `SeedAdmin__Email` / `SeedAdmin__Password` | first run | Creates the first SuperAdmin in Production if it doesn't exist |
| `Billing__FakeSigningKey`              | no       | Signs fake checkout links and webhooks; random per start when unset |
| `Billing__RunMaintenance`              | no       | Hourly renew-and-expire job, default `true` |

Demo data and the default SuperAdmin are only seeded in Development.

## How access works

`IHotelAccessService` is the single rule for which hotels a user can work with; every endpoint uses it.

| Role        | Access |
|-------------|--------|
| SuperAdmin  | Every hotel; manages users and subscriptions |
| Admin       | Hotels they own plus the hotel they're assigned to; manages their own staff (`/api/Staff`) and billing |
| Manager     | The hotel they're assigned to (`ApplicationUser.HotelId`) |
| Housekeeper | Their assigned hotel's rooms and housekeeping tasks |
| Guest       | Browses hotels; books and sees only reservations for their own guest profile |

Staff see guests who belong to their hotels (walk-ins created there, or anyone with a reservation there).

Sessions: tokens carry the user's security stamp. Deactivating a user or changing their roles
ends their sessions immediately. Five failed logins lock an account for 15 minutes.

## Reservations and money

- **Availability** (`StayAvailability`): every stay is a time window. Overnight stays entered as
  dates use the hotel's check-in/check-out times (default 14:00 / 11:00), so same-day turnover
  works. After an overnight stay the room needs the hotel's cleaning buffer (`BufferTimeHours`).
  Booking locks the room row so concurrent requests can't double-book.
- **Money:** `TotalAmount = room price − DiscountAmount + ExtraCharges`. Every payment and refund
  is a row in `Payments`; `DepositAmount` is their net total and only changes through the
  payment/refund endpoints.
- **Lifecycle:** Pending → Confirmed → CheckedIn → CheckedOut, or Cancelled / NoShow. Staff bookings
  are confirmed on creation; guests' online bookings wait as Pending. Bookings that haven't started
  and have no payments can be deleted; everything else is cancelled to keep history.
- Walk-in check-in and express checkout (`WalkInService`) run as single transactions.

Business-rule violations throw `BusinessRuleException` and return 400 with the message;
missing records return 404; anything unexpected is logged and returns 500.

## Subscriptions and billing

Each hotel owner (Admin) has one `Subscription`, which covers all their hotels and staff. Owners sign
up at `POST /api/Auth/register-owner` and start a 30-day trial of Pro.

| Plan    | Price (EUR)      | Hotels | Rooms | Staff     | Inventory | Reports      |
|---------|------------------|--------|-------|-----------|-----------|--------------|
| Free    | 0                | 1      | 5     | 1         | view only | last 30 days |
| Starter | 12/mo, 120/yr    | 1      | 20    | 5         | ✓         | ✓            |
| Pro     | 29/mo, 290/yr    | 3      | 60    | unlimited | ✓         | ✓            |

- **The database decides access**, not the payment provider: `Subscription.GetEffectivePlan` looks at
  the status and `AccessUntil`/`GraceUntil`. Plans live in `PlanCatalog`; checks in `EntitlementService`.
  Going over a limit returns **402** with `data.code = "plan_limit"`. SuperAdmins aren't limited.
  Downgrades never delete anything; owners just can't add beyond the plan.
- **Payment providers** implement `IBillingProvider` and report `BillingEvent`s through
  `POST /api/billing/webhooks/{provider}` (signature-checked, each event applied once). Until a real
  provider is chosen, `FakeBillingProvider` stands in: the app's own test checkout page
  (`/dashboard/billing/checkout`) approves or declines, and renewals are "charged" by the hourly
  `SubscriptionMaintenanceService`, which also ends trials, cancelled plans and grace periods (14 days
  after a failed payment). `POST /api/billing/fake/simulate` (SuperAdmin, not in Production) renews or
  fails a payment now, or runs the job.
- **SuperAdmin tools** (`/api/admin/subscriptions`): extend a trial or paid period (card payers'
  next charge moves too, without charging), record a bank transfer, give free access, change plan,
  or grant grace. Each change needs a reason and is kept in `SubscriptionEvent` with who made it;
  owners see the history on their Billing page.

## Project layout

```
Controllers/          HTTP endpoints (thin; access checks + service calls)
Services/             Business logic (Interfaces/ + Implementations/)
Authorization/        Resource-based authorization handlers
Data/                 ApplicationDbContext, DbSeeder
Models/               Entities, DTOs, enums
Infrastructure/       Middleware, exceptions, mapping, query helpers
Migrations/           EF Core migrations
HotelManagement.Tests/  Unit and integration tests
```

## Database migrations

`dotnet-ef` is installed as a local tool (`dotnet tool restore` once):

```bash
dotnet ef migrations add <Name>
dotnet ef database update      # or just start the app; it migrates on startup
```

## Tests

```bash
dotnet test HotelManagement.sln
```

Unit tests use the EF Core in-memory provider; integration tests run the whole API in memory
via `WebApplicationFactory` (see `HotelManagement.Tests/Helpers`).

## Deployment

- **Docker Compose:** copy `.env.example` to `.env`, fill in the values, then `docker compose up -d`.
  The compose file refuses to start without `JWT_SECRET` and `POSTGRES_PASSWORD`.
- **Railway:** builds from the `Dockerfile` and provides `DATABASE_URL`; set `JwtSettings__Secret`,
  `Cors__AllowedOrigins__0` and, for the first deploy, `SeedAdmin__Email`/`SeedAdmin__Password`.
  Health check: `GET /health`.
