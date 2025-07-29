using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using HOAManagementCompany.Authorization.Requirements;
using HOAManagementCompany.Services;

namespace HOAManagementCompany.Authorization.Handlers;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly UserRoleService _userRoleService;

    public PermissionHandler(UserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Check if user has the required permission through their role
        var hasPermission = await _userRoleService.UserHasPermissionAsync(userId, requirement.Permission);
        
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
} 