namespace CoreBPM.Server.Domain.Org;

/// <summary>Внутренняя ссылка компании (таблица org_company_links).</summary>
public class OrgCompanyLink
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
