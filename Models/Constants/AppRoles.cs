namespace HotelManagement.Models.Constants;

/// <summary>
/// Application role definitions
/// </summary>
public static class AppRoles
{
    // System & Management
    public const string SuperAdmin = "SuperAdmin";  // Full system access
    public const string Admin = "Admin";            // Hotel administration
    public const string Manager = "Manager";        // Hotel management
    
    // Staff
    public const string Housekeeper = "Housekeeper"; // Room cleaning & maintenance
    
    // Customers
    public const string Guest = "Guest";            // Registered customers
    
    /// <summary>
    /// All available roles in the system
    /// </summary>
    public static string[] AllRoles => new[]
    {
        SuperAdmin,
        Admin,
        Manager,
        Housekeeper,
        Guest
    };
    
    /// <summary>
    /// Roles with administrative privileges
    /// </summary>
    public static string[] AdminRoles => new[]
    {
        SuperAdmin,
        Admin
    };
    
    /// <summary>
    /// Roles with management privileges
    /// </summary>
    public static string[] ManagementRoles => new[]
    {
        SuperAdmin,
        Admin,
        Manager
    };
    
    /// <summary>
    /// Staff roles
    /// </summary>
    public static string[] StaffRoles => new[]
    {
        Housekeeper
    };
}
