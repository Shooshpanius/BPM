namespace CoreBPM.Server.Application.Org.DTOs;

/// <summary>Сведения о компании.</summary>
public class CompanyInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
}

/// <summary>Запрос обновления сведений о компании.</summary>
public class UpdateCompanyInfoRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
}

/// <summary>Новость компании.</summary>
public class CompanyNewsDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Запрос создания новости.</summary>
public class CreateNewsRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
}

/// <summary>Запрос обновления новости.</summary>
public class UpdateNewsRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public bool? IsPublished { get; set; }
}

/// <summary>Внутренняя ссылка компании.</summary>
public class CompanyLinkDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Запрос обновления списка ссылок компании (полная замена).</summary>
public class UpdateCompanyLinksRequest
{
    public List<CompanyLinkDto> Links { get; set; } = new();
}
