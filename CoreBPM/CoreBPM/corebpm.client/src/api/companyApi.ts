// API-клиент страницы компании (FR-ORG-03)

export interface CompanyInfoDto {
    id: string;
    name: string;
    description?: string;
    phone?: string;
    email?: string;
    address?: string;
    website?: string;
    logoUrl?: string;
}

export interface UpdateCompanyInfoRequest {
    name?: string;
    description?: string;
    phone?: string;
    email?: string;
    address?: string;
    website?: string;
}

export interface CompanyNewsDto {
    id: string;
    title: string;
    content: string;
    authorId: string;
    isPublished: boolean;
    createdAt: string;
    updatedAt: string;
}

export interface CreateNewsRequest {
    title: string;
    content: string;
    isPublished: boolean;
}

export interface UpdateNewsRequest {
    title?: string;
    content?: string;
    isPublished?: boolean;
}

export interface CompanyLinkDto {
    id: string;
    title: string;
    url: string;
    sortOrder: number;
}

async function fetchJson<T>(url: string, token: string, options?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        ...options,
        headers: {
            Authorization: `Bearer ${token}`,
            'Content-Type': 'application/json',
            ...(options?.headers ?? {}),
        },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        let message = text || `HTTP ${res.status}`;
        try {
            const body = JSON.parse(text);
            if (body?.error) message = body.error;
        } catch { /* текст не JSON */ }
        throw new Error(message);
    }
    if (res.status === 204) return undefined as unknown as T;
    return res.json() as Promise<T>;
}

export const getCompanyInfo = (token: string): Promise<CompanyInfoDto> =>
    fetchJson('/api/company', token);

export const updateCompanyInfo = (token: string, req: UpdateCompanyInfoRequest): Promise<CompanyInfoDto> =>
    fetchJson('/api/company', token, { method: 'PUT', body: JSON.stringify(req) });

export const getCompanyNews = (token: string): Promise<CompanyNewsDto[]> =>
    fetchJson('/api/company/news', token);

export const createCompanyNews = (token: string, req: CreateNewsRequest): Promise<CompanyNewsDto> =>
    fetchJson('/api/company/news', token, { method: 'POST', body: JSON.stringify(req) });

export const updateCompanyNews = (token: string, id: string, req: UpdateNewsRequest): Promise<CompanyNewsDto> =>
    fetchJson(`/api/company/news/${id}`, token, { method: 'PUT', body: JSON.stringify(req) });

export const deleteCompanyNews = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/company/news/${id}`, token, { method: 'DELETE' });

export const getCompanyLinks = (token: string): Promise<CompanyLinkDto[]> =>
    fetchJson('/api/company/links', token);

export const updateCompanyLinks = (token: string, links: CompanyLinkDto[]): Promise<CompanyLinkDto[]> =>
    fetchJson('/api/company/links', token, { method: 'PUT', body: JSON.stringify({ links }) });
