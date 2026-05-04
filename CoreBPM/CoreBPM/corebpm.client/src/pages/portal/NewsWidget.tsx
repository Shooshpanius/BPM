import { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getCompanyNews, type CompanyNewsDto } from '../../api/companyApi';

export function NewsWidget() {
    const { accessToken } = useAuth();
    const [news, setNews] = useState<CompanyNewsDto[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        if (!accessToken) return;
        getCompanyNews(accessToken).then(setNews).catch(() => {}).finally(() => setLoading(false));
    }, [accessToken]);

    if (loading) return <div className="widget-loading">Загрузка...</div>;

    return (
        <div className="portal-widget__content">
            {news.length === 0
                ? <p className="widget-empty">Нет новостей</p>
                : <ul className="widget-news-list">
                    {news.slice(0, 4).map(n => (
                        <li key={n.id} className="widget-news-item">
                            <span className="widget-news-title">{n.title}</span>
                            <span className="widget-news-date">
                                {new Date(n.createdAt).toLocaleDateString('ru-RU')}
                            </span>
                        </li>
                    ))}
                </ul>
            }
        </div>
    );
}
