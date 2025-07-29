using HOAManagementCompany.Components;
using HOAManagementCompany.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using HOAManagementCompany.Authorization.Requirements;
using HOAManagementCompany.Authorization.Handlers;
using HOAManagementCompany.Constants;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add Identity services
builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Add authentication state
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<ViolationService>();
builder.Services.AddScoped<UserRoleService>();
builder.Services.AddHttpContextAccessor();

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    // Violation policies
    options.AddPolicy("ViolationsRead", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.ViolationsRead)));
    options.AddPolicy("ViolationsCreate", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.ViolationsCreate)));
    options.AddPolicy("ViolationsUpdate", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.ViolationsUpdate)));
    options.AddPolicy("ViolationsDelete", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.ViolationsDelete)));

    // ViolationType policies
    options.AddPolicy("ViolationTypesRead", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.ViolationTypesRead)));
    options.AddPolicy("ViolationTypesCreate", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.ViolationTypesCreate)));
    options.AddPolicy("ViolationTypesUpdate", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.ViolationTypesUpdate)));
    options.AddPolicy("ViolationTypesDelete", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.ViolationTypesDelete)));

    // User management policies
    options.AddPolicy("UsersRead", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.UsersRead)));
    options.AddPolicy("UsersCreate", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.UsersCreate)));
    options.AddPolicy("UsersUpdate", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.UsersUpdate)));
    options.AddPolicy("UsersDelete", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.UsersDelete)));

    // Role management policies
    options.AddPolicy("RolesManage", policy =>
        policy.Requirements.Add(new PermissionRequirement(Permissions.RolesManage)));
});

// Register authorization handlers
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

// Ensure proper scoping for Identity services
builder.Services.Configure<IdentityOptions>(options =>
{
    // Configure Identity to use scoped DbContext
    options.Stores.MaxLengthForKeys = 128;
}); 
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

// Add Identity endpoints
app.MapIdentityApi<IdentityUser>();

// Seed data
await ApplicationDbContext.SeedDataAsync(app.Services);

app.Run();