using Dashboard.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

#region Domain Layer
using Domain.Repositories;
using Domain.Repositories.Users;
#endregion
#region Application Layer
using Application;
using Application.Service.Auth;
using Application.Service.Nifi;
#endregion
#region Infrastructure Layer
using Infrastructure.Persistence.Repositories;
using Infrastructure.Persistence;
#endregion

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });
builder.Services.AddControllers();
builder.Services.AddHttpClient();


#region DbContext
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
#endregion

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

#region AddScoped

#region Authentication & Authorization
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILdapAuthService, LdapAuthService>();
builder.Services.AddScoped<IAuthService, AuthService>();
#endregion

#region NIFI Integration
builder.Services.AddHttpClient<INifiService, NifiService>();
#endregion

#endregion

#region JWT Configuration
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"];

if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT SecretKey is not configured in appsettings.json");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
#endregion

#region AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
#endregion

var app = builder.Build();

// Development only: auto-migrate and seed database
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            logger.LogInformation("Applying pending database migrations (Development)...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations.");
            throw;
        }
    }

    await Infrastructure.Persistence.DatabaseSeeder.SeedDevelopmentDataAsync(app.Services, app.Environment.EnvironmentName);
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
