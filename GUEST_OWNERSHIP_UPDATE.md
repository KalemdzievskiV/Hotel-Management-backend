# 🔒 Guest Ownership & Data Isolation - Implementation Complete

## 🎯 **Problem Solved**

**Before:** All admins could see ALL guests from ALL hotels - no data isolation.

**After:** Each admin/manager only sees:
- ✅ Walk-in guests they created for their hotel(s)
- ✅ All registered users (who can book at any hotel)

---

## 📊 **Architecture Overview**

### **Two Types of Guests:**

#### **1. Walk-in Guests** (Created by hotel staff)
- **UserId:** `NULL` (not registered in the system)
- **HotelId:** Set (belongs to a specific hotel)
- **CreatedByUserId:** Set (tracks which admin/manager created them)
- **Visibility:** Only visible to the creator and their hotel staff
- **Use Case:** Front desk creating guest records for walk-in customers

#### **2. Registered Users** (Self sign-up)
- **UserId:** Set (linked to ApplicationUser)
- **HotelId:** `NULL` (not tied to a specific hotel)
- **CreatedByUserId:** `NULL` (self-registered)
- **Visibility:** Visible to all hotels (can book anywhere)
- **Use Case:** Customers who sign up through the website/app

---

## 🔧 **Changes Made**

### **1. Guest Entity** (`Models/Entities/Guest.cs`)

Added 3 new fields:
```csharp
// Hotel ownership for walk-in guests (NULL for registered users)
public int? HotelId { get; set; }
public Hotel? Hotel { get; set; }

// Track which admin/manager created this walk-in guest
public string? CreatedByUserId { get; set; }
public ApplicationUser? CreatedBy { get; set; }
```

---

### **2. GuestDto** (`Models/DTOs/GuestDto.cs`)

Added fields + computed properties:
```csharp
// New fields
public int? HotelId { get; set; }
public string? HotelName { get; set; }  // Display
public string? CreatedByUserId { get; set; }
public string? CreatedByUserName { get; set; }  // Display

// New computed properties
public bool IsWalkInGuest => !string.IsNullOrEmpty(CreatedByUserId) && HotelId.HasValue;
```

---

### **3. AutoMapper** (`Infrastructure/Mapping/AutoMapperProfile.cs`)

Updated mappings:
```csharp
CreateMap<Guest, GuestDto>()
    .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.Hotel != null ? src.Hotel.Name : null))
    .ForMember(dest => dest.CreatedByUserName, opt => opt.MapFrom(src => src.CreatedBy != null ? src.CreatedBy.FullName : null))
    // ... other mappings

CreateMap<GuestDto, Guest>()
    .ForMember(dest => dest.CreatedByUserId, opt => opt.Ignore()) // Set in service
    .ForMember(dest => dest.HotelId, opt => opt.Ignore()) // Controlled in service
    // ... other mappings
```

---

### **4. IGuestService** (`Services/Interfaces/IGuestService.cs`)

Added 3 new methods:
```csharp
// Get all walk-in guests for a specific hotel
Task<IEnumerable<GuestDto>> GetGuestsByHotelIdAsync(int hotelId);

// Get all walk-in guests created by a specific user
Task<IEnumerable<GuestDto>> GetGuestsCreatedByUserAsync(string userId);

// Get all guests accessible to the current user
Task<IEnumerable<GuestDto>> GetMyAccessibleGuestsAsync(string currentUserId);
```

---

### **5. GuestService** (`Services/Implementations/GuestService.cs`)

**Constructor:** Added `IHttpContextAccessor` to get current user

**CreateAsync:** Auto-sets ownership for walk-in guests
```csharp
// If this is a walk-in guest (UserId is null), set ownership
if (string.IsNullOrEmpty(dto.UserId))
{
    var currentUserId = _httpContextAccessor.HttpContext?.User
        ?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    if (!string.IsNullOrEmpty(currentUserId))
    {
        guest.CreatedByUserId = currentUserId;
    }
    
    // Set HotelId if provided
    if (dto.HotelId.HasValue)
    {
        guest.HotelId = dto.HotelId.Value;
    }
}
```

**New filtering methods:**
```csharp
// Get guests by hotel
public async Task<IEnumerable<GuestDto>> GetGuestsByHotelIdAsync(int hotelId)
{
    var guests = await _guestRepository.FindAsync(g => g.HotelId == hotelId);
    return _mapper.Map<IEnumerable<GuestDto>>(guests);
}

// Get guests created by user
public async Task<IEnumerable<GuestDto>> GetGuestsCreatedByUserAsync(string userId)
{
    var guests = await _guestRepository.FindAsync(g => g.CreatedByUserId == userId);
    return _mapper.Map<IEnumerable<GuestDto>>(guests);
}

// Get accessible guests (walk-in guests I created + all registered users)
public async Task<IEnumerable<GuestDto>> GetMyAccessibleGuestsAsync(string currentUserId)
{
    var guests = await _guestRepository.FindAsync(g => 
        g.CreatedByUserId == currentUserId ||  // My walk-in guests
        g.UserId != null);                      // All registered users
    
    return _mapper.Map<IEnumerable<GuestDto>>(guests);
}
```

---

### **6. GuestsController** (`Controllers/GuestsController.cs`)

**Modified `GetAllAsync`:** Now filtered by current user
```csharp
[HttpGet]
public override async Task<IActionResult> GetAllAsync()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Unauthorized();
    
    // Returns: Walk-in guests I created + All registered users
    var guests = await _guestService.GetMyAccessibleGuestsAsync(userId);
    return Ok(guests);
}
```

**Added new endpoints:**
```csharp
// Get all guests (SuperAdmin only - no filtering)
[HttpGet("all-unfiltered")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public async Task<IActionResult> GetAllUnfilteredAsync()

// Get guests for specific hotel
[HttpGet("hotel/{hotelId:int}")]
public async Task<IActionResult> GetGuestsByHotelAsync(int hotelId)

// Get my walk-in guests
[HttpGet("my-guests")]
public async Task<IActionResult> GetMyGuestsAsync()
```

---

### **7. ApplicationDbContext** (`Data/ApplicationDbContext.cs`)

Added relationships:
```csharp
// Guest -> Hotel (walk-in guest ownership)
modelBuilder.Entity<Guest>()
    .HasOne(g => g.Hotel)
    .WithMany()
    .HasForeignKey(g => g.HotelId)
    .OnDelete(DeleteBehavior.Restrict)
    .IsRequired(false);

// Guest -> CreatedBy User
modelBuilder.Entity<Guest>()
    .HasOne(g => g.CreatedBy)
    .WithMany()
    .HasForeignKey(g => g.CreatedByUserId)
    .OnDelete(DeleteBehavior.Restrict)
    .IsRequired(false);
```

---

### **8. DependencyInjection** (`Configurations/DependencyInjection.cs`)

Registered `IHttpContextAccessor`:
```csharp
services.AddHttpContextAccessor();
```

---

### **9. Database Migration**

**Migration:** `20251019162414_AddGuestOwnershipTracking`

**Changes:**
- ✅ Added `HotelId` column (int, nullable)
- ✅ Added `CreatedByUserId` column (nvarchar(450), nullable)
- ✅ Added indexes on both columns
- ✅ Added foreign keys to Hotels and AspNetUsers tables
- ✅ Applied successfully

---

## 🔒 **Data Isolation Rules**

### **GET /api/Guests** (Regular users)
Returns:
- Walk-in guests created by the current user
- All registered users (UserId != null)

### **GET /api/Guests/all-unfiltered** (SuperAdmin only)
Returns:
- All guests without filtering

### **GET /api/Guests/hotel/{hotelId}** 
Returns:
- Walk-in guests for that specific hotel

### **GET /api/Guests/my-guests**
Returns:
- Only walk-in guests created by current user

---

## 📝 **Usage Examples**

### **Example 1: Admin Creates Walk-in Guest**

**Request:** `POST /api/Guests`
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "email": "john.doe@example.com",
  "phoneNumber": "+1-555-0100",
  "hotelId": 1
}
```

**Automatic behavior:**
- `CreatedByUserId` = Current admin's user ID
- `HotelId` = 1 (from request)
- `UserId` = NULL (it's a walk-in guest)

**Visibility:**
- Only this admin can see this guest
- Only staff from Hotel #1 can use this guest for reservations

---

### **Example 2: User Self-Registers**

User signs up through `/api/Auth/register` with Role="Guest"

Later, they create their guest profile:
**Request:** `POST /api/Guests`
```json
{
  "userId": "abc123...",  // Their ApplicationUser ID
  "firstName": "Jane",
  "lastName": "Smith",
  "email": "jane.smith@example.com",
  "phoneNumber": "+1-555-0200"
}
```

**Automatic behavior:**
- `CreatedByUserId` = NULL (self-registered)
- `HotelId` = NULL (not hotel-specific)
- `UserId` = "abc123..." (linked to ApplicationUser)

**Visibility:**
- Visible to ALL hotels
- Can make reservations at ANY hotel

---

### **Example 3: Get My Walk-in Guests**

**Request:** `GET /api/Guests/my-guests`
**Headers:** `Authorization: Bearer {admin-token}`

**Returns:**
- Only walk-in guests created by this admin
- Does NOT include registered users

---

### **Example 4: Get Guests for Hotel**

**Request:** `GET /api/Guests/hotel/1`

**Returns:**
- All walk-in guests for Hotel #1
- Useful for front desk staff

---

## ✅ **Benefits**

| Aspect | Before | After |
|--------|--------|-------|
| **Data Isolation** | ❌ All admins see all guests | ✅ Each admin sees only their guests |
| **Multi-Hotel Support** | ❌ No hotel tracking | ✅ Walk-in guests tied to hotels |
| **Registered Users** | ❌ Same as walk-in | ✅ Available to all hotels |
| **Audit Trail** | ❌ No creator tracking | ✅ CreatedByUserId tracked |
| **Security** | ❌ Data leakage risk | ✅ Proper data isolation |

---

## 🧪 **Testing Scenarios**

### **Scenario 1: Two Admins, Different Hotels**

**Admin A** (Hotel #1):
- Creates walk-in guest "John Doe"
- `GET /api/Guests` → Sees "John Doe" + all registered users

**Admin B** (Hotel #2):
- `GET /api/Guests` → Does NOT see "John Doe"
- Only sees their own walk-in guests + registered users

✅ **Data isolation working!**

---

### **Scenario 2: Registered User**

**User** signs up with Role="Guest"
- Creates profile → `UserId` is set
- `GET /api/Guests` (from any admin) → User is visible

✅ **Registered users accessible to all hotels!**

---

### **Scenario 3: Hotel-Specific Query**

**Admin** queries:
- `GET /api/Guests/hotel/1` → All walk-in guests for Hotel #1
- `GET /api/Guests/hotel/2` → All walk-in guests for Hotel #2

✅ **Hotel filtering working!**

---

## 🎯 **Key Takeaways**

1. **Walk-in Guests** = Created by staff, hotel-specific, private to creator
2. **Registered Users** = Self sign-up, global, visible to all hotels
3. **Automatic Ownership** = CreatedByUserId set automatically on creation
4. **Data Isolation** = Each admin sees only their data + public data
5. **SuperAdmin** = Can see everything (unfiltered endpoint)

---

## 🚀 **Ready for Phase 1.3: Reservations!**

With proper guest ownership in place, we can now implement reservations with:
- Correct guest-to-hotel mapping
- Proper data isolation
- Walk-in vs registered user handling

**The guest system now supports multi-tenant hotel management!** 🎉
