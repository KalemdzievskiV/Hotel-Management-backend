# Role-Based Access & Dashboard Refactoring Plan

## 🎯 Problem Statement

**Current Issue:**
All roles see the same dashboard and navigation. We need role-specific views.

**Goal:**
Create role-specific dashboards with appropriate UI/UX for each user type.

**Note:** Backend authorization remains unchanged (SuperAdmin keeps full access for flexibility).

---

## 👥 Refined Role Definitions

### **1. SuperAdmin (System Administrator)**

**Purpose:** System-level administration and oversight

**Primary Responsibilities:**
- ✅ Create hotels and assign them to Admins
- ✅ Manage all users (create, edit, delete, assign roles)
- ✅ System settings and configuration
- 🔮 Manage subscriptions (future)
- 🔮 View system-wide analytics/reports
- 🔮 Audit logs and security

**Should NOT See:**
- ❌ Individual room details from all hotels
- ❌ Individual reservation details from all hotels
- ❌ Day-to-day hotel operations

**Dashboard Components:**
- User management table
- Hotel list (with owner assignments)
- Quick stats: Total Users, Total Hotels, Active Subscriptions
- System health indicators
- Recent user activity

---

### **2. Admin (Hotel Owner/Manager)**

**Purpose:** Owns and operates one or more hotels

**Primary Responsibilities:**
- ✅ Create their own hotels
- ✅ View/manage their own hotels (edit details, settings)
- ✅ Create/manage rooms in their hotels
- ✅ View/manage reservations in their hotels
- ✅ Manage guests for their hotels
- ✅ View hotel-specific analytics

**Dashboard Components:**
- Hotel selector dropdown (if multiple hotels)
- Occupancy overview (current/upcoming)
- Revenue chart
- Recent reservations table
- Room status overview
- Quick actions: New reservation, Add room

---

### **3. Manager (Hotel Staff)**

**Purpose:** Manages day-to-day operations of assigned hotel(s)

**Primary Responsibilities:**
- ✅ View hotels they're assigned to
- ✅ Create/manage rooms
- ✅ View/manage reservations
- ✅ Manage guests
- ❌ Cannot edit hotel details
- ❌ Cannot delete hotels

**Dashboard Components:**
- Same as Admin, but read-only hotel settings
- Focus on operations: Check-ins, Check-outs, Room status
- Task list (cleaning, maintenance)

---

### **4. Guest (Customer)**

**Purpose:** Book and manage their own reservations

**Primary Responsibilities:**
- ✅ Search available rooms
- ✅ Create reservations
- ✅ View their own reservations
- ✅ Update their profile
- ❌ Cannot see other guests' data
- ❌ Cannot see hotel management features

**Dashboard Components:**
- My upcoming reservations
- My past reservations
- Search available rooms
- Profile settings

---

## 📊 Revised Permission Matrix

| Resource/Action | SuperAdmin | Admin | Manager | Guest |
|-----------------|-----------|-------|---------|-------|
| **Hotels** |
| View All Hotels | ✅ All (list only) | ✅ Own only | ✅ Assigned only | ❌ |
| View Hotel Details | ✅ Any | ✅ Own only | ✅ Assigned only | ❌ |
| Create Hotel | ✅ | ❌ | ❌ | ❌ |
| Edit Hotel | ✅ Any | ✅ Own only | ❌ | ❌ |
| Delete Hotel | ✅ Any | ❌ | ❌ | ❌ |
| **Rooms** |
| View All Rooms | ❌ | ✅ Own hotels | ✅ Assigned hotels | ❌ |
| View Room Details | ❌ | ✅ Own hotels | ✅ Assigned hotels | ✅ When booking |
| Create Room | ❌ | ✅ Own hotels | ✅ Assigned hotels | ❌ |
| Edit Room | ❌ | ✅ Own hotels | ✅ Assigned hotels | ❌ |
| Delete Room | ❌ | ✅ Own hotels | ✅ Assigned hotels | ❌ |
| **Reservations** |
| View All Reservations | ❌ | ✅ Own hotels | ✅ Assigned hotels | ❌ |
| View Reservation Details | ❌ | ✅ Own hotels | ✅ Assigned hotels | ✅ Own only |
| Create Reservation | ❌ | ✅ Own hotels | ✅ Assigned hotels | ✅ |
| Edit Reservation | ❌ | ✅ Own hotels | ✅ Assigned hotels | ✅ Own only |
| Delete Reservation | ❌ | ✅ Own hotels | ❌ | ❌ |
| **Users** |
| View All Users | ✅ | ❌ | ❌ | ❌ |
| Create User | ✅ | ❌ | ❌ | ❌ |
| Edit User | ✅ | ❌ | ❌ | ✅ Own profile |
| Delete User | ✅ | ❌ | ❌ | ❌ |
| Assign Roles | ✅ | ❌ | ❌ | ❌ |
| **Guests** |
| View Guests | ❌ | ✅ Own hotels | ✅ Assigned hotels | ❌ |
| Manage Guests | ❌ | ✅ Own hotels | ✅ Assigned hotels | ❌ |
| **System** |
| System Settings | ✅ | ❌ | ❌ | ❌ |
| View Audit Logs | ✅ | ❌ | ❌ | ❌ |
| Manage Subscriptions | 🔮 ✅ | 🔮 View own | ❌ | ❌ |

---

## 🔧 Backend Changes Required

### **Phase 1: Update Authorization Logic**

#### **1. HotelsController**
```diff
- Current: SuperAdmin sees ALL hotels
+ New: SuperAdmin sees list for assignment purposes only
```

**Changes:**
- `GET /api/hotels` - SuperAdmin can list all hotels (for assignment)
- `GET /api/hotels/{id}` - SuperAdmin can view any hotel details
- Keep create/edit/delete permissions for SuperAdmin

#### **2. RoomsController**
```diff
- Current: SuperAdmin sees ALL rooms
+ New: SuperAdmin should NOT access room endpoints
```

**Changes:**
- `GET /api/rooms` - Remove SuperAdmin bypass, return 403
- `GET /api/rooms/{id}` - Remove SuperAdmin bypass, return 403
- SuperAdmin doesn't need room operations

#### **3. ReservationsController**
```diff
- Current: SuperAdmin sees ALL reservations
+ New: SuperAdmin should NOT access reservation endpoints
```

**Changes:**
- `GET /api/reservations` - Remove SuperAdmin bypass, return 403
- `GET /api/reservations/{id}` - Remove SuperAdmin bypass, return 403
- SuperAdmin doesn't need reservation operations

#### **4. UsersController** (Create if not exists)
```diff
+ New: SuperAdmin-only user management endpoints
```

**New Endpoints:**
- `GET /api/users` - List all users (SuperAdmin only)
- `POST /api/users` - Create user (SuperAdmin only)
- `PUT /api/users/{id}` - Edit user (SuperAdmin only)
- `DELETE /api/users/{id}` - Delete user (SuperAdmin only)
- `PUT /api/users/{id}/role` - Assign role (SuperAdmin only)

---

### **Phase 2: Service Layer Updates**

#### **HotelService**
- Keep current filtering logic
- SuperAdmin can list all hotels (for assignment)

#### **RoomService**
- Remove SuperAdmin bypass
- Only Admin/Manager of hotel can access rooms

#### **ReservationService**
- Remove SuperAdmin bypass
- Only Admin/Manager of hotel can access reservations

#### **UserService** (Create)
- SuperAdmin-only access
- CRUD operations for users
- Role assignment logic

---

## 🎨 Frontend Implementation Plan

### **Phase 1: Authentication & Route Guards**

#### **1. Auth Context/Service**
```typescript
interface User {
  id: string;
  email: string;
  role: 'SuperAdmin' | 'Admin' | 'Manager' | 'Guest';
  hotelIds?: number[]; // For Admin/Manager
}

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isSuperAdmin: boolean;
  isAdmin: boolean;
  isManager: boolean;
  isGuest: boolean;
  login: (credentials) => Promise<void>;
  logout: () => void;
}
```

#### **2. Route Guards**
```typescript
// Example protected route structure
/dashboard
  /super-admin (SuperAdmin only)
    - Users management
    - Hotels list (for assignment)
    - System settings
  
  /admin (Admin only)
    - Hotel management
    - Rooms management
    - Reservations management
    - Analytics
  
  /manager (Manager only)
    - Operations dashboard
    - Reservations
    - Room status
  
  /guest (Guest only)
    - My reservations
    - Search & book
```

#### **3. Permission Hooks**
```typescript
const usePermissions = () => {
  const { user } = useAuth();
  
  return {
    canViewHotels: ['SuperAdmin', 'Admin', 'Manager'].includes(user.role),
    canCreateHotel: user.role === 'SuperAdmin',
    canEditHotel: ['SuperAdmin', 'Admin'].includes(user.role),
    canViewRooms: ['Admin', 'Manager'].includes(user.role),
    canManageReservations: ['Admin', 'Manager'].includes(user.role),
    canManageUsers: user.role === 'SuperAdmin',
  };
};
```

---

### **Phase 2: Role-Specific Dashboards**

#### **1. SuperAdmin Dashboard** (`/dashboard/super-admin`)

**Components:**
```
┌─────────────────────────────────────────┐
│ System Overview                         │
├─────────────────────────────────────────┤
│ [Total Users: 25] [Total Hotels: 10]   │
│ [Active Subscriptions: 8]               │
├─────────────────────────────────────────┤
│ Users Management                        │
│ [+ Add User]                            │
│ ┌─────────────────────────────────────┐ │
│ │ Email    │ Role   │ Status │ Actions││
│ │ admin1@  │ Admin  │ Active │ [Edit] ││
│ │ manager1@│ Manager│ Active │ [Edit] ││
│ └─────────────────────────────────────┘ │
├─────────────────────────────────────────┤
│ Hotels Overview                         │
│ [+ Create Hotel]                        │
│ ┌─────────────────────────────────────┐ │
│ │ Name     │ Owner  │ Rooms │ Actions ││
│ │ Hotel A  │ admin1 │ 25    │ [View]  ││
│ │ Hotel B  │ admin2 │ 15    │ [View]  ││
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

#### **2. Admin Dashboard** (`/dashboard/admin`)

**Components:**
```
┌─────────────────────────────────────────┐
│ Hotel Selector: [Grand Hotel ▼]        │
├─────────────────────────────────────────┤
│ Quick Stats                             │
│ [Occupancy: 75%] [Today: 5 Check-ins]  │
│ [Revenue: $15,420] [Pending: 3]        │
├─────────────────────────────────────────┤
│ Occupancy Chart                         │
│     [Graph showing next 30 days]        │
├─────────────────────────────────────────┤
│ Recent Reservations                     │
│ [+ New Reservation]                     │
│ ┌─────────────────────────────────────┐ │
│ │ Guest  │ Room │ Dates    │ Status   ││
│ │ John D.│ 101  │ Oct 10-12│ Confirmed││
│ └─────────────────────────────────────┘ │
├─────────────────────────────────────────┤
│ Room Status Overview                    │
│ [Available: 12] [Occupied: 18]         │
│ [Cleaning: 3] [Maintenance: 2]         │
└─────────────────────────────────────────┘
```

#### **3. Manager Dashboard** (`/dashboard/manager`)

**Components:**
```
┌─────────────────────────────────────────┐
│ Today's Operations                      │
├─────────────────────────────────────────┤
│ Check-ins Today                         │
│ ┌─────────────────────────────────────┐ │
│ │ 10:00 │ John Doe │ Room 101         ││
│ │ 14:00 │ Jane S.  │ Room 205         ││
│ └─────────────────────────────────────┘ │
├─────────────────────────────────────────┤
│ Check-outs Today                        │
│ ┌─────────────────────────────────────┐ │
│ │ 11:00 │ Mike T.  │ Room 303         ││
│ └─────────────────────────────────────┘ │
├─────────────────────────────────────────┤
│ Room Status                             │
│ [Clean: 15] [Dirty: 5] [Maintenance: 2]│
└─────────────────────────────────────────┘
```

#### **4. Guest Dashboard** (`/dashboard/guest`)

**Components:**
```
┌─────────────────────────────────────────┐
│ My Upcoming Reservations                │
│ ┌─────────────────────────────────────┐ │
│ │ Hotel    │ Room │ Dates    │ Actions││
│ │ Grand H. │ 201  │ Nov 15-17│ [View] ││
│ └─────────────────────────────────────┘ │
├─────────────────────────────────────────┤
│ Search Available Rooms                  │
│ [Search Form]                           │
├─────────────────────────────────────────┤
│ Past Reservations                       │
│ ┌─────────────────────────────────────┐ │
│ │ Hotel    │ Dates    │ Status        ││
│ │ Plaza H. │ Oct 1-3  │ Completed     ││
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

---

### **Phase 3: Navigation Structure**

#### **SuperAdmin Navigation:**
```
- Dashboard
- Users
- Hotels
- System Settings
- Profile
- Logout
```

#### **Admin Navigation:**
```
- Dashboard
- Hotels (My Hotels)
  - Hotel Details
  - Edit Hotel
- Rooms
  - All Rooms
  - Add Room
- Reservations
  - All Reservations
  - New Reservation
- Guests
- Analytics
- Profile
- Logout
```

#### **Manager Navigation:**
```
- Dashboard
- Reservations
- Rooms
- Guests
- Profile
- Logout
```

#### **Guest Navigation:**
```
- Search Hotels
- My Reservations
- Profile
- Logout
```

---

## 📋 Implementation Task List

### **Backend Tasks:**

#### **High Priority:**
- [ ] Remove SuperAdmin bypass in RoomsController
- [ ] Remove SuperAdmin bypass in ReservationsController
- [ ] Keep SuperAdmin access to HotelsController (list/view for assignment)
- [ ] Update authorization handlers to reflect new permissions
- [ ] Update unit tests for new authorization logic

#### **Medium Priority:**
- [ ] Create UsersController (SuperAdmin only)
- [ ] Create UserService
- [ ] Add user management endpoints
- [ ] Add role assignment endpoint

#### **Low Priority:**
- [ ] Add system-wide analytics endpoint (SuperAdmin)
- [ ] Add audit logging
- [ ] Add hotel assignment tracking

---

### **Frontend Tasks:**

#### **Phase 1: Foundation (Week 1)**
- [ ] Create AuthContext with role checking
- [ ] Implement route guards
- [ ] Create permission hooks
- [ ] Set up role-based routing

#### **Phase 2: Dashboards (Week 2)**
- [ ] Create SuperAdmin dashboard
  - [ ] Users management table
  - [ ] Hotels list
  - [ ] System stats
- [ ] Create Admin dashboard
  - [ ] Hotel selector
  - [ ] Stats cards
  - [ ] Occupancy chart
  - [ ] Reservations table

#### **Phase 3: Navigation & UX (Week 3)**
- [ ] Role-based navigation menu
- [ ] Conditional rendering components
- [ ] Hide/show based on permissions
- [ ] Handle 403 errors gracefully

#### **Phase 4: Manager & Guest (Week 4)**
- [ ] Create Manager dashboard
- [ ] Create Guest dashboard
- [ ] Search & booking flow for guests
- [ ] My reservations page

---

## 🎯 Success Criteria

**Backend:**
- ✅ SuperAdmin cannot access individual room/reservation endpoints
- ✅ SuperAdmin can manage users
- ✅ SuperAdmin can view/create/edit hotels
- ✅ All authorization tests passing
- ✅ Clear separation of system admin vs hotel operations

**Frontend:**
- ✅ Role-specific dashboards render correctly
- ✅ Users redirected to appropriate dashboard on login
- ✅ Navigation shows only permitted links
- ✅ 403 errors handled gracefully
- ✅ Permission-based UI rendering works

---

## 🔮 Future Enhancements

1. **Hotel-Manager Assignment Table**
   - Allow assigning managers to specific hotels
   - Manager can work in multiple hotels

2. **Department/Role Permissions**
   - Housekeeping role
   - Front desk role
   - Maintenance role

3. **Subscription Management**
   - SuperAdmin can manage subscriptions
   - Admin can view their subscription status
   - Tiered features based on subscription

4. **Advanced Analytics**
   - System-wide analytics for SuperAdmin
   - Hotel-specific analytics for Admin
   - Performance metrics

---

## 📝 Notes

- **Breaking Change:** This will change SuperAdmin behavior significantly
- **Migration:** Existing SuperAdmin users should still work, but won't see operational data
- **Testing:** Need comprehensive testing of new permission model
- **Documentation:** Update API documentation to reflect new permissions

---

## ✅ Approval Required

Before proceeding, please confirm:
- [ ] SuperAdmin should NOT see all rooms/reservations
- [ ] SuperAdmin focus should be user & hotel management
- [ ] Role-specific dashboards make sense
- [ ] Permission matrix is correct
- [ ] Ready to start backend refactoring

**Once approved, we'll start with backend changes first, then move to frontend implementation.**
