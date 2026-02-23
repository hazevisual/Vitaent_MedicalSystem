using Vitaent.Application.Tenancy;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Vitaent.Api.Auth;
using Vitaent.Api.Endpoints;
using Vitaent.Api.Middleware;
using Vitaent.Api.Tenancy;
using Vitaent.Domain.Entities;
using Vitaent.Infrastructure.Persistence;
using Vitaent.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsqlOptions => npgsqlOptions.MigrationsAssembly("Vitaent.Infrastructure")));
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddSingleton<ITenantSlugResolver, TenantSlugResolver>();
builder.Services.AddScoped<RefreshTokenCookieService>();

var app = builder.Build();

try
{
    await DatabaseInitializer.InitializeAsync(app.Services, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Backend startup initialization failed. Exiting.");
    throw;
}

var fallbackUiPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "frontend-static"));
if (Directory.Exists(fallbackUiPath))
{
    var fallbackProvider = new PhysicalFileProvider(fallbackUiPath);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fallbackProvider,
        RequestPath = "/static-fallback"
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fallbackProvider,
        RequestPath = "/static-fallback"
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Development environment initialized with automatic migration and seed.");
}

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AppDbContext dbContext) =>
{
    if (!await dbContext.Database.CanConnectAsync())
    {
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database not ready");
    }

    return Results.Ok("OK");
})
   .WithName("Health")
   .WithOpenApi()
   .AllowAnonymous();

app.MapGet("/db/ping", async (AppDbContext dbContext) =>
{
    try
    {
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1;");
        await dbContext.Database.CloseConnectionAsync();

        return Results.Ok("OK");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database unreachable: {ex.Message}");
    }
})
.WithName("DatabasePing")
.WithOpenApi()
.AllowAnonymous();

app.MapGet("/api/tenant/me", async (ITenantContext tenantContext, AppDbContext dbContext) =>
{
    if (tenantContext.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(tenantContext.TenantSlug))
    {
        return Results.NotFound(new { message = "Tenant not found" });
    }

    var tenant = await dbContext.Tenants
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == tenantContext.TenantId);

    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found" });
    }

    return Results.Ok(new
    {
        tenantId = tenant.Id,
        slug = tenant.Slug,
        name = tenant.Name
    });
})
.WithName("TenantMe")
.WithOpenApi()
.AllowAnonymous();

app.MapGet("/api/tenant/branding", async (ITenantContext tenantContext, AppDbContext dbContext) =>
{
    if (tenantContext.TenantId == Guid.Empty)
    {
        return Results.NotFound(new { message = "Tenant not found" });
    }

    var branding = await dbContext.TenantBrandings
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.TenantId == tenantContext.TenantId);

    return branding is null
        ? Results.NotFound(new { message = "Branding not found" })
        : Results.Ok(new { tenantId = branding.TenantId, json = branding.Json });
})
.WithName("TenantBranding")
.WithOpenApi()
.AllowAnonymous();


app.MapGet("/api/doctors", async (AppDbContext dbContext) =>
{
    var doctors = await dbContext.Doctors
        .AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.Name)
        .Select(x => new DoctorResponse(x.Id, x.Name, x.IsActive, x.CreatedAt))
        .ToListAsync();

    return Results.Ok(doctors);
})
.WithName("GetDoctors")
.WithTags("Doctors")
.WithSummary("List active doctors")
.WithDescription("Returns active doctors for the current tenant.")
.Produces<List<DoctorResponse>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.WithOpenApi();

app.MapPost("/api/appointments", async (CreateAppointmentRequest request, AppDbContext dbContext) =>
{
    var errors = new Dictionary<string, string[]>();

    var trimmedPatientName = (request.PatientName ?? string.Empty).Trim();

    if (string.IsNullOrWhiteSpace(trimmedPatientName))
    {
        errors["patientName"] = ["patientName is required."];
    }
    else if (trimmedPatientName.Length < 2 || trimmedPatientName.Length > 120)
    {
        errors["patientName"] = ["patientName must be between 2 and 120 characters."];
    }

    Guid doctorId = default;

if (string.IsNullOrWhiteSpace(request.DoctorId))
{
    errors["doctorId"] = ["doctorId must be a valid GUID."];
}
else if (!Guid.TryParse(request.DoctorId, out doctorId))
{
    errors["doctorId"] = ["doctorId must be a valid GUID."];
}

    if (request.StartsAt == default)
    {
        errors["startsAt"] = ["startsAt is required."];
    }

    if (request.EndsAt == default)
    {
        errors["endsAt"] = ["endsAt is required."];
    }

    if (request.StartsAt != default && request.EndsAt != default)
    {
        if (request.StartsAt >= request.EndsAt)
        {
            errors["startsAt"] = ["startsAt must be before endsAt."];
        }

        var duration = request.EndsAt - request.StartsAt;
        if (duration < TimeSpan.FromMinutes(5) || duration > TimeSpan.FromMinutes(120))
        {
            errors["duration"] = ["duration must be between 5 and 120 minutes."];
        }

        if (request.StartsAt < DateTimeOffset.UtcNow.AddMinutes(-1))
        {
            errors["startsAt"] = ["startsAt must be greater than or equal to now minus 1 minute."];
        }
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(
            errors,
            title: "Appointment validation failed",
            detail: "One or more validation errors occurred.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var doctor = await dbContext.Doctors
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == doctorId && x.IsActive);

    if (doctor is null)
    {
        return Results.Problem(
            title: "Doctor not found",
            detail: "Doctor not found",
            statusCode: StatusCodes.Status404NotFound);
    }

    var appointment = new Appointment
    {
        Id = Guid.NewGuid(),
        TenantId = doctor.TenantId,
        DoctorId = doctorId,
        PatientName = trimmedPatientName,
        StartsAt = request.StartsAt,
        EndsAt = request.EndsAt,
        Status = AppointmentStatus.Pending,
        CreatedAt = DateTimeOffset.UtcNow
    };

    dbContext.Appointments.Add(appointment);

    try
    {
        await dbContext.SaveChangesAsync();
    }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
    {
        return Results.Problem(
            title: "Slot already booked",
            detail: "Slot already booked",
            statusCode: StatusCodes.Status409Conflict);
    }

    return Results.Ok(new AppointmentResponse(
        appointment.Id,
        appointment.DoctorId,
        appointment.PatientName,
        appointment.StartsAt,
        appointment.EndsAt,
        appointment.Status.ToString(),
        appointment.CreatedAt));
})
.WithName("CreateAppointment")
.WithTags("Appointments")
.WithSummary("Create appointment")
.WithDescription("Creates a pending appointment for an active doctor in the current tenant.")
.Produces<AppointmentResponse>(StatusCodes.Status200OK)
.Produces<AppointmentResponse>(StatusCodes.Status201Created)
.ProducesValidationProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status409Conflict)
.WithOpenApi(operation =>
{
    operation.RequestBody ??= new OpenApiRequestBody();
    if (operation.RequestBody.Content.TryGetValue("application/json", out var requestContent))
    {
        requestContent.Example = new OpenApiObject
        {
            ["doctorId"] = new OpenApiString("11111111-1111-1111-1111-111111111111"),
            ["patientName"] = new OpenApiString("Peter Parker"),
            ["startsAt"] = new OpenApiString("2026-03-01T10:00:00Z"),
            ["endsAt"] = new OpenApiString("2026-03-01T10:30:00Z")
        };
    }

    if (operation.Responses.TryGetValue("404", out var notFound) && notFound.Content.TryGetValue("application/problem+json", out var notFoundContent))
    {
        notFoundContent.Example = new OpenApiObject
        {
            ["title"] = new OpenApiString("Doctor not found"),
            ["detail"] = new OpenApiString("Doctor not found"),
            ["status"] = new OpenApiInteger(404)
        };
    }

    if (operation.Responses.TryGetValue("409", out var conflict) && conflict.Content.TryGetValue("application/problem+json", out var conflictContent))
    {
        conflictContent.Example = new OpenApiObject
        {
            ["title"] = new OpenApiString("Slot already booked"),
            ["detail"] = new OpenApiString("Slot already booked"),
            ["status"] = new OpenApiInteger(409)
        };
    }

    return operation;
});

app.MapGet("/api/appointments", async (DateTimeOffset from, DateTimeOffset to, AppDbContext dbContext) =>
{
    if (from >= to)
    {
        return Results.BadRequest(new { message = "from must be before to" });
    }

    var appointments = await dbContext.Appointments
        .AsNoTracking()
        .Where(x => x.StartsAt >= from && x.StartsAt < to)
        .OrderBy(x => x.StartsAt)
        .Select(x => new AppointmentResponse(x.Id, x.DoctorId, x.PatientName, x.StartsAt, x.EndsAt, x.Status.ToString(), x.CreatedAt))
        .ToListAsync();

    return Results.Ok(appointments);
})
.WithName("GetAppointments")
.WithTags("Appointments")
.WithSummary("List appointments in range")
.WithDescription("Returns tenant appointments whose startsAt is within [from, to).")
.Produces<List<AppointmentResponse>>(StatusCodes.Status200OK)
.ProducesValidationProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.WithOpenApi();

app.MapAuthEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();

public record CreateAppointmentRequest(string DoctorId, string PatientName, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public record DoctorResponse(Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAt);
public record AppointmentResponse(Guid Id, Guid DoctorId, string PatientName, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, DateTimeOffset CreatedAt);

public partial class Program;
