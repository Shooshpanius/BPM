import { useEffect, useState, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getDashboard,
    saveDashboard,
    addWidget,
    deleteWidget,
    resetDashboard,
    type PortalDashboardWidgetDto,
    type SaveWidgetRequest,
} from '../../api/portalApi';
import { MyTasksWidget } from './MyTasksWidget';
import { MyProcessesWidget } from './MyProcessesWidget';
import { NotificationsWidget } from './NotificationsWidget';
import { AwaitingActionWidget } from './AwaitingActionWidget';
import { QuickActionsWidget } from './QuickActionsWidget';
import { NewsWidget } from './NewsWidget';
import './PortalDashboardPage.css';

interface Props {
    onOpenTask?: (id: string) => void;
    onOpenSection?: (section: string) => void;
    onOpenInstance?: (id: string) => void;
}

const WIDGET_LABELS: Record<string, string> = {
    'my-tasks': 'Мои задачи',
    'my-processes': 'Мои процессы',
    'awaiting-action': 'Ожидает действия',
    'notifications': 'Уведомления',
    'quick-actions': 'Быстрые действия',
    'news': 'Новости компании',
    'my-colleagues': 'Мои коллеги',
    'recent-documents': 'Последние документы',
};

const AVAILABLE_WIDGETS = Object.entries(WIDGET_LABELS).map(([type, label]) => ({ type, label }));

/** Главная страница-портал с настраиваемыми виджетами (FR-PORTAL-01). */
export function PortalDashboardPage({ onOpenTask, onOpenSection, onOpenInstance }: Props) {
    const { accessToken } = useAuth();
    const [widgets, setWidgets] = useState<PortalDashboardWidgetDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [editMode, setEditMode] = useState(false);
    const [showAddPanel, setShowAddPanel] = useState(false);
    const [addSearch, setAddSearch] = useState('');
    const [saving, setSaving] = useState(false);

    const loadWidgets = useCallback(() => {
        if (!accessToken) return;
        setLoading(true);
        getDashboard(accessToken)
            .then(setWidgets)
            .catch(() => {})
            .finally(() => setLoading(false));
    }, [accessToken]);

    useEffect(() => { loadWidgets(); }, [loadWidgets]);

    const handleSave = async () => {
        if (!accessToken) return;
        setSaving(true);
        try {
            const req = {
                widgets: widgets.map((w): SaveWidgetRequest => ({
                    id: w.id,
                    widgetType: w.widgetType,
                    col: w.col,
                    row: w.row,
                    colSpan: w.colSpan,
                    rowSpan: w.rowSpan,
                    title: w.title,
                    configJson: w.configJson,
                    isCollapsed: w.isCollapsed,
                    sortOrder: w.sortOrder,
                })),
            };
            const saved = await saveDashboard(accessToken, req);
            setWidgets(saved);
            setEditMode(false);
        } catch {
            // ignore
        } finally {
            setSaving(false);
        }
    };

    const handleCancel = () => {
        setEditMode(false);
        loadWidgets();
    };

    const handleAddWidget = async (type: string) => {
        if (!accessToken) return;
        try {
            const newWidget = await addWidget(accessToken, {
                widgetType: type,
                col: 0,
                row: widgets.length,
                colSpan: 2,
                rowSpan: 1,
            });
            setWidgets(prev => [...prev, newWidget]);
            setShowAddPanel(false);
        } catch { /* ignore */ }
    };

    const handleDeleteWidget = async (id: string) => {
        if (!accessToken) return;
        try {
            await deleteWidget(accessToken, id);
            setWidgets(prev => prev.filter(w => w.id !== id));
        } catch { /* ignore */ }
    };

    const handleCollapse = (id: string) => {
        setWidgets(prev => prev.map(w =>
            w.id === id ? { ...w, isCollapsed: !w.isCollapsed } : w
        ));
    };

    const handleReset = async () => {
        if (!accessToken) return;
        if (!confirm('Сбросить дашборд к настройкам по умолчанию?')) return;
        await resetDashboard(accessToken);
        loadWidgets();
    };

    const renderWidgetContent = (w: PortalDashboardWidgetDto) => {
        switch (w.widgetType) {
            case 'my-tasks':
                return <MyTasksWidget onOpenTask={onOpenTask} />;
            case 'my-processes':
                return <MyProcessesWidget onOpenInstance={onOpenInstance} />;
            case 'notifications':
                return <NotificationsWidget />;
            case 'awaiting-action':
                return <AwaitingActionWidget />;
            case 'quick-actions':
                return <QuickActionsWidget onOpenSection={onOpenSection ?? (() => {})} />;
            case 'news':
                return <NewsWidget />;
            default:
                return <div className="widget-empty">{WIDGET_LABELS[w.widgetType] ?? w.widgetType}</div>;
        }
    };

    const filteredAvailable = AVAILABLE_WIDGETS.filter(aw =>
        aw.label.toLowerCase().includes(addSearch.toLowerCase()) &&
        !widgets.some(w => w.widgetType === aw.type)
    );

    if (loading) {
        return <div className="portal-loading">Загрузка дашборда...</div>;
    }

    return (
        <div className="portal-dashboard">
            <div className="portal-dashboard__toolbar">
                <h1 className="portal-dashboard__title">Главная страница</h1>
                <div className="portal-dashboard__actions">
                    {!editMode ? (
                        <button className="portal-btn portal-btn--secondary" onClick={() => setEditMode(true)}>
                            ✏ Настроить
                        </button>
                    ) : (
                        <>
                            <button className="portal-btn portal-btn--secondary" onClick={() => setShowAddPanel(p => !p)}>
                                + Добавить виджет
                            </button>
                            <button className="portal-btn portal-btn--ghost" onClick={handleReset}>
                                ↺ Сбросить
                            </button>
                            <button className="portal-btn portal-btn--ghost" onClick={handleCancel}>
                                Отмена
                            </button>
                            <button className="portal-btn portal-btn--primary" onClick={handleSave} disabled={saving}>
                                {saving ? 'Сохранение...' : 'Сохранить'}
                            </button>
                        </>
                    )}
                </div>
            </div>

            {editMode && showAddPanel && (
                <div className="portal-add-panel">
                    <div className="portal-add-panel__header">
                        <span>Доступные виджеты</span>
                        <input
                            type="text"
                            placeholder="Поиск..."
                            value={addSearch}
                            onChange={e => setAddSearch(e.target.value)}
                            className="portal-add-search"
                        />
                    </div>
                    <div className="portal-add-panel__list">
                        {filteredAvailable.length === 0
                            ? <p className="portal-add-empty">Все виджеты уже добавлены</p>
                            : filteredAvailable.map(aw => (
                                <button
                                    key={aw.type}
                                    className="portal-add-item"
                                    onClick={() => handleAddWidget(aw.type)}
                                >
                                    {aw.label}
                                </button>
                            ))
                        }
                    </div>
                </div>
            )}

            <div className="portal-widget-grid">
                {widgets.map(w => (
                    <div
                        key={w.id}
                        className={`portal-widget portal-widget--col${w.colSpan}${w.isCollapsed ? ' portal-widget--collapsed' : ''}`}
                        style={{
                            gridColumn: `${w.col + 1} / span ${w.colSpan}`,
                            gridRow: `${w.row + 1} / span ${w.isCollapsed ? 1 : w.rowSpan}`,
                        }}
                    >
                        <div className="portal-widget__header">
                            <span className="portal-widget__label">
                                {w.title ?? WIDGET_LABELS[w.widgetType] ?? w.widgetType}
                            </span>
                            <div className="portal-widget__controls">
                                <button
                                    className="portal-widget__ctrl-btn"
                                    title={w.isCollapsed ? 'Развернуть' : 'Свернуть'}
                                    onClick={() => handleCollapse(w.id)}
                                >
                                    {w.isCollapsed ? '▼' : '▲'}
                                </button>
                                {editMode && (
                                    <button
                                        className="portal-widget__ctrl-btn portal-widget__ctrl-btn--delete"
                                        title="Удалить виджет"
                                        onClick={() => handleDeleteWidget(w.id)}
                                    >
                                        ✕
                                    </button>
                                )}
                            </div>
                        </div>
                        {!w.isCollapsed && renderWidgetContent(w)}
                    </div>
                ))}
            </div>

            {widgets.length === 0 && !loading && (
                <div className="portal-empty">
                    <p>Дашборд пуст. Нажмите «Настроить» для добавления виджетов.</p>
                </div>
            )}
        </div>
    );
}
