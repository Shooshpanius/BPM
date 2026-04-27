import { useState, useEffect, useCallback } from 'react';
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
} from '../../api/adminApi';
import './AdminPage.css';

type Tab = 'organizations' | 'users';

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
                    className={`admin-tab${activeTab === 'users' ? ' active' : ''}`}
                    onClick={() => setActiveTab('users')}
                >
                    Пользователи
                </button>
            </div>

            <div className="admin-content">
                {activeTab === 'organizations' && <OrganizationsTab />}
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

    const handleDelete = async (id: string) => {
        if (!confirm('Удалить организацию?')) return;
        try {
            await adminApi.deleteOrganization(token, id);
            await load();
        } catch (e) { alert(String(e)); }
    };

    const handleSetPrimary = async (id: string) => {
        try {
            await adminApi.setOrganizationPrimary(token, id);
            await load();
        } catch (e) { alert(String(e)); }
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
                                                    onClick={() => handleDelete(org.id)}>
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

    const handleDelete = async (id: string) => {
        if (!confirm('Деактивировать пользователя?')) return;
        try {
            await adminApi.deleteUser(token, id);
            await load();
        } catch (e) { alert(String(e)); }
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
                                                    onClick={() => handleDelete(user.id)}>
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
        </>
    );
}

interface UserFormProps {
    user: AdminUserListItemDto | null;
    token: string;
    onClose: () => void;
    onSaved: () => void;
}

function UserFormModal({ user, token, onClose, onSaved }: UserFormProps) {
    const [firstName, setFirstName] = useState(user?.firstName ?? '');
    const [lastName, setLastName] = useState(user?.lastName ?? '');
    const [middleName, setMiddleName] = useState(user?.middleName ?? '');
    const [workEmail, setWorkEmail] = useState(user?.workEmail ?? '');
    const [phone, setPhone] = useState(user?.phone ?? '');
    const [username, setUsername] = useState(user?.username ?? '');
    const [password, setPassword] = useState('');
    const [isActive, setIsActive] = useState(user?.isActive ?? true);
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);

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
        <div className="modal-overlay" onClick={onClose}>
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
                        <input type="email" value={workEmail} onChange={e => setWorkEmail(e.target.value)} placeholder="user@company.ru" />
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
                                <input type="password" value={password} onChange={e => setPassword(e.target.value)} />
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

    const [selectedOrg, setSelectedOrg] = useState('');
    const [position, setPosition] = useState('');
    const [adding, setAdding] = useState(false);

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

    const handleAdd = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!selectedOrg) { setError('Выберите организацию'); return; }
        setAdding(true);
        setError('');
        try {
            const req: CreateEmployeeRequest = {
                userId,
                organizationId: selectedOrg,
                position: position || undefined,
            };
            await adminApi.createEmployee(token, req);
            setSelectedOrg('');
            setPosition('');
            await load();
        } catch (e) {
            setError(String(e));
        } finally {
            setAdding(false);
        }
    };

    const handleRemove = async (id: string) => {
        if (!confirm('Удалить запись сотрудника?')) return;
        try {
            await adminApi.deleteEmployee(token, id);
            await load();
        } catch (e) { alert(String(e)); }
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
                                        <th>Должность</th>
                                        <th>Статус</th>
                                        <th></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {employees.map(emp => (
                                        <tr key={emp.id}>
                                            <td>{emp.organizationName}</td>
                                            <td>{emp.position ?? '—'}</td>
                                            <td>
                                                <span className={`badge ${emp.isActive ? 'badge-active' : 'badge-inactive'}`}>
                                                    {emp.isActive ? 'Активен' : 'Неактивен'}
                                                </span>
                                            </td>
                                            <td>
                                                <button className="btn-danger btn-sm"
                                                    onClick={() => handleRemove(emp.id)}>
                                                    Удалить
                                                </button>
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

                        {availableOrgs.length > 0 && (
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
                                        <label>Должность</label>
                                        <input value={position} onChange={e => setPosition(e.target.value)} placeholder="Менеджер" />
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

                        {availableOrgs.length === 0 && (
                            <div className="modal-actions">
                                <button className="btn-secondary" onClick={onClose}>Закрыть</button>
                            </div>
                        )}
                    </>
                )}
            </div>
        </div>
    );
}
