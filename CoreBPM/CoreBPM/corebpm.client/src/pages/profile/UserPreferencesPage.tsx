import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/preferencesApi';
import type { UserPreferencesDto, UpdatePreferencesRequest } from '../../api/preferencesApi';
import './UserPreferencesPage.css';

interface UserPreferencesPageProps {
    userId: string;
}

const LANGUAGES = [
    { value: 'ru', label: 'Русский' },
    { value: 'en', label: 'English' },
];

const THEMES = [
    { value: 'system', label: 'Системная' },
    { value: 'light', label: 'Светлая' },
    { value: 'dark', label: 'Тёмная' },
];

const PAGE_SIZES = [10, 25, 50, 100];

/** Страница настроек пользователя (FR-ORG-02.3). */
export function UserPreferencesPage({ userId }: UserPreferencesPageProps) {
    const { accessToken: token } = useAuth();

    const [prefs, setPrefs] = useState<UserPreferencesDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);
    const [saveError, setSaveError] = useState<string | null>(null);
    const [saved, setSaved] = useState(false);

    const [form, setForm] = useState<UpdatePreferencesRequest>({});

    useEffect(() => {
        if (!token) return;
        setLoading(true);
        api.getUserPreferences(token, userId)
            .then(p => {
                setPrefs(p);
                setForm({
                    language: p.language,
                    timeZone: p.timeZone ?? '',
                    theme: p.theme,
                    dateFormat: p.dateFormat ?? '',
                    pageSize: p.pageSize,
                });
            })
            .catch(e => setError(e.message ?? 'Ошибка загрузки настроек'))
            .finally(() => setLoading(false));
    }, [token, userId]); // eslint-disable-line react-hooks/exhaustive-deps

    const handleChange = <K extends keyof UpdatePreferencesRequest>(field: K, value: UpdatePreferencesRequest[K]) => {
        setForm(prev => ({ ...prev, [field]: value }));
        setSaved(false);
    };

    const handleSave = async () => {
        if (!token) return;
        setSaving(true);
        setSaveError(null);
        try {
            const updated = await api.updateUserPreferences(token, userId, form);
            setPrefs(updated);
            setSaved(true);
        } catch (e: unknown) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    if (loading) return <div className="prefs-status">Загрузка…</div>;
    if (error) return <div className="prefs-status prefs-error">{error}</div>;
    if (!prefs) return null;

    return (
        <div className="prefs-root">
            <h1 className="prefs-title">Настройки</h1>

            <div className="prefs-form">
                <div className="prefs-section-label">Интерфейс</div>

                <div className="prefs-row">
                    <label className="prefs-label">Язык</label>
                    <select
                        className="prefs-select"
                        value={form.language ?? 'ru'}
                        onChange={e => handleChange('language', e.target.value)}
                    >
                        {LANGUAGES.map(l => (
                            <option key={l.value} value={l.value}>{l.label}</option>
                        ))}
                    </select>
                </div>

                <div className="prefs-row">
                    <label className="prefs-label">Тема оформления</label>
                    <select
                        className="prefs-select"
                        value={form.theme ?? 'system'}
                        onChange={e => handleChange('theme', e.target.value)}
                    >
                        {THEMES.map(t => (
                            <option key={t.value} value={t.value}>{t.label}</option>
                        ))}
                    </select>
                </div>

                <div className="prefs-row">
                    <label className="prefs-label">Размер страницы</label>
                    <select
                        className="prefs-select"
                        value={String(form.pageSize ?? 25)}
                        onChange={e => handleChange('pageSize', Number(e.target.value))}
                    >
                        {PAGE_SIZES.map(s => (
                            <option key={s} value={String(s)}>{s} записей</option>
                        ))}
                    </select>
                </div>

                <div className="prefs-section-label">Региональные</div>

                <div className="prefs-row">
                    <label className="prefs-label">Часовой пояс</label>
                    <input
                        className="prefs-input"
                        value={form.timeZone ?? ''}
                        onChange={e => handleChange('timeZone', e.target.value)}
                        placeholder="Europe/Moscow"
                    />
                </div>

                <div className="prefs-row">
                    <label className="prefs-label">Формат даты</label>
                    <input
                        className="prefs-input"
                        value={form.dateFormat ?? ''}
                        onChange={e => handleChange('dateFormat', e.target.value)}
                        placeholder="dd.MM.yyyy"
                    />
                </div>
            </div>

            {saveError && <div className="prefs-save-error">{saveError}</div>}
            {saved && <div className="prefs-save-ok">Настройки сохранены</div>}

            <div className="prefs-actions">
                <button
                    className="prefs-save-btn"
                    onClick={handleSave}
                    disabled={saving}
                >
                    {saving ? 'Сохранение…' : 'Сохранить'}
                </button>
            </div>
        </div>
    );
}
