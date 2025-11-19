# Authorization Implementation - Hotel Management System

## 🎯 Overview

Implemented hotel-level authorization with the following permission model:
- **SuperAdmin**: Full access to all resources
- **Admin**: Full access to their own hotels and related resources
- **Manager**: Read/use access to their hotels, cannot create/edit/delete hotels
- **Guest**: Only access to their own reservations

## 📋 Implementation Summary

### 1. **Authorization Requirements** ✅

Created three authorization requirements in `Authorization/Requirements/`:

#### **HotelOwnershipRequirement**
- Checks if user has access to view/use a hotel
- SuperAdmin: All hotels
- Admin/Manager: Only hotels they own (OwnerId matches UserId)

#### **ManageHotelRequirement**
- Determines if user can create, edit, or delete hotels
- SuperAdmin: Yes, all hotels
- Admin: Yes, their own hotels only
- Manager: No (read-only)
- Guest: No

#### **ReservationAccessRequirement**
- Controls access to reservations
- SuperAdmin: All reservations
- Admin/Manager: Reservations in their hotels
- Guest: Only their own reservations

---

### 2. **Authorization Handlers** ✅

Created six authorization handlers in `Authorization/Handlers/`:

#### **HotelOwnershipHandler**
- Handles `HotelOwnershipRequirement` for `Hotel` entities
- Checks `hotel.OwnerId == userId` for Admin/Manager
- Always succeeds for SuperAdmin

#### **HotelOwnershipByIdHandler**
- Handles `HotelOwnershipRequirement` for hotel IDs
- Loads hotel from database and checks ownership
- Used when only hotel ID is available (not full entity)

#### **ManageHotelHandler**
- Handles `ManageHotelRequirement` for hotel management
- SuperAdmin: Can manage all
- Admin: Can manage their own
- Manager/Guest: Cannot manage

#### **CreateHotelHandler**
- Handles `ManageHotelRequirement` for hotel creation
- SuperAdmin and Admin can create
- Manager and Guest cannot create

#### **ReservationAccessHandler**
- Handles `ReservationAccessRequirement` for `Reservation` entities
- Checks hotel ownership for Admin/Manager
- Checks guest ownership for Guest role

#### **ReservationAccessByIdHandler**
- Handles `ReservationAccessRequirement` for reservation IDs
- Loads full reservation with includes and checks access

---

### 3. **Authorization Policies** ✅

Registered in `Configurations/DependencyInjection.cs`:

```csharp
services.AddAuthorization(options =>
{
    // Resource-based policies
    options.AddPolicy("CanViewHotel", policy =>
        policy.Requirements.Add(new HotelOwnershipRequirement()));

    options.AddPolicy("CanManageHotel", policy =>
        policy.Requirements.Add(new ManageHotelRequirement()));

    options.AddPolicy("CanAccessReservation", policy =>
        policy.Requirements.Add(new ReservationAccessRequirement()));

    // Role-based policies (for quick checks)
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("SuperAdmin", "Admin"));

    options.AddPolicy("ManagerOrAbove", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "Manager"));
});
```

---

### 4. **Service Layer Filtering** ✅

Updated `HotelService` to automatically filter data based on user ownership:

#### **GetAllAsync()**
```csharp
// SuperAdmin sees all hotels
// Admin/Manager see only their hotels (where OwnerId == userId)
var query = _context.Hotels.Include(h => h.Owner).AsQueryable();

if (!IsSuperAdmin())
{
    var userId = GetCurrentUserId();
    query = query.Where(h => h.OwnerId == userId);
}
```

#### **GetByIdAsync()**
```csharp
// Same filtering logic - prevents access to other admins' hotels
```

**Benefits:**
- ✅ No need to check authorization in controllers for list/get operations
- ✅ Automatic data isolation
- ✅ Prevents data leakage between admins

---

### 5. **Unit Tests** ✅

Created comprehensive test suites:

#### **HotelOwnershipHandlerTests** (6 tests)
- ✅ SuperAdmin can access any hotel
- ✅ Admin can access own hotel
- ✅ Admin cannot access other admin's hotel
- ✅ Manager can access own hotel
- ✅ Manager cannot access other hotel
- ✅ Guest cannot access any hotel

#### **ManageHotelHandlerTests** (9 tests)
- ✅ SuperAdmin can manage any hotel
- ✅ Admin can manage own hotel
- ✅ Admin cannot manage other admin's hotel
- ✅ Manager cannot manage hotels
- ✅ Guest cannot manage hotels
- ✅ SuperAdmin can create hotels
- ✅ Admin can create hotels
- ✅ Manager cannot create hotels
- ✅ Guest cannot create hotels

**Test Results:** 15/15 tests passed ✅

---

## 📊 Permission Matrix

| Action | SuperAdmin | Admin | Manager | Guest |
|--------|-----------|-------|---------|-------|
| **Hotels** |
| View All | ✅ All | ✅ Own only | ✅ Own only | ❌ |
| View Single | ✅ Any | ✅ Own only | ✅ Own only | ❌ |
| Create | ✅ | ✅ | ❌ | ❌ |
| Edit | ✅ Any | ✅ Own only | ❌ | ❌ |
| Delete | ✅ Any | ✅ Own only | ❌ | ❌ |
| **Reservations** |
| View All | ✅ All | ✅ Own hotels | ✅ Own hotels | ❌ |
| View Single | ✅ Any | ✅ Own hotels | ✅ Own hotels | ✅ Own only |
| Create | ✅ | ✅ Own hotels | ✅ Own hotels | ✅ |
| Edit | ✅ | ✅ Own hotels | ✅ Own hotels | ✅ Own only |
| Delete | ✅ | ✅ Own hotels | ✅ Own hotels | ❌ |
| **Rooms** |
| Manage | ✅ All | ✅ Own hotels | ✅ Own hotels | ❌ |
| **Users** |
| Manage | ✅ All | ❌ | ❌ | ❌ |

---

## 🔧 Implementation Details

### **Key Design Decisions:**

1. **Hotel Ownership via OwnerId**
   - Hotels have `OwnerId` property linking to the creator
   - Simple and direct ownership model
   - Easy to query and filter

2. **Manager = Admin - Creation Rights**
   - Managers have same permissions as Admins
   - EXCEPT: Cannot create/edit/delete hotels
   - Can manage all other resources in their hotels

3. **Service-Level Filtering**
   - Authorization happens at service layer
   - Controllers don't need explicit checks for list operations
   - Consistent across all endpoints

4. **Resource-Based Authorization**
   - Uses ASP.NET Core's `IAuthorizationHandler<TRequirement, TResource>`
   - Can check ownership of specific entities
   - Flexible and extensible

---

## 📁 Files Created

### Authorization Infrastructure
```
Authorization/
├── Requirements/
│   ├── HotelOwnershipRequirement.cs
│   ├── ManageHotelRequirement.cs
│   └── ReservationAccessRequirement.cs
└── Handlers/
    ├── HotelOwnershipHandler.cs (+ ByIdHandler)
    ├── ManageHotelHandler.cs (+ CreateHotelHandler)
    └── ReservationAccessHandler.cs (+ ByIdHandler)
```

### Tests
```
Tests/Authorization/
├── HotelOwnershipHandlerTests.cs (6 tests)
└── ManageHotelHandlerTests.cs (9 tests)
```

### Configuration
```
Configurations/
└── DependencyInjection.cs (updated with policies)
```

### Services
```
Services/Implementations/
└── HotelService.cs (updated with filtering)
```

---

## 🚀 Next Steps

### **Immediate (Required):**

1. **Apply Authorization to Controllers** 🔄
   - Add `[Authorize(Policy = "...")]` attributes to controllers
   - Use `IAuthorizationService` for resource-based checks
   - Test with different user roles

2. **Extend to Rooms Service**
   - Add ownership filtering to RoomService
   - Ensure rooms are filtered by hotel ownership

3. **Extend to Reservations Service**
   - Add ownership/access filtering to ReservationService
   - Handle both hotel ownership and guest ownership

### **Future Enhancements:**

4. **Hotel-Manager Assignment Table** (Optional)
   - Create `HotelManagers` join table
   - Allow assigning managers to specific hotels
   - Update authorization to check assignments

5. **Frontend Authorization**
   - Hide/show UI elements based on permissions
   - Route guards
   - API error handling (403 Forbidden)

6. **Audit Logging**
   - Log authorization failures
   - Track who accessed what

7. **Advanced Scenarios**
   - Department-level permissions
   - Temporary access grants
   - Delegation

---

## 🧪 Testing Instructions

### **Run Authorization Tests:**
```bash
dotnet test --filter "FullyQualifiedName~HotelManagement.Tests.Authorization"
```

### **Manual Testing:**

1. **Login as Admin**
   - Create a hotel
   - Verify you can see and edit it
   - Verify you CANNOT see other admins' hotels

2. **Login as Manager**
   - Try to create a hotel (should fail)
   - Verify you can view assigned hotels
   - Verify you can manage reservations

3. **Login as Guest**
   - Try to view hotels (should fail/empty)
   - Create a reservation
   - Verify you can only see your own reservations

---

## 📝 Notes

### **Important Considerations:**

1. **OwnerId vs CreatedBy**
   - We use `OwnerId` field (not `CreatedBy`)
   - `OwnerId` is set when hotel is created
   - Cannot be changed after creation

2. **SuperAdmin Bypass**
   - SuperAdmin bypasses all authorization checks
   - Has full access to all resources
   - Use carefully in production

3. **Guest Isolation**
   - Guests can ONLY see their own reservations
   - Cannot see any hotels or rooms directly
   - Access through reservation booking flow only

4. **Performance**
   - Service-layer filtering adds WHERE clause to queries
   - No N+1 query problems
   - Consider adding indexes on `OwnerId` for large datasets

---

## ✅ Summary

**Completed:**
- ✅ Authorization requirements and handlers
- ✅ Policy-based authorization
- ✅ Service-layer data filtering
- ✅ 15 unit tests (all passing)
- ✅ Documentation

**Status:** Backend authorization infrastructure complete! Ready to apply to controllers and extend to other services.

**Next:** Apply authorization policies to controllers and test end-to-end.
