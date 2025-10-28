# Authentication & Authorization Guide

## ✅ Implementation Complete!

Your Hotel Management API now has **JWT Authentication** and **Role-Based Authorization**.

---

## 🔐 What Was Implemented

### 1. **ASP.NET Core Identity**
- User management with secure password hashing
- Role-based access control
- Email-based login

### 2. **JWT Token Authentication**
- Stateless authentication
- Secure token generation
- 60-minute token expiration

### 3. **Role-Based Authorization**
Roles available:
- **Admin** - Full access (GET, POST, PUT, DELETE)
- **Manager** - Can view, create, and update (GET, POST, PUT)
- **Guest** - Can only view (GET)

### 4. **Protected Endpoints**
All `/api/Hotels` endpoints now require authentication:
- `GET /api/Hotels` - Any authenticated user
- `GET /api/Hotels/{id}` - Any authenticated user
- `POST /api/Hotels` - Admin or Manager only
- `PUT /api/Hotels/{id}` - Admin or Manager only
- `DELETE /api/Hotels/{id}` - Admin only

---

## 🚀 How to Use

### **Step 1: Register a New User**

**Endpoint:** `POST /api/Auth/register`

**Request Body:**
```json
{
  "fullName": "John Doe",
  "email": "admin@hotel.com",
  "password": "Admin123",
  "role": "Admin"
}
```

**Available Roles:** `Admin`, `Manager`, `Guest`

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "email": "admin@hotel.com",
  "fullName": "John Doe",
  "roles": ["Admin"],
  "expiresAt": "2025-10-18T15:15:00Z"
}
```

---

### **Step 2: Login**

**Endpoint:** `POST /api/Auth/login`

**Request Body:**
```json
{
  "email": "admin@hotel.com",
  "password": "Admin123"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "email": "admin@hotel.com",
  "fullName": "John Doe",
  "roles": ["Admin"],
  "expiresAt": "2025-10-18T15:15:00Z"
}
```

---

### **Step 3: Use the Token**

Copy the `token` from the response and use it in your requests.

#### **In Swagger:**
1. Click the **"Authorize"** button (🔓 icon) at the top
2. Enter: `Bearer <your-token-here>`
3. Click **"Authorize"**
4. Now all requests will include the token

#### **In Postman/Other Tools:**
Add header:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## 📋 Testing Scenarios

### **Test 1: Create a Hotel (Requires Admin/Manager)**
```json
POST /api/Hotels
Authorization: Bearer <admin-token>

{
  "name": "Grand Hotel"
}
```

✅ **Admin/Manager**: Success (201 Created)  
❌ **Guest**: 403 Forbidden  
❌ **No Token**: 401 Unauthorized

---

### **Test 2: Get All Hotels (Requires Authentication)**
```
GET /api/Hotels
Authorization: Bearer <any-token>
```

✅ **Any authenticated user**: Success (200 OK)  
❌ **No Token**: 401 Unauthorized

---

### **Test 3: Delete a Hotel (Requires Admin)**
```
DELETE /api/Hotels/1
Authorization: Bearer <admin-token>
```

✅ **Admin**: Success (204 No Content)  
❌ **Manager/Guest**: 403 Forbidden  
❌ **No Token**: 401 Unauthorized

---

## 🔧 Configuration

### **JWT Settings** (`appsettings.json`)
```json
{
  "JwtSettings": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLongForHS256!",
    "Issuer": "HotelManagementAPI",
    "Audience": "HotelManagementClient",
    "ExpiryMinutes": 60
  }
}
```

**⚠️ IMPORTANT:** Change the `Secret` in production to a strong, random key!

---

## 🗄️ Database Tables Created

ASP.NET Core Identity created these tables:
- `AspNetUsers` - User accounts
- `AspNetRoles` - Roles (Admin, Manager, Guest)
- `AspNetUserRoles` - User-Role relationships
- `AspNetUserClaims`, `AspNetRoleClaims`, etc.

---

## 🎯 Quick Start Commands

### Register an Admin:
```bash
curl -X POST http://localhost:5213/api/Auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Admin User",
    "email": "admin@hotel.com",
    "password": "Admin123",
    "role": "Admin"
  }'
```

### Login:
```bash
curl -X POST http://localhost:5213/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@hotel.com",
    "password": "Admin123"
  }'
```

### Create Hotel (with token):
```bash
curl -X POST http://localhost:5213/api/Hotels \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -d '{"name": "Luxury Hotel"}'
```

---

## 🔒 Security Best Practices

1. **Change JWT Secret** - Use a strong, random secret in production
2. **Use HTTPS** - Enable `app.UseHttpsRedirection()` in production
3. **Store Tokens Securely** - Never store tokens in localStorage (use httpOnly cookies in production)
4. **Refresh Tokens** - Consider implementing refresh tokens for longer sessions
5. **Rate Limiting** - Add rate limiting to prevent brute force attacks

---

## 🎉 You're All Set!

Your authentication system is ready to use. Start by:
1. Running your application
2. Opening Swagger at `http://localhost:5213/swagger`
3. Registering a new user with Admin role
4. Testing the protected endpoints

Happy coding! 🚀
