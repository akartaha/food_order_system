using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using food_order_system1.customAuthorization;
using food_order_system1.Data;
using food_order_system1.Middleware;
using food_order_system1.Middlware;
using food_order_system1.Modles;
using food_order_system1.Service;
using food_order_system1.Service.RestaurantService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


builder.Services.AddEndpointsApiExplorer();
//--------------------------------------------
// Add Serilog system
// -------------------------------------------

Log.Logger = new LoggerConfiguration()
               .Enrich.FromLogContext()
              .MinimumLevel.Information()
             //  .WriteTo.Console(new CompactJsonFormatter())
              .WriteTo.Console(outputTemplate:"[{Timestamp:HH:mm:ss} {Level}] [User: {UserId}] {Message:lj}{NewLine}{Exception}")
             
              .WriteTo.File(
                   formatter: new CompactJsonFormatter(),
                   path: "logs/log-.json",
                   rollingInterval: RollingInterval.Day)
              .CreateLogger();


builder.Logging.ClearProviders(); // ❌ remove default logging
builder.Host.UseSerilog();

//--------------------------------------------
// add caching system (in-memory) & (redis)
//-------------------------------------------
builder.Services.AddMemoryCache();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["RedisConnection:Configuration"];
    options.InstanceName = builder.Configuration["RedisConnection:InstanceName"];
});
//--------------------------------------------
//  add service layer
//-------------------------------------------
builder.Services.AddScoped<IRestauantService, RestaurantService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IAuthService,AuthService >();
builder.Services.AddScoped<IItemService,ItemService >();
builder.Services.AddScoped<IMenuService,MenuService >();
builder.Services.AddScoped<IOrderSerivce,OrderSerivce >();
builder.Services.AddScoped<IUserService,UserService >();
builder.Services.AddScoped<IcacheService,CacheService >();
builder.Services.AddScoped<ICustomRateLimiter,CustomRateLimiter >();


//---------------------------------------------
// reate limiting service 
//---------------------------------------------
builder.Services.AddRateLimiter(options =>
options.AddFixedWindowLimiter("fixed" , opt =>
{
    opt.PermitLimit=10 ;
    opt.Window = TimeSpan.FromMinutes(3);
    opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    opt.QueueLimit = 2;
}
)
);
//---------------------------------------------
// email service 
//--------------------------------------------

builder.Services.Configure<EmailSetting>(
    builder.Configuration.GetSection("EmailSettings")
);

builder.Services.AddScoped<IEmailService, EmailService>();
//---------------------------------------------
// Add Authentication (jwt)
//---------------------------------------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {

           ValidateIssuer = true,
            ValidIssuer = builder.Configuration["jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["jwt:Key"]))
    };
   options.Events = new JwtBearerEvents
{
OnAuthenticationFailed = context =>
{
    context.NoResult();   // IMPORTANT: stop pipeline

    context.Response.StatusCode = 401;
    context.Response.ContentType = "application/json";

    return context.Response.WriteAsync(
        "{\"message\": \"Authentication failed. Token invalid or expired.\"}");
},

    OnChallenge = context =>
    {
        context.HandleResponse(); // prevent default 302 redirect
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            "{\"message\": \"Authentication required or token missing.\"}");
    },

    OnForbidden = context =>
    {
        context.Response.StatusCode = 403;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            "{\"message\": \"You do not have permission to access this resource.\"}");
    }
};
})

.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"];
    options.ClientSecret = builder.Configuration["Google:ClientSecret"];
    options.CallbackPath = "/signin-google";
    options.SignInScheme = IdentityConstants.ExternalScheme;
});

//---------------------------------------------------------
// configure cookie 
//---------------------------------------------------------

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

//--------------------------------------------------------
// configure Swager 
//-------------------------------------------------------
builder.Services.AddSwaggerGen(c =>
{
     var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
    
       c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Food Order API",
        Description = "API for managing food orders, customers, and restaurants"
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

//--------------------------------------------------------
// ADD Databse(MYSQL) Connection 
//--------------------------------------------------------
builder.Services.AddDbContext<AppUser>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MariaDbServerVersion(new Version(8, 0, 26))));


//--------------------------------------------------------
// ADD Identity user 
//--------------------------------------------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5); // Lock for 5 minutes
    options.Lockout.MaxFailedAccessAttempts = 5; // 5 wrong attempts allowed
    options.Lockout.AllowedForNewUsers = true;

     options.User.RequireUniqueEmail = true;

}
).AddEntityFrameworkStores<AppUser>()
    .AddDefaultTokenProviders();

//--------------------------------------------------------
// ADD Authorization policies and handlers
//--------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CartOwnerShipPolicy", policy =>
        policy.Requirements.Add(new CartOwnerShipRequirement()));

         options.AddPolicy("UserOwnerShipPolicy", policy =>
        policy.Requirements.Add(new UserOwnerShipRequirement()));
 
    options.AddPolicy("RestaurantOwnerShipAndAdminPolicy", policy =>
        policy.Requirements.Add(new RestauantOwnerShipAndAdminRequirement()));
    
});

builder.Services.AddScoped<IAuthorizationHandler, CartOwnerShipAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OwnerAndAdminAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, UserOwnerShipAuthorizationHandler>();

builder.Services.AddControllers();



var app = builder.Build();

//--------------------------------------------------------------------
// automatically log all requests and responses, including exceptions
//--------------------------------------------------------------------
app.UseSerilogRequestLogging(options=> {
     options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        diagnosticContext.Set("UserId", userId ?? "Anonymous");

        var correlationId = httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault();
        diagnosticContext.Set("CorrelationId", correlationId);
};}); // 🔥 THIS LINE DOES EVERYTHING

// -----------------
// Seed Roles
// -----------------
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Admin", "Customer", "RestaurantManager" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
//app.UseRateLimiter();
// optional, for catching exceptions
 // for enriching log context with user informatio// must come after Authentication

if (!app.Environment.IsEnvironment("Testing")){
    app.UseMiddleware<CorrelationIdMiddleware>();
}
app.UseAuthentication();
app.UseAuthorization();
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseMiddleware<RateLimitingMiddlware>();

app.UseMiddleware<UserEnrichmentMiddleware>();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
}
app.MapControllers();
app.Run();

public partial class Program { }