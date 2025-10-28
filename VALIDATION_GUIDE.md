# Validation & Error Handling Guide

## 🎯 Architecture Overview

Your application now has a **unified approach** to error handling that separates concerns:

```
┌─────────────────────────────────────────────────────┐
│                   Client Request                     │
└──────────────────┬──────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────┐
│          1. FluentValidation (Auto)                  │
│  ✓ Validates DTOs before controller action          │
│  ✓ Returns 400 with validation errors               │
└──────────────────┬──────────────────────────────────┘
                   │ (if valid)
                   ▼
┌─────────────────────────────────────────────────────┐
│          2. Controller Action                        │
│  ✓ Business logic execution                         │
│  ✓ Service calls                                    │
└──────────────────┬──────────────────────────────────┘
                   │ (if exception)
                   ▼
┌─────────────────────────────────────────────────────┐
│       3. Exception Middleware (Global)               │
│  ✓ Catches all unhandled exceptions                 │
│  ✓ Returns 500/404/401 with error message          │
└──────────────────┬──────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────┐
│            Unified ApiResponse<T>                    │
│  { success, data, message, errors, statusCode }     │
└─────────────────────────────────────────────────────┘
```

---

## 🔄 Before vs After

### **❌ OLD APPROACH - Data Annotations**

```csharp
public class HotelDto
{
    [Required(ErrorMessage = "Hotel name is required")]
    [MaxLength(200, ErrorMessage = "Hotel name cannot exceed 200 characters")]
    [MinLength(2, ErrorMessage = "Hotel name must be at least 2 characters")]
    public string Name { get; set; }
}
```

**Problems:**
- ❌ Validation logic mixed with DTOs
- ❌ Hard to test
- ❌ Limited validation capabilities
- ❌ Inconsistent error response format

### **✅ NEW APPROACH - FluentValidation**

```csharp
// DTO - Clean, no validation clutter
public class HotelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Validator - Separate, testable, powerful
public class HotelDtoValidator : AbstractValidator<HotelDto>
{
    public HotelDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Hotel name is required")
            .MinimumLength(2).WithMessage("Hotel name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Hotel name cannot exceed 200 characters")
            .Matches("^[a-zA-Z0-9\\s-]+$").WithMessage("Hotel name can only contain letters, numbers, spaces, and hyphens");
    }
}
```

**Benefits:**
- ✅ Separation of concerns
- ✅ Easily testable
- ✅ Advanced validation rules
- ✅ Consistent API responses
- ✅ Better error messages

---

## 📦 Unified API Response Format

### **All responses now use this structure:**

```json
{
  "success": true/false,
  "data": { ... },
  "message": "Optional message",
  "errors": ["List of errors"],
  "statusCode": 200
}
```

### **Examples:**

#### **✅ Success Response (200)**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "name": "Grand Hotel"
  },
  "message": null,
  "errors": null,
  "statusCode": 200
}
```

#### **❌ Validation Error (400)**
```json
{
  "success": false,
  "data": null,
  "message": "Validation failed",
  "errors": [
    "Hotel name is required",
    "Hotel name must be at least 2 characters"
  ],
  "statusCode": 400
}
```

#### **❌ Exception Error (500)**
```json
{
  "success": false,
  "data": null,
  "message": "An error occurred processing your request",
  "errors": ["Stack trace in development only"],
  "statusCode": 500
}
```

---

## 🛠️ How It Works

### **1. FluentValidation (Automatic)**

When a request comes in, FluentValidation **automatically** validates the DTO:

```csharp
[HttpPost]
public async Task<IActionResult> CreateAsync([FromBody] HotelDto dto)
{
    // Validation happens BEFORE this line
    // If invalid, 400 response is returned automatically
    // You don't need to check ModelState anymore!
    
    var created = await _service.CreateAsync(dto);
    return CreatedAtAction("GetById", new { id = created.Id }, created);
}
```

### **2. ValidationFilter (Intercepts Invalid Requests)**

The `ValidationFilter` catches invalid `ModelState` and returns a consistent response:

```csharp
public void OnActionExecuting(ActionExecutingContext context)
{
    if (!context.ModelState.IsValid)
    {
        var errors = context.ModelState
            .SelectMany(x => x.Value!.Errors)
            .Select(x => x.ErrorMessage)
            .ToList();

        var response = ApiResponse<object>.ValidationErrorResponse(errors);
        context.Result = new BadRequestObjectResult(response);
    }
}
```

### **3. Exception Middleware (Catches Unhandled Exceptions)**

Catches any exception during execution:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);
    }
    catch (Exception ex)
    {
        // Logs the error
        // Returns ApiResponse with appropriate status code
    }
}
```

---

## 📝 Creating Validators

### **Basic Example:**

```csharp
using FluentValidation;
using HotelManagement.Models.DTOs;

namespace HotelManagement.Validators;

public class HotelDtoValidator : AbstractValidator<HotelDto>
{
    public HotelDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Hotel name is required")
            .MinimumLength(2).WithMessage("Hotel name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Hotel name cannot exceed 200 characters");
    }
}
```

### **Advanced Example:**

```csharp
public class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256)
            .MustAsync(async (email, cancellation) => 
            {
                // Custom async validation
                return await IsEmailUnique(email);
            }).WithMessage("Email already exists");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(6)
            .Matches(@"[A-Z]").WithMessage("Password must contain uppercase")
            .Matches(@"[a-z]").WithMessage("Password must contain lowercase")
            .Matches(@"[0-9]").WithMessage("Password must contain number");

        RuleFor(x => x.Role)
            .Must(role => new[] { "Admin", "Manager", "Guest" }.Contains(role))
            .WithMessage("Role must be one of: Admin, Manager, Guest");
    }
}
```

### **Common Validation Rules:**

```csharp
// Required fields
RuleFor(x => x.Name).NotEmpty();
RuleFor(x => x.Email).NotNull();

// String length
RuleFor(x => x.Name).Length(2, 100);
RuleFor(x => x.Description).MaximumLength(500);

// Regex patterns
RuleFor(x => x.Email).EmailAddress();
RuleFor(x => x.Phone).Matches(@"^\+?[1-9]\d{1,14}$");

// Numeric ranges
RuleFor(x => x.Age).InclusiveBetween(18, 120);
RuleFor(x => x.Price).GreaterThan(0);

// Custom validation
RuleFor(x => x.StartDate)
    .Must((model, startDate) => startDate < model.EndDate)
    .WithMessage("Start date must be before end date");

// Conditional validation
RuleFor(x => x.CompanyName)
    .NotEmpty()
    .When(x => x.IsCompany);

// Collection validation
RuleForEach(x => x.Items).SetValidator(new ItemValidator());
RuleFor(x => x.Items).Must(x => x.Count <= 100);
```

---

## 🧪 Testing Validators

```csharp
using FluentValidation.TestHelper;
using Xunit;

public class HotelDtoValidatorTests
{
    private readonly HotelDtoValidator _validator;

    public HotelDtoValidatorTests()
    {
        _validator = new HotelDtoValidator();
    }

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var dto = new HotelDto { Name = "" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Hotel name is required");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Name_Is_Valid()
    {
        var dto = new HotelDto { Name = "Valid Hotel" };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}
```

---

## 🎨 Optional: Using ApiResponse in Controllers

If you want to manually wrap your responses (though it's not required):

```csharp
[HttpGet]
public async Task<IActionResult> GetAllAsync()
{
    var hotels = await _service.GetAllAsync();
    var response = ApiResponse<IEnumerable<HotelDto>>.SuccessResponse(
        data: hotels,
        message: "Hotels retrieved successfully"
    );
    return Ok(response);
}

[HttpPost]
public async Task<IActionResult> CreateAsync([FromBody] HotelDto dto)
{
    var created = await _service.CreateAsync(dto);
    var response = ApiResponse<HotelDto>.CreatedResponse(
        data: created,
        message: "Hotel created successfully"
    );
    return CreatedAtAction("GetById", new { id = created.Id }, response);
}
```

---

## 🚀 Migration Guide

### **Step 1: Remove Data Annotations (Optional)**

You can now remove `[Required]`, `[MaxLength]`, etc. from your DTOs since FluentValidation handles it:

```csharp
// Before
public class HotelDto
{
    [Required(ErrorMessage = "Hotel name is required")]
    [MaxLength(200)]
    public string Name { get; set; }
}

// After (cleaner!)
public class HotelDto
{
    public string Name { get; set; } = string.Empty;
}
```

**Note:** Keep `[MaxLength]` on **entities** for database constraints, but you can remove them from DTOs.

### **Step 2: Controllers Stay the Same**

No changes needed! The validation happens automatically:

```csharp
[HttpPost]
public async Task<IActionResult> CreateAsync([FromBody] HotelDto dto)
{
    // Validation already happened
    // No need to check ModelState.IsValid
    var created = await _service.CreateAsync(dto);
    return CreatedAtAction("GetById", new { id = created.Id }, created);
}
```

---

## 🔍 Error Response Examples

### **Validation Error (Multiple Fields)**

**Request:**
```json
POST /api/Hotels
{
  "name": ""
}
```

**Response (400):**
```json
{
  "success": false,
  "data": null,
  "message": "Validation failed",
  "errors": [
    "Hotel name is required",
    "Hotel name must be at least 2 characters"
  ],
  "statusCode": 400
}
```

### **Unauthorized (JWT Missing)**

**Response (401):**
```json
{
  "success": false,
  "data": null,
  "message": "Unauthorized access",
  "errors": null,
  "statusCode": 401
}
```

### **Not Found**

**Response (404):**
```json
{
  "success": false,
  "data": null,
  "message": "Hotel not found",
  "errors": null,
  "statusCode": 404
}
```

---

## ✅ Benefits of This Approach

| Aspect | Benefit |
|--------|---------|
| **Separation of Concerns** | Validation logic separate from DTOs |
| **Testability** | Easy to unit test validators |
| **Reusability** | Share validators across multiple endpoints |
| **Consistency** | All errors use same format |
| **Maintainability** | Easy to update validation rules |
| **Advanced Rules** | Complex validation (async, conditional, cross-field) |
| **Better UX** | Clear, specific error messages |
| **Type Safety** | Compile-time validation of rules |

---

## 🎓 Next Steps

1. ✅ **FluentValidation is installed** and configured
2. ✅ **Validators created** for HotelDto, RegisterRequestDto, LoginRequestDto
3. ✅ **ValidationFilter** intercepts invalid requests
4. ✅ **ExceptionMiddleware** uses unified ApiResponse format
5. **Optional:** Remove data annotations from DTOs (keep on entities!)
6. **Optional:** Wrap controller responses in ApiResponse (not required)
7. **Create validators** for any new DTOs you add

---

## 📚 Further Reading

- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [ASP.NET Core Validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation)
- [REST API Best Practices](https://restfulapi.net/)

---

## 💡 Pro Tips

1. **Keep validators close to DTOs** - Easy to find and maintain
2. **Write validator tests** - Ensure rules work as expected
3. **Use custom error messages** - Make them user-friendly
4. **Consider localization** - For multi-language apps
5. **Don't over-validate** - Trust your database constraints too
6. **Use async validators sparingly** - They can slow down requests
7. **Document complex rules** - Help future developers understand why

Your validation is now **production-ready**, **testable**, and **maintainable**! 🚀
