# Database Migration Commands

## Create New Migration for Extended Hotel Model

Run this command to create a migration for the updated Hotel entity:

```bash
dotnet ef migrations add ExtendHotelModel
```

## Apply Migration to Database

```bash
dotnet ef database update
```

## If You Need to Remove the Last Migration (Before Applying)

```bash
dotnet ef migrations remove
```

## View Migration SQL (Without Applying)

```bash
dotnet ef migrations script
```

---

## What This Migration Includes:

### **Hotel Table Changes:**
- ✅ Added `OwnerId` (FK to AspNetUsers)
- ✅ Added `Description`
- ✅ Added `Address`, `City`, `Country`, `PostalCode`
- ✅ Added `PhoneNumber`, `Email`, `Website`
- ✅ Added `Stars`, `Rating`, `TotalReviews`
- ✅ Added `Amenities`
- ✅ Added `CheckInTime`, `CheckOutTime`
- ✅ Added `IsActive`
- ✅ Added `UpdatedAt`

### **New Tables:**
- ✅ Rooms (placeholder - will be expanded in Phase 1.1)
- ✅ Reservations (placeholder - will be expanded in Phase 1.3)

### **Relationships:**
- ✅ Hotel → Owner (ApplicationUser)
- ✅ Hotel → Rooms (1-to-many)
- ✅ Hotel → Reservations (1-to-many)
- ✅ Room → Hotel (many-to-1)
- ✅ Room → Reservations (1-to-many)
- ✅ Reservation → Hotel (many-to-1)
- ✅ Reservation → Room (many-to-1)

---

## After Migration:

Test in Swagger:
1. Register an Admin user
2. Login to get JWT token
3. Create a hotel with all the new fields
4. Verify OwnerId is set automatically
5. Try to access another admin's hotel (should be forbidden)
6. Test as SuperAdmin (should see all hotels)
