using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using YeaFlow.Api.Infrastructure;
using YeaFlow.Domain.Connectors;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Tenancy;
using YeaFlow.Infrastructure.Context;
using YeaFlow.Infrastructure.Persistence;

namespace YeaFlow.Api.Demo;

/// <summary>
/// Local demo mode: a pick-a-person sign-in and a seeded demo tenant, so YeaFlow can be tried end to end before real
/// OIDC sign-in (milestone 2.2) exists. It only runs in the Development environment with <c>DemoMode:Enabled</c>; the API
/// refuses to start with it anywhere else. Users and permissions still come from the app database, exactly as for OIDC.
/// </summary>
public static class DemoMode
{
    public const string SectionName = "DemoMode";
    public const string Scheme = "Demo";
    public const string CookieName = "yf_demo";

    public static readonly Guid TenantId = new("d3e0f1a0-0000-4000-8000-000000000001");
    public static readonly Guid CompanyId = new("d3e0f1a0-0000-4000-8000-000000000002");
    public static readonly Guid ConnectorId = new("d3e0f1a0-0000-4000-8000-000000000003");
    public const string SapCompanyDb = "SBO_DEMO_KE";

    public sealed record Persona(string Key, string DisplayName, string Email, string Description, string[] Roles, bool Mfa);

    public static readonly IReadOnlyList<Persona> Personas =
    [
        new("sales", "Wanjiru Kamau", "wanjiru@demo.yeaflow.local", "Sales user: creates sales orders.", [Role.Sales], false),
        new("admin", "Otieno Ochieng", "otieno@demo.yeaflow.local", "App administrator with sales access (signs in with MFA).", [Role.AppAdministrator, Role.Sales], true),
    ];

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>($"{SectionName}:Enabled");

    /// <summary>Throws unless demo mode is off, or on in Development. Called before anything else is registered.</summary>
    public static void EnsureAllowed(IConfiguration configuration, IHostEnvironment environment)
    {
        if (IsEnabled(configuration) && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("DemoMode is for local development only and cannot be enabled outside the Development environment.");
        }
    }

    public static AuthenticationBuilder AddDemoSignIn(this AuthenticationBuilder auth) =>
        auth.AddCookie(Scheme, o =>
        {
            o.Cookie.Name = CookieName;
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = true;

            // An API: answer 401/403 instead of redirecting to a login page.
            o.Events.OnRedirectToLogin = c =>
            {
                c.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = c =>
            {
                c.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

    public static bool HasDemoCookie(HttpContext context) => context.Request.Cookies.ContainsKey(CookieName);

    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        var demo = app.MapGroup("/api/demo").AllowAnonymous().WithTags("Local demo");

        demo.MapGet("/personas", () => Results.Ok(Personas.Select(p => new { p.Key, p.DisplayName, p.Description })));

        demo.MapPost("/sign-in", async (SignInRequest request, HttpContext http) =>
        {
            var persona = Personas.FirstOrDefault(p => p.Key == request.Persona);
            if (persona is null)
            {
                return Results.Problem(title: "Unknown demo person", statusCode: StatusCodes.Status400BadRequest);
            }

            // Only who signed in is in the cookie; the user's roles and companies are read from the database per request.
            var claims = new List<Claim>
            {
                new("sub", SubjectFor(persona)),
                new(ExecutionContextMiddleware.TenantClaim, TenantId.ToString()),
            };
            if (persona.Mfa)
            {
                claims.Add(new Claim("amr", "mfa"));
            }

            await http.SignInAsync(Scheme, new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme, "sub", "roles")));
            return Results.NoContent();
        });

        demo.MapPost("/sign-out", async (HttpContext http) =>
        {
            await http.SignOutAsync(Scheme);
            return Results.NoContent();
        });

        return app;
    }

    /// <summary>Applies migrations and creates the demo tenant, company, people and connector if they are missing.</summary>
    public static Task SeedAsync(IServiceProvider services, string connectorSecret) =>
        services.GetRequiredService<TenantScope>().RunAsync(TenantId, "demo-seed", async sp =>
        {
            var db = sp.GetRequiredService<YeaFlowDbContext>();
            var now = sp.GetRequiredService<TimeProvider>().GetUtcNow();
            await db.Database.MigrateAsync();

            if (await db.Tenants.AnyAsync(t => t.Id == TenantId))
            {
                return 0;
            }

            var tenant = new Tenant(TenantId, "Demo Ltd", "demo", Package.Business, now);
            var connector = Connector.RegisterWithKnownSecret(ConnectorId, TenantId, "Demo connector (simulated SAP)", connectorSecret, now);
            var company = new Company(CompanyId, TenantId, "Demo Kenya Ltd", SapCompanyDb, SapDatabaseType.SqlServer);
            company.AssignConnector(connector.Id);
            db.Tenants.Add(tenant);
            db.Connectors.Add(connector);
            db.Companies.Add(company);

            var active = 0;
            foreach (var persona in Personas)
            {
                var user = new AppUser(Guid.NewGuid(), TenantId, persona.Email, persona.DisplayName);
                foreach (var role in persona.Roles)
                {
                    user.AssignRole(role);
                }

                user.LinkExternalSubject(SubjectFor(persona));
                user.GrantCompany(company.Id);
                user.Activate(tenant.Package, active++);
                db.Users.Add(user);
            }

            await db.SaveChangesAsync();
            return 1;
        });

    private static string SubjectFor(Persona persona) => $"demo-{persona.Key}";

    public sealed record SignInRequest(string Persona);
}
