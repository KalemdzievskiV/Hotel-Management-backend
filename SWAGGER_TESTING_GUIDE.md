# 🧪 Swagger API Testing Guide

## 🎯 **Complete Test Workflow**

This guide will walk you through testing **all features** of the Hotel Management API using Swagger.

---

## 🚀 **Phase 1: Authentication Setup**

### **Step 1.1: Register Admin User**

1. **Expand** `POST /api/Auth/register`
2. **Click** "Try it out"
3. **Paste this JSON:**
```json
{
  "fullName": "Admin User",
  "email": "admin@hotel.com",
  "password": "Admin123!",
  "role": "Admin"
}
```
4. **Click** "Execute"
5. **Expected:** `200 OK` with success message

---

### **Step 1.2: Login to Get Token**

1. **Expand** `POST /api/Auth/login`
2. **Click** "Try it out"
3. **Paste this JSON:**
```json
{
  "email": "admin@hotel.com",
  "password": "Admin123!"
}
```
4. **Click** "Execute"
5. **Expected:** `200 OK` with a token
6. **COPY THE TOKEN** (you'll need it for all other requests)

Example response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "email": "admin@hotel.com",
  "fullName": "Admin User",
  "roles": ["Admin"]
}
```

---

### **Step 1.3: Authorize Swagger**

1. **Scroll to top** of Swagger page
2. **Click** the green "Authorize" button (🔓)
3. **Enter:** `Bearer <paste-your-token-here>`
   - Example: `Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
4. **Click** "Authorize"
5. **Click** "Close"
6. **You should now see** 🔒 (locked padlock) on all endpoints

**✅ You're now authenticated!** All requests will include your token.

---

## 🏨 **Phase 2: Hotel Management**

### **Step 2.1: Create Your First Hotel**

1. **Expand** `POST /api/Hotels`
2. **Click** "Try it out"
3. **Paste this JSON:**
```json
{
  "name": "Grand Plaza Hotel",
  "address": "123 Main Street",
  "city": "New York",
  "country": "USA",
  "postalCode": "10001",
  "stars": 5,
  "email": "contact@grandplaza.com",
  "phoneNumber": "+1-212-555-0100",
  "website": "https://www.grandplaza.com",
  "description": "Luxury 5-star hotel in the heart of Manhattan",
  "checkInTime": "15:00",
  "checkOutTime": "11:00",
  "rating": 4.8,
  "amenities": "WiFi,Pool,Gym,Spa,Restaurant,Bar,Parking",
  "parkingAvailable": true,
  "petFriendly": false
}
```
4. **Click** "Execute"
5. **Expected:** `201 Created`
6. **Note the `id`** in the response (you'll need it for rooms)

---

### **Step 2.2: Get All Hotels**

1. **Expand** `GET /api/Hotels`
2. **Click** "Try it out"
3. **Click** "Execute"
4. **Expected:** `200 OK` with array containing your hotel

**Verify:**
- ✅ Hotel has an ID
- ✅ `ownerId` is set (your user ID)
- ✅ `createdAt` timestamp is present
- ✅ All fields match what you entered

---

### **Step 2.3: Get Hotel by ID**

1. **Expand** `GET /api/Hotels/{id}`
2. **Click** "Try it out"
3. **Enter** the hotel ID from Step 2.1
4. **Click** "Execute"
5. **Expected:** `200 OK` with hotel details

---

### **Step 2.4: Update Hotel**

1. **Expand** `PUT /api/Hotels/{id}`
2. **Click** "Try it out"
3. **Enter** hotel ID in the path
4. **Modify the JSON** (change description, rating, etc.):
```json
{
  "id": 1,
  "name": "Grand Plaza Hotel & Spa",
  "address": "123 Main Street",
  "city": "New York",
  "country": "USA",
  "postalCode": "10001",
  "stars": 5,
  "email": "contact@grandplaza.com",
  "phoneNumber": "+1-212-555-0100",
  "website": "https://www.grandplaza.com",
  "description": "Updated: Award-winning luxury hotel with world-class spa",
  "checkInTime": "15:00",
  "checkOutTime": "11:00",
  "rating": 4.9,
  "amenities": "WiFi,Pool,Gym,Spa,Restaurant,Bar,Parking,Conference",
  "parkingAvailable": true,
  "petFriendly": true
}
```
5. **Click** "Execute"
6. **Expected:** `200 OK`
7. **Verify:** `updatedAt` timestamp is now set

---

### **Step 2.5: Create Second Hotel (for testing)**

```json
{
  "name": "Seaside Resort",
  "address": "456 Beach Avenue",
  "city": "Miami",
  "country": "USA",
  "postalCode": "33139",
  "stars": 4,
  "email": "info@seasideresort.com",
  "phoneNumber": "+1-305-555-0200",
  "website": "https://www.seasideresort.com",
  "description": "Beautiful beachfront resort",
  "checkInTime": "14:00",
  "checkOutTime": "12:00",
  "rating": 4.5,
  "amenities": "WiFi,Pool,Beach,Restaurant,Bar",
  "parkingAvailable": true,
  "petFriendly": true
}
```

---

## 🛏️ **Phase 3: Room Management**

### **Step 3.1: Create Deluxe Room**

1. **Expand** `POST /api/Rooms`
2. **Click** "Try it out"
3. **Paste this JSON** (use your hotel ID):
```json
{
  "hotelId": 1,
  "roomNumber": "101",
  "type": 6,
  "floor": 1,
  "capacity": 2,
  "pricePerNight": 350.00,
  "description": "Deluxe room with king bed and ocean view",
  "amenities": "WiFi,TV,AC,Minibar,Safe,Balcony,Coffee Maker",
  "bedType": "1 King Bed",
  "hasBalcony": true,
  "hasBathtub": true,
  "isSmokingAllowed": false,
  "viewType": "Ocean View",
  "areaSqM": 45.5,
  "status": 1,
  "isActive": true
}
```

**Room Type Reference:**
- 1 = Single
- 2 = Double
- 3 = Twin
- 4 = Triple
- 5 = Suite
- 6 = Deluxe
- 7 = Presidential
- 8 = Studio
- 9 = Family
- 10 = Accessible

**Room Status Reference:**
- 1 = Available
- 2 = Occupied
- 3 = Cleaning
- 4 = Maintenance
- 5 = OutOfService
- 6 = Reserved

4. **Click** "Execute"
5. **Expected:** `201 Created`
6. **Note the room `id`**

---

### **Step 3.2: Create Multiple Room Types**

Create these additional rooms to test variety:

**Standard Double Room:**
```json
{
  "hotelId": 1,
  "roomNumber": "102",
  "type": 2,
  "floor": 1,
  "capacity": 2,
  "pricePerNight": 150.00,
  "description": "Comfortable double room",
  "amenities": "WiFi,TV,AC",
  "bedType": "1 Queen Bed",
  "hasBalcony": false,
  "hasBathtub": false,
  "isSmokingAllowed": false,
  "viewType": "City View",
  "areaSqM": 25.0,
  "status": 1,
  "isActive": true
}
```

**Presidential Suite:**
```json
{
  "hotelId": 1,
  "roomNumber": "501",
  "type": 7,
  "floor": 5,
  "capacity": 4,
  "pricePerNight": 1200.00,
  "description": "Luxurious presidential suite with panoramic views",
  "amenities": "WiFi,TV,AC,Minibar,Safe,Balcony,Jacuzzi,Kitchen,Living Room",
  "bedType": "1 King Bed + 2 Twin Beds",
  "hasBalcony": true,
  "hasBathtub": true,
  "isSmokingAllowed": false,
  "viewType": "Panoramic Ocean View",
  "areaSqM": 120.0,
  "status": 1,
  "isActive": true
}
```

**Family Room:**
```json
{
  "hotelId": 1,
  "roomNumber": "201",
  "type": 9,
  "floor": 2,
  "capacity": 5,
  "pricePerNight": 280.00,
  "description": "Spacious family room with extra beds",
  "amenities": "WiFi,TV,AC,Minibar,Safe",
  "bedType": "1 King Bed + 2 Single Beds + 1 Sofa Bed",
  "hasBalcony": false,
  "hasBathtub": true,
  "isSmokingAllowed": false,
  "viewType": "Garden View",
  "areaSqM": 55.0,
  "status": 1,
  "isActive": true
}
```

---

### **Step 3.3: Get All Rooms for Hotel**

1. **Expand** `GET /api/Rooms/hotel/{hotelId}`
2. **Click** "Try it out"
3. **Enter** your hotel ID (e.g., `1`)
4. **Click** "Execute"
5. **Expected:** `200 OK` with array of all rooms for that hotel

**Verify:**
- ✅ All 4 rooms are returned
- ✅ Different room types and prices
- ✅ All belong to hotel ID 1

---

### **Step 3.4: Get Available Rooms**

1. **Expand** `GET /api/Rooms/hotel/{hotelId}/available`
2. **Click** "Try it out"
3. **Enter** hotel ID
4. **Click** "Execute"
5. **Expected:** All rooms with `status: 1` (Available) and `isActive: true`

---

### **Step 3.5: Update Room**

1. **Expand** `PUT /api/Rooms/{id}`
2. **Click** "Try it out"
3. **Enter** room ID (e.g., the first room you created)
4. **Update the price:**
```json
{
  "id": 1,
  "hotelId": 1,
  "roomNumber": "101",
  "type": 6,
  "floor": 1,
  "capacity": 2,
  "pricePerNight": 400.00,
  "description": "Deluxe room with king bed and ocean view - PRICE UPDATED",
  "amenities": "WiFi,TV,AC,Minibar,Safe,Balcony,Coffee Maker,Bathrobe",
  "bedType": "1 King Bed",
  "hasBalcony": true,
  "hasBathtub": true,
  "isSmokingAllowed": false,
  "viewType": "Ocean View",
  "areaSqM": 45.5,
  "status": 1,
  "isActive": true
}
```
5. **Click** "Execute"
6. **Expected:** `200 OK` with updated data
7. **Verify:** `updatedAt` timestamp is set

---

### **Step 3.6: Test Room Number Uniqueness**

1. **Try to create a duplicate room number:**
```json
{
  "hotelId": 1,
  "roomNumber": "101",
  "type": 2,
  "capacity": 2,
  "pricePerNight": 100.00
}
```
2. **Expected:** `500 Internal Server Error` with message about duplicate room number
3. **This proves:** Room number uniqueness validation works! ✅

---

## 🧹 **Phase 4: Room Status Management**

### **Step 4.1: Update Room Status to Cleaning**

1. **Expand** `PATCH /api/Rooms/{id}/status`
2. **Click** "Try it out"
3. **Enter** room ID
4. **Paste:**
```json
{
  "status": 3
}
```
5. **Click** "Execute"
6. **Expected:** `200 OK` with `status: 3` (Cleaning)

---

### **Step 4.2: Mark Room as Cleaned**

1. **Expand** `POST /api/Rooms/{id}/clean`
2. **Click** "Try it out"
3. **Enter** same room ID
4. **Click** "Execute"
5. **Expected:** `200 OK` with success message

**Get the room again and verify:**
- ✅ `status` is back to `1` (Available)
- ✅ `lastCleaned` timestamp is set
- ✅ `updatedAt` timestamp is updated

---

### **Step 4.3: Record Maintenance**

1. **Expand** `POST /api/Rooms/{id}/maintenance`
2. **Click** "Try it out"
3. **Enter** room ID
4. **Paste:**
```json
{
  "notes": "AC unit serviced, filter replaced, thermostat calibrated"
}
```
5. **Click** "Execute"
6. **Expected:** `200 OK`

**Get the room and verify:**
- ✅ `status` changed to `4` (Maintenance)
- ✅ `lastMaintenance` timestamp is set
- ✅ `notes` field contains maintenance info

---

### **Step 4.4: Get Rooms by Status**

1. **Expand** `GET /api/Rooms/hotel/{hotelId}/status/{status}`
2. **Click** "Try it out"
3. **Enter** hotel ID and status `4` (Maintenance)
4. **Click** "Execute"
5. **Expected:** Only rooms in maintenance status

---

## 👥 **Phase 5: Multi-User Testing**

### **Step 5.1: Register Manager User**

1. **Logout** by clicking "Authorize" → "Logout"
2. **Register new user:**
```json
{
  "fullName": "Hotel Manager",
  "email": "manager@hotel.com",
  "password": "Manager123!",
  "role": "Manager"
}
```
3. **Login** with manager credentials
4. **Copy new token** and authorize

---

### **Step 5.2: Test Manager Permissions**

**✅ Managers CAN:**
- Create rooms (`POST /api/Rooms`)
- Update rooms (`PUT /api/Rooms/{id}`)
- Mark rooms as cleaned
- Record maintenance
- Get all data

**❌ Managers CANNOT:**
- Delete rooms (`DELETE /api/Rooms/{id}`) - Try it! Should get `403 Forbidden`
- Delete hotels (`DELETE /api/Hotels/{id}`) - Admin only

---

### **Step 5.3: Register Housekeeper**

```json
{
  "fullName": "Housekeeper Staff",
  "email": "housekeeper@hotel.com",
  "password": "Housekeeper123!",
  "role": "Housekeeper"
}
```

**✅ Housekeepers CAN:**
- Mark rooms as cleaned (`POST /api/Rooms/{id}/clean`)
- Update room status (`PATCH /api/Rooms/{id}/status`)
- View room data

**❌ Housekeepers CANNOT:**
- Create/update/delete rooms
- Create/update/delete hotels

---

### **Step 5.4: Register Guest User**

```json
{
  "fullName": "Guest User",
  "email": "guest@hotel.com",
  "password": "Guest123!",
  "role": "Guest"
}
```

**✅ Guests CAN:**
- View hotels and rooms
- Get available rooms

**❌ Guests CANNOT:**
- Create, update, or delete anything
- Try creating a room: Should get `403 Forbidden`

---

## 🧪 **Phase 6: Advanced Testing**

### **Step 6.1: Test Hotel Ownership**

1. **Login as Admin**
2. **Get all hotels:** `GET /api/Hotels`
3. **Verify:** You see hotels owned by your admin user
4. **Note:** `ownerId` field should match your user ID

---

### **Step 6.2: Test Room Filtering**

**Test 1: Get only available rooms**
- `GET /api/Rooms/hotel/1/available`
- Should exclude maintenance/occupied rooms

**Test 2: Get rooms by status**
- `GET /api/Rooms/hotel/1/status/1` (Available)
- `GET /api/Rooms/hotel/1/status/4` (Maintenance)

---

### **Step 6.3: Test Data Validation**

**Test invalid email:**
```json
{
  "name": "Test Hotel",
  "email": "not-an-email",
  ...
}
```
**Expected:** Validation error

**Test invalid stars (>5):**
```json
{
  "name": "Test Hotel",
  "stars": 10,
  ...
}
```
**Expected:** Validation error

**Test invalid room capacity (>20):**
```json
{
  "hotelId": 1,
  "roomNumber": "999",
  "capacity": 50,
  ...
}
```
**Expected:** Validation error

---

### **Step 6.4: Test Deletion (Admin Only)**

1. **Login as Admin**
2. **Create a test room to delete:**
```json
{
  "hotelId": 1,
  "roomNumber": "999",
  "type": 1,
  "capacity": 1,
  "pricePerNight": 50.00,
  "status": 1,
  "isActive": true
}
```
3. **Delete it:** `DELETE /api/Rooms/{id}`
4. **Expected:** `204 No Content`
5. **Verify:** Getting that room returns `404 Not Found`

---

## ✅ **Verification Checklist**

After completing all phases, verify:

### **Hotels:**
- ✅ Created at least 2 hotels
- ✅ Updated hotel details
- ✅ Hotels show owner information
- ✅ Timestamps (createdAt, updatedAt) work

### **Rooms:**
- ✅ Created rooms with different types (Single, Double, Deluxe, Presidential)
- ✅ Rooms have unique numbers per hotel
- ✅ Room validation works (duplicate prevention)
- ✅ All room features work (amenities, balcony, bathtub, view)

### **Room Status:**
- ✅ Can change room status
- ✅ Marking as cleaned sets timestamp and status to Available
- ✅ Recording maintenance sets timestamp and status to Maintenance
- ✅ Can filter rooms by status

### **Authorization:**
- ✅ Admin can do everything
- ✅ Manager can create/update but not delete
- ✅ Housekeeper can clean rooms and update status
- ✅ Guest can only view data

### **Validation:**
- ✅ Email format validated
- ✅ Stars range (1-5) enforced
- ✅ Room number uniqueness enforced per hotel
- ✅ Capacity limits enforced

---

## 🎯 **Sample Test Scenarios**

### **Scenario 1: Complete Hotel Setup**
1. Create hotel
2. Create 5 rooms (different types)
3. Update pricing for peak season
4. Mark 2 rooms as occupied
5. Send 1 room to maintenance
6. Get available rooms (should show 2)

### **Scenario 2: Daily Housekeeping**
1. Login as Housekeeper
2. Get all rooms in "Cleaning" status
3. Mark each as cleaned
4. Verify they're now "Available"

### **Scenario 3: Multi-Hotel Management**
1. Create 3 hotels in different cities
2. Create rooms for each hotel
3. Verify room numbers can be the same across hotels
4. Filter rooms by hotel

---

## 📊 **Expected Data After Testing**

If you completed all steps, you should have:

- **Users:** 4 (Admin, Manager, Housekeeper, Guest)
- **Hotels:** 2-3 hotels
- **Rooms:** 4-8 rooms across hotels
- **Room Types:** Multiple (Single, Double, Deluxe, Presidential, Family)
- **Room Statuses:** Mix of Available, Maintenance, Cleaning

---

## 🐛 **Troubleshooting**

### **Problem: "Unauthorized" error**
**Solution:** 
- Make sure you clicked "Authorize" and entered the token
- Token should be: `Bearer <your-token>`
- Token expires after 60 minutes - login again

### **Problem: "Forbidden" error**
**Solution:**
- You're trying an action your role doesn't allow
- Check the role requirements in the endpoint description
- Login as Admin for full access

### **Problem: Validation errors**
**Solution:**
- Check all required fields are provided
- Verify data types (numbers vs strings)
- Check value ranges (stars 1-5, capacity 1-20)

### **Problem: Duplicate room number error**
**Solution:**
- This is expected! Room numbers must be unique per hotel
- Change the room number to something else
- Or create the room in a different hotel

---

## 🎉 **Congratulations!**

You've successfully tested the complete Hotel Management API!

**What you've tested:**
- ✅ JWT Authentication & Authorization
- ✅ Hotel CRUD operations
- ✅ Room CRUD operations with 25+ properties
- ✅ Room status management (Cleaning, Maintenance)
- ✅ Role-based access control (4 roles)
- ✅ Data validation (FluentValidation)
- ✅ Room number uniqueness
- ✅ Automatic timestamps
- ✅ Complex queries and filtering

**Your API is production-ready for Phase 1.1!** 🚀

---

## 📝 **Next Steps**

1. **Phase 1.2:** Guest Entity (for walk-in and registered guests)
2. **Phase 1.3:** Reservation System (bookings, check-in/out)
3. **Phase 2:** Advanced features (payments, reporting, etc.)

**Ready to continue development or test more scenarios!**
