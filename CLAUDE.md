# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ASP.NET Core 9.0 Web API for a hotel management platform. C#, EF Core, **PostgreSQL** (Npgsql). The frontend that consumes it lives in the sibling `../hotel-management-frontend/`.

The many `*.md` files here are product/planning docs and progress logs — frequently aspirational or outdated. Trust the code. In particular `ARCHITECTURE.md` says "SQL Server" but the app runs on **PostgreSQL**, and lists files as "to be created" that already exist.

## Commands

```powershell
dotnet build HotelManagement.sln      # build app + tests
dotnet run                            # run API → http://localhost:5213, Swagger at /swagger (Development only)
dotnet test HotelManagement.sln       # run all tests (xUnit)
dotnet test HotelManagement.sln --filter "FullyQualifiedName~ReservationServiceTests"   # single test class
dotnet test HotelManagement.sln --filter "DisplayName~CreateAsync"                      # single test by name

# EF Core migrations
dotnet ef migrations add <Name>
dotnet ef database update             # also applied automatically on startup via context.Database.MigrateAsync()
```

**Tests live in their own project, `HotelManagement.Tests/`** (nested inside this folder, referencing `../HotelManagement.csproj`). `HotelManagement.csproj` excludes `HotelManagement.Tests/**` from compilation, so test files never end up in the app. Because the folder holds both `HotelManagement.sln` and `HotelManagement.csproj`, pass the `.sln` (or `HotelManagement.Tests`) explicitly to `dotnet build`/`dotnet test`. Frameworks: xUnit, Moq, FluentAssertions. Integration tests use `Microsoft.AspNetCore.Mvc.Testing` + `HotelManagement.Tests/Helpers/CustomWebApplicationFactory.cs` (EF Core InMemory provider).

## Architecture

Layered: **Controller → Service → `IGenericRepository<T>` → `ApplicationDbContext` (PostgreSQL)**. AutoMapper maps `Models/Entities` ↔ `Models/DTOs`; FluentValidation validates DTOs.

- **`Program.cs`** — wires the pipeline. On startup it auto-applies migrations and runs `DbSeeder` (seeds roles, a default SuperAdmin, and mock data). The `DATABASE_URL` env var (Railway) overrides the connection string at runtime. Sets `Npgsql.EnableLegacyTimestampBehavior = true` so `DateTime.Kind=Unspecified` can be written to `timestamptz` — be deliberate about `DateTime.Kind` when handling dates.
- **`Configurations/DependencyInjection.cs`** (`AddProjectServices`) — the single place ALL DI is registered: DbContext, the open-generic `IGenericRepository<>`, every service, AutoMapper, Identity, JWT bearer auth, FluentValidation, and authorization policies/handlers. **Add new services, validators, and auth handlers here.** (Note: this file currently registers some services twice — harmless but don't be surprised.)
- **`Repositories/GenericRepository<T>`** — the only repository, registered as `typeof(IGenericRepository<>)`. Services depend on `IGenericRepository<TEntity>`, not per-entity repositories.
- **Generic CRUD base**: `Controllers/CrudController<TDto>` + `Services/CrudService<TEntity,TDto>` + `ICrudService<TDto>` give generic GET/POST/PUT/DELETE with baked-in role authorization. Most feature controllers (e.g. `HotelsController`, `ReservationsController`) are hand-written against a dedicated service instead — **check the existing controller before assuming which pattern applies.**
- **Cross-cutting**: `Infrastructure/Middleware/ExceptionHandlingMiddleware` (global error → JSON, registered first via `app.UseExceptionHandling()`); `Infrastructure/Filters/ValidationFilter` (global filter turning validation failures into consistent 400s).

## Auth & authorization

- JWT bearer auth. Tokens issued by `Services/Implementations/TokenService`; config in `appsettings.json` → `JwtSettings`.
- **Roles** (`Models/Constants/AppRoles.cs`): `SuperAdmin`, `Admin`, `Manager`, `Housekeeper`, `Guest`. Use the `AppRoles` constants in `[Authorize(Roles = ...)]` — not string literals.
- **Resource-based authorization** in `Authorization/` as requirement + handler pairs (e.g. `HotelOwnershipRequirement`/`HotelOwnershipHandler`, `ReservationAccessRequirement`/`ReservationAccessHandler`), surfaced as policies: `CanViewHotel`, `CanManageHotel`, `CanAccessReservation`, plus role shortcuts `AdminOnly`, `ManagerOrAbove`. This enforces ownership scoping — a `Hotel` has an `OwnerId` and an Admin manages only their own hotels. **New handlers must be registered in `DependencyInjection.cs`.**

## Domain

Core: `Hotel` (has `OwnerId`) → `Room` → `Reservation` ↔ `Guest`. Plus `InventoryItem`/`InventoryTransaction` and `HousekeepingTask`. Reservations support overnight and short-stay/hourly bookings (`BookingType` enum). Feature areas each with controller + service: `Reports`, `Inventory`, `Housekeeping`, `WalkIn`, `Users`. Enums in `Models/Enums/` are the source of truth for statuses/types.

## Adding an entity end-to-end (backend)

Entity (`Models/Entities`) → `DbSet` on `ApplicationDbContext` + `dotnet ef migrations add` → DTO(s) in `Models/DTOs` → AutoMapper mapping in `Infrastructure/Mapping/AutoMapperProfile` → FluentValidation validator in `Validators/` → Service + interface in `Services/` → **register in `Configurations/DependencyInjection.cs`** → Controller. Then add the matching `lib/api/` module, hook, and types on the frontend.

## Conventions

- Nullable reference types and implicit usings are enabled (see `HotelManagement.csproj`).
- CORS allowed origins come from `appsettings.json` → `Cors:AllowedOrigins` (overridable via env). The frontend dev origin must be listed there or requests are blocked.
