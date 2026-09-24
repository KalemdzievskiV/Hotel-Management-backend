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

Demo data and the default SuperAdmin are only seeded in Development.

## How access works

`IHotelAccessService` is the single rule for which hotels a user can work with; every endpoint uses it.

| Role        | Access |
|-------------|--------|
| SuperAdmin  | Every hotel; manages users |
| Admin       | Hotels they own plus the hotel they're assigned to |
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
- **Lifecycle:** Pending → Confirmed → CheckedIn → CheckedOut, or Cancelled / NoShow. Only pending
  reservations without payments can be deleted; everything else is cancelled to keep history.
- Walk-in check-in and express checkout (`WalkInService`) run as single transactions.

Business-rule violations throw `BusinessRuleException` and return 400 with the message;
missing records return 404; anything unexpected is logged and returns 500.

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
