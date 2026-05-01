import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getNodeAnalytics, type NodeAnalyticsDto } from '../../api/bpmApi';
import './NodeAnalyticsPanel.css';

interface Props {
    processId: string;
}

function fmtMs(ms: number) {
    if (ms < 1) return '< 1 мс';
    if (ms < 1000) return `${ms.toFixed(0)} мс`;
    return `${(ms / 1000).toFixed(2)} с`;
}

/** Панель аналитики выполнения узлов процесса (FR-BPM-02.5). */
export function NodeAnalyticsPanel({ processId }: Props) {
    const { accessToken } = useAuth();

    const [data, setData] = useState<NodeAnalyticsDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [period, setPeriod] = useState<'7d' | '30d' | '90d'>('30d');

    useEffect(() => {
        if (!accessToken) return;
        setLoading(true);
        setError(null);
        const now = new Date();
        const days = period === '7d' ? 7 : period === '30d' ? 30 : 90;
        const from = new Date(now.getTime() - days * 86400_000).toISOString();
        getNodeAnalytics(accessToken, processId, from)
            .then(setData)
            .catch(() => setError('Ошибка загрузки аналитики'))
            .finally(() => setLoading(false));
    }, [accessToken, processId, period]);

    if (loading) return <div className="nap-loading">Загрузка аналитики…</div>;
    if (error) return <div className="nap-error">{error}</div>;
    if (data.length === 0)
        return (
            <div className="nap-empty">
                <p>📊 Нет данных о выполнении узлов за выбранный период</p>
                <p className="nap-hint">Аналитика накапливается по мере выполнения экземпляров</p>
            </div>
        );

    return (
        <div className="nap-root">
            <div className="nap-toolbar">
                <span className="nap-title">Производительность узлов</span>
                <div className="nap-period-btns">
                    {(['7d', '30d', '90d'] as const).map(p => (
                        <button
                            key={p}
                            className={`nap-period-btn${period === p ? ' nap-period-btn--active' : ''}`}
                            onClick={() => setPeriod(p)}
                        >
                            {p === '7d' ? '7 дней' : p === '30d' ? '30 дней' : '90 дней'}
                        </button>
                    ))}
                </div>
            </div>

            <div className="nap-table-wrap">
                <table className="nap-table">
                    <thead>
                        <tr>
                            <th>Узел</th>
                            <th>Тип</th>
                            <th>Выполнений</th>
                            <th>Avg</th>
                            <th>P50</th>
                            <th>P95</th>
                            <th>Ошибок</th>
                        </tr>
                    </thead>
                    <tbody>
                        {data.map(n => (
                            <tr key={n.elementId}>
                                <td>
                                    <div className="nap-cell-name">{n.elementName ?? n.elementId}</div>
                                    <div className="nap-cell-id">{n.elementId}</div>
                                </td>
                                <td>{n.elementId.includes('Service') ? 'Service' : n.elementId.includes('Script') ? 'Script' : '—'}</td>
                                <td className="nap-cell-num">{n.executionCount}</td>
                                <td className="nap-cell-num">{fmtMs(n.avgDurationMs)}</td>
                                <td className="nap-cell-num">{fmtMs(n.p50DurationMs)}</td>
                                <td className="nap-cell-num nap-p95">{fmtMs(n.p95DurationMs)}</td>
                                <td className="nap-cell-num">
                                    {n.errorCount > 0
                                        ? <span className="nap-errors">{n.errorCount}</span>
                                        : <span className="nap-ok">0</span>
                                    }
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
