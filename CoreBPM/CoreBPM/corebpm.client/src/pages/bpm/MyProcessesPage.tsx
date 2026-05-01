import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getMyInstances,
    getSavedFilters,
    createSavedFilter,
    deleteSavedFilter,
    exportMyInstances,
    type MyInstancesFilter,
    type MyInstancesRole,
    type BpmInstanceListItemDto,
    type BpmSavedFilterDto,
    type BpmInstanceState,
} from '../../api/bpmApi';
import './MyProcessesPage.css';
interface MyProcessesPageProps {
    onOpenInstance: (instanceId: string) => void;
}

type TabId = 'processes' | 'tasks';

const ROLE_LABELS: Record<MyInstancesRole, string> = {
    All: 'Все',
    Initiator: 'Созданы мной',
    Responsible: 'Моя ответственность',
    Participant: 'Я участник',
};

const STATE_LABELS: Record<string, string> = {
    '': 'Все состояния',
    Active: 'Активные',
    Completed: 'Завершённые',
    Cancelled: 'Прерванные',
    Suspended: 'Приостановленные',
    Faulted: 'С ошибкой',
};

const STATE_BADGE: Record<BpmInstanceState, string> = {
    Active: 'badge--active',
    Completed: 'badge--completed',
    Cancelled: 'badge--cancelled',
    Suspended: 'badge--suspended',
    Faulted: 'badge--faulted',
};

const STATE_LABEL: Record<BpmInstanceState, string> = {
    Active: 'Активный',
    Completed: 'Завершён',
    Cancelled: 'Прерван',
    Suspended: 'Приостановлен',
    Faulted: 'Ошибка',
};

/** Раздел «Мои процессы» — личный раздел пользователя (FR-BPM-02.3). */
export function MyProcessesPage({ onOpenInstance }: MyProcessesPageProps) {
    const { accessToken } = useAuth();
    const [tab, setTab] = useState<TabId>('processes');

    // ─── Фильтры ────────────────────────────────────────────────────────────
    const [role, setRole] = useState<MyInstancesRole>('Initiator');
    const [state, setState] = useState<BpmInstanceState | ''>('');
    const [search, setSearch] = useState('');
    const [processId, setProcessId] = useState('');
    const [dateFrom, setDateFrom] = useState('');
    const [dateTo, setDateTo] = useState('');
    const [showAdvanced, setShowAdvanced] = useState(false);

    // ─── Данные ─────────────────────────────────────────────────────────────
    const [items, setItems] = useState<BpmInstanceListItemDto[]>([]);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(1);
    const PAGE_SIZE = 30;
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // ─── Процессы для dropdown ───────────────────────────────────────────────
    const [processList, setProcessList] = useState<{ id: string; name: string }[]>([]);

    // ─── Сохранённые фильтры ────────────────────────────────────────────────
    const [savedFilters, setSavedFilters] = useState<BpmSavedFilterDto[]>([]);
    const [saveFilterName, setSaveFilterName] = useState('');
    const [showSaveForm, setShowSaveForm] = useState(false);

    // ─── Загрузка списка процессов (один раз) ───────────────────────────────
    useEffect(() => {
        if (!accessToken) return;
        // Загружаем организации, затем процессы первой организации
        fetch('/api/org/directory/organizations', {
            headers: { Authorization: `Bearer ${accessToken}` },
        })
            .then(r => r.json())
            .then((orgs: { id: string }[]) => {
                if (orgs.length === 0) return Promise.resolve([] as { id: string; name: string }[]);
                return fetch(`/api/bpm/processes?organizationId=${orgs[0].id}`, {
                    headers: { Authorization: `Bearer ${accessToken}` },
                }).then(r2 => r2.json() as Promise<{ id: string; name: string }[]>);
            })
            .then(p => setProcessList(p.map(x => ({ id: x.id, name: x.name }))))
            .catch(() => {});
        getSavedFilters(accessToken).then(setSavedFilters).catch(() => {});
    }, [accessToken]);

    // ─── Загрузка экземпляров ───────────────────────────────────────────────
    const load = useCallback(async (p: number) => {
        if (!accessToken) return;
        setLoading(true);
        setError(null);
        try {
            const filter: MyInstancesFilter = {
                role,
                state: state || undefined,
                search: search.trim() || undefined,
                processId: processId || undefined,
                dateFrom: dateFrom || undefined,
                dateTo: dateTo || undefined,
            };
            const result = await getMyInstances(accessToken, filter, p, PAGE_SIZE);
            setItems(result.items);
            setTotal(result.total);
            setPage(p);
        } catch {
            setError('Ошибка при загрузке данных');
        } finally {
            setLoading(false);
        }
    }, [accessToken, role, state, search, processId, dateFrom, dateTo]);

    // Автоматический рефреш при смене фильтров
    useEffect(() => { load(1); }, [load]);

    // ─── Ссылка на поиск ────────────────────────────────────────────────────
    const handleCopyLink = () => {
        const params = new URLSearchParams({ role, ...(state && { state }), ...(search && { search }) });
        const url = `${window.location.origin}${window.location.pathname}#my-processes?${params}`;
        navigator.clipboard.writeText(url).catch(() => {});
    };

    // ─── Сохранить фильтр ───────────────────────────────────────────────────
    const handleSaveFilter = async () => {
        if (!accessToken || !saveFilterName.trim()) return;
        const filtersJson = JSON.stringify({ role, state, search, processId, dateFrom, dateTo });
        const created = await createSavedFilter(accessToken, { name: saveFilterName.trim(), filtersJson });
        setSavedFilters(prev => [...prev, created]);
        setSaveFilterName('');
        setShowSaveForm(false);
    };

    const handleApplySaved = (f: BpmSavedFilterDto) => {
        try {
            const parsed = JSON.parse(f.filtersJson);
            if (parsed.role) setRole(parsed.role);
            if (parsed.state !== undefined) setState(parsed.state);
            if (parsed.search !== undefined) setSearch(parsed.search);
            if (parsed.processId !== undefined) setProcessId(parsed.processId);
            if (parsed.dateFrom !== undefined) setDateFrom(parsed.dateFrom);
            if (parsed.dateTo !== undefined) setDateTo(parsed.dateTo);
        } catch { /* игнорируем невалидный JSON */ }
    };

    const handleDeleteSaved = async (id: string) => {
        if (!accessToken) return;
        await deleteSavedFilter(accessToken, id);
        setSavedFilters(prev => prev.filter(f => f.id !== id));
    };

    const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

    // ─── Экспорт в CSV ──────────────────────────────────────────────────────
    const handleExport = async () => {
        if (!accessToken) return;
        try {
            const filter: MyInstancesFilter = {
                role,
                state: state || undefined,
                search: search.trim() || undefined,
                processId: processId || undefined,
                dateFrom: dateFrom || undefined,
                dateTo: dateTo || undefined,
            };
            const blob = await exportMyInstances(accessToken, filter);
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'my-processes.csv';
            a.click();
            URL.revokeObjectURL(url);
        } catch { /* тихая обработка */ }
    };

    return (
        <div className="mpp-root">
            <div className="mpp-header">
                <h1 className="mpp-title">Мои процессы</h1>
                <div className="mpp-tabs">
                    <button
                        className={`mpp-tab${tab === 'processes' ? ' mpp-tab--active' : ''}`}
                        onClick={() => setTab('processes')}
                    >
                        Процессы
                        {total > 0 && <span className="mpp-tab-count">{total}</span>}
                    </button>
                    <button
                        className={`mpp-tab${tab === 'tasks' ? ' mpp-tab--active' : ''}`}
                        onClick={() => setTab('tasks')}
                    >
                        Мои задачи
                    </button>
                </div>
            </div>

            {tab === 'processes' && (
                <>
                    {/* ─── Фильтры ─────────────────────────────────── */}
                    <div className="mpp-filters">
                        <div className="mpp-role-chips">
                            {(Object.keys(ROLE_LABELS) as MyInstancesRole[]).map(r => (
                                <button
                                    key={r}
                                    className={`mpp-chip${role === r ? ' mpp-chip--active' : ''}`}
                                    onClick={() => setRole(r)}
                                >
                                    {ROLE_LABELS[r]}
                                </button>
                            ))}
                        </div>
                        <div className="mpp-search-row">
                            <input
                                className="mpp-search"
                                type="text"
                                placeholder="Поиск по названию экземпляра…"
                                value={search}
                                onChange={e => setSearch(e.target.value)}
                            />
                            <select
                                className="mpp-select"
                                value={state}
                                onChange={e => setState(e.target.value as BpmInstanceState | '')}
                            >
                                {Object.entries(STATE_LABELS).map(([v, l]) => (
                                    <option key={v} value={v}>{l}</option>
                                ))}
                            </select>
                            <button
                                className={`mpp-btn-text${showAdvanced ? ' active' : ''}`}
                                onClick={() => setShowAdvanced(!showAdvanced)}
                            >
                                {showAdvanced ? '▲ Свернуть' : '▼ Расширенный поиск'}
                            </button>
                        </div>

                        {showAdvanced && (
                            <div className="mpp-advanced">
                                <div className="mpp-advanced-row">
                                    <label className="mpp-label">
                                        Процесс
                                        <select
                                            className="mpp-select"
                                            value={processId}
                                            onChange={e => setProcessId(e.target.value)}
                                        >
                                            <option value="">— Все процессы —</option>
                                            {processList.map(p => (
                                                <option key={p.id} value={p.id}>{p.name}</option>
                                            ))}
                                        </select>
                                    </label>
                                    <label className="mpp-label">
                                        Дата запуска от
                                        <input
                                            type="date"
                                            className="mpp-input"
                                            value={dateFrom}
                                            onChange={e => setDateFrom(e.target.value)}
                                        />
                                    </label>
                                    <label className="mpp-label">
                                        Дата запуска до
                                        <input
                                            type="date"
                                            className="mpp-input"
                                            value={dateTo}
                                            onChange={e => setDateTo(e.target.value)}
                                        />
                                    </label>
                                </div>
                                <div className="mpp-advanced-actions">
                                    <button className="mpp-btn-text" onClick={handleCopyLink} title="Скопировать ссылку на текущий поиск">
                                        🔗 Скопировать ссылку на поиск
                                    </button>
                                    <button className="mpp-btn-text" onClick={() => setShowSaveForm(!showSaveForm)}>
                                        ⭐ Сохранить фильтр
                                    </button>
                                    <button className="mpp-btn-text" onClick={handleExport} title="Экспорт результатов в CSV">
                                        ⬇️ Экспорт в CSV
                                    </button>
                                </div>
                                {showSaveForm && (
                                    <div className="mpp-save-form">
                                        <input
                                            className="mpp-input"
                                            placeholder="Название фильтра"
                                            value={saveFilterName}
                                            onChange={e => setSaveFilterName(e.target.value)}
                                        />
                                        <button className="mpp-btn-primary" onClick={handleSaveFilter} disabled={!saveFilterName.trim()}>
                                            Сохранить
                                        </button>
                                        <button className="mpp-btn-text" onClick={() => setShowSaveForm(false)}>Отмена</button>
                                    </div>
                                )}
                                {savedFilters.length > 0 && (
                                    <div className="mpp-saved-filters">
                                        <span className="mpp-saved-label">Сохранённые фильтры:</span>
                                        {savedFilters.map(f => (
                                            <div key={f.id} className="mpp-saved-item">
                                                <button className="mpp-chip" onClick={() => handleApplySaved(f)}>
                                                    {f.name}
                                                </button>
                                                <button
                                                    className="mpp-btn-icon"
                                                    title="Удалить"
                                                    onClick={() => handleDeleteSaved(f.id)}
                                                >✕</button>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        )}
                    </div>

                    {/* ─── Список ──────────────────────────────────── */}
                    {loading && <div className="mpp-loading">Загрузка…</div>}
                    {error && <div className="mpp-error">{error}</div>}
                    {!loading && !error && items.length === 0 && (
                        <div className="mpp-empty">
                            <div className="mpp-empty-icon">📋</div>
                            <p>Экземпляры процессов не найдены</p>
                            <p className="mpp-empty-hint">Запустите процесс, чтобы он появился здесь</p>
                        </div>
                    )}
                    {!loading && items.length > 0 && (
                        <div className="mpp-table-wrap">
                            <table className="mpp-table">
                                <thead>
                                    <tr>
                                        <th>Название</th>
                                        <th>Процесс</th>
                                        <th>Состояние</th>
                                        <th>Инициатор</th>
                                        <th>Ответственный</th>
                                        <th>Запущен</th>
                                        <th>Завершён</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {items.map(item => (
                                        <tr
                                            key={item.id}
                                            className="mpp-row"
                                            onClick={() => onOpenInstance(item.id)}
                                            role="button"
                                            tabIndex={0}
                                            onKeyDown={e => e.key === 'Enter' && onOpenInstance(item.id)}
                                        >
                                            <td className="mpp-cell-name">{item.name}</td>
                                            <td>{item.processName}</td>
                                            <td>
                                                <span className={`mpp-badge ${STATE_BADGE[item.state]}`}>
                                                    {STATE_LABEL[item.state]}
                                                </span>
                                            </td>
                                            <td>{item.initiatorDisplayName ?? '—'}</td>
                                            <td>{item.responsibleDisplayName ?? '—'}</td>
                                            <td>{new Date(item.startedAt).toLocaleDateString('ru-RU')}</td>
                                            <td>
                                                {item.completedAt
                                                    ? new Date(item.completedAt).toLocaleDateString('ru-RU')
                                                    : item.cancelledAt
                                                        ? new Date(item.cancelledAt).toLocaleDateString('ru-RU')
                                                        : '—'}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>

                            {/* Пагинация */}
                            {totalPages > 1 && (
                                <div className="mpp-pagination">
                                    <button
                                        className="mpp-page-btn"
                                        disabled={page === 1}
                                        onClick={() => load(page - 1)}
                                    >◀</button>
                                    <span className="mpp-page-info">Стр. {page} / {totalPages} (всего {total})</span>
                                    <button
                                        className="mpp-page-btn"
                                        disabled={page >= totalPages}
                                        onClick={() => load(page + 1)}
                                    >▶</button>
                                </div>
                            )}
                        </div>
                    )}
                </>
            )}

            {tab === 'tasks' && (
                <div className="mpp-tasks-placeholder">
                    <div className="mpp-empty-icon">🗂️</div>
                    <p>Вкладка «Мои задачи» будет доступна после запуска движка выполнения процессов.</p>
                    <p className="mpp-empty-hint">Здесь будут отображаться активные задачи, назначенные вам в рамках бизнес-процессов.</p>
                </div>
            )}
        </div>
    );
}
