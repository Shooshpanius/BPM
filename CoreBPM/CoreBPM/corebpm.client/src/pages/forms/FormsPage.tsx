import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/formsApi';
import type { FormListItemDto } from '../../api/formsApi';
import './FormsPage.css';

const STATUS_LABELS: Record<string, string> = {
    Draft: 'Черновик',
    Published: 'Опубликована',
    Archived: 'Архив',
};

interface FormsPageProps {
    onOpenBuilder: (formId: string) => void;
}

/** Страница списка форм задач (FR-BPM-01.4). */
export function FormsPage({ onOpenBuilder }: FormsPageProps) {
    const { accessToken: token } = useAuth();

    const [forms, setForms] = useState<FormListItemDto[]>([]);
    const [search, setSearch] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Диалог создания / редактирования
    const [showForm, setShowForm] = useState(false);
    const [editForm, setEditForm] = useState<FormListItemDto | null>(null);
    const [formName, setFormName] = useState('');
    const [formDesc, setFormDesc] = useState('');
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

    const loadForms = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.getForms(token);
            setForms(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token]);

    useEffect(() => { loadForms(); }, [loadForms]);

    const filteredForms = forms.filter(f =>
        f.name.toLowerCase().includes(search.toLowerCase()) ||
        (f.description ?? '').toLowerCase().includes(search.toLowerCase())
    );

    const openCreate = () => {
        setEditForm(null);
        setFormName('');
        setFormDesc('');
        setFormError(null);
        setShowForm(true);
    };

    const openEdit = (f: FormListItemDto) => {
        setEditForm(f);
        setFormName(f.name);
        setFormDesc(f.description ?? '');
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
            const req = { name: formName.trim(), description: formDesc.trim() || undefined };
            if (editForm) {
                await api.updateForm(token, editForm.id, req);
            } else {
                const created = await api.createForm(token, req);
                setShowForm(false);
                await loadForms();
                onOpenBuilder(created.id);
                return;
            }
            setShowForm(false);
            await loadForms();
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
            await api.deleteForm(token, deleteId);
            setDeleteId(null);
            await loadForms();
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
        <div className="fp-root">
            <div className="fp-header">
                <h1 className="fp-title">Формы задач</h1>
                <button className="fp-btn-primary" onClick={openCreate}>
                    + Создать форму
                </button>
            </div>

            <div className="fp-search-row">
                <input
                    className="fp-search"
                    type="search"
                    placeholder="Поиск по названию…"
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                />
            </div>

            {error && <div className="fp-error">{error}</div>}

            {loading ? (
                <div className="fp-loading">Загрузка…</div>
            ) : filteredForms.length === 0 ? (
                <div className="fp-empty">
                    <div className="fp-empty-icon">📝</div>
                    <h2 className="fp-empty-title">{search ? 'Ничего не найдено' : 'Форм нет'}</h2>
                    {!search && (
                        <>
                            <p className="fp-empty-sub">Создайте первую форму для задач бизнес-процессов</p>
                            <button className="fp-btn-primary" onClick={openCreate}>Создать форму</button>
                        </>
                    )}
                </div>
            ) : (
                <div className="fp-list">
                    {filteredForms.map(f => (
                        <div key={f.id} className="fp-card">
                            <div
                                className="fp-card-main"
                                role="button"
                                tabIndex={0}
                                onClick={() => onOpenBuilder(f.id)}
                                onKeyDown={e => e.key === 'Enter' && onOpenBuilder(f.id)}
                            >
                                <div className="fp-card-name">{f.name}</div>
                                {f.description && <div className="fp-card-desc">{f.description}</div>}
                                <div className="fp-card-meta">
                                    {f.latestVersionStatus && (
                                        <span className={`fp-badge fp-badge--${f.latestVersionStatus.toLowerCase()}`}>
                                            {STATUS_LABELS[f.latestVersionStatus] ?? f.latestVersionStatus}
                                        </span>
                                    )}
                                    <span className="fp-card-date">Обновлено: {formatDate(f.updatedAt)}</span>
                                    <span className="fp-card-versions">Версий: {f.totalVersions}</span>
                                </div>
                            </div>
                            <div className="fp-card-actions">
                                <button
                                    className="fp-btn-icon fp-btn-open"
                                    title="Открыть конструктор"
                                    onClick={() => onOpenBuilder(f.id)}
                                >
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="16" height="16">
                                        <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                                        <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                                    </svg>
                                </button>
                                <button
                                    className="fp-btn-icon fp-btn-edit"
                                    title="Переименовать"
                                    onClick={() => openEdit(f)}
                                >
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="16" height="16">
                                        <circle cx="12" cy="12" r="3"/>
                                        <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>
                                    </svg>
                                </button>
                                <button
                                    className="fp-btn-icon fp-btn-delete"
                                    title="Удалить"
                                    onClick={() => setDeleteId(f.id)}
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
                <div className="fp-modal-overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('form'); }}>
                    <div className="fp-modal" onClick={e => e.stopPropagation()}>
                        <h2 className="fp-modal-title">
                            {editForm ? 'Редактировать форму' : 'Новая форма задачи'}
                        </h2>
                        {formError && <div className="fp-modal-error">{formError}</div>}
                        <div className="fp-modal-field">
                            <label>Название *</label>
                            <input
                                className="fp-input"
                                value={formName}
                                onChange={e => setFormName(e.target.value)}
                                placeholder="Название формы"
                                autoFocus
                            />
                        </div>
                        <div className="fp-modal-field">
                            <label>Описание</label>
                            <textarea
                                className="fp-input fp-textarea"
                                value={formDesc}
                                onChange={e => setFormDesc(e.target.value)}
                                placeholder="Необязательное описание"
                                rows={3}
                            />
                        </div>
                        <div className="fp-modal-actions">
                            <button className={`fp-btn-secondary${sc('form')}`} onClick={() => setShowForm(false)} disabled={saving}>
                                Отмена
                            </button>
                            <button className={`fp-btn-primary${sc('form')}`} onClick={handleSave} disabled={saving}>
                                {saving ? 'Сохранение…' : editForm ? 'Сохранить' : 'Создать и открыть'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Подтверждение удаления */}
            {deleteId && (
                <div className="fp-modal-overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('delete'); }}>
                    <div className="fp-modal fp-modal--confirm" onClick={e => e.stopPropagation()}>
                        <h2 className="fp-modal-title">Удалить форму?</h2>
                        <p className="fp-modal-text">
                            Это действие необратимо. Форма с опубликованными версиями не может быть удалена.
                        </p>
                        <div className="fp-modal-actions">
                            <button className={`fp-btn-secondary${sc('delete')}`} onClick={() => setDeleteId(null)} disabled={deleting}>
                                Отмена
                            </button>
                            <button className={`fp-btn-danger${sc('delete')}`} onClick={handleDelete} disabled={deleting}>
                                {deleting ? 'Удаление…' : 'Удалить'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
