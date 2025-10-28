# 🎭 Role Management Guide

## 📋 Available Roles

Your Hotel Management system now has **5 roles** with different permission levels:

### **1. SuperAdmin** 👑
- **Highest level access**
- Full system control
- Can manage all users, roles, and system settings
- Can perform ALL operations

**Use Case:** System owner, IT administrators

---

### **2. Admin** 🔑
- **High-level administrative access**
- Can manage hotels, reservations, and staff
- Can create/edit/delete most resources
- Cannot delete other admins (only SuperAdmin can)

**Use Case:** Hotel chain administrators, regional managers

---

### **3. Manager** 📊
- **Hotel management access**
- Can create and edit hotels and reservations
- Can view all data
- Cannot delete hotels or critical data

**Use Case:** Hotel managers, property managers

---

### **4. Housekeeper** 🧹
- **Staff access** (for future features)
- Can view assigned rooms/tasks
- Update cleaning/maintenance status
- Limited to operational tasks

**Use Case:** Cleaning staff, maintenance workers

---

### **5. Guest** 🏨
- **Customer access**
- Can view available hotels
- Can make reservations for themselves
- Can view their own bookings
- Cannot access administrative features

**Use Case:** Registered customers who book online

---

## 🔐 Permission Matrix

| Action | SuperAdmin | Admin | Manager | Housekeeper | Guest |
|--------|------------|-------|---------|-------------|-------|
| **Hotels** |
| View Hotels | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create Hotel | ✅ | ✅ | ✅ | ❌ | ❌ |
| Edit Hotel | ✅ | ✅ | ✅ | ❌ | ❌ |
| Delete Hotel | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Reservations** |
| View All Reservations | ✅ | ✅ | ✅ | ❌ | ❌ |
| View Own Reservations | ✅ | ✅ | ✅ | ❌ | ✅ |
| Create Reservation | ✅ | ✅ | ✅ | ❌ | ✅ |
| Cancel Any Reservation | ✅ | ✅ | ✅ | ❌ | ❌ |
| Cancel Own Reservation | ✅ | ✅ | ✅ | ❌ | ✅ |
| **Users** |
| View All Users | ✅ | ✅ | ❌ | ❌ | ❌ |
| Create Users | ✅ | ✅ | ❌ | ❌ | ❌ |
| Delete Users | ✅ | ❌ | ❌ | ❌ | ❌ |
| Manage Roles | ✅ | ❌ | ❌ | ❌ | ❌ |

---

## 🚀 How to Use Roles

### **1. Register a User with a Role**

**Request:**
```http
POST /api/Auth/register
Content-Type: application/json

{
  "fullName": "John Doe",
  "email": "john@example.com",
  "password": "SecurePass123",
  "role": "Manager"
}
```

**Valid Roles:**
- `SuperAdmin`
- `Admin`
- `Manager`
- `Housekeeper`
- `Guest`

---

### **2. Role-Based Authorization in Controllers**

**Using AppRoles Constants:**

```csharp
using HotelManagement.Models.Constants;

// Single role
[Authorize(Roles = AppRoles.Admin)]
public async Task<IActionResult> AdminOnly() { }

// Multiple roles (OR logic - user needs ANY)
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
public async Task<IActionResult> AdminLevelAccess() { }

// Using role groups
[Authorize(Roles = string.Join(",", AppRoles.ManagementRoles))]
public async Task<IActionResult> ManagementAccess() { }
```

**Current CrudController Authorization:**

```csharp
// Anyone authenticated can view
[HttpGet]
[Authorize]
public async Task<IActionResult> GetAllAsync() { }

// SuperAdmin, Admin, Manager can create/edit
[HttpPost]
[Authorize(Roles = "SuperAdmin,Admin,Manager")]
public async Task<IActionResult> CreateAsync([FromBody] TDto dto) { }

// Only SuperAdmin and Admin can delete
[HttpDelete("{id}")]
[Authorize(Roles = "SuperAdmin,Admin")]
public async Task<IActionResult> DeleteAsync(int id) { }
```

---

### **3. Check User Roles in Code**

```csharp
// In a controller
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
var roles = User.Claims
    .Where(c => c.Type == ClaimTypes.Role)
    .Select(c => c.Value)
    .ToList();

// Check specific role
if (User.IsInRole(AppRoles.Admin))
{
    // Admin-only logic
}

// Using UserManager
var user = await _userManager.FindByIdAsync(userId);
var userRoles = await _userManager.GetRolesAsync(user);
bool isManager = userRoles.Contains(AppRoles.Manager);
```

---

## 🔧 Role Management Operations

### **Create Roles (Automatic on Startup)**

Roles are automatically seeded when the application starts:

```csharp
// In Program.cs - already configured
await DbSeeder.SeedRolesAsync(roleManager);
```

**Console Output:**
```
✅ Role 'SuperAdmin' created successfully
✅ Role 'Admin' created successfully
✅ Role 'Manager' created successfully
✅ Role 'Housekeeper' created successfully
✅ Role 'Guest' created successfully
```

---

### **Create SuperAdmin User (Optional)**

Uncomment in `Program.cs`:

```csharp
// Seed a default SuperAdmin (uncomment to enable)
var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
await DbSeeder.SeedSuperAdminAsync(userManager);
```

**Default Credentials:**
- Email: `superadmin@hotel.com`
- Password: `SuperAdmin123!`

⚠️ **Change the password immediately in production!**

---

## 📁 Key Files

| File | Purpose |
|------|---------|
| `Models/Constants/AppRoles.cs` | Role definitions and constants |
| `Data/DbSeeder.cs` | Database seeding logic |
| `Program.cs` | Auto-seeds roles on startup |
| `Validators/RegisterRequestDtoValidator.cs` | Validates role names |
| `Controllers/CrudController.cs` | Uses roles for authorization |

---

## 🎯 Role Groups (Helper Arrays)

```csharp
// All roles in the system
AppRoles.AllRoles 
// => ["SuperAdmin", "Admin", "Manager", "Housekeeper", "Guest"]

// Administrative roles
AppRoles.AdminRoles
// => ["SuperAdmin", "Admin"]

// Management roles (can create/edit)
AppRoles.ManagementRoles
// => ["SuperAdmin", "Admin", "Manager"]

// Staff roles
AppRoles.StaffRoles
// => ["Housekeeper"]
```

**Usage:**
```csharp
[Authorize(Roles = string.Join(",", AppRoles.ManagementRoles))]
```

---

## 🔄 Adding New Roles

### **Step 1: Add to AppRoles.cs**

```csharp
public const string Receptionist = "Receptionist";

public static string[] AllRoles => new[]
{
    SuperAdmin,
    Admin,
    Manager,
    Receptionist,  // Add here
    Housekeeper,
    Guest
};
```

### **Step 2: Restart Application**

Roles are auto-seeded on startup. New roles will be created automatically.

### **Step 3: Update Authorization**

```csharp
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Receptionist}")]
```

---

## 🧪 Testing Roles in Swagger

1. **Register a user:**
   ```json
   POST /api/Auth/register
   {
     "fullName": "Test Admin",
     "email": "admin@test.com",
     "password": "Admin123",
     "role": "Admin"
   }
   ```

2. **Login:**
   ```json
   POST /api/Auth/login
   {
     "email": "admin@test.com",
     "password": "Admin123"
   }
   ```

3. **Copy the token from response**

4. **Click "Authorize" button in Swagger**

5. **Enter:** `Bearer <your-token>`

6. **Test endpoints** - you'll now have Admin permissions!

---

## 🚨 Security Best Practices

1. ✅ **Always validate roles** before registration
2. ✅ **Use constants** (AppRoles) instead of magic strings
3. ✅ **Never hardcode credentials** in production
4. ✅ **Change default SuperAdmin password** immediately
5. ✅ **Use HTTPS** in production for token transmission
6. ✅ **Log role assignments** for audit trails
7. ✅ **Implement password complexity** (already configured)
8. ✅ **Set proper token expiration** (currently 60 minutes)

---

## 📊 Role Hierarchy Visualization

```
┌─────────────────────────────────────┐
│          SuperAdmin (👑)            │  ← Full Control
├─────────────────────────────────────┤
│            Admin (🔑)               │  ← High-level Admin
├─────────────────────────────────────┤
│           Manager (📊)              │  ← Hotel Management
├─────────────────────────────────────┤
│  Housekeeper (🧹) │  Guest (🏨)    │  ← Operational / Customer
└─────────────────────────────────────┘
```

---

## 💡 Future Enhancements

Consider adding these roles as your system grows:

- **Receptionist** - Front desk operations
- **AccountManager** - Financial operations
- **MaintenanceStaff** - Repairs and maintenance
- **VIPGuest** - Premium customer with extra privileges
- **CorporateAccount** - Business account management

---

## 🎉 Summary

Your application now has:
- ✅ 5 well-defined roles
- ✅ Automatic role seeding on startup
- ✅ Type-safe role constants
- ✅ Comprehensive authorization
- ✅ Optional SuperAdmin user creation
- ✅ Validation for all role assignments

**Next time you start the app, all roles will be automatically created in the database!** 🚀
