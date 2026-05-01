import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/improvementsApi';
import type {
    ImprovementDto,
    ImprovementListRole,
    BpmImprovementStatus,
    ImprovementMonitorItemDto,
} from '../../api/improvementsApi';
import { IMPROVEMENT_STATUS_LABELS, IMPROVEMENT_STATUS_BADGE, exportImprovements } from '../../api/improvementsApi';
import { CreateImprovementDialog } from '../../components/bpm/CreateImprovementDialog';
import { getDirectoryEmployees } from '../../api/orgDirectoryApi';
import type { DirectoryEmployeeDto } from '../../api/orgDirectoryApi';
import './ImprovementsPage.css';

type TabId = 'all' | 'my' | 'current' | 'monitor-my' | 'monitor-full';

const TAB_LABELS: Record<TabId, string> = {
    all: 'Все',
    my: 'Мои улучшения',
    current: 'Текущие',
    'monitor-my': 'Мой монитор',
    'monitor-full': 'Полный монитор',
};

const TAB_ROLE: Record<'all' | 'my' | 'current', ImprovementListRole> = {
    all: 'All',
    my: 'My',
    current: 'Current',
};

/** Страница «Улучшения» (FR-BPM-03.1). */
export function ImprovementsPage() {
    const { accessToken: token, hasRole } = useAuth();
    const isAdmin = hasRole('Admin');

    const [tab, setTab] = useState<TabId>('all');
    const [items, setItems] = useState<ImprovementDto[]>([]);
    const [monitorItems, setMonitorItems] = useState<ImprovementMonitorItemDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Фильтры
    const [filterStatus, setFilterStatus] = useState<BpmImprovementStatus | ''>('');
    const [filterDateFrom, setFilterDateFrom] = useState('');
    const [filterDateTo, setFilterDateTo] = useState('');

    // Диалог создания
    const [showCreate, setShowCreate] = useState(false);
    const [processes, setProcesses] = useState<{ id: string; name: string }[]>([]);

    // Диалог принятия
    const [acceptItem, setAcceptItem] = useState<ImprovementDto | null>(null);
    const [acceptAssignedId, setAcceptAssignedId] = useState('');
    const [acceptDueDate, setAcceptDueDate] = useState('');
    const [acceptComment, setAcceptComment] = useState('');
    const [acceptSaving, setAcceptSaving] = useState(false);
    const [acceptError, setAcceptError] = useState<string | null>(null);

    // Диалог отклонения
    const [rejectItem, setRejectItem] = useState<ImprovementDto | null>(null);
    const [rejectComment, setRejectComment] = useState('');
    const [rejectSaving, setRejectSaving] = useState(false);
    const [rejectError, setRejectError] = useState<string | null>(null);

    // Диалог завершения
    const [completeItem, setCompleteItem] = useState<ImprovementDto | null>(null);
    const [resolution, setResolution] = useState('');
    const [completeSaving, setCompleteSaving] = useState(false);
    const [completeError, setCompleteError] = useState<string | null>(null);

    // Список сотрудников для выбора исполнителя
    const [employees, setEmployees] = useState<DirectoryEmployeeDto[]>([]);
    const [employeeSearch, setEmployeeSearch] = useState('');

    const filteredEmployees = employees.filter(e =>
        e.displayName.toLowerCase().includes(employeeSearch.toLowerCase())
    );

    const load = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            if (tab === 'monitor-my') {
                const data = await api.getImprovementMonitorMy(token);
                setMonitorItems(data);
            } else if (tab === 'monitor-full') {
                const data = await api.getImprovementMonitorFull(token);
                setMonitorItems(data);
            } else {
                const data = await api.listImprovements(token, {
                    role: TAB_ROLE[tab],
                    status: filterStatus || undefined,
                    dateFrom: filterDateFrom || undefined,
                    dateTo: filterDateTo || undefined,
                });
                setItems(data);
            }
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, tab, filterStatus, filterDateFrom, filterDateTo]);

    useEffect(() => { load(); }, [load]);

    // Загружаем список процессов и сотрудников при открытии диалога создания
    useEffect(() => {
        if (!showCreate || !token) return;
        fetch('/api/bpm/processes?template=false', { headers: { Authorization: `Bearer ${token}` } })
            .then(r => r.ok ? r.json() : [])
            .then((data: { id: string; name: string }[]) => setProcesses(data))
            .catch(() => setProcesses([]));
    }, [showCreate, token]);

    useEffect(() => {
        if (!acceptItem || !token) return;
        getDirectoryEmployees(token, {})
            .then(setEmployees)
            .catch(() => setEmployees([]));
    }, [acceptItem, token]);

    const handleAccept = async () => {
        if (!acceptItem || !token) return;
        if (!acceptAssignedId) { setAcceptError('Выберите исполнителя'); return; }
        if (!acceptDueDate) { setAcceptError('Укажите срок исполнения'); return; }
        setAcceptSaving(true);
        setAcceptError(null);
        try {
            await api.acceptImprovement(token, acceptItem.id, {
                assignedUserId: acceptAssignedId,
                dueDate: new Date(acceptDueDate).toISOString(),
                comment: acceptComment || undefined,
            });
            setAcceptItem(null);
            setAcceptAssignedId('');
            setAcceptDueDate('');
            setAcceptComment('');
            await load();
        } catch (e) {
            setAcceptError(e instanceof Error ? e.message : 'Ошибка при принятии');
        } finally {
            setAcceptSaving(false);
        }
    };

    const handleReject = async () => {
        if (!rejectItem || !token) return;
        setRejectSaving(true);
        setRejectError(null);
        try {
            await api.rejectImprovement(token, rejectItem.id, { comment: rejectComment || undefined });
            setRejectItem(null);
            setRejectComment('');
            await load();
        } catch (e) {
            setRejectError(e instanceof Error ? e.message : 'Ошибка при отклонении');
        } finally {
            setRejectSaving(false);
        }
    };

    const handleComplete = async () => {
        if (!completeItem || !token) return;
        if (!resolution.trim()) { setCompleteError('Введите резолюцию исполнителя'); return; }
        setCompleteSaving(true);
        setCompleteError(null);
        try {
            await api.completeImprovement(token, completeItem.id, { resolution: resolution.trim() });
            setCompleteItem(null);
            setResolution('');
            await load();
        } catch (e) {
            setCompleteError(e instanceof Error ? e.message : 'Ошибка при завершении');
        } finally {
            setCompleteSaving(false);
        }
    };

    const formatDate = (d?: string) => d ? new Date(d).toLocaleDateString('ru-RU') : '—';

    return (
        <div className="imp-root">
            <div className="imp-header">
                <h1 className="imp-title">Улучшения</h1>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <button className="imp-btn-secondary" onClick={() => token && exportImprovements(token)}
                        title="Экспорт в CSV">
                        ⬇ CSV
                    </button>
                    <button className="imp-btn-primary" onClick={() => setShowCreate(true)}>
                        + Предложить улучшение
                    </button>
                </div>
            </div>

            {/* Вкладки */}
            <div className="imp-tabs">
                {(['all', 'my', 'current', 'monitor-my'] as TabId[]).map(t => (
                    <button
                        key={t}
                        className={`imp-tab${tab === t ? ' imp-tab--active' : ''}`}
                        onClick={() => setTab(t)}
                    >
                        {TAB_LABELS[t]}
                    </button>
                ))}
                {isAdmin && (
                    <button
                        className={`imp-tab${tab === 'monitor-full' ? ' imp-tab--active' : ''}`}
                        onClick={() => setTab('monitor-full')}
                    >
                        {TAB_LABELS['monitor-full']}
                    </button>
                )}
            </div>

            {/* Фильтры — только для вкладок списка */}
            {(tab === 'all' || tab === 'my' || tab === 'current') && (
                <div className="imp-filters">
                    <select
                        className="imp-select"
                        value={filterStatus}
                        onChange={e => setFilterStatus(e.target.value as BpmImprovementStatus | '')}
                    >
                        <option value="">Все статусы</option>
                        {(Object.keys(IMPROVEMENT_STATUS_LABELS) as BpmImprovementStatus[]).map(s => (
                            <option key={s} value={s}>{IMPROVEMENT_STATUS_LABELS[s]}</option>
                        ))}
                    </select>
                    <input
                        type="date"
                        className="imp-input"
                        title="Дата с"
                        value={filterDateFrom}
                        onChange={e => setFilterDateFrom(e.target.value)}
                    />
                    <span className="imp-filter-sep">—</span>
                    <input
                        type="date"
                        className="imp-input"
                        title="Дата по"
                        value={filterDateTo}
                        onChange={e => setFilterDateTo(e.target.value)}
                    />
                    {(filterStatus || filterDateFrom || filterDateTo) && (
                        <button className="imp-btn-reset" onClick={() => {
                            setFilterStatus('');
                            setFilterDateFrom('');
                            setFilterDateTo('');
                        }}>
                            Сбросить
                        </button>
                    )}
                </div>
            )}

            {/* Контент */}
            <div className="imp-content">
                {loading && <div className="imp-state">Загрузка…</div>}
                {error && <div className="imp-error">{error}</div>}

                {/* Монитор */}
                {!loading && !error && (tab === 'monitor-my' || tab === 'monitor-full') && (
                    monitorItems.length === 0
                        ? <div className="imp-state">Нет данных</div>
                        : (
                            <div className="imp-table-wrap">
                                <table className="imp-table">
                                    <thead>
                                        <tr>
                                            <th>Процесс</th>
                                            <th>Ожидает</th>
                                            <th>Принято</th>
                                            <th>В работе</th>
                                            <th>Завершено</th>
                                            <th>Отклонено</th>
                                            <th>Всего</th>
                                            <th>Владельцы</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {monitorItems.map(m => (
                                            <tr key={m.processId}>
                                                <td className="imp-td-name">{m.processName}</td>
                                                <td>{m.pendingCount || '—'}</td>
                                                <td>{m.acceptedCount || '—'}</td>
                                                <td>{m.inProgressCount || '—'}</td>
                                                <td>{m.completedCount || '—'}</td>
                                                <td>{m.rejectedCount || '—'}</td>
                                                <td><strong>{m.totalCount}</strong></td>
                                                <td className="imp-td-owners">{m.owners.join(', ') || '—'}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        )
                )}

                {/* Список предложений */}
                {!loading && !error && (tab === 'all' || tab === 'my' || tab === 'current') && (
                    items.length === 0
                        ? <div className="imp-state">Предложения не найдены</div>
                        : (
                            <div className="imp-list">
                                {items.map(item => (
                                    <div key={item.id} className="imp-card">
                                        <div className="imp-card-top">
                                            <span className={`imp-badge ${IMPROVEMENT_STATUS_BADGE[item.status]}`}>
                                                {IMPROVEMENT_STATUS_LABELS[item.status]}
                                            </span>
                                            <span className="imp-card-process">{item.processName}</span>
                                            <span className="imp-card-date">{formatDate(item.createdAt)}</span>
                                        </div>
                                        <div className="imp-card-subject">{item.subject}</div>
                                        {item.description && (
                                            <div className="imp-card-desc">{item.description}</div>
                                        )}
                                        <div className="imp-card-meta">
                                            <span>Инициатор: <strong>{item.initiatorDisplayName}</strong></span>
                                            {item.assignedDisplayName && (
                                                <span>Исполнитель: <strong>{item.assignedDisplayName}</strong></span>
                                            )}
                                            {item.dueDate && (
                                                <span>Срок: <strong>{formatDate(item.dueDate)}</strong></span>
                                            )}
                                        </div>
                                        {item.reviewComment && (
                                            <div className="imp-card-comment">
                                                <span className="imp-card-comment-label">Комментарий: </span>
                                                {item.reviewComment}
                                            </div>
                                        )}
                                        {item.resolution && (
                                            <div className="imp-card-resolution">
                                                <span className="imp-card-comment-label">Резолюция: </span>
                                                {item.resolution}
                                            </div>
                                        )}
                                        {/* Кнопки действий */}
                                        {item.status === 'Pending' && (
                                            <div className="imp-card-actions">
                                                <button
                                                    className="imp-btn-accept"
                                                    onClick={() => { setAcceptItem(item); setAcceptError(null); }}
                                                >
                                                    Принять
                                                </button>
                                                <button
                                                    className="imp-btn-reject"
                                                    onClick={() => { setRejectItem(item); setRejectError(null); }}
                                                >
                                                    Отклонить
                                                </button>
                                            </div>
                                        )}
                                        {(item.status === 'Accepted' || item.status === 'InProgress') && (
                                            <div className="imp-card-actions">
                                                <button
                                                    className="imp-btn-complete"
                                                    onClick={() => { setCompleteItem(item); setCompleteError(null); }}
                                                >
                                                    Завершить выполнение
                                                </button>
                                            </div>
                                        )}
                                    </div>
                                ))}
                            </div>
                        )
                )}
            </div>

            {/* Диалог создания */}
            {showCreate && token && (
                <CreateImprovementDialog
                    token={token}
                    processes={processes}
                    onCreated={() => { setShowCreate(false); load(); }}
                    onClose={() => setShowCreate(false)}
                />
            )}

            {/* Диалог принятия */}
            {acceptItem && (
                <div className="imp-overlay">
                    <div className="imp-dialog">
                        <div className="imp-dialog-header">
                            <h3 className="imp-dialog-title">Принять предложение</h3>
                            <button className="imp-dialog-close" onClick={() => setAcceptItem(null)}>✕</button>
                        </div>
                        <div className="imp-dialog-body">
                            <p className="imp-dialog-subject">«{acceptItem.subject}»</p>
                            <div className="imp-field">
                                <label className="imp-label">Исполнитель *</label>
                                <input
                                    className="imp-input"
                                    placeholder="Поиск по имени…"
                                    value={employeeSearch}
                                    onChange={e => setEmployeeSearch(e.target.value)}
                                />
                                {employeeSearch && filteredEmployees.length > 0 && (
                                    <div className="imp-suggest">
                                        {filteredEmployees.slice(0, 8).map(e => (
                                            <button
                                                key={e.id}
                                                className={`imp-suggest-item${acceptAssignedId === e.userId ? ' imp-suggest-item--active' : ''}`}
                                                onClick={() => {
                                                    setAcceptAssignedId(e.userId);
                                                    setEmployeeSearch(e.displayName);
                                                }}
                                            >
                                                {e.displayName}
                                                {e.position && <span className="imp-suggest-pos"> — {e.position}</span>}
                                            </button>
                                        ))}
                                    </div>
                                )}
                            </div>
                            <div className="imp-field">
                                <label className="imp-label">Срок исполнения *</label>
                                <input
                                    type="date"
                                    className="imp-input"
                                    value={acceptDueDate}
                                    onChange={e => setAcceptDueDate(e.target.value)}
                                />
                            </div>
                            <div className="imp-field">
                                <label className="imp-label">Комментарий</label>
                                <textarea
                                    className="imp-textarea"
                                    rows={3}
                                    value={acceptComment}
                                    onChange={e => setAcceptComment(e.target.value)}
                                    placeholder="Необязательный комментарий"
                                />
                            </div>
                            {acceptError && <div className="imp-error">{acceptError}</div>}
                        </div>
                        <div className="imp-dialog-footer">
                            <button className="imp-btn-secondary" onClick={() => setAcceptItem(null)} disabled={acceptSaving}>Отмена</button>
                            <button className="imp-btn-primary-sm imp-btn-accept" onClick={handleAccept} disabled={acceptSaving}>
                                {acceptSaving ? 'Сохранение…' : 'Принять'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог отклонения */}
            {rejectItem && (
                <div className="imp-overlay">
                    <div className="imp-dialog">
                        <div className="imp-dialog-header">
                            <h3 className="imp-dialog-title">Отклонить предложение</h3>
                            <button className="imp-dialog-close" onClick={() => setRejectItem(null)}>✕</button>
                        </div>
                        <div className="imp-dialog-body">
                            <p className="imp-dialog-subject">«{rejectItem.subject}»</p>
                            <div className="imp-field">
                                <label className="imp-label">Комментарий</label>
                                <textarea
                                    className="imp-textarea"
                                    rows={3}
                                    value={rejectComment}
                                    onChange={e => setRejectComment(e.target.value)}
                                    placeholder="Причина отклонения (необязательно)"
                                />
                            </div>
                            {rejectError && <div className="imp-error">{rejectError}</div>}
                        </div>
                        <div className="imp-dialog-footer">
                            <button className="imp-btn-secondary" onClick={() => setRejectItem(null)} disabled={rejectSaving}>Отмена</button>
                            <button className="imp-btn-primary-sm imp-btn-reject" onClick={handleReject} disabled={rejectSaving}>
                                {rejectSaving ? 'Сохранение…' : 'Отклонить'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог завершения */}
            {completeItem && (
                <div className="imp-overlay">
                    <div className="imp-dialog">
                        <div className="imp-dialog-header">
                            <h3 className="imp-dialog-title">Завершить выполнение</h3>
                            <button className="imp-dialog-close" onClick={() => setCompleteItem(null)}>✕</button>
                        </div>
                        <div className="imp-dialog-body">
                            <p className="imp-dialog-subject">«{completeItem.subject}»</p>
                            <div className="imp-field">
                                <label className="imp-label">Резолюция исполнителя *</label>
                                <textarea
                                    className="imp-textarea"
                                    rows={4}
                                    value={resolution}
                                    onChange={e => setResolution(e.target.value)}
                                    placeholder="Опишите, что было сделано"
                                />
                            </div>
                            {completeError && <div className="imp-error">{completeError}</div>}
                        </div>
                        <div className="imp-dialog-footer">
                            <button className="imp-btn-secondary" onClick={() => setCompleteItem(null)} disabled={completeSaving}>Отмена</button>
                            <button className="imp-btn-primary-sm imp-btn-complete" onClick={handleComplete} disabled={completeSaving}>
                                {completeSaving ? 'Сохранение…' : 'Завершить'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
