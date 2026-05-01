import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getMigrationPackages,
    createMigrationPackage,
    type MigrationPackageListItemDto,
    type MigrationPackageStatus,
    type CreateMigrationPackageRequest,
} from '../../api/migrationApi';
import { getProcesses, getInstances, getProcessVersions, type BpmProcessListItemDto, type BpmInstanceListItemDto } from '../../api/bpmApi';
import './MigrationPackagesPage.css';

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
    New:                'mp-badge--new',
    Running:            'mp-badge--running',
    Completed:          'mp-badge--completed',
    CompletedWithErrors:'mp-badge--errors',
    Cancelled:          'mp-badge--cancelled',
};

type TabId = 'active' | 'archived';

// ─── Интерфейс пропсов ───────────────────────────────────────────────────────

interface Props {
    onOpenDetail: (id: string) => void;
}

// ─── Диалог создания пакета ───────────────────────────────────────────────────

interface CreateDialogProps {
    token: string;
    onCreated: (id: string) => void;
    onClose: () => void;
}

function CreateMigrationDialog({ token, onCreated, onClose }: CreateDialogProps) {
    const [name, setName] = useState('');
    const [processes, setProcesses] = useState<BpmProcessListItemDto[]>([]);
    const [selectedProcessId, setSelectedProcessId] = useState('');
    const [instances, setInstances] = useState<BpmInstanceListItemDto[]>([]);
    const [versions, setVersions] = useState<{ id: string; versionNumber: number; status: string }[]>([]);
    const [selectedTargetVersionId, setSelectedTargetVersionId] = useState('');
    const [selectedInstanceIds, setSelectedInstanceIds] = useState<Set<string>>(new Set());
    const [step, setStep] = useState<1 | 2 | 3>(1);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        // Получаем список процессов (используем пустой organizationId — сервер вернёт все доступные)
        getProcesses(token, '00000000-0000-0000-0000-000000000000')
            .then(setProcesses)
            .catch(() => { /* игнорируем — покажем пустой список */ });
    }, [token]);

    useEffect(() => {
        if (!selectedProcessId) { setInstances([]); return; }
        getInstances(token, selectedProcessId)
            .then(data => setInstances(data))
            .catch(() => setInstances([]));
    }, [token, selectedProcessId]);

    useEffect(() => {
        if (!selectedProcessId) { setVersions([]); return; }
        getProcessVersions(token, selectedProcessId)
            .then(data => setVersions(data.filter(v => v.status === 'Active')))
            .catch(() => setVersions([]));
    }, [token, selectedProcessId]);

    const toggleInstance = (id: string) => {
        setSelectedInstanceIds(prev => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id); else next.add(id);
            return next;
        });
    };

    const handleCreate = async () => {
        if (!name.trim()) { setError('Укажите наименование пакета'); return; }
        if (selectedInstanceIds.size === 0) { setError('Выберите хотя бы один экземпляр'); return; }
        if (!selectedTargetVersionId) { setError('Выберите целевую версию'); return; }

        setSaving(true);
        setError(null);
        try {
            const req: CreateMigrationPackageRequest = {
                name: name.trim(),
                items: Array.from(selectedInstanceIds).map(instanceId => ({
                    instanceId,
                    targetVersionId: selectedTargetVersionId,
                })),
            };
            const pkg = await createMigrationPackage(token, req);
            onCreated(pkg.id);
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : 'Ошибка создания пакета');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="mp-dialog-overlay" onClick={onClose}>
            <div className="mp-dialog" onClick={e => e.stopPropagation()}>
                <div className="mp-dialog-header">
                    <h2 className="mp-dialog-title">Создание пакета миграции</h2>
                    <button className="mp-dialog-close" onClick={onClose} aria-label="Закрыть">✕</button>
                </div>

                {/* Шаг 1: Наименование и выбор процесса */}
                {step === 1 && (
                    <div className="mp-dialog-body">
                        <label className="mp-field-label">Наименование пакета</label>
                        <input
                            className="mp-field-input"
                            value={name}
                            onChange={e => setName(e.target.value)}
                            placeholder="Например: Обновление v1.0 → v2.0 в июне"
                        />
                        <label className="mp-field-label" style={{ marginTop: 16 }}>Процесс</label>
                        <select
                            className="mp-field-input"
                            value={selectedProcessId}
                            onChange={e => { setSelectedProcessId(e.target.value); setSelectedInstanceIds(new Set()); }}
                        >
                            <option value="">— выберите процесс —</option>
                            {processes.map(p => (
                                <option key={p.id} value={p.id}>{p.name}</option>
                            ))}
                        </select>
                        {error && <p className="mp-dialog-error">{error}</p>}
                        <div className="mp-dialog-actions">
                            <button className="mp-btn mp-btn--secondary" onClick={onClose}>Отмена</button>
                            <button
                                className="mp-btn mp-btn--primary"
                                onClick={() => { if (!name.trim() || !selectedProcessId) { setError('Заполните наименование и выберите процесс'); return; } setError(null); setStep(2); }}
                            >Далее →</button>
                        </div>
                    </div>
                )}

                {/* Шаг 2: Выбор экземпляров */}
                {step === 2 && (
                    <div className="mp-dialog-body">
                        <p className="mp-dialog-hint">Выберите активные экземпляры для перевода на новую версию:</p>
                        {instances.length === 0
                            ? <p className="mp-dialog-hint">Нет активных экземпляров для данного процесса</p>
                            : (
                                <div className="mp-instance-list">
                                    {instances.map(inst => (
                                        <label key={inst.id} className="mp-instance-row">
                                            <input
                                                type="checkbox"
                                                checked={selectedInstanceIds.has(inst.id)}
                                                onChange={() => toggleInstance(inst.id)}
                                            />
                                            <span className="mp-instance-name">{inst.name}</span>
                                        </label>
                                    ))}
                                </div>
                            )
                        }
                        {error && <p className="mp-dialog-error">{error}</p>}
                        <div className="mp-dialog-actions">
                            <button className="mp-btn mp-btn--secondary" onClick={() => { setError(null); setStep(1); }}>← Назад</button>
                            <button
                                className="mp-btn mp-btn--primary"
                                onClick={() => { if (selectedInstanceIds.size === 0) { setError('Выберите хотя бы один экземпляр'); return; } setError(null); setStep(3); }}
                            >Далее →</button>
                        </div>
                    </div>
                )}

                {/* Шаг 3: Выбор целевой версии */}
                {step === 3 && (
                    <div className="mp-dialog-body">
                        <p className="mp-dialog-hint">Выбрано экземпляров: <strong>{selectedInstanceIds.size}</strong></p>
                        <label className="mp-field-label">Целевая версия (активная)</label>
                        <select
                            className="mp-field-input"
                            value={selectedTargetVersionId}
                            onChange={e => setSelectedTargetVersionId(e.target.value)}
                        >
                            <option value="">— выберите версию —</option>
                            {versions.map(v => (
                                <option key={v.id} value={v.id}>Версия {v.versionNumber}</option>
                            ))}
                        </select>
                        {error && <p className="mp-dialog-error">{error}</p>}
                        <div className="mp-dialog-actions">
                            <button className="mp-btn mp-btn--secondary" onClick={() => { setError(null); setStep(2); }}>← Назад</button>
                            <button className="mp-btn mp-btn--primary" onClick={handleCreate} disabled={saving}>
                                {saving ? 'Создание...' : 'Создать пакет'}
                            </button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}

// ─── Основной компонент ───────────────────────────────────────────────────────

/** Список пакетов миграции версий экземпляров (FR-BPM-02.7). */
export function MigrationPackagesPage({ onOpenDetail }: Props) {
    const { accessToken } = useAuth();
    const [tab, setTab] = useState<TabId>('active');
    const [packages, setPackages] = useState<MigrationPackageListItemDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [showCreateDialog, setShowCreateDialog] = useState(false);

    const loadPackages = useCallback(async () => {
        if (!accessToken) return;
        setLoading(true);
        setError(null);
        try {
            const isActive = tab === 'active' ? true : false;
            const data = await getMigrationPackages(accessToken, { isActive });
            setPackages(data);
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [accessToken, tab]);

    useEffect(() => { loadPackages(); }, [loadPackages]);

    return (
        <div className="mp-root">
            <div className="mp-header">
                <h1 className="mp-title">Смена версии (миграция)</h1>
                <button className="mp-btn mp-btn--primary" onClick={() => setShowCreateDialog(true)}>
                    + Создать пакет
                </button>
            </div>

            {/* Вкладки */}
            <div className="mp-tabs">
                <button
                    className={`mp-tab${tab === 'active' ? ' mp-tab--active' : ''}`}
                    onClick={() => setTab('active')}
                >Текущие операции</button>
                <button
                    className={`mp-tab${tab === 'archived' ? ' mp-tab--active' : ''}`}
                    onClick={() => setTab('archived')}
                >Обработанные</button>
            </div>

            {/* Таблица */}
            <div className="mp-content">
                {loading && <p className="mp-loading">Загрузка...</p>}
                {error && <p className="mp-error">{error}</p>}
                {!loading && !error && packages.length === 0 && (
                    <p className="mp-empty">Пакеты миграции отсутствуют</p>
                )}
                {!loading && !error && packages.length > 0 && (
                    <table className="mp-table">
                        <thead>
                            <tr>
                                <th>Наименование</th>
                                <th>Автор</th>
                                <th>Дата создания</th>
                                <th>Статус</th>
                                <th>Элементов</th>
                                <th>Выполнено</th>
                                <th>Ошибок</th>
                            </tr>
                        </thead>
                        <tbody>
                            {packages.map(pkg => (
                                <tr key={pkg.id} className="mp-row" onClick={() => onOpenDetail(pkg.id)}>
                                    <td className="mp-row-name">{pkg.name}</td>
                                    <td>{pkg.createdByUserName || '—'}</td>
                                    <td>{fmtDate(pkg.createdAt)}</td>
                                    <td>
                                        <span className={`mp-badge ${PKG_STATUS_CSS[pkg.status]}`}>
                                            {PKG_STATUS_LABELS[pkg.status]}
                                        </span>
                                    </td>
                                    <td>{pkg.totalItems}</td>
                                    <td className="mp-cell--ok">{pkg.migratedItems}</td>
                                    <td className={pkg.errorItems > 0 ? 'mp-cell--err' : ''}>{pkg.errorItems}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </div>

            {showCreateDialog && accessToken && (
                <CreateMigrationDialog
                    token={accessToken}
                    onCreated={id => { setShowCreateDialog(false); loadPackages(); onOpenDetail(id); }}
                    onClose={() => setShowCreateDialog(false)}
                />
            )}
        </div>
    );
}
