# 🧪 Test Database Configuration

## ❌ **Previous Problem**
Integration tests were writing to your **development database**, creating 70+ test records that polluted your real data.

## ✅ **Solution Implemented**
Tests now use an **in-memory database** that is:
- ✅ Created fresh for each test run
- ✅ Completely isolated from your real database
- ✅ Automatically discarded after tests finish
- ✅ Fast and doesn't require SQL Server

---

## 📁 **What Was Changed**

### **1. Added NuGet Package**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
```

### **2. Created Custom Test Factory**
**File:** `Tests/Helpers/CustomWebApplicationFactory.cs`

This factory:
- Replaces SQL Server with in-memory database
- Creates a unique database for each test run (using GUID)
- Ensures database is created before tests run
- Automatically cleans up after tests

### **3. Updated Integration Tests**
Both test classes now use `CustomWebApplicationFactory`:
- ✅ `HotelsControllerIntegrationTests.cs`
- ✅ `RoomsControllerIntegrationTests.cs`

---

## 🗄️ **Database Usage**

### **Development Database** (SQL Server)
- Used when you run the application normally (`dotnet run` or Swagger)
- Connection string: `DefaultConnection` in `appsettings.json`
- Your real data lives here
- **NOT affected by tests anymore** ✅

### **Test Database** (In-Memory)
- Used only when running tests (`dotnet test`)
- Exists only in RAM during test execution
- Unique GUID per test run prevents conflicts
- Automatically cleaned up when tests finish

---

## 🧹 **Cleaning Up Old Test Data**

### **Option 1: SQL Script (Recommended)**
Run the provided SQL script to clean test data from your development database:

```bash
# File: Scripts/CleanTestData.sql
```

**To execute:**
1. Open SQL Server Management Studio (SSMS) or Azure Data Studio
2. Connect to your database
3. Open `Scripts/CleanTestData.sql`
4. Review the script (it targets records with "Test" in the name)
5. Execute it

### **Option 2: Manual Cleanup**
```sql
-- Delete test records manually
DELETE FROM Rooms WHERE HotelId IN (SELECT Id FROM Hotels WHERE Name LIKE 'Test%');
DELETE FROM Hotels WHERE Name LIKE 'Test%';
DELETE FROM AspNetUsers WHERE Email LIKE 'test%@test.com';
```

### **Option 3: Nuclear Option (Development Only!)**
```bash
# Drop and recreate the entire database
dotnet ef database drop --force
dotnet ef database update
```

**⚠️ WARNING:** Option 3 will delete ALL data, not just test data!

---

## ✅ **Verification**

### **Test the New Setup**

1. **Stop the running application** (if it's running)

2. **Run tests:**
```bash
dotnet test
```

3. **Check your database:**
   - You should see **NO NEW test records** added
   - Tests should pass: `101/101 ✅`

4. **Verify isolation:**
   - Query your Hotels table: `SELECT * FROM Hotels`
   - You should only see YOUR real hotels, not test data

---

## 🚀 **Testing in Swagger**

Now that tests are isolated, you can safely:

1. **Clean up old test data** (using the SQL script)

2. **Run the application:**
```bash
dotnet run
```

3. **Open Swagger:**
   - Navigate to `https://localhost:5001/swagger` (or your configured port)

4. **Test the APIs:**
   - Register/Login to get a token
   - Create real hotels and rooms
   - No interference from test data!

---

## 📊 **Benefits of In-Memory Database**

| Aspect | Before | After |
|--------|--------|-------|
| **Test Speed** | Slower (SQL Server I/O) | ⚡ Faster (RAM) |
| **Data Pollution** | ❌ 70+ test records | ✅ None |
| **Setup Required** | SQL Server running | ✅ None |
| **Cleanup Required** | Manual deletion | ✅ Automatic |
| **Isolation** | ❌ Shared database | ✅ Unique per run |
| **CI/CD Friendly** | Requires DB setup | ✅ Works anywhere |

---

## 🔍 **How It Works**

### **Test Execution Flow**

```
1. Test starts
   ↓
2. CustomWebApplicationFactory creates unique in-memory database
   (e.g., "TestDatabase_a7b3c45d-...")
   ↓
3. EF Core migrations run automatically (creates tables in memory)
   ↓
4. Tests run (create users, hotels, rooms)
   ↓
5. Tests finish
   ↓
6. In-memory database is discarded
   ✅ No trace left in SQL Server
```

### **Code Example**
```csharp
// Each test run gets a unique database
services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseInMemoryDatabase("TestDatabase_" + Guid.NewGuid());
});
```

---

## 🛠️ **Troubleshooting**

### **Problem: Tests fail with "database already exists"**
**Solution:** This shouldn't happen with our GUID-based naming, but if it does:
- Each test run creates a unique database name
- Restart the test run

### **Problem: Tests are slow**
**Possible causes:**
- First test run compiles the app (normal)
- Too many test records created (each test creates data)
- Network latency (shouldn't affect in-memory DB)

**Solution:**
- In-memory DB should be faster than SQL Server
- If still slow, consider test parallelization

### **Problem: Old test data still in database**
**Cause:** This is from BEFORE we added in-memory DB
**Solution:** Run the cleanup script (see above)

---

## 📝 **Best Practices Going Forward**

### **✅ DO**
- Run `dotnet test` freely - it won't pollute your database
- Use descriptive names for test data (for debugging)
- Keep integration tests focused and fast

### **❌ DON'T**
- Don't manually connect to in-memory database (it's temporary)
- Don't rely on test data persisting between runs
- Don't use production database for testing

---

## 🎯 **Summary**

**Before:**
```
dotnet test → Writes to SQL Server → 70+ test records 😞
```

**After:**
```
dotnet test → Uses in-memory DB → No pollution! 😊
```

**Your development database is now safe!** ✅

All 101 tests will run in isolation, and your SQL Server database will only contain the data YOU create through the application or Swagger.

---

## 🚦 **Next Steps**

1. ✅ Clean up old test data (run SQL script)
2. ✅ Stop any running instances of the app
3. ✅ Run `dotnet test` to verify new setup (optional)
4. ✅ Start app with `dotnet run`
5. ✅ Test in Swagger with clean database!

**You're ready to test the API in Swagger!** 🎉
