import {
    createContext,
    useContext,
    useEffect,
    useRef,
    useState,
    type ReactNode,
} from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from './AuthContext';

// ─── Типы ─────────────────────────────────────────────────────────────────────

export interface BpmNotification {
    id: string;
    type: string;
    message: string;
    occurredAt: Date;
    read: boolean;
    /** Дополнительные данные (зависят от типа). */
    payload?: unknown;
}

interface BpmNotificationsCtx {
    notifications: BpmNotification[];
    unreadCount: number;
    markRead: (id: string) => void;
    clearAll: () => void;
}

// ─── Контекст ─────────────────────────────────────────────────────────────────

const BpmNotificationsContext = createContext<BpmNotificationsCtx>({
    notifications: [],
    unreadCount: 0,
    markRead: () => {},
    clearAll: () => {},
});

// ─── Провайдер ────────────────────────────────────────────────────────────────

/** Провайдер SignalR-уведомлений BPM. Оборачивает корневой компонент приложения. */
export function BpmNotificationsProvider({ children }: { children: ReactNode }) {
    const { accessToken, isAuthenticated } = useAuth();
    const [notifications, setNotifications] = useState<BpmNotification[]>([]);
    const connectionRef = useRef<signalR.HubConnection | null>(null);

    useEffect(() => {
        if (!isAuthenticated || !accessToken) return;

        const conn = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/bpm-notifications', {
                accessTokenFactory: () => accessToken,
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connectionRef.current = conn;

        conn.on('bpm:notification', (data: Record<string, unknown>) => {
            const notification = buildNotification(data);
            setNotifications(prev => [notification, ...prev].slice(0, 100));
        });

        conn.start().catch(() => {
            // Подключение по SignalR необязательно; при недоступности просто молчим
        });

        return () => {
            conn.stop();
            connectionRef.current = null;
        };
    }, [isAuthenticated, accessToken]);

    const markRead = (id: string) =>
        setNotifications(prev => prev.map(n => n.id === id ? { ...n, read: true } : n));

    const clearAll = () => setNotifications([]);

    const unreadCount = notifications.filter(n => !n.read).length;

    return (
        <BpmNotificationsContext.Provider value={{ notifications, unreadCount, markRead, clearAll }}>
            {children}
            <BpmNotificationToast notifications={notifications.filter(n => !n.read).slice(0, 3)} onClose={markRead} />
        </BpmNotificationsContext.Provider>
    );
}

// ─── Хук ─────────────────────────────────────────────────────────────────────

export function useBpmNotifications(): BpmNotificationsCtx {
    return useContext(BpmNotificationsContext);
}

// ─── Компонент тоста ─────────────────────────────────────────────────────────

function BpmNotificationToast({
    notifications,
    onClose,
}: {
    notifications: BpmNotification[];
    onClose: (id: string) => void;
}) {
    if (notifications.length === 0) return null;

    return (
        <div style={{
            position: 'fixed',
            bottom: 24,
            right: 24,
            zIndex: 9999,
            display: 'flex',
            flexDirection: 'column',
            gap: 8,
            maxWidth: 340,
        }}>
            {notifications.map(n => (
                <div
                    key={n.id}
                    role="alert"
                    style={{
                        background: '#1f2937',
                        color: '#f9fafb',
                        borderRadius: 8,
                        padding: '10px 14px',
                        fontSize: 13,
                        display: 'flex',
                        gap: 10,
                        alignItems: 'flex-start',
                        boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
                        borderLeft: '4px solid ' + toastColor(n.type),
                    }}
                >
                    <span style={{ lineHeight: 1.4, flex: 1 }}>{n.message}</span>
                    <button
                        onClick={() => onClose(n.id)}
                        aria-label="Закрыть"
                        style={{
                            background: 'none',
                            border: 'none',
                            color: '#9ca3af',
                            cursor: 'pointer',
                            padding: 0,
                            fontSize: 16,
                            lineHeight: 1,
                        }}
                    >✕</button>
                </div>
            ))}
        </div>
    );
}

// ─── Утилиты ─────────────────────────────────────────────────────────────────

function buildNotification(data: Record<string, unknown>): BpmNotification {
    const type = typeof data.type === 'string' ? data.type : 'Info';
    let message = '';

    switch (type) {
        case 'JobFailed':
            message = `Ошибка задания: ${data.processName ?? ''}${data.instanceName ? ` / ${data.instanceName}` : ''}`;
            if (data.error) message += ` — ${data.error}`;
            break;
        case 'MigrationPackageCompleted':
            if (data.hasErrors) {
                message = `Пакет миграции «${data.packageName ?? ''}» завершён с ошибками: переведено ${data.migrated ?? 0} / ${data.total ?? 0}, с ошибками ${data.failed ?? 0}`;
            } else {
                message = `Пакет миграции «${data.packageName ?? ''}» успешно завершён: переведено ${data.migrated ?? 0} из ${data.total ?? 0}`;
            }
            break;
        case 'ImprovementStatusChanged': {
            const statusLabels: Record<string, string> = {
                Accepted: 'принято',
                Rejected: 'отклонено',
                Completed: 'завершено',
            };
            const statusLabel = typeof data.newStatus === 'string'
                ? (statusLabels[data.newStatus] ?? data.newStatus)
                : '';
            message = `Предложение «${data.subject ?? ''}» по процессу «${data.processName ?? ''}» ${statusLabel}`;
            break;
        }
        default:
            message = typeof data.message === 'string' ? data.message : `Уведомление: ${type}`;
    }

    return {
        id: crypto.randomUUID(),
        type,
        message,
        occurredAt: new Date(),
        read: false,
        payload: data,
    };
}

function toastColor(type: string): string {
    switch (type) {
        case 'JobFailed': return '#ef4444';
        case 'MigrationPackageCompleted': return '#10b981';
        case 'ImprovementStatusChanged': return '#f59e0b';
        default: return '#3b82f6';
    }
}
