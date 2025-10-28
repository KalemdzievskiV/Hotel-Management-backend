# 🏨 Hotel Management System - Backend API Reference

## 📋 **Complete API Overview for Frontend Development**

**Base URL:** `https://localhost:5001/api` (Development)

**Authentication:** JWT Bearer Token in `Authorization: Bearer {token}` header

---

## 🔐 **1. AUTHENTICATION**

### **Base:** `/api/Auth`

#### **POST /api/Auth/register**
- **Public** (No auth required)
- **Registers Guest users only** (Staff must be created by SuperAdmin)
- **Body:**
  ```typescript
  {
    firstName: string;
    lastName: string;
    email: string;
    password: string;
    role: "Guest"; // Fixed to Guest for public registration
  }
  ```
- **Returns:** `AuthResponseDto`

#### **POST /api/Auth/login**
- **Public**
- **Body:**
  ```typescript
  {
    email: string;
    password: string;
  }
  ```
- **Returns:**
  ```typescript
  {
    token: string;
    email: string;
    fullName: string;
    roles: string[]; // ["Guest"], ["Admin"], ["SuperAdmin", "Admin"], etc.
    expiresAt: Date;
  }
  ```

---

## 👤 **2. USER MANAGEMENT**

### **Base:** `/api/Users`

**All endpoints require authentication**

| Endpoint | Method | Roles | Description |
|----------|--------|-------|-------------|
| `/api/Users` | POST | SuperAdmin | Create new user (staff) |
| `/api/Users` | GET | SuperAdmin, Admin, Manager | List all users |
| `/api/Users/{id}` | GET | All | Get user by ID |
| `/api/Users/{id}` | PUT | All | Update user profile |
| `/api/Users/{id}` | DELETE | SuperAdmin | Delete user |
| `/api/Users/role/{role}` | GET | SuperAdmin | Get users by role |
| `/api/Users/hotel/{hotelId}` | GET | SuperAdmin, Admin, Manager | Get users by hotel |
| `/api/Users/search?searchTerm=` | GET | SuperAdmin, Admin | Search users |
| `/api/Users/{id}/activate` | POST | SuperAdmin | Activate user |
| `/api/Users/{id}/deactivate` | POST | SuperAdmin | Deactivate user |
| `/api/Users/{id}/hotel` | PATCH | SuperAdmin | Assign user to hotel |
| `/api/Users/{id}/role` | PATCH | SuperAdmin | Update user role |
| `/api/Users/{id}/roles` | POST | SuperAdmin | Add role to user |
| `/api/Users/{id}/roles/{role}` | DELETE | SuperAdmin | Remove role |
| `/api/Users/stats/count` | GET | SuperAdmin | Total user count |
| `/api/Users/stats/by-role` | GET | SuperAdmin | User count by role |

---

## 🏨 **3. HOTELS**

### **Base:** `/api/Hotels`

| Endpoint | Method | Roles | Description |
|----------|--------|-------|-------------|
| `/api/Hotels` | POST | Admin, SuperAdmin | Create hotel |
| `/api/Hotels` | GET | All authenticated | List all hotels |
| `/api/Hotels/{id}` | GET | All authenticated | Get hotel by ID |
| `/api/Hotels/{id}` | PUT | Admin, SuperAdmin | Update hotel |
| `/api/Hotels/{id}` | DELETE | Admin, SuperAdmin | Delete hotel |
| `/api/Hotels/search?name=` | GET | All authenticated | Search hotels by name |
| `/api/Hotels/stats/count` | GET | SuperAdmin | Total hotel count |

### **Hotel Entity Properties:**

```typescript
interface Hotel {
  id: number;
  ownerId: string;
  ownerName?: string; // Computed in DTO
  
  // Basic
  name: string;
  description?: string;
  
  // Location
  address: string;
  city: string;
  country: string;
  postalCode?: string;
  
  // Contact
  phoneNumber?: string;
  email?: string;
  website?: string;
  
  // Rating
  stars: number; // 1-5
  rating: number; // 0-5.00
  totalReviews: number;
  
  // Amenities
  amenities?: string; // Comma-separated: "WiFi,Parking,Pool,Gym,Restaurant"
  
  // Business
  checkInTime?: string; // "14:00"
  checkOutTime?: string; // "11:00"
  
  // Status
  isActive: boolean;
  
  // Timestamps
  createdAt: Date;
  updatedAt?: Date;
  
  // Computed
  totalRooms?: number;
  totalReservations?: number;
}
```

---

## 🛏️ **4. ROOMS**

### **Base:** `/api/Rooms`

| Endpoint | Method | Roles | Description |
|----------|--------|-------|-------------|
| `/api/Rooms` | POST | Admin, Manager | Create room |
| `/api/Rooms` | GET | All authenticated | List all rooms |
| `/api/Rooms/{id}` | GET | All authenticated | Get room by ID |
| `/api/Rooms/{id}` | PUT | Admin, Manager | Update room |
| `/api/Rooms/{id}` | DELETE | Admin | Delete room |
| `/api/Rooms/hotel/{hotelId}` | GET | All authenticated | Get rooms by hotel |
| `/api/Rooms/hotel/{hotelId}/available` | GET | All authenticated | Get available rooms |
| `/api/Rooms/hotel/{hotelId}/short-stay` | GET | All authenticated | Get short-stay enabled rooms |
| `/api/Rooms/{id}/status` | PATCH | Admin, Manager, Housekeeper | Update room status |
| `/api/Rooms/{id}/clean` | POST | Admin, Manager, Housekeeper | Mark room as cleaned |
| `/api/Rooms/{id}/maintenance` | POST | Admin, Manager | Record maintenance |
| `/api/Rooms/stats/count` | GET | SuperAdmin, Admin, Manager | Total room count |

### **Room Entity Properties:**

```typescript
interface Room {
  id: number;
  hotelId: number;
  hotelName?: string; // Computed
  
  // Identification
  roomNumber: string;
  type: RoomType; // Enum
  floor: number;
  
  // Capacity & Pricing
  capacity: number; // 1-20
  pricePerNight: number; // Decimal
  
  // Short-Stay Support
  allowsShortStay: boolean;
  shortStayHourlyRate?: number;
  minimumShortStayHours?: number; // Default 2
  maximumShortStayHours?: number; // Default 12
  
  // Details
  description?: string;
  amenities?: string; // "WiFi,TV,AC,Minibar,Balcony"
  images?: string; // JSON array or comma-separated URLs
  areaSqM?: number;
  
  // Status
  status: RoomStatus; // Enum
  isActive: boolean;
  
  // Features
  bedType?: string; // "1 King Bed" or "2 Single Beds"
  hasBathtub: boolean;
  hasBalcony: boolean;
  isSmokingAllowed: boolean;
  viewType?: string; // "Sea View", "City View"
  
  // Timestamps
  createdAt: Date;
  updatedAt?: Date;
  lastCleaned?: Date;
  lastMaintenance?: Date;
  
  // Notes
  notes?: string;
  
  // Computed
  totalReservations?: number;
}
```

### **Room Enums:**

```typescript
enum RoomType {
  Single = 1,
  Double = 2,
  Twin = 3,
  Triple = 4,
  Suite = 5,
  Deluxe = 6,
  Presidential = 7,
  Studio = 8,
  Family = 9,
  Accessible = 10
}

enum RoomStatus {
  Available = 1,
  Occupied = 2,
  Cleaning = 3,
  Maintenance = 4,
  OutOfService = 5,
  Reserved = 6
}
```

---

## 👥 **5. GUESTS**

### **Base:** `/api/Guests`

| Endpoint | Method | Roles | Description |
|----------|--------|-------|-------------|
| `/api/Guests` | POST | Admin, Manager | Create walk-in guest |
| `/api/Guests` | GET | All authenticated | List accessible guests |
| `/api/Guests/{id}` | GET | All authenticated | Get guest by ID |
| `/api/Guests/{id}` | PUT | Admin, Manager | Update guest |
| `/api/Guests/{id}` | DELETE | Admin | Delete guest |
| `/api/Guests/my-guests` | GET | Admin, Manager | My walk-in guests only |
| `/api/Guests/hotel/{hotelId}` | GET | Admin, Manager | Guests by hotel |
| `/api/Guests/all-unfiltered` | GET | SuperAdmin | All guests (no filter) |
| `/api/Guests/{id}/vip` | PATCH | Admin, Manager | Toggle VIP status |
| `/api/Guests/{id}/blacklist` | POST | Admin, Manager | Blacklist guest |
| `/api/Guests/{id}/unblacklist` | POST | Admin, Manager | Remove from blacklist |
| `/api/Guests/search?query=` | GET | All authenticated | Search guests |
| `/api/Guests/stats/count` | GET | SuperAdmin, Admin | Total guest count |

### **Guest Entity Properties:**

```typescript
interface Guest {
  id: number;
  userId?: string; // NULL = walk-in, Set = registered user
  hotelId?: number; // For walk-in guests
  createdByUserId?: string; // Who created walk-in guest
  
  // Personal
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  
  // Identification
  identificationNumber?: string;
  identificationType?: string; // "Passport", "Driver License", "National ID"
  dateOfBirth?: Date;
  nationality?: string;
  gender?: string;
  
  // Address
  address?: string;
  city?: string;
  state?: string;
  country?: string;
  postalCode?: string;
  
  // Emergency Contact
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  emergencyContactRelationship?: string;
  
  // Preferences
  specialRequests?: string;
  preferences?: string;
  isVIP: boolean;
  loyaltyProgramNumber?: string;
  
  // Communication
  emailNotifications: boolean;
  smsNotifications: boolean;
  preferredLanguage?: string;
  
  // Billing
  companyName?: string; // For business travelers
  taxId?: string;
  
  // Status
  isActive: boolean;
  isBlacklisted: boolean;
  blacklistReason?: string;
  notes?: string; // Staff notes
  
  // Timestamps
  createdAt: Date;
  updatedAt?: Date;
  lastStayDate?: Date;
  
  // Computed in DTO
  hotelName?: string;
  createdByUserName?: string;
  isWalkInGuest?: boolean; // userId === null
  totalReservations?: number;
}
```

---

## 📅 **6. RESERVATIONS**

### **Base:** `/api/Reservations`

| Endpoint | Method | Roles | Description |
|----------|--------|-------|-------------|
| `/api/Reservations` | POST | All authenticated | Create reservation |
| `/api/Reservations` | GET | Admin, Manager | List all reservations |
| `/api/Reservations/{id}` | GET | All authenticated | Get reservation by ID |
| `/api/Reservations/{id}` | PUT | All authenticated | Update reservation |
| `/api/Reservations/{id}` | DELETE | Admin | Delete (Pending only) |
| `/api/Reservations/hotel/{hotelId}` | GET | Admin, Manager | Reservations by hotel |
| `/api/Reservations/room/{roomId}` | GET | Admin, Manager | Reservations by room |
| `/api/Reservations/guest/{guestId}` | GET | All authenticated | Reservations by guest |
| `/api/Reservations/status/{status}` | GET | Admin, Manager | Reservations by status |
| `/api/Reservations/daterange` | GET | Admin, Manager | Reservations by date range |
| `/api/Reservations/my-reservations` | GET | All authenticated | Current user's reservations |
| `/api/Reservations/room/{roomId}/availability` | GET | All authenticated | Check room availability |
| `/api/Reservations/room/{roomId}/conflicts` | GET | Admin, Manager | Get conflicting reservations |
| `/api/Reservations/{id}/confirm` | POST | Admin, Manager | Confirm reservation |
| `/api/Reservations/{id}/checkin` | POST | Admin, Manager | Check-in guest |
| `/api/Reservations/{id}/checkout` | POST | Admin, Manager | Check-out guest |
| `/api/Reservations/{id}/cancel` | POST | All authenticated | Cancel reservation |
| `/api/Reservations/{id}/noshow` | POST | Admin, Manager | Mark as no-show |
| `/api/Reservations/{id}/payment` | POST | Admin, Manager | Record payment |
| `/api/Reservations/{id}/refund` | POST | Admin, Manager | Record refund |
| `/api/Reservations/stats/count` | GET | Admin, Manager | Total reservation count |
| `/api/Reservations/stats/revenue` | GET | Admin, Manager | Total revenue |
| `/api/Reservations/stats/by-status` | GET | Admin, Manager | Count by status |
| `/api/Reservations/stats/by-month/{year}` | GET | Admin, Manager | Monthly trends |

### **Reservation Entity Properties:**

```typescript
interface Reservation {
  id: number;
  hotelId: number;
  roomId: number;
  guestId: number;
  createdByUserId: string;
  
  // Booking Details
  bookingType: BookingType; // Daily or ShortStay
  checkInDate: Date;
  checkOutDate: Date;
  durationInHours?: number; // For short-stay
  numberOfGuests: number;
  
  // Status
  status: ReservationStatus;
  
  // Financial
  totalAmount: number; // Auto-calculated
  depositAmount: number;
  remainingAmount: number; // Auto-calculated
  paymentStatus: PaymentStatus;
  paymentMethod?: PaymentMethod;
  paymentReference?: string;
  
  // Notes
  specialRequests?: string; // Guest requests
  notes?: string; // Staff notes
  
  // Timestamps
  createdAt: Date;
  updatedAt?: Date;
  confirmedAt?: Date;
  checkedInAt?: Date;
  checkedOutAt?: Date;
  cancelledAt?: Date;
  cancellationReason?: string;
  
  // Computed Properties
  totalNights?: number; // For daily bookings
  isActive?: boolean; // Not cancelled/completed/noshow
  canCheckIn?: boolean; // Status + date checks
  canCheckOut?: boolean;
  canCancel?: boolean;
  
  // Computed in DTO
  hotelName?: string;
  roomNumber?: string;
  guestName?: string;
  createdByUserName?: string;
}
```

### **Reservation Enums:**

```typescript
enum BookingType {
  Daily = 0,      // Overnight stays
  ShortStay = 1   // Hourly bookings
}

enum ReservationStatus {
  Pending = 0,
  Confirmed = 1,
  CheckedIn = 2,
  CheckedOut = 3,
  Cancelled = 4,
  NoShow = 5
}

enum PaymentStatus {
  Unpaid = 0,
  PartiallyPaid = 1,
  Paid = 2,
  Refunding = 3,
  Refunded = 4
}

enum PaymentMethod {
  Cash = 0,
  CreditCard = 1,
  DebitCard = 2,
  BankTransfer = 3,
  Online = 4,
  PayOnArrival = 5
}
```

### **Reservation Status Workflow:**

```
Pending → Confirm → Confirmed → Check-In → CheckedIn → Check-Out → CheckedOut
   ↓                    ↓
 Cancel              Cancel
   ↓                    ↓
Cancelled           Cancelled

Confirmed → (no-show) → NoShow
```

---

## 🎭 **ROLES & PERMISSIONS**

### **Role Hierarchy:**

```typescript
const roles = {
  SuperAdmin: {
    permissions: "ALL",
    canManage: ["users", "hotels", "rooms", "guests", "reservations"],
    description: "Full system access"
  },
  
  Admin: {
    permissions: "Hotel-level full access",
    canManage: ["their hotels", "rooms", "guests", "reservations", "staff"],
    description: "Hotel owner/administrator"
  },
  
  Manager: {
    permissions: "Hotel operations",
    canManage: ["rooms", "guests", "reservations", "check-in/out"],
    description: "Hotel manager"
  },
  
  Housekeeper: {
    permissions: "Room maintenance",
    canManage: ["room status", "cleaning"],
    description: "Housekeeping staff"
  },
  
  Guest: {
    permissions: "Basic user",
    canManage: ["their profile", "their reservations"],
    description: "Registered guest"
  }
};
```

---

## 📊 **KEY BUSINESS LOGIC**

### **1. Reservation Pricing:**

```typescript
// Daily Booking
totalAmount = nights × room.pricePerNight

// Short-Stay Booking  
totalAmount = hours × room.shortStayHourlyRate

// Payment Status Logic
if (depositAmount === 0) → Unpaid
if (0 < depositAmount < totalAmount) → PartiallyPaid
if (depositAmount >= totalAmount) → Paid
```

### **2. Room Availability:**

```typescript
// Room is available if:
- No overlapping reservations OR
- Overlapping reservations are Cancelled, CheckedOut, or NoShow
```

### **3. Short-Stay Validation:**

```typescript
// For short-stay bookings:
- room.allowsShortStay must be true
- duration >= room.minimumShortStayHours (default 2)
- duration <= room.maximumShortStayHours (default 12)
```

### **4. Guest Types:**

```typescript
// Walk-in Guest:
- userId === null
- hotelId !== null (belongs to specific hotel)
- createdByUserId !== null (created by staff)
- Visible only to creator + admins

// Registered User:
- userId !== null (linked to ApplicationUser)
- hotelId === null (not hotel-specific)
- Visible to all hotels
```

---

## 🔑 **JWT TOKEN STRUCTURE**

```typescript
interface JWTPayload {
  sub: string; // User ID
  email: string;
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": string[]; // Roles
  exp: number; // Expiration timestamp
}
```

**Usage in Frontend:**

```typescript
// Store token
localStorage.setItem('token', authResponse.token);
localStorage.setItem('userRoles', JSON.stringify(authResponse.roles));

// Add to API requests
headers: {
  'Authorization': `Bearer ${token}`,
  'Content-Type': 'application/json'
}
```

---

## 🎨 **FRONTEND RECOMMENDATIONS**

### **Priority Pages to Build:**

1. **Authentication** (Login/Register)
2. **Dashboard** (Role-specific)
3. **Hotels** (List/Create/Edit)
4. **Rooms** (Inventory + Status management)
5. **Reservations** (Calendar + Booking form)
6. **Guests** (Directory)
7. **Users** (SuperAdmin only)

### **Key UI Components Needed:**

- ✅ Data Tables (filterable, sortable)
- ✅ Forms (create/edit entities)
- ✅ Calendar/Date Pickers
- ✅ Status Badges
- ✅ Statistics Cards
- ✅ Charts (revenue, occupancy)
- ✅ Modals/Dialogs
- ✅ Toast Notifications

---

## 📝 **NOTES FOR FRONTEND:**

1. **All dates are in UTC** - convert to local timezone for display
2. **Enums are integers** - map to friendly labels
3. **Nullable fields** - handle undefined/null gracefully
4. **Computed properties** - calculated on backend, read-only in frontend
5. **Amenities/Images** - stored as comma-separated strings or JSON
6. **Role-based rendering** - show/hide features based on user roles
7. **Status colors** - use consistent color schemes for statuses

---

**This document provides everything needed to build a comprehensive Next.js frontend!** 🚀
