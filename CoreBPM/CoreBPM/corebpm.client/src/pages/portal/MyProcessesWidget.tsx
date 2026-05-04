import { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getMyInstances, type BpmInstanceListItemDto } from '../../api/bpmApi';

interface Props { onOpenInstance?: (id: string) => void; }

export function MyProcessesWidget({ onOpenInstance }: Props) {
    const { accessToken } = useAuth();
    const [instances, setInstances] = useState<BpmInstanceListItemDto[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        if (!accessToken) return;
        getMyInstances(accessToken, {}, 1, 6)
            .then(r => setInstances(r.items))
            .catch(() => {})
            .finally(() => setLoading(false));
    }, [accessToken]);

    if (loading) return <div className="widget-loading">Загрузка...</div>;

    return (
        <div className="portal-widget__content">
            {instances.length === 0
                ? <p className="widget-empty">Нет запущенных процессов</p>
                : <ul className="widget-process-list">
                    {instances.map(inst => (
                        <li key={inst.id} className="widget-process-item">
                            <button
                                className="widget-task-link"
                                onClick={() => onOpenInstance?.(inst.id)}
                            >
                                <span className="widget-task-title">{inst.processName ?? inst.name}</span>
                                <span className="widget-task-status">{inst.state}</span>
                            </button>
                        </li>
                    ))}
                </ul>
            }
        </div>
    );
}
