import { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { listTasks, type TaskSummaryDto } from '../../api/tasksApi';

interface Props { onOpenTask?: (id: string) => void; }

export function MyTasksWidget({ onOpenTask }: Props) {
    const { accessToken } = useAuth();
    const [tasks, setTasks] = useState<TaskSummaryDto[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        if (!accessToken) return;
        listTasks(accessToken, { page: 1, pageSize: 8, group: 'incoming' })
            .then(r => setTasks(r))
            .catch(() => {})
            .finally(() => setLoading(false));
    }, [accessToken]);

    if (loading) return <div className="widget-loading">Загрузка...</div>;

    return (
        <div className="portal-widget__content">
            {tasks.length === 0
                ? <p className="widget-empty">Нет активных задач</p>
                : <ul className="widget-task-list">
                    {tasks.map(t => (
                        <li key={t.id} className="widget-task-item">
                            <button
                                className="widget-task-link"
                                onClick={() => onOpenTask?.(t.id)}
                            >
                                <span className="widget-task-title">{t.subject}</span>
                                <span className={`widget-task-priority priority-${t.priority.toLowerCase()}`}>
                                    {t.priority}
                                </span>
                            </button>
                        </li>
                    ))}
                </ul>
            }
        </div>
    );
}
