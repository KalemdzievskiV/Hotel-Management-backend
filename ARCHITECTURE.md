# 🏗️ System Architecture

## 📐 High-Level Architecture

```
┌────────────────────────────────────────────────────────────────┐
│                     React Frontend (SPA)                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐        │
│  │ Admin Portal │  │ Guest Portal │  │ Staff Portal │        │
│  └──────────────┘  └──────────────┘  └──────────────┘        │
└────────────────────────┬───────────────────────────────────────┘
                         │ HTTPS + JWT
                         │
┌────────────────────────▼───────────────────────────────────────┐
│              ASP.NET Core Web API (.NET 9)                      │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │  Controllers (API Endpoints)                             │ │
│  │  - AuthController  - HotelsController  - RoomsController │ │
│  │  - ReservationsController  - GuestsController            │ │
│  └───────────────────┬──────────────────────────────────────┘ │
│                      │                                          │
│  ┌───────────────────▼──────────────────────────────────────┐ │
│  │  Middleware Layer                                        │ │
│  │  - ExceptionHandling  - JWT Authentication               │ │
│  │  - Validation  - Authorization                           │ │
│  └───────────────────┬──────────────────────────────────────┘ │
│                      │                                          │
│  ┌───────────────────▼──────────────────────────────────────┐ │
│  │  Services (Business Logic)                               │ │
│  │  - HotelService  - RoomService  - ReservationService     │ │
│  │  - AvailabilityService  - PricingService  - EmailService │ │
│  └───────────────────┬──────────────────────────────────────┘ │
│                      │                                          │
│  ┌───────────────────▼──────────────────────────────────────┐ │
│  │  Repositories (Data Access)                              │ │
│  │  - GenericRepository<T>  - Specialized Repositories      │ │
│  └───────────────────┬──────────────────────────────────────┘ │
│                      │                                          │
│  ┌───────────────────▼──────────────────────────────────────┐ │
│  │  Entity Framework Core (ORM)                             │ │
│  │  - DbContext  - Migrations  - Query Translation          │ │
│  └───────────────────┬──────────────────────────────────────┘ │
└────────────────────────┼───────────────────────────────────────┘
                         │
┌────────────────────────▼───────────────────────────────────────┐
│                   SQL Server Database                           │
│  - AspNetUsers/Roles (Identity)  - Hotels  - Rooms             │
│  - Guests  - Reservations  - Payments  - AuditLogs             │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🗂️ Project Structure

```
HotelManagement/
├── 📁 Controllers/              # API Endpoints
│   ├── AuthController.cs
│   ├── CrudController.cs        # Base controller
│   ├── HotelsController.cs
│   ├── RoomsController.cs       # (To be created)
│   └── ReservationsController.cs # (To be created)
│
├── 📁 Services/
│   ├── 📁 Interfaces/
│   │   ├── ICrudService.cs
│   │   ├── ITokenService.cs
│   │   ├── IRoomService.cs      # (To be created)
│   │   └── IReservationService.cs # (To be created)
│   │
│   └── 📁 Implementations/
│       ├── CrudService.cs
│       ├── TokenService.cs
│       ├── RoomService.cs       # (To be created)
│       └── ReservationService.cs # (To be created)
│
├── 📁 Repositories/
│   ├── 📁 Interfaces/
│   │   └── IGenericRepository.cs
│   │
│   └── 📁 Implementations/
│       └── GenericRepository.cs
│
├── 📁 Models/
│   ├── 📁 Entities/             # Database models
│   │   ├── ApplicationUser.cs
│   │   ├── Hotel.cs
│   │   ├── Room.cs              # (To be created)
│   │   ├── Guest.cs             # (To be created)
│   │   └── Reservation.cs       # (To be created)
│   │
│   ├── 📁 DTOs/                 # Data Transfer Objects
│   │   ├── HotelDto.cs
│   │   ├── RoomDto.cs           # (To be created)
│   │   └── ReservationDto.cs    # (To be created)
│   │
│   ├── 📁 Constants/
│   │   └── AppRoles.cs
│   │
│   └── 📁 Enums/                # (To be created)
│       ├── RoomType.cs
│       ├── RoomStatus.cs
│       └── ReservationStatus.cs
│
├── 📁 Data/
│   ├── ApplicationDbContext.cs
│   └── DbSeeder.cs
│
├── 📁 Infrastructure/
│   ├── 📁 Middleware/
│   │   └── ExceptionHandlingMiddleware.cs
│   │
│   ├── 📁 Filters/
│   │   └── ValidationFilter.cs
│   │
│   └── 📁 Mapping/
│       └── AutoMapperProfile.cs
│
├── 📁 Validators/
│   ├── HotelDtoValidator.cs
│   ├── RegisterRequestDtoValidator.cs
│   └── LoginRequestDtoValidator.cs
│
├── 📁 Configurations/
│   └── DependencyInjection.cs
│
├── 📁 Migrations/
│   ├── 20251015191712_InitialCreate.cs
│   ├── 20251018140243_UpdatedModels.cs
│   └── 20251018141520_AddIdentity.cs
│
├── 📁 Tests/
│   ├── 📁 Services/
│   │   ├── TokenServiceTests.cs
│   │   └── CrudServiceTests.cs
│   │
│   ├── 📁 Integration/
│   │   └── HotelsControllerIntegrationTests.cs
│   │
│   └── 📁 Validation/
│       └── DtoValidationTests.cs
│
├── Program.cs                   # Entry point
├── appsettings.json            # Configuration
└── HotelManagement.csproj      # Project file
```

---

## 🔄 Request Flow

### **Example: Creating a Reservation**

```
1. User (Frontend)
   ↓
   POST /api/Reservations
   Body: { "roomId": 1, "checkIn": "2025-06-01", ... }
   Headers: { "Authorization": "Bearer <JWT>" }

2. ASP.NET Core Pipeline
   ↓
   [Middleware] JWT Authentication
   - Validates token
   - Sets User claims
   ↓
   [Middleware] Authorization
   - Checks if user has "Guest" or "Admin" role
   ↓
   [Filter] Validation
   - FluentValidation validates DTO
   - Returns 400 if invalid

3. ReservationsController
   ↓
   [Authorize(Roles = "Admin,Manager,Guest")]
   public async Task<IActionResult> CreateAsync([FromBody] ReservationDto dto)
   {
       var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
       var reservation = await _reservationService.CreateAsync(dto, userId);
       return Created(..., reservation);
   }

4. ReservationService
   ↓
   public async Task<ReservationDto> CreateAsync(ReservationDto dto, string userId)
   {
       // 1. Check room availability
       var isAvailable = await _availabilityService.CheckAvailability(...);
       if (!isAvailable) throw new InvalidOperationException("Room not available");
       
       // 2. Calculate pricing
       var totalAmount = await _pricingService.CalculateTotalAmount(...);
       
       // 3. Create reservation entity
       var reservation = _mapper.Map<Reservation>(dto);
       reservation.CreatedByUserId = userId;
       reservation.TotalAmount = totalAmount;
       reservation.Status = ReservationStatus.Pending;
       
       // 4. Save to database
       await _repository.AddAsync(reservation);
       await _repository.SaveAsync();
       
       // 5. Send confirmation email (async)
       _ = _emailService.SendReservationConfirmationAsync(reservation);
       
       return _mapper.Map<ReservationDto>(reservation);
   }

5. Repository
   ↓
   public async Task AddAsync(Reservation entity)
   {
       await _dbContext.Reservations.AddAsync(entity);
   }
   
   public async Task SaveAsync()
   {
       await _dbContext.SaveChangesAsync();
   }

6. Entity Framework Core
   ↓
   Translates to SQL:
   INSERT INTO Reservations (RoomId, GuestId, CheckInDate, ...)
   VALUES (1, 123, '2025-06-01', ...)

7. SQL Server
   ↓
   Saves data and returns generated ID

8. Response to Frontend
   ↓
   HTTP 201 Created
   {
     "id": 456,
     "roomId": 1,
     "checkInDate": "2025-06-01",
     "status": "Pending",
     ...
   }
```

---

## 🗄️ Database Schema

```sql
-- Identity Tables (ASP.NET Core Identity)
AspNetUsers
  - Id (PK)
  - UserName
  - Email
  - PasswordHash
  - FullName (custom)
  - CreatedAt (custom)

AspNetRoles
  - Id (PK)
  - Name (SuperAdmin, Admin, Manager, Housekeeper, Guest)

AspNetUserRoles (Many-to-Many)
  - UserId (FK → AspNetUsers)
  - RoleId (FK → AspNetRoles)

-- Application Tables
Hotels
  - Id (PK)
  - OwnerId (FK → AspNetUsers) -- Admin who owns this hotel
  - Name
  - Address, City, Country
  - Description
  - Rating
  - CreatedAt

Rooms
  - Id (PK)
  - HotelId (FK → Hotels)
  - RoomNumber
  - Type (enum: Single, Double, Suite, etc.)
  - Capacity
  - PricePerNight
  - Floor
  - Status (enum: Available, Occupied, Maintenance)
  - Description
  - Amenities (JSON)
  - CreatedAt

Guests
  - Id (PK)
  - UserId (nullable FK → AspNetUsers) -- If registered user
  - FirstName
  - LastName
  - Email
  - PhoneNumber
  - IdentificationNumber
  - DateOfBirth
  - Nationality
  - Address
  - SpecialRequests
  - CreatedAt

Reservations
  - Id (PK)
  - HotelId (FK → Hotels)
  - RoomId (FK → Rooms)
  - GuestId (FK → Guests)
  - CreatedByUserId (FK → AspNetUsers) -- Who made the booking
  - CheckInDate
  - CheckOutDate
  - Status (enum: Pending, Confirmed, CheckedIn, CheckedOut, Cancelled)
  - TotalAmount
  - Deposit
  - RemainingAmount
  - PaymentStatus (enum: Pending, Paid, Refunded)
  - PaymentMethod
  - SpecialRequests
  - Notes
  - CreatedAt
  - UpdatedAt
  - CancelledAt

-- Future Tables (Optional)
Payments
  - Id (PK)
  - ReservationId (FK → Reservations)
  - Amount
  - PaymentMethod
  - TransactionId
  - Status
  - CreatedAt

HotelImages
  - Id (PK)
  - HotelId (FK → Hotels)
  - Url
  - IsPrimary
  - DisplayOrder

RoomImages
  - Id (PK)
  - RoomId (FK → Rooms)
  - Url
  - IsPrimary
  - DisplayOrder

AuditLogs
  - Id (PK)
  - UserId (FK → AspNetUsers)
  - Action (Created, Updated, Deleted)
  - EntityType
  - EntityId
  - Changes (JSON)
  - Timestamp

Notifications
  - Id (PK)
  - UserId (FK → AspNetUsers)
  - Type
  - Message
  - IsRead
  - CreatedAt
```

---

## 🔐 Authentication & Authorization Flow

```
1. User Registration
   POST /api/Auth/register
   {
     "fullName": "John Doe",
     "email": "john@example.com",
     "password": "Pass123",
     "role": "Admin"
   }
   ↓
   UserManager.CreateAsync(user, password)
   UserManager.AddToRoleAsync(user, "Admin")
   ↓
   User created in AspNetUsers
   Role assigned in AspNetUserRoles

2. User Login
   POST /api/Auth/login
   {
     "email": "john@example.com",
     "password": "Pass123"
   }
   ↓
   SignInManager.CheckPasswordSignInAsync(user, password)
   UserManager.GetRolesAsync(user)
   ↓
   TokenService.GenerateJwtToken(user, roles)
   ↓
   Returns JWT token:
   {
     "token": "eyJhbGciOiJIUzI1NiIs...",
     "expiresAt": "2025-10-18T18:30:00Z",
     "user": { "id": "...", "fullName": "John Doe", "email": "..." }
   }

3. Authenticated Request
   GET /api/Hotels
   Headers: { "Authorization": "Bearer eyJhbGc..." }
   ↓
   [Middleware] JWT Authentication
   - Validates token signature
   - Checks expiration
   - Decodes claims (email, name, roles)
   - Sets HttpContext.User
   ↓
   [Authorize] attribute checks if user is authenticated
   ↓
   Controller action executes

4. Role-Based Authorization
   POST /api/Hotels
   Headers: { "Authorization": "Bearer ..." }
   ↓
   [Authorize(Roles = "SuperAdmin,Admin,Manager")]
   ↓
   Checks if User.IsInRole("SuperAdmin") OR
          User.IsInRole("Admin") OR
          User.IsInRole("Manager")
   ↓
   If true: Allow
   If false: Return 403 Forbidden
```

---

## 🧩 Design Patterns Used

### **1. Repository Pattern**
```csharp
IGenericRepository<T> → GenericRepository<T>
- Abstracts data access
- Makes testing easier (mock repositories)
- Centralizes database operations
```

### **2. Service Layer Pattern**
```csharp
IHotelService → HotelService
- Encapsulates business logic
- Keeps controllers thin
- Reusable across controllers
```

### **3. DTO Pattern**
```csharp
Entity (Hotel) ↔ DTO (HotelDto) via AutoMapper
- Separates internal model from API contract
- Controls what data is exposed
- Adds validation layer
```

### **4. Dependency Injection**
```csharp
services.AddScoped<IHotelService, HotelService>();
- Loose coupling
- Easy testing (inject mocks)
- Configuration in one place
```

### **5. Middleware Pattern**
```csharp
app.UseExceptionHandling();
app.UseAuthentication();
app.UseAuthorization();
- Cross-cutting concerns
- Request/response pipeline
- Reusable logic
```

### **6. Filter Pattern**
```csharp
ValidationFilter implements IActionFilter
- Pre/post action processing
- Validation, logging, caching
```

---

## 🔄 Entity Relationships

```
ApplicationUser (1) ──owns──> (*) Hotel
Hotel (1) ──has──> (*) Room
Hotel (1) ──has──> (*) Reservation
Room (1) ──has──> (*) Reservation
Guest (1) ──makes──> (*) Reservation
ApplicationUser (1) ──creates──> (*) Reservation
Guest (0..1) ──links to──> (1) ApplicationUser
```

**Translation:**
- One Admin owns many Hotels
- One Hotel has many Rooms
- One Hotel has many Reservations
- One Room has many Reservations
- One Guest makes many Reservations
- One User (Admin/Manager) creates many Reservations
- One Guest optionally links to one User (if they register)

---

## 🚀 Key Technologies

| Technology | Purpose | Version |
|------------|---------|---------|
| .NET | Backend framework | 9.0 |
| ASP.NET Core | Web API | 9.0 |
| Entity Framework Core | ORM | 9.0 |
| SQL Server | Database | 2019+ |
| Identity | Authentication | 9.0 |
| JWT | Token authentication | 8.0 |
| AutoMapper | Object mapping | 13.0 |
| FluentValidation | DTO validation | 11.5 |
| xUnit | Testing | 2.9 |
| Moq | Mocking | 4.20 |
| Swagger | API documentation | 9.0 |

---

## 📊 Scalability Considerations

### **Current Setup (Small-Medium Scale)**
- Single server
- Single database
- In-memory caching (optional)
- Good for: 1000s of concurrent users

### **Future Scaling Options**
- Load balancer + multiple API servers
- Redis cache for session data
- Read replicas for database
- CDN for static content
- Azure/AWS cloud hosting
- Good for: 10,000s+ concurrent users

---

## 🎯 Next Architecture Decisions

As you build Phase 2+, consider:

1. **Caching Strategy**
   - Redis for distributed cache
   - Cache frequently accessed hotels/rooms
   - Cache calendar data

2. **Background Jobs**
   - Hangfire for scheduled tasks
   - Email sending (async)
   - Report generation

3. **Event-Driven Architecture** (Optional)
   - Publish events (ReservationCreated, RoomStatusChanged)
   - Separate microservices can subscribe
   - Better decoupling

4. **API Versioning**
   - /api/v1/hotels
   - Allows breaking changes without disrupting clients

---

**This architecture is proven, scalable, and maintainable!** 🎉

It follows industry best practices and will easily support your hotel management features.
