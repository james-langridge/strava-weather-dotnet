using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StravaWeather.Api.Data;
using StravaWeather.Api.Extensions;
using StravaWeather.Api.Middleware;
using System.Text;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting web application");
    
    var builder = WebApplication.CreateBuilder(args);
    
    // Add Serilog
    builder.Host.UseSerilog();
    
    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    // Configure Database
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
    // Configure Authentication
    var jwtSecret = builder.Configuration["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET not configured");
    var key = Encoding.UTF8.GetBytes(jwtSecret);
    
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = "strava-weather-api",
            ValidateAudience = true,
            ValidAudience = "strava-weather-client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        
        // Read JWT from cookie
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies["strava-weather-session"];
                return Task.CompletedTask;
            }
        };
    });
    
    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWeb", policy =>
        {
            var allowedOrigins = builder.Configuration["APP_URL"]?.Split(',') ?? new[] { "http://localhost:5173" };
            policy.WithOrigins(allowedOrigins)
                  .AllowCredentials()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
    
    // Register application services
    builder.Services.AddApplicationServices(builder.Configuration);
    
    // Add HttpClient
    builder.Services.AddHttpClient();
    
    // Add memory cache
    builder.Services.AddMemoryCache();
    
    // Add response caching
    builder.Services.AddResponseCaching();
    
    // Build app
    var app = builder.Build();
    
    // Configure the HTTP request pipeline
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    
    app.UseHttpsRedirection();
    app.UseCors("AllowWeb");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseResponseCaching();
    
    app.MapControllers();
    
    // Ensure database is created and migrations applied
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (dbContext.Database.GetPendingMigrations().Any())
        {
            Log.Information("Applying database migrations");
            dbContext.Database.Migrate();
        }
    }
    
    // Initialize webhook subscription in production
    if (app.Environment.IsProduction())
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000); // Wait for app to fully start
            using var scope = app.Services.CreateScope();
            var webhookService = scope.ServiceProvider.GetRequiredService<IWebhookSubscriptionService>();
            try
            {
                await webhookService.EnsureSubscriptionExistsAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to ensure webhook subscription exists");
            }
        });
    }
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}