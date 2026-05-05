import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/rulesApi';
import type { DmnTableListItemDto, DmnHitPolicy } from '../../api/rulesApi';
import './RulesPage.css';

const HIT_POLICY_LABELS: Record<DmnHitPolicy, string> = {
    Unique: 'UNIQUE',
    First: 'FIRST',
    Any: 'ANY',
    Collect: 'COLLECT',
    RuleOrder: 'RULE ORDER',
    OutputOrder: 'OUTPUT ORDER',
};

const STATUS_LABELS: Record<string, string> = {
    Draft: 'Черновик',
    Published: 'Опубликована',
    Archived: 'Архив',
};

interface RulesPageProps {
    onOpenEditor: (tableId: string) => void;
}

/** Страница списка DMN-таблиц бизнес-правил. */
export function RulesPage({ onOpenEditor }: RulesPageProps) {
    const { accessToken: token } = useAuth();

    const [tables, setTables] = useState<DmnTableListItemDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Диалог создания/редактирования
    const [showForm, setShowForm] = useState(false);
    const [editTable, setEditTable] = useState<DmnTableListItemDto | null>(null);
    const [formName, setFormName] = useState('');
    const [formDesc, setFormDesc] = useState('');
    const [formPolicy, setFormPolicy] = useState<DmnHitPolicy>('Unique');
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);

    // Подтверждение удаления
    const [deleteId, setDeleteId] = useState<string | null>(null);
    const [deleting, setDeleting] = useState(false);

    const [shakeTarget, setShakeTarget] = useState<string | null>(null);
    const shakeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const triggerShake = (target: string) => {
        if (shakeTimerRef.current) clearTimeout(shakeTimerRef.current);
        setShakeTarget(target);
        shakeTimerRef.current = setTimeout(() => setShakeTarget(null), 900);
    };
    const sc = (target: string) => shakeTarget === target ? ' btn-flash' : '';

    const loadTables = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.getDmnTables(token);
            setTables(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token]);

    useEffect(() => { loadTables(); }, [loadTables]);

    const openCreate = () => {
        setEditTable(null);
        setFormName('');
        setFormDesc('');
        setFormPolicy('Unique');
        setFormError(null);
        setShowForm(true);
    };

    const openEdit = (t: DmnTableListItemDto) => {
        setEditTable(t);
        setFormName(t.name);
        setFormDesc(t.description ?? '');
        setFormPolicy(t.hitPolicy);
        setFormError(null);
        setShowForm(true);
    };

    const handleSave = async () => {
        if (!token || !formName.trim()) {
            setFormError('Название обязательно');
            return;
        }
        setSaving(true);
        setFormError(null);
        try {
            const req = { name: formName.trim(), description: formDesc.trim() || undefined, hitPolicy: formPolicy };
            if (editTable) {
                await api.updateDmnTable(token, editTable.id, req);
            } else {
                await api.createDmnTable(token, req);
            }
            setShowForm(false);
            await loadTables();
        } catch (e) {
            setFormError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async () => {
        if (!token || !deleteId) return;
        setDeleting(true);
        try {
            await api.deleteDmnTable(token, deleteId);
            setDeleteId(null);
            await loadTables();
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка удаления');
            setDeleteId(null);
        } finally {
            setDeleting(false);
        }
    };

    const formatDate = (iso: string) =>
        new Date(iso).toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' });

    return (
        <div className="rp-root">
            <div className="rp-header">
                <h1 className="rp-title">Бизнес-правила (DMN)</h1>
                <button className="rp-btn-primary" onClick={openCreate}>
                    + Создать таблицу
                </button>
            </div>

            {error && <div className="rp-error">{error}</div>}

            {loading ? (
                <div className="rp-loading">Загрузка…</div>
            ) : tables.length === 0 ? (
                <div className="rp-empty">
                    <div className="rp-empty-icon">📋</div>
                    <h2 className="rp-empty-title">Таблиц правил нет</h2>
                    <p className="rp-empty-sub">Создайте первую DMN-таблицу для автоматизации бизнес-решений</p>
                    <button className="rp-btn-primary" onClick={openCreate}>Создать таблицу</button>
                </div>
            ) : (
                <div className="rp-list">
                    {tables.map(t => (
                        <div key={t.id} className="rp-card">
                            <div
                                className="rp-card-main"
                                role="button"
                                tabIndex={0}
                                onClick={() => onOpenEditor(t.id)}
                                onKeyDown={e => e.key === 'Enter' && onOpenEditor(t.id)}
                            >
                                <div className="rp-card-name">{t.name}</div>
                                {t.description && <div className="rp-card-desc">{t.description}</div>}
                                <div className="rp-card-meta">
                                    <span className="rp-badge rp-badge--policy">{HIT_POLICY_LABELS[t.hitPolicy]}</span>
                                    {t.latestVersionStatus && (
                                        <span className={`rp-badge rp-badge--${t.latestVersionStatus.toLowerCase()}`}>
                                            {STATUS_LABELS[t.latestVersionStatus] ?? t.latestVersionStatus}
                                        </span>
                                    )}
                                    <span className="rp-card-date">Обновлено: {formatDate(t.updatedAt)}</span>
                                    <span className="rp-card-versions">Версий: {t.totalVersions}</span>
                                </div>
                            </div>
                            <div className="rp-card-actions">
                                <button
                                    className="rp-btn-icon rp-btn-open"
                                    title="Открыть редактор"
                                    onClick={() => onOpenEditor(t.id)}
                                >
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="16" height="16">
                                        <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                                        <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                                    </svg>
                                </button>
                                <button
                                    className="rp-btn-icon rp-btn-edit"
                                    title="Редактировать свойства"
                                    onClick={() => openEdit(t)}
                                >
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="16" height="16">
                                        <circle cx="12" cy="12" r="3"/>
                                        <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>
                                    </svg>
                                </button>
                                <button
                                    className="rp-btn-icon rp-btn-delete"
                                    title="Удалить"
                                    onClick={() => setDeleteId(t.id)}
                                >
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="16" height="16">
                                        <polyline points="3 6 5 6 21 6"/>
                                        <path d="M19 6l-1 14H6L5 6"/>
                                        <path d="M10 11v6M14 11v6"/>
                                        <path d="M9 6V4h6v2"/>
                                    </svg>
                                </button>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {/* Модальное окно создания/редактирования */}
            {showForm && (
                <div className="rp-modal-overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('form'); }}>
                    <div className="rp-modal" onClick={e => e.stopPropagation()}>
                        <h2 className="rp-modal-title">
                            {editTable ? 'Редактировать таблицу' : 'Новая таблица правил'}
                        </h2>
                        {formError && <div className="rp-modal-error">{formError}</div>}
                        <div className="rp-modal-field">
                            <label>Название *</label>
                            <input
                                className="rp-input"
                                value={formName}
                                onChange={e => setFormName(e.target.value)}
                                placeholder="Название таблицы"
                                autoFocus
                            />
                        </div>
                        <div className="rp-modal-field">
                            <label>Описание</label>
                            <textarea
                                className="rp-input rp-textarea"
                                value={formDesc}
                                onChange={e => setFormDesc(e.target.value)}
                                placeholder="Необязательное описание"
                                rows={3}
                            />
                        </div>
                        <div className="rp-modal-field">
                            <label>Хит-политика</label>
                            <select
                                className="rp-input"
                                value={formPolicy}
                                onChange={e => setFormPolicy(e.target.value as DmnHitPolicy)}
                            >
                                {(Object.keys(HIT_POLICY_LABELS) as DmnHitPolicy[]).map(p => (
                                    <option key={p} value={p}>{HIT_POLICY_LABELS[p]}</option>
                                ))}
                            </select>
                        </div>
                        <div className="rp-modal-actions">
                            <button className={`rp-btn-secondary${sc('form')}`} onClick={() => setShowForm(false)} disabled={saving}>
                                Отмена
                            </button>
                            <button className={`rp-btn-primary${sc('form')}`} onClick={handleSave} disabled={saving}>
                                {saving ? 'Сохранение…' : 'Сохранить'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Подтверждение удаления */}
            {deleteId && (
                <div className="rp-modal-overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('delete'); }}>
                    <div className="rp-modal rp-modal--confirm" onClick={e => e.stopPropagation()}>
                        <h2 className="rp-modal-title">Удалить таблицу?</h2>
                        <p className="rp-modal-text">
                            Это действие необратимо. Таблица с опубликованными версиями не может быть удалена.
                        </p>
                        <div className="rp-modal-actions">
                            <button className={`rp-btn-secondary${sc('delete')}`} onClick={() => setDeleteId(null)} disabled={deleting}>
                                Отмена
                            </button>
                            <button className={`rp-btn-danger${sc('delete')}`} onClick={handleDelete} disabled={deleting}>
                                {deleting ? 'Удаление…' : 'Удалить'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
