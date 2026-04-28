import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as adminApi from '../../api/adminApi';
import type {
    OrganizationDto,
    CreateOrganizationRequest,
    UpdateOrganizationRequest,
    AdminUserListItemDto,
    CreateUserRequest,
    UpdateUserRequest,
    EmployeeDto,
    CreateEmployeeRequest,
    UpdateEmployeeRequest,
    DepartmentDto,
    DepartmentTreeDto,
    CreateDepartmentRequest,
    UpdateDepartmentRequest,
    PositionDto,
    CreatePositionRequest,
    UpdatePositionRequest,
    PositionCategory,
    PositionStatus,
} from '../../api/adminApi';
import './AdminPage.css';

type Tab = 'organizations' | 'departments' | 'positions' | 'users';

interface AdminPageProps {
    onBack: () => void;
}

// ─────────────────────────────────────────────
// Компонент подтверждения действия
// ─────────────────────────────────────────────

interface ConfirmModalProps {
    message: string;
    onConfirm: () => void;
    onCancel: () => void;
}

function ConfirmModal({ message, onConfirm, onCancel }: ConfirmModalProps) {
    return (
        <div className="modal-overlay" onClick={onCancel}>
            <div className="modal" style={{ width: 380 }} onClick={e => e.stopPropagation()}>
                <h3>Подтверждение</h3>
                <p style={{ margin: '0 0 20px', color: '#374151', fontSize: '0.9rem' }}>{message}</p>
                <div className="modal-actions">
                    <button className="btn-secondary" onClick={onCancel}>Отмена</button>
                    <button className="btn-danger" onClick={onConfirm}>Подтвердить</button>
                </div>
            </div>
        </div>
    );
}

interface AdminPageProps {
    onBack: () => void;
}

/** Административная панель: управление организациями, пользователями и сотрудниками. */
export function AdminPage({ onBack }: AdminPageProps) {
    const [activeTab, setActiveTab] = useState<Tab>('organizations');

    return (
        <div className="admin-root">
            <header className="admin-header">
                <div className="admin-header-brand">
                    <span>⬡ Core BPM</span>
                    <span className="brand-sep">›</span>
                    <span>Администрирование</span>
                </div>
                <button className="admin-back-btn" onClick={onBack}>← Назад</button>
            </header>

            <div className="admin-tabs">
                <button
                    className={`admin-tab${activeTab === 'organizations' ? ' active' : ''}`}
                    onClick={() => setActiveTab('organizations')}
                >
                    Организации
                </button>
                <button
                    className={`admin-tab${activeTab === 'departments' ? ' active' : ''}`}
                    onClick={() => setActiveTab('departments')}
                >
                    Подразделения
                </button>
                <button
                    className={`admin-tab${activeTab === 'positions' ? ' active' : ''}`}
                    onClick={() => setActiveTab('positions')}
                >
                    Должности
                </button>
                <button
                    className={`admin-tab${activeTab === 'users' ? ' active' : ''}`}
                    onClick={() => setActiveTab('users')}
                >
                    Пользователи
                </button>
            </div>

            <div className="admin-content">
                {activeTab === 'organizations' && <OrganizationsTab />}
                {activeTab === 'departments' && <DepartmentsTab />}
                {activeTab === 'positions' && <PositionsTab />}
                {activeTab === 'users' && <UsersTab />}
            </div>
        </div>
    );
}

// ─────────────────────────────────────────────
// Вкладка «Организации»
// ─────────────────────────────────────────────

function OrganizationsTab() {
    const { accessToken } = useAuth();
    const token = accessToken!;

    const [orgs, setOrgs] = useState<OrganizationDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    const [showForm, setShowForm] = useState(false);
    const [editOrg, setEditOrg] = useState<OrganizationDto | null>(null);
    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        setError('');
        try {
            setOrgs(await adminApi.getOrganizations(token));
        } catch (e) {
            setError(String(e));
        } finally {
            setLoading(false);
        }
    }, [token]);

    useEffect(() => { load(); }, [load]);

    const handleDeleteConfirmed = async () => {
        if (!confirmDeleteId) return;
        try {
            await adminApi.deleteOrganization(token, confirmDeleteId);
            await load();
        } catch (e) { setError(String(e)); } finally {
            setConfirmDeleteId(null);
        }
    };

    const handleSetPrimary = async (id: string) => {
        try {
            await adminApi.setOrganizationPrimary(token, id);
            await load();
        } catch (e) { setError(String(e)); }
    };

    return (
        <>
            <div className="section-header">
                <h2>Организации</h2>
                <button className="btn-primary" onClick={() => { setEditOrg(null); setShowForm(true); }}>
                    + Создать организацию
                </button>
            </div>

            {error && <div className="error-msg">{error}</div>}
            {loading
                ? <div className="loading-msg">Загрузка…</div>
                : orgs.length === 0
                    ? <div className="empty-msg">Организации не созданы</div>
                    : (
                        <table className="data-table">
                            <thead>
                                <tr>
                                    <th>Наименование</th>
                                    <th>Статус</th>
                                    <th>Сотрудников</th>
                                    <th>Действия</th>
                                </tr>
                            </thead>
                            <tbody>
                                {orgs.map(org => (
                                    <tr key={org.id}>
                                        <td>
                                            {org.name}
                                            {org.isPrimary && (
                                                <span className="badge badge-primary" style={{ marginLeft: 8 }}>Основная</span>
                                            )}
                                            {org.description && (
                                                <div style={{ fontSize: '0.78rem', color: '#94a3b8', marginTop: 2 }}>{org.description}</div>
                                            )}
                                        </td>
                                        <td>
                                            <span className={`badge ${org.isActive ? 'badge-active' : 'badge-inactive'}`}>
                                                {org.isActive ? 'Активна' : 'Неактивна'}
                                            </span>
                                        </td>
                                        <td>{org.employeesCount}</td>
                                        <td>
                                            <div className="row-actions">
                                                <button className="btn-secondary btn-sm"
                                                    onClick={() => { setEditOrg(org); setShowForm(true); }}>
                                                    Изменить
                                                </button>
                                                {!org.isPrimary && org.isActive && (
                                                    <button className="btn-secondary btn-sm"
                                                        onClick={() => handleSetPrimary(org.id)}>
                                                        Сделать основной
                                                    </button>
                                                )}
                                                <button className="btn-danger btn-sm"
                                                    onClick={() => setConfirmDeleteId(org.id)}>
                                                    Удалить
                                                </button>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )
            }

            {showForm && (
                <OrganizationFormModal
                    org={editOrg}
                    token={token}
                    onClose={() => setShowForm(false)}
                    onSaved={() => { setShowForm(false); load(); }}
                />
            )}

            {confirmDeleteId && (
                <ConfirmModal
                    message="Удалить организацию? Это действие необратимо."
                    onConfirm={handleDeleteConfirmed}
                    onCancel={() => setConfirmDeleteId(null)}
                />
            )}
        </>
    );
}

interface OrgFormProps {
    org: OrganizationDto | null;
    token: string;
    onClose: () => void;
    onSaved: () => void;
}

function OrganizationFormModal({ org, token, onClose, onSaved }: OrgFormProps) {
    const [name, setName] = useState(org?.name ?? '');
    const [description, setDescription] = useState(org?.description ?? '');
    const [isPrimary, setIsPrimary] = useState(org?.isPrimary ?? false);
    const [isActive, setIsActive] = useState(org?.isActive ?? true);
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!name.trim()) { setError('Введите наименование организации'); return; }
        setSaving(true);
        setError('');
        try {
            if (org) {
                const req: UpdateOrganizationRequest = { name, description: description || undefined, isPrimary, isActive };
                await adminApi.updateOrganization(token, org.id, req);
            } else {
                const req: CreateOrganizationRequest = { name, description: description || undefined, isPrimary, isActive };
                await adminApi.createOrganization(token, req);
            }
            onSaved();
        } catch (e) {
            setError(String(e));
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" onClick={e => e.stopPropagation()}>
                <h3>{org ? 'Редактировать организацию' : 'Создать организацию'}</h3>
                {error && <div className="error-msg">{error}</div>}
                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Наименование *</label>
                        <input value={name} onChange={e => setName(e.target.value)} placeholder="ООО «Компания»" />
                    </div>
                    <div className="form-group">
                        <label>Описание</label>
                        <textarea value={description} onChange={e => setDescription(e.target.value)} placeholder="Краткое описание" />
                    </div>
                    <div className="form-group">
                        <label className="form-check">
                            <input type="checkbox" checked={isPrimary} onChange={e => setIsPrimary(e.target.checked)} />
                            Основная организация
                        </label>
                    </div>
                    <div className="form-group">
                        <label className="form-check">
                            <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
                            Активна
                        </label>
                    </div>
                    <div className="modal-actions">
                        <button type="button" className="btn-secondary" onClick={onClose}>Отмена</button>
                        <button type="submit" className="btn-primary" disabled={saving}>
                            {saving ? 'Сохранение…' : 'Сохранить'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}

// ─────────────────────────────────────────────
// Вкладка «Подразделения»
// ─────────────────────────────────────────────

interface DeptFormTrigger {
    orgId: string;
    parentId?: string;
    dept: DepartmentDto | null;
}

function DepartmentsTab() {
    const { accessToken } = useAuth();
    const token = accessToken!;

    const [orgs, setOrgs] = useState<OrganizationDto[]>([]);
    const [tree, setTree] = useState<DepartmentTreeDto[]>([]);
    const [selectedOrgId, setSelectedOrgId] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const [formTrigger, setFormTrigger] = useState<DeptFormTrigger | null>(null);
    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    // Загрузить список организаций один раз
    useEffect(() => {
        setLoading(true);
        adminApi.getOrganizations(token)
            .then(data => {
                setOrgs(data);
                // Автовыбор основной или первой организации
                const primary = data.find(o => o.isPrimary && o.isActive) ?? data.find(o => o.isActive);
                if (primary) setSelectedOrgId(primary.id);
            })
            .catch(e => setError(String(e)))
            .finally(() => setLoading(false));
    }, [token]);

    const loadTree = useCallback(async () => {
        if (!selectedOrgId) { setTree([]); return; }
        setLoading(true);
        setError('');
        try {
            setTree(await adminApi.getDepartmentsTree(token, selectedOrgId));
        } catch (e) {
            setError(String(e));
        } finally {
            setLoading(false);
        }
    }, [token, selectedOrgId]);

    useEffect(() => { loadTree(); }, [loadTree]);

    const handleDeleteConfirmed = async () => {
        if (!confirmDeleteId) return;
        try {
            await adminApi.deleteDepartment(token, confirmDeleteId);
            await loadTree();
        } catch (e) { setError(String(e)); } finally {
            setConfirmDeleteId(null);
        }
    };

    const openCreate = (orgId: string, parentId?: string) =>
        setFormTrigger({ orgId, parentId, dept: null });

    const openEdit = (dept: DepartmentDto) =>
        setFormTrigger({ orgId: dept.organizationId, dept });

    // Получить плоский dept по id из дерева (нужен для редактирования)
    const handleEditFromTree = async (nodeId: string) => {
        // Загружаем полный DepartmentDto для редактирования
        try {
            const dept = await adminApi.getDepartmentById(token, nodeId);
            openEdit(dept);
        } catch (e) { setError(String(e)); }
    };

    return (
        <>
            <div className="section-header">
                <h2>Подразделения</h2>
                {selectedOrgId && (
                    <button className="btn-primary" onClick={() => openCreate(selectedOrgId)}>
                        + Создать подразделение
                    </button>
                )}
            </div>

            <div className="form-group" style={{ marginBottom: 16, maxWidth: 340 }}>
                <label>Организация</label>
                <select value={selectedOrgId} onChange={e => setSelectedOrgId(e.target.value)}>
                    <option value="">— Выберите организацию —</option>
                    {orgs.map(o => (
                        <option key={o.id} value={o.id}>{o.name}</option>
                    ))}
                </select>
            </div>

            {error && <div className="error-msg">{error}</div>}

            {!selectedOrgId ? (
                <div className="empty-msg">Выберите организацию для просмотра структуры подразделений</div>
            ) : loading ? (
                <div className="loading-msg">Загрузка…</div>
            ) : tree.length === 0 ? (
                <div className="empty-msg">Подразделения не созданы</div>
            ) : (
                <div className="dept-tree">
                    {tree.map(node => (
                        <DepartmentTreeNode
                            key={node.id}
                            node={node}
                            level={0}
                            orgId={selectedOrgId}
                            onAddChild={(parentId) => openCreate(selectedOrgId, parentId)}
                            onEdit={handleEditFromTree}
                            onDelete={(id) => setConfirmDeleteId(id)}
                        />
                    ))}
                </div>
            )}

            {formTrigger && (
                <DepartmentFormModal
                    dept={formTrigger.dept}
                    orgs={orgs}
                    presetOrgId={formTrigger.orgId}
                    presetParentId={formTrigger.parentId}
                    token={token}
                    onClose={() => setFormTrigger(null)}
                    onSaved={() => { setFormTrigger(null); loadTree(); }}
                />
            )}

            {confirmDeleteId && (
                <ConfirmModal
                    message="Удалить подразделение? Это невозможно, если есть дочерние подразделения или активные сотрудники."
                    onConfirm={handleDeleteConfirmed}
                    onCancel={() => setConfirmDeleteId(null)}
                />
            )}
        </>
    );
}

// ─────────────────────────────────────────────
// Узел дерева подразделений
// ─────────────────────────────────────────────

interface DepartmentTreeNodeProps {
    node: DepartmentTreeDto;
    level: number;
    orgId: string;
    onAddChild: (parentId: string) => void;
    onEdit: (id: string) => void;
    onDelete: (id: string) => void;
}

function DepartmentTreeNode({ node, level, orgId, onAddChild, onEdit, onDelete }: DepartmentTreeNodeProps) {
    const [expanded, setExpanded] = useState(true);
    const hasChildren = node.children.length > 0;

    return (
        <div className="dept-tree-node" style={{ marginLeft: level * 24 }}>
            <div className={`dept-tree-row${!node.isActive ? ' dept-inactive' : ''}`}>
                <button
                    className="dept-tree-toggle"
                    onClick={() => setExpanded(v => !v)}
                    disabled={!hasChildren}
                    title={hasChildren ? (expanded ? 'Свернуть' : 'Развернуть') : undefined}
                >
                    {hasChildren ? (expanded ? '▾' : '▸') : '·'}
                </button>
                <span className="dept-tree-name">
                    {node.name}
                    {!node.isActive && <span className="badge badge-inactive" style={{ marginLeft: 6 }}>Неактивно</span>}
                    {node.description && (
                        <span className="dept-tree-desc"> — {node.description}</span>
                    )}
                </span>
                <span className="dept-tree-count" title="Сотрудников">{node.employeesCount > 0 ? `👤 ${node.employeesCount}` : ''}</span>
                <div className="row-actions dept-tree-actions">
                    <button
                        className="btn-secondary btn-sm btn-icon"
                        title="Добавить вложенное подразделение"
                        onClick={() => onAddChild(node.id)}
                    >
                        +
                    </button>
                    <button className="btn-secondary btn-sm" onClick={() => onEdit(node.id)}>
                        Изменить
                    </button>
                    <button className="btn-danger btn-sm" onClick={() => onDelete(node.id)}>
                        Удалить
                    </button>
                </div>
            </div>
            {hasChildren && expanded && (
                <div className="dept-tree-children">
                    {node.children.map(child => (
                        <DepartmentTreeNode
                            key={child.id}
                            node={child}
                            level={level + 1}
                            orgId={orgId}
                            onAddChild={onAddChild}
                            onEdit={onEdit}
                            onDelete={onDelete}
                        />
                    ))}
                </div>
            )}
        </div>
    );
}

interface DepartmentFormProps {
    dept: DepartmentDto | null;
    orgs: OrganizationDto[];
    presetOrgId?: string;
    presetParentId?: string;
    token: string;
    onClose: () => void;
    onSaved: () => void;
}

function DepartmentFormModal({ dept, orgs, presetOrgId, presetParentId, token, onClose, onSaved }: DepartmentFormProps) {
    const [orgId, setOrgId] = useState(dept?.organizationId ?? presetOrgId ?? '');
    const [parentId, setParentId] = useState(dept?.parentId ?? presetParentId ?? '');
    const [name, setName] = useState(dept?.name ?? '');
    const [description, setDescription] = useState(dept?.description ?? '');
    const [isActive, setIsActive] = useState(dept?.isActive ?? true);
    const [siblings, setSiblings] = useState<DepartmentDto[]>([]);
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);

    // Загрузить подразделения выбранной организации для списка родителей
    useEffect(() => {
        if (!orgId) { setSiblings([]); return; }
        adminApi.getDepartments(token, orgId).then(data => {
            setSiblings(dept ? data.filter(d => d.id !== dept.id) : data);
        }).catch(() => setSiblings([]));
    }, [orgId, token, dept]);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!name.trim()) { setError('Введите наименование подразделения'); return; }
        if (!orgId) { setError('Выберите организацию'); return; }
        setSaving(true);
        setError('');
        try {
            if (dept) {
                const req: UpdateDepartmentRequest = {
                    name,
                    description: description || undefined,
                    parentId: parentId || undefined,
                    isActive,
                };
                await adminApi.updateDepartment(token, dept.id, req);
            } else {
                const req: CreateDepartmentRequest = {
                    organizationId: orgId,
                    name,
                    description: description || undefined,
                    parentId: parentId || undefined,
                };
                await adminApi.createDepartment(token, req);
            }
            onSaved();
        } catch (e) {
            setError(String(e));
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" onClick={e => e.stopPropagation()}>
                <h3>{dept ? 'Редактировать подразделение' : 'Создать подразделение'}</h3>
                {error && <div className="error-msg">{error}</div>}
                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Организация *</label>
                        <select value={orgId} onChange={e => { setOrgId(e.target.value); setParentId(''); }} disabled={!!dept || !!presetOrgId}>
                            <option value="">— Выберите организацию —</option>
                            {orgs.map(o => (
                                <option key={o.id} value={o.id}>{o.name}</option>
                            ))}
                        </select>
                    </div>
                    <div className="form-group">
                        <label>Родительское подразделение</label>
                        <select value={parentId} onChange={e => setParentId(e.target.value)} disabled={!orgId}>
                            <option value="">— Корневое подразделение —</option>
                            {siblings.map(d => (
                                <option key={d.id} value={d.id}>{d.name}</option>
                            ))}
                        </select>
                    </div>
                    <div className="form-group">
                        <label>Наименование *</label>
                        <input value={name} onChange={e => setName(e.target.value)} placeholder="Отдел разработки" />
                    </div>
                    <div className="form-group">
                        <label>Описание</label>
                        <textarea value={description} onChange={e => setDescription(e.target.value)} placeholder="Краткое описание" />
                    </div>
                    {dept && (
                        <div className="form-group">
                            <label className="form-check">
                                <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
                                Активно
                            </label>
                        </div>
                    )}
                    <div className="modal-actions">
                        <button type="button" className="btn-secondary" onClick={onClose}>Отмена</button>
                        <button type="submit" className="btn-primary" disabled={saving}>
                            {saving ? 'Сохранение…' : 'Сохранить'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}
// ─────────────────────────────────────────────
// Вкладка «Должности»
// ─────────────────────────────────────────────

function PositionsTab() {
    const { accessToken } = useAuth();
    const token = accessToken!;

    const [orgs, setOrgs] = useState<OrganizationDto[]>([]);
    const [selectedOrgId, setSelectedOrgId] = useState('');
    const [positions, setPositions] = useState<PositionDto[]>([]);
    const [depts, setDepts] = useState<DepartmentDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const [showForm, setShowForm] = useState(false);
    const [editPosition, setEditPosition] = useState<PositionDto | null>(null);
    const [confirmArchiveId, setConfirmArchiveId] = useState<string | null>(null);

    // Загрузить список организаций один раз
    useEffect(() => {
        adminApi.getOrganizations(token)
            .then(data => {
                setOrgs(data.filter(o => o.isActive));
                const primary = data.find(o => o.isPrimary && o.isActive) ?? data.find(o => o.isActive);
                if (primary) setSelectedOrgId(primary.id);
            })
            .catch(e => setError(String(e)));
    }, [token]);

    // Загрузить должности при выборе организации
    const loadPositions = useCallback(async () => {
        if (!selectedOrgId) { setPositions([]); return; }
        setLoading(true);
        setError('');
        try {
            setPositions(await adminApi.getPositions(token, selectedOrgId));
        } catch (e) {
            setError(String(e));
        } finally {
            setLoading(false);
        }
    }, [token, selectedOrgId]);

    useEffect(() => { loadPositions(); }, [loadPositions]);

    // Загрузить подразделения для формы
    useEffect(() => {
        if (!selectedOrgId) { setDepts([]); return; }
        adminApi.getDepartments(token, selectedOrgId)
            .then(data => setDepts(data.filter(d => d.isActive)))
            .catch(() => setDepts([]));
    }, [selectedOrgId, token]);

    const handleArchiveConfirmed = async () => {
        if (!confirmArchiveId) return;
        try {
            await adminApi.archivePosition(token, confirmArchiveId);
            await loadPositions();
        } catch (e) { setError(String(e)); } finally {
            setConfirmArchiveId(null);
        }
    };

    const CATEGORY_LABELS: Record<string, string> = {
        Managerial: 'Руководящая',
        Regular: 'Рядовая',
        Project: 'Проектная',
    };

    return (
        <>
            <div className="section-header">
                <h2>Должности</h2>
                {selectedOrgId && (
                    <button className="btn-primary" onClick={() => { setEditPosition(null); setShowForm(true); }}>
                        + Создать должность
                    </button>
                )}
            </div>

            <div className="form-group" style={{ marginBottom: 16, maxWidth: 340 }}>
                <label>Организация</label>
                <select value={selectedOrgId} onChange={e => setSelectedOrgId(e.target.value)}>
                    <option value="">— Выберите организацию —</option>
                    {orgs.map(o => (
                        <option key={o.id} value={o.id}>{o.name}</option>
                    ))}
                </select>
            </div>

            {error && <div className="error-msg">{error}</div>}

            {!selectedOrgId ? (
                <div className="empty-msg">Выберите организацию для просмотра должностей</div>
            ) : loading ? (
                <div className="loading-msg">Загрузка…</div>
            ) : positions.length === 0 ? (
                <div className="empty-msg">Должности не созданы</div>
            ) : (
                <table className="data-table">
                    <thead>
                        <tr>
                            <th>Наименование</th>
                            <th>Подразделение</th>
                            <th>Категория</th>
                            <th>Ставок (план / занято)</th>
                            <th>Действия</th>
                        </tr>
                    </thead>
                    <tbody>
                        {positions.map(pos => (
                            <tr key={pos.id}>
                                <td>
                                    {pos.name}
                                    {pos.code && (
                                        <span style={{ marginLeft: 6, fontSize: '0.78rem', color: '#94a3b8' }}>[{pos.code}]</span>
                                    )}
                                    {pos.description && (
                                        <div style={{ fontSize: '0.78rem', color: '#94a3b8', marginTop: 2 }}>{pos.description}</div>
                                    )}
                                </td>
                                <td>{pos.departmentName}</td>
                                <td>{CATEGORY_LABELS[pos.category] ?? pos.category}</td>
                                <td>{pos.plannedHeadcount} / {pos.occupiedHeadcount}</td>
                                <td>
                                    <div className="row-actions">
                                        <button className="btn-secondary btn-sm"
                                            onClick={() => { setEditPosition(pos); setShowForm(true); }}>
                                            Изменить
                                        </button>
                                        <button className="btn-danger btn-sm"
                                            onClick={() => setConfirmArchiveId(pos.id)}>
                                            Архивировать
                                        </button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}

            {showForm && (
                <PositionFormModal
                    position={editPosition}
                    depts={depts}
                    token={token}
                    onClose={() => setShowForm(false)}
                    onSaved={() => { setShowForm(false); loadPositions(); }}
                />
            )}

            {confirmArchiveId && (
                <ConfirmModal
                    message="Архивировать должность? Она не будет доступна для назначения новым сотрудникам."
                    onConfirm={handleArchiveConfirmed}
                    onCancel={() => setConfirmArchiveId(null)}
                />
            )}
        </>
    );
}

interface PositionFormProps {
    position: PositionDto | null;
    depts: DepartmentDto[];
    token: string;
    onClose: () => void;
    onSaved: () => void;
}

function PositionFormModal({ position, depts, token, onClose, onSaved }: PositionFormProps) {
    const [name, setName] = useState(position?.name ?? '');
    const [code, setCode] = useState(position?.code ?? '');
    const [description, setDescription] = useState(position?.description ?? '');
    const [departmentId, setDepartmentId] = useState(position?.departmentId ?? '');
    const [category, setCategory] = useState<PositionCategory>(position?.category ?? 'Regular');
    const [status, setStatus] = useState<PositionStatus>(position?.status ?? 'Active');
    const [plannedHeadcount, setPlannedHeadcount] = useState(String(position?.plannedHeadcount ?? 1));
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!name.trim()) { setError('Введите наименование должности'); return; }
        if (!departmentId) { setError('Выберите подразделение'); return; }
        const headcount = parseFloat(plannedHeadcount);
        if (isNaN(headcount) || headcount <= 0) { setError('Плановое число ставок должно быть больше нуля'); return; }
        setSaving(true);
        setError('');
        try {
            if (position) {
                const req: UpdatePositionRequest = {
                    name: name.trim(),
                    code: code.trim() || undefined,
                    description: description.trim() || undefined,
                    departmentId,
                    category,
                    status,
                    plannedHeadcount: headcount,
                };
                await adminApi.updatePosition(token, position.id, req);
            } else {
                const req: CreatePositionRequest = {
                    name: name.trim(),
                    code: code.trim() || undefined,
                    description: description.trim() || undefined,
                    departmentId,
                    category,
                    plannedHeadcount: headcount,
                };
                await adminApi.createPosition(token, req);
            }
            onSaved();
        } catch (e) {
            setError(String(e));
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" onClick={e => e.stopPropagation()}>
                <h3>{position ? 'Редактировать должность' : 'Создать должность'}</h3>
                {error && <div className="error-msg">{error}</div>}
                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Наименование *</label>
                        <input value={name} onChange={e => setName(e.target.value)} placeholder="Ведущий инженер" />
                    </div>
                    <div className="form-group">
                        <label>Код</label>
                        <input value={code} onChange={e => setCode(e.target.value)} placeholder="ENG-01" />
                    </div>
                    <div className="form-group">
                        <label>Подразделение *</label>
                        <select value={departmentId} onChange={e => setDepartmentId(e.target.value)} disabled={!!position}>
                            <option value="">— Выберите подразделение —</option>
                            {depts.map(d => (
                                <option key={d.id} value={d.id}>{d.name}</option>
                            ))}
                        </select>
                    </div>
                    <div className="form-group">
                        <label>Категория</label>
                        <select value={category} onChange={e => setCategory(e.target.value as PositionCategory)}>
                            <option value="Regular">Рядовая</option>
                            <option value="Managerial">Руководящая</option>
                            <option value="Project">Проектная</option>
                        </select>
                    </div>
                    {position && (
                        <div className="form-group">
                            <label>Статус</label>
                            <select value={status} onChange={e => setStatus(e.target.value as PositionStatus)}>
                                <option value="Active">Активна</option>
                                <option value="Archived">Архивирована</option>
                            </select>
                        </div>
                    )}
                    <div className="form-group">
                        <label>Плановое число ставок *</label>
                        <input
                            type="number"
                            min="0.5"
                            step="0.5"
                            value={plannedHeadcount}
                            onChange={e => setPlannedHeadcount(e.target.value)}
                        />
                    </div>
                    <div className="form-group">
                        <label>Описание</label>
                        <textarea value={description} onChange={e => setDescription(e.target.value)} placeholder="Краткое описание обязанностей" />
                    </div>
                    <div className="modal-actions">
                        <button type="button" className="btn-secondary" onClick={onClose}>Отмена</button>
                        <button type="submit" className="btn-primary" disabled={saving}>
                            {saving ? 'Сохранение…' : 'Сохранить'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}



    const { accessToken } = useAuth();
    const token = accessToken!;

    const [users, setUsers] = useState<AdminUserListItemDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    const [showForm, setShowForm] = useState(false);
    const [editUser, setEditUser] = useState<AdminUserListItemDto | null>(null);
    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    // Карточка сотрудника (для добавления в организацию)
    const [employeeUserId, setEmployeeUserId] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        setError('');
        try {
            setUsers(await adminApi.getUsers(token));
        } catch (e) {
            setError(String(e));
        } finally {
            setLoading(false);
        }
    }, [token]);

    useEffect(() => { load(); }, [load]);

    const handleDeleteConfirmed = async () => {
        if (!confirmDeleteId) return;
        try {
            await adminApi.deleteUser(token, confirmDeleteId);
            await load();
        } catch (e) { setError(String(e)); } finally {
            setConfirmDeleteId(null);
        }
    };

    return (
        <>
            <div className="section-header">
                <h2>Пользователи системы</h2>
                <button className="btn-primary" onClick={() => { setEditUser(null); setShowForm(true); }}>
                    + Создать пользователя
                </button>
            </div>

            {error && <div className="error-msg">{error}</div>}
            {loading
                ? <div className="loading-msg">Загрузка…</div>
                : users.length === 0
                    ? <div className="empty-msg">Пользователи не найдены</div>
                    : (
                        <table className="data-table">
                            <thead>
                                <tr>
                                    <th>ФИО</th>
                                    <th>Email</th>
                                    <th>Логин</th>
                                    <th>Статус</th>
                                    <th>Действия</th>
                                </tr>
                            </thead>
                            <tbody>
                                {users.map(user => (
                                    <tr key={user.id}>
                                        <td>
                                            {user.displayName}
                                            {user.phone && (
                                                <div style={{ fontSize: '0.78rem', color: '#94a3b8', marginTop: 2 }}>{user.phone}</div>
                                            )}
                                        </td>
                                        <td>{user.workEmail}</td>
                                        <td>{user.username ?? '—'}</td>
                                        <td>
                                            <span className={`badge ${user.isActive ? 'badge-active' : 'badge-inactive'}`}>
                                                {user.isActive ? 'Активен' : 'Неактивен'}
                                            </span>
                                        </td>
                                        <td>
                                            <div className="row-actions">
                                                <button className="btn-secondary btn-sm"
                                                    onClick={() => { setEditUser(user); setShowForm(true); }}>
                                                    Изменить
                                                </button>
                                                <button className="btn-secondary btn-sm"
                                                    onClick={() => setEmployeeUserId(user.id)}>
                                                    Сотрудник
                                                </button>
                                                <button className="btn-danger btn-sm"
                                                    onClick={() => setConfirmDeleteId(user.id)}>
                                                    Деактивировать
                                                </button>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )
            }

            {showForm && (
                <UserFormModal
                    user={editUser}
                    token={token}
                    onClose={() => setShowForm(false)}
                    onSaved={() => { setShowForm(false); load(); }}
                />
            )}

            {employeeUserId && (
                <EmployeeModal
                    userId={employeeUserId}
                    userName={users.find(u => u.id === employeeUserId)?.displayName ?? ''}
                    token={token}
                    onClose={() => setEmployeeUserId(null)}
                />
            )}

            {confirmDeleteId && (
                <ConfirmModal
                    message="Деактивировать пользователя? Он не сможет войти в систему."
                    onConfirm={handleDeleteConfirmed}
                    onCancel={() => setConfirmDeleteId(null)}
                />
            )}
        </>
    );
}

interface UserFormProps {
    user: AdminUserListItemDto | null;
    token: string;
    onClose: () => void;
    onSaved: () => void;
}

// ─── Утилиты транслитерации и генерации ───────────────────────────────────────

const TRANSLIT_MAP: Record<string, string> = {
    'а': 'a',  'б': 'b',  'в': 'v',  'г': 'g',  'д': 'd',
    'е': 'e',  'ё': 'yo', 'ж': 'zh', 'з': 'z',  'и': 'i',
    'й': 'y',  'к': 'k',  'л': 'l',  'м': 'm',  'н': 'n',
    'о': 'o',  'п': 'p',  'р': 'r',  'с': 's',  'т': 't',
    'у': 'u',  'ф': 'f',  'х': 'kh', 'ц': 'ts', 'ч': 'ch',
    'ш': 'sh', 'щ': 'shch', 'ъ': '', 'ы': 'y',  'ь': '',
    'э': 'e',  'ю': 'yu', 'я': 'ya',
};

function transliterate(str: string): string {
    return str.toLowerCase().split('').map(ch => TRANSLIT_MAP[ch] ?? ch).join('');
}

function buildEmail(lastName: string, firstName: string): string {
    const last = transliterate(lastName.trim());
    const first = firstName.trim() ? transliterate(firstName.trim()[0]) : '';
    if (!last) return '';
    return `${last}${first}@local`;
}

function generatePassword(): string {
    const lower = 'abcdefghijklmnopqrstuvwxyz';
    const upper = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
    const digits = '0123456789';
    const special = '!@#$%&*';
    const all = lower + upper + digits + special;
    const randomIndex = (max: number) => {
        const arr = new Uint32Array(1);
        crypto.getRandomValues(arr);
        return arr[0] % max;
    };
    const pick = (s: string) => s[randomIndex(s.length)];
    // Гарантируем по одному символу каждого типа
    const base = [pick(lower), pick(upper), pick(digits), pick(special)];
    for (let i = base.length; i < 12; i++) base.push(pick(all));
    // Криптографически стойкое перемешивание (Fisher-Yates)
    for (let i = base.length - 1; i > 0; i--) {
        const j = randomIndex(i + 1);
        [base[i], base[j]] = [base[j], base[i]];
    }
    return base.join('');
}

// ──────────────────────────────────────────────────────────────────────────────

function UserFormModal({ user, token, onClose, onSaved }: UserFormProps) {
    const [firstName, setFirstName] = useState(user?.firstName ?? '');
    const [lastName, setLastName] = useState(user?.lastName ?? '');
    const [middleName, setMiddleName] = useState(user?.middleName ?? '');
    const [workEmail, setWorkEmail] = useState(user?.workEmail ?? '');
    const [phone, setPhone] = useState(user?.phone ?? '');
    const [username, setUsername] = useState(user?.username ?? '');
    const [password, setPassword] = useState(() => !user ? generatePassword() : '');
    const [showPassword, setShowPassword] = useState(false);
    const [isActive, setIsActive] = useState(user?.isActive ?? true);
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);
    const [flashButtons, setFlashButtons] = useState(false);

    // Признак того, что email был изменён вручную (отключает автозаполнение)
    const emailManuallyEdited = useRef(!!user);

    // Автозаполнение email по ФИО в режиме создания
    useEffect(() => {
        if (!user && !emailManuallyEdited.current) {
            const generated = buildEmail(lastName, firstName);
            if (generated) setWorkEmail(generated);
        }
    }, [firstName, lastName, user]);

    const handleEmailChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        emailManuallyEdited.current = true;
        setWorkEmail(e.target.value);
    };

    // При клике мимо модального окна подсвечиваем кнопки действий
    const handleOverlayClick = () => {
        if (flashButtons) return;
        setFlashButtons(true);
        setTimeout(() => setFlashButtons(false), 900);
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setSaving(true);
        setError('');
        try {
            if (user) {
                const req: UpdateUserRequest = {
                    firstName, lastName, middleName: middleName || undefined,
                    workEmail, phone: phone || undefined, isActive,
                };
                await adminApi.updateUser(token, user.id, req);
            } else {
                const req: CreateUserRequest = {
                    firstName, lastName, middleName: middleName || undefined,
                    workEmail, phone: phone || undefined, username, password, isActive,
                };
                await adminApi.createUser(token, req);
            }
            onSaved();
        } catch (e) {
            setError(String(e));
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="modal-overlay" onClick={handleOverlayClick}>
            <div className="modal" onClick={e => e.stopPropagation()}>
                <h3>{user ? 'Редактировать пользователя' : 'Создать пользователя'}</h3>
                {error && <div className="error-msg">{error}</div>}
                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Фамилия *</label>
                        <input value={lastName} onChange={e => setLastName(e.target.value)} placeholder="Иванов" />
                    </div>
                    <div className="form-group">
                        <label>Имя *</label>
                        <input value={firstName} onChange={e => setFirstName(e.target.value)} placeholder="Иван" />
                    </div>
                    <div className="form-group">
                        <label>Отчество</label>
                        <input value={middleName} onChange={e => setMiddleName(e.target.value)} placeholder="Иванович" />
                    </div>
                    <div className="form-group">
                        <label>Email *</label>
                        <input type="email" value={workEmail} onChange={handleEmailChange} placeholder="user@company.ru" />
                    </div>
                    <div className="form-group">
                        <label>Телефон</label>
                        <input value={phone} onChange={e => setPhone(e.target.value)} placeholder="+7 900 000 0000" />
                    </div>
                    {!user && (
                        <>
                            <div className="form-group">
                                <label>Логин *</label>
                                <input value={username} onChange={e => setUsername(e.target.value)} placeholder="ivanov" />
                            </div>
                            <div className="form-group">
                                <label>Пароль * (мин. 8 символов)</label>
                                <div className="password-input-wrap">
                                    <input
                                        type={showPassword ? 'text' : 'password'}
                                        value={password}
                                        onChange={e => setPassword(e.target.value)}
                                    />
                                    <button
                                        type="button"
                                        className="btn-show-password"
                                        onClick={() => setShowPassword(v => !v)}
                                        title={showPassword ? 'Скрыть пароль' : 'Показать пароль'}
                                    >
                                        {showPassword ? '🙈' : '👁'}
                                    </button>
                                </div>
                            </div>
                        </>
                    )}
                    <div className="form-group">
                        <label className="form-check">
                            <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
                            Активен
                        </label>
                    </div>
                    <div className="modal-actions">
                        <button
                            type="button"
                            className={`btn-secondary${flashButtons ? ' btn-flash' : ''}`}
                            onClick={onClose}
                        >
                            Отмена
                        </button>
                        <button
                            type="submit"
                            className={`btn-primary${flashButtons ? ' btn-flash' : ''}`}
                            disabled={saving}
                        >
                            {saving ? 'Сохранение…' : 'Сохранить'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}

// ─────────────────────────────────────────────
// Модальное окно: управление сотрудниками
// ─────────────────────────────────────────────

interface EmployeeModalProps {
    userId: string;
    userName: string;
    token: string;
    onClose: () => void;
}

function EmployeeModal({ userId, userName, token, onClose }: EmployeeModalProps) {
    const [employees, setEmployees] = useState<EmployeeDto[]>([]);
    const [orgs, setOrgs] = useState<OrganizationDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    // Форма добавления
    const [selectedOrg, setSelectedOrg] = useState('');
    const [selectedDept, setSelectedDept] = useState('');
    const [depts, setDepts] = useState<DepartmentDto[]>([]);
    const [deptsLoading, setDeptsLoading] = useState(false);
    const [positions, setPositions] = useState<PositionDto[]>([]);
    const [positionsLoading, setPositionsLoading] = useState(false);
    const [selectedPositionId, setSelectedPositionId] = useState('');
    const [adding, setAdding] = useState(false);

    // Форма редактирования
    const [editEmployee, setEditEmployee] = useState<EmployeeDto | null>(null);
    const [editDeptId, setEditDeptId] = useState('');
    const [editPositionId, setEditPositionId] = useState('');
    const [editIsActive, setEditIsActive] = useState(true);
    const [editDepts, setEditDepts] = useState<DepartmentDto[]>([]);
    const [editDeptsLoading, setEditDeptsLoading] = useState(false);
    const [editPositions, setEditPositions] = useState<PositionDto[]>([]);
    const [editPositionsLoading, setEditPositionsLoading] = useState(false);
    const [saving, setSaving] = useState(false);

    const [confirmRemoveId, setConfirmRemoveId] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const [emps, orgsData] = await Promise.all([
                adminApi.getUserEmployees(token, userId),
                adminApi.getOrganizations(token),
            ]);
            setEmployees(emps);
            setOrgs(orgsData.filter(o => o.isActive));
        } catch (e) {
            setError(String(e));
        } finally {
            setLoading(false);
        }
    }, [token, userId]);

    useEffect(() => { load(); }, [load]);

    // Загрузить подразделения при выборе организации (форма добавления)
    useEffect(() => {
        if (!selectedOrg) { setDepts([]); setSelectedDept(''); setPositions([]); setSelectedPositionId(''); return; }
        setDeptsLoading(true);
        adminApi.getDepartments(token, selectedOrg)
            .then(data => { setDepts(data.filter(d => d.isActive)); setSelectedDept(''); })
            .catch(() => setDepts([]))
            .finally(() => setDeptsLoading(false));
        setPositionsLoading(true);
        adminApi.getPositions(token, selectedOrg)
            .then(data => { setPositions(data); setSelectedPositionId(''); })
            .catch(() => setPositions([]))
            .finally(() => setPositionsLoading(false));
    }, [selectedOrg, token]);

    // Загрузить подразделения и должности при открытии формы редактирования
    useEffect(() => {
        if (!editEmployee) { setEditDepts([]); setEditPositions([]); return; }
        setEditDeptsLoading(true);
        adminApi.getDepartments(token, editEmployee.organizationId)
            .then(data => setEditDepts(data.filter(d => d.isActive)))
            .catch(e => { setEditDepts([]); setError(String(e)); })
            .finally(() => setEditDeptsLoading(false));
        setEditPositionsLoading(true);
        adminApi.getPositions(token, editEmployee.organizationId)
            .then(data => setEditPositions(data))
            .catch(() => setEditPositions([]))
            .finally(() => setEditPositionsLoading(false));
    }, [editEmployee, token]);

    const openEdit = (emp: EmployeeDto) => {
        setEditEmployee(emp);
        setEditDeptId(emp.departmentId ?? '');
        setEditPositionId(emp.positionId ?? '');
        setEditIsActive(emp.isActive);
    };

    const handleAdd = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!selectedOrg) { setError('Выберите организацию'); return; }
        if (!selectedDept) { setError('Выберите подразделение'); return; }
        setAdding(true);
        setError('');
        try {
            const req: CreateEmployeeRequest = {
                userId,
                organizationId: selectedOrg,
                departmentId: selectedDept,
                positionId: selectedPositionId || undefined,
            };
            await adminApi.createEmployee(token, req);
            setSelectedOrg('');
            setSelectedDept('');
            setDepts([]);
            setSelectedPositionId('');
            setPositions([]);
            await load();
        } catch (e) {
            setError(String(e));
        } finally {
            setAdding(false);
        }
    };

    const handleSaveEdit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!editEmployee) return;
        if (!editDeptId) { setError('Выберите подразделение'); return; }
        setSaving(true);
        setError('');
        try {
            const req: UpdateEmployeeRequest = {
                departmentId: editDeptId,
                positionId: editPositionId || undefined,
                isActive: editIsActive,
            };
            await adminApi.updateEmployee(token, editEmployee.id, req);
            setEditEmployee(null);
            await load();
        } catch (e) {
            setError(String(e));
        } finally {
            setSaving(false);
        }
    };

    const handleRemoveConfirmed = async () => {
        if (!confirmRemoveId) return;
        try {
            await adminApi.deleteEmployee(token, confirmRemoveId);
            await load();
        } catch (e) { setError(String(e)); } finally {
            setConfirmRemoveId(null);
        }
    };

    // Организации, в которых пользователь ещё не является сотрудником
    const availableOrgs = orgs.filter(o => !employees.some(e => e.organizationId === o.id));

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" style={{ width: 560 }} onClick={e => e.stopPropagation()}>
                <h3>Сотрудник: {userName}</h3>
                {error && <div className="error-msg">{error}</div>}

                {loading ? <div className="loading-msg">Загрузка…</div> : (
                    <>
                        {employees.length > 0 ? (
                            <table className="employee-sub-table" style={{ marginBottom: 20 }}>
                                <thead>
                                    <tr>
                                        <th>Организация</th>
                                        <th>Подразделение</th>
                                        <th>Должность</th>
                                        <th>Статус</th>
                                        <th></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {employees.map(emp => (
                                        <tr key={emp.id}>
                                            <td>{emp.organizationName}</td>
                                            <td>{emp.departmentName ?? '—'}</td>
                                            <td>{emp.positionName ?? '—'}</td>
                                            <td>
                                                <span className={`badge ${emp.isActive ? 'badge-active' : 'badge-inactive'}`}>
                                                    {emp.isActive ? 'Активен' : 'Неактивен'}
                                                </span>
                                            </td>
                                            <td>
                                                <div className="row-actions">
                                                    <button className="btn-secondary btn-sm"
                                                        onClick={() => openEdit(emp)}>
                                                        Изменить
                                                    </button>
                                                    <button className="btn-danger btn-sm"
                                                        onClick={() => setConfirmRemoveId(emp.id)}>
                                                        Удалить
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        ) : (
                            <div className="empty-msg" style={{ padding: '16px 0' }}>
                                Пользователь не является сотрудником ни одной организации
                            </div>
                        )}

                        {/* Форма редактирования сотрудника */}
                        {editEmployee && (
                            <>
                                <hr style={{ border: 'none', borderTop: '1px solid #e2e8f0', margin: '0 0 16px' }} />
                                <p style={{ fontWeight: 600, fontSize: '0.875rem', margin: '0 0 12px', color: '#374151' }}>
                                    Редактировать запись в «{editEmployee.organizationName}»:
                                </p>
                                <form onSubmit={handleSaveEdit}>
                                    <div className="form-group">
                                        <label>Подразделение *</label>
                                        <select
                                            value={editDeptId}
                                            onChange={e => setEditDeptId(e.target.value)}
                                            disabled={editDeptsLoading}
                                        >
                                            <option value="">
                                                {editDeptsLoading ? 'Загрузка…' : '— Выберите подразделение —'}
                                            </option>
                                            {editDepts.map(d => (
                                                <option key={d.id} value={d.id}>{d.name}</option>
                                            ))}
                                        </select>
                                    </div>
                                    <div className="form-group">
                                        <label>Должность</label>
                                        <select
                                            value={editPositionId}
                                            onChange={e => setEditPositionId(e.target.value)}
                                            disabled={editPositionsLoading}
                                        >
                                            <option value="">
                                                {editPositionsLoading ? 'Загрузка…' : '— Без должности —'}
                                            </option>
                                            {editPositions.map(p => (
                                                <option key={p.id} value={p.id}>{p.name}</option>
                                            ))}
                                        </select>
                                    </div>
                                    <div className="form-group">
                                        <label className="form-check">
                                            <input type="checkbox" checked={editIsActive} onChange={e => setEditIsActive(e.target.checked)} />
                                            Активен
                                        </label>
                                    </div>
                                    <div className="modal-actions">
                                        <button type="button" className="btn-secondary" onClick={() => setEditEmployee(null)}>Отмена</button>
                                        <button type="submit" className="btn-primary" disabled={saving}>
                                            {saving ? 'Сохранение…' : 'Сохранить'}
                                        </button>
                                    </div>
                                </form>
                            </>
                        )}

                        {/* Форма добавления в организацию */}
                        {!editEmployee && availableOrgs.length > 0 && (
                            <>
                                <hr style={{ border: 'none', borderTop: '1px solid #e2e8f0', margin: '0 0 16px' }} />
                                <p style={{ fontWeight: 600, fontSize: '0.875rem', margin: '0 0 12px', color: '#374151' }}>
                                    Добавить в организацию:
                                </p>
                                <form onSubmit={handleAdd}>
                                    <div className="form-group">
                                        <label>Организация *</label>
                                        <select value={selectedOrg} onChange={e => setSelectedOrg(e.target.value)}>
                                            <option value="">— Выберите организацию —</option>
                                            {availableOrgs.map(o => (
                                                <option key={o.id} value={o.id}>{o.name}</option>
                                            ))}
                                        </select>
                                    </div>
                                    <div className="form-group">
                                        <label>Подразделение *</label>
                                        <select
                                            value={selectedDept}
                                            onChange={e => setSelectedDept(e.target.value)}
                                            disabled={!selectedOrg || deptsLoading}
                                        >
                                            <option value="">
                                                {deptsLoading ? 'Загрузка…' : '— Выберите подразделение —'}
                                            </option>
                                            {depts.map(d => (
                                                <option key={d.id} value={d.id}>{d.name}</option>
                                            ))}
                                        </select>
                                    </div>
                                    <div className="form-group">
                                        <label>Должность</label>
                                        <select
                                            value={selectedPositionId}
                                            onChange={e => setSelectedPositionId(e.target.value)}
                                            disabled={!selectedOrg || positionsLoading}
                                        >
                                            <option value="">
                                                {positionsLoading ? 'Загрузка…' : '— Без должности —'}
                                            </option>
                                            {positions.map(p => (
                                                <option key={p.id} value={p.id}>{p.name}</option>
                                            ))}
                                        </select>
                                    </div>
                                    <div className="modal-actions">
                                        <button type="button" className="btn-secondary" onClick={onClose}>Закрыть</button>
                                        <button type="submit" className="btn-primary" disabled={adding}>
                                            {adding ? 'Добавление…' : 'Добавить'}
                                        </button>
                                    </div>
                                </form>
                            </>
                        )}

                        {!editEmployee && availableOrgs.length === 0 && (
                            <div className="modal-actions">
                                <button className="btn-secondary" onClick={onClose}>Закрыть</button>
                            </div>
                        )}
                    </>
                )}
            </div>

            {confirmRemoveId && (
                <ConfirmModal
                    message="Удалить запись сотрудника? Это действие необратимо."
                    onConfirm={handleRemoveConfirmed}
                    onCancel={() => setConfirmRemoveId(null)}
                />
            )}
        </div>
    );
}
