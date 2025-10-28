# 🏨 Hotel Management System - Development Roadmap

## 📊 Current Status: Foundation Phase Complete ✅

### ✅ **Completed:**
- ASP.NET Core Web API setup
- JWT Authentication & Authorization
- Identity with 5 roles (SuperAdmin, Admin, Manager, Housekeeper, Guest)
- FluentValidation for DTOs
- Global error handling middleware
- AutoMapper configuration
- Generic Repository pattern
- CRUD service implementation
- Swagger with JWT support
- Basic Hotel entity & endpoints
- Unit & Integration tests

---

## 🎯 **System Architecture Overview**

```
┌─────────────────────────────────────────────────────────────┐
│                     React Frontend (Future)                  │
│  - Admin Dashboard  - Booking Interface  - Calendar View    │
└─────────────────────────┬───────────────────────────────────┘
                          │ REST API (JWT)
┌─────────────────────────▼───────────────────────────────────┐
│                    ASP.NET Core Web API                      │
│  Controllers → Services → Repositories → EF Core → SQL DB   │
└──────────────────────────────────────────────────────────────┘
```

### **Key Entities:**
```
ApplicationUser (Admin/Manager/Staff)
    ↓ owns
Hotel
    ↓ has many
Room
    ↓ has many
Reservation
    ↓ has one
Guest (walk-in or registered)
```

---

## 📅 **Phase-Based Development Plan**

---

## **PHASE 1: Core Domain Entities** 🏗️
**Duration:** 1-2 weeks  
**Priority:** CRITICAL  
**Goal:** Build the foundational data model

### **1.1 Room Entity & Management**
- [ ] Create `Room` entity
  ```csharp
  - Id, HotelId, RoomNumber, RoomType, Floor
  - Capacity, PricePerNight, Status
  - Amenities (JSON or separate table)
  - Images, Description
  ```
- [ ] Create `RoomType` enum (Single, Double, Suite, Deluxe, etc.)
- [ ] Create `RoomStatus` enum (Available, Occupied, Maintenance, OutOfService)
- [ ] Create `RoomDto` and mapping
- [ ] Create `RoomService` and `IRoomService`
- [ ] Create `RoomsController` with CRUD operations
- [ ] Add validation (room number unique per hotel)
- [ ] Create database migration
- [ ] Write unit tests for RoomService
- [ ] Write integration tests for RoomsController

### **1.2 Guest Entity (Separate from Users)**
- [ ] Create `Guest` entity
  ```csharp
  - Id, FirstName, LastName, Email, Phone
  - IdentificationNumber, DateOfBirth, Nationality
  - Address, City, Country, PostalCode
  - UserId (nullable FK to ApplicationUser)
  - SpecialRequests
  ```
- [ ] Create `GuestDto` and mapping
- [ ] Create `GuestService` and `IGuestService`
- [ ] Create `GuestsController` (Admin/Manager only)
- [ ] Add validation
- [ ] Create database migration
- [ ] Write tests

### **1.3 Reservation Entity**
- [ ] Create `Reservation` entity
  ```csharp
  - Id, HotelId, RoomId, GuestId
  - CreatedByUserId (who made booking)
  - CheckInDate, CheckOutDate
  - Status (Pending, Confirmed, CheckedIn, CheckedOut, Cancelled)
  - TotalAmount, Deposit, RemainingAmount
  - PaymentStatus, PaymentMethod
  - SpecialRequests, Notes
  - CreatedAt, UpdatedAt, CancelledAt
  ```
- [ ] Create `ReservationStatus` enum
- [ ] Create `PaymentStatus` enum
- [ ] Create `ReservationDto` and mapping
- [ ] Create `ReservationService` with business logic
- [ ] Create `ReservationsController`
- [ ] Add validation (check room availability, date conflicts)
- [ ] Create database migration
- [ ] Write tests

### **1.4 Admin-Hotel Ownership**
- [ ] Add `OwnerId` (FK to ApplicationUser) to `Hotel` entity
- [ ] Update HotelDto to include owner info
- [ ] Add filtering: Admins see only their hotels
- [ ] SuperAdmin sees all hotels
- [ ] Update HotelService to enforce ownership
- [ ] Update migration
- [ ] Write tests for ownership logic

---

## **PHASE 2: Business Logic & Validation** 🧠
**Duration:** 2-3 weeks  
**Priority:** HIGH  
**Goal:** Implement core booking logic and constraints

### **2.1 Availability Checking**
- [ ] Create `AvailabilityService`
  ```csharp
  - CheckRoomAvailability(roomId, checkIn, checkOut)
  - GetAvailableRooms(hotelId, checkIn, checkOut, roomType)
  - GetAvailabilityCalendar(roomId, month, year)
  ```
- [ ] Handle overlapping reservations
- [ ] Consider check-out day availability (same-day check-in)
- [ ] Add buffer time for cleaning (optional)
- [ ] Write comprehensive tests

### **2.2 Pricing & Payment Logic**
- [ ] Create `PricingService`
  ```csharp
  - CalculateTotalAmount(roomId, checkIn, checkOut)
  - ApplyDiscounts(reservationId, discountCode)
  - CalculateDeposit(totalAmount)
  ```
- [ ] Support seasonal pricing (optional)
- [ ] Support weekend/weekday pricing (optional)
- [ ] Create `Payment` entity (if needed)
- [ ] Add payment tracking
- [ ] Write tests

### **2.3 Reservation Workflow**
- [ ] Create reservation state machine
  ```
  Pending → Confirmed → CheckedIn → CheckedOut
                ↓
            Cancelled
  ```
- [ ] Add `ConfirmReservation()` method
- [ ] Add `CheckIn()` method (change room status)
- [ ] Add `CheckOut()` method (calculate final bill)
- [ ] Add `CancelReservation()` method (refund logic)
- [ ] Send email notifications (optional for now)
- [ ] Write tests for each state transition

### **2.4 Room Management**
- [ ] Add `UpdateRoomStatus()` method
- [ ] Add `MarkRoomForMaintenance()` method
- [ ] Add `AssignHousekeeper()` method (future)
- [ ] Create `RoomHistory` entity for tracking (optional)
- [ ] Write tests

---

## **PHASE 3: Advanced Querying & Filtering** 🔍
**Duration:** 1-2 weeks  
**Priority:** HIGH  
**Goal:** Implement complex queries for different views

### **3.1 Search & Filter Services**
- [ ] Create `SearchService` or extend existing services
- [ ] **Hotels:**
  - Filter by city, country, rating, price range
  - Search by name, description
  - Sort by price, rating, name
- [ ] **Rooms:**
  - Filter by type, capacity, price range, availability
  - Filter by amenities
  - Sort by price, capacity
- [ ] **Reservations:**
  - Filter by status, date range, hotel, guest
  - Filter by created by (admin/manager)
  - Search by guest name, email, reservation ID
  - Sort by date, status, amount
- [ ] **Guests:**
  - Search by name, email, phone
  - Filter by nationality
  - View guest history

### **3.2 Create Query DTOs**
- [ ] `SearchHotelsQuery` with pagination
- [ ] `SearchRoomsQuery` with filters
- [ ] `SearchReservationsQuery` with filters
- [ ] Use specification pattern (optional for complex queries)
- [ ] Add pagination helper classes
- [ ] Write tests

### **3.3 Dashboard Statistics**
- [ ] Create `DashboardService`
- [ ] **Admin Dashboard:**
  - Total hotels, rooms, reservations
  - Revenue this month/year
  - Occupancy rate
  - Upcoming check-ins/check-outs
  - Recent reservations
- [ ] **Hotel-specific Dashboard:**
  - Hotel occupancy rate
  - Revenue by date range
  - Room performance
  - Top guests
- [ ] Create `DashboardController`
- [ ] Write tests

---

## **PHASE 4: Calendar & Scheduling** 📅
**Duration:** 1-2 weeks  
**Priority:** MEDIUM  
**Goal:** Implement calendar views for room reservations

### **4.1 Calendar API**
- [ ] Create `CalendarController`
- [ ] **Endpoints:**
  ```csharp
  GET /api/calendar/hotel/{hotelId}?month=5&year=2025
  GET /api/calendar/room/{roomId}?month=5&year=2025
  ```
- [ ] Return calendar data structure:
  ```json
  {
    "month": 5,
    "year": 2025,
    "rooms": [
      {
        "roomId": 1,
        "roomNumber": "101",
        "reservations": [
          { "checkIn": "2025-05-10", "checkOut": "2025-05-15", "guest": "..." }
        ]
      }
    ]
  }
  ```
- [ ] Support different views (month, week, day)
- [ ] Write tests

### **4.2 Conflict Detection**
- [ ] Visual indication of overlapping bookings
- [ ] Highlight rooms under maintenance
- [ ] Show room status in calendar
- [ ] Write tests

---

## **PHASE 5: Role-Based Views & Permissions** 🔐
**Duration:** 1 week  
**Priority:** HIGH  
**Goal:** Implement fine-grained authorization

### **5.1 Policy-Based Authorization**
- [ ] Create custom authorization policies
  ```csharp
  - "CanManageHotels" (SuperAdmin, Admin owner)
  - "CanViewAllReservations" (SuperAdmin, Admin, Manager)
  - "CanModifyReservation" (owner or creator)
  - "CanDeleteReservation" (SuperAdmin, Admin)
  ```
- [ ] Configure in `Program.cs`
- [ ] Replace simple [Authorize(Roles)] with policies
- [ ] Write tests

### **5.2 Data Filtering by Role**
- [ ] Admin sees only their hotels/rooms/reservations
- [ ] Manager sees hotels they're assigned to
- [ ] Guest sees only their own reservations
- [ ] SuperAdmin sees everything
- [ ] Implement in services layer
- [ ] Write tests

### **5.3 Authorization Handlers**
- [ ] Create `HotelOwnershipRequirement` and `HotelOwnershipHandler`
- [ ] Create `ReservationOwnershipRequirement` and handler
- [ ] Use in controllers:
  ```csharp
  [Authorize(Policy = "CanManageHotel")]
  ```
- [ ] Write tests

---

## **PHASE 6: Notifications & Communication** 📧
**Duration:** 1-2 weeks  
**Priority:** MEDIUM  
**Goal:** Implement email/SMS notifications

### **6.1 Email Service**
- [ ] Create `IEmailService` interface
- [ ] Implement with SendGrid or SMTP
- [ ] Create email templates:
  - Reservation confirmation
  - Check-in reminder (1 day before)
  - Cancellation confirmation
  - Payment receipt
- [ ] Add to configuration (appsettings.json)
- [ ] Make async/background processing
- [ ] Write tests (with mocks)

### **6.2 Notification System**
- [ ] Create `Notification` entity
  ```csharp
  - Id, UserId, Type, Message, IsRead, CreatedAt
  ```
- [ ] Create `NotificationService`
- [ ] Create `NotificationsController`
- [ ] Store in-app notifications
- [ ] Add real-time with SignalR (optional)
- [ ] Write tests

### **6.3 Audit Logging**
- [ ] Create `AuditLog` entity
  ```csharp
  - Id, UserId, Action, EntityType, EntityId, Changes, Timestamp
  ```
- [ ] Log important actions:
  - Reservation created/modified/cancelled
  - Room status changes
  - User role changes
- [ ] Create `AuditLogService`
- [ ] Add to critical operations
- [ ] Create admin view for audit logs

---

## **PHASE 7: File Management & Media** 📸
**Duration:** 1 week  
**Priority:** LOW-MEDIUM  
**Goal:** Handle hotel/room images

### **7.1 File Upload**
- [ ] Create `FileService` for Azure Blob Storage or local storage
- [ ] Create `HotelImage` entity
  ```csharp
  - Id, HotelId, Url, IsPrimary, DisplayOrder
  ```
- [ ] Create `RoomImage` entity
- [ ] Create upload endpoint:
  ```csharp
  POST /api/hotels/{id}/images
  POST /api/rooms/{id}/images
  ```
- [ ] Validate file types, size
- [ ] Generate thumbnails (optional)
- [ ] Write tests

### **7.2 Image Management**
- [ ] Set primary image
- [ ] Reorder images
- [ ] Delete images (with file cleanup)
- [ ] Serve images via CDN (optional)

---

## **PHASE 8: Reporting & Analytics** 📊
**Duration:** 1-2 weeks  
**Priority:** LOW  
**Goal:** Generate business reports

### **8.1 Report Service**
- [ ] Create `ReportService`
- [ ] **Revenue Reports:**
  - Daily/Weekly/Monthly revenue
  - Revenue by hotel
  - Revenue by room type
- [ ] **Occupancy Reports:**
  - Occupancy rate by date range
  - Peak seasons identification
- [ ] **Guest Reports:**
  - Top guests by bookings
  - Guest demographics
- [ ] Export to PDF/Excel (optional)

### **8.2 Report Endpoints**
- [ ] Create `ReportsController`
- [ ] GET /api/reports/revenue?from=...&to=...
- [ ] GET /api/reports/occupancy?hotelId=...
- [ ] GET /api/reports/guests
- [ ] Cache reports (optional)
- [ ] Write tests

---

## **PHASE 9: Frontend Integration** 💻
**Duration:** 4-6 weeks  
**Priority:** HIGH  
**Goal:** Build React frontend

### **9.1 Setup & Architecture**
- [ ] Initialize React + TypeScript project
- [ ] Setup React Router v6
- [ ] Setup Redux Toolkit or Zustand for state
- [ ] Setup TanStack Query (React Query) for API calls
- [ ] Setup Axios with interceptors (JWT)
- [ ] Setup TailwindCSS + shadcn/ui
- [ ] Setup ESLint + Prettier

### **9.2 Authentication & Layout**
- [ ] Create login/register pages
- [ ] Create PrivateRoute component
- [ ] Create AppLayout with sidebar
- [ ] Role-based navigation menu
- [ ] User profile dropdown
- [ ] Logout functionality

### **9.3 Admin Dashboard**
- [ ] Dashboard page with statistics cards
- [ ] Charts (ApexCharts or Recharts)
- [ ] Recent activity feed
- [ ] Quick actions

### **9.4 Hotel Management (Admin)**
- [ ] Hotels list with search/filter
- [ ] Create/Edit hotel form
- [ ] Hotel detail view
- [ ] Room management within hotel
- [ ] Image upload component

### **9.5 Room Management**
- [ ] Rooms list by hotel
- [ ] Create/Edit room form
- [ ] Room types & amenities
- [ ] Status management

### **9.6 Reservation Management**
- [ ] Reservations list with filters
- [ ] Create reservation (admin/guest)
- [ ] Reservation detail view
- [ ] Check-in/Check-out actions
- [ ] Cancel reservation
- [ ] Payment tracking

### **9.7 Calendar View**
- [ ] Integrate FullCalendar or react-big-calendar
- [ ] Monthly view by hotel
- [ ] Room-specific view
- [ ] Click to create reservation
- [ ] Drag-and-drop (optional)

### **9.8 Guest Interface**
- [ ] Browse hotels (public)
- [ ] Search & filter hotels
- [ ] Hotel detail page
- [ ] Room selection
- [ ] Booking form
- [ ] My reservations page
- [ ] Profile management

### **9.9 Staff Views (Housekeeper)**
- [ ] Task list (rooms to clean)
- [ ] Mark room as cleaned
- [ ] Report maintenance issues

---

## **PHASE 10: Performance & Optimization** ⚡
**Duration:** 1-2 weeks  
**Priority:** MEDIUM  
**Goal:** Optimize for scale

### **10.1 Database Optimization**
- [ ] Add database indexes:
  - `Reservations(CheckInDate, CheckOutDate)`
  - `Reservations(Status)`
  - `Rooms(HotelId, Status)`
  - `Hotels(OwnerId)`
- [ ] Analyze slow queries
- [ ] Add composite indexes where needed
- [ ] Implement database connection pooling

### **10.2 Caching**
- [ ] Add Redis or in-memory caching
- [ ] Cache frequently accessed data:
  - Hotel list
  - Room availability
  - Dashboard statistics
- [ ] Implement cache invalidation strategy
- [ ] Cache configuration in appsettings.json

### **10.3 API Optimization**
- [ ] Implement pagination for all list endpoints
- [ ] Add ETag support for conditional requests
- [ ] Implement response compression (gzip)
- [ ] Add API rate limiting
- [ ] Optimize AutoMapper mappings

### **10.4 Background Jobs**
- [ ] Setup Hangfire or Quartz.NET
- [ ] Create background jobs:
  - Send reminder emails (daily)
  - Update expired reservations (hourly)
  - Generate reports (nightly)
  - Clean up old notifications
- [ ] Add job dashboard (Hangfire UI)

---

## **PHASE 11: DevOps & Deployment** 🚀
**Duration:** 1 week  
**Priority:** MEDIUM  
**Goal:** Setup CI/CD and deployment

### **11.1 Docker**
- [ ] Create Dockerfile for API
- [ ] Create Dockerfile for React app
- [ ] Create docker-compose.yml (API + DB + Redis)
- [ ] Test locally

### **11.2 CI/CD Pipeline**
- [ ] Setup GitHub Actions or Azure DevOps
- [ ] Pipeline stages:
  - Build
  - Run tests
  - Build Docker images
  - Deploy to staging
  - Deploy to production (manual approval)
- [ ] Environment variables management
- [ ] Database migration automation

### **11.3 Deployment**
- [ ] Deploy API to Azure App Service or AWS
- [ ] Deploy frontend to Vercel/Netlify
- [ ] Setup Azure SQL Database or AWS RDS
- [ ] Setup Redis Cache
- [ ] Configure Azure Blob Storage for images
- [ ] Setup Application Insights for monitoring
- [ ] Configure custom domain & SSL

---

## **PHASE 12: Testing & Quality Assurance** 🧪
**Duration:** Ongoing + 1 week focused effort  
**Priority:** HIGH  
**Goal:** Ensure system reliability

### **12.1 Backend Testing**
- [ ] Unit tests for all services (target: 80%+ coverage)
- [ ] Integration tests for all controllers
- [ ] Repository tests with in-memory database
- [ ] Validation tests for all DTOs
- [ ] Test authorization policies
- [ ] Test edge cases and error handling

### **12.2 Frontend Testing**
- [ ] Unit tests for utility functions
- [ ] Component tests with React Testing Library
- [ ] Integration tests with MSW (Mock Service Worker)
- [ ] E2E tests with Playwright or Cypress:
  - User registration & login
  - Complete booking flow
  - Admin hotel management
  - Calendar interactions

### **12.3 Performance Testing**
- [ ] Load testing with k6 or JMeter
- [ ] Test concurrent bookings
- [ ] Test database under load
- [ ] Identify bottlenecks

### **12.4 Security Testing**
- [ ] Run OWASP ZAP scan
- [ ] Test JWT token security
- [ ] Test SQL injection prevention
- [ ] Test XSS prevention
- [ ] Test CSRF protection
- [ ] Test rate limiting

---

## **PHASE 13: Documentation & Handoff** 📚
**Duration:** 1 week  
**Priority:** MEDIUM  
**Goal:** Create comprehensive documentation

### **13.1 API Documentation**
- [ ] Enhance Swagger descriptions
- [ ] Add code examples for each endpoint
- [ ] Document error codes
- [ ] Create Postman collection
- [ ] API versioning guide

### **13.2 Developer Documentation**
- [ ] Architecture overview
- [ ] Setup guide (local development)
- [ ] Database schema documentation
- [ ] Coding standards & conventions
- [ ] Git workflow guide
- [ ] Deployment guide

### **13.3 User Documentation**
- [ ] Admin user guide
- [ ] Guest booking guide
- [ ] FAQ section
- [ ] Video tutorials (optional)

---

## **OPTIONAL FUTURE ENHANCEMENTS** 🌟

### **Multi-language Support**
- [ ] Add localization (i18next for React, resources for .NET)
- [ ] Support English, Spanish, French, etc.

### **Payment Integration**
- [ ] Integrate Stripe or PayPal
- [ ] Secure payment processing
- [ ] Refund handling

### **Reviews & Ratings**
- [ ] Allow guests to review hotels
- [ ] Rating system (1-5 stars)
- [ ] Display average rating

### **Loyalty Program**
- [ ] Points system
- [ ] Membership tiers (Bronze, Silver, Gold)
- [ ] Discounts for members

### **Mobile App**
- [ ] React Native or .NET MAUI
- [ ] Push notifications
- [ ] Mobile check-in

### **Advanced Features**
- [ ] Dynamic pricing based on demand
- [ ] Package deals (room + breakfast)
- [ ] Group bookings
- [ ] Corporate accounts
- [ ] Integration with booking.com, Expedia (optional)

---

## 📊 **Estimated Timeline**

| Phase | Duration | Dependencies |
|-------|----------|--------------|
| Phase 1: Core Entities | 1-2 weeks | Foundation (Done) |
| Phase 2: Business Logic | 2-3 weeks | Phase 1 |
| Phase 3: Advanced Queries | 1-2 weeks | Phase 1 |
| Phase 4: Calendar | 1-2 weeks | Phase 1, 2 |
| Phase 5: Role-Based Views | 1 week | Phase 1, 2 |
| Phase 6: Notifications | 1-2 weeks | Phase 2 |
| Phase 7: File Management | 1 week | Phase 1 |
| Phase 8: Reporting | 1-2 weeks | Phase 1, 2, 3 |
| Phase 9: Frontend | 4-6 weeks | All backend phases |
| Phase 10: Optimization | 1-2 weeks | All backend phases |
| Phase 11: DevOps | 1 week | Phase 9 |
| Phase 12: Testing | Ongoing + 1 week | All phases |
| Phase 13: Documentation | 1 week | All phases |

**Total Backend Development:** ~10-14 weeks  
**Total Frontend Development:** ~4-6 weeks  
**Total Project Duration:** ~14-20 weeks (3.5 - 5 months)

*Assumes 1 developer working full-time. Adjust accordingly for team size.*

---

## 🎯 **Recommended Execution Order**

### **Sprint 1-2: Foundation Entities**
- Phase 1 (Room, Guest, Reservation entities)
- Update Hotel for ownership

### **Sprint 3-4: Core Business Logic**
- Phase 2 (Availability, Pricing, Workflows)

### **Sprint 5: Querying & Filtering**
- Phase 3 (Search, Filter, Dashboard)

### **Sprint 6: Calendar & Authorization**
- Phase 4 (Calendar)
- Phase 5 (Role-based permissions)

### **Sprint 7-8: Supporting Features**
- Phase 6 (Notifications)
- Phase 7 (File uploads)

### **Sprint 9-12: Frontend Development**
- Phase 9 (React app)

### **Sprint 13: Polish & Deploy**
- Phase 8 (Reporting)
- Phase 10 (Optimization)
- Phase 11 (DevOps)

### **Sprint 14: Final Testing & Launch**
- Phase 12 (Testing)
- Phase 13 (Documentation)

---

## 🚀 **Next Immediate Steps**

### **Start Phase 1.1: Room Entity**

1. Create `Room.cs` entity
2. Create `RoomDto.cs`
3. Add to `ApplicationDbContext`
4. Create migration
5. Create `IRoomService` and `RoomService`
6. Create `RoomsController`
7. Test in Swagger

**Would you like me to start implementing Phase 1.1 (Room Entity) now?** 🏗️

---

## 📝 **Notes**

- Each phase is independent enough to be worked on by different developers
- Frontend can start once backend API is stable (after Phase 5)
- Testing should be ongoing, not just Phase 12
- Documentation should be written alongside code, not at the end
- Adjust priorities based on business needs

**This roadmap ensures:**
- ✅ Scalable architecture
- ✅ Clean separation of concerns
- ✅ Testable code
- ✅ Production-ready features
- ✅ Maintainable codebase
