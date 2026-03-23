using System.Text;
using food_order_system1.customAuthorization;
using food_order_system1.Data;
using food_order_system1.Middleware;
using food_order_system1.Modles;
using food_order_system1.Service;
using food_order_system1.Service.RestaurantService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


builder.Services.AddEndpointsApiExplorer();
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
    c.SwaggerDoc("v1", new() { Title = "My API", Version = "v1" });
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

// ADD Identity user 
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

}

).AddEntityFrameworkStores<AppUser>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UserOwnerShipPolicy", policy =>
        policy.Requirements.Add(new UserOwnerShipRequirement()));
 
    options.AddPolicy("RestauantOwnerShipAndAdminPolicy", policy =>
        policy.Requirements.Add(new RestauantOwnerShipAndAdminRequirement()));
    
});

builder.Services.AddScoped<IAuthorizationHandler, UserOwnerShipAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OwnerAndAdminAuthorizationHandler>();

builder.Services.AddControllers();

var app = builder.Build();

// -----------------
// Seed Roles
// -----------------
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


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); // optional, for catching exceptions

app.UseAuthentication();  // must come before Authorization
app.UseAuthorization();   // must come after Authentication


app.MapControllers();

app.Run();



