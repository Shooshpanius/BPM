import { useState, useEffect, useRef, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getQueue,
    getQueueStats,
    retryJob,
    cancelQueueTimer,
    rescheduleTimer,
    type BpmExecutionJobDto,
    type BpmJobStatus,
    type QueueStatsDto,
} from '../../api/bpmApi';
import './ExecutionQueuePage.css';

// ─── Вспомогательные утилиты ─────────────────────────────────────────────────

function fmtDate(iso?: string) {
    if (!iso) return '—';
    try { return new Date(iso).toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }); }
    catch { return iso; }
}

function fmtDuration(startedAt?: string, completedAt?: string): string {
    if (!startedAt || !completedAt) return '—';
    const ms = new Date(completedAt).getTime() - new Date(startedAt).getTime();
    if (ms < 1000) return `${ms} мс`;
    if (ms < 60000) return `${(ms / 1000).toFixed(1)} с`;
    return `${Math.floor(ms / 60000)} мин`;
}

// ─── Константы статусов ───────────────────────────────────────────────────────

const STATUS_LABELS: Record<BpmJobStatus, string> = {
    Pending:   'Ожидает',
    Running:   'Выполняется',
    Scheduled: 'Запланировано',
    Completed: 'Выполнено',
    Failed:    'Ошибка',
};

const STATUS_CSS: Record<BpmJobStatus, string> = {
    Pending:   'q-badge--pending',
    Running:   'q-badge--running',
    Scheduled: 'q-badge--scheduled',
    Completed: 'q-badge--completed',
    Failed:    'q-badge--failed',
};

const ELEMENT_TYPE_LABELS: Record<string, string> = {
    serviceTask: 'Service Task',
    scriptTask:  'Script Task',
    timerEvent:  'Таймер',
    rpaTask:     'RPA-задача',
    callActivity: 'Call Activity',
};

function elementLabel(type: string) {
    return ELEMENT_TYPE_LABELS[type] ?? type;
}

// ─── Компонент ────────────────────────────────────────────────────────────────

type TabId = 'active' | 'failed';

/** Страница очереди исполнения и ошибок (FR-BPM-02.5). */
export function ExecutionQueuePage() {
    const { accessToken } = useAuth();

    const [tab, setTab] = useState<TabId>('active');
    const [jobs, setJobs] = useState<BpmExecutionJobDto[]>([]);
    const [stats, setStats] = useState<QueueStatsDto | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Фильтры
    const [search, setSearch] = useState('');
    const [includeScheduled, setIncludeScheduled] = useState(false);
    const [page, setPage] = useState(1);
    const PAGE_SIZE = 50;

    // Автообновление
    const [autoRefresh, setAutoRefresh] = useState(true);
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

    // Контекстное меню
    const [ctxJob, setCtxJob] = useState<BpmExecutionJobDto | null>(null);
    const [ctxPos, setCtxPos] = useState({ x: 0, y: 0 });
    const [rescheduleModal, setRescheduleModal] = useState<BpmExecutionJobDto | null>(null);
    const [rescheduleValue, setRescheduleValue] = useState('');

    // ─── Загрузка данных ──────────────────────────────────────────────────────

    const load = useCallback(async () => {
        if (!accessToken) return;
        setLoading(true);
        setError(null);
        try {
            const statusFilter: BpmJobStatus | undefined =
                tab === 'failed' ? 'Failed' : undefined;
            const [list, s] = await Promise.all([
                getQueue(accessToken, {
                    status: statusFilter,
                    instanceName: search.trim() || undefined,
                    includeScheduled: tab === 'active' ? includeScheduled : undefined,
                    page,
                    pageSize: PAGE_SIZE,
                }),
                getQueueStats(accessToken).catch(() => null),
            ]);
            setJobs(list);
            if (s) setStats(s);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [accessToken, tab, search, includeScheduled, page]);

    useEffect(() => { load(); }, [load]);

    // Авто-обновление каждые 15 секунд
    useEffect(() => {
        if (timerRef.current) clearInterval(timerRef.current);
        if (autoRefresh) {
            timerRef.current = setInterval(() => { load(); }, 15_000);
        }
        return () => { if (timerRef.current) clearInterval(timerRef.current); };
    }, [autoRefresh, load]);

    // ─── Действия ─────────────────────────────────────────────────────────────

    const handleRetry = async (job: BpmExecutionJobDto) => {
        if (!accessToken) return;
        try {
            await retryJob(accessToken, job.id);
            load();
        } catch { setError('Ошибка при повторном запуске'); }
        setCtxJob(null);
    };

    const handleCancelTimer = async (job: BpmExecutionJobDto) => {
        if (!accessToken) return;
        try {
            await cancelQueueTimer(accessToken, job.id);
            load();
        } catch { setError('Ошибка при отмене таймера'); }
        setCtxJob(null);
    };

    const handleReschedule = async () => {
        if (!accessToken || !rescheduleModal || !rescheduleValue) return;
        try {
            await rescheduleTimer(accessToken, rescheduleModal.id, new Date(rescheduleValue).toISOString());
            setRescheduleModal(null);
            load();
        } catch { setError('Ошибка при переносе таймера'); }
    };

    // ─── Рендер ───────────────────────────────────────────────────────────────

    return (
        <div className="eq-root" onClick={() => setCtxJob(null)}>
            {/* Заголовок */}
            <div className="eq-header">
                <h1 className="eq-title">Очередь исполнения</h1>
                <div className="eq-header-controls">
                    <label className="eq-auto-refresh">
                        <input
                            type="checkbox"
                            checked={autoRefresh}
                            onChange={e => setAutoRefresh(e.target.checked)}
                        />
                        <span>Авто-обновление</span>
                    </label>
                    <button className="eq-refresh-btn" onClick={load} title="Обновить">↻</button>
                </div>
            </div>

            {/* Счётчики */}
            {stats && (
                <div className="eq-stats">
                    <StatBadge label="Выполняется" value={stats.running} cls="eq-stat--running" />
                    <StatBadge label="Ожидает" value={stats.pending} cls="eq-stat--pending" />
                    <StatBadge label="Запланировано" value={stats.scheduled} cls="eq-stat--scheduled" />
                    <StatBadge label="Ошибки" value={stats.failed} cls="eq-stat--failed" />
                </div>
            )}

            {/* Вкладки */}
            <div className="eq-tabs">
                <button
                    className={`eq-tab${tab === 'active' ? ' eq-tab--active' : ''}`}
                    onClick={() => { setTab('active'); setPage(1); }}
                >
                    Текущие операции
                    {stats && stats.running + stats.pending > 0 && (
                        <span className="eq-tab-badge">{stats.running + stats.pending}</span>
                    )}
                </button>
                <button
                    className={`eq-tab${tab === 'failed' ? ' eq-tab--active' : ''}`}
                    onClick={() => { setTab('failed'); setPage(1); }}
                >
                    Ошибки
                    {stats && stats.failed > 0 && (
                        <span className="eq-tab-badge eq-tab-badge--error">{stats.failed}</span>
                    )}
                </button>
            </div>

            {/* Панель поиска */}
            <div className="eq-toolbar">
                <input
                    className="eq-search"
                    type="text"
                    placeholder="Поиск по названию экземпляра…"
                    value={search}
                    onChange={e => { setSearch(e.target.value); setPage(1); }}
                />
                {tab === 'active' && (
                    <label className="eq-checkbox">
                        <input
                            type="checkbox"
                            checked={includeScheduled}
                            onChange={e => setIncludeScheduled(e.target.checked)}
                        />
                        <span>Показать запланированные</span>
                    </label>
                )}
                <span className="eq-count">{jobs.length} записей</span>
            </div>

            {error && <div className="eq-error">{error}</div>}

            {loading && <div className="eq-loading">Загрузка…</div>}

            {!loading && jobs.length === 0 && (
                <div className="eq-empty">
                    <div className="eq-empty-icon">{tab === 'failed' ? '✅' : '⚙️'}</div>
                    <p>{tab === 'failed' ? 'Ошибок нет' : 'Очередь пуста'}</p>
                </div>
            )}

            {/* Таблица */}
            {!loading && jobs.length > 0 && (
                <div className="eq-table-wrap">
                    <table className="eq-table">
                        <thead>
                            <tr>
                                <th>Статус</th>
                                <th>Экземпляр / Процесс</th>
                                <th>Операция</th>
                                <th>Попытка</th>
                                <th>Последнее выполнение</th>
                                <th>Следующее выполнение</th>
                                <th>Сервер</th>
                            </tr>
                        </thead>
                        <tbody>
                            {jobs.map(job => (
                                <tr
                                    key={job.id}
                                    onContextMenu={e => {
                                        e.preventDefault();
                                        setCtxJob(job);
                                        setCtxPos({ x: e.clientX, y: e.clientY });
                                    }}
                                    className={job.status === 'Failed' ? 'eq-row--error' : ''}
                                >
                                    <td>
                                        <span className={`q-badge ${STATUS_CSS[job.status]}`}>
                                            {STATUS_LABELS[job.status]}
                                        </span>
                                    </td>
                                    <td>
                                        <div className="eq-cell-main">{job.instanceName ?? '—'}</div>
                                        <div className="eq-cell-sub">{job.processName}</div>
                                    </td>
                                    <td>
                                        <div className="eq-cell-main">{job.operationName ?? job.elementId}</div>
                                        <div className="eq-cell-sub">{elementLabel(job.elementType)}</div>
                                    </td>
                                    <td>
                                        {job.attemptNumber}/{job.maxAttempts}
                                    </td>
                                    <td>
                                        <div>{fmtDate(job.startedAt)}</div>
                                        {job.completedAt && (
                                            <div className="eq-cell-sub">
                                                Длительность: {fmtDuration(job.startedAt, job.completedAt)}
                                            </div>
                                        )}
                                        {job.lastError && (
                                            <div className="eq-cell-error" title={job.lastError}>
                                                {job.lastError.length > 60
                                                    ? job.lastError.slice(0, 60) + '…'
                                                    : job.lastError}
                                            </div>
                                        )}
                                    </td>
                                    <td>{fmtDate(job.nextRunAt)}</td>
                                    <td>{job.serverHost ?? '—'}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {/* Пагинация */}
                    {(jobs.length === PAGE_SIZE || page > 1) && (
                        <div className="eq-pagination">
                            {page > 1 && (
                                <button className="eq-page-btn" onClick={() => setPage(p => p - 1)}>← Назад</button>
                            )}
                            <span className="eq-page-num">Стр. {page}</span>
                            {jobs.length === PAGE_SIZE && (
                                <button className="eq-page-btn" onClick={() => setPage(p => p + 1)}>Вперёд →</button>
                            )}
                        </div>
                    )}
                </div>
            )}

            {/* Контекстное меню */}
            {ctxJob && (
                <div
                    className="eq-ctx-menu"
                    style={{ top: ctxPos.y, left: ctxPos.x }}
                    onClick={e => e.stopPropagation()}
                >
                    <button className="eq-ctx-item" onClick={() => handleRetry(ctxJob)}>
                        ▶ Выполнить (принудительно)
                    </button>
                    {ctxJob.isTimer && ctxJob.status !== 'Failed' && (
                        <button
                            className="eq-ctx-item"
                            onClick={() => {
                                const d = new Date(ctxJob.nextRunAt ?? Date.now() + 3600000);
                                setRescheduleValue(d.toISOString().slice(0, 16));
                                setRescheduleModal(ctxJob);
                                setCtxJob(null);
                            }}
                        >
                            🕒 Перенести время
                        </button>
                    )}
                    {ctxJob.isTimer && (
                        <button className="eq-ctx-item eq-ctx-item--danger" onClick={() => handleCancelTimer(ctxJob)}>
                            ✕ Прервать таймер
                        </button>
                    )}
                </div>
            )}

            {/* Модальное окно переноса таймера */}
            {rescheduleModal && (
                <div className="eq-modal-overlay" onClick={() => setRescheduleModal(null)}>
                    <div className="eq-modal" onClick={e => e.stopPropagation()}>
                        <h3 className="eq-modal-title">Перенести таймер</h3>
                        <p className="eq-modal-sub">{rescheduleModal.operationName ?? rescheduleModal.elementId}</p>
                        <input
                            className="eq-modal-input"
                            type="datetime-local"
                            value={rescheduleValue}
                            onChange={e => setRescheduleValue(e.target.value)}
                        />
                        <div className="eq-modal-actions">
                            <button className="eq-modal-cancel" onClick={() => setRescheduleModal(null)}>Отмена</button>
                            <button className="eq-modal-ok" onClick={handleReschedule}>Перенести</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

// ─── Вспомогательный компонент ───────────────────────────────────────────────

function StatBadge({ label, value, cls }: { label: string; value: number; cls: string }) {
    return (
        <div className={`eq-stat ${cls}`}>
            <span className="eq-stat-value">{value}</span>
            <span className="eq-stat-label">{label}</span>
        </div>
    );
}
