using CoreBPM.Server.Application.Admin.Interfaces;
using CoreBPM.Server.Application.Admin.Services;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Application.Org.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CoreBPM.Server.Application.Auth.Interfaces;
using CoreBPM.Server.Application.Auth.Services;
using CoreBPM.Server.Infrastructure.Middleware;
using CoreBPM.Server.Infrastructure.Persistence;
using CoreBPM.Server.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Чтение переменных окружения с префиксом BPM_S_
builder.Configuration.AddEnvironmentVariables("BPM_S_");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Подключение к PostgreSQL через Npgsql с именованием колонок в snake_case
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// Настройка JWT-аутентификации
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey не настроен");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "CoreBPM",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "CoreBPM",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Регистрация сервисов
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Регистрация сервисов административной панели
builder.Services.AddScoped<IAdminOrganizationService, AdminOrganizationService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminDepartmentService, AdminDepartmentService>();
builder.Services.AddScoped<IAdminEmployeeService, AdminEmployeeService>();

// Регистрация сервисов Org (адресная книга и управление подразделениями)
builder.Services.AddScoped<IOrgDirectoryService, OrgDirectoryService>();
builder.Services.AddScoped<IOrgUnitsService, OrgUnitsService>();

var app = builder.Build();

// ExceptionHandlingMiddleware должен быть первым
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

// Применение миграций и инициализация базы данных при старте
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
try
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    startupLogger.LogInformation("Миграции базы данных применены успешно.");
}
catch (Exception ex)
{
    startupLogger.LogError(ex, "Ошибка при применении миграций базы данных.");
    throw;
}

// Инициализация администратора при первом развёртывании
var seederLogger = app.Services.GetRequiredService<ILogger<Program>>();
try
{
    await AdminSeeder.SeedAsync(app.Services, seederLogger);
}
catch (Exception ex)
{
    seederLogger.LogError(ex, "Ошибка при инициализации пользователя admin. Проверьте подключение к БД и настройки конфигурации.");
}

app.Run();
