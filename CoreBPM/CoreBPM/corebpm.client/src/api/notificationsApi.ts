// API-клиент для in-app уведомлений (FR-MSG-02.1) и настроек SMTP (FR-ADM-02.1)

export interface InboxEntryDto {
    id: string;
    type: string;
    title: string;
    body: string;
    link: string | null;
    isRead: boolean;
    createdAt: string;
    readAt: string | null;
}

export interface InboxListResponse {
    items: InboxEntryDto[];
    total: number;
    page: number;
    pageSize: number;
}

export interface SmtpSettingsDto {
    host: string;
    port: number;
    useSsl: boolean;
    username: string | null;
    password: string | null;
    fromAddress: string;
    fromName: string;
}

// ─── In-app уведомления ────────────────────────────────────────────────────────

export async function getNotifications(params?: {
    read?: boolean;
    type?: string;
    page?: number;
    pageSize?: number;
}): Promise<InboxListResponse> {
    const qs = new URLSearchParams();
    if (params?.read !== undefined) qs.set('read', String(params.read));
    if (params?.type) qs.set('type', params.type);
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    const res = await fetch(`/api/notifications?${qs}`, {
        headers: { 'Content-Type': 'application/json' },
    });
    if (!res.ok) throw new Error('Ошибка загрузки уведомлений');
    return res.json();
}

export async function getUnreadNotificationsCount(): Promise<number> {
    const res = await fetch('/api/notifications/unread-count');
    if (!res.ok) return 0;
    const data = await res.json();
    return data.count ?? 0;
}

export async function markNotificationRead(id: string): Promise<void> {
    await fetch(`/api/notifications/${id}/read`, { method: 'PUT' });
}

export async function markAllNotificationsRead(): Promise<void> {
    await fetch('/api/notifications/read-all', { method: 'PUT' });
}

export async function deleteNotification(id: string): Promise<void> {
    await fetch(`/api/notifications/${id}`, { method: 'DELETE' });
}

// ─── SMTP настройки ────────────────────────────────────────────────────────────

export async function getSmtpSettings(): Promise<SmtpSettingsDto> {
    const res = await fetch('/api/admin/settings/smtp');
    if (!res.ok) throw new Error('Ошибка загрузки настроек SMTP');
    return res.json();
}

export async function saveSmtpSettings(dto: SmtpSettingsDto): Promise<void> {
    const res = await fetch('/api/admin/settings/smtp', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(dto),
    });
    if (!res.ok) throw new Error('Ошибка сохранения настроек SMTP');
}

export async function testSmtpSettings(): Promise<boolean> {
    const res = await fetch('/api/admin/settings/smtp/test', { method: 'POST' });
    if (!res.ok) return false;
    const data = await res.json();
    return data.success === true;
}
