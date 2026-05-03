import { useState, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getTimelogsReport,
    getTimelogsReportExportUrl,
} from '../../api/tasksApi';
import type { TimelogReportItemDto, TimelogReportFilter } from '../../api/tasksApi';

/** Форматирует длительность в минутах в строку ЧЧ:ММ. */
function formatMinutes(minutes: number): string {
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return h > 0 ? `${h} ч ${m} мин` : `${m} мин`;
}

/** Отчёт по трудозатратам задач (FR-TASK-01.4). */
export default function TimelogsReportPage() {
    const { accessToken: token } = useAuth();

    const [items, setItems] = useState<TimelogReportItemDto[]>([]);
    const [totalCount, setTotalCount] = useState(0);
    const [totalMinutes, setTotalMinutes] = useState(0);
    const [page, setPage] = useState(1);
    const perPage = 50;

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [searched, setSearched] = useState(false);

    // Фильтры
    const [dateFrom, setDateFrom] = useState('');
    const [dateTo, setDateTo] = useState('');

    const buildFilter = useCallback((p: number): TimelogReportFilter => ({
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
        page: p,
        perPage,
    }), [dateFrom, dateTo]);

    const load = useCallback(async (p: number) => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const result = await getTimelogsReport(token, buildFilter(p));
            setItems(result.items);
            setTotalCount(result.totalCount);
            setTotalMinutes(result.totalMinutes);
            setPage(p);
            setSearched(true);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, buildFilter]);

    const handleSearch = () => load(1);

    const totalPages = Math.ceil(totalCount / perPage);

    const exportUrl = getTimelogsReportExportUrl(buildFilter(1));

    return (
        <div style={{ padding: 24 }}>
            <h2>Отчёт по трудозатратам</h2>

            {/* Фильтры */}
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 16, alignItems: 'flex-end' }}>
                <div>
                    <label style={{ display: 'block', fontSize: 12, marginBottom: 4 }}>Дата с</label>
                    <input
                        type="date"
                        value={dateFrom}
                        onChange={e => setDateFrom(e.target.value)}
                        style={{ padding: '6px 10px', border: '1px solid #ccc', borderRadius: 4 }}
                    />
                </div>
                <div>
                    <label style={{ display: 'block', fontSize: 12, marginBottom: 4 }}>Дата по</label>
                    <input
                        type="date"
                        value={dateTo}
                        onChange={e => setDateTo(e.target.value)}
                        style={{ padding: '6px 10px', border: '1px solid #ccc', borderRadius: 4 }}
                    />
                </div>
                <button
                    onClick={handleSearch}
                    disabled={loading}
                    style={{
                        padding: '8px 20px', background: '#1890ff', color: '#fff',
                        border: 'none', borderRadius: 4, cursor: 'pointer',
                    }}
                >
                    {loading ? 'Загрузка...' : 'Показать'}
                </button>
                {searched && (
                    <a
                        href={exportUrl}
                        download="timelogs-report.csv"
                        style={{
                            padding: '8px 20px', background: '#52c41a', color: '#fff',
                            borderRadius: 4, textDecoration: 'none', fontSize: 14,
                        }}
                    >
                        Экспорт CSV
                    </a>
                )}
            </div>

            {error && <div style={{ color: 'red', marginBottom: 12 }}>{error}</div>}

            {searched && (
                <>
                    {/* Итоги */}
                    <div style={{ marginBottom: 12, fontSize: 14, color: '#555' }}>
                        Всего записей: <strong>{totalCount}</strong> &nbsp;|&nbsp;
                        Суммарно: <strong>{formatMinutes(totalMinutes)}</strong>
                    </div>

                    {/* Таблица */}
                    <div style={{ overflowX: 'auto' }}>
                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 14 }}>
                            <thead>
                                <tr style={{ background: '#f5f5f5' }}>
                                    <th style={thStyle}>Задача</th>
                                    <th style={thStyle}>Пользователь</th>
                                    <th style={thStyle}>Вид деятельности</th>
                                    <th style={thStyle}>Длительность</th>
                                    <th style={thStyle}>Дата начала</th>
                                    <th style={thStyle}>Комментарий</th>
                                </tr>
                            </thead>
                            <tbody>
                                {items.length === 0 && (
                                    <tr>
                                        <td colSpan={6} style={{ textAlign: 'center', padding: 24, color: '#aaa' }}>
                                            Нет записей за выбранный период
                                        </td>
                                    </tr>
                                )}
                                {items.map(item => (
                                    <tr key={item.id} style={{ borderBottom: '1px solid #f0f0f0' }}>
                                        <td style={tdStyle}>
                                            <span style={{ fontWeight: 500 }}>T-{item.taskNumber}</span>
                                            {' '}{item.taskSubject}
                                        </td>
                                        <td style={tdStyle}>{item.userName}</td>
                                        <td style={tdStyle}>{item.activityTypeName ?? '—'}</td>
                                        <td style={tdStyle}>{formatMinutes(item.durationMinutes)}</td>
                                        <td style={tdStyle}>{new Date(item.startDate).toLocaleDateString('ru-RU')}</td>
                                        <td style={tdStyle}>{item.comment ?? ''}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    {/* Пагинация */}
                    {totalPages > 1 && (
                        <div style={{ marginTop: 16, display: 'flex', gap: 8 }}>
                            <button
                                disabled={page <= 1 || loading}
                                onClick={() => load(page - 1)}
                                style={pageButtonStyle}
                            >
                                ←
                            </button>
                            <span style={{ lineHeight: '32px', fontSize: 14 }}>
                                Страница {page} / {totalPages}
                            </span>
                            <button
                                disabled={page >= totalPages || loading}
                                onClick={() => load(page + 1)}
                                style={pageButtonStyle}
                            >
                                →
                            </button>
                        </div>
                    )}
                </>
            )}
        </div>
    );
}

const thStyle: React.CSSProperties = {
    padding: '8px 12px',
    textAlign: 'left',
    borderBottom: '2px solid #e8e8e8',
    whiteSpace: 'nowrap',
};

const tdStyle: React.CSSProperties = {
    padding: '8px 12px',
    verticalAlign: 'top',
};

const pageButtonStyle: React.CSSProperties = {
    padding: '4px 12px',
    border: '1px solid #d9d9d9',
    borderRadius: 4,
    background: '#fff',
    cursor: 'pointer',
};
