import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getMyMonitorProcesses,
    getFullMonitorProcesses,
    getBpmDashboard,
    type BpmProcessMonitorItemDto,
    type BpmDashboardDto,
} from '../../api/bpmApi';
import './ProcessMonitorListPage.css';

interface Props {
    onOpenMonitor: (processId: string, processName: string) => void;
}

type TabId = 'my' | 'full';

/** Список мониторов процессов с агрегированной статистикой (FR-BPM-02.4). */
export function ProcessMonitorListPage({ onOpenMonitor }: Props) {
    const { accessToken, hasRole } = useAuth();
    const isAdmin = hasRole('Admin');

    const [tab, setTab] = useState<TabId>('my');
    const [myItems, setMyItems] = useState<BpmProcessMonitorItemDto[]>([]);
    const [fullItems, setFullItems] = useState<BpmProcessMonitorItemDto[]>([]);
    const [loadingMy, setLoadingMy] = useState(false);
    const [loadingFull, setLoadingFull] = useState(false);
    const [errorMy, setErrorMy] = useState<string | null>(null);
    const [errorFull, setErrorFull] = useState<string | null>(null);
    const [search, setSearch] = useState('');
    const [dashboard, setDashboard] = useState<BpmDashboardDto | null>(null);

    // ─── Загрузка «Мой монитор» ─────────────────────────────────────────────
    useEffect(() => {
        if (!accessToken) return;
        setLoadingMy(true);
        setErrorMy(null);
        getMyMonitorProcesses(accessToken)
            .then(setMyItems)
            .catch(() => setErrorMy('Ошибка загрузки'))
            .finally(() => setLoadingMy(false));
        // Дашборд загружаем параллельно
        getBpmDashboard(accessToken).then(setDashboard).catch(() => {});
    }, [accessToken]);

    // ─── Загрузка «Полный монитор» ──────────────────────────────────────────
    useEffect(() => {
        if (!accessToken || !isAdmin || tab !== 'full') return;
        if (fullItems.length > 0) return; // уже загружено
        setLoadingFull(true);
        setErrorFull(null);
        getFullMonitorProcesses(accessToken)
            .then(setFullItems)
            .catch(() => setErrorFull('Ошибка загрузки'))
            .finally(() => setLoadingFull(false));
    }, [accessToken, isAdmin, tab, fullItems.length]);

    const items = tab === 'my' ? myItems : fullItems;
    const loading = tab === 'my' ? loadingMy : loadingFull;
    const error = tab === 'my' ? errorMy : errorFull;

    const filtered = search.trim()
        ? items.filter(p =>
            p.processName.toLowerCase().includes(search.toLowerCase()) ||
            (p.owners.some(o => o.toLowerCase().includes(search.toLowerCase()))) ||
            (p.curators.some(c => c.toLowerCase().includes(search.toLowerCase())))
          )
        : items;

    return (
        <div className="pml-root">
            <div className="pml-header">
                <h1 className="pml-title">Монитор процессов</h1>
                {isAdmin && (
                    <div className="pml-tabs">
                        <button
                            className={`pml-tab${tab === 'my' ? ' pml-tab--active' : ''}`}
                            onClick={() => setTab('my')}
                        >
                            Мой монитор
                        </button>
                        <button
                            className={`pml-tab${tab === 'full' ? ' pml-tab--active' : ''}`}
                            onClick={() => setTab('full')}
                        >
                            Полный монитор
                        </button>
                    </div>
                )}
            </div>

            {/* ─── Дашборд ─────────────────────────────────────────────────── */}
            {dashboard && (
                <div className="pml-dashboard">
                    <div className="pml-dash-stat">
                        <span className="pml-dash-value">{dashboard.totalProcesses}</span>
                        <span className="pml-dash-label">Процессов</span>
                    </div>
                    <div className="pml-dash-stat pml-dash-stat--active">
                        <span className="pml-dash-value">{dashboard.activeInstances}</span>
                        <span className="pml-dash-label">Активных</span>
                    </div>
                    <div className="pml-dash-stat pml-dash-stat--suspended">
                        <span className="pml-dash-value">{dashboard.suspendedInstances}</span>
                        <span className="pml-dash-label">На паузе</span>
                    </div>
                    <div className="pml-dash-stat pml-dash-stat--completed">
                        <span className="pml-dash-value">{dashboard.completedInstances}</span>
                        <span className="pml-dash-label">Завершено</span>
                    </div>
                    <div className="pml-dash-stat pml-dash-stat--cancelled">
                        <span className="pml-dash-value">{dashboard.cancelledInstances}</span>
                        <span className="pml-dash-label">Прервано</span>
                    </div>
                    {dashboard.faultedInstances > 0 && (
                        <div className="pml-dash-stat pml-dash-stat--faulted">
                            <span className="pml-dash-value">{dashboard.faultedInstances}</span>
                            <span className="pml-dash-label">С ошибкой</span>
                        </div>
                    )}
                    {dashboard.failedJobs > 0 && (
                        <div className="pml-dash-stat pml-dash-stat--error">
                            <span className="pml-dash-value">{dashboard.failedJobs}</span>
                            <span className="pml-dash-label">Failed-заданий</span>
                        </div>
                    )}
                </div>
            )}

            <div className="pml-toolbar">
                <input
                    className="pml-search"
                    type="text"
                    placeholder="Поиск по названию процесса или владельцу…"
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                />
                <span className="pml-count">{filtered.length} процессов</span>
            </div>

            {loading && <div className="pml-loading">Загрузка…</div>}
            {error && <div className="pml-error">{error}</div>}

            {!loading && !error && filtered.length === 0 && (
                <div className="pml-empty">
                    <div className="pml-empty-icon">📊</div>
                    {tab === 'my'
                        ? <p>Вы не являетесь Владельцем или Куратором ни одного процесса</p>
                        : <p>Процессы не найдены</p>
                    }
                    <p className="pml-empty-hint">Назначьте себя Владельцем процесса на вкладке «Роли» в дизайнере</p>
                </div>
            )}

            {!loading && filtered.length > 0 && (
                <div className="pml-grid">
                    {filtered.map(item => (
                        <ProcessMonitorCard
                            key={item.processId}
                            item={item}
                            onClick={() => onOpenMonitor(item.processId, item.processName)}
                        />
                    ))}
                </div>
            )}
        </div>
    );
}

// ─── Карточка процесса ────────────────────────────────────────────────────────

interface CardProps {
    item: BpmProcessMonitorItemDto;
    onClick: () => void;
}

function ProcessMonitorCard({ item, onClick }: CardProps) {
    const total = item.activeCount + item.suspendedCount + item.completedCount + item.cancelledCount;

    return (
        <div className="pml-card" onClick={onClick} role="button" tabIndex={0}
            onKeyDown={e => e.key === 'Enter' && onClick()}>
            <div className="pml-card-header">
                <div className="pml-card-title">{item.processName}</div>
                {item.activeVersionNumber != null && (
                    <span className="pml-card-version">v{item.activeVersionNumber}</span>
                )}
            </div>

            {item.processDescription && (
                <div className="pml-card-desc">{item.processDescription}</div>
            )}

            {/* Статистика по состояниям */}
            <div className="pml-card-stats">
                <StatBadge label="Активных" value={item.activeCount} cls="stat--active" />
                <StatBadge label="На паузе" value={item.suspendedCount} cls="stat--suspended" />
                <StatBadge label="Завершено" value={item.completedCount} cls="stat--completed" />
                <StatBadge label="Прервано" value={item.cancelledCount} cls="stat--cancelled" />
            </div>

            {/* Прогресс-бар */}
            {total > 0 && (
                <div className="pml-progress" title={`Всего: ${total}`}>
                    {item.activeCount > 0 && (
                        <div
                            className="pml-progress-seg pml-progress-seg--active"
                            style={{ width: `${(item.activeCount / total) * 100}%` }}
                        />
                    )}
                    {item.suspendedCount > 0 && (
                        <div
                            className="pml-progress-seg pml-progress-seg--suspended"
                            style={{ width: `${(item.suspendedCount / total) * 100}%` }}
                        />
                    )}
                    {item.completedCount > 0 && (
                        <div
                            className="pml-progress-seg pml-progress-seg--completed"
                            style={{ width: `${(item.completedCount / total) * 100}%` }}
                        />
                    )}
                    {item.cancelledCount > 0 && (
                        <div
                            className="pml-progress-seg pml-progress-seg--cancelled"
                            style={{ width: `${(item.cancelledCount / total) * 100}%` }}
                        />
                    )}
                </div>
            )}

            {/* Владельцы / Кураторы */}
            {(item.owners.length > 0 || item.curators.length > 0) && (
                <div className="pml-card-roles">
                    {item.owners.length > 0 && (
                        <div className="pml-card-role">
                            <span className="pml-role-label">Владелец:</span>
                            <span>{item.owners.join(', ')}</span>
                        </div>
                    )}
                    {item.curators.length > 0 && (
                        <div className="pml-card-role">
                            <span className="pml-role-label">Кураторы:</span>
                            <span>{item.curators.join(', ')}</span>
                        </div>
                    )}
                </div>
            )}

            <div className="pml-card-footer">
                {item.publishedAt
                    ? <span>Опубликован: {new Date(item.publishedAt).toLocaleDateString('ru-RU')}</span>
                    : <span className="pml-card-draft">Нет активной версии</span>
                }
                <span className="pml-card-arrow">→</span>
            </div>
        </div>
    );
}

interface StatBadgeProps {
    label: string;
    value: number;
    cls: string;
}

function StatBadge({ label, value, cls }: StatBadgeProps) {
    return (
        <div className={`pml-stat ${cls}`}>
            <span className="pml-stat-value">{value}</span>
            <span className="pml-stat-label">{label}</span>
        </div>
    );
}
