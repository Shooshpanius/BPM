using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Domain.Auth;
using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Infrastructure.Persistence;

/// <summary>Контекст базы данных приложения.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<OrgUser> OrgUsers => Set<OrgUser>();
    public DbSet<OrgOrganization> OrgOrganizations => Set<OrgOrganization>();
    public DbSet<OrgEmployee> OrgEmployees => Set<OrgEmployee>();
    public DbSet<AuthAccount> AuthAccounts => Set<AuthAccount>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuthRole> AuthRoles => Set<AuthRole>();
    public DbSet<AuthUserRole> AuthUserRoles => Set<AuthUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Таблица профилей пользователей
        modelBuilder.Entity<OrgUser>(e =>
        {
            e.ToTable("org_users");
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.WorkEmail).IsUnique();
            e.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
            e.Property(u => u.LastName).IsRequired().HasMaxLength(100);
            e.Property(u => u.MiddleName).HasMaxLength(100);
            e.Property(u => u.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(u => u.WorkEmail).IsRequired().HasMaxLength(320);
            e.Property(u => u.Phone).HasMaxLength(50);
            e.Property(u => u.AvatarUrl).HasMaxLength(500);
        });

        // Таблица организаций
        modelBuilder.Entity<OrgOrganization>(e =>
        {
            e.ToTable("org_organizations");
            e.HasKey(o => o.Id);
            e.Property(o => o.Name).IsRequired().HasMaxLength(300);
            e.Property(o => o.Description).HasMaxLength(1000);

            // Только одна организация может иметь признак IsPrimary = true
            e.HasIndex(o => o.IsPrimary)
             .HasFilter("is_primary = true")
             .IsUnique();
        });

        // Таблица сотрудников (связь пользователь ↔ организация)
        modelBuilder.Entity<OrgEmployee>(e =>
        {
            e.ToTable("org_employees");
            e.HasKey(emp => emp.Id);
            e.Property(emp => emp.Position).HasMaxLength(200);

            // Пара пользователь–организация уникальна
            e.HasIndex(emp => new { emp.UserId, emp.OrganizationId }).IsUnique();

            e.HasOne(emp => emp.User)
             .WithMany(u => u.Employees)
             .HasForeignKey(emp => emp.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(emp => emp.Organization)
             .WithMany(o => o.Employees)
             .HasForeignKey(emp => emp.OrganizationId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Таблица учётных записей
        modelBuilder.Entity<AuthAccount>(e =>
        {
            e.ToTable("auth_accounts");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Username).IsUnique();
            e.Property(a => a.Username).IsRequired().HasMaxLength(100);
            e.Property(a => a.PasswordHash).IsRequired();

            // FK → org_users
            e.HasOne(a => a.User)
             .WithOne(u => u.Account)
             .HasForeignKey<AuthAccount>(a => a.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Таблица сессий
        modelBuilder.Entity<AuthSession>(e =>
        {
            e.ToTable("auth_sessions");
            e.HasKey(s => s.Id);
            e.Property(s => s.RefreshTokenHash).IsRequired();
            e.Property(s => s.DeviceInfo).HasMaxLength(500);
            e.Property(s => s.IpAddress).HasMaxLength(45);

            // Каскадное удаление сессий при удалении аккаунта
            e.HasOne(s => s.Account)
             .WithMany(a => a.Sessions)
             .HasForeignKey(s => s.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Таблица ролей
        modelBuilder.Entity<AuthRole>(e =>
        {
            e.ToTable("auth_roles");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Name).IsRequired().HasMaxLength(100);
            e.Property(r => r.Description).HasMaxLength(500);
        });

        // Таблица связи пользователь–роль
        modelBuilder.Entity<AuthUserRole>(e =>
        {
            e.ToTable("auth_user_roles");
            e.HasKey(ur => ur.Id);

            e.HasOne(ur => ur.Account)
             .WithMany(a => a.UserRoles)
             .HasForeignKey(ur => ur.AccountId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ur => ur.Role)
             .WithMany(r => r.UserRoles)
             .HasForeignKey(ur => ur.RoleId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed системных ролей
        SeedSystemRoles(modelBuilder);
    }

    private static void SeedSystemRoles(ModelBuilder modelBuilder)
    {
        var now = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<AuthRole>().HasData(
            new AuthRole
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                Name = "Admin",
                Description = "Системный администратор",
                IsSystem = true,
                CreatedAt = now
            },
            new AuthRole
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                Name = "User",
                Description = "Обычный пользователь",
                IsSystem = true,
                CreatedAt = now
            },
            new AuthRole
            {
                Id = new Guid("00000000-0000-0000-0000-000000000003"),
                Name = "Guest",
                Description = "Гость",
                IsSystem = true,
                CreatedAt = now
            }
        );
    }
}
