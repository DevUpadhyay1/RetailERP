using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Auditing;
using RetailERP.Data.Identity;
using RetailERP.Data.Seed;
using RetailERP.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;
using RetailERP.Hubs;
using RetailERP.Services.BackgroundJobs;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

// ──────────────────────────────────────────────────────
// Serilog bootstrap (catches startup exceptions)
// ──────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/retailerp-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("RetailERP starting up…");

    // Sprint 6: QuestPDF community licence (free for revenue < $1M)
    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    var builder = WebApplication.CreateBuilder(args);

    // Replace default logging with Serilog
    builder.Host.UseSerilog();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<AuditingSaveChangesInterceptor>();

    builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    {
        options.UseSqlServer(connectionString);
        options.AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>());
    });

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // ──────────────────────────────────────────────────────
    // Identity + Roles
    // ──────────────────────────────────────────────────────
    builder.Services
        .AddDefaultIdentity<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.User.RequireUniqueEmail = true;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddClaimsPrincipalFactory<TenantClaimsPrincipalFactory>();  // Sprint 4

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login";
        options.AccessDeniedPath = "/Identity/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        options.Events.OnValidatePrincipal = async context =>
        {
            if (context.Principal?.Identity?.IsAuthenticated != true)
                return;

            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.GetUserAsync(context.Principal);
            if (user is null)
                return;

            if (!user.IsActive)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            }
        };
    });

    // ──────────────────────────────────────────────────────
    // Sprint 5: JWT Bearer Authentication (for REST API)
    // ──────────────────────────────────────────────────────
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtOpts = new JwtOptions();
    jwtSection.Bind(jwtOpts);
    builder.Services.AddSingleton(jwtOpts);
    builder.Services.AddSingleton<JwtTokenService>();

    builder.Services.AddAuthentication()
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOpts.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOpts.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.SecretKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

    // ──────────────────────────────────────────────────────
    // Rate Limiting (Sprint 1)
    // ──────────────────────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Login / Register — 5 attempts per minute per IP
        options.AddPolicy("Login", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        // POS AJAX — 60 requests per minute per user
        options.AddPolicy("Pos", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 2
                }));

        // General API — 100 requests per minute per IP
        options.AddPolicy("Api", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        // Global concurrency limiter — max 200 concurrent requests
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetConcurrencyLimiter(
                partitionKey: "global",
                factory: _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 200,
                    QueueLimit = 50,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));
    });

    // ──────────────────────────────────────────────────────
    // Antiforgery — Cookie-to-Header for AJAX (Sprint 1)
    // Framework's own cookie (.AspNetCore.Antiforgery.*) stays default (HttpOnly).
    // A separate "XSRF-TOKEN" cookie with the request-token is emitted
    // by middleware below so JS can read it and send it as the header.
    // ──────────────────────────────────────────────────────
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-XSRF-TOKEN";       // JS sends cookie value in this header
    });

    builder.Services.AddControllersWithViews();
    builder.Services.AddRazorPages();

    // ──────────────────────────────────────────────────────
    // Sprint 5: Swagger / OpenAPI
    // ──────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "RetailERP API",
            Version = "v1",
            Description = "Sprint 5 — REST API with JWT authentication for RetailERP"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token"
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

    // Auth-by-default
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    // ──────────────────────────────────────────────────────
    // Health Checks (Sprint 1)
    // ──────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddSqlServer(connectionString, name: "sqlserver", tags: new[] { "db", "ready" });

    // App services
    builder.Services.AddScoped<InvoiceService>();
    builder.Services.AddScoped<PurchaseService>();
    builder.Services.AddScoped<PosBillingService>();
    builder.Services.AddScoped<LoyaltyService>();
    builder.Services.AddScoped<CouponService>();
    builder.Services.AddScoped<EodService>();
    builder.Services.AddScoped<SyncService>();
    builder.Services.AddScoped<AuditService>();
    builder.Services.AddScoped<PromotionService>();  // Sprint 7
    builder.Services.AddScoped<GstReportService>();   // Sprint 8
    builder.Services.AddScoped<EInvoiceService>();    // Sprint 8
    builder.Services.AddScoped<ItemOnboardingService>();
    builder.Services.AddScoped<PortalService>();      // Sprint 14

    // Sprint 9: SignalR + Background Jobs
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<EmailQueueService>();
    builder.Services.AddHostedService<EmailSenderWorker>();
    builder.Services.AddHostedService<StockAlertWorker>();
    builder.Services.AddHostedService<SyncQueueWorker>();
    builder.Services.AddHostedService<EodAutoWorker>();

    // Sprint 2: Razorpay payment gateway
    builder.Services.Configure<RazorpayOptions>(builder.Configuration.GetSection("Razorpay"));
    builder.Services.AddHttpClient<RazorpayService>();

    // Sprint 3: Dashboard service
    builder.Services.AddScoped<DashboardService>();

    // Sprint 12: Barcode label printing
    builder.Services.AddScoped<BarcodeLabelService>();

    // Sprint 13: Forecasting + reorder + anomaly detection
    builder.Services.AddScoped<ForecastService>();

    // Sprint 11: SMS / WhatsApp / Email notification services
    builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection("Twilio"));
    builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
    builder.Services.AddHttpClient<SmsService>();
    builder.Services.AddHttpClient<WhatsAppService>();
    builder.Services.AddScoped<NotificationService>();

    // Sprint 6: PDF services
    builder.Services.AddScoped<ReceiptPdfService>();
    builder.Services.AddScoped<InvoicePdfService>();

    // Sprint 4: Multi-tenant provider
    builder.Services.AddScoped<ITenantProvider, TenantProvider>();
    builder.Services.AddScoped<CacheService>();

    // Sprint 4: Redis distributed cache (falls back to in-memory if Redis unavailable)
    var redisConn = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(redisConn))
    {
        try
        {
            // Test the connection at startup
            var redis = ConnectionMultiplexer.Connect(redisConn + ",abortConnect=false,connectTimeout=3000");
            redis.Dispose();
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConn;
                options.InstanceName = "RetailERP:";
            });
            Log.Information("Sprint 4: Redis cache connected at {RedisConn}", redisConn);
        }
        catch
        {
            Log.Warning("Sprint 4: Redis unavailable at {RedisConn} — falling back to in-memory cache", redisConn);
            builder.Services.AddDistributedMemoryCache();
        }
    }
    else
    {
        Log.Information("Sprint 4: No Redis connection string — using in-memory distributed cache");
        builder.Services.AddDistributedMemoryCache();
    }

    // Sprint 15: Franchise management
    builder.Services.AddScoped<FranchiseService>();

    // Sprint 15: Multi-language localization (Hindi, Gujarati, Marathi)
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
    builder.Services.AddControllersWithViews()
        .AddViewLocalization()
        .AddDataAnnotationsLocalization();
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        var supportedCultures = new[]
        {
            new CultureInfo("en"),
            new CultureInfo("hi"),
            new CultureInfo("gu"),
            new CultureInfo("mr")
        };
        options.DefaultRequestCulture = new RequestCulture("en");
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;
        options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
    });

    builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
    builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

    // Seeder
    builder.Services.AddTransient<DbSeeder>();

    var app = builder.Build();

    // ──────────────────────────────────────────────────────
    // Middleware Pipeline
    // ──────────────────────────────────────────────────────
    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();

        // Sprint 5: Swagger UI
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "RetailERP API v1");
            c.RoutePrefix = "swagger";
        });

        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    // Serilog request logging (enriched with HTTP info)
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
            diagnosticContext.Set("UserName", httpContext.User?.Identity?.Name ?? "anonymous");
        };
    });

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // Security headers (Sprint 1 — enhanced with CSP)
    app.Use(async (context, next) =>
    {
        var h = context.Response.Headers;
        h.TryAdd("X-Content-Type-Options", "nosniff");
        h.TryAdd("X-Frame-Options", "DENY");
        h.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        h.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        h.TryAdd("X-XSS-Protection", "1; mode=block");
        h.TryAdd("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://checkout.razorpay.com https://api.razorpay.com; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "font-src 'self' https://cdn.jsdelivr.net data:; " +
            "img-src 'self' data: https://rzp.io https://lumberjack.razorpay.com; " +
            "connect-src 'self' wss: ws: https://cdn.jsdelivr.net https://api.razorpay.com https://lumberjack.razorpay.com; " +
            "frame-src https://api.razorpay.com https://checkout.razorpay.com; " +
            "frame-ancestors 'none';");
        await next();
    });

    // Sprint 15: Request localization middleware
    app.UseRequestLocalization();

    app.UseRouting();

    app.UseRateLimiter();          // Sprint 1 — rate limiting
    app.UseAuthentication();
    app.UseAuthorization();

    // Emit XSRF-TOKEN cookie on every authenticated response (Sprint 1)
    app.Use(async (context, next) =>
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var antiforgery = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!,
                new CookieOptions { HttpOnly = false, SameSite = SameSiteMode.Strict, Secure = context.Request.IsHttps });
        }
        await next();
    });

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapRazorPages();

    // Sprint 9: SignalR hub endpoint
    app.MapHub<RetailHub>("/hubs/retail");

    // Health check endpoint — anonymous access
    app.MapHealthChecks("/health").AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "RetailERP terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
