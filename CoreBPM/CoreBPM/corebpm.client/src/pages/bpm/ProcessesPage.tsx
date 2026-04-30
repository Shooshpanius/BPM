import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/bpmApi';
import type { BpmProcessListItemDto } from '../../api/bpmApi';
import './ProcessesPage.css';

interface ProcessesPageProps {
    onOpenDesigner: (processId: string) => void;
    onOpenMonitor?: (processId: string, processName: string) => void;
}

/** Страница списка бизнес-процессов с созданием и удалением. */
export function ProcessesPage({ onOpenDesigner, onOpenMonitor }: ProcessesPageProps) {
    const { accessToken: token } = useAuth();

    const [organizations, setOrganizations] = useState<{ id: string; name: string }[]>([]);
    const [selectedOrgId, setSelectedOrgId] = useState<string | null>(null);
    const [processes, setProcesses] = useState<BpmProcessListItemDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [orgLoadError, setOrgLoadError] = useState<string | null>(null);

    // Диалог создания
    const [showCreate, setShowCreate] = useState(false);
    const [createName, setCreateName] = useState('');
    const [createDesc, setCreateDesc] = useState('');
    const [creating, setCreating] = useState(false);
    const [createError, setCreateError] = useState<string | null>(null);

    // Подтверждение удаления
    const [deleteId, setDeleteId] = useState<string | null>(null);
    const [deleting, setDeleting] = useState(false);

    // Загрузка организаций
    useEffect(() => {
        if (!token) return;
        fetch('/api/org/directory/organizations', {
            headers: { Authorization: `Bearer ${token}` },
        })
            .then(r => {
                if (!r.ok) throw new Error(`HTTP ${r.status}`);
                return r.json();
            })
            .then((orgs: { id: string; name: string }[]) => {
                setOrganizations(orgs);
                if (orgs.length > 0) setSelectedOrgId(orgs[0].id);
            })
            .catch((e: unknown) => {
                setOrgLoadError(e instanceof Error ? e.message : 'Не удалось загрузить список организаций');
            });
    }, [token]);

    // Загрузка процессов
    const loadProcesses = useCallback(async () => {
        if (!token || !selectedOrgId) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.getProcesses(token, selectedOrgId);
            setProcesses(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, selectedOrgId]);

    useEffect(() => { loadProcesses(); }, [loadProcesses]);

    // Создание процесса
    const handleCreate = async () => {
        if (!token || !selectedOrgId || !createName.trim()) return;
        setCreating(true);
        setCreateError(null);
        try {
            const proc = await api.createProcess(token, {
                organizationId: selectedOrgId,
                name: createName.trim(),
                description: createDesc.trim() || undefined,
            });
            setShowCreate(false);
            setCreateName('');
            setCreateDesc('');
            await loadProcesses();
            onOpenDesigner(proc.id);
        } catch (e) {
            setCreateError(e instanceof Error ? e.message : 'Ошибка создания');
        } finally {
            setCreating(false);
        }
    };

    // Удаление процесса
    const handleDelete = async () => {
        if (!token || !deleteId) return;
        setDeleting(true);
        try {
            await api.deleteProcess(token, deleteId);
            setDeleteId(null);
            await loadProcesses();
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка удаления');
        } finally {
            setDeleting(false);
        }
    };

    const formatDate = (iso: string) =>
        new Date(iso).toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' });

    const statusBadge = (p: BpmProcessListItemDto) => {
        if (p.activeVersionNumber != null)
            return <span className="pp-badge pp-badge--active">Активен v{p.activeVersionNumber}</span>;
        if (p.totalVersions > 0)
            return <span className="pp-badge pp-badge--draft">Черновик</span>;
        return <span className="pp-badge pp-badge--empty">Пусто</span>;
    };

    return (
        <div className="pp-root">
            <div className="pp-header">
                <h1 className="pp-title">Бизнес-процессы</h1>
                <div className="pp-header-actions">
                    {organizations.length > 1 && (
                        <select
                            className="pp-org-select"
                            value={selectedOrgId ?? ''}
                            onChange={e => setSelectedOrgId(e.target.value)}
                            aria-label="Организация"
                        >
                            {organizations.map(o => (
                                <option key={o.id} value={o.id}>{o.name}</option>
                            ))}
                        </select>
                    )}
                    <button className="pp-btn-primary" onClick={() => setShowCreate(true)}>
                        + Создать процесс
                    </button>
                </div>
            </div>

            {loading && <div className="pp-loading">Загрузка...</div>}
            {orgLoadError && <div className="pp-error">{orgLoadError}</div>}
            {error && <div className="pp-error">{error}</div>}

            {!loading && !error && processes.length === 0 && (
                <div className="pp-empty">
                    <div className="pp-empty-icon" aria-hidden="true">⬡</div>
                    <p className="pp-empty-title">Процессов ещё нет</p>
                    <p className="pp-empty-sub">Создайте первый бизнес-процесс, чтобы начать моделирование</p>
                    <button className="pp-btn-primary" onClick={() => setShowCreate(true)}>
                        Создать процесс
                    </button>
                </div>
            )}

            {processes.length > 0 && (
                <div className="pp-list">
                    {processes.map(p => (
                        <div key={p.id} className="pp-card">
                            <div className="pp-card-main" onClick={() => onOpenDesigner(p.id)} role="button" tabIndex={0}
                                onKeyDown={e => e.key === 'Enter' && onOpenDesigner(p.id)}>
                                <div className="pp-card-name">{p.name}</div>
                                {p.description && <div className="pp-card-desc">{p.description}</div>}
                                <div className="pp-card-meta">
                                    {statusBadge(p)}
                                    <span className="pp-card-date">Обновлён {formatDate(p.updatedAt)}</span>
                                    <span className="pp-card-versions">{p.totalVersions} {p.totalVersions === 1 ? 'версия' : 'версий'}</span>
                                </div>
                            </div>
                            <div className="pp-card-actions">
                                <button
                                    className="pp-btn-icon pp-btn-open"
                                    title="Открыть в дизайнере"
                                    onClick={() => onOpenDesigner(p.id)}
                                    aria-label="Открыть в дизайнере"
                                >
                                    ✎
                                </button>
                                {onOpenMonitor && (
                                    <button
                                        className="pp-btn-icon"
                                        title="Монитор экземпляров"
                                        onClick={() => onOpenMonitor(p.id, p.name)}
                                        aria-label="Монитор экземпляров"
                                        style={{ fontSize: 14 }}
                                    >
                                        ⊞
                                    </button>
                                )}
                                <button
                                    className="pp-btn-icon pp-btn-delete"
                                    title="Удалить процесс"
                                    onClick={() => setDeleteId(p.id)}
                                    aria-label="Удалить процесс"
                                >
                                    ✕
                                </button>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {/* Диалог создания */}
            {showCreate && (
                <div className="pp-modal-overlay" onClick={() => setShowCreate(false)}>
                    <div className="pp-modal" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true" aria-labelledby="pp-create-title">
                        <h2 id="pp-create-title" className="pp-modal-title">Новый бизнес-процесс</h2>
                        <div className="pp-modal-field">
                            <label htmlFor="pp-create-name">Название *</label>
                            <input
                                id="pp-create-name"
                                className="pp-input"
                                type="text"
                                value={createName}
                                onChange={e => setCreateName(e.target.value)}
                                placeholder="Например: Согласование договора"
                                autoFocus
                                onKeyDown={e => e.key === 'Enter' && handleCreate()}
                            />
                        </div>
                        <div className="pp-modal-field">
                            <label htmlFor="pp-create-desc">Описание</label>
                            <textarea
                                id="pp-create-desc"
                                className="pp-input pp-textarea"
                                value={createDesc}
                                onChange={e => setCreateDesc(e.target.value)}
                                placeholder="Краткое описание назначения процесса"
                                rows={3}
                            />
                        </div>
                        {createError && <div className="pp-modal-error">{createError}</div>}
                        <div className="pp-modal-actions">
                            <button className="pp-btn-secondary" onClick={() => setShowCreate(false)} disabled={creating}>
                                Отмена
                            </button>
                            <button
                                className="pp-btn-primary"
                                onClick={handleCreate}
                                disabled={creating || !createName.trim()}
                            >
                                {creating ? 'Создание...' : 'Создать и открыть'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог подтверждения удаления */}
            {deleteId && (
                <div className="pp-modal-overlay" onClick={() => setDeleteId(null)}>
                    <div className="pp-modal pp-modal--confirm" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
                        <h2 className="pp-modal-title">Удалить процесс?</h2>
                        <p className="pp-modal-text">
                            Процесс «{processes.find(p => p.id === deleteId)?.name}» и все его версии будут удалены. Это действие необратимо.
                        </p>
                        <div className="pp-modal-actions">
                            <button className="pp-btn-secondary" onClick={() => setDeleteId(null)} disabled={deleting}>
                                Отмена
                            </button>
                            <button className="pp-btn-danger" onClick={handleDelete} disabled={deleting}>
                                {deleting ? 'Удаление...' : 'Удалить'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
