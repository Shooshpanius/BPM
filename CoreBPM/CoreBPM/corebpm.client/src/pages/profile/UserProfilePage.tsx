import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/profileApi';
import type { UserProfileDto, UpdateProfileRequest } from '../../api/profileApi';
import './UserProfilePage.css';

interface UserProfilePageProps {
    userId: string;
}

/** Страница просмотра и редактирования профиля пользователя (FR-ORG-02.1). */
export function UserProfilePage({ userId }: UserProfilePageProps) {
    const { accessToken: token, hasRole } = useAuth();
    const isAdmin = hasRole('Admin');

    const [profile, setProfile] = useState<UserProfileDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);
    const [saveError, setSaveError] = useState<string | null>(null);
    const [saved, setSaved] = useState(false);

    // Форма
    const [form, setForm] = useState<UpdateProfileRequest>({});

    useEffect(() => {
        if (!token) return;
        setLoading(true);
        api.getUserProfile(token, userId)
            .then(p => {
                setProfile(p);
                setForm({
                    firstName: p.firstName,
                    lastName: p.lastName,
                    middleName: p.middleName ?? '',
                    displayName: p.displayName,
                    phone: p.phone ?? '',
                    mobilePhone: p.mobilePhone ?? '',
                    internalPhone: p.internalPhone ?? '',
                    personalEmail: p.personalEmail ?? '',
                    bio: p.bio ?? '',
                    birthDate: p.birthDate ?? '',
                    birthDateVisibility: p.birthDateVisibility ?? 'all',
                });
            })
            .catch(e => setError(e.message ?? 'Ошибка загрузки профиля'))
            .finally(() => setLoading(false));
    }, [token, userId]); // eslint-disable-line react-hooks/exhaustive-deps

    const handleChange = (field: keyof UpdateProfileRequest, value: string) => {
        setForm(prev => ({ ...prev, [field]: value }));
        setSaved(false);
    };

    const handleSave = async () => {
        if (!token) return;
        setSaving(true);
        setSaveError(null);
        try {
            const updated = await api.updateUserProfile(token, userId, form);
            setProfile(updated);
            setSaved(true);
        } catch (e: unknown) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handleDeleteAvatar = async () => {
        if (!token) return;
        try {
            await api.deleteUserAvatar(token, userId);
            setProfile(prev => prev ? { ...prev, avatarUrl: undefined } : prev);
        } catch (e: unknown) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка удаления аватара');
        }
    };

    if (loading) return <div className="profile-status">Загрузка…</div>;
    if (error) return <div className="profile-status profile-error">{error}</div>;
    if (!profile) return null;

    const initials = ((profile.firstName?.[0] ?? '') + (profile.lastName?.[0] ?? '')).toUpperCase() || '?';

    return (
        <div className="profile-root">
            <h1 className="profile-title">Профиль пользователя</h1>

            {/* Аватар */}
            <div className="profile-avatar-section">
                <div className="profile-avatar">
                    {profile.avatarUrl
                        ? <img src={profile.avatarUrl} alt={profile.displayName} className="profile-avatar-img" />
                        : <span className="profile-avatar-initials">{initials}</span>
                    }
                </div>
                {profile.avatarUrl && (
                    <button className="profile-avatar-delete" onClick={handleDeleteAvatar} title="Удалить аватар">
                        Удалить фото
                    </button>
                )}
            </div>

            {/* Поля профиля */}
            <div className="profile-form">
                <div className="profile-form-row">
                    <label className="profile-label">Фамилия</label>
                    <input
                        className="profile-input"
                        value={form.lastName ?? ''}
                        onChange={e => handleChange('lastName', e.target.value)}
                    />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Имя</label>
                    <input
                        className="profile-input"
                        value={form.firstName ?? ''}
                        onChange={e => handleChange('firstName', e.target.value)}
                    />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Отчество</label>
                    <input
                        className="profile-input"
                        value={form.middleName ?? ''}
                        onChange={e => handleChange('middleName', e.target.value)}
                    />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Отображаемое имя</label>
                    <input
                        className="profile-input"
                        value={form.displayName ?? ''}
                        onChange={e => handleChange('displayName', e.target.value)}
                    />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Рабочий email</label>
                    <input className="profile-input profile-input--readonly" value={profile.workEmail} readOnly />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Должность</label>
                    <input className="profile-input profile-input--readonly" value={profile.position ?? '—'} readOnly />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Подразделение</label>
                    <input className="profile-input profile-input--readonly" value={profile.department ?? '—'} readOnly />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Организация</label>
                    <input className="profile-input profile-input--readonly" value={profile.organization ?? '—'} readOnly />
                </div>

                <div className="profile-section-divider">Контакты</div>

                <div className="profile-form-row">
                    <label className="profile-label">Рабочий телефон</label>
                    <input
                        className="profile-input"
                        value={form.phone ?? ''}
                        onChange={e => handleChange('phone', e.target.value)}
                        placeholder="+7 (999) 000-00-00"
                    />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Мобильный телефон</label>
                    <input
                        className="profile-input"
                        value={form.mobilePhone ?? ''}
                        onChange={e => handleChange('mobilePhone', e.target.value)}
                        placeholder="+7 (999) 000-00-00"
                    />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Внутренний номер</label>
                    <input
                        className="profile-input"
                        value={form.internalPhone ?? ''}
                        onChange={e => handleChange('internalPhone', e.target.value)}
                        placeholder="123"
                    />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Личный email</label>
                    <input
                        className="profile-input"
                        type="email"
                        value={form.personalEmail ?? ''}
                        onChange={e => handleChange('personalEmail', e.target.value)}
                    />
                </div>

                <div className="profile-section-divider">О себе</div>

                <div className="profile-form-row profile-form-row--textarea">
                    <label className="profile-label">Биография</label>
                    <textarea
                        className="profile-textarea"
                        value={form.bio ?? ''}
                        onChange={e => handleChange('bio', e.target.value)}
                        rows={4}
                        placeholder="Расскажите о себе…"
                    />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Дата рождения</label>
                    <input
                        className="profile-input"
                        type="date"
                        value={form.birthDate ?? ''}
                        onChange={e => handleChange('birthDate', e.target.value)}
                    />
                </div>
                <div className="profile-form-row">
                    <label className="profile-label">Кому видна дата рождения</label>
                    <select
                        className="profile-select"
                        value={form.birthDateVisibility ?? 'all'}
                        onChange={e => handleChange('birthDateVisibility', e.target.value)}
                    >
                        <option value="all">Всем</option>
                        <option value="department">Только подразделению</option>
                        <option value="admin">Только администратору</option>
                    </select>
                </div>
            </div>

            {saveError && <div className="profile-save-error">{saveError}</div>}
            {saved && <div className="profile-save-ok">Профиль сохранён</div>}

            <div className="profile-actions">
                <button
                    className="profile-save-btn"
                    onClick={handleSave}
                    disabled={saving}
                >
                    {saving ? 'Сохранение…' : 'Сохранить'}
                </button>
            </div>

            {/* Поле только для администраторов — неактивный пользователь */}
            {isAdmin && !profile.isActive && (
                <div className="profile-inactive-badge">Пользователь неактивен</div>
            )}
        </div>
    );
}
