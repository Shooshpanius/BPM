import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/bpmApi';
import type { InstanceStatusOptionDto } from '../../api/bpmApi';
import './ProcessMonitorPage.css';

// ─── Типы ─────────────────────────────────────────────────────────────────────

/** Заглушка экземпляра процесса (FR-BPM-02 ещё не реализован) */
interface ProcessInstanceStub {
    id: string;
    processId: string;
    processName: string;
    statusCode: string;
    startedAt: string;
    startedByName: string;
}

type ViewMode = 'list' | 'kanban';

// ─── Утилиты ─────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
    try {
        return new Date(iso).toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    } catch { return iso; }
}

// ─── Пропсы ───────────────────────────────────────────────────────────────────

interface Props {
    processId: string;
    processName: string;
    onBack: () => void;
}

// ─── Компонент ───────────────────────────────────────────────────────────────

/**
 * ProcessMonitorPage — монитор экземпляров процесса с фильтром по статусам
 * и Kanban-видом, сгруппированным по пользовательским статусам.
 * Данные об экземплярах будут доступны после реализации FR-BPM-02.
 */
export function ProcessMonitorPage({ processId, processName, onBack }: Props) {
    const { accessToken: token } = useAuth();

    const [viewMode, setViewMode] = useState<ViewMode>('list');
    const [statusOptions, setStatusOptions] = useState<InstanceStatusOptionDto[]>([]);
    const [filterStatus, setFilterStatus] = useState<string>('');
    const [instances, setInstances] = useState<ProcessInstanceStub[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // ─── Загрузка статусов ────────────────────────────────────────────────────

    const loadData = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const statusConfig = await api.getStatusConfig(token, processId);
            setStatusOptions(statusConfig.options ?? []);
        } catch {
            // Статусы не настроены — не критично
        }
        // TODO: после реализации FR-BPM-02 — загружать реальные экземпляры процесса через
        // GET /api/bpm/processes/{processId}/instances?statusCode={filterStatus}
        // Пока показываем заглушку
        setInstances([]);
        setLoading(false);
    }, [token, processId]);

    useEffect(() => { loadData(); }, [loadData]);

    // ─── Фильтрация ───────────────────────────────────────────────────────────

    const filteredInstances = filterStatus
        ? instances.filter(i => i.statusCode === filterStatus)
        : instances;

    // ─── Kanban: группировка по статусу ───────────────────────────────────────

    const columns: { code: string; name: string; items: ProcessInstanceStub[] }[] = statusOptions.length > 0
        ? statusOptions.map(opt => ({
            code: opt.code,
            name: opt.name,
            items: instances.filter(i => i.statusCode === opt.code),
        }))
        : [{ code: '', name: 'Все', items: filteredInstances }];

    return (
        <div className="pmon-root">
            {/* Заголовок */}
            <div className="pmon-header">
                <button className="pmon-back-btn" onClick={onBack}>← Процессы</button>
                <h2 className="pmon-title">Монитор: {processName}</h2>
                <div className="pmon-header-spacer" />
                <div className="pmon-view-toggle" role="group" aria-label="Вид">
                    <button
                        className={`pmon-view-btn${viewMode === 'list' ? ' active' : ''}`}
                        onClick={() => setViewMode('list')}
                        title="Список"
                    >☰ Список</button>
                    <button
                        className={`pmon-view-btn${viewMode === 'kanban' ? ' active' : ''}`}
                        onClick={() => setViewMode('kanban')}
                        title="Kanban"
                    >⊞ Kanban</button>
                </div>
            </div>

            {/* Фильтры */}
            <div className="pmon-filters">
                <label className="pmon-filter-label">Статус:</label>
                <select
                    className="pmon-select"
                    value={filterStatus}
                    onChange={e => setFilterStatus(e.target.value)}
                >
                    <option value="">Все</option>
                    {statusOptions.map(opt => (
                        <option key={opt.id} value={opt.code}>{opt.name}</option>
                    ))}
                </select>
                <span className="pmon-count">{filteredInstances.length} записей</span>
            </div>

            {error && <div className="pmon-error">{error}</div>}

            {/* Заглушка — ожидание FR-BPM-02 */}
            {!loading && instances.length === 0 && (
                <div className="pmon-placeholder">
                    <div className="pmon-placeholder-icon">⚙️</div>
                    <div className="pmon-placeholder-title">Данные появятся после реализации FR-BPM-02</div>
                    <p className="pmon-placeholder-text">
                        Монитор экземпляров процесса будет показывать запущенные и завершённые экземпляры
                        после внедрения движка выполнения бизнес-процессов (FR-BPM-02).
                    </p>
                    {statusOptions.length === 0 && (
                        <p className="pmon-placeholder-text">
                            Для этого процесса не настроены пользовательские статусы.
                            Добавьте их во вкладке «Статусы» в дизайнере процесса.
                        </p>
                    )}
                    {statusOptions.length > 0 && (
                        <div className="pmon-status-preview">
                            <p style={{ fontWeight: 500, marginBottom: 8 }}>Настроенные статусы:</p>
                            {statusOptions.map(opt => (
                                <span key={opt.id} className="pmon-status-badge">
                                    {opt.name} <code style={{ fontSize: 10 }}>({opt.code})</code>
                                </span>
                            ))}
                        </div>
                    )}
                </div>
            )}

            {/* Список */}
            {viewMode === 'list' && filteredInstances.length > 0 && (
                <div className="pmon-list">
                    <table className="pmon-table">
                        <thead>
                            <tr>
                                <th>ID</th>
                                <th>Процесс</th>
                                <th>Статус</th>
                                <th>Запущен</th>
                                <th>Инициатор</th>
                            </tr>
                        </thead>
                        <tbody>
                            {filteredInstances.map(inst => (
                                <tr key={inst.id}>
                                    <td><code style={{ fontSize: 11 }}>{inst.id.slice(0, 8)}…</code></td>
                                    <td>{inst.processName}</td>
                                    <td>
                                        <span className="pmon-status-badge">
                                            {statusOptions.find(o => o.code === inst.statusCode)?.name ?? inst.statusCode}
                                        </span>
                                    </td>
                                    <td>{formatDate(inst.startedAt)}</td>
                                    <td>{inst.startedByName}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Kanban */}
            {viewMode === 'kanban' && (
                <div className="pmon-kanban">
                    {columns.map(col => (
                        <div key={col.code} className="pmon-kanban-col">
                            <div className="pmon-kanban-col-header">
                                {col.name}
                                <span className="pmon-kanban-count">{col.items.length}</span>
                            </div>
                            <div className="pmon-kanban-col-body">
                                {col.items.map(inst => (
                                    <div key={inst.id} className="pmon-kanban-card">
                                        <div className="pmon-kanban-card-title">{inst.processName}</div>
                                        <div className="pmon-kanban-card-meta">
                                            <span>{formatDate(inst.startedAt)}</span>
                                            <span>{inst.startedByName}</span>
                                        </div>
                                    </div>
                                ))}
                                {col.items.length === 0 && (
                                    <div className="pmon-kanban-empty">Нет экземпляров</div>
                                )}
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}
