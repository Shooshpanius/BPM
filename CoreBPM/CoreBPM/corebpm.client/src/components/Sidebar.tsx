import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import { getTaskCounters, type TaskCountersDto } from '../api/tasksApi';
import { useBpmNotifications } from '../context/BpmNotificationsContext';
import { APP_VERSION, LAST_PR_DATE } from '../version';
import './Sidebar.css';

export type SidebarSection = 'tasks' | 'tasks-periodic' | 'tasks-dashboard' | 'contacts' | 'org-structure' | 'bpm-processes' | 'bpm-my-processes' | 'bpm-monitor' | 'bpm-queue' | 'bpm-documentation' | 'bpm-rules' | 'bpm-forms' | 'bpm-scripts' | 'bpm-migration' | 'bpm-improvements' | 'bpm-analytics' | 'task-control-settings' | 'timelogs-report' | 'notification-settings';

interface SidebarProps {
    active: SidebarSection;
    onSelect: (section: SidebarSection) => void;
}

/** Вертикальный сайдбар навигации (~60px) с иконками разделов. */
export function Sidebar({ active, onSelect }: SidebarProps) {
    const { hasRole, accessToken } = useAuth();
    const canManageOrg = hasRole('Admin') || hasRole('HR');
    const [counters, setCounters] = useState<TaskCountersDto | null>(null);
    const { notifications } = useBpmNotifications();

    const loadCounters = useCallback(() => {
        if (!accessToken) return;
        getTaskCounters(accessToken).then(c => setCounters(c)).catch(() => {});
    }, [accessToken]);

    // FR-TASK-02.2: Периодическое обновление счётчиков задач (fallback каждые 5 минут)
    useEffect(() => {
        if (!accessToken) return;
        loadCounters();
        const id = setInterval(loadCounters, 300_000);
        return () => clearInterval(id);
    }, [accessToken, loadCounters]);

    // FR-TASK-02.2: Push-обновление счётчиков при получении SignalR-события TaskCountersUpdated
    useEffect(() => {
        const last = notifications[0];
        if (last?.type === 'TaskCountersUpdated') {
            loadCounters();
        }
    }, [notifications, loadCounters]);

    return (
        <nav className="sidebar" aria-label="Разделы системы">
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
                label="Периодические задачи"
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
            {/* FR-TASK-02.3: Дашборд задач */}
            <SidebarItem
                id="tasks-dashboard"
                label="Дашборд задач"
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
            <div className="sidebar-divider" role="separator" />
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
            <div className="sidebar-divider" role="separator" />
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
                    label="Отчёт по трудозатратам"
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
            {/* FR-TASK-02.3: Настройки уведомлений — доступно всем авторизованным */}
            <SidebarItem
                id="notification-settings"
                label="Настройки уведомлений"
                active={active === 'notification-settings'}
                onClick={() => onSelect('notification-settings')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
                        <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
                    </svg>
                }
            />
            {canManageOrg && (
                <>
                    <div className="sidebar-divider" role="separator" />
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
                </>
            )}
            <div className="sidebar-version" aria-label="Версия системы">
                <span>v{APP_VERSION}</span>
                <span>{LAST_PR_DATE}</span>
            </div>
        </nav>
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

    return (
        <nav className="sidebar" aria-label="Разделы системы">
            <SidebarItem
                id="tasks"
                label="Задачи"
                active={active === 'tasks'}
                onClick={() => onSelect('tasks')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <polyline points="9 11 12 14 22 4"/>
                        <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>
                    </svg>
                }
            />
            <SidebarItem
                id="tasks-periodic"
                label="Периодические задачи"
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
            {/* FR-TASK-02.3: Дашборд задач */}
            <SidebarItem
                id="tasks-dashboard"
                label="Дашборд задач"
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
            <div className="sidebar-divider" role="separator" />
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
            <div className="sidebar-divider" role="separator" />
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
                    label="Отчёт по трудозатратам"
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
            {/* FR-TASK-02.3: Настройки уведомлений — доступно всем авторизованным */}
            <SidebarItem
                id="notification-settings"
                label="Настройки уведомлений"
                active={active === 'notification-settings'}
                onClick={() => onSelect('notification-settings')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
                        <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
                    </svg>
                }
            />
            {canManageOrg && (
                <>
                    <div className="sidebar-divider" role="separator" />
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
                </>
            )}
            <div className="sidebar-version" aria-label="Версия системы">
                <span>v{APP_VERSION}</span>
                <span>{LAST_PR_DATE}</span>
            </div>
        </nav>
    );
}

interface SidebarItemProps {
    id: string;
    label: string;
    active: boolean;
    onClick: () => void;
    icon: React.ReactNode;
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
