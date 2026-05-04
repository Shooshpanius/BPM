namespace CoreBPM.Server.Domain.Org;

/// <summary>Сведения о компании — синглтон (таблица org_company_info).</summary>
public class OrgCompanyInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
