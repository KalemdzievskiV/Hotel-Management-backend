# 🎨 Hotel Management Frontend - Architecture Plan

## 📊 **Backend Analysis Summary**

### **What You Have:**

✅ **4 Core Entities:**
- Hotel (20+ properties, ownership tracking)
- Room (30+ properties, dual-pricing, short-stay support)
- Guest (40+ properties, walk-in vs registered, VIP/blacklist)
- Reservation (30+ properties, status workflow, payment tracking)

✅ **76+ API Endpoints:**
- Authentication: 2 endpoints
- Users: 16 endpoints (full CRUD + management)
- Hotels: 8 endpoints
- Rooms: 13 endpoints (including short-stay)
- Guests: 13 endpoints (with ownership filtering)
- Reservations: 24 endpoints (complete booking system)

✅ **5 User Roles:**
- SuperAdmin (full system access)
- Admin (hotel owner/administrator)
- Manager (hotel operations)
- Housekeeper (room maintenance)
- Guest (registered customer)

✅ **Advanced Features:**
- JWT authentication with refresh
- Role-based authorization
- Multi-tenant support (hotel ownership)
- Short-stay/hourly bookings
- Complete payment tracking
- Status workflows
- Data isolation (walk-in guests)
- Statistics & reporting

---

## 🏗️ **Next.js 14 Project Structure**

```
hotel-management-frontend/
├── src/
│   ├── app/                          # Next.js 14 App Router
│   │   ├── (auth)/                   # Auth layout group
│   │   │   ├── login/
│   │   │   │   └── page.tsx
│   │   │   └── register/
│   │   │       └── page.tsx
│   │   │
│   │   ├── (dashboard)/              # Dashboard layout group
│   │   │   ├── layout.tsx            # Sidebar + Header
│   │   │   ├── page.tsx              # Dashboard home
│   │   │   │
│   │   │   ├── hotels/
│   │   │   │   ├── page.tsx          # Hotels list
│   │   │   │   ├── new/
│   │   │   │   │   └── page.tsx      # Create hotel
│   │   │   │   └── [id]/
│   │   │   │       ├── page.tsx      # Hotel details
│   │   │   │       └── edit/
│   │   │   │           └── page.tsx  # Edit hotel
│   │   │   │
│   │   │   ├── rooms/
│   │   │   │   ├── page.tsx          # Rooms list
│   │   │   │   ├── new/
│   │   │   │   │   └── page.tsx
│   │   │   │   └── [id]/
│   │   │   │       ├── page.tsx      # Room details
│   │   │   │       └── edit/
│   │   │   │           └── page.tsx
│   │   │   │
│   │   │   ├── reservations/
│   │   │   │   ├── page.tsx          # Reservations list
│   │   │   │   ├── calendar/
│   │   │   │   │   └── page.tsx      # Calendar view
│   │   │   │   ├── new/
│   │   │   │   │   └── page.tsx      # Create booking
│   │   │   │   └── [id]/
│   │   │   │       ├── page.tsx      # Reservation details
│   │   │   │       └── edit/
│   │   │   │           └── page.tsx
│   │   │   │
│   │   │   ├── guests/
│   │   │   │   ├── page.tsx          # Guests directory
│   │   │   │   ├── new/
│   │   │   │   │   └── page.tsx
│   │   │   │   └── [id]/
│   │   │   │       ├── page.tsx
│   │   │   │       └── edit/
│   │   │   │           └── page.tsx
│   │   │   │
│   │   │   ├── users/                # SuperAdmin only
│   │   │   │   ├── page.tsx
│   │   │   │   ├── new/
│   │   │   │   │   └── page.tsx
│   │   │   │   └── [id]/
│   │   │   │       └── page.tsx
│   │   │   │
│   │   │   └── reports/              # Statistics
│   │   │       └── page.tsx
│   │   │
│   │   ├── layout.tsx                # Root layout
│   │   ├── globals.css               # Global styles
│   │   └── providers.tsx             # React Query provider
│   │
│   ├── components/
│   │   ├── ui/                       # shadcn/ui components
│   │   │   ├── button.tsx
│   │   │   ├── card.tsx
│   │   │   ├── dialog.tsx
│   │   │   ├── dropdown-menu.tsx
│   │   │   ├── form.tsx
│   │   │   ├── input.tsx
│   │   │   ├── select.tsx
│   │   │   ├── table.tsx
│   │   │   ├── toast.tsx
│   │   │   ├── badge.tsx
│   │   │   ├── calendar.tsx
│   │   │   └── ...
│   │   │
│   │   ├── layout/
│   │   │   ├── Sidebar.tsx
│   │   │   ├── Header.tsx
│   │   │   ├── UserMenu.tsx
│   │   │   └── NavigationMenu.tsx
│   │   │
│   │   ├── forms/
│   │   │   ├── HotelForm.tsx
│   │   │   ├── RoomForm.tsx
│   │   │   ├── GuestForm.tsx
│   │   │   ├── ReservationForm.tsx
│   │   │   └── UserForm.tsx
│   │   │
│   │   ├── tables/
│   │   │   ├── HotelsTable.tsx
│   │   │   ├── RoomsTable.tsx
│   │   │   ├── GuestsTable.tsx
│   │   │   ├── ReservationsTable.tsx
│   │   │   └── UsersTable.tsx
│   │   │
│   │   ├── cards/
│   │   │   ├── StatCard.tsx
│   │   │   ├── HotelCard.tsx
│   │   │   ├── RoomCard.tsx
│   │   │   └── ReservationCard.tsx
│   │   │
│   │   ├── charts/
│   │   │   ├── RevenueChart.tsx
│   │   │   ├── OccupancyChart.tsx
│   │   │   └── BookingTrendsChart.tsx
│   │   │
│   │   └── shared/
│   │       ├── StatusBadge.tsx
│   │       ├── RoleBadge.tsx
│   │       ├── LoadingSpinner.tsx
│   │       ├── EmptyState.tsx
│   │       ├── ErrorAlert.tsx
│   │       └── ConfirmDialog.tsx
│   │
│   ├── lib/
│   │   ├── api/                      # API client
│   │   │   ├── client.ts             # Axios instance
│   │   │   ├── auth.ts
│   │   │   ├── hotels.ts
│   │   │   ├── rooms.ts
│   │   │   ├── guests.ts
│   │   │   ├── reservations.ts
│   │   │   └── users.ts
│   │   │
│   │   ├── hooks/                    # Custom React hooks
│   │   │   ├── useAuth.ts
│   │   │   ├── useHotels.ts
│   │   │   ├── useRooms.ts
│   │   │   ├── useGuests.ts
│   │   │   ├── useReservations.ts
│   │   │   └── useUsers.ts
│   │   │
│   │   ├── utils/
│   │   │   ├── date.ts               # Date formatting
│   │   │   ├── currency.ts           # Price formatting
│   │   │   ├── enums.ts              # Enum helpers
│   │   │   ├── validation.ts         # Form validation
│   │   │   └── cn.ts                 # Class name utility
│   │   │
│   │   └── constants.ts              # Constants
│   │
│   ├── types/
│   │   ├── api.ts                    # API response types
│   │   ├── hotel.ts
│   │   ├── room.ts
│   │   ├── guest.ts
│   │   ├── reservation.ts
│   │   ├── user.ts
│   │   └── enums.ts                  # All enums
│   │
│   ├── store/                        # State management
│   │   ├── authStore.ts              # Zustand store for auth
│   │   ├── hotelStore.ts             # Selected hotel context
│   │   └── uiStore.ts                # UI state (sidebar, theme)
│   │
│   └── middleware.ts                 # Auth middleware
│
├── public/
│   ├── images/
│   └── icons/
│
├── .env.local                        # Environment variables
├── next.config.js
├── tailwind.config.ts
├── tsconfig.json
├── components.json                   # shadcn/ui config
└── package.json
```

---

## 🎨 **Tech Stack Details**

### **Core:**
```json
{
  "framework": "Next.js 14",
  "language": "TypeScript",
  "styling": "TailwindCSS",
  "components": "shadcn/ui",
  "icons": "Lucide React",
  "dataFetching": "TanStack Query (React Query)",
  "forms": "React Hook Form",
  "validation": "Zod",
  "state": "Zustand",
  "http": "Axios",
  "charts": "Recharts",
  "dates": "date-fns"
}
```

---

## 📦 **Key Dependencies**

```json
{
  "dependencies": {
    "next": "^14.0.0",
    "react": "^18.2.0",
    "react-dom": "^18.2.0",
    "typescript": "^5.3.0",
    
    "axios": "^1.6.0",
    "@tanstack/react-query": "^5.0.0",
    "zustand": "^4.4.0",
    
    "react-hook-form": "^7.48.0",
    "zod": "^3.22.0",
    "@hookform/resolvers": "^3.3.0",
    
    "tailwindcss": "^3.4.0",
    "@radix-ui/react-*": "latest",
    "lucide-react": "^0.294.0",
    "class-variance-authority": "^0.7.0",
    "clsx": "^2.0.0",
    "tailwind-merge": "^2.0.0",
    
    "recharts": "^2.10.0",
    "date-fns": "^3.0.0"
  },
  "devDependencies": {
    "@types/node": "^20.0.0",
    "@types/react": "^18.2.0",
    "eslint": "^8.0.0",
    "eslint-config-next": "^14.0.0"
  }
}
```

---

## 🔐 **Authentication Flow**

```typescript
// lib/store/authStore.ts
interface AuthState {
  token: string | null;
  user: {
    email: string;
    fullName: string;
    roles: string[];
  } | null;
  isAuthenticated: boolean;
  login: (credentials: LoginDto) => Promise<void>;
  logout: () => void;
  hasRole: (role: string) => boolean;
}

// Usage in components
const { user, hasRole, logout } = useAuthStore();

if (hasRole('SuperAdmin')) {
  // Show admin features
}
```

---

## 🎯 **Component Examples**

### **1. Status Badge Component**

```tsx
// components/shared/StatusBadge.tsx
interface StatusBadgeProps {
  status: ReservationStatus | RoomStatus;
  type: 'reservation' | 'room';
}

export function StatusBadge({ status, type }: StatusBadgeProps) {
  const colors = {
    reservation: {
      Pending: 'bg-yellow-100 text-yellow-800',
      Confirmed: 'bg-blue-100 text-blue-800',
      CheckedIn: 'bg-green-100 text-green-800',
      CheckedOut: 'bg-gray-100 text-gray-800',
      Cancelled: 'bg-red-100 text-red-800',
      NoShow: 'bg-orange-100 text-orange-800',
    },
    room: {
      Available: 'bg-green-100 text-green-800',
      Occupied: 'bg-blue-100 text-blue-800',
      Cleaning: 'bg-yellow-100 text-yellow-800',
      Maintenance: 'bg-orange-100 text-orange-800',
      OutOfService: 'bg-red-100 text-red-800',
      Reserved: 'bg-purple-100 text-purple-800',
    }
  };
  
  return (
    <Badge className={colors[type][status]}>
      {statusLabels[status]}
    </Badge>
  );
}
```

### **2. Reservation Form**

```tsx
// components/forms/ReservationForm.tsx
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';

const reservationSchema = z.object({
  hotelId: z.number(),
  roomId: z.number(),
  guestId: z.number(),
  bookingType: z.enum(['Daily', 'ShortStay']),
  checkInDate: z.date(),
  checkOutDate: z.date(),
  numberOfGuests: z.number().min(1).max(20),
  depositAmount: z.number().min(0),
  paymentMethod: z.enum(['Cash', 'CreditCard', ...]),
});

export function ReservationForm() {
  const form = useForm({
    resolver: zodResolver(reservationSchema),
  });
  
  // Room availability check
  const { data: isAvailable } = useRoomAvailability(
    form.watch('roomId'),
    form.watch('checkInDate'),
    form.watch('checkOutDate')
  );
  
  // Auto-calculate price
  const { data: calculatedPrice } = useCalculatePrice(
    form.watch('roomId'),
    form.watch('bookingType'),
    form.watch('checkInDate'),
    form.watch('checkOutDate')
  );
  
  return (
    <Form {...form}>
      {/* Form fields */}
    </Form>
  );
}
```

### **3. Dashboard Page**

```tsx
// app/(dashboard)/page.tsx
export default async function DashboardPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Dashboard</h1>
        <RoleBasedContent allowedRoles={['Admin', 'Manager']}>
          <Button>Create Reservation</Button>
        </RoleBasedContent>
      </div>
      
      {/* Statistics Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <StatCard
          title="Total Reservations"
          value={stats.totalReservations}
          icon={CalendarIcon}
        />
        <StatCard
          title="Revenue (This Month)"
          value={formatCurrency(stats.revenue)}
          icon={DollarSignIcon}
        />
        <StatCard
          title="Occupancy Rate"
          value={`${stats.occupancy}%`}
          icon={BedIcon}
        />
        <StatCard
          title="Pending Check-ins"
          value={stats.pendingCheckIns}
          icon={ClockIcon}
        />
      </div>
      
      {/* Charts */}
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Revenue Trends</CardTitle>
          </CardHeader>
          <CardContent>
            <RevenueChart data={stats.monthlyRevenue} />
          </CardContent>
        </Card>
        
        <Card>
          <CardHeader>
            <CardTitle>Booking Status</CardTitle>
          </CardHeader>
          <CardContent>
            <BookingStatusChart data={stats.byStatus} />
          </CardContent>
        </Card>
      </div>
      
      {/* Recent Reservations */}
      <Card>
        <CardHeader>
          <CardTitle>Recent Reservations</CardTitle>
        </CardHeader>
        <CardContent>
          <RecentReservationsTable />
        </CardContent>
      </Card>
    </div>
  );
}
```

---

## 🎨 **Role-Based UI Components**

```tsx
// components/shared/RoleBasedContent.tsx
interface RoleBasedContentProps {
  allowedRoles: string[];
  children: React.ReactNode;
  fallback?: React.ReactNode;
}

export function RoleBasedContent({ 
  allowedRoles, 
  children, 
  fallback 
}: RoleBasedContentProps) {
  const { user } = useAuthStore();
  
  const hasPermission = user?.roles.some(role => 
    allowedRoles.includes(role)
  );
  
  if (!hasPermission) return fallback || null;
  
  return <>{children}</>;
}

// Usage
<RoleBasedContent allowedRoles={['SuperAdmin', 'Admin']}>
  <Button onClick={deleteHotel}>Delete Hotel</Button>
</RoleBasedContent>
```

---

## 📊 **Data Fetching Pattern**

```typescript
// lib/hooks/useReservations.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { reservationApi } from '@/lib/api/reservations';

export function useReservations() {
  return useQuery({
    queryKey: ['reservations'],
    queryFn: reservationApi.getAll,
  });
}

export function useCreateReservation() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: reservationApi.create,
    onSuccess: () => {
      // Invalidate and refetch
      queryClient.invalidateQueries(['reservations']);
      toast.success('Reservation created successfully');
    },
    onError: (error) => {
      toast.error('Failed to create reservation');
    },
  });
}

export function useConfirmReservation() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: reservationApi.confirm,
    onSuccess: () => {
      queryClient.invalidateQueries(['reservations']);
    },
  });
}
```

---

## 🎯 **Priority Implementation Order**

### **Phase 1: Foundation (Week 1)**
1. ✅ Project setup with Next.js 14 + TypeScript
2. ✅ Install dependencies (TailwindCSS, shadcn/ui, etc.)
3. ✅ Configure TypeScript types from backend
4. ✅ Setup API client with Axios
5. ✅ Authentication pages (Login/Register)
6. ✅ Auth store with Zustand
7. ✅ Protected route middleware
8. ✅ Dashboard layout (Sidebar + Header)

### **Phase 2: Core Features (Week 2)**
9. ✅ Hotels management (List, Create, Edit, Delete)
10. ✅ Rooms management (List, Create, Edit)
11. ✅ Room status management UI
12. ✅ Guests directory (List, Create, Edit)
13. ✅ User management (SuperAdmin only)

### **Phase 3: Reservations (Week 3)**
14. ✅ Reservations list with filters
15. ✅ Create reservation form
16. ✅ Room availability checker
17. ✅ Reservation details page
18. ✅ Check-in/Check-out interface
19. ✅ Payment recording UI
20. ✅ Calendar view for reservations

### **Phase 4: Polish & Reports (Week 4)**
21. ✅ Dashboard statistics
22. ✅ Revenue charts
23. ✅ Occupancy reports
24. ✅ Mobile responsive design
25. ✅ Error handling & loading states
26. ✅ Toast notifications
27. ✅ Search & filters
28. ✅ Export functionality

---

## 🎨 **Design System**

### **Colors:**
```css
/* Primary (Blue) */
--primary: 222.2 47.4% 11.2%;
--primary-foreground: 210 40% 98%;

/* Status Colors */
--success: 142 76% 36%;  /* Green */
--warning: 38 92% 50%;   /* Orange */
--error: 0 84% 60%;      /* Red */
--info: 221 83% 53%;     /* Blue */
```

### **Typography:**
```css
font-family: 'Inter', sans-serif;

h1: 2.25rem (36px) - Bold
h2: 1.875rem (30px) - SemiBold
h3: 1.5rem (24px) - SemiBold
body: 1rem (16px) - Regular
small: 0.875rem (14px) - Regular
```

---

## 📱 **Responsive Breakpoints**

```typescript
const breakpoints = {
  sm: '640px',   // Mobile
  md: '768px',   // Tablet
  lg: '1024px',  // Desktop
  xl: '1280px',  // Large Desktop
  '2xl': '1536px' // Extra Large
};
```

---

## ✅ **What's Next?**

**Ready to scaffold the project!** I can:

1. Create the complete Next.js project structure
2. Generate all TypeScript types from your backend
3. Build the API client layer
4. Create authentication flow
5. Build the first few pages (Dashboard, Hotels, Rooms)
6. Setup shadcn/ui components

**Would you like me to start creating the Next.js project?** 🚀
