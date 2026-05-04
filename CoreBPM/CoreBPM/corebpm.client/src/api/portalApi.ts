// API-клиент портала (FR-PORTAL-01)
const BASE = '/api/portal';

export interface PortalDashboardWidgetDto {
    id: string;
    widgetType: string;
    col: number;
    row: number;
    colSpan: number;
    rowSpan: number;
    title?: string;
    configJson?: string;
    isCollapsed: boolean;
    sortOrder: number;
}

export interface SaveDashboardRequest {
    widgets: SaveWidgetRequest[];
}

export interface SaveWidgetRequest {
    id?: string;
    widgetType: string;
    col: number;
    row: number;
    colSpan: number;
    rowSpan: number;
    title?: string;
    configJson?: string;
    isCollapsed: boolean;
    sortOrder: number;
}

export interface AddWidgetRequest {
    widgetType: string;
    col: number;
    row: number;
    colSpan?: number;
    rowSpan?: number;
    title?: string;
    configJson?: string;
}

export interface UpdateWidgetRequest {
    col?: number;
    row?: number;
    colSpan?: number;
    rowSpan?: number;
    title?: string;
    configJson?: string;
    isCollapsed?: boolean;
}

export interface PortalBrandingDto {
    id: string;
    systemName: string;
    logoUrl?: string;
    faviconUrl?: string;
    primaryColor?: string;
    accentColor?: string;
    globalTheme: string;
}

export interface UpdateBrandingRequest {
    systemName?: string;
    logoUrl?: string;
    faviconUrl?: string;
    primaryColor?: string;
    accentColor?: string;
    globalTheme?: string;
}

export async function getDashboard(token: string): Promise<PortalDashboardWidgetDto[]> {
    const r = await fetch(`${BASE}/dashboard`, { headers: { Authorization: `Bearer ${token}` } });
    if (!r.ok) throw new Error('Ошибка загрузки дашборда');
    return r.json();
}

export async function saveDashboard(token: string, req: SaveDashboardRequest): Promise<PortalDashboardWidgetDto[]> {
    const r = await fetch(`${BASE}/dashboard`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
    });
    if (!r.ok) throw new Error('Ошибка сохранения дашборда');
    return r.json();
}

export async function addWidget(token: string, req: AddWidgetRequest): Promise<PortalDashboardWidgetDto> {
    const r = await fetch(`${BASE}/dashboard/widgets`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
    });
    if (!r.ok) throw new Error('Ошибка добавления виджета');
    return r.json();
}

export async function updateWidget(token: string, widgetId: string, req: UpdateWidgetRequest): Promise<PortalDashboardWidgetDto> {
    const r = await fetch(`${BASE}/dashboard/widgets/${widgetId}`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
    });
    if (!r.ok) throw new Error('Ошибка обновления виджета');
    return r.json();
}

export async function deleteWidget(token: string, widgetId: string): Promise<void> {
    const r = await fetch(`${BASE}/dashboard/widgets/${widgetId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) throw new Error('Ошибка удаления виджета');
}

export async function resetDashboard(token: string): Promise<void> {
    await fetch(`${BASE}/dashboard/reset`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
    });
}

export async function getBranding(token: string): Promise<PortalBrandingDto> {
    const r = await fetch(`${BASE}/branding`, { headers: { Authorization: `Bearer ${token}` } });
    if (!r.ok) throw new Error('Ошибка загрузки брендинга');
    return r.json();
}

export async function updateBranding(token: string, req: UpdateBrandingRequest): Promise<PortalBrandingDto> {
    const r = await fetch(`${BASE}/branding`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
    });
    if (!r.ok) throw new Error('Ошибка сохранения брендинга');
    return r.json();
}
