import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/bpmApi';
import type { BpmProcessListItemDto } from '../../api/bpmApi';
import './ProcessesPage.css';

interface ProcessesPageProps {
    onOpenDesigner: (processId: string) => void;
    onOpenMonitor?: (processId: string, processName: string) => void;
}

/** Страница списка бизнес-процессов с тегами, шаблонами и мониторингом. */
export function ProcessesPage({ onOpenDesigner, onOpenMonitor }: ProcessesPageProps) {
    const { accessToken: token } = useAuth();

    const [organizations, setOrganizations] = useState<{ id: string; name: string }[]>([]);
    const [selectedOrgId, setSelectedOrgId] = useState<string | null>(null);
    const [processes, setProcesses] = useState<BpmProcessListItemDto[]>([]);
    const [templates, setTemplates] = useState<BpmProcessListItemDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [orgLoadError, setOrgLoadError] = useState<string | null>(null);

    // Вкладки: 'processes' | 'templates'
    const [activeTab, setActiveTab] = useState<'processes' | 'templates'>('processes');

    // Фильтр по тегу
    const [tagFilter, setTagFilter] = useState<string>('');

    // Диалог создания
    const [showCreate, setShowCreate] = useState(false);
    const [createName, setCreateName] = useState('');
    const [createDesc, setCreateDesc] = useState('');
    const [createIsTemplate, setCreateIsTemplate] = useState(false);
    const [createTags, setCreateTags] = useState('');
    const [creating, setCreating] = useState(false);
    const [createError, setCreateError] = useState<string | null>(null);

    // Диалог создания из шаблона
    const [fromTemplateId, setFromTemplateId] = useState<string | null>(null);
    const [fromTemplateName, setFromTemplateName] = useState('');
    const [fromTemplateDesc, setFromTemplateDesc] = useState('');
    const [fromTemplateCreating, setFromTemplateCreating] = useState(false);
    const [fromTemplateError, setFromTemplateError] = useState<string | null>(null);

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

    // Загрузка процессов и шаблонов
    const loadProcesses = useCallback(async () => {
        if (!token || !selectedOrgId) return;
        setLoading(true);
        setError(null);
        try {
            const [data, tmpl] = await Promise.all([
                api.getProcesses(token, selectedOrgId),
                api.getProcessTemplates(token, selectedOrgId),
            ]);
            setProcesses(data);
            setTemplates(tmpl);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, selectedOrgId]);

    useEffect(() => { loadProcesses(); }, [loadProcesses]);

    // Все уникальные теги для фильтра
    const allTags = Array.from(new Set(processes.flatMap(p => p.tags ?? []))).sort();

    // Отфильтрованные процессы
    const filteredProcesses = tagFilter
        ? processes.filter(p => (p.tags ?? []).includes(tagFilter))
        : processes;

    // Создание процесса
    const handleCreate = async () => {
        if (!token || !selectedOrgId || !createName.trim()) return;
        setCreating(true);
        setCreateError(null);
        try {
            const tags = createTags.split(',').map(t => t.trim()).filter(Boolean);
            const proc = await api.createProcess(token, {
                organizationId: selectedOrgId,
                name: createName.trim(),
                description: createDesc.trim() || undefined,
                tags,
                isTemplate: createIsTemplate,
            });
            setShowCreate(false);
            setCreateName('');
            setCreateDesc('');
            setCreateTags('');
            setCreateIsTemplate(false);
            await loadProcesses();
            if (!createIsTemplate) onOpenDesigner(proc.id);
        } catch (e) {
            setCreateError(e instanceof Error ? e.message : 'Ошибка создания');
        } finally {
            setCreating(false);
        }
    };

    // Создание из шаблона
    const handleCreateFromTemplate = async () => {
        if (!token || !selectedOrgId || !fromTemplateId || !fromTemplateName.trim()) return;
        setFromTemplateCreating(true);
        setFromTemplateError(null);
        try {
            const proc = await api.createProcessFromTemplate(token, fromTemplateId, {
                organizationId: selectedOrgId,
                name: fromTemplateName.trim(),
                description: fromTemplateDesc.trim() || undefined,
            });
            setFromTemplateId(null);
            setFromTemplateName('');
            setFromTemplateDesc('');
            await loadProcesses();
            onOpenDesigner(proc.id);
        } catch (e) {
            setFromTemplateError(e instanceof Error ? e.message : 'Ошибка создания');
        } finally {
            setFromTemplateCreating(false);
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

    const renderProcessCard = (p: BpmProcessListItemDto, isTemplate = false) => (
        <div key={p.id} className={`pp-card${isTemplate ? ' pp-card--template' : ''}`}>
            <div className="pp-card-main" onClick={() => onOpenDesigner(p.id)} role="button" tabIndex={0}
                onKeyDown={e => e.key === 'Enter' && onOpenDesigner(p.id)}>
                <div className="pp-card-name">
                    {isTemplate && <span className="pp-template-icon" title="Шаблон">📋 </span>}
                    {p.name}
                </div>
                {p.description && <div className="pp-card-desc">{p.description}</div>}
                {(p.tags ?? []).length > 0 && (
                    <div className="pp-card-tags">
                        {p.tags.map(tag => (
                            <button
                                key={tag}
                                className={`pp-tag${tagFilter === tag ? ' pp-tag--active' : ''}`}
                                onClick={e => { e.stopPropagation(); setTagFilter(tagFilter === tag ? '' : tag); }}
                                type="button"
                            >
                                {tag}
                            </button>
                        ))}
                    </div>
                )}
                <div className="pp-card-meta">
                    {statusBadge(p)}
                    <span className="pp-card-date">Обновлён {formatDate(p.updatedAt)}</span>
                    <span className="pp-card-versions">{p.totalVersions} {p.totalVersions === 1 ? 'версия' : 'версий'}</span>
                </div>
            </div>
            <div className="pp-card-actions">
                {isTemplate ? (
                    <button
                        className="pp-btn-icon pp-btn-open"
                        title="Создать процесс из шаблона"
                        onClick={() => { setFromTemplateId(p.id); setFromTemplateName(''); setFromTemplateDesc(''); }}
                        aria-label="Создать из шаблона"
                    >
                        ⊕
                    </button>
                ) : (
                    <button
                        className="pp-btn-icon pp-btn-open"
                        title="Открыть в дизайнере"
                        onClick={() => onOpenDesigner(p.id)}
                        aria-label="Открыть в дизайнере"
                    >
                        ✎
                    </button>
                )}
                {!isTemplate && onOpenMonitor && (
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
                    title="Удалить"
                    onClick={() => setDeleteId(p.id)}
                    aria-label="Удалить"
                >
                    ✕
                </button>
            </div>
        </div>
    );

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

            {/* Вкладки */}
            <div className="pp-tabs" role="tablist">
                <button
                    className={`pp-tab${activeTab === 'processes' ? ' pp-tab--active' : ''}`}
                    onClick={() => setActiveTab('processes')}
                    role="tab"
                    aria-selected={activeTab === 'processes'}
                >
                    Процессы
                    {processes.length > 0 && <span className="pp-tab-count">{processes.length}</span>}
                </button>
                <button
                    className={`pp-tab${activeTab === 'templates' ? ' pp-tab--active' : ''}`}
                    onClick={() => setActiveTab('templates')}
                    role="tab"
                    aria-selected={activeTab === 'templates'}
                >
                    Шаблоны
                    {templates.length > 0 && <span className="pp-tab-count">{templates.length}</span>}
                </button>
            </div>

            {loading && <div className="pp-loading">Загрузка...</div>}
            {orgLoadError && <div className="pp-error">{orgLoadError}</div>}
            {error && <div className="pp-error">{error}</div>}

            {/* Фильтр по тегу (только на вкладке процессов) */}
            {activeTab === 'processes' && allTags.length > 0 && (
                <div className="pp-tag-filter">
                    <span className="pp-tag-filter-label">Тег:</span>
                    <button
                        className={`pp-tag${tagFilter === '' ? ' pp-tag--active' : ''}`}
                        onClick={() => setTagFilter('')}
                        type="button"
                    >
                        Все
                    </button>
                    {allTags.map(tag => (
                        <button
                            key={tag}
                            className={`pp-tag${tagFilter === tag ? ' pp-tag--active' : ''}`}
                            onClick={() => setTagFilter(tagFilter === tag ? '' : tag)}
                            type="button"
                        >
                            {tag}
                        </button>
                    ))}
                </div>
            )}

            {/* Список процессов */}
            {activeTab === 'processes' && (
                <>
                    {!loading && !error && filteredProcesses.length === 0 && (
                        <div className="pp-empty">
                            <div className="pp-empty-icon" aria-hidden="true">⬡</div>
                            <p className="pp-empty-title">
                                {tagFilter ? `Нет процессов с тегом «${tagFilter}»` : 'Процессов ещё нет'}
                            </p>
                            {!tagFilter && (
                                <>
                                    <p className="pp-empty-sub">Создайте первый бизнес-процесс или используйте шаблон</p>
                                    <button className="pp-btn-primary" onClick={() => setShowCreate(true)}>
                                        Создать процесс
                                    </button>
                                </>
                            )}
                        </div>
                    )}
                    {filteredProcesses.length > 0 && (
                        <div className="pp-list">
                            {filteredProcesses.map(p => renderProcessCard(p, false))}
                        </div>
                    )}
                </>
            )}

            {/* Список шаблонов */}
            {activeTab === 'templates' && (
                <>
                    {!loading && !error && templates.length === 0 && (
                        <div className="pp-empty">
                            <div className="pp-empty-icon" aria-hidden="true">📋</div>
                            <p className="pp-empty-title">Шаблонов ещё нет</p>
                            <p className="pp-empty-sub">
                                При создании процесса установите флаг «Сохранить как шаблон»
                            </p>
                        </div>
                    )}
                    {templates.length > 0 && (
                        <div className="pp-list">
                            {templates.map(p => renderProcessCard(p, true))}
                        </div>
                    )}
                </>
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
                        <div className="pp-modal-field">
                            <label htmlFor="pp-create-tags">Теги (через запятую)</label>
                            <input
                                id="pp-create-tags"
                                className="pp-input"
                                type="text"
                                value={createTags}
                                onChange={e => setCreateTags(e.target.value)}
                                placeholder="HR, Согласование, Закупки"
                            />
                        </div>
                        <div className="pp-modal-field pp-modal-field--inline">
                            <input
                                id="pp-create-template"
                                type="checkbox"
                                checked={createIsTemplate}
                                onChange={e => setCreateIsTemplate(e.target.checked)}
                            />
                            <label htmlFor="pp-create-template" style={{ cursor: 'pointer' }}>
                                Сохранить как шаблон
                            </label>
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
                                {creating ? 'Создание...' : (createIsTemplate ? 'Создать шаблон' : 'Создать и открыть')}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог создания из шаблона */}
            {fromTemplateId && (
                <div className="pp-modal-overlay" onClick={() => setFromTemplateId(null)}>
                    <div className="pp-modal" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
                        <h2 className="pp-modal-title">Создать процесс из шаблона</h2>
                        <p className="pp-modal-text" style={{ marginBottom: 12, fontSize: 13 }}>
                            Шаблон: <strong>{templates.find(t => t.id === fromTemplateId)?.name}</strong>
                        </p>
                        <div className="pp-modal-field">
                            <label htmlFor="pp-from-tmpl-name">Название нового процесса *</label>
                            <input
                                id="pp-from-tmpl-name"
                                className="pp-input"
                                type="text"
                                value={fromTemplateName}
                                onChange={e => setFromTemplateName(e.target.value)}
                                placeholder="Название процесса"
                                autoFocus
                            />
                        </div>
                        <div className="pp-modal-field">
                            <label htmlFor="pp-from-tmpl-desc">Описание</label>
                            <textarea
                                id="pp-from-tmpl-desc"
                                className="pp-input pp-textarea"
                                value={fromTemplateDesc}
                                onChange={e => setFromTemplateDesc(e.target.value)}
                                rows={2}
                            />
                        </div>
                        {fromTemplateError && <div className="pp-modal-error">{fromTemplateError}</div>}
                        <div className="pp-modal-actions">
                            <button className="pp-btn-secondary" onClick={() => setFromTemplateId(null)} disabled={fromTemplateCreating}>
                                Отмена
                            </button>
                            <button
                                className="pp-btn-primary"
                                onClick={handleCreateFromTemplate}
                                disabled={fromTemplateCreating || !fromTemplateName.trim()}
                            >
                                {fromTemplateCreating ? 'Создание...' : 'Создать и открыть'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог подтверждения удаления */}
            {deleteId && (
                <div className="pp-modal-overlay" onClick={() => setDeleteId(null)}>
                    <div className="pp-modal pp-modal--confirm" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
                        <h2 className="pp-modal-title">Удалить?</h2>
                        <p className="pp-modal-text">
                            «{[...processes, ...templates].find(p => p.id === deleteId)?.name}» и все версии будут удалены.
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
