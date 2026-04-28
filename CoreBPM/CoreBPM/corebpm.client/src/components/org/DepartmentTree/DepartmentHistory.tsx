import { useEffect, useState } from 'react';
import type { UnitHistoryDto } from '../../../api/unitsApi';
import { getUnitHistory, CHANGE_TYPE_LABELS } from '../../../api/unitsApi';
import './DepartmentHistory.css';

interface DepartmentHistoryProps {
    unitId: string;
    unitName: string;
    token: string;
    onClose: () => void;
}

function formatDate(iso: string): string {
    try {
        return new Date(iso).toLocaleString('ru-RU', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
        });
    } catch {
        return iso;
    }
}

/** Панель истории изменений подразделения (drawer). */
export function DepartmentHistory({ unitId, unitName, token, onClose }: DepartmentHistoryProps) {
    const [items, setItems] = useState<UnitHistoryDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        setLoading(true);
        setError(null);
        getUnitHistory(token, unitId)
            .then(setItems)
            .catch(e => setError(e.message ?? 'Ошибка загрузки'))
            .finally(() => setLoading(false));
    }, [token, unitId]);

    useEffect(() => {
        const onKeyDown = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKeyDown);
        return () => document.removeEventListener('keydown', onKeyDown);
    }, [onClose]);

    return (
        <div className="hist-overlay" onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}>
            <div className="hist-panel" role="dialog" aria-modal="true" aria-label="История изменений">
                <div className="hist-header">
                    <div>
                        <div className="hist-title">История изменений</div>
                        <div className="hist-subtitle">{unitName}</div>
                    </div>
                    <button className="hist-close" onClick={onClose} aria-label="Закрыть">×</button>
                </div>

                <div className="hist-body">
                    {loading && <div className="hist-status">Загрузка…</div>}
                    {!loading && error && <div className="hist-status hist-status--error">{error}</div>}
                    {!loading && !error && items.length === 0 && (
                        <div className="hist-status">История пуста</div>
                    )}
                    {!loading && !error && items.length > 0 && (
                        <ul className="hist-list">
                            {items.map(item => (
                                <li key={item.id} className="hist-item">
                                    <div className="hist-item-header">
                                        <span className={`hist-badge hist-badge--${item.changeType}`}>
                                            {CHANGE_TYPE_LABELS[item.changeType]}
                                        </span>
                                        <span className="hist-date">{formatDate(item.changedAt)}</span>
                                    </div>
                                    {item.changedByUserName && (
                                        <div className="hist-author">{item.changedByUserName}</div>
                                    )}
                                    {(item.oldValue || item.newValue) && (
                                        <details className="hist-details">
                                            <summary className="hist-details-summary">Подробности</summary>
                                            <div className="hist-values">
                                                {item.oldValue && (
                                                    <div className="hist-value hist-value--old">
                                                        <span className="hist-value-label">Было:</span>
                                                        <code className="hist-value-code">{item.oldValue}</code>
                                                    </div>
                                                )}
                                                {item.newValue && (
                                                    <div className="hist-value hist-value--new">
                                                        <span className="hist-value-label">Стало:</span>
                                                        <code className="hist-value-code">{item.newValue}</code>
                                                    </div>
                                                )}
                                            </div>
                                        </details>
                                    )}
                                </li>
                            ))}
                        </ul>
                    )}
                </div>
            </div>
        </div>
    );
}
