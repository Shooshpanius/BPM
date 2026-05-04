import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/companyApi';
import type { CompanyInfoDto, CompanyNewsDto, CompanyLinkDto } from '../../api/companyApi';
import './CompanyPage.css';

/** Страница компании (FR-ORG-03). */
export function CompanyPage() {
    const { accessToken: token, hasRole } = useAuth();
    const isAdmin = hasRole('Admin');

    const [info, setInfo] = useState<CompanyInfoDto | null>(null);
    const [news, setNews] = useState<CompanyNewsDto[]>([]);
    const [links, setLinks] = useState<CompanyLinkDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // Редактирование информации о компании
    const [editInfo, setEditInfo] = useState(false);
    const [infoForm, setInfoForm] = useState<api.UpdateCompanyInfoRequest>({});
    const [infoSaving, setInfoSaving] = useState(false);

    // Новость
    const [showNewsForm, setShowNewsForm] = useState(false);
    const [newsForm, setNewsForm] = useState<api.CreateNewsRequest>({ title: '', content: '', isPublished: false });
    const [newsEditId, setNewsEditId] = useState<string | null>(null);
    const [newsSaving, setNewsSaving] = useState(false);

    useEffect(() => {
        if (!token) return;
        setLoading(true);
        Promise.all([
            api.getCompanyInfo(token),
            api.getCompanyNews(token),
            api.getCompanyLinks(token),
        ])
            .then(([i, n, l]) => {
                setInfo(i);
                setNews(n);
                setLinks(l);
                setInfoForm({ name: i.name, description: i.description ?? '', phone: i.phone ?? '', email: i.email ?? '', address: i.address ?? '', website: i.website ?? '' });
            })
            .catch(e => setError(e.message ?? 'Ошибка загрузки'))
            .finally(() => setLoading(false));
    }, [token]); // eslint-disable-line react-hooks/exhaustive-deps

    const handleSaveInfo = async () => {
        if (!token) return;
        setInfoSaving(true);
        try {
            const updated = await api.updateCompanyInfo(token, infoForm);
            setInfo(updated);
            setEditInfo(false);
        } finally {
            setInfoSaving(false);
        }
    };

    const handleNewsSubmit = async () => {
        if (!token) return;
        setNewsSaving(true);
        try {
            if (newsEditId) {
                const updated = await api.updateCompanyNews(token, newsEditId, newsForm);
                setNews(prev => prev.map(n => n.id === newsEditId ? updated : n));
            } else {
                const created = await api.createCompanyNews(token, newsForm);
                setNews(prev => [created, ...prev]);
            }
            setShowNewsForm(false);
            setNewsEditId(null);
            setNewsForm({ title: '', content: '', isPublished: false });
        } finally {
            setNewsSaving(false);
        }
    };

    const handleDeleteNews = async (id: string) => {
        if (!token) return;
        await api.deleteCompanyNews(token, id);
        setNews(prev => prev.filter(n => n.id !== id));
    };

    const openEditNews = (item: CompanyNewsDto) => {
        setNewsEditId(item.id);
        setNewsForm({ title: item.title, content: item.content, isPublished: item.isPublished });
        setShowNewsForm(true);
    };

    if (loading) return <div className="company-status">Загрузка…</div>;
    if (error) return <div className="company-status company-error">{error}</div>;

    return (
        <div className="company-root">
            {/* Блок информации о компании */}
            <div className="company-card">
                <div className="company-card-header">
                    <h2 className="company-card-title">О компании</h2>
                    {isAdmin && !editInfo && (
                        <button className="company-btn company-btn--ghost" onClick={() => setEditInfo(true)}>Редактировать</button>
                    )}
                </div>

                {editInfo ? (
                    <div className="company-form">
                        <div className="company-form-row">
                            <label className="company-label">Название</label>
                            <input className="company-input" value={infoForm.name ?? ''} onChange={e => setInfoForm(f => ({ ...f, name: e.target.value }))} />
                        </div>
                        <div className="company-form-row">
                            <label className="company-label">Описание</label>
                            <textarea className="company-textarea" value={infoForm.description ?? ''} onChange={e => setInfoForm(f => ({ ...f, description: e.target.value }))} rows={3} />
                        </div>
                        <div className="company-form-row">
                            <label className="company-label">Телефон</label>
                            <input className="company-input" value={infoForm.phone ?? ''} onChange={e => setInfoForm(f => ({ ...f, phone: e.target.value }))} />
                        </div>
                        <div className="company-form-row">
                            <label className="company-label">Email</label>
                            <input className="company-input" type="email" value={infoForm.email ?? ''} onChange={e => setInfoForm(f => ({ ...f, email: e.target.value }))} />
                        </div>
                        <div className="company-form-row">
                            <label className="company-label">Адрес</label>
                            <input className="company-input" value={infoForm.address ?? ''} onChange={e => setInfoForm(f => ({ ...f, address: e.target.value }))} />
                        </div>
                        <div className="company-form-row">
                            <label className="company-label">Сайт</label>
                            <input className="company-input" type="url" value={infoForm.website ?? ''} onChange={e => setInfoForm(f => ({ ...f, website: e.target.value }))} />
                        </div>
                        <div className="company-form-actions">
                            <button className="company-btn company-btn--primary" onClick={handleSaveInfo} disabled={infoSaving}>
                                {infoSaving ? 'Сохранение…' : 'Сохранить'}
                            </button>
                            <button className="company-btn company-btn--ghost" onClick={() => setEditInfo(false)}>Отмена</button>
                        </div>
                    </div>
                ) : (
                    <div className="company-info-view">
                        {info?.logoUrl && <img src={info.logoUrl} alt={info?.name} className="company-logo" />}
                        <h1 className="company-name">{info?.name}</h1>
                        {info?.description && <p className="company-description">{info.description}</p>}
                        <div className="company-contacts">
                            {info?.phone && <span>📞 {info.phone}</span>}
                            {info?.email && <a href={`mailto:${info.email}`}>{info.email}</a>}
                            {info?.address && <span>📍 {info.address}</span>}
                            {info?.website && <a href={info.website} target="_blank" rel="noopener noreferrer">{info.website}</a>}
                        </div>
                    </div>
                )}
            </div>

            {/* Полезные ссылки */}
            {links.length > 0 && (
                <div className="company-card">
                    <div className="company-card-header">
                        <h2 className="company-card-title">Полезные ссылки</h2>
                    </div>
                    <ul className="company-links-list">
                        {links.map(l => (
                            <li key={l.id}>
                                <a href={l.url} target="_blank" rel="noopener noreferrer" className="company-link">{l.title}</a>
                            </li>
                        ))}
                    </ul>
                </div>
            )}

            {/* Новости */}
            <div className="company-card">
                <div className="company-card-header">
                    <h2 className="company-card-title">Новости</h2>
                    {isAdmin && (
                        <button className="company-btn company-btn--primary" onClick={() => { setShowNewsForm(true); setNewsEditId(null); setNewsForm({ title: '', content: '', isPublished: false }); }}>
                            + Добавить
                        </button>
                    )}
                </div>

                {showNewsForm && (
                    <div className="company-form company-news-form">
                        <div className="company-form-row">
                            <label className="company-label">Заголовок</label>
                            <input className="company-input" value={newsForm.title} onChange={e => setNewsForm(f => ({ ...f, title: e.target.value }))} />
                        </div>
                        <div className="company-form-row">
                            <label className="company-label">Текст</label>
                            <textarea className="company-textarea" value={newsForm.content} onChange={e => setNewsForm(f => ({ ...f, content: e.target.value }))} rows={4} />
                        </div>
                        <div className="company-form-row company-form-row--checkbox">
                            <label className="company-label">Опубликовать</label>
                            <input type="checkbox" checked={newsForm.isPublished} onChange={e => setNewsForm(f => ({ ...f, isPublished: e.target.checked }))} />
                        </div>
                        <div className="company-form-actions">
                            <button className="company-btn company-btn--primary" onClick={handleNewsSubmit} disabled={newsSaving}>
                                {newsSaving ? 'Сохранение…' : newsEditId ? 'Обновить' : 'Создать'}
                            </button>
                            <button className="company-btn company-btn--ghost" onClick={() => { setShowNewsForm(false); setNewsEditId(null); }}>Отмена</button>
                        </div>
                    </div>
                )}

                {news.length === 0 && !showNewsForm && (
                    <p className="company-empty">Новостей пока нет.</p>
                )}

                {news.map(item => (
                    <div key={item.id} className={`company-news-item${item.isPublished ? '' : ' company-news-item--draft'}`}>
                        <div className="company-news-header">
                            <span className="company-news-title">{item.title}</span>
                            <span className="company-news-date">{new Date(item.createdAt).toLocaleDateString('ru-RU')}</span>
                            {!item.isPublished && <span className="company-news-draft">Черновик</span>}
                        </div>
                        <p className="company-news-content">{item.content}</p>
                        {isAdmin && (
                            <div className="company-news-actions">
                                <button className="company-btn company-btn--ghost company-btn--sm" onClick={() => openEditNews(item)}>Редактировать</button>
                                <button className="company-btn company-btn--danger company-btn--sm" onClick={() => handleDeleteNews(item.id)}>Удалить</button>
                            </div>
                        )}
                    </div>
                ))}
            </div>
        </div>
    );
}
