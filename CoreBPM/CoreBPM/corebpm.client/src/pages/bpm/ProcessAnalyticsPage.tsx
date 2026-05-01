import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/analyticsApi';
import * as bpmApi from '../../api/bpmApi';
import './ProcessAnalyticsPage.css';

// ─── Вспомогательные функции ─────────────────────────────────────────────────

function fmtMin(min: number): string {
    if (min < 1) return `${Math.round(min * 60)} с`;
    if (min < 60) return `${Math.round(min)} мин`;
    const h = Math.floor(min / 60);
    const m = Math.round(min % 60);
    return m > 0 ? `${h} ч ${m} мин` : `${h} ч`;
}

function fmtMs(ms: number): string {
    if (ms < 1000) return `${Math.round(ms)} мс`;
    return `${(ms / 1000).toFixed(1)} с`;
}

type KpiColor = 'good' | 'bad' | 'neutral';
function kpiColor(value: number, target: number | undefined, higherIsBetter: boolean): KpiColor {
    if (target == null) return 'neutral';
    return higherIsBetter ? (value >= target ? 'good' : 'bad') : (value <= target ? 'good' : 'bad');
}

// ─── Пропсы ──────────────────────────────────────────────────────────────────

interface Props {
    processId: string;
    processName: string;
    onBack: () => void;
}

type Tab = 'overview' | 'heatmap' | 'funnel' | 'compare' | 'trend';

// ─── Компонент ───────────────────────────────────────────────────────────────

export function ProcessAnalyticsPage({ processId, processName, onBack }: Props) {
    const { accessToken: token } = useAuth();
    const [tab, setTab] = useState<Tab>('overview');

    // Период
    const [fromDate, setFromDate] = useState('');
    const [toDate, setToDate] = useState('');

    // Версии
    const [versions, setVersions] = useState<bpmApi.BpmProcessVersionInfoDto[]>([]);
    const [heatmapVersionId, setHeatmapVersionId] = useState('');
    const [funnelVersionId, setFunnelVersionId] = useState('');
    const [compareVAId, setCompareVAId] = useState('');
    const [compareVBId, setCompareVBId] = useState('');

    // Данные
    const [analytics, setAnalytics] = useState<api.ProcessAnalyticsDto | null>(null);
    const [heatmap, setHeatmap] = useState<api.NodeHeatMapDto[]>([]);
    const [funnel, setFunnel] = useState<api.ProcessFunnelStepDto[]>([]);
    const [comparison, setComparison] = useState<api.ProcessVersionComparisonDto | null>(null);
    const [trend, setTrend] = useState<api.ProcessTrendPointDto[]>([]);
    const [trendGranularity, setTrendGranularity] = useState<'day' | 'week' | 'month'>('week');

    // Состояние загрузки
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Загрузить версии при монтировании
    useEffect(() => {
        if (!token) return;
        bpmApi.getProcessVersions(token, processId)
            .then(vs => {
                const published = vs.filter(v => v.status === 'Published');
                setVersions(published);
                if (published.length >= 2) {
                    setCompareVAId(published[published.length - 2].id);
                    setCompareVBId(published[published.length - 1].id);
                } else if (published.length === 1) {
                    setCompareVAId(published[0].id);
                    setCompareVBId(published[0].id);
                }
            })
            .catch(() => {});
    }, [token, processId]);

    // ─── Загрузка «Обзор» ────────────────────────────────────────────────────

    const loadOverview = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.getProcessAnalytics(token, processId, fromDate || undefined, toDate || undefined);
            setAnalytics(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId, fromDate, toDate]);

    useEffect(() => {
        if (tab === 'overview') loadOverview();
    }, [tab, loadOverview]);

    // ─── Загрузка «Тепловая карта» ───────────────────────────────────────────

    const loadHeatmap = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.getNodeHeatMap(
                token, processId,
                heatmapVersionId || undefined,
                fromDate || undefined,
                toDate || undefined,
            );
            setHeatmap(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId, heatmapVersionId, fromDate, toDate]);

    useEffect(() => {
        if (tab === 'heatmap') loadHeatmap();
    }, [tab, loadHeatmap]);

    // ─── Загрузка «Воронка» ──────────────────────────────────────────────────

    const loadFunnel = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.getProcessFunnel(
                token, processId,
                funnelVersionId || undefined,
                fromDate || undefined,
                toDate || undefined,
            );
            setFunnel(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId, funnelVersionId, fromDate, toDate]);

    useEffect(() => {
        if (tab === 'funnel') loadFunnel();
    }, [tab, loadFunnel]);

    // ─── Загрузка «Сравнение версий» ─────────────────────────────────────────

    const loadComparison = useCallback(async () => {
        if (!token || !compareVAId || !compareVBId) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.getVersionComparison(
                token, processId, compareVAId, compareVBId,
                fromDate || undefined,
                toDate || undefined,
            );
            setComparison(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId, compareVAId, compareVBId, fromDate, toDate]);

    useEffect(() => {
        if (tab === 'compare') loadComparison();
    }, [tab, loadComparison]);

    // ─── Загрузка «Тренд» ────────────────────────────────────────────────────

    const loadTrend = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.getProcessTrend(token, processId, trendGranularity, fromDate || undefined, toDate || undefined);
            setTrend(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId, trendGranularity, fromDate, toDate]);

    useEffect(() => {
        if (tab === 'trend') loadTrend();
    }, [tab, loadTrend]);

    // ─── Render ──────────────────────────────────────────────────────────────

    const periodBar = (
        <div className="pa-period">
            <label>Период:</label>
            <input type="date" value={fromDate} onChange={e => setFromDate(e.target.value)} />
            <span>—</span>
            <input type="date" value={toDate} onChange={e => setToDate(e.target.value)} />
        </div>
    );

    return (
        <div className="pa-root">
            <div className="pa-header">
                <button className="pa-back-btn" onClick={onBack}>← Назад</button>
                <span className="pa-title">Аналитика процесса: {processName}</span>
            </div>

            <div className="pa-tabs">
                {(['overview', 'heatmap', 'funnel', 'compare', 'trend'] as Tab[]).map(t => (
                    <button
                        key={t}
                        className={`pa-tab${tab === t ? ' active' : ''}`}
                        onClick={() => setTab(t)}
                    >
                        {TAB_LABELS[t]}
                    </button>
                ))}
            </div>

            <div className="pa-body">
                {tab === 'overview' && renderOverview()}
                {tab === 'heatmap' && renderHeatmap()}
                {tab === 'funnel' && renderFunnel()}
                {tab === 'compare' && renderCompare()}
                {tab === 'trend' && renderTrend()}
            </div>
        </div>
    );

    // ─── Вкладка «Обзор» ─────────────────────────────────────────────────────

    function renderOverview() {
        return (
            <>
                {periodBar}
                {loading && <div className="pa-loading">Загрузка…</div>}
                {error && <div className="pa-error">{error}</div>}
                {!loading && !error && analytics && (
                    <>
                        <div className="pa-kpi-grid">
                            <KpiCard label="Всего экземпляров" value={String(analytics.totalInstances)} cls="neutral" />
                            <KpiCard label="Завершено" value={String(analytics.completedInstances)} cls="neutral" />
                            <KpiCard label="Ошибок" value={String(analytics.faultedInstances)} cls={analytics.faultedInstances > 0 ? 'bad' : 'good'} />
                            <KpiCard
                                label="% в срок"
                                value={`${analytics.onTimePercent}%`}
                                cls={kpiColor(analytics.onTimePercent, analytics.targetOnTimePercent, true)}
                                target={analytics.targetOnTimePercent != null ? `Цель: ${analytics.targetOnTimePercent}%` : undefined}
                            />
                            <KpiCard
                                label="Среднее время цикла"
                                value={fmtMin(analytics.avgCycleTimeMinutes)}
                                cls={kpiColor(analytics.avgCycleTimeMinutes, analytics.targetCycleTimeMinutes, false)}
                                target={analytics.targetCycleTimeMinutes != null ? `Цель: ${fmtMin(analytics.targetCycleTimeMinutes)}` : undefined}
                            />
                            <KpiCard label="Медиана" value={fmtMin(analytics.medianCycleTimeMinutes)} cls="neutral" />
                            <KpiCard label="P95" value={fmtMin(analytics.p95CycleTimeMinutes)} cls="neutral" />
                            <KpiCard label="% ошибок" value={`${analytics.faultedPercent}%`} cls={analytics.faultedPercent > 10 ? 'bad' : 'good'} />
                        </div>

                        {analytics.cycleTimeHistogram.length > 0 && (
                            <CycleTimeHistogram buckets={analytics.cycleTimeHistogram} />
                        )}
                    </>
                )}
            </>
        );
    }

    // ─── Вкладка «Тепловая карта» ────────────────────────────────────────────

    function renderHeatmap() {
        return (
            <>
                <div className="pa-period">
                    <label>Версия:</label>
                    <select value={heatmapVersionId} onChange={e => setHeatmapVersionId(e.target.value)}>
                        <option value="">Все версии</option>
                        {versions.map(v => (
                            <option key={v.id} value={v.id}>v{v.versionNumber}</option>
                        ))}
                    </select>
                    <label>Период:</label>
                    <input type="date" value={fromDate} onChange={e => setFromDate(e.target.value)} />
                    <span>—</span>
                    <input type="date" value={toDate} onChange={e => setToDate(e.target.value)} />
                </div>
                {loading && <div className="pa-loading">Загрузка…</div>}
                {error && <div className="pa-error">{error}</div>}
                {!loading && !error && heatmap.length === 0 && <div className="pa-empty">Нет данных о выполнении узлов в указанном периоде</div>}
                {!loading && !error && heatmap.length > 0 && (
                    <div className="pa-table">
                        <table>
                            <thead>
                                <tr>
                                    <th>Узел</th>
                                    <th>Среднее время</th>
                                    <th>Прохождений</th>
                                    <th style={{ minWidth: 160 }}>Нагрев</th>
                                </tr>
                            </thead>
                            <tbody>
                                {heatmap.map(n => (
                                    <tr key={n.elementId}>
                                        <td>{n.elementName}</td>
                                        <td>{fmtMs(n.avgDurationMs)}</td>
                                        <td>{n.passCount}</td>
                                        <td>
                                            <div className="pa-heat-cell">
                                                <div
                                                    className="pa-heat-bar"
                                                    style={{ width: `${Math.round(n.heatLevel * 140)}px` }}
                                                />
                                                <span style={{ fontSize: '0.75rem', color: '#5a6072' }}>
                                                    {Math.round(n.heatLevel * 100)}%
                                                </span>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </>
        );
    }

    // ─── Вкладка «Воронка» ───────────────────────────────────────────────────

    function renderFunnel() {
        return (
            <>
                <div className="pa-period">
                    <label>Версия:</label>
                    <select value={funnelVersionId} onChange={e => setFunnelVersionId(e.target.value)}>
                        <option value="">Все версии</option>
                        {versions.map(v => (
                            <option key={v.id} value={v.id}>v{v.versionNumber}</option>
                        ))}
                    </select>
                    <label>Период:</label>
                    <input type="date" value={fromDate} onChange={e => setFromDate(e.target.value)} />
                    <span>—</span>
                    <input type="date" value={toDate} onChange={e => setToDate(e.target.value)} />
                </div>
                {loading && <div className="pa-loading">Загрузка…</div>}
                {error && <div className="pa-error">{error}</div>}
                {!loading && !error && funnel.length === 0 && <div className="pa-empty">Нет данных о прохождении узлов</div>}
                {!loading && !error && funnel.length > 0 && (
                    <div className="pa-funnel">
                        {funnel.map(step => (
                            <div key={step.elementId} className="pa-funnel-step">
                                <span className="pa-funnel-name">{step.elementName}</span>
                                <span className="pa-funnel-reached">↓ {step.reachedCount} достигли</span>
                                <span className="pa-funnel-passed">✓ {step.passedCount} прошли</span>
                                <span className={`pa-funnel-dropoff ${step.dropOffPercent >= 30 ? 'high' : step.dropOffPercent >= 10 ? 'mid' : 'low'}`}>
                                    -{step.dropOffPercent}%
                                </span>
                            </div>
                        ))}
                    </div>
                )}
            </>
        );
    }

    // ─── Вкладка «Сравнение версий» ──────────────────────────────────────────

    function renderCompare() {
        return (
            <>
                <div className="pa-compare-selectors">
                    <label>Версия A:</label>
                    <select value={compareVAId} onChange={e => setCompareVAId(e.target.value)}>
                        {versions.map(v => (
                            <option key={v.id} value={v.id}>v{v.versionNumber}</option>
                        ))}
                    </select>
                    <label>Версия B:</label>
                    <select value={compareVBId} onChange={e => setCompareVBId(e.target.value)}>
                        {versions.map(v => (
                            <option key={v.id} value={v.id}>v{v.versionNumber}</option>
                        ))}
                    </select>
                    {periodBar}
                </div>
                {loading && <div className="pa-loading">Загрузка…</div>}
                {error && <div className="pa-error">{error}</div>}
                {!loading && !error && !comparison && versions.length < 2 && (
                    <div className="pa-empty">Для сравнения необходимо минимум 2 опубликованных версии</div>
                )}
                {!loading && !error && comparison && (
                    <div className="pa-compare-table">
                        <table>
                            <thead>
                                <tr>
                                    <th>Метрика</th>
                                    <th>Версия {comparison.versionANumber}</th>
                                    <th>Версия {comparison.versionBNumber}</th>
                                    <th>Δ</th>
                                </tr>
                            </thead>
                            <tbody>
                                {buildCompareRows(comparison).map(row => (
                                    <tr key={row.label}>
                                        <td>{row.label}</td>
                                        <td>{row.a}</td>
                                        <td>{row.b}</td>
                                        <td>
                                            <span className={row.deltaClass}>{row.delta}</span>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </>
        );
    }

    // ─── Вкладка «Тренд» ─────────────────────────────────────────────────────

    function renderTrend() {
        const maxAvg = trend.length > 0 ? Math.max(...trend.map(p => p.avgCycleTimeMinutes), 1) : 1;
        const maxInstances = trend.length > 0 ? Math.max(...trend.map(p => p.totalInstances), 1) : 1;

        return (
            <>
                <div className="pa-period">
                    <label>Гранулярность:</label>
                    <select
                        value={trendGranularity}
                        onChange={e => setTrendGranularity(e.target.value as 'day' | 'week' | 'month')}
                    >
                        <option value="day">День</option>
                        <option value="week">Неделя</option>
                        <option value="month">Месяц</option>
                    </select>
                    <label>Период:</label>
                    <input type="date" value={fromDate} onChange={e => setFromDate(e.target.value)} />
                    <span>—</span>
                    <input type="date" value={toDate} onChange={e => setToDate(e.target.value)} />
                    <button className="pa-btn-load" onClick={loadTrend}>Обновить</button>
                </div>
                {loading && <div className="pa-loading">Загрузка…</div>}
                {error && <div className="pa-error">{error}</div>}
                {!loading && !error && trend.length === 0 && (
                    <div className="pa-empty">Нет данных за выбранный период</div>
                )}
                {!loading && !error && trend.length > 0 && (
                    <>
                        {/* SVG-линейный график avg cycle time */}
                        <div className="pa-trend-title">Среднее время цикла (мин)</div>
                        <TrendLineChart
                            points={trend.map(p => ({
                                label: p.periodStart.slice(0, 10),
                                value: p.avgCycleTimeMinutes,
                                maxValue: maxAvg,
                            }))}
                            color="#3b7dd8"
                        />

                        <div className="pa-trend-title" style={{ marginTop: '1.5rem' }}>% выполнено в срок</div>
                        <TrendLineChart
                            points={trend.map(p => ({
                                label: p.periodStart.slice(0, 10),
                                value: p.onTimePercent,
                                maxValue: 100,
                            }))}
                            color="#22b573"
                        />

                        {/* Таблица */}
                        <div className="pa-table" style={{ marginTop: '1.5rem' }}>
                            <table>
                                <thead>
                                    <tr>
                                        <th>Период</th>
                                        <th>Экземпляров</th>
                                        <th>Avg цикл (мин)</th>
                                        <th>% в срок</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {trend.map((p, i) => (
                                        <tr key={i}>
                                            <td>{p.periodStart.slice(0, 10)}</td>
                                            <td>{p.totalInstances}</td>
                                            <td>{fmtMin(p.avgCycleTimeMinutes)}</td>
                                            <td>
                                                <span style={{
                                                    color: p.onTimePercent >= 80 ? '#22b573' : p.onTimePercent >= 50 ? '#f59e0b' : '#ef4444',
                                                    fontWeight: 600,
                                                }}>
                                                    {p.onTimePercent}%
                                                </span>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>

                        {/* Мини-гистограмма экземпляров */}
                        <div className="pa-trend-title" style={{ marginTop: '1.5rem' }}>Количество экземпляров по периодам</div>
                        <div className="pa-trend-bars">
                            {trend.map((p, i) => (
                                <div key={i} className="pa-trend-bar-col" title={`${p.periodStart.slice(0, 10)}: ${p.totalInstances} экз.`}>
                                    <div
                                        className="pa-trend-bar"
                                        style={{ height: `${Math.round((p.totalInstances / maxInstances) * 80)}px` }}
                                    />
                                    <div className="pa-trend-bar-label">{p.totalInstances}</div>
                                </div>
                            ))}
                        </div>
                    </>
                )}
            </>
        );
    }
}

// ─── Вспомогательные компоненты ──────────────────────────────────────────────

interface KpiCardProps {
    label: string;
    value: string;
    cls: KpiColor;
    target?: string;
}

function KpiCard({ label, value, cls, target }: KpiCardProps) {
    return (
        <div className={`pa-kpi-card ${cls}`}>
            <div className="pa-kpi-label">{label}</div>
            <div className="pa-kpi-value">{value}</div>
            {target && <div className="pa-kpi-target">{target}</div>}
        </div>
    );
}

function CycleTimeHistogram({ buckets }: { buckets: api.CycleTimeHistogramBucketDto[] }) {
    const maxCount = Math.max(...buckets.map(b => b.count), 1);
    return (
        <div className="pa-histogram">
            <div className="pa-histogram-title">Распределение времени цикла</div>
            <div className="pa-histogram-bars">
                {buckets.map((b, i) => (
                    <div key={i} className="pa-histogram-col" title={`${b.fromMinutes}–${b.toMinutes} мин: ${b.count} экз.`}>
                        <div
                            className="pa-histogram-bar"
                            style={{ height: `${Math.round((b.count / maxCount) * 96)}px` }}
                        />
                        <div className="pa-histogram-tick">{b.fromMinutes}</div>
                    </div>
                ))}
            </div>
        </div>
    );
}

const TAB_LABELS: Record<string, string> = {
    overview: 'Обзор',
    heatmap: 'Тепловая карта',
    funnel: 'Воронка',
    compare: 'Сравнение версий',
    trend: 'Тренд',
};

// ─── SVG линейный график тренда ──────────────────────────────────────────────

interface TrendPoint {
    label: string;
    value: number;
    maxValue: number;
}

function TrendLineChart({ points, color }: { points: TrendPoint[]; color: string }) {
    if (points.length === 0) return null;

    const W = 700;
    const H = 120;
    const PAD_X = 40;
    const PAD_Y = 16;
    const chartW = W - PAD_X * 2;
    const chartH = H - PAD_Y * 2;

    const maxVal = Math.max(...points.map(p => p.value), points[0].maxValue * 0.01, 1);

    const cx = (i: number) => PAD_X + (i / Math.max(points.length - 1, 1)) * chartW;
    const cy = (v: number) => PAD_Y + chartH - (v / maxVal) * chartH;

    const pathD = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${cx(i).toFixed(1)} ${cy(p.value).toFixed(1)}`).join(' ');

    return (
        <svg viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', maxWidth: W, height: H, display: 'block', overflow: 'visible' }}>
            {/* Горизонтальные сетки */}
            {[0, 0.25, 0.5, 0.75, 1].map(f => (
                <line key={f} x1={PAD_X} x2={W - PAD_X}
                    y1={PAD_Y + chartH * (1 - f)} y2={PAD_Y + chartH * (1 - f)}
                    stroke="#e2e8f0" strokeWidth="1" />
            ))}
            {/* Линия */}
            <path d={pathD} fill="none" stroke={color} strokeWidth="2.5" strokeLinejoin="round" strokeLinecap="round" />
            {/* Точки */}
            {points.map((p, i) => (
                <circle key={i} cx={cx(i)} cy={cy(p.value)} r={4} fill={color}>
                    <title>{`${p.label}: ${p.value}`}</title>
                </circle>
            ))}
            {/* Подписи X */}
            {points.filter((_, i) => points.length <= 10 || i % Math.ceil(points.length / 10) === 0).map((p, _, arr) => {
                const origIdx = points.indexOf(p);
                return (
                    <text key={origIdx} x={cx(origIdx)} y={H - 2}
                        textAnchor="middle" fontSize="10" fill="#8896a5">
                        {p.label.slice(5)} {/* MM-DD */}
                    </text>
                );
            })}
            {/* Подпись Y max */}
            <text x={PAD_X - 4} y={PAD_Y + 4} textAnchor="end" fontSize="10" fill="#8896a5">
                {Math.round(maxVal)}
            </text>
        </svg>
    );
}

interface CompareRow {
    label: string;
    a: string;
    b: string;
    delta: string;
    deltaClass: string;
}

function buildCompareRows(c: api.ProcessVersionComparisonDto): CompareRow[] {
    const a = c.versionAAnalytics;
    const b = c.versionBAnalytics;

    const row = (
        label: string,
        aVal: number,
        bVal: number,
        fmt: (v: number) => string,
        higherIsBetter: boolean,
    ): CompareRow => {
        const diff = bVal - aVal;
        const better = higherIsBetter ? diff > 0 : diff < 0;
        const sign   = diff > 0 ? '+' : '';
        return {
            label,
            a: fmt(aVal),
            b: fmt(bVal),
            delta: diff === 0 ? '=' : `${sign}${fmt(diff)}`,
            deltaClass: diff === 0 ? 'pa-delta-neutral' : better ? 'pa-delta-positive' : 'pa-delta-negative',
        };
    };

    const fmtMin = (m: number) => `${Math.round(m)} мин`;
    const fmtPct = (v: number) => `${Math.round(v)}%`;
    const fmtInt = (v: number) => String(Math.round(v));

    return [
        row('Всего экземпляров',   a.totalInstances,       b.totalInstances,       fmtInt, true),
        row('Завершено',           a.completedInstances,   b.completedInstances,   fmtInt, true),
        row('Ошибок',              a.faultedInstances,     b.faultedInstances,     fmtInt, false),
        row('% в срок',            a.onTimePercent,        b.onTimePercent,        fmtPct, true),
        row('% ошибок',            a.faultedPercent,       b.faultedPercent,       fmtPct, false),
        row('Среднее время цикла', a.avgCycleTimeMinutes,  b.avgCycleTimeMinutes,  fmtMin, false),
        row('Медиана',             a.medianCycleTimeMinutes, b.medianCycleTimeMinutes, fmtMin, false),
        row('P95',                 a.p95CycleTimeMinutes,  b.p95CycleTimeMinutes,  fmtMin, false),
    ];
}
