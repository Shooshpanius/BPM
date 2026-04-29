import { useState, useEffect, useRef, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getUnitsTree,
    getUnitById,
    createUnit,
    updateUnit,
    archiveUnit,
    moveUnit,
    DEPARTMENT_STATUS_ACTIVE,
    DEPARTMENT_STATUS_ARCHIVED,
    type OrgUnitTreeDto,
    type OrgUnitDto,
    type CreateUnitRequest,
    type UpdateUnitRequest,
    type DepartmentStatus,
} from '../../api/unitsApi';
import type { DirectoryOrganizationDto } from '../../api/orgDirectoryApi';
import { getDirectoryOrganizations } from '../../api/orgDirectoryApi';
import { getOrgChart, type OrgChartDto } from '../../api/orgChartApi';
import { DepartmentTree } from '../../components/org/DepartmentTree/DepartmentTree';
import { DepartmentForm } from '../../components/org/DepartmentTree/DepartmentForm';
import { DepartmentHistory } from '../../components/org/DepartmentTree/DepartmentHistory';
import { OrgChartView } from '../../components/org/OrgChartView';
import { OrgChartTable } from '../../components/org/OrgChartTable';
import './OrgStructurePage.css';

type StatusFilter = 'active' | 'archived' | 'all';
type TabId = 'chart' | 'list' | 'manage';

const TABS: { id: TabId; label: string; adminOnly: boolean }[] = [
    { id: 'chart',  label: 'Органиграмма', adminOnly: false },
    { id: 'list',   label: 'Список',       adminOnly: false },
    { id: 'manage', label: 'Управление',   adminOnly: true  },
];

/** Страница оргструктуры: органиграмма, список и управление подразделениями. */
export function OrgStructurePage() {
    const { accessToken: token, hasRole } = useAuth();
    const canManage = hasRole('Admin') || hasRole('HR');

    // ─── Общее состояние ───
    const [organizations, setOrganizations] = useState<DirectoryOrganizationDto[]>([]);
    const [selectedOrgId, setSelectedOrgId] = useState<string>('');
    const [activeTab, setActiveTab] = useState<TabId>('chart');

    // ─── Поиск ───
    const [search, setSearch] = useState('');
    const [debouncedSearch, setDebouncedSearch] = useState('');
    const searchTimer = useRef<number | null>(null);

    // ─── Данные органиграммы ───
    const [chartData, setChartData] = useState<OrgChartDto | null>(null);
    const [chartLoading, setChartLoading] = useState(false);
    const [chartError, setChartError] = useState<string | null>(null);

    // ─── Состояние вкладки «Управление» ───
    const [tree, setTree] = useState<OrgUnitTreeDto[]>([]);
    const [treeLoading, setTreeLoading] = useState(false);
    const [treeError, setTreeError] = useState<string | null>(null);
    const [selectedUnit, setSelectedUnit] = useState<OrgUnitDto | null>(null);
    const [detailLoading, setDetailLoading] = useState(false);
    const [statusFilter, setStatusFilter] = useState<StatusFilter>('active');
    type FormMode = { mode: 'create'; parentId?: string } | { mode: 'edit'; unitId: string } | null;
    const [formMode, setFormMode] = useState<FormMode>(null);
    const [editUnitData, setEditUnitData] = useState<OrgUnitDto | null>(null);
    const [historyUnitId, setHistoryUnitId] = useState<string | null>(null);
    const [historyUnitName, setHistoryUnitName] = useState('');

    // ─── Загрузка организаций ───
    useEffect(() => {
        if (!token) return;
        getDirectoryOrganizations(token)
            .then(orgs => {
                setOrganizations(orgs);
                if (orgs.length > 0) setSelectedOrgId(orgs[0].id);
            })
            .catch(() => { /* нет организаций */ });
    }, [token]);

    // ─── Дебаунс поиска ───
    useEffect(() => {
        if (searchTimer.current !== null) window.clearTimeout(searchTimer.current);
        searchTimer.current = window.setTimeout(() => setDebouncedSearch(search), 300);
        return () => { if (searchTimer.current !== null) window.clearTimeout(searchTimer.current); };
    }, [search]);

    // ─── Загрузка органиграммы ───
    const loadChart = useCallback((orgId: string, searchQuery: string) => {
        if (!token || !orgId) return;
        setChartLoading(true);
        setChartError(null);
        getOrgChart(token, {
            organizationId: orgId,
            search: searchQuery || undefined,
            extended: canManage,
        })
            .then(setChartData)
            .catch(e => setChartError((e as Error).message ?? 'Ошибка загрузки'))
            .finally(() => setChartLoading(false));
    }, [token, canManage]);

    useEffect(() => {
        if ((activeTab === 'chart' || activeTab === 'list') && selectedOrgId) {
            loadChart(selectedOrgId, debouncedSearch);
        }
    }, [activeTab, selectedOrgId, debouncedSearch, loadChart]);

    // ─── Загрузка дерева управления ───
    const loadTree = useCallback((orgId: string, status: StatusFilter, searchQuery: string) => {
        if (!token || !orgId) return;
        setTreeLoading(true);
        setTreeError(null);

        const apiStatus: DepartmentStatus | undefined =
            status === 'active' ? DEPARTMENT_STATUS_ACTIVE :
            status === 'archived' ? DEPARTMENT_STATUS_ARCHIVED : undefined;

        getUnitsTree(token, { organizationId: orgId, status: apiStatus, search: searchQuery || undefined })
            .then(setTree)
            .catch(e => setTreeError((e as Error).message ?? 'Ошибка загрузки'))
            .finally(() => setTreeLoading(false));
    }, [token]);

    useEffect(() => {
        if (activeTab === 'manage' && selectedOrgId) {
            loadTree(selectedOrgId, statusFilter, debouncedSearch);
        }
    }, [activeTab, selectedOrgId, statusFilter, debouncedSearch, loadTree]);

    // ─── Смена организации ───
    const handleOrgChange = (orgId: string) => {
        setSelectedOrgId(orgId);
        setSelectedUnit(null);
        setSearch('');
    };

    // ─── Обработчики вкладки «Управление» ───
    const handleSelect = (id: string) => {
        if (!token) return;
        setDetailLoading(true);
        getUnitById(token, id)
            .then(setSelectedUnit)
            .catch(() => setSelectedUnit(null))
            .finally(() => setDetailLoading(false));
    };

    const handleAddChild = (parentId: string) => {
        setEditUnitData(null);
        setFormMode({ mode: 'create', parentId });
    };

    const handleCreate = () => {
        setEditUnitData(null);
        setFormMode({ mode: 'create' });
    };

    const handleEdit = async (id: string) => {
        if (!token) return;
        try {
            const unit = await getUnitById(token, id);
            setEditUnitData(unit);
            setFormMode({ mode: 'edit', unitId: id });
        } catch {
            // ignore
        }
    };

    const handleArchive = async (id: string) => {
        if (!token || !window.confirm('Архивировать подразделение?')) return;
        try {
            await archiveUnit(token, id);
            loadTree(selectedOrgId, statusFilter, debouncedSearch);
            if (selectedUnit?.id === id) setSelectedUnit(null);
        } catch (e: unknown) {
            alert(e instanceof Error ? e.message : 'Ошибка архивирования');
        }
    };

    const handleMove = async (unitId: string, newParentId: string | null) => {
        if (!token) return;
        try {
            await moveUnit(token, unitId, { newParentId: newParentId ?? undefined });
            loadTree(selectedOrgId, statusFilter, debouncedSearch);
            if (selectedUnit?.id === unitId) handleSelect(unitId);
        } catch (e: unknown) {
            alert(e instanceof Error ? e.message : 'Ошибка перемещения');
        }
    };

    const handleFormSave = async (data: CreateUnitRequest | UpdateUnitRequest) => {
        if (!token) return;
        if (formMode?.mode === 'create') {
            await createUnit(token, data as CreateUnitRequest);
        } else if (formMode?.mode === 'edit') {
            await updateUnit(token, formMode.unitId, data as UpdateUnitRequest);
        }
        setFormMode(null);
        setEditUnitData(null);
        loadTree(selectedOrgId, statusFilter, debouncedSearch);
    };

    const handleShowHistory = (id: string, name: string) => {
        setHistoryUnitId(id);
        setHistoryUnitName(name);
    };

    const showSearch = activeTab === 'chart' || activeTab === 'list' || activeTab === 'manage';

    return (
        <div className="org-root">
            {/* Тулбар */}
            <div className="org-toolbar">
                <h1 className="org-title">Оргструктура</h1>

                {/* Табы */}
                <div className="org-tabs" role="tablist">
                    {TABS.filter(t => !t.adminOnly || canManage).map(tab => (
                        <button
                            key={tab.id}
                            role="tab"
                            aria-selected={activeTab === tab.id}
                            className={`org-tab${activeTab === tab.id ? ' active' : ''}`}
                            onClick={() => { setActiveTab(tab.id); setSearch(''); }}
                        >
                            {tab.label}
                        </button>
                    ))}
                </div>

                <div className="org-toolbar-controls">
                    {/* Выбор организации */}
                    {organizations.length > 1 && (
                        <select
                            className="org-select"
                            value={selectedOrgId}
                            onChange={e => handleOrgChange(e.target.value)}
                            aria-label="Организация"
                        >
                            {organizations.map(o => (
                                <option key={o.id} value={o.id}>{o.name}</option>
                            ))}
                        </select>
                    )}

                    {/* Фильтр статуса — только в «Управление» */}
                    {activeTab === 'manage' && (
                        <div className="org-status-filter" role="group" aria-label="Фильтр по статусу">
                            {(['active', 'all', 'archived'] as StatusFilter[]).map(s => (
                                <button
                                    key={s}
                                    className={`org-status-btn${statusFilter === s ? ' active' : ''}`}
                                    onClick={() => setStatusFilter(s)}
                                >
                                    {s === 'active' ? 'Активные' : s === 'archived' ? 'Архивные' : 'Все'}
                                </button>
                            ))}
                        </div>
                    )}

                    {/* Поиск */}
                    {showSearch && (
                        <div className="org-search">
                            <span className="org-search-icon" aria-hidden="true">
                                <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" width="14" height="14">
                                    <circle cx="8.5" cy="8.5" r="5.5"/>
                                    <path d="M15 15l-3.5-3.5" strokeLinecap="round"/>
                                </svg>
                            </span>
                            <input
                                className="org-search-input"
                                type="search"
                                placeholder={activeTab === 'manage' ? 'Поиск по названию или коду…' : 'Поиск по ФИО, должности, подразделению…'}
                                value={search}
                                onChange={e => setSearch(e.target.value)}
                                aria-label="Поиск"
                            />
                            {search && (
                                <button className="org-search-clear" onClick={() => setSearch('')} aria-label="Очистить">×</button>
                            )}
                        </div>
                    )}

                    {/* Кнопка добавления — только в «Управление» */}
                    {activeTab === 'manage' && canManage && (
                        <button className="org-add-btn" onClick={handleCreate}>
                            + Добавить
                        </button>
                    )}
                </div>
            </div>

            {/* ─── Вкладка: Органиграмма ─── */}
            {activeTab === 'chart' && (
                <>
                    {chartLoading && <div className="org-status">Загрузка…</div>}
                    {!chartLoading && chartError && (
                        <div className="org-status org-status--error">{chartError}</div>
                    )}
                    {!chartLoading && !chartError && !selectedOrgId && (
                        <div className="org-status">Выберите организацию</div>
                    )}
                    {!chartLoading && !chartError && chartData && (
                        <OrgChartView
                            departments={chartData.departments}
                            unassignedEmployees={chartData.unassignedEmployees}
                            extended={canManage}
                        />
                    )}
                </>
            )}

            {/* ─── Вкладка: Список ─── */}
            {activeTab === 'list' && (
                <>
                    {chartLoading && <div className="org-status">Загрузка…</div>}
                    {!chartLoading && chartError && (
                        <div className="org-status org-status--error">{chartError}</div>
                    )}
                    {!chartLoading && !chartError && !selectedOrgId && (
                        <div className="org-status">Выберите организацию</div>
                    )}
                    {!chartLoading && !chartError && chartData && (
                        <OrgChartTable
                            departments={chartData.departments}
                            unassignedEmployees={chartData.unassignedEmployees}
                            search={debouncedSearch}
                            extended={canManage}
                        />
                    )}
                </>
            )}

            {/* ─── Вкладка: Управление ─── */}
            {activeTab === 'manage' && (
                <div className="org-body">
                    {/* Дерево */}
                    <div className="org-tree-panel">
                        {treeLoading && <div className="org-status">Загрузка…</div>}
                        {!treeLoading && treeError && (
                            <div className="org-status org-status--error">{treeError}</div>
                        )}
                        {!treeLoading && !treeError && !selectedOrgId && (
                            <div className="org-status">Выберите организацию</div>
                        )}
                        {!treeLoading && !treeError && selectedOrgId && (
                            <DepartmentTree
                                nodes={tree}
                                searchQuery={debouncedSearch}
                                canManage={canManage}
                                onSelect={handleSelect}
                                onAddChild={handleAddChild}
                                onEdit={handleEdit}
                                onArchive={handleArchive}
                                onMove={handleMove}
                            />
                        )}
                    </div>

                    {/* Панель деталей */}
                    <div className="org-detail-panel">
                        {detailLoading && <div className="org-status">Загрузка…</div>}
                        {!detailLoading && !selectedUnit && (
                            <div className="org-detail-empty">Выберите подразделение в дереве</div>
                        )}
                        {!detailLoading && selectedUnit && (
                            <div className="org-detail">
                                {selectedUnit.breadcrumb.length > 0 && (
                                    <nav className="org-breadcrumb" aria-label="Путь">
                                        {selectedUnit.breadcrumb.map((b, i) => (
                                            <span key={b.id} className="org-breadcrumb-item">
                                                {i > 0 && <span className="org-breadcrumb-sep">›</span>}
                                                {b.name}
                                            </span>
                                        ))}
                                    </nav>
                                )}

                                <div className="org-detail-header">
                                    <div>
                                        <h2 className="org-detail-name">{selectedUnit.name}</h2>
                                        {selectedUnit.shortName && (
                                            <span className="org-detail-short-name">{selectedUnit.shortName}</span>
                                        )}
                                    </div>
                                    <span className={`org-detail-status org-detail-status--${selectedUnit.status}`}>
                                        {selectedUnit.status === DEPARTMENT_STATUS_ACTIVE ? 'Активное' : 'Архивное'}
                                    </span>
                                </div>

                                <dl className="org-detail-meta">
                                    {selectedUnit.code && (
                                        <>
                                            <dt>Код</dt>
                                            <dd><code>{selectedUnit.code}</code></dd>
                                        </>
                                    )}
                                    <dt>Сотрудников</dt>
                                    <dd>
                                        {selectedUnit.directEmployeesCount} прямых
                                        {selectedUnit.totalEmployeesCount !== selectedUnit.directEmployeesCount && (
                                            <> / {selectedUnit.totalEmployeesCount} всего</>
                                        )}
                                    </dd>
                                    <dt>Создано</dt>
                                    <dd>{new Date(selectedUnit.createdAt).toLocaleDateString('ru-RU')}</dd>
                                    {selectedUnit.updatedAt !== selectedUnit.createdAt && (
                                        <>
                                            <dt>Обновлено</dt>
                                            <dd>{new Date(selectedUnit.updatedAt).toLocaleDateString('ru-RU')}</dd>
                                        </>
                                    )}
                                </dl>

                                {selectedUnit.description && (
                                    <p className="org-detail-description">{selectedUnit.description}</p>
                                )}

                                <div className="org-detail-actions">
                                    <button
                                        className="org-action-btn"
                                        onClick={() => handleShowHistory(selectedUnit.id, selectedUnit.name)}
                                    >
                                        История изменений
                                    </button>
                                    {canManage && (
                                        <>
                                            <button
                                                className="org-action-btn org-action-btn--primary"
                                                onClick={() => handleEdit(selectedUnit.id)}
                                            >
                                                Редактировать
                                            </button>
                                            {selectedUnit.status === DEPARTMENT_STATUS_ACTIVE && (
                                                <button
                                                    className="org-action-btn org-action-btn--danger"
                                                    onClick={() => handleArchive(selectedUnit.id)}
                                                >
                                                    Архивировать
                                                </button>
                                            )}
                                        </>
                                    )}
                                </div>
                            </div>
                        )}
                    </div>
                </div>
            )}

            {/* Форма создания/редактирования */}
            {formMode && (
                <DepartmentForm
                    editUnit={formMode.mode === 'edit' ? editUnitData : null}
                    parentId={formMode.mode === 'create' ? formMode.parentId : undefined}
                    organizationId={selectedOrgId}
                    allUnits={tree}
                    onSave={handleFormSave}
                    onClose={() => { setFormMode(null); setEditUnitData(null); }}
                />
            )}

            {/* Панель истории */}
            {historyUnitId && token && (
                <DepartmentHistory
                    unitId={historyUnitId}
                    unitName={historyUnitName}
                    token={token}
                    onClose={() => setHistoryUnitId(null)}
                />
            )}
        </div>
    );
}
