using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HOAManagementCompany.Models;
using HOAManagementCompany.Constants;
using System;
using System.Linq;

namespace HOAManagementCompany.Services;

public class UserRoleService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;

    public UserRoleService(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<List<IdentityUser>> GetAllUsersAsync()
    {
        return await _userManager.Users.ToListAsync();
    }

    public async Task<List<IdentityRole>> GetAllRolesAsync()
    {
        return await _roleManager.Roles.ToListAsync();
    }

    public async Task<List<string>> GetUserRolesAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return new List<string>();

        return (await _userManager.GetRolesAsync(user)).ToList();
    }

    public async Task<bool> AddUserToRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        var result = await _userManager.AddToRoleAsync(user, roleName);
        return result.Succeeded;
    }

    public async Task<bool> RemoveUserFromRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        return result.Succeeded;
    }

    public async Task<bool> IsUserInRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        return await _userManager.IsInRoleAsync(user, roleName);
    }

    public async Task<IdentityUser?> GetUserByIdAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId);
    }

    public async Task<bool> UserHasPermissionAsync(string userId, string permission)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            var userRoles = await _userManager.GetRolesAsync(user);
            
            if (!userRoles.Any())
                return false;
            
            // Use a single query to check all roles at once to avoid multiple database calls
            var hasPermission = await _context.RolePermissions
                .AnyAsync(rp => userRoles.Contains(rp.RoleName) && rp.Permission == permission);
            
            return hasPermission;
        }
        catch (Exception ex)
        {
            // Log the exception but don't throw it to prevent breaking the authorization flow
            // In production, you should use proper logging here
            Console.WriteLine($"Error checking permission {permission} for user {userId}: {ex.Message}");
            return false;
        }
    }

    public async Task<List<string>> GetUserPermissionsAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return new List<string>();

            var userRoles = await _userManager.GetRolesAsync(user);
            
            if (!userRoles.Any())
                return new List<string>();
            
            var permissions = await _context.RolePermissions
                .Where(rp => userRoles.Contains(rp.RoleName))
                .Select(rp => rp.Permission)
                .Distinct()
                .ToListAsync();

            return permissions;
        }
        catch (Exception ex)
        {
            // Log the exception but don't throw it to prevent breaking the authorization flow
            // In production, you should use proper logging here
            Console.WriteLine($"Error getting permissions for user {userId}: {ex.Message}");
            return new List<string>();
        }
    }
} 