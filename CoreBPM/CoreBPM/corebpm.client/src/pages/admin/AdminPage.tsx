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
    PositionDto,
    CreatePositionRequest,
    UpdatePositionRequest,
    PositionCategory,
    PositionStatus,
    AssignmentDto,
    CreateAssignmentRequest,
    UpdateAssignmentRequest,
} from '../../api/adminApi';
import './AdminPage.css';

type Tab = 'organizations' | 'positions' | 'assignments' | 'users';

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
                    className={`admin-tab${activeTab === 'positions' ? ' active' : ''}`}
                    onClick={() => setActiveTab('positions')}
                >
                    Должности
                </button>
                <button
                    className={`admin-tab${activeTab === 'assignments' ? ' active' : ''}`}
                    onClick={() => setActiveTab('assignments')}
                >
                    Назначения
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
                {activeTab === 'positions' && <PositionsTab />}
                {activeTab === 'assignments' && <AssignmentsTab />}
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

    // Фильтры
    const [searchQuery, setSearchQuery] = useState('');
    const [showArchived, setShowArchived] = useState(false);

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

    // Загрузить должности при выборе организации или переключении фильтра архива
    const loadPositions = useCallback(async () => {
        if (!selectedOrgId) { setPositions([]); return; }
        setLoading(true);
        setError('');
        try {
            const status: PositionStatus | undefined = showArchived ? 'Archived' : 'Active';
            setPositions(await adminApi.getPositions(token, selectedOrgId, status));
        } catch (e) {
            setError(String(e));
        } finally {
            setLoading(false);
        }
    }, [token, selectedOrgId, showArchived]);

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

    // Клиентская фильтрация по строке поиска
    const q = searchQuery.trim().toLowerCase();
    const filteredPositions = q
        ? positions.filter(p =>
            p.name.toLowerCase().includes(q) ||
            (p.code ?? '').toLowerCase().includes(q) ||
            (p.description ?? '').toLowerCase().includes(q) ||
            (p.departmentName ?? '').toLowerCase().includes(q)
        )
        : positions;

    return (
        <>
            <div className="section-header">
                <h2>Должности</h2>
                {selectedOrgId && !showArchived && (
                    <button className="btn-primary" onClick={() => { setEditPosition(null); setShowForm(true); }}>
                        + Создать должность
                    </button>
                )}
            </div>

            {/* Строка фильтров */}
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: 16 }}>
                <div className="form-group" style={{ marginBottom: 0, minWidth: 240, maxWidth: 340, flex: '1 1 240px' }}>
                    <label>Организация</label>
                    <select value={selectedOrgId} onChange={e => setSelectedOrgId(e.target.value)}>
                        <option value="">— Выберите организацию —</option>
                        {orgs.map(o => (
                            <option key={o.id} value={o.id}>{o.name}</option>
                        ))}
                    </select>
                </div>

                <div className="form-group" style={{ marginBottom: 0, flex: '2 1 220px' }}>
                    <label>Поиск</label>
                    <input
                        type="search"
                        placeholder="Название, код, описание, подразделение…"
                        value={searchQuery}
                        onChange={e => setSearchQuery(e.target.value)}
                    />
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: 8, paddingBottom: 2, whiteSpace: 'nowrap' }}>
                    <input
                        id="show-archived-positions"
                        type="checkbox"
                        checked={showArchived}
                        onChange={e => setShowArchived(e.target.checked)}
                    />
                    <label htmlFor="show-archived-positions" style={{ margin: 0, cursor: 'pointer', userSelect: 'none' }}>
                        Показать архивные
                    </label>
                </div>
            </div>

            {error && <div className="error-msg">{error}</div>}

            {!selectedOrgId ? (
                <div className="empty-msg">Выберите организацию для просмотра должностей</div>
            ) : loading ? (
                <div className="loading-msg">Загрузка…</div>
            ) : filteredPositions.length === 0 ? (
                <div className="empty-msg">
                    {q ? 'Ничего не найдено по запросу' : showArchived ? 'Архивных должностей нет' : 'Должности не созданы'}
                </div>
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
                        {filteredPositions.map(pos => (
                            <tr key={pos.id} style={pos.status === 'Archived' ? { opacity: 0.65 } : undefined}>
                                <td>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                                        {pos.name}
                                        {pos.code && (
                                            <span style={{ fontSize: '0.78rem', color: '#94a3b8' }}>[{pos.code}]</span>
                                        )}
                                        {pos.status === 'Archived' && (
                                            <span className="badge badge-inactive" style={{ fontSize: '0.72rem' }}>Архив</span>
                                        )}
                                    </div>
                                    {pos.description && (
                                        <div style={{ fontSize: '0.78rem', color: '#94a3b8', marginTop: 2 }}>{pos.description}</div>
                                    )}
                                </td>
                                <td>{pos.departmentName ?? '—'}</td>
                                <td>{CATEGORY_LABELS[pos.category] ?? pos.category}</td>
                                <td>{pos.plannedHeadcount} / {pos.occupiedHeadcount}</td>
                                <td>
                                    <div className="row-actions">
                                        {pos.status !== 'Archived' && (
                                            <>
                                                <button className="btn-secondary btn-sm"
                                                    onClick={() => { setEditPosition(pos); setShowForm(true); }}>
                                                    Изменить
                                                </button>
                                                <button className="btn-danger btn-sm"
                                                    onClick={() => setConfirmArchiveId(pos.id)}>
                                                    Архивировать
                                                </button>
                                            </>
                                        )}
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
                    organizationId={selectedOrgId}
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
    organizationId: string;
    depts: DepartmentDto[];
    token: string;
    onClose: () => void;
    onSaved: () => void;
}

function PositionFormModal({ position, organizationId, depts, token, onClose, onSaved }: PositionFormProps) {
    const [name, setName] = useState(position?.name ?? '');
    const [code, setCode] = useState(position?.code ?? '');
    const [description, setDescription] = useState(position?.description ?? '');
    const [departmentId, setDepartmentId] = useState(position?.departmentId ?? '');
    const [category, setCategory] = useState<PositionCategory>(position?.category ?? 'Regular');
    const [status, setStatus] = useState<PositionStatus>(position?.status ?? 'Active');
    const [plannedHeadcount, setPlannedHeadcount] = useState(String(position?.plannedHeadcount ?? 1));
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);
    const [flashButtons, setFlashButtons] = useState(false);

    // При клике мимо модального окна подсвечиваем кнопки действий
    const handleOverlayClick = useCallback(() => {
        if (flashButtons) return;
        setFlashButtons(true);
        setTimeout(() => setFlashButtons(false), 900);
    }, [flashButtons]);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!name.trim()) { setError('Введите наименование должности'); return; }
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
                    departmentId: departmentId || undefined,
                    category,
                    status,
                    plannedHeadcount: headcount,
                };
                await adminApi.updatePosition(token, position.id, req);
            } else {
                const req: CreatePositionRequest = {
                    organizationId,
                    name: name.trim(),
                    code: code.trim() || undefined,
                    description: description.trim() || undefined,
                    departmentId: departmentId || undefined,
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
        <div className="modal-overlay" onClick={handleOverlayClick}>
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
                        <label>Подразделение</label>
                        <select value={departmentId} onChange={e => setDepartmentId(e.target.value)}>
                            <option value="">— Без привязки к подразделению —</option>
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
// Вкладка «Пользователи»
// ─────────────────────────────────────────────

function UsersTab() {
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
    const handleOverlayClick = useCallback(() => {
        if (flashButtons) return;
        setFlashButtons(true);
        setTimeout(() => setFlashButtons(false), 900);
    }, [flashButtons]);

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

// ─────────────────────────────────────────────
// Вкладка «Назначения»
// ─────────────────────────────────────────────

const RATE_OPTIONS: number[] = [0.25, 0.5, 0.75, 1.0];
const RATE_LABELS: Record<number, string> = {
    0.25: '0.25 (четверть ставки)',
    0.5: '0.5 (полставки)',
    0.75: '0.75 (три четверти)',
    1.0: '1.0 (полная ставка)',
};

function todayIso(): string {
    return new Date().toISOString().slice(0, 10);
}

function AssignmentsTab() {
    const { accessToken } = useAuth();
    const token = accessToken!;

    const [orgs, setOrgs] = useState<OrganizationDto[]>([]);
    const [selectedOrgId, setSelectedOrgId] = useState('');
    const [assignments, setAssignments] = useState<AssignmentDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [activeOnly, setActiveOnly] = useState(true);

    const [showForm, setShowForm] = useState(false);
    const [editAssignment, setEditAssignment] = useState<AssignmentDto | null>(null);
    const [confirmEndId, setConfirmEndId] = useState<string | null>(null);

    // Загрузить список организаций один раз
    useEffect(() => {
        adminApi.getOrganizations(token)
            .then(data => {
                const active = data.filter(o => o.isActive);
                setOrgs(active);
                const primary = active.find(o => o.isPrimary) ?? active[0];
                if (primary) setSelectedOrgId(primary.id);
            })
            .catch(e => setError(String(e)));
    }, [token]);

    const loadAssignments = useCallback(async () => {
        if (!selectedOrgId) { setAssignments([]); return; }
        setLoading(true);
        setError('');
        try {
            setAssignments(await adminApi.getAssignments(token, {
                organizationId: selectedOrgId,
                activeOnly: activeOnly || undefined,
            }));
        } catch (e) {
            setError(String(e));
        } finally {
            setLoading(false);
        }
    }, [token, selectedOrgId, activeOnly]);

    useEffect(() => { loadAssignments(); }, [loadAssignments]);

    const handleEndConfirmed = async () => {
        if (!confirmEndId) return;
        try {
            await adminApi.deleteAssignment(token, confirmEndId);
            await loadAssignments();
        } catch (e) { setError(String(e)); } finally {
            setConfirmEndId(null);
        }
    };

    const displayDate = (d?: string) => d ? d : '—';

    return (
        <>
            <div className="section-header">
                <h2>Назначения на должности</h2>
                {selectedOrgId && (
                    <button className="btn-primary" onClick={() => { setEditAssignment(null); setShowForm(true); }}>
                        + Назначить
                    </button>
                )}
            </div>

            {/* Фильтры */}
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: 16 }}>
                <div className="form-group" style={{ marginBottom: 0, minWidth: 240, maxWidth: 340, flex: '1 1 240px' }}>
                    <label>Организация</label>
                    <select value={selectedOrgId} onChange={e => setSelectedOrgId(e.target.value)}>
                        <option value="">— Выберите организацию —</option>
                        {orgs.map(o => (
                            <option key={o.id} value={o.id}>{o.name}</option>
                        ))}
                    </select>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, paddingBottom: 2, whiteSpace: 'nowrap' }}>
                    <input
                        id="assignments-active-only"
                        type="checkbox"
                        checked={activeOnly}
                        onChange={e => setActiveOnly(e.target.checked)}
                    />
                    <label htmlFor="assignments-active-only" style={{ margin: 0, cursor: 'pointer', userSelect: 'none' }}>
                        Только активные
                    </label>
                </div>
            </div>

            {error && <div className="error-msg">{error}</div>}

            {!selectedOrgId ? (
                <div className="empty-msg">Выберите организацию для просмотра назначений</div>
            ) : loading ? (
                <div className="loading-msg">Загрузка…</div>
            ) : assignments.length === 0 ? (
                <div className="empty-msg">{activeOnly ? 'Активных назначений нет' : 'Назначений нет'}</div>
            ) : (
                <table className="data-table">
                    <thead>
                        <tr>
                            <th>Пользователь</th>
                            <th>Должность / Подразделение</th>
                            <th>Ставка</th>
                            <th>Тип</th>
                            <th>Дата начала</th>
                            <th>Дата окончания</th>
                            <th>Статус</th>
                            <th>Действия</th>
                        </tr>
                    </thead>
                    <tbody>
                        {assignments.map(a => (
                            <tr key={a.id} style={!a.isActive ? { opacity: 0.6 } : undefined}>
                                <td>
                                    <div>{a.userDisplayName}</div>
                                    <div style={{ fontSize: '0.78rem', color: '#94a3b8' }}>{a.userWorkEmail}</div>
                                </td>
                                <td>
                                    <div>{a.positionName}</div>
                                    {a.departmentName && (
                                        <div style={{ fontSize: '0.78rem', color: '#94a3b8' }}>{a.departmentName}</div>
                                    )}
                                </td>
                                <td>{a.rate}</td>
                                <td>
                                    <span className={`badge ${a.isPrimary ? 'badge-primary' : 'badge-active'}`}>
                                        {a.isPrimary ? 'Основное' : 'Совмещение'}
                                    </span>
                                </td>
                                <td>{displayDate(a.startDate)}</td>
                                <td>{displayDate(a.endDate)}</td>
                                <td>
                                    <span className={`badge ${a.isActive ? 'badge-active' : 'badge-inactive'}`}>
                                        {a.isActive ? 'Активно' : 'Завершено'}
                                    </span>
                                </td>
                                <td>
                                    <div className="row-actions">
                                        {a.isActive && (
                                            <>
                                                <button className="btn-secondary btn-sm"
                                                    onClick={() => { setEditAssignment(a); setShowForm(true); }}>
                                                    Изменить
                                                </button>
                                                <button className="btn-danger btn-sm"
                                                    onClick={() => setConfirmEndId(a.id)}>
                                                    Завершить
                                                </button>
                                            </>
                                        )}
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}

            {showForm && (
                <AssignmentFormModal
                    assignment={editAssignment}
                    organizationId={selectedOrgId}
                    token={token}
                    onClose={() => setShowForm(false)}
                    onSaved={() => { setShowForm(false); loadAssignments(); }}
                />
            )}

            {confirmEndId && (
                <ConfirmModal
                    message="Завершить назначение? Дата окончания будет установлена на сегодня, роли должности будут сняты."
                    onConfirm={handleEndConfirmed}
                    onCancel={() => setConfirmEndId(null)}
                />
            )}
        </>
    );
}

interface AssignmentFormProps {
    assignment: AssignmentDto | null;
    organizationId: string;
    token: string;
    onClose: () => void;
    onSaved: () => void;
}

function AssignmentFormModal({ assignment, organizationId, token, onClose, onSaved }: AssignmentFormProps) {
    const [employees, setEmployees] = useState<EmployeeDto[]>([]);
    const [positions, setPositions] = useState<PositionDto[]>([]);
    const [loadingData, setLoadingData] = useState(true);

    const [userId, setUserId] = useState(assignment?.userId ?? '');
    const [positionId, setPositionId] = useState(assignment?.positionId ?? '');
    const [rate, setRate] = useState(String(assignment?.rate ?? 1.0));
    const [isPrimary, setIsPrimary] = useState(assignment?.isPrimary ?? true);
    const [startDate, setStartDate] = useState(assignment?.startDate ?? todayIso());
    const [endDate, setEndDate] = useState(assignment?.endDate ?? '');
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        if (!organizationId) return;
        setLoadingData(true);
        Promise.all([
            adminApi.getEmployees(token, organizationId),
            adminApi.getPositions(token, organizationId),
        ])
            .then(([emps, pos]) => {
                setEmployees(emps.filter(e => e.isActive));
                setPositions(pos);
            })
            .catch(e => setError(String(e)))
            .finally(() => setLoadingData(false));
    }, [token, organizationId]);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!userId) { setError('Выберите пользователя'); return; }
        if (!positionId) { setError('Выберите должность'); return; }
        const rateNum = parseFloat(rate);
        if (!RATE_OPTIONS.includes(rateNum)) { setError('Выберите допустимую ставку'); return; }
        setSaving(true);
        setError('');
        try {
            if (assignment) {
                const req: UpdateAssignmentRequest = {
                    rate: rateNum,
                    isPrimary,
                    startDate,
                    endDate: endDate || undefined,
                };
                await adminApi.updateAssignment(token, assignment.id, req);
            } else {
                const req: CreateAssignmentRequest = {
                    userId,
                    positionId,
                    rate: rateNum,
                    isPrimary,
                    startDate,
                    endDate: endDate || undefined,
                };
                await adminApi.createAssignment(token, req);
            }
            onSaved();
        } catch (e) {
            setError(String(e));
        } finally {
            setSaving(false);
        }
    };

    const isEdit = !!assignment;

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" onClick={e => e.stopPropagation()}>
                <h3>{isEdit ? 'Изменить назначение' : 'Назначить на должность'}</h3>
                {error && <div className="error-msg">{error}</div>}
                {loadingData ? (
                    <div className="loading-msg">Загрузка данных…</div>
                ) : (
                    <form onSubmit={handleSubmit}>
                        {!isEdit && (
                            <div className="form-group">
                                <label>Пользователь *</label>
                                <select value={userId} onChange={e => setUserId(e.target.value)}>
                                    <option value="">— Выберите пользователя —</option>
                                    {employees.map(emp => (
                                        <option key={emp.userId} value={emp.userId}>
                                            {emp.userDisplayName} ({emp.userWorkEmail})
                                        </option>
                                    ))}
                                </select>
                            </div>
                        )}
                        {isEdit && (
                            <div className="form-group">
                                <label>Пользователь</label>
                                <input value={assignment.userDisplayName} readOnly disabled />
                            </div>
                        )}
                        {!isEdit && (
                            <div className="form-group">
                                <label>Должность *</label>
                                <select value={positionId} onChange={e => setPositionId(e.target.value)}>
                                    <option value="">— Выберите должность —</option>
                                    {positions.map(p => (
                                        <option key={p.id} value={p.id}>
                                            {p.name}{p.departmentName ? ` (${p.departmentName})` : ''}
                                        </option>
                                    ))}
                                </select>
                            </div>
                        )}
                        {isEdit && (
                            <div className="form-group">
                                <label>Должность</label>
                                <input value={assignment.positionName} readOnly disabled />
                            </div>
                        )}
                        <div className="form-group">
                            <label>Ставка *</label>
                            <select value={rate} onChange={e => setRate(e.target.value)}>
                                {RATE_OPTIONS.map(r => (
                                    <option key={r} value={r}>{RATE_LABELS[r]}</option>
                                ))}
                            </select>
                        </div>
                        <div className="form-group">
                            <label className="form-check">
                                <input
                                    type="checkbox"
                                    checked={isPrimary}
                                    onChange={e => setIsPrimary(e.target.checked)}
                                />
                                Основное назначение
                            </label>
                        </div>
                        <div className="form-group">
                            <label>Дата начала *</label>
                            <input
                                type="date"
                                value={startDate}
                                onChange={e => setStartDate(e.target.value)}
                            />
                        </div>
                        <div className="form-group">
                            <label>Дата окончания (необязательно)</label>
                            <input
                                type="date"
                                value={endDate}
                                onChange={e => setEndDate(e.target.value)}
                            />
                        </div>
                        <div className="modal-actions">
                            <button type="button" className="btn-secondary" onClick={onClose}>Отмена</button>
                            <button type="submit" className="btn-primary" disabled={saving}>
                                {saving ? 'Сохранение…' : 'Сохранить'}
                            </button>
                        </div>
                    </form>
                )}
            </div>
        </div>
    );
}
