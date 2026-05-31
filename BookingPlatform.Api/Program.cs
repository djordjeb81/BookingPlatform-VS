using System.Text;
using BookingPlatform.Api.Services;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using BookingPlatform.Api.Setup;
using BookingPlatform.Contracts.Common;
using System.Text.Json;
using BookingPlatform.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<DevelopmentSeedOptions>(
builder.Configuration.GetSection("SeedData"));
builder.Services.AddScoped<IStaffScheduleResolver, StaffScheduleResolver>();
builder.Services.AddScoped<AppointmentCleanupService>();
builder.Services.AddHostedService<AppointmentCleanupHostedService>();
builder.Services.AddScoped<IBusinessCustomerLinkingService, BusinessCustomerLinkingService>();
builder.Services.AddScoped<IClientRegistrationCodeService, ClientRegistrationCodeService>();
builder.Services.AddScoped<BusinessCustomerCleanupService>();
builder.Services.AddHostedService<BusinessCustomerCleanupHostedService>();
builder.Services.AddScoped<IFirebasePushNotificationService, FirebasePushNotificationService>();
builder.Services.AddScoped<IChatSystemMessageService, ChatSystemMessageService>();
builder.Services.AddScoped<ISystemAlarmService, SystemAlarmService>();
builder.Services.AddSignalR();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BookingPlatform API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Unesi samo JWT token, bez prefiksa Bearer."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAppointmentSchedulingService, AppointmentSchedulingService>();
builder.Services.AddScoped<IAppointmentWorkflowService, AppointmentWorkflowService>();
builder.Services.AddScoped<IDeviceLicenseService, DeviceLicenseService>();

var jwtKey = builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Jwt:Key nije podešen.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("Jwt:Issuer nije podešen.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
                  ?? throw new InvalidOperationException("Jwt:Audience nije podešen.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";

                var payload = JsonSerializer.Serialize(new ApiErrorResponse
                {
                    Message = "Korisnik nije autentifikovan.",
                    ReasonCode = "unauthenticated",
                    ReasonCodes = new List<string> { "unauthenticated" }
                });

                await context.Response.WriteAsync(payload);
            }
        },
        OnForbidden = async context =>
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";

                var payload = JsonSerializer.Serialize(new ApiErrorResponse
                {
                    Message = "Korisnik nema dozvolu za ovu radnju.",
                    ReasonCode = "forbidden",
                    ReasonCodes = new List<string> { "forbidden" }
                });

                await context.Response.WriteAsync(payload);
            }
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();
await DevelopmentDataSeeder.SeedAsync(app);

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<BusinessActivityHub>("/hubs/business-activity");

app.Run();

public partial class Program
{
}