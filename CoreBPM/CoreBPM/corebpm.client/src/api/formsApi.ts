// API-клиент для управления формами задач (/api/forms)

export type FormVersionStatus = 'Draft' | 'Published' | 'Archived';

// ─── Форма ────────────────────────────────────────────────────────────────────

export interface FormListItemDto {
    id: string;
    name: string;
    description?: string;
    processId?: string;
    elementId?: string;
    totalVersions: number;
    latestVersionStatus?: FormVersionStatus;
    createdAt: string;
    updatedAt: string;
}

export interface FormDto {
    id: string;
    name: string;
    description?: string;
    processId?: string;
    elementId?: string;
    totalVersions: number;
    createdAt: string;
    updatedAt: string;
}

export interface CreateFormRequest {
    name: string;
    description?: string;
    processId?: string;
    elementId?: string;
}

export interface UpdateFormRequest {
    name: string;
    description?: string;
    processId?: string;
    elementId?: string;
}

// ─── Версия формы ─────────────────────────────────────────────────────────────

export interface FormVersionInfoDto {
    id: string;
    versionNumber: number;
    status: FormVersionStatus;
    createdAt: string;
    publishedAt?: string;
}

export interface FormVersionDto {
    id: string;
    formId: string;
    versionNumber: number;
    status: FormVersionStatus;
    createdAt: string;
    publishedAt?: string;
    schema: FormSchema;
}

export interface SaveFormVersionRequest {
    schema: FormSchema;
}

// ─── Схема формы ──────────────────────────────────────────────────────────────

/** Полная схема формы — массив секций. */
export interface FormSchema {
    sections: FormSection[];
}

/** Секция формы с заголовком и набором строк. */
export interface FormSection {
    id: string;
    title?: string;
    /** Количество колонок секции: 1, 2 или 3. */
    columns: 1 | 2 | 3;
    rows: FormRow[];
}

/** Строка внутри секции — массив полей. */
export interface FormRow {
    id: string;
    fields: FormField[];
}

/** Определение одного поля формы. */
export interface FormField {
    id: string;
    type: FormFieldType;
    label: string;
    placeholder?: string;
    required?: boolean;
    readOnly?: boolean;
    /** Привязка к переменной процесса (имя переменной). */
    variableName?: string;
    /** EL-выражение условной видимости. */
    visibilityExpression?: string;
    /** Маска ввода (например: +7 (###) ###-##-##). */
    inputMask?: string;
    /** Regex-правило валидации. */
    validationRegex?: string;
    /** Сообщение при ошибке валидации. */
    validationMessage?: string;
    /** Статичные варианты для Select (ключ/значение). */
    options?: { value: string; label: string }[];
    /** Дополнительные настройки специфичные для типа поля. */
    extra?: Record<string, unknown>;
}

/** Тип поля формы. */
export type FormFieldType =
    // Ввод
    | 'text'
    | 'textarea'
    | 'number'
    | 'datetime'
    | 'checkbox'
    | 'radio'
    | 'select'
    | 'user-picker'
    | 'file-upload'
    // Отображение
    | 'label'
    | 'section-header'
    | 'divider'
    | 'html-block'
    // Специальные
    | 'approval';

// ─── Пустая схема по умолчанию ────────────────────────────────────────────────

export const EMPTY_SCHEMA: FormSchema = { sections: [] };

// ─── Fetch helper ─────────────────────────────────────────────────────────────

async function fetchJson<T>(url: string, token: string, init?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        ...init,
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
            ...init?.headers,
        },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        let message = text || `HTTP ${res.status}`;
        try {
            const body = JSON.parse(text);
            if (body?.error) message = body.error;
        } catch { /* тело не является JSON */ }
        throw new Error(message);
    }
    if (res.status === 204) return undefined as unknown as T;
    return res.json() as Promise<T>;
}

// ─── Формы ────────────────────────────────────────────────────────────────────

/** Список форм (опционально фильтр по processId). */
export const getForms = (token: string, processId?: string): Promise<FormListItemDto[]> => {
    const url = processId ? `/api/forms?processId=${processId}` : '/api/forms';
    return fetchJson(url, token);
};

/** Получить форму по ID. */
export const getForm = (token: string, formId: string): Promise<FormDto> =>
    fetchJson(`/api/forms/${formId}`, token);

/** Создать форму. */
export const createForm = (token: string, data: CreateFormRequest): Promise<FormDto> =>
    fetchJson('/api/forms', token, { method: 'POST', body: JSON.stringify(data) });

/** Обновить метаданные формы. */
export const updateForm = (token: string, formId: string, data: UpdateFormRequest): Promise<FormDto> =>
    fetchJson(`/api/forms/${formId}`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Удалить форму. */
export const deleteForm = (token: string, formId: string): Promise<void> =>
    fetchJson(`/api/forms/${formId}`, token, { method: 'DELETE' });

// ─── Версии ───────────────────────────────────────────────────────────────────

/** Список версий формы. */
export const getFormVersions = (token: string, formId: string): Promise<FormVersionInfoDto[]> =>
    fetchJson(`/api/forms/${formId}/versions`, token);

/** Конкретная версия со схемой. */
export const getFormVersion = (token: string, formId: string, versionId: string): Promise<FormVersionDto> =>
    fetchJson(`/api/forms/${formId}/versions/${versionId}`, token);

/** Сохранить новый черновик. */
export const saveFormDraft = (token: string, formId: string, data: SaveFormVersionRequest): Promise<FormVersionDto> =>
    fetchJson(`/api/forms/${formId}/versions`, token, { method: 'POST', body: JSON.stringify(data) });

/** Опубликовать версию. */
export const publishFormVersion = (token: string, formId: string, versionId: string): Promise<FormVersionInfoDto> =>
    fetchJson(`/api/forms/${formId}/versions/${versionId}/publish`, token, { method: 'POST' });

/** Откатить к версии (создаёт копию). */
export const rollbackFormVersion = (token: string, formId: string, versionId: string): Promise<FormVersionDto> =>
    fetchJson(`/api/forms/${formId}/versions/${versionId}/rollback`, token, { method: 'POST' });
