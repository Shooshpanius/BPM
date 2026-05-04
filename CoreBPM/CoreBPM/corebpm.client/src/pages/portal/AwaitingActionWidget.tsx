import { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getTaskCounters, type TaskCountersDto } from '../../api/tasksApi';

export function AwaitingActionWidget() {
    const { accessToken } = useAuth();
    const [counters, setCounters] = useState<TaskCountersDto | null>(null);

    useEffect(() => {
        if (!accessToken) return;
        getTaskCounters(accessToken).then(setCounters).catch(() => {});
    }, [accessToken]);

    if (!counters) return <div className="widget-loading">Загрузка...</div>;

    const total = counters.incoming + counters.onApproval;
    return (
        <div className="portal-widget__content">
            <div className="widget-awaiting-total">{total}</div>
            <p className="widget-awaiting-label">объектов ожидают вашего действия</p>
            <div className="widget-awaiting-details">
                {counters.incoming > 0 && (
                    <span className="widget-awaiting-chip">Входящие: {counters.incoming}</span>
                )}
                {counters.onApproval > 0 && (
                    <span className="widget-awaiting-chip">На согласовании: {counters.onApproval}</span>
                )}
                {counters.overdue > 0 && (
                    <span className="widget-awaiting-chip widget-awaiting-chip--overdue">Просрочено: {counters.overdue}</span>
                )}
            </div>
        </div>
    );
}
