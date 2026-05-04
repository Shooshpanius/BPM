import { useBpmNotifications } from '../../context/BpmNotificationsContext';

export function NotificationsWidget() {
    const { notifications } = useBpmNotifications();
    const recent = notifications.slice(0, 6);

    return (
        <div className="portal-widget__content">
            {recent.length === 0
                ? <p className="widget-empty">Нет уведомлений</p>
                : <ul className="widget-notification-list">
                    {recent.map((n, i) => (
                        <li key={i} className="widget-notification-item">
                            <span className="widget-notif-type">{n.type}</span>
                            <span className="widget-notif-msg">{n.message}</span>
                        </li>
                    ))}
                </ul>
            }
        </div>
    );
}
