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
    public DbSet<OrgDepartment> OrgDepartments => Set<OrgDepartment>();
    public DbSet<OrgDepartmentHistory> OrgDepartmentHistories => Set<OrgDepartmentHistory>();
    public DbSet<OrgEmployee> OrgEmployees => Set<OrgEmployee>();
    public DbSet<OrgPosition> OrgPositions => Set<OrgPosition>();
    public DbSet<OrgPositionAttachment> OrgPositionAttachments => Set<OrgPositionAttachment>();
    public DbSet<OrgPositionRoleMapping> OrgPositionRoleMappings => Set<OrgPositionRoleMapping>();
    public DbSet<OrgPositionAssignment> OrgPositionAssignments => Set<OrgPositionAssignment>();
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

        // Таблица сотрудников (связь пользователь ↔ организация ↔ подразделение ↔ должность)
        modelBuilder.Entity<OrgEmployee>(e =>
        {
            e.ToTable("org_employees");
            e.HasKey(emp => emp.Id);

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

            e.HasOne(emp => emp.Department)
             .WithMany(d => d.Employees)
             .HasForeignKey(emp => emp.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);

            e.HasOne(emp => emp.JobPosition)
             .WithMany()
             .HasForeignKey(emp => emp.PositionId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        // Таблица подразделений
        modelBuilder.Entity<OrgDepartment>(e =>
        {
            e.ToTable("org_departments");
            e.HasKey(d => d.Id);
            e.Property(d => d.Name).IsRequired().HasMaxLength(300);
            e.Property(d => d.ShortName).HasMaxLength(50);
            e.Property(d => d.Code).HasMaxLength(50);
            e.Property(d => d.Description).HasMaxLength(1000);
            e.Property(d => d.Path).IsRequired().HasMaxLength(1000);
            e.Property(d => d.Status).HasConversion<int>();

            // Уникальный код в рамках организации (только там, где Code != null)
            e.HasIndex(d => new { d.OrganizationId, d.Code })
             .HasFilter("code IS NOT NULL")
             .IsUnique();

            e.HasOne(d => d.Organization)
             .WithMany(o => o.Departments)
             .HasForeignKey(d => d.OrganizationId)
             .OnDelete(DeleteBehavior.Restrict);

            // Самоссылающийся FK для иерархии
            e.HasOne(d => d.Parent)
             .WithMany(d => d.Children)
             .HasForeignKey(d => d.ParentId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
        });

        // Таблица истории изменений подразделений
        modelBuilder.Entity<OrgDepartmentHistory>(e =>
        {
            e.ToTable("org_department_history");
            e.HasKey(h => h.Id);
            e.Property(h => h.ChangeType).HasConversion<int>();
            e.Property(h => h.OldValue).HasColumnType("text");
            e.Property(h => h.NewValue).HasColumnType("text");

            e.HasOne(h => h.Department)
             .WithMany()
             .HasForeignKey(h => h.DepartmentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(h => h.ChangedByUser)
             .WithMany()
             .HasForeignKey(h => h.ChangedByUserId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
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

        // Таблица должностей
        modelBuilder.Entity<OrgPosition>(e =>
        {
            e.ToTable("org_positions");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(300);
            e.Property(p => p.Code).HasMaxLength(50);
            e.Property(p => p.Description).HasMaxLength(2000);
            e.Property(p => p.Category).HasConversion<int>();
            e.Property(p => p.Status).HasConversion<int>();
            e.Property(p => p.PlannedHeadcount).HasColumnType("decimal(5,2)");

            // Фильтр мягкого удаления — по умолчанию скрываем удалённые записи
            e.HasQueryFilter(p => !p.IsDeleted);

            // Уникальный код в рамках организации (только там, где Code != null)
            e.HasIndex(p => new { p.OrganizationId, p.Code })
             .HasFilter("code IS NOT NULL AND is_deleted = false")
             .IsUnique();

            e.HasOne(p => p.Organization)
             .WithMany(o => o.Positions)
             .HasForeignKey(p => p.OrganizationId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Department)
             .WithMany()
             .HasForeignKey(p => p.DepartmentId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        // Таблица вложений должностей
        modelBuilder.Entity<OrgPositionAttachment>(e =>
        {
            e.ToTable("org_position_attachments");
            e.HasKey(a => a.Id);
            e.Property(a => a.FileName).IsRequired().HasMaxLength(500);
            e.Property(a => a.ContentType).IsRequired().HasMaxLength(200);
            e.Property(a => a.FilePath).IsRequired().HasMaxLength(1000);
            e.Property(a => a.Description).HasMaxLength(1000);

            e.HasOne(a => a.Position)
             .WithMany(p => p.Attachments)
             .HasForeignKey(a => a.PositionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Таблица матрицы ролей должностей
        modelBuilder.Entity<OrgPositionRoleMapping>(e =>
        {
            e.ToTable("org_position_role_mappings");
            e.HasKey(r => r.Id);
            e.Property(r => r.RoleName).IsRequired().HasMaxLength(100);

            // Уникальная пара должность–роль
            e.HasIndex(r => new { r.PositionId, r.RoleName }).IsUnique();

            e.HasOne(r => r.Position)
             .WithMany(p => p.RoleMappings)
             .HasForeignKey(r => r.PositionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Таблица назначений пользователей на должности
        modelBuilder.Entity<OrgPositionAssignment>(e =>
        {
            e.ToTable("org_position_assignments", t =>
            {
                // Ставка должна быть одним из допустимых значений: 0.25, 0.5, 0.75, 1.0
                t.HasCheckConstraint("ck_org_position_assignments_rate",
                    "rate IN (0.25, 0.50, 0.75, 1.00)");
                // Дата окончания должна быть не раньше даты начала
                t.HasCheckConstraint("ck_org_position_assignments_dates",
                    "end_date IS NULL OR end_date >= start_date");
            });

            e.HasKey(a => a.Id);

            e.Property(a => a.Rate)
             .HasColumnType("decimal(4,2)");

            // Индексы для производительности
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.OrganizationId);
            e.HasIndex(a => new { a.PositionId, a.EndDate });

            e.HasOne(a => a.User)
             .WithMany()
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Position)
             .WithMany(p => p.Assignments)
             .HasForeignKey(a => a.PositionId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Organization)
             .WithMany()
             .HasForeignKey(a => a.OrganizationId)
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
