import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/analyticsApi';
import './AnalyticsSummaryPage.css';

// ─── Вспомогательные функции ─────────────────────────────────────────────────

function fmtMin(min: number): string {
    if (min === 0) return '—';
    if (min < 1) return `${Math.round(min * 60)} с`;
    if (min < 60) return `${Math.round(min)} мин`;
    const h = Math.floor(min / 60);
    const m = Math.round(min % 60);
    return m > 0 ? `${h} ч ${m} мин` : `${h} ч`;
}

function pctClass(value: number, target: number | undefined, higherIsBetter: boolean): string {
    if (target == null) return 'as-pct-neutral';
    const good = higherIsBetter ? value >= target : value <= target;
    return good ? 'as-pct-good' : value / (higherIsBetter ? target : (target || 1)) < 0.7 ? 'as-pct-bad' : 'as-pct-warn';
}

// ─── Пропсы ──────────────────────────────────────────────────────────────────

interface Props {
    onOpenProcess?: (processId: string, processName: string) => void;
}

// ─── Компонент ───────────────────────────────────────────────────────────────

export function AnalyticsSummaryPage({ onOpenProcess }: Props) {
    const { accessToken: token } = useAuth();
    const [fromDate, setFromDate] = useState('');
    const [toDate, setToDate] = useState('');
    const [items, setItems] = useState<api.ProcessAnalyticsSummaryItemDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.getAnalyticsSummary(token, fromDate || undefined, toDate || undefined);
            setItems(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, fromDate, toDate]);

    useEffect(() => { load(); }, [load]);

    return (
        <div className="as-root">
            <div className="as-header">
                <span className="as-title">📊 Аналитика: сводный отчёт</span>
                <button
                    className="as-export-btn"
                    disabled={items.length === 0}
                    title="Экспортировать таблицу в Excel"
                    onClick={() => api.exportAnalyticsSummary(token!, fromDate || undefined, toDate || undefined)}
                >
                    📥 Экспорт в Excel
                </button>
            </div>

            <div className="as-body">
                <div className="as-period">
                    <label>Период:</label>
                    <input type="date" value={fromDate} onChange={e => setFromDate(e.target.value)} />
                    <span>—</span>
                    <input type="date" value={toDate} onChange={e => setToDate(e.target.value)} />
                </div>

                {loading && <div className="as-loading">Загрузка…</div>}
                {error && <div className="as-error">{error}</div>}
                {!loading && !error && items.length === 0 && (
                    <div className="as-empty">Нет данных за указанный период</div>
                )}
                {!loading && !error && items.length > 0 && (
                    <div className="as-table-wrap">
                        <table className="as-table">
                            <thead>
                                <tr>
                                    <th>Процесс</th>
                                    <th>Экземпляров</th>
                                    <th>Avg цикл</th>
                                    <th>% в срок</th>
                                    <th>% ошибок</th>
                                </tr>
                            </thead>
                            <tbody>
                                {items.map(item => (
                                    <tr
                                        key={item.processId}
                                        className="clickable"
                                        onClick={() => onOpenProcess?.(item.processId, item.processName)}
                                        title="Открыть детальную аналитику"
                                    >
                                        <td>{item.processName}</td>
                                        <td>{item.totalInstances}</td>
                                        <td>{fmtMin(item.avgCycleTimeMinutes)}</td>
                                        <td>
                                            <span className={pctClass(item.onTimePercent, item.targetOnTimePercent, true)}>
                                                {item.onTimePercent}%
                                                {item.targetOnTimePercent != null && (
                                                    <small style={{ fontWeight: 400, marginLeft: 4, color: '#5a6072' }}>
                                                        / {item.targetOnTimePercent}%
                                                    </small>
                                                )}
                                            </span>
                                        </td>
                                        <td>
                                            <span className={pctClass(item.faultedPercent, item.targetOnTimePercent != null ? 100 - item.targetOnTimePercent : undefined, false)}>
                                                {item.faultedPercent}%
                                            </span>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </div>
    );
}
