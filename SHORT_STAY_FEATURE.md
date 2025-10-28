# 🕐 Short-Stay / Hourly Booking Feature

## 🎯 **Purpose**

Enable hotels to offer **short-stay (hourly) bookings** alongside traditional nightly stays. Perfect for hotels offering day-use rooms, meeting rooms, or hourly accommodations.

---

## ✅ **What Was Implemented**

### **Dual-Pricing Model**
Rooms can now support:
1. ✅ **Daily bookings** (traditional overnight stays)
2. ✅ **Short-stay bookings** (hourly rentals)
3. ✅ **Both** (room available for both booking types)

---

## 📊 **Room Entity Extensions**

### **New Properties Added to Room:**

```csharp
// Short Stay Support
public bool AllowsShortStay { get; set; } = false;
public decimal? ShortStayHourlyRate { get; set; }    // Price per hour
public int? MinimumShortStayHours { get; set; } = 2; // Minimum: 2 hours
public int? MaximumShortStayHours { get; set; } = 12; // Maximum: 12 hours

// Existing (unchanged)
public decimal PricePerNight { get; set; }           // Daily rate
```

---

## 🎭 **Room Configuration Types**

### **1. Daily-Only Room (Traditional)**
```json
{
  "allowsShortStay": false,
  "pricePerNight": 150.00,
  "shortStayHourlyRate": null,
  "minimumShortStayHours": null,
  "maximumShortStayHours": null
}
```
**Use Case:** Regular hotel room, overnight stays only

---

### **2. Short-Stay + Daily Room (Dual Mode)**
```json
{
  "allowsShortStay": true,
  "pricePerNight": 150.00,
  "shortStayHourlyRate": 25.00,
  "minimumShortStayHours": 2,
  "maximumShortStayHours": 12
}
```
**Use Case:** Room available for both overnight and hourly bookings

---

### **3. Short-Stay Only Room**
```json
{
  "allowsShortStay": true,
  "pricePerNight": 0,
  "shortStayHourlyRate": 30.00,
  "minimumShortStayHours": 4,
  "maximumShortStayHours": 8
}
```
**Use Case:** Meeting room, day-use room, hourly rentals only

---

## ✅ **Validation Rules**

### **Implemented in RoomDtoValidator:**

1. ✅ **If `AllowsShortStay = true`, then `ShortStayHourlyRate` is required**
   ```
   Error: "Hourly rate is required when short stay is enabled"
   ```

2. ✅ **Hourly rate must be > 0 and ≤ 10,000**
   ```
   Error: "Hourly rate must be greater than 0"
   Error: "Hourly rate cannot exceed 10,000"
   ```

3. ✅ **Minimum hours: 1-24**
   ```
   Error: "Minimum hours must be between 1 and 24"
   ```

4. ✅ **Maximum hours: 1-24**
   ```
   Error: "Maximum hours must be between 1 and 24"
   ```

5. ✅ **Maximum ≥ Minimum**
   ```
   Error: "Maximum hours must be greater than or equal to minimum hours"
   ```

---

## 🌐 **API Endpoints**

### **1. Create Short-Stay Room**
```http
POST /api/Rooms
Authorization: Bearer {admin-token}
```

**Request:**
```json
{
  "hotelId": 1,
  "roomNumber": "201",
  "type": "Deluxe",
  "capacity": 2,
  "pricePerNight": 150.00,
  "allowsShortStay": true,
  "shortStayHourlyRate": 25.00,
  "minimumShortStayHours": 2,
  "maximumShortStayHours": 12,
  "description": "Deluxe room with short-stay option",
  "amenities": "WiFi,TV,AC,Minibar"
}
```

**Response: 201 Created**
```json
{
  "id": 42,
  "hotelId": 1,
  "roomNumber": "201",
  "type": "Deluxe",
  "pricePerNight": 150.00,
  "allowsShortStay": true,
  "shortStayHourlyRate": 25.00,
  "minimumShortStayHours": 2,
  "maximumShortStayHours": 12,
  "status": "Available",
  "isActive": true
}
```

---

### **2. Get Short-Stay Rooms for Hotel**
```http
GET /api/Rooms/hotel/1/short-stay
Authorization: Bearer {token}
```

**Response:**
```json
[
  {
    "id": 42,
    "roomNumber": "201",
    "type": "Deluxe",
    "allowsShortStay": true,
    "shortStayHourlyRate": 25.00,
    "minimumShortStayHours": 2,
    "maximumShortStayHours": 12
  },
  {
    "id": 43,
    "roomNumber": "202",
    "type": "Suite",
    "allowsShortStay": true,
    "shortStayHourlyRate": 40.00,
    "minimumShortStayHours": 4,
    "maximumShortStayHours": 8
  }
]
```

---

### **3. Update Room to Enable Short-Stay**
```http
PUT /api/Rooms/42
Authorization: Bearer {admin-token}
```

**Request:**
```json
{
  "hotelId": 1,
  "roomNumber": "201",
  "type": "Deluxe",
  "capacity": 2,
  "pricePerNight": 150.00,
  "allowsShortStay": true,
  "shortStayHourlyRate": 30.00,
  "minimumShortStayHours": 3,
  "maximumShortStayHours": 10
}
```

---

## 💰 **Pricing Calculation Examples**

### **Scenario 1: 4-Hour Short Stay**
```
Room: Deluxe #201
Hourly Rate: $25/hour
Booking: 2:00 PM - 6:00 PM (4 hours)

Calculation: 4 hours × $25 = $100
```

---

### **Scenario 2: Overnight Stay**
```
Room: Deluxe #201
Daily Rate: $150/night
Booking: Oct 20 2:00 PM - Oct 21 12:00 PM (1 night)

Calculation: 1 night × $150 = $150
```

---

### **Scenario 3: Maximum Stay**
```
Room: Suite #202
Hourly Rate: $40/hour
Maximum: 8 hours
Booking: 9:00 AM - 5:00 PM (8 hours)

Calculation: 8 hours × $40 = $320
```

---

## 🔄 **Reservation Integration (Phase 1.3)**

When implementing reservations, add:

### **BookingType Enum:**
```csharp
public enum BookingType
{
    Daily,      // Traditional overnight stay
    ShortStay   // Hourly booking
}
```

### **Reservation Extensions:**
```csharp
public class Reservation
{
    // Existing fields...
    public BookingType BookingType { get; set; } = BookingType.Daily;
    public int? DurationInHours { get; set; }  // For short stays
    
    // Calculated price based on booking type
}
```

### **Pricing Service Method:**
```csharp
public decimal CalculateReservationPrice(
    Room room, 
    DateTime checkIn, 
    DateTime checkOut, 
    BookingType bookingType)
{
    if (bookingType == BookingType.ShortStay)
    {
        if (!room.AllowsShortStay)
            throw new InvalidOperationException(
                "Room does not support short stays");
            
        var hours = (int)Math.Ceiling((checkOut - checkIn).TotalHours);
        
        if (hours < room.MinimumShortStayHours)
            throw new InvalidOperationException(
                $"Minimum stay is {room.MinimumShortStayHours} hours");
            
        if (hours > room.MaximumShortStayHours)
            throw new InvalidOperationException(
                $"Maximum stay is {room.MaximumShortStayHours} hours");
            
        return hours * room.ShortStayHourlyRate.Value;
    }
    else // Daily booking
    {
        var nights = (checkOut.Date - checkIn.Date).Days;
        if (nights < 1) nights = 1; // At least 1 night
        return nights * room.PricePerNight;
    }
}
```

---

## 🧪 **Testing in Swagger**

### **Test 1: Create Short-Stay Room**
1. Login as Admin
2. `POST /api/Rooms`
3. Use request body above
4. **Expected:** Room created with short-stay enabled

---

### **Test 2: Validation - Missing Hourly Rate**
```json
{
  "allowsShortStay": true,
  "shortStayHourlyRate": null
}
```
**Expected:** 400 Bad Request
**Error:** "Hourly rate is required when short stay is enabled"

---

### **Test 3: Validation - Max < Min**
```json
{
  "allowsShortStay": true,
  "shortStayHourlyRate": 25.00,
  "minimumShortStayHours": 8,
  "maximumShortStayHours": 4
}
```
**Expected:** 400 Bad Request
**Error:** "Maximum hours must be greater than or equal to minimum hours"

---

### **Test 4: Get Short-Stay Rooms**
```http
GET /api/Rooms/hotel/1/short-stay
```
**Expected:** List of rooms where `allowsShortStay = true`

---

## 📁 **Files Modified**

1. ✅ `Models/Entities/Room.cs` - Added 4 short-stay properties
2. ✅ `Models/DTOs/RoomDto.cs` - Added 4 short-stay properties with validation
3. ✅ `Validators/RoomDtoValidator.cs` - Added 5 validation rules
4. ✅ `Controllers/RoomsController.cs` - Added `GET /api/Rooms/hotel/{id}/short-stay`

**Database:**
- ✅ Migration: `AddShortStaySupport`
- ✅ Added 4 columns to `Rooms` table

---

## 🗄️ **Database Changes**

```sql
ALTER TABLE [Rooms] ADD [AllowsShortStay] bit NOT NULL DEFAULT 0;
ALTER TABLE [Rooms] ADD [ShortStayHourlyRate] decimal(10,2) NULL;
ALTER TABLE [Rooms] ADD [MinimumShortStayHours] int NULL;
ALTER TABLE [Rooms] ADD [MaximumShortStayHours] int NULL;
```

**Existing rooms:** All default to `AllowsShortStay = false` (no breaking changes!)

---

## 💡 **Use Cases**

### **Use Case 1: Business Hotel**
```
Rooms 101-120: Regular rooms (daily only)
Rooms 201-210: Meeting rooms (short-stay only, 2-8 hours)
Rooms 301-305: Executive suites (both daily & short-stay)
```

---

### **Use Case 2: Airport Hotel**
```
Day-use rooms: 4-12 hour stays for travelers in transit
Overnight rooms: Traditional nightly bookings
```

---

### **Use Case 3: Resort Hotel**
```
Pool cabanas: 2-6 hour rentals
Spa rooms: 1-4 hour sessions
Hotel rooms: Nightly stays
```

---

## ✅ **Benefits**

| Benefit | Description |
|---------|-------------|
| **Flexible Pricing** | Rooms can offer both hourly and daily rates |
| **Revenue Optimization** | Maximize room utilization during off-peak hours |
| **Simple Implementation** | No complex tables, just 4 new fields |
| **Backward Compatible** | Existing rooms unaffected (default = false) |
| **Clear Validation** | Business rules enforced at API level |
| **Easy to Query** | Filter short-stay rooms with single flag |

---

## 🚀 **Next Steps (Phase 1.3: Reservations)**

When building the reservation system:

1. ✅ Add `BookingType` enum (Daily/ShortStay)
2. ✅ Add `DurationInHours` to Reservation entity
3. ✅ Implement pricing calculation service
4. ✅ Validate booking duration against room limits
5. ✅ Show appropriate UI for short-stay vs daily bookings
6. ✅ Generate invoices with correct pricing

---

## 🎯 **Summary**

**Short-stay support is now live!**

- ✅ **4 new room properties** added
- ✅ **Validation rules** enforced
- ✅ **API endpoint** to filter short-stay rooms
- ✅ **Database migration** applied
- ✅ **Backward compatible** with existing rooms
- ✅ **Ready for reservations** (Phase 1.3)

Hotels can now offer:
- 📅 **Daily bookings** (traditional)
- 🕐 **Hourly bookings** (2-24 hours)
- 🎭 **Both** (maximum flexibility)

**The system is ready for short-stay reservations!** 🎉
