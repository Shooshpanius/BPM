import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getMigrationPackage,
    getMigrationPackageItems,
    startMigrationPackage,
    cancelMigrationPackage,
    manualMigrateItem,
    type MigrationPackageDetailDto,
    type MigrationItemDto,
    type MigrationItemStatus,
    type MigrationPackageStatus,
    type ManualMigrateItemRequest,
} from '../../api/migrationApi';
import { useModalShake } from '../../hooks/useModalShake';
import './MigrationPackageDetailPage.css';

// ─── Вспомогательные утилиты ─────────────────────────────────────────────────

function fmtDate(iso?: string) {
    if (!iso) return '—';
    try {
        return new Date(iso).toLocaleString('ru-RU', {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit',
        });
    } catch { return iso; }
}

// ─── Константы статусов ───────────────────────────────────────────────────────

const PKG_STATUS_LABELS: Record<MigrationPackageStatus, string> = {
    New:                'Новый',
    Running:            'Выполняется',
    Completed:          'Выполнено',
    CompletedWithErrors:'Выполнено с ошибками',
    Cancelled:          'Отменён',
};

const PKG_STATUS_CSS: Record<MigrationPackageStatus, string> = {
    New:                'mpd-badge--new',
    Running:            'mpd-badge--running',
    Completed:          'mpd-badge--completed',
    CompletedWithErrors:'mpd-badge--errors',
    Cancelled:          'mpd-badge--cancelled',
};

const ITEM_STATUS_LABELS: Record<MigrationItemStatus, string> = {
    New:                   'Новый',
    InProgress:            'В работе',
    Migrated:              'Переведён',
    CriticalError:         'Критическая ошибка',
    Busy:                  'Занят',
    RequiresManualHandling:'Ручная обработка',
    OtherError:            'Ошибка',
    NotApplicable:         'Не применимо',
    NoMigrationNeeded:     'Обновление не нужно',
};

const ITEM_STATUS_CSS: Record<MigrationItemStatus, string> = {
    New:                   'mpd-item-badge--new',
    InProgress:            'mpd-item-badge--running',
    Migrated:              'mpd-item-badge--ok',
    CriticalError:         'mpd-item-badge--critical',
    Busy:                  'mpd-item-badge--busy',
    RequiresManualHandling:'mpd-item-badge--manual',
    OtherError:            'mpd-item-badge--error',
    NotApplicable:         'mpd-item-badge--na',
    NoMigrationNeeded:     'mpd-item-badge--skip',
};

// ─── Интерфейс пропсов ───────────────────────────────────────────────────────

interface Props {
    packageId: string;
    onBack: () => void;
}

// ─── Диалог ручной обработки ─────────────────────────────────────────────────

interface ManualDialogProps {
    item: MigrationItemDto;
    onSave: (itemId: string, req: ManualMigrateItemRequest) => Promise<void>;
    onClose: () => void;
}

function ManualMigrateDialog({ item, onSave, onClose }: ManualDialogProps) {
    const [url, setUrl] = useState(item.manualChangeUrl ?? '');
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const { shaking, shake } = useModalShake();

    const handleSave = async () => {
        setSaving(true);
        setError(null);
        try {
            await onSave(item.id, { manualChangeUrl: url || undefined });
            onClose();
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="mpd-dialog-overlay" onClick={(e) => { if (e.target === e.currentTarget) shake(); }}>
            <div className="mpd-dialog" onClick={e => e.stopPropagation()}>
                <div className="mpd-dialog-header">
                    <h2 className="mpd-dialog-title">Ручная обработка</h2>
                    <button className={`mpd-dialog-close${shaking ? ' btn-flash' : ''}`} onClick={onClose} aria-label="Закрыть">✕</button>
                </div>
                <div className="mpd-dialog-body">
                    <p className="mpd-dialog-hint">
                        Экземпляр: <strong>{item.instanceName}</strong>
                    </p>
                    <p className="mpd-dialog-hint">
                        Комментарий: {item.errorComment || '—'}
                    </p>
                    <label className="mpd-field-label">Ссылка на ручное изменение (необязательно)</label>
                    <input
                        className="mpd-field-input"
                        value={url}
                        onChange={e => setUrl(e.target.value)}
                        placeholder="https://..."
                    />
                    {error && <p className="mpd-dialog-error">{error}</p>}
                    <div className="mpd-dialog-actions">
                        <button className={`mpd-btn mpd-btn--secondary${shaking ? ' btn-flash' : ''}`} onClick={onClose}>Отмена</button>
                        <button className={`mpd-btn mpd-btn--primary${shaking ? ' btn-flash' : ''}`} onClick={handleSave} disabled={saving}>
                            {saving ? 'Сохранение...' : 'Отметить как выполненное'}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}

// ─── Основной компонент ───────────────────────────────────────────────────────

/** Детальная страница пакета миграции версий (FR-BPM-02.7). */
export function MigrationPackageDetailPage({ packageId, onBack }: Props) {
    const { accessToken } = useAuth();
    const [pkg, setPkg] = useState<MigrationPackageDetailDto | null>(null);
    const [items, setItems] = useState<MigrationItemDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [actionError, setActionError] = useState<string | null>(null);
    const [manualItem, setManualItem] = useState<MigrationItemDto | null>(null);

    const loadData = useCallback(async () => {
        if (!accessToken) return;
        setError(null);
        try {
            const [pkgData, itemsData] = await Promise.all([
                getMigrationPackage(accessToken, packageId),
                getMigrationPackageItems(accessToken, packageId, { pageSize: 100 }),
            ]);
            setPkg(pkgData);
            setItems(itemsData);
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [accessToken, packageId]);

    useEffect(() => { loadData(); }, [loadData]);

    const handleStart = async () => {
        if (!accessToken || !pkg) return;
        setActionError(null);
        try {
            await startMigrationPackage(accessToken, packageId);
            await loadData();
        } catch (e: unknown) {
            setActionError(e instanceof Error ? e.message : 'Ошибка запуска');
        }
    };

    const handleCancel = async () => {
        if (!accessToken || !pkg) return;
        if (!confirm('Отменить выполнение пакета миграции?')) return;
        setActionError(null);
        try {
            await cancelMigrationPackage(accessToken, packageId);
            await loadData();
        } catch (e: unknown) {
            setActionError(e instanceof Error ? e.message : 'Ошибка отмены');
        }
    };

    const handleManualSave = async (itemId: string, req: ManualMigrateItemRequest) => {
        if (!accessToken) return;
        await manualMigrateItem(accessToken, packageId, itemId, req);
        await loadData();
    };

    if (loading) return <div className="mpd-root"><p className="mpd-loading">Загрузка...</p></div>;
    if (error || !pkg) return (
        <div className="mpd-root">
            <button className="mpd-back-btn" onClick={onBack}>← Назад</button>
            <p className="mpd-error">{error ?? 'Пакет не найден'}</p>
        </div>
    );

    // Прогресс
    const total = pkg.totalItems;
    const progress = total > 0 ? Math.round((pkg.migratedItems / total) * 100) : 0;

    return (
        <div className="mpd-root">
            {/* Шапка */}
            <div className="mpd-header">
                <button className="mpd-back-btn" onClick={onBack}>← Назад</button>
                <div className="mpd-header-info">
                    <h1 className="mpd-title">{pkg.name}</h1>
                    <div className="mpd-meta">
                        <span className={`mpd-badge ${PKG_STATUS_CSS[pkg.status]}`}>
                            {PKG_STATUS_LABELS[pkg.status]}
                        </span>
                        <span className="mpd-meta-item">Автор: {pkg.createdByUserName || '—'}</span>
                        <span className="mpd-meta-item">Создан: {fmtDate(pkg.createdAt)}</span>
                        {pkg.completedAt && (
                            <span className="mpd-meta-item">Завершён: {fmtDate(pkg.completedAt)}</span>
                        )}
                    </div>
                </div>
                <div className="mpd-actions">
                    {pkg.status === 'New' && (
                        <button className="mpd-btn mpd-btn--primary" onClick={handleStart}>
                            ▷ Запустить
                        </button>
                    )}
                    {(pkg.status === 'New' || pkg.status === 'Running') && (
                        <button className="mpd-btn mpd-btn--danger" onClick={handleCancel}>
                            Отменить
                        </button>
                    )}
                </div>
            </div>

            {actionError && <p className="mpd-action-error">{actionError}</p>}

            {/* Статистика */}
            <div className="mpd-stats">
                <div className="mpd-stat">
                    <span className="mpd-stat-label">Всего</span>
                    <span className="mpd-stat-value">{total}</span>
                </div>
                <div className="mpd-stat">
                    <span className="mpd-stat-label">Переведено</span>
                    <span className="mpd-stat-value mpd-stat-value--ok">{pkg.migratedItems}</span>
                </div>
                <div className="mpd-stat">
                    <span className="mpd-stat-label">Ошибок</span>
                    <span className={`mpd-stat-value${pkg.errorItems > 0 ? ' mpd-stat-value--err' : ''}`}>{pkg.errorItems}</span>
                </div>
                <div className="mpd-stat">
                    <span className="mpd-stat-label">Ожидает</span>
                    <span className="mpd-stat-value">{pkg.pendingItems}</span>
                </div>
                {total > 0 && (
                    <div className="mpd-progress-wrap">
                        <div className="mpd-progress-bar">
                            <div className="mpd-progress-fill" style={{ width: `${progress}%` }} />
                        </div>
                        <span className="mpd-progress-label">{progress}%</span>
                    </div>
                )}
            </div>

            {/* Таблица элементов */}
            <div className="mpd-content">
                {items.length === 0
                    ? <p className="mpd-empty">Элементы пакета отсутствуют</p>
                    : (
                        <table className="mpd-table">
                            <thead>
                                <tr>
                                    <th>Экземпляр</th>
                                    <th>Процесс</th>
                                    <th>Целевая версия</th>
                                    <th>Статус</th>
                                    <th>Комментарий</th>
                                    <th>Обработан</th>
                                    <th></th>
                                </tr>
                            </thead>
                            <tbody>
                                {items.map(item => (
                                    <tr key={item.id} className="mpd-row">
                                        <td className="mpd-row-name">{item.instanceName}</td>
                                        <td>{item.processName}</td>
                                        <td>v{item.targetVersionNumber}</td>
                                        <td>
                                            <span className={`mpd-item-badge ${ITEM_STATUS_CSS[item.status]}`}>
                                                {ITEM_STATUS_LABELS[item.status]}
                                            </span>
                                        </td>
                                        <td className="mpd-row-comment">
                                            {item.manualChangeUrl
                                                ? <a href={item.manualChangeUrl} target="_blank" rel="noreferrer">Ссылка ↗</a>
                                                : item.errorComment || '—'
                                            }
                                        </td>
                                        <td>{fmtDate(item.processedAt)}</td>
                                        <td>
                                            {(item.status === 'RequiresManualHandling' ||
                                              item.status === 'CriticalError' ||
                                              item.status === 'OtherError') && (
                                                <button
                                                    className="mpd-btn mpd-btn--sm mpd-btn--secondary"
                                                    onClick={() => setManualItem(item)}
                                                >
                                                    Ручная обработка
                                                </button>
                                            )}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )
                }
            </div>

            {manualItem && (
                <ManualMigrateDialog
                    item={manualItem}
                    onSave={handleManualSave}
                    onClose={() => setManualItem(null)}
                />
            )}
        </div>
    );
}
