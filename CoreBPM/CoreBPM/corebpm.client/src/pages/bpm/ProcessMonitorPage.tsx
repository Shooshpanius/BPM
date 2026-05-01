import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/bpmApi';
import type {
    BpmInstanceListItemDto,
    BpmInstanceState,
    InstanceStatusOptionDto,
    BpmProcessStatsDto,
} from '../../api/bpmApi';
import { NodeAnalyticsPanel } from '../../components/bpm/NodeAnalyticsPanel';
import './ProcessMonitorPage.css';

// ─── Утилиты ─────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
    try {
        return new Date(iso).toLocaleDateString('ru-RU', {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit',
        });
    } catch { return iso; }
}

const STATE_LABELS: Record<BpmInstanceState, string> = {
    Active: 'Выполняется',
    Completed: 'Завершён',
    Cancelled: 'Прерван',
    Suspended: 'Приостановлен',
    Faulted: 'Ошибка',
};

const STATE_COLORS: Record<BpmInstanceState, string> = {
    Active: '#dbeafe',
    Completed: '#dcfce7',
    Cancelled: '#fee2e2',
    Suspended: '#fef9c3',
    Faulted: '#fce7f3',
};

const STATE_TEXT_COLORS: Record<BpmInstanceState, string> = {
    Active: '#1d4ed8',
    Completed: '#15803d',
    Cancelled: '#b91c1c',
    Suspended: '#854d0e',
    Faulted: '#9d174d',
};

// ─── Пропсы ───────────────────────────────────────────────────────────────────

interface Props {
    processId: string;
    processName: string;
    onBack: () => void;
    onOpenInstance?: (instanceId: string) => void;
}

type ViewMode = 'list' | 'kanban' | 'analytics';

// ─── Компонент ───────────────────────────────────────────────────────────────

/**
 * ProcessMonitorPage — монитор экземпляров процесса.
 * Показывает реальные данные из API с фильтрами по состоянию и Kanban-видом.
 */
export function ProcessMonitorPage({ processId, processName, onBack, onOpenInstance }: Props) {
    const { accessToken: token } = useAuth();

    const [viewMode, setViewMode] = useState<ViewMode>('list');
    const [statusOptions, setStatusOptions] = useState<InstanceStatusOptionDto[]>([]);
    const [filterState, setFilterState] = useState<BpmInstanceState | ''>('');
    const [instances, setInstances] = useState<BpmInstanceListItemDto[]>([]);
    const [stats, setStats] = useState<BpmProcessStatsDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [page, setPage] = useState(1);
    const PAGE_SIZE = 50;

    // ─── Загрузка ─────────────────────────────────────────────────────────────

    const loadData = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const [statusConfig, insts, processStats] = await Promise.all([
                api.getStatusConfig(token, processId).catch(() => ({ options: [] })),
                api.getInstances(token, processId, page, PAGE_SIZE),
                api.getProcessStats(token, processId).catch(() => null),
            ]);
            setStatusOptions(statusConfig.options ?? []);
            setInstances(insts);
            setStats(processStats);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId, page]);

    useEffect(() => { loadData(); }, [loadData]);

    // ─── Фильтрация ───────────────────────────────────────────────────────────

    const filtered = filterState
        ? instances.filter(i => i.state === filterState)
        : instances;

    // ─── Kanban-группировка по состоянию ─────────────────────────────────────

    const ALWAYS_SHOWN_STATES: (BpmInstanceState | '')[] = ['Active' as BpmInstanceState];

    const kanbanColumns: { state: BpmInstanceState | ''; label: string; items: BpmInstanceListItemDto[] }[] =
        ([
            { state: 'Active' as BpmInstanceState, label: 'Выполняется' },
            { state: 'Faulted' as BpmInstanceState, label: 'Ошибка' },
            { state: 'Suspended' as BpmInstanceState, label: 'Приостановлен' },
            { state: 'Completed' as BpmInstanceState, label: 'Завершён' },
            { state: 'Cancelled' as BpmInstanceState, label: 'Прерван' },
        ]).map(col => ({
            ...col,
            items: instances.filter(i => i.state === col.state),
        })).filter(col => col.items.length > 0 || ALWAYS_SHOWN_STATES.includes(col.state));

    return (
        <div className="pmon-root">
            {/* Заголовок */}
            <div className="pmon-header">
                <button className="pmon-back-btn" onClick={onBack}>← Процессы</button>
                <h2 className="pmon-title">Монитор: {processName}</h2>
                <div className="pmon-header-spacer" />
                <div className="pmon-view-toggle" role="group" aria-label="Вид">
                    <button
                        className={`pmon-view-btn${viewMode === 'list' ? ' active' : ''}`}
                        onClick={() => setViewMode('list')}
                        title="Список"
                    >☰ Список</button>
                    <button
                        className={`pmon-view-btn${viewMode === 'kanban' ? ' active' : ''}`}
                        onClick={() => setViewMode('kanban')}
                        title="Kanban"
                    >⊞ Kanban</button>
                    <button
                        className={`pmon-view-btn${viewMode === 'analytics' ? ' active' : ''}`}
                        onClick={() => setViewMode('analytics')}
                        title="Аналитика узлов"
                    >📊 Аналитика</button>
                    <button
                        className="pmon-view-btn"
                        onClick={async () => {
                            if (!token) return;
                            try {
                                const blob = await api.exportProcessInstances(token, processId);
                                const url = URL.createObjectURL(blob);
                                const a = document.createElement('a');
                                a.href = url;
                                a.download = `${processName.replace(/[^a-zA-ZА-яа-я0-9]/g, '_')}_instances.csv`;
                                a.click();
                                URL.revokeObjectURL(url);
                            } catch { /* тихая обработка */ }
                        }}
                        title="Экспортировать список в CSV"
                    >⬇️ CSV</button>
                </div>
            </div>

            {/* Информационная панель процесса */}
            {stats && (
                <div className="pmon-info-panel">
                    <div className="pmon-info-stats">
                        <InfoStat label="Активных" value={stats.activeCount} cls="info-stat--active" />
                        <InfoStat label="На паузе" value={stats.suspendedCount} cls="info-stat--suspended" />
                        <InfoStat label="Завершено" value={stats.completedCount} cls="info-stat--completed" />
                        <InfoStat label="Прервано" value={stats.cancelledCount} cls="info-stat--cancelled" />
                        <InfoStat label="Всего" value={stats.totalCount} cls="info-stat--total" />
                    </div>
                    {(stats.owners.length > 0 || stats.curators.length > 0 || stats.activeVersionNumber != null) && (
                        <div className="pmon-info-meta">
                            {stats.activeVersionNumber != null && (
                                <span className="pmon-meta-item">
                                    <span className="pmon-meta-label">Версия:</span> v{stats.activeVersionNumber}
                                    {stats.publishedAt && ` (${new Date(stats.publishedAt).toLocaleDateString('ru-RU')})`}
                                </span>
                            )}
                            {stats.owners.length > 0 && (
                                <span className="pmon-meta-item">
                                    <span className="pmon-meta-label">Владелец:</span> {stats.owners.join(', ')}
                                </span>
                            )}
                            {stats.curators.length > 0 && (
                                <span className="pmon-meta-item">
                                    <span className="pmon-meta-label">Кураторы:</span> {stats.curators.join(', ')}
                                </span>
                            )}
                        </div>
                    )}
                </div>
            )}

            {/* Фильтры */}
            <div className="pmon-filters">
                <label className="pmon-filter-label">Состояние:</label>
                <select
                    className="pmon-select"
                    value={filterState}
                    onChange={e => setFilterState(e.target.value as BpmInstanceState | '')}
                >
                    <option value="">Все</option>
                    {(['Active', 'Suspended', 'Completed', 'Cancelled'] as BpmInstanceState[]).map(s => (
                        <option key={s} value={s}>{STATE_LABELS[s]}</option>
                    ))}
                </select>
                <span className="pmon-count">{filtered.length} записей</span>
                <div style={{ flex: 1 }} />
                <button
                    className="pmon-view-btn"
                    style={{ border: '1px solid #dde1e7', borderRadius: 6 }}
                    onClick={loadData}
                    title="Обновить"
                >↻ Обновить</button>
            </div>

            {error && <div className="pmon-error">{error}</div>}

            {loading && (
                <div className="pmon-placeholder" style={{ padding: 24 }}>
                    <span style={{ color: '#9ca3af' }}>Загрузка…</span>
                </div>
            )}

            {/* Пустое состояние */}
            {!loading && instances.length === 0 && (
                <div className="pmon-placeholder">
                    <div className="pmon-placeholder-icon">⚙️</div>
                    <div className="pmon-placeholder-title">Нет запущенных экземпляров</div>
                    <p className="pmon-placeholder-text">
                        Запустите процесс «{processName}» с помощью кнопки ▷ на странице процессов.
                    </p>
                    {statusOptions.length > 0 && (
                        <div className="pmon-status-preview">
                            <p style={{ fontWeight: 500, marginBottom: 8 }}>Статусы:</p>
                            {statusOptions.map(opt => (
                                <span key={opt.id} className="pmon-status-badge">{opt.name}</span>
                            ))}
                        </div>
                    )}
                </div>
            )}

            {/* Список */}
            {!loading && viewMode === 'list' && filtered.length > 0 && (
                <div className="pmon-list">
                    <table className="pmon-table">
                        <thead>
                            <tr>
                                <th>Название</th>
                                <th>Состояние</th>
                                <th>Запущен</th>
                                <th>Инициатор</th>
                                <th>Ответственный</th>
                                <th>Версия</th>
                            </tr>
                        </thead>
                        <tbody>
                            {filtered.map(inst => (
                                <tr
                                    key={inst.id}
                                    style={{ cursor: onOpenInstance ? 'pointer' : 'default' }}
                                    onClick={() => onOpenInstance?.(inst.id)}
                                >
                                    <td>
                                        <span style={{ fontWeight: 500 }}>{inst.name}</span>
                                    </td>
                                    <td>
                                        <span
                                            className="pmon-status-badge"
                                            style={{
                                                background: STATE_COLORS[inst.state],
                                                color: STATE_TEXT_COLORS[inst.state],
                                            }}
                                        >
                                            {STATE_LABELS[inst.state]}
                                        </span>
                                    </td>
                                    <td>{formatDate(inst.startedAt)}</td>
                                    <td>{inst.initiatorDisplayName ?? '—'}</td>
                                    <td>{inst.responsibleDisplayName ?? '—'}</td>
                                    <td>v{inst.processVersionNumber}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {/* Пагинация */}
                    {(instances.length === PAGE_SIZE || page > 1) && (
                        <div style={{ display: 'flex', gap: 8, justifyContent: 'center', padding: '16px 0' }}>
                            {page > 1 && (
                                <button className="pmon-view-btn" onClick={() => setPage(p => p - 1)}
                                    style={{ border: '1px solid #dde1e7', borderRadius: 6, padding: '5px 12px' }}>
                                    ← Назад
                                </button>
                            )}
                            <span style={{ alignSelf: 'center', fontSize: 13, color: '#6b7280' }}>Стр. {page}</span>
                            {instances.length === PAGE_SIZE && (
                                <button className="pmon-view-btn" onClick={() => setPage(p => p + 1)}
                                    style={{ border: '1px solid #dde1e7', borderRadius: 6, padding: '5px 12px' }}>
                                    Вперёд →
                                </button>
                            )}
                        </div>
                    )}
                </div>
            )}

            {/* Kanban */}
            {!loading && viewMode === 'kanban' && (
                <div className="pmon-kanban">
                    {kanbanColumns.map(col => (
                        <div key={col.state} className="pmon-kanban-col">
                            <div
                                className="pmon-kanban-col-header"
                                style={{ borderBottom: `2px solid ${STATE_COLORS[col.state as BpmInstanceState] ?? '#e5e7eb'}` }}
                            >
                                {col.label}
                                <span className="pmon-kanban-count">{col.items.length}</span>
                            </div>
                            <div className="pmon-kanban-col-body">
                                {col.items.map(inst => (
                                    <div
                                        key={inst.id}
                                        className="pmon-kanban-card"
                                        style={{ cursor: onOpenInstance ? 'pointer' : 'default' }}
                                        onClick={() => onOpenInstance?.(inst.id)}
                                    >
                                        <div className="pmon-kanban-card-title">{inst.name}</div>
                                        <div className="pmon-kanban-card-meta">
                                            <span>{formatDate(inst.startedAt)}</span>
                                            <span>{inst.initiatorDisplayName ?? '—'}</span>
                                        </div>
                                    </div>
                                ))}
                                {col.items.length === 0 && (
                                    <div className="pmon-kanban-empty">Нет экземпляров</div>
                                )}
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {/* Аналитика узлов */}
            {viewMode === 'analytics' && (
                <div className="pmon-analytics">
                    <NodeAnalyticsPanel processId={processId} />
                </div>
            )}
        </div>
    );
}

// ─── Вспомогательный компонент статистики ────────────────────────────────────

function InfoStat({ label, value, cls }: { label: string; value: number; cls: string }) {
    return (
        <div className={`pmon-info-stat ${cls}`}>
            <span className="pmon-info-stat-value">{value}</span>
            <span className="pmon-info-stat-label">{label}</span>
        </div>
    );
}
