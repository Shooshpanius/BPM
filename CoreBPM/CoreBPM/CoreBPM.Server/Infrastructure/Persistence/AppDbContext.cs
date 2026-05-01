using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Domain.Auth;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Domain.Rules;

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
    public DbSet<BpmProcess> BpmProcesses => Set<BpmProcess>();
    public DbSet<BpmProcessVersion> BpmProcessVersions => Set<BpmProcessVersion>();
    public DbSet<BpmElementConfig> BpmElementConfigs => Set<BpmElementConfig>();
    public DbSet<BpmProcessVariable> BpmProcessVariables => Set<BpmProcessVariable>();
    public DbSet<BpmRaciEntry> BpmRaciEntries => Set<BpmRaciEntry>();
    public DbSet<BpmProcessRoleConfig> BpmProcessRoleConfigs => Set<BpmProcessRoleConfig>();
    public DbSet<BpmInstance> BpmInstances => Set<BpmInstance>();
    public DbSet<BpmInstanceVariable> BpmInstanceVariables => Set<BpmInstanceVariable>();
    public DbSet<BpmSchedulerJob> BpmSchedulerJobs => Set<BpmSchedulerJob>();
    public DbSet<BpmInstanceHistoryEntry> BpmInstanceHistoryEntries => Set<BpmInstanceHistoryEntry>();
    public DbSet<BpmInstanceParticipant> BpmInstanceParticipants => Set<BpmInstanceParticipant>();
    public DbSet<BpmSavedFilter> BpmSavedFilters => Set<BpmSavedFilter>();
    public DbSet<BpmExecutionJob> BpmExecutionJobs => Set<BpmExecutionJob>();
    public DbSet<BpmProcessDocSnapshot> BpmProcessDocSnapshots => Set<BpmProcessDocSnapshot>();
    public DbSet<BpmToken> BpmTokens => Set<BpmToken>();
    public DbSet<BpmJoinCounter> BpmJoinCounters => Set<BpmJoinCounter>();
    public DbSet<BpmVersionMigrationPackage> BpmVersionMigrationPackages => Set<BpmVersionMigrationPackage>();
    public DbSet<BpmVersionMigrationItem> BpmVersionMigrationItems => Set<BpmVersionMigrationItem>();
    public DbSet<BpmTaskForm> BpmTaskForms => Set<BpmTaskForm>();
    public DbSet<BpmTaskFormVersion> BpmTaskFormVersions => Set<BpmTaskFormVersion>();
    public DbSet<BpmInstanceStatusConfig> BpmInstanceStatusConfigs => Set<BpmInstanceStatusConfig>();
    public DbSet<BpmInstanceStatusOption> BpmInstanceStatusOptions => Set<BpmInstanceStatusOption>();
    public DbSet<BpmDiagramLock> BpmDiagramLocks => Set<BpmDiagramLock>();
    public DbSet<BpmScriptModule> BpmScriptModules => Set<BpmScriptModule>();
    public DbSet<BpmDesignerExtension> BpmDesignerExtensions => Set<BpmDesignerExtension>();
    public DbSet<BpmGlobalModule> BpmGlobalModules => Set<BpmGlobalModule>();
    public DbSet<BpmGlobalModuleFile> BpmGlobalModuleFiles => Set<BpmGlobalModuleFile>();
    public DbSet<BpmImprovement> BpmImprovements => Set<BpmImprovement>();
    public DbSet<BpmSignal> BpmSignals => Set<BpmSignal>();
    public DbSet<BpmMessage> BpmMessages => Set<BpmMessage>();
    public DbSet<DmnTable> DmnTables => Set<DmnTable>();
    public DbSet<DmnTableVersion> DmnTableVersions => Set<DmnTableVersion>();
    public DbSet<DmnColumn> DmnColumns => Set<DmnColumn>();
    public DbSet<DmnRow> DmnRows => Set<DmnRow>();
    public DbSet<DmnCell> DmnCells => Set<DmnCell>();
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
        // Должность и подразделение определяются через OrgPositionAssignment, а не через поля PositionId / DepartmentId
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

            e.HasOne(a => a.Department)
             .WithMany()
             .HasForeignKey(a => a.DepartmentId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        // Seed системных ролей
        SeedSystemRoles(modelBuilder);

        // Таблица определений бизнес-процессов
        modelBuilder.Entity<BpmProcess>(e =>
        {
            e.ToTable("bpm_processes");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(300);
            e.Property(p => p.Description).HasMaxLength(2000);
            e.Property(p => p.ExternalStartMethodsJson).IsRequired().HasColumnType("jsonb");
            e.Property(p => p.ExternalStartAllowedIps).HasMaxLength(1000);
            e.Property(p => p.ExternalStartTokenHash).HasMaxLength(128);
            e.Property(p => p.ExternalStartTokenPreview).HasMaxLength(32);
            e.Property(p => p.InstanceNameMode).HasConversion<int>();
            e.Property(p => p.InstanceNameTemplate).HasMaxLength(1000);
            e.Property(p => p.DataClassName).IsRequired().HasMaxLength(200);
            e.Property(p => p.DataTableName).IsRequired().HasMaxLength(200);
            e.Property(p => p.ProcessMetricsClassName).IsRequired().HasMaxLength(200);
            e.Property(p => p.ProcessMetricsTableName).IsRequired().HasMaxLength(200);
            e.Property(p => p.InstanceMetricsClassName).IsRequired().HasMaxLength(200);
            e.Property(p => p.InstanceMetricsTableName).IsRequired().HasMaxLength(200);

            e.HasQueryFilter(p => !p.IsDeleted);

            e.HasIndex(p => p.OrganizationId);
            e.HasIndex(p => p.CreatedByUserId);
        });

        // Таблица версий диаграмм бизнес-процессов
        modelBuilder.Entity<BpmProcessVersion>(e =>
        {
            e.ToTable("bpm_process_versions");
            e.HasKey(v => v.Id);
            e.Property(v => v.Status).HasConversion<int>();
            e.Property(v => v.DiagramXml).HasColumnType("text");

            e.HasIndex(v => v.ProcessId);
            e.HasIndex(v => new { v.ProcessId, v.VersionNumber }).IsUnique();

            e.HasOne(v => v.Process)
             .WithMany(p => p.Versions)
             .HasForeignKey(v => v.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Таблица мягких блокировок диаграмм
        modelBuilder.Entity<BpmDiagramLock>(e =>
        {
            e.ToTable("bpm_diagram_locks");
            e.HasKey(l => l.Id);
            e.Property(l => l.LockedByDisplayName).IsRequired().HasMaxLength(300);

            // Один замок на процесс
            e.HasIndex(l => l.ProcessId).IsUnique();
            e.HasIndex(l => l.LockedUntil);

            e.HasOne(l => l.Process)
             .WithMany()
             .HasForeignKey(l => l.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Таблица кастомных конфигураций элементов BPMN
        modelBuilder.Entity<BpmElementConfig>(e =>
        {
            e.ToTable("bpm_element_configs");
            e.HasKey(c => c.Id);
            e.Property(c => c.ElementId).IsRequired().HasMaxLength(200);
            e.Property(c => c.ConfigJson).IsRequired().HasColumnType("jsonb");

            // Уникальная пара процесс–elementId
            e.HasIndex(c => new { c.ProcessId, c.ElementId }).IsUnique();

            e.HasOne(c => c.Process)
             .WithMany()
             .HasForeignKey(c => c.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Таблица переменных контекста процесса
        modelBuilder.Entity<BpmProcessVariable>(e =>
        {
            e.ToTable("bpm_process_variables");
            e.HasKey(v => v.Id);
            e.Property(v => v.Name).IsRequired().HasMaxLength(200);
            e.Property(v => v.VariableType).HasConversion<int>();
            e.Property(v => v.DefaultValue).HasMaxLength(2000);

            // Имя переменной уникально в рамках процесса
            e.HasIndex(v => new { v.ProcessId, v.Name }).IsUnique();

            e.HasOne(v => v.Process)
             .WithMany()
             .HasForeignKey(v => v.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Таблица RACI-матрицы
        modelBuilder.Entity<BpmRaciEntry>(e =>
        {
            e.ToTable("bpm_raci_entries");
            e.HasKey(r => r.Id);
            e.Property(r => r.Stage).IsRequired().HasMaxLength(300);
            e.Property(r => r.Role).IsRequired().HasMaxLength(300);
            e.Property(r => r.RaciType).HasConversion<int>();

            e.HasIndex(r => r.ProcessId);

            e.HasOne(r => r.Process)
             .WithMany()
             .HasForeignKey(r => r.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Роли процесса (Владелец/Куратор) ────────────────────────────────────

        modelBuilder.Entity<BpmProcessRoleConfig>(e =>
        {
            e.ToTable("bpm_process_role_configs");
            e.HasKey(r => r.Id);
            e.Property(r => r.RoleType).HasConversion<int>();
            e.Property(r => r.AssigneeType).HasConversion<int>();
            e.Property(r => r.AssigneeId).IsRequired().HasMaxLength(100);
            e.Property(r => r.DisplayName).IsRequired().HasMaxLength(500);

            e.HasIndex(r => r.ProcessId);

            e.HasOne(r => r.Process)
             .WithMany()
             .HasForeignKey(r => r.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Экземпляры процессов ─────────────────────────────────────────────────

        modelBuilder.Entity<BpmInstance>(e =>
        {
            e.ToTable("bpm_instances");
            e.HasKey(i => i.Id);
            e.Property(i => i.Name).IsRequired().HasMaxLength(500);
            e.Property(i => i.State).HasConversion<int>();
            e.Property(i => i.LaunchSource).HasConversion<int>();
            e.Property(i => i.ExternalReference).HasMaxLength(300);
            e.Property(i => i.CancelReason).HasMaxLength(2000);

            e.HasIndex(i => i.ProcessId);
            e.HasIndex(i => i.State);
            e.HasIndex(i => i.InitiatorUserId);
            e.HasIndex(i => new { i.ProcessId, i.StartedAt });

            e.HasOne(i => i.Process)
             .WithMany()
             .HasForeignKey(i => i.ProcessId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(i => i.ProcessVersion)
             .WithMany()
             .HasForeignKey(i => i.ProcessVersionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BpmInstanceVariable>(e =>
        {
            e.ToTable("bpm_instance_variables");
            e.HasKey(v => v.Id);
            e.Property(v => v.Name).IsRequired().HasMaxLength(200);

            e.HasIndex(v => v.InstanceId);

            e.HasOne(v => v.Instance)
             .WithMany(i => i.Variables)
             .HasForeignKey(v => v.InstanceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BpmSchedulerJob>(e =>
        {
            e.ToTable("bpm_scheduler_jobs");
            e.HasKey(j => j.Id);
            e.Property(j => j.ElementId).IsRequired().HasMaxLength(200);
            e.Property(j => j.TimerType).IsRequired().HasMaxLength(50);
            e.Property(j => j.TimerValue).IsRequired().HasMaxLength(500);
            e.Property(j => j.TimeZone).HasMaxLength(100);
            e.Property(j => j.Status).HasConversion<int>();
            e.Property(j => j.LastError).HasMaxLength(4000);

            e.HasIndex(j => j.ProcessId);
            e.HasIndex(j => new { j.IsActive, j.NextFireAt });

            e.HasOne(j => j.Process)
             .WithMany()
             .HasForeignKey(j => j.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(j => j.ProcessVersion)
             .WithMany()
             .HasForeignKey(j => j.ProcessVersionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Очередь исполнения ──────────────────────────────────────────────────

        modelBuilder.Entity<BpmExecutionJob>(e =>
        {
            e.ToTable("bpm_execution_jobs");
            e.HasKey(j => j.Id);
            e.Property(j => j.ElementId).IsRequired().HasMaxLength(200);
            e.Property(j => j.ElementType).IsRequired().HasMaxLength(100);
            e.Property(j => j.OperationName).HasMaxLength(500);
            e.Property(j => j.Status).HasConversion<int>();
            e.Property(j => j.LastError).HasMaxLength(4000);
            e.Property(j => j.ServerHost).HasMaxLength(200);

            e.HasIndex(j => j.Status);
            e.HasIndex(j => j.ProcessId);
            e.HasIndex(j => j.InstanceId);
            e.HasIndex(j => new { j.Status, j.NextRunAt });

            e.HasOne(j => j.Process)
             .WithMany()
             .HasForeignKey(j => j.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(j => j.ProcessVersion)
             .WithMany()
             .HasForeignKey(j => j.ProcessVersionId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(j => j.Instance)
             .WithMany()
             .HasForeignKey(j => j.InstanceId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        // ─── Журнал истории и участники экземпляра ───────────────────────────────

        modelBuilder.Entity<BpmInstanceHistoryEntry>(e =>
        {
            e.ToTable("bpm_instance_history");
            e.HasKey(h => h.Id);
            e.Property(h => h.EventType).HasConversion<int>();
            e.Property(h => h.Text).HasMaxLength(4000);
            e.Property(h => h.MetaJson).HasColumnType("text");
            e.Property(h => h.ElementId).HasMaxLength(200);
            e.Property(h => h.ElementName).HasMaxLength(500);

            e.HasIndex(h => h.InstanceId);
            e.HasIndex(h => new { h.InstanceId, h.OccurredAt });
            e.HasIndex(h => new { h.ElementId, h.OccurredAt });

            e.HasOne(h => h.Instance)
             .WithMany()
             .HasForeignKey(h => h.InstanceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BpmInstanceParticipant>(e =>
        {
            e.ToTable("bpm_instance_participants");
            e.HasKey(p => p.Id);
            e.Property(p => p.DisplayName).HasMaxLength(300);
            e.HasIndex(p => new { p.InstanceId, p.UserId }).IsUnique();

            e.HasOne(p => p.Instance)
             .WithMany()
             .HasForeignKey(p => p.InstanceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Сохранённые фильтры «Мои процессы» ──────────────────────────────────

        modelBuilder.Entity<BpmSavedFilter>(e =>
        {
            e.ToTable("bpm_saved_filters");
            e.HasKey(f => f.Id);
            e.Property(f => f.Name).IsRequired().HasMaxLength(200);
            e.Property(f => f.FiltersJson).HasColumnType("text");
            e.HasIndex(f => f.UserId);
        });

        // ─── DMN: таблицы бизнес-правил ─────────────────────────────────────────

        // ─── Формы задач ─────────────────────────────────────────────────────────

        modelBuilder.Entity<BpmTaskForm>(e =>
        {
            e.ToTable("bpm_task_forms");
            e.HasKey(f => f.Id);
            e.Property(f => f.Name).IsRequired().HasMaxLength(300);
            e.Property(f => f.Description).HasMaxLength(2000);
            e.Property(f => f.ElementId).HasMaxLength(200);

            e.HasIndex(f => f.ProcessId);

            e.HasOne(f => f.Process)
             .WithMany()
             .HasForeignKey(f => f.ProcessId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        modelBuilder.Entity<BpmTaskFormVersion>(e =>
        {
            e.ToTable("bpm_task_form_versions");
            e.HasKey(v => v.Id);
            e.Property(v => v.Schema).IsRequired().HasColumnType("jsonb");
            e.Property(v => v.Status).HasConversion<int>();

            e.HasIndex(v => new { v.FormId, v.VersionNumber }).IsUnique();

            e.HasOne(v => v.Form)
             .WithMany(f => f.Versions)
             .HasForeignKey(v => v.FormId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Пользовательские статусы экземпляров процесса ──────────────────────

        modelBuilder.Entity<BpmInstanceStatusConfig>(e =>
        {
            e.ToTable("bpm_instance_status_configs");
            e.HasKey(c => c.Id);
            e.Property(c => c.OnInterruptAction).HasConversion<int>();
            e.Property(c => c.OnInterruptScriptId).HasMaxLength(500);

            // Один конфиг на процесс
            e.HasIndex(c => c.ProcessId).IsUnique();

            e.HasOne(c => c.Process)
             .WithMany()
             .HasForeignKey(c => c.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BpmInstanceStatusOption>(e =>
        {
            e.ToTable("bpm_instance_status_options");
            e.HasKey(o => o.Id);
            e.Property(o => o.Name).IsRequired().HasMaxLength(300);
            e.Property(o => o.Code).IsRequired().HasMaxLength(200);

            // Код уникален в рамках процесса
            e.HasIndex(o => new { o.ProcessId, o.Code }).IsUnique();

            e.HasOne(o => o.Process)
             .WithMany()
             .HasForeignKey(o => o.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── DMN: таблицы бизнес-правил ─────────────────────────────────────────

        modelBuilder.Entity<DmnTable>(e =>
        {
            e.ToTable("rules_dmn_tables");
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(300);
            e.Property(t => t.Description).HasMaxLength(2000);
            e.Property(t => t.HitPolicy).HasConversion<int>();
        });

        modelBuilder.Entity<DmnTableVersion>(e =>
        {
            e.ToTable("rules_dmn_table_versions");
            e.HasKey(v => v.Id);
            e.Property(v => v.Status).HasConversion<int>();

            e.HasIndex(v => new { v.TableId, v.VersionNumber }).IsUnique();

            e.HasOne(v => v.Table)
             .WithMany(t => t.Versions)
             .HasForeignKey(v => v.TableId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DmnColumn>(e =>
        {
            e.ToTable("rules_dmn_columns");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(300);
            e.Property(c => c.ColumnKind).HasConversion<int>();
            e.Property(c => c.ValueType).HasConversion<int>();

            e.HasIndex(c => new { c.VersionId, c.Order });

            e.HasOne(c => c.Version)
             .WithMany(v => v.Columns)
             .HasForeignKey(c => c.VersionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DmnRow>(e =>
        {
            e.ToTable("rules_dmn_rows");
            e.HasKey(r => r.Id);

            e.HasIndex(r => new { r.VersionId, r.Order });

            e.HasOne(r => r.Version)
             .WithMany(v => v.Rows)
             .HasForeignKey(r => r.VersionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DmnCell>(e =>
        {
            e.ToTable("rules_dmn_cells");
            e.HasKey(c => c.Id);
            e.Property(c => c.Value).HasMaxLength(2000);
            e.Property(c => c.Annotation).HasMaxLength(1000);

            // Уникальная пара ячейка: строка × колонка
            e.HasIndex(c => new { c.RowId, c.ColumnId }).IsUnique();

            e.HasOne(c => c.Row)
             .WithMany(r => r.Cells)
             .HasForeignKey(c => c.RowId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.Column)
             .WithMany(col => col.Cells)
             .HasForeignKey(c => c.ColumnId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Сценарии процессов ──────────────────────────────────────────────────

        modelBuilder.Entity<BpmScriptModule>(e =>
        {
            e.ToTable("bpm_script_modules");
            e.HasKey(s => s.Id);
            e.Property(s => s.ScriptBody).HasColumnType("text");
            e.Property(s => s.Language).IsRequired().HasMaxLength(50);

            // Один модуль на версию процесса
            e.HasIndex(s => s.ProcessVersionId).IsUnique();

            e.HasOne(s => s.ProcessVersion)
             .WithOne(v => v.ScriptModule)
             .HasForeignKey<BpmScriptModule>(s => s.ProcessVersionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Пользовательские расширения дизайнера ───────────────────────────────

        modelBuilder.Entity<BpmDesignerExtension>(e =>
        {
            e.ToTable("bpm_designer_extensions");
            e.HasKey(ex => ex.Id);
            e.Property(ex => ex.Name).IsRequired().HasMaxLength(300);
            e.Property(ex => ex.Description).HasMaxLength(2000);
            e.Property(ex => ex.FolderPath).HasMaxLength(500);
            e.Property(ex => ex.ScriptBody).HasColumnType("text");

            e.HasQueryFilter(ex => !ex.IsDeleted);

            e.HasIndex(ex => ex.OrganizationId);
        });

        // ─── Глобальные модули ───────────────────────────────────────────────────

        modelBuilder.Entity<BpmGlobalModule>(e =>
        {
            e.ToTable("bpm_global_modules");
            e.HasKey(m => m.Id);
            e.Property(m => m.Name).IsRequired().HasMaxLength(300);
            e.Property(m => m.Description).HasMaxLength(2000);

            e.HasQueryFilter(m => !m.IsDeleted);

            e.HasIndex(m => m.OrganizationId);
        });

        modelBuilder.Entity<BpmGlobalModuleFile>(e =>
        {
            e.ToTable("bpm_global_module_files");
            e.HasKey(f => f.Id);
            e.Property(f => f.FileName).IsRequired().HasMaxLength(300);
            e.Property(f => f.ScriptBody).HasColumnType("text");

            e.HasIndex(f => new { f.ModuleId, f.Order });

            e.HasOne(f => f.Module)
             .WithMany(m => m.Files)
             .HasForeignKey(f => f.ModuleId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Реестр сигналов BPMN ───────────────────────────────────────────────

        modelBuilder.Entity<BpmSignal>(e =>
        {
            e.ToTable("bpm_signals");
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).IsRequired().HasMaxLength(200);
            e.Property(s => s.Code).IsRequired().HasMaxLength(100);
            e.Property(s => s.Description).HasMaxLength(1000);

            // Код уникален в рамках организации
            e.HasIndex(s => new { s.OrganizationId, s.Code }).IsUnique();
            e.HasIndex(s => s.OrganizationId);
        });

        // ─── Реестр сообщений BPMN ──────────────────────────────────────────────

        modelBuilder.Entity<BpmMessage>(e =>
        {
            e.ToTable("bpm_messages");
            e.HasKey(m => m.Id);
            e.Property(m => m.Name).IsRequired().HasMaxLength(200);
            e.Property(m => m.Code).IsRequired().HasMaxLength(100);
            e.Property(m => m.Description).HasMaxLength(1000);

            // Код уникален в рамках организации
            e.HasIndex(m => new { m.OrganizationId, m.Code }).IsUnique();
            e.HasIndex(m => m.OrganizationId);
        });

        modelBuilder.Entity<BpmVersionMigrationPackage>(e =>
        {
            e.ToTable("bpm_version_migration_packages");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(300);
            e.Property(p => p.Status).HasConversion<int>();
            e.HasIndex(p => p.Status);
            e.HasIndex(p => p.CreatedByUserId);
        });

        modelBuilder.Entity<BpmVersionMigrationItem>(e =>
        {
            e.ToTable("bpm_version_migration_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.Status).HasConversion<int>();
            e.Property(i => i.ErrorComment).HasMaxLength(2000);
            e.Property(i => i.ManualChangeUrl).HasMaxLength(1000);
            e.HasIndex(i => i.PackageId);
            e.HasIndex(i => i.InstanceId);
            e.HasIndex(i => i.Status);

            e.HasOne(i => i.Package)
             .WithMany(p => p.Items)
             .HasForeignKey(i => i.PackageId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(i => i.Instance)
             .WithMany()
             .HasForeignKey(i => i.InstanceId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(i => i.Process)
             .WithMany()
             .HasForeignKey(i => i.ProcessId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(i => i.TargetVersion)
             .WithMany()
             .HasForeignKey(i => i.TargetVersionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Токены выполнения BPMN ──────────────────────────────────────────────

        modelBuilder.Entity<BpmToken>(e =>
        {
            e.ToTable("bpm_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.ElementId).IsRequired().HasMaxLength(200);
            e.Property(t => t.ElementType).IsRequired().HasMaxLength(100);
            e.Property(t => t.ElementName).HasMaxLength(500);
            e.Property(t => t.Status).HasConversion<int>();
            e.Property(t => t.SignalCode).HasMaxLength(200);
            e.Property(t => t.MessageCode).HasMaxLength(200);
            e.Property(t => t.CorrelationKey).HasMaxLength(500);

            e.HasIndex(t => t.InstanceId);
            e.HasIndex(t => new { t.Status, t.SignalCode });
            e.HasIndex(t => new { t.Status, t.MessageCode });

            e.HasOne(t => t.Instance)
             .WithMany()
             .HasForeignKey(t => t.InstanceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BpmJoinCounter>(e =>
        {
            e.ToTable("bpm_join_counters");
            e.HasKey(c => c.Id);
            e.Property(c => c.GatewayElementId).IsRequired().HasMaxLength(200);

            e.HasIndex(c => new { c.InstanceId, c.GatewayElementId }).IsUnique();

            e.HasOne(c => c.Instance)
             .WithMany()
             .HasForeignKey(c => c.InstanceId)
             .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<BpmImprovement>(e =>
        {
            e.ToTable("bpm_improvements");
            e.HasKey(i => i.Id);
            e.Property(i => i.Subject).IsRequired().HasMaxLength(500);
            e.Property(i => i.SourceTaskElementId).HasMaxLength(200);

            e.HasIndex(i => i.ProcessId);
            e.HasIndex(i => i.InitiatorUserId);
            e.HasIndex(i => i.Status);

            e.HasOne(i => i.Process)
             .WithMany()
             .HasForeignKey(i => i.ProcessId)
             .OnDelete(DeleteBehavior.Cascade);
        });
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
