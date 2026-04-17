using JwtAuthApi_Sonnet45.Application.Services;
using JwtAuthApi_Sonnet45.Data;
using JwtAuthApi_Sonnet45.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;


// AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// // Configure Serilog
// Log.Logger = new LoggerConfiguration()
//     .ReadFrom.Configuration(builder.Configuration)
//     .Enrich.FromLogContext()
//     .WriteTo.Console()
//     .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
//     .CreateLogger();
// builder.Host.UseSerilog();
#region by human
builder.Host.UseSerilog((_, config) => config.ReadFrom.Configuration(builder.Configuration));
#endregion

// Register application services with proper lifetimes
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Add DbContext with connection string
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("CorsOrigins").Get<string[]>())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("RefreshTokenJwtSettings");

var keyFromConfig = jwtSettings["Key"];
Console.WriteLine($"DEBUG JWT KEY: {keyFromConfig}");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = false;
    options.RequireHttpsMetadata = false; // Allow HTTP in development
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero, // remove default 5-minute grace so expired tokens are rejected immediately
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"])),
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            Console.WriteLine($"RAW AUTH HEADER: {context.Request.Headers.Authorization}");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("TOKEN ERROR: " + context.Exception);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});

// // Configure Rate Limiting
// builder.Services.AddRateLimiter(options =>
// {
//     options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
//         RateLimitPartition.GetFixedWindowLimiter(
//             partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
//             factory: _ => new FixedWindowRateLimiterOptions
//             {
//                 PermitLimit = 100,
//                 Window = TimeSpan.FromMinutes(1),
//                 QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
//                 QueueLimit = 2
//             }));

//     options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
// });
#region by human
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 4;
        opt.Window = TimeSpan.FromSeconds(12);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});
#endregion

// // Add Response Compression
// builder.Services.AddResponseCompression(options =>
// {
//     options.EnableForHttps = true;
// });

// // Add Health Checks
// builder.Services.AddHealthChecks()
//     .AddDbContextCheck<ApplicationDbContext>();

builder.Services.AddControllers();

// Configure Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "CMS API", Version = "v1" });

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer", // lowercase per RFC 7235
        BearerFormat = "JWT",
        Description = "JWT Authorization header using Bearer scheme"
    });
    // options.AddSecurityDefinition("X-Device-Id", new OpenApiSecurityScheme
    // {
    //     Description = "Device identifier (required for auth endpoints even when not logged in)",
    //     Name = "X-Device-Id",                  // Tên header
    //     In = ParameterLocation.Header,         // Gửi qua header
    //     Type = SecuritySchemeType.ApiKey,      // Kiểu apiKey
    //     Scheme = "X-Device-Id"                 // Tên scheme
    // });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "bearer"
                }
            },
            Array.Empty<string>()
        },
        // // X-Device-Id
        // {
        //     new OpenApiSecurityScheme
        //     {
        //         Reference = new OpenApiReference
        //         {
        //             Type = ReferenceType.SecurityScheme,
        //             Id = "X-Device-Id"
        //         }
        //     },
        //     Array.Empty<string>()
        // }
    });
});

// root error at: Application\Services\AuthService.cs
var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();

// app.UseResponseCompression();
app.UseCors("AllowSpecificOrigins");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// app.MapHealthChecks("/health");

// Database migration and seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
        await DbInitializer.SeedAsync(context);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while migrating or seeding the database");
    }
}

Log.Information("Application starting...");
app.Run();