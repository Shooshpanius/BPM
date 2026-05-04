import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getTaskDashboard } from '../../api/tasksApi';
import type { TaskDashboardDto } from '../../api/tasksApi';

const STATUS_LABELS: Record<string, string> = {
    New: 'Новые', InProgress: 'В работе', OnApproval: 'На согласовании',
    DoneNeedsControl: 'Выполн. (ожид. контроль)', Done: 'Выполнено',
    CannotDo: 'Невозможно', Closed: 'Закрыто', Postponed: 'Отложено',
};

const PRIORITY_LABELS: Record<string, string> = {
    Critical: 'Критический', High: 'Высокий', Medium: 'Средний', Low: 'Низкий',
};

const PRIORITY_COLORS: Record<string, string> = {
    Critical: '#d9534f', High: '#e07b39', Medium: '#f0ad4e', Low: '#5bc0de',
};

/** Дашборд задач пользователя (FR-TASK-02.3). */
export function TaskDashboardPage() {
    const { accessToken: token } = useAuth();
    const [data, setData] = useState<TaskDashboardDto | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        if (!token) return;
        setLoading(true);
        getTaskDashboard(token)
            .then(setData)
            .catch(e => setError(e.message ?? 'Ошибка загрузки дашборда'))
            .finally(() => setLoading(false));
    }, [token]);

    if (loading) return <div className="tasks-dashboard__loading">Загрузка…</div>;
    if (error) return <div className="tasks-dashboard__error">{error}</div>;
    if (!data) return null;

    const maxDay = Math.max(1, ...data.dailyStats.map(d => Math.max(d.created, d.closed)));

    return (
        <div className="tasks-dashboard">
            <h2 className="tasks-dashboard__title">Дашборд задач</h2>

            {/* Счётчики */}
            <div className="tasks-dashboard__counters">
                <div className="tasks-dashboard__counter tasks-dashboard__counter--open">
                    <span className="tasks-dashboard__counter-value">{data.openCount}</span>
                    <span className="tasks-dashboard__counter-label">Открытых задач</span>
                </div>
                <div className="tasks-dashboard__counter tasks-dashboard__counter--overdue">
                    <span className="tasks-dashboard__counter-value">{data.overdueCount}</span>
                    <span className="tasks-dashboard__counter-label">Просроченных</span>
                </div>
            </div>

            {/* По статусам */}
            <div className="tasks-dashboard__section">
                <h3 className="tasks-dashboard__section-title">По статусам</h3>
                <div className="tasks-dashboard__bars">
                    {Object.entries(data.byStatus).map(([status, count]) => (
                        <div key={status} className="tasks-dashboard__bar-row">
                            <span className="tasks-dashboard__bar-label">{STATUS_LABELS[status] ?? status}</span>
                            <div className="tasks-dashboard__bar-track">
                                <div
                                    className="tasks-dashboard__bar-fill"
                                    style={{ width: `${Math.round((count / (data.openCount || 1)) * 100)}%`, background: '#4a90d9' }}
                                />
                            </div>
                            <span className="tasks-dashboard__bar-count">{count}</span>
                        </div>
                    ))}
                </div>
            </div>

            {/* По приоритетам */}
            <div className="tasks-dashboard__section">
                <h3 className="tasks-dashboard__section-title">По приоритетам</h3>
                <div className="tasks-dashboard__bars">
                    {Object.entries(data.byPriority).map(([priority, count]) => (
                        <div key={priority} className="tasks-dashboard__bar-row">
                            <span className="tasks-dashboard__bar-label">{PRIORITY_LABELS[priority] ?? priority}</span>
                            <div className="tasks-dashboard__bar-track">
                                <div
                                    className="tasks-dashboard__bar-fill"
                                    style={{ width: `${Math.round((count / (data.openCount || 1)) * 100)}%`, background: PRIORITY_COLORS[priority] ?? '#aaa' }}
                                />
                            </div>
                            <span className="tasks-dashboard__bar-count">{count}</span>
                        </div>
                    ))}
                </div>
            </div>

            {/* Динамика за 30 дней */}
            <div className="tasks-dashboard__section">
                <h3 className="tasks-dashboard__section-title">Динамика создания / закрытия задач (30 дней)</h3>
                <div className="tasks-dashboard__chart">
                    {data.dailyStats.map((d) => (
                        <div key={d.date} className="tasks-dashboard__chart-col" title={`${d.date}\nСоздано: ${d.created}\nЗакрыто: ${d.closed}`}>
                            <div
                                className="tasks-dashboard__chart-bar tasks-dashboard__chart-bar--created"
                                style={{ height: `${Math.round((d.created / maxDay) * 80)}px` }}
                            />
                            <div
                                className="tasks-dashboard__chart-bar tasks-dashboard__chart-bar--closed"
                                style={{ height: `${Math.round((d.closed / maxDay) * 80)}px` }}
                            />
                        </div>
                    ))}
                </div>
                <div className="tasks-dashboard__legend">
                    <span className="tasks-dashboard__legend-item tasks-dashboard__legend-item--created">Создано</span>
                    <span className="tasks-dashboard__legend-item tasks-dashboard__legend-item--closed">Закрыто</span>
                </div>
            </div>
        </div>
    );
}
