using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RetailERP.Data;
using RetailERP.Data.Auditing;
using RetailERP.Data.Identity;
using RetailERP.Data.Seed;
using RetailERP.Hubs;
using RetailERP.Infrastructure.Production;
using RetailERP.Services;
using RetailERP.Services.BackgroundJobs;
using Serilog;
using StackExchange.Redis;

namespace RetailERP.Infrastructure;

/// <summary>
/// Registers all application services (extracted from <see cref="Program"/> for maintainability).
/// </summary>
public static class WebApplicationBuilderExtensions
{
    public static void AddRetailErp(this WebApplicationBuilder builder)
    {
        ProductionStartupValidation.ThrowIfInvalidForProduction(builder.Environment, builder.Configuration);

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
            .AddClaimsPrincipalFactory<TenantClaimsPrincipalFactory>();

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

            if (builder.Environment.IsProduction())
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
            }
        });

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

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("ApiCors", policy =>
            {
                var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
                var validOrigins = origins?
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<string>();

                if (validOrigins.Length == 0)
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                        Log.Information("CORS: Development mode with AllowAnyOrigin.");
                    }
                    else
                    {
                        // Production fallback: deny cross-origin browser calls unless explicitly configured.
                        policy.SetIsOriginAllowed(_ => false);
                        Log.Warning("CORS: No Cors:AllowedOrigins configured for Production. Cross-origin browser requests will be blocked.");
                    }
                }
                else
                {
                    policy.WithOrigins(validOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                    Log.Information("CORS: Allowed origins configured: {Origins}", string.Join(", ", validOrigins));
                }
            });
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("Login", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy("Pos", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 2
                    }));

            options.AddPolicy("Api", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

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

        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            if (builder.Environment.IsProduction())
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        // Single registration: MVC + view localization (avoid duplicate AddControllersWithViews)
        builder.Services.AddControllersWithViews(options =>
            {
                // Enforce CSRF validation for all unsafe MVC actions by default.
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            })
            .AddViewLocalization()
            .AddDataAnnotationsLocalization();
        builder.Services.AddRazorPages();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "RetailERP API",
                Version = "v1",
                Description = "REST API with JWT authentication for RetailERP"
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

        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // Redis (optional) — must be resolved before health checks so we can probe the same connection.
        var redisConn = builder.Configuration.GetConnectionString("Redis");
        var redisCacheEnabled = false;
        if (!string.IsNullOrEmpty(redisConn))
        {
            try
            {
                var redisProbe = ConnectionMultiplexer.Connect(redisConn + ",abortConnect=false,connectTimeout=3000");
                redisProbe.Dispose();
                builder.Services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConn;
                    options.InstanceName = "RetailERP:";
                });
                redisCacheEnabled = true;
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

        var healthChecks = builder.Services.AddHealthChecks()
            .AddSqlServer(connectionString, name: "sqlserver", tags: new[] { "db", "ready" });
        if (redisCacheEnabled && !string.IsNullOrEmpty(redisConn))
            healthChecks.AddRedis(redisConn, name: "redis", tags: new[] { "cache", "ready" });

        builder.Services.AddScoped<InvoiceService>();
        builder.Services.AddScoped<PurchaseService>();
        builder.Services.AddScoped<PosBillingService>();
        builder.Services.AddScoped<LoyaltyService>();
        builder.Services.AddScoped<CouponService>();
        builder.Services.AddScoped<EodService>();
        builder.Services.AddScoped<SyncService>();
        builder.Services.AddScoped<AuditService>();
        builder.Services.AddScoped<PromotionService>();
        builder.Services.AddScoped<GstReportService>();
        builder.Services.AddScoped<EInvoiceService>();
        builder.Services.AddScoped<ItemOnboardingService>();
        builder.Services.AddScoped<CustomerOnboardingService>();
        builder.Services.AddScoped<SupplierOnboardingService>();
        builder.Services.AddScoped<PortalService>();

        builder.Services.AddSignalR();
        builder.Services.AddSingleton<EmailQueueService>();
        builder.Services.AddHostedService<EmailSenderWorker>();
        builder.Services.AddHostedService<StockAlertWorker>();
        builder.Services.AddHostedService<SyncQueueWorker>();
        builder.Services.AddHostedService<EodAutoWorker>();

        builder.Services.Configure<RazorpayOptions>(builder.Configuration.GetSection("Razorpay"));
        builder.Services.AddHttpClient<RazorpayService>();

        builder.Services.AddScoped<DashboardService>();
        builder.Services.AddScoped<BarcodeLabelService>();
        builder.Services.AddScoped<ForecastService>();

        builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection("Twilio"));
        builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
        builder.Services.AddHttpClient<SmsService>();
        builder.Services.AddHttpClient<WhatsAppService>();
        builder.Services.AddScoped<NotificationService>();

        builder.Services.AddScoped<ReceiptPdfService>();
        builder.Services.AddScoped<InvoicePdfService>();

        builder.Services.AddScoped<ITenantProvider, TenantProvider>();
        builder.Services.AddScoped<CacheService>();

        builder.Services.AddScoped<FranchiseService>();

        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
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

        builder.Services.AddTransient<DbSeeder>();

        // Behind nginx / cloud load balancer: correct scheme and client IP for HTTPS redirects and logs.
        if (!builder.Environment.IsDevelopment())
        {
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(180);
                options.IncludeSubDomains = true;
            });
        }
    }
}
