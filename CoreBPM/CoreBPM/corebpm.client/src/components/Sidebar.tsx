import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import { getTaskCounters, type TaskCountersDto } from '../api/tasksApi';
import { getUnreadCount } from '../api/messagesApi';
import { useBpmNotifications } from '../context/BpmNotificationsContext';
import { APP_VERSION, LAST_PR_DATE } from '../version';
import './Sidebar.css';

export type SidebarSection = 'portal' | 'notifications' | 'tasks' | 'tasks-periodic' | 'tasks-dashboard' | 'contacts' | 'org-structure' | 'company' | 'user-profile' | 'user-preferences' | 'bpm-processes' | 'bpm-my-processes' | 'bpm-monitor' | 'bpm-queue' | 'bpm-documentation' | 'bpm-rules' | 'bpm-forms' | 'bpm-scripts' | 'bpm-migration' | 'bpm-improvements' | 'bpm-analytics' | 'task-control-settings' | 'timelogs-report' | 'notification-settings' | 'smtp-settings' | 'email-templates' | 'sms-settings' | 'push-settings' | 'notification-templates' | 'notification-logs' | 'notification-stats' | 'messages' | 'channels';

/** Идентификаторы групп в боковом меню */
type GroupId = 'tasks' | 'communication' | 'org' | 'bpm' | 'admin';

/** Принадлежность разделов к группам боковой навигации */
const SECTION_GROUP: Partial<Record<SidebarSection, GroupId>> = {
    tasks: 'tasks', 'tasks-periodic': 'tasks', 'tasks-dashboard': 'tasks',
    messages: 'communication', channels: 'communication', notifications: 'communication',
    contacts: 'org', company: 'org', 'user-profile': 'org', 'user-preferences': 'org',
    'notification-settings': 'org',
    'bpm-processes': 'bpm', 'bpm-my-processes': 'bpm', 'bpm-monitor': 'bpm',
    'bpm-queue': 'bpm', 'bpm-migration': 'bpm', 'bpm-documentation': 'bpm',
    'bpm-rules': 'bpm', 'bpm-forms': 'bpm', 'bpm-scripts': 'bpm',
    'bpm-improvements': 'bpm', 'bpm-analytics': 'bpm',
    'org-structure': 'admin', 'task-control-settings': 'admin', 'timelogs-report': 'admin',
    'smtp-settings': 'admin', 'email-templates': 'admin', 'sms-settings': 'admin',
    'push-settings': 'admin', 'notification-templates': 'admin',
    'notification-logs': 'admin', 'notification-stats': 'admin',
};

interface SidebarProps {
    active: SidebarSection;
    onSelect: (section: SidebarSection) => void;
}

/** Вертикальный сайдбар навигации (~60px) с иконками разделов. */
export function Sidebar({ active, onSelect }: SidebarProps) {
    const { hasRole, accessToken } = useAuth();
    const canManageOrg = hasRole('Admin') || hasRole('HR');
    const [counters, setCounters] = useState<TaskCountersDto | null>(null);
    const [unreadMessages, setUnreadMessages] = useState(0);
    const { notifications, unreadCount: inboxUnread } = useBpmNotifications();

    // По умолчанию все группы свёрнуты; при открытии одной — остальные закрываются (accordion)
    const [expandedGroups, setExpandedGroups] = useState<Set<GroupId>>(() => {
        const initial = SECTION_GROUP[active as SidebarSection];
        return initial ? new Set<GroupId>([initial]) : new Set<GroupId>();
    });

    /** Первый пункт каждой группы с учётом ролей текущего пользователя */
    const getGroupFirstItem = useCallback((groupId: GroupId): SidebarSection | null => {
        switch (groupId) {
            case 'tasks':         return 'tasks';
            case 'communication': return 'messages';
            case 'org':           return 'contacts';
            case 'bpm':           return 'bpm-processes';
            case 'admin':
                if (canManageOrg) return 'org-structure';
                if (hasRole('Admin')) return 'task-control-settings';
                return null;
            default:              return null;
        }
    }, [canManageOrg, hasRole]);

    const toggleGroup = (groupId: GroupId) => {
        if (expandedGroups.has(groupId)) {
            // закрываем текущую
            setExpandedGroups(new Set<GroupId>());
        } else {
            // открываем только эту, остальные закрываем
            setExpandedGroups(new Set<GroupId>([groupId]));
            // переходим на первый пункт группы
            const firstItem = getGroupFirstItem(groupId);
            if (firstItem) onSelect(firstItem);
        }
    };

    const loadCounters = useCallback(() => {
        if (!accessToken) return;
        getTaskCounters(accessToken).then(c => setCounters(c)).catch(() => {});
    }, [accessToken]);

    const loadUnreadMessages = useCallback(() => {
        if (!accessToken) return;
        getUnreadCount(accessToken).then(r => setUnreadMessages(r.totalUnread)).catch(() => {});
    }, [accessToken]);

    // При смене активного раздела — раскрываем только его группу (accordion)
    useEffect(() => {
        const group = SECTION_GROUP[active];
        if (group) {
            setExpandedGroups(new Set<GroupId>([group]));
        }
    }, [active]);

    // FR-TASK-02.2: Периодическое обновление счётчиков задач (fallback каждые 5 минут)
    useEffect(() => {
        if (!accessToken) return;
        loadCounters();
        const id = setInterval(loadCounters, 300_000);
        return () => clearInterval(id);
    }, [accessToken, loadCounters]);

    // FR-MSG-01: Периодическое обновление счётчика непрочитанных сообщений (каждые 60 секунд)
    useEffect(() => {
        if (!accessToken) return;
        loadUnreadMessages();
        const id = setInterval(loadUnreadMessages, 60_000);
        return () => clearInterval(id);
    }, [accessToken, loadUnreadMessages]);

    // FR-TASK-02.2: Push-обновление счётчиков при получении SignalR-события TaskCountersUpdated
    useEffect(() => {
        const last = notifications[0];
        if (last?.type === 'TaskCountersUpdated') {
            loadCounters();
        }
        // FR-MSG-01: Push-обновление счётчика непрочитанных при получении нового сообщения
        if (last?.type === 'NewMessage') {
            loadUnreadMessages();
        }
    }, [notifications, loadCounters, loadUnreadMessages]);

    return (
        <nav className="sidebar" aria-label="Разделы системы">
            {/* FR-PORTAL-01: Главная страница — всегда видима */}
            <SidebarItem
                id="portal"
                label="Главная"
                active={active === 'portal'}
                onClick={() => onSelect('portal')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>
                        <polyline points="9 22 9 12 15 12 15 22"/>
                    </svg>
                }
            />

            {/* Группа: Задачи */}
            <div className="sidebar-divider" role="separator" />
            <SidebarGroup
                id="tasks"
                label="Задачи"
                expanded={expandedGroups.has('tasks')}
                onToggle={() => toggleGroup('tasks')}
                hasActive={SECTION_GROUP[active] === 'tasks'}
                badge={!expandedGroups.has('tasks') && counters && counters.incoming > 0 ? counters.incoming : undefined}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <polyline points="9 11 12 14 22 4"/>
                        <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>
                    </svg>
                }
            >
                <SidebarItem
                    id="tasks"
                    label="Задачи"
                    active={active === 'tasks'}
                    onClick={() => onSelect('tasks')}
                    badge={counters && counters.incoming > 0 ? counters.incoming : undefined}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <polyline points="9 11 12 14 22 4"/>
                            <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="tasks-periodic"
                    label="Периодические"
                    active={active === 'tasks-periodic'}
                    onClick={() => onSelect('tasks-periodic')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M17 2.1l4 4-4 4"/>
                            <path d="M3 12.2v-2a4 4 0 0 1 4-4h12.8M7 21.9l-4-4 4-4"/>
                            <path d="M21 11.8v2a4 4 0 0 1-4 4H4.2"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="tasks-dashboard"
                    label="Дашборд"
                    active={active === 'tasks-dashboard'}
                    onClick={() => onSelect('tasks-dashboard')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <rect x="3" y="3" width="7" height="7"/>
                            <rect x="14" y="3" width="7" height="7"/>
                            <rect x="14" y="14" width="7" height="7"/>
                            <rect x="3" y="14" width="7" height="7"/>
                        </svg>
                    }
                />
            </SidebarGroup>

            {/* Группа: Общение */}
            <div className="sidebar-divider" role="separator" />
            <SidebarGroup
                id="communication"
                label="Общение"
                expanded={expandedGroups.has('communication')}
                onToggle={() => toggleGroup('communication')}
                hasActive={SECTION_GROUP[active] === 'communication'}
                badge={!expandedGroups.has('communication') && (unreadMessages + inboxUnread) > 0 ? unreadMessages + inboxUnread : undefined}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
                    </svg>
                }
            >
                <SidebarItem
                    id="messages"
                    label="Сообщения"
                    active={active === 'messages'}
                    onClick={() => onSelect('messages')}
                    badge={unreadMessages > 0 ? unreadMessages : undefined}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="channels"
                    label="Каналы"
                    active={active === 'channels'}
                    onClick={() => onSelect('channels')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.69 13.5a19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 3.6 2.69h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L7.91 10a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92z"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="notifications"
                    label="Уведомления"
                    active={active === 'notifications'}
                    onClick={() => onSelect('notifications')}
                    badge={inboxUnread > 0 ? inboxUnread : undefined}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
                            <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
                        </svg>
                    }
                />
            </SidebarGroup>

            {/* Группа: Люди */}
            <div className="sidebar-divider" role="separator" />
            <SidebarGroup
                id="org"
                label="Люди"
                expanded={expandedGroups.has('org')}
                onToggle={() => toggleGroup('org')}
                hasActive={SECTION_GROUP[active] === 'org'}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/>
                        <circle cx="9" cy="7" r="4"/>
                        <path d="M23 21v-2a4 4 0 0 0-3-3.87"/>
                        <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
                    </svg>
                }
            >
                <SidebarItem
                    id="contacts"
                    label="Контакты"
                    active={active === 'contacts'}
                    onClick={() => onSelect('contacts')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/>
                            <circle cx="9" cy="7" r="4"/>
                            <path d="M23 21v-2a4 4 0 0 0-3-3.87"/>
                            <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="company"
                    label="Компания"
                    active={active === 'company'}
                    onClick={() => onSelect('company')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M3 21V6a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v15"/>
                            <path d="M9 21V12h6v9"/>
                            <path d="M3 21h18"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="user-profile"
                    label="Мой профиль"
                    active={active === 'user-profile'}
                    onClick={() => onSelect('user-profile')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <circle cx="12" cy="8" r="4"/>
                            <path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="user-preferences"
                    label="Настройки"
                    active={active === 'user-preferences'}
                    onClick={() => onSelect('user-preferences')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <circle cx="12" cy="12" r="3"/>
                            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="notification-settings"
                    label="Уведомления"
                    active={active === 'notification-settings'}
                    onClick={() => onSelect('notification-settings')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
                            <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
                        </svg>
                    }
                />
            </SidebarGroup>

            {/* Группа: BPM */}
            <div className="sidebar-divider" role="separator" />
            <SidebarGroup
                id="bpm"
                label="BPM"
                expanded={expandedGroups.has('bpm')}
                onToggle={() => toggleGroup('bpm')}
                hasActive={SECTION_GROUP[active] === 'bpm'}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <rect x="2" y="7" width="6" height="5" rx="1"/>
                        <rect x="16" y="7" width="6" height="5" rx="1"/>
                        <rect x="9" y="14" width="6" height="5" rx="1"/>
                        <path d="M8 9.5h2.5a1.5 1.5 0 0 1 1.5 1.5v1"/>
                        <path d="M16 9.5h-2.5A1.5 1.5 0 0 0 12 11v1"/>
                        <path d="M12 12v2"/>
                    </svg>
                }
            >
                <SidebarItem
                    id="bpm-processes"
                    label="Процессы"
                    active={active === 'bpm-processes'}
                    onClick={() => onSelect('bpm-processes')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <rect x="2" y="7" width="6" height="5" rx="1"/>
                            <rect x="16" y="7" width="6" height="5" rx="1"/>
                            <rect x="9" y="14" width="6" height="5" rx="1"/>
                            <path d="M8 9.5h2.5a1.5 1.5 0 0 1 1.5 1.5v1"/>
                            <path d="M16 9.5h-2.5A1.5 1.5 0 0 0 12 11v1"/>
                            <path d="M12 12v2"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="bpm-my-processes"
                    label="Мои процессы"
                    active={active === 'bpm-my-processes'}
                    onClick={() => onSelect('bpm-my-processes')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <circle cx="12" cy="8" r="4"/>
                            <path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>
                            <path d="M16 11l1.5 1.5L21 9"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="bpm-monitor"
                    label="Монитор"
                    active={active === 'bpm-monitor'}
                    onClick={() => onSelect('bpm-monitor')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <rect x="2" y="3" width="20" height="14" rx="2"/>
                            <path d="M8 21h8M12 17v4"/>
                            <path d="M7 8h2v5H7zM11 6h2v7h-2zM15 10h2v3h-2z"/>
                        </svg>
                    }
                />
                {hasRole('Admin') && (
                    <SidebarItem
                        id="bpm-queue"
                        label="Очередь"
                        active={active === 'bpm-queue'}
                        onClick={() => onSelect('bpm-queue')}
                        icon={
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                <path d="M3 6h18M3 12h18M3 18h18"/>
                                <circle cx="21" cy="6" r="0" fill="currentColor"/>
                            </svg>
                        }
                    />
                )}
                {hasRole('Admin') && (
                    <SidebarItem
                        id="bpm-migration"
                        label="Смена версии"
                        active={active === 'bpm-migration'}
                        onClick={() => onSelect('bpm-migration')}
                        icon={
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                <path d="M7 16V4m0 0L3 8m4-4 4 4"/>
                                <path d="M17 8v12m0 0 4-4m-4 4-4-4"/>
                            </svg>
                        }
                    />
                )}
                <SidebarItem
                    id="bpm-documentation"
                    label="Документирование"
                    active={active === 'bpm-documentation'}
                    onClick={() => onSelect('bpm-documentation')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                            <polyline points="14 2 14 8 20 8"/>
                            <line x1="16" y1="13" x2="8" y2="13"/>
                            <line x1="16" y1="17" x2="8" y2="17"/>
                            <polyline points="10 9 9 9 8 9"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="bpm-rules"
                    label="Бизнес-правила"
                    active={active === 'bpm-rules'}
                    onClick={() => onSelect('bpm-rules')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <rect x="3" y="3" width="18" height="18" rx="2"/>
                            <path d="M3 9h18"/>
                            <path d="M3 15h18"/>
                            <path d="M9 3v18"/>
                            <path d="M15 3v18"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="bpm-forms"
                    label="Формы задач"
                    active={active === 'bpm-forms'}
                    onClick={() => onSelect('bpm-forms')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <rect x="3" y="3" width="18" height="18" rx="2"/>
                            <path d="M7 8h10"/>
                            <path d="M7 12h6"/>
                            <path d="M7 16h4"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="bpm-scripts"
                    label="Сценарии"
                    active={active === 'bpm-scripts'}
                    onClick={() => onSelect('bpm-scripts')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <polyline points="16 18 22 12 16 6"/>
                            <polyline points="8 6 2 12 8 18"/>
                        </svg>
                    }
                />
                <SidebarItem
                    id="bpm-improvements"
                    label="Улучшения"
                    active={active === 'bpm-improvements'}
                    onClick={() => onSelect('bpm-improvements')}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/>
                        </svg>
                    }
                />
                {hasRole('Admin') && (
                    <SidebarItem
                        id="bpm-analytics"
                        label="Аналитика"
                        active={active === 'bpm-analytics'}
                        onClick={() => onSelect('bpm-analytics')}
                        icon={
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                <line x1="18" y1="20" x2="18" y2="10"/>
                                <line x1="12" y1="20" x2="12" y2="4"/>
                                <line x1="6" y1="20" x2="6" y2="14"/>
                            </svg>
                        }
                    />
                )}
            </SidebarGroup>

            {/* Группа: Управление (Admin + HR) */}
            {(hasRole('Admin') || canManageOrg) && (
                <>
                    <div className="sidebar-divider" role="separator" />
                    <SidebarGroup
                    id="admin"
                    label="Управление"
                    expanded={expandedGroups.has('admin')}
                    onToggle={() => toggleGroup('admin')}
                    hasActive={SECTION_GROUP[active] === 'admin'}
                    icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
                        </svg>
                    }
                >
                    {canManageOrg && (
                        <SidebarItem
                            id="org-structure"
                            label="Оргструктура"
                            active={active === 'org-structure'}
                            onClick={() => onSelect('org-structure')}
                            icon={
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <rect x="3" y="3" width="6" height="4" rx="1"/>
                                    <rect x="9" y="10" width="6" height="4" rx="1"/>
                                    <rect x="15" y="17" width="6" height="4" rx="1"/>
                                    <rect x="3" y="17" width="6" height="4" rx="1"/>
                                    <path d="M6 7v3"/>
                                    <path d="M12 14v3"/>
                                    <path d="M6 10h12"/>
                                </svg>
                            }
                        />
                    )}
                    {hasRole('Admin') && (
                        <SidebarItem
                            id="task-control-settings"
                            label="Настройки задач"
                            active={active === 'task-control-settings'}
                            onClick={() => onSelect('task-control-settings')}
                            icon={
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <path d="M12 20h9"/>
                                    <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z"/>
                                </svg>
                            }
                        />
                    )}
                    {hasRole('Admin') && (
                        <SidebarItem
                            id="timelogs-report"
                            label="Трудозатраты"
                            active={active === 'timelogs-report'}
                            onClick={() => onSelect('timelogs-report')}
                            icon={
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
                                    <line x1="16" y1="2" x2="16" y2="6"/>
                                    <line x1="8" y1="2" x2="8" y2="6"/>
                                    <line x1="3" y1="10" x2="21" y2="10"/>
                                    <line x1="8" y1="14" x2="16" y2="14"/>
                                </svg>
                            }
                        />
                    )}
                    {hasRole('Admin') && (
                        <SidebarItem
                            id="smtp-settings"
                            label="Настройки SMTP"
                            active={active === 'smtp-settings'}
                            onClick={() => onSelect('smtp-settings')}
                            icon={
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/>
                                    <polyline points="22,6 12,13 2,6"/>
                                </svg>
                            }
                        />
                    )}
                    {hasRole('Admin') && (
                        <SidebarItem
                            id="email-templates"
                            label="Шаблоны email"
                            active={active === 'email-templates'}
                            onClick={() => onSelect('email-templates')}
                            icon={
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/>
                                    <polyline points="22,6 12,13 2,6"/>
                                    <line x1="12" y1="12" x2="12" y2="18"/>
                                    <line x1="9" y1="15" x2="15" y2="15"/>
                                </svg>
                            }
                        />
                    )}
                    {hasRole('Admin') && (
                        <SidebarItem
                            id="sms-settings"
                            label="Настройки SMS"
                            active={active === 'sms-settings'}
                            onClick={() => onSelect('sms-settings')}
                            icon={
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
                                </svg>
                            }
                        />
                    )}
                    {hasRole('Admin') && (
                        <SidebarItem
                            id="notification-templates"
                            label="Шаблоны уведомлений"
                            active={active === 'notification-templates'}
                            onClick={() => onSelect('notification-templates')}
                            icon={
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <rect x="3" y="3" width="18" height="18" rx="2"/>
                                    <line x1="7" y1="8" x2="17" y2="8"/>
                                    <line x1="7" y1="12" x2="14" y2="12"/>
                                    <line x1="7" y1="16" x2="11" y2="16"/>
                                </svg>
                            }
                        />
                    )}
                    {hasRole('Admin') && (
                        <SidebarItem
                            id="notification-logs"
                            label="Журнал доставки"
                            active={active === 'notification-logs'}
                            onClick={() => onSelect('notification-logs')}
                            icon={
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                                    <polyline points="14 2 14 8 20 8"/>
                                    <line x1="9" y1="13" x2="15" y2="13"/>
                                    <line x1="9" y1="17" x2="12" y2="17"/>
                                </svg>
                            }
                        />
                    )}
                    {hasRole('Admin') && (
                        <SidebarItem
                            id="notification-stats"
                            label="Статистика уведомлений"
                            active={active === 'notification-stats'}
                            onClick={() => onSelect('notification-stats')}
                            icon={
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <line x1="18" y1="20" x2="18" y2="10"/>
                                    <line x1="12" y1="20" x2="12" y2="4"/>
                                    <line x1="6" y1="20" x2="6" y2="14"/>
                                </svg>
                            }
                        />
                    )}
                </SidebarGroup>
                </>
            )}

            <div className="sidebar-version" aria-label="Версия системы">
                <span>v{APP_VERSION}</span>
                <span>{LAST_PR_DATE}</span>
            </div>
        </nav>
    );
}

interface SidebarGroupProps {
    /** Уникальный идентификатор группы (используется для aria-label) */
    id: string;
    label: string;
    expanded: boolean;
    onToggle: () => void;
    /** Является ли один из дочерних разделов активным */
    hasActive: boolean;
    /** Суммарный бейдж — показывается на заголовке группы когда она свёрнута */
    badge?: number;
    icon: React.ReactNode;
    children: React.ReactNode;
}

function SidebarGroup({ id: _id, label, expanded, onToggle, hasActive, badge, icon, children }: SidebarGroupProps) {
    return (
        <div className={`sidebar-group${expanded ? ' expanded' : ''}${hasActive && !expanded ? ' has-active' : ''}`}>
            <button
                className={`sidebar-item sidebar-group-header${hasActive ? ' group-active' : ''}`}
                onClick={onToggle}
                title={label}
                aria-label={label}
                aria-expanded={expanded}
            >
                <span className="sidebar-icon" style={{ position: 'relative' }}>
                    {icon}
                    {badge !== undefined && badge > 0 && (
                        <span className="sidebar-badge" aria-hidden="true">
                            {badge > 99 ? '99+' : badge}
                        </span>
                    )}
                    <span className="sidebar-group-chevron" aria-hidden="true" />
                </span>
                <span className="sidebar-label">{label}</span>
            </button>
            {expanded && (
                <div className="sidebar-group-children" role="group" aria-label={label}>
                    {children}
                </div>
            )}
        </div>
    );
}


interface SidebarItemProps {
    id: string;
    label: string;
    active: boolean;
    onClick: () => void;
    icon: React.ReactNode;
    badge?: number;
}

function SidebarItem({ label, active, onClick, icon, badge }: SidebarItemProps) {
    return (
        <button
            className={`sidebar-item${active ? ' active' : ''}`}
            onClick={onClick}
            title={badge ? `${label} (${badge})` : label}
            aria-label={badge ? `${label}: ${badge}` : label}
            aria-pressed={active}
        >
            <span className="sidebar-icon" style={{ position: 'relative' }}>
                {icon}
                {badge !== undefined && badge > 0 && (
                    <span className="sidebar-badge" aria-hidden="true">
                        {badge > 99 ? '99+' : badge}
                    </span>
                )}
            </span>
            <span className="sidebar-label">{label}</span>
        </button>
    );
}
