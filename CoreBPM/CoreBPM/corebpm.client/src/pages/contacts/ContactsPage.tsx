import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/orgDirectoryApi';
import type {
    DirectoryOrganizationDto,
    DirectoryDepartmentTreeDto,
    DirectoryEmployeeDto,
} from '../../api/orgDirectoryApi';
import './ContactsPage.css';

// ─── Вспомогательные функции ───

function getInitials(emp: DirectoryEmployeeDto): string {
    const first = emp.firstName?.[0] ?? '';
    const last = emp.lastName?.[0] ?? '';
    if (first || last) return (first + last).toUpperCase();
    return emp.displayName[0]?.toUpperCase() ?? '?';
}

/** Страница адресной книги: дерево оргструктуры + карточки сотрудников + поиск. */
export function ContactsPage() {
    const { accessToken: token } = useAuth();

    // Данные дерева
    const [organizations, setOrganizations] = useState<DirectoryOrganizationDto[]>([]);
    const [treeByOrg, setTreeByOrg] = useState<Record<string, DirectoryDepartmentTreeDto[]>>({});
    const [expandedOrgs, setExpandedOrgs] = useState<Set<string>>(new Set());
    const [expandedDepts, setExpandedDepts] = useState<Set<string>>(new Set());
    const [treeLoading, setTreeLoading] = useState(false);

    // Выделение узла
    const [selectedOrgId, setSelectedOrgId] = useState<string | null>(null);
    const [selectedDeptId, setSelectedDeptId] = useState<string | null>(null);

    // Сотрудники
    const [employees, setEmployees] = useState<DirectoryEmployeeDto[]>([]);
    const [empLoading, setEmpLoading] = useState(false);
    const [empError, setEmpError] = useState<string | null>(null);

    // Поиск
    const [search, setSearch] = useState('');
    const [searchScope, setSearchScope] = useState<'current' | 'global'>('current');
    const searchTimer = useRef<number | null>(null);

    // Загрузка организаций при монтировании
    useEffect(() => {
        if (!token) return;
        setTreeLoading(true);
        api.getDirectoryOrganizations(token)
            .then(orgs => {
                setOrganizations(orgs);
                // Раскрываем и выбираем первую организацию
                if (orgs.length > 0) {
                    const first = orgs[0];
                    setExpandedOrgs(new Set([first.id]));
                    setSelectedOrgId(first.id);
                    loadDepartmentTree(first.id);
                }
            })
            .catch(() => {/* игнорируем, дерево просто останется пустым */})
            .finally(() => setTreeLoading(false));
    }, [token]); // eslint-disable-line react-hooks/exhaustive-deps

    const loadDepartmentTree = useCallback(async (orgId: string) => {
        if (!token) return;
        setTreeByOrg(prev => {
            if (prev[orgId]) return prev; // уже загружено
            return prev;
        });
        // Читаем актуальное значение через setter-callback, чтобы не добавлять treeByOrg в зависимости
        setTreeByOrg(prev => {
            if (prev[orgId]) return prev;
            // Запускаем загрузку асинхронно
            api.getDirectoryDepartmentTree(token, orgId)
                .then(tree => setTreeByOrg(p => ({ ...p, [orgId]: tree })))
                .catch(() => {/* игнорируем */});
            // Временно ставим пустой массив как признак «загружается»
            return { ...prev, [orgId]: [] };
        });
    }, [token]); // eslint-disable-line react-hooks/exhaustive-deps

    // Загрузка сотрудников при смене выбора или поиска
    useEffect(() => {
        if (!token) return;
        if (searchTimer.current !== null) window.clearTimeout(searchTimer.current);

        const performLoad = () => {
            const isGlobal = searchScope === 'global' && search.trim().length > 0;
            setEmpLoading(true);
            setEmpError(null);
            api.getDirectoryEmployees(token, {
                organizationId: isGlobal ? undefined : (selectedDeptId ? undefined : selectedOrgId ?? undefined),
                departmentId: isGlobal ? undefined : (selectedDeptId ?? undefined),
                search: search.trim() || undefined,
            })
                .then(setEmployees)
                .catch(e => setEmpError(e.message ?? 'Ошибка загрузки'))
                .finally(() => setEmpLoading(false));
        };

        if (search.trim()) {
            searchTimer.current = window.setTimeout(performLoad, 300);
        } else {
            performLoad();
        }

        return () => {
            if (searchTimer.current !== null) window.clearTimeout(searchTimer.current);
        };
    }, [token, selectedOrgId, selectedDeptId, search, searchScope]); // eslint-disable-line react-hooks/exhaustive-deps

    // ─── Обработчики дерева ───

    const toggleOrg = async (orgId: string) => {
        const isExpanded = expandedOrgs.has(orgId);
        // Аккордеон: только одна организация развёрнута одновременно
        setExpandedOrgs(isExpanded ? new Set() : new Set([orgId]));
        setSelectedOrgId(orgId);
        setSelectedDeptId(null);
        setSearch('');
        if (!isExpanded) await loadDepartmentTree(orgId);
    };

    const selectOrg = (orgId: string) => {
        setSelectedOrgId(orgId);
        setSelectedDeptId(null);
        setSearch('');
    };

    const toggleDept = (deptId: string) => {
        setExpandedDepts(prev => {
            const next = new Set(prev);
            if (next.has(deptId)) next.delete(deptId); else next.add(deptId);
            return next;
        });
    };

    const selectDept = (deptId: string, orgId: string) => {
        setSelectedOrgId(orgId);
        setSelectedDeptId(deptId);
        setSearch('');
    };

    // ─── Рендер дерева ───

    const renderDeptTree = (nodes: DirectoryDepartmentTreeDto[], orgId: string, depth = 0): React.ReactNode => {
        return nodes.map(node => (
            <div key={node.id} className="tree-dept-wrapper">
                <div
                    className={`tree-node dept-node${selectedDeptId === node.id ? ' selected' : ''}`}
                    style={{ paddingLeft: `${16 + depth * 14}px` }}
                    onClick={() => {
                        if (node.children.length > 0) toggleDept(node.id);
                        selectDept(node.id, orgId);
                    }}
                >
                    {node.children.length > 0 && (
                        <span className={`tree-arrow${expandedDepts.has(node.id) ? ' open' : ''}`}>›</span>
                    )}
                    {node.children.length === 0 && <span className="tree-arrow-placeholder" />}
                    <span className="tree-icon dept-icon" aria-hidden="true">
                        <svg viewBox="0 0 20 20" fill="currentColor" width="14" height="14"><path d="M3 4a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V4zm0 6a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-6zm10 0a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1h-2a1 1 0 0 1-1-1v-6z"/></svg>
                    </span>
                    <span className="tree-name">{node.name}</span>
                    {node.employeesCount > 0 && (
                        <span className="tree-count">{node.employeesCount}</span>
                    )}
                </div>
                {expandedDepts.has(node.id) && node.children.length > 0 && (
                    <div className="tree-children">
                        {renderDeptTree(node.children, orgId, depth + 1)}
                    </div>
                )}
            </div>
        ));
    };

    const renderTree = () => {
        if (treeLoading && organizations.length === 0) {
            return <div className="tree-loading">Загрузка…</div>;
        }
        return organizations.map(org => (
            <div key={org.id} className="tree-org-wrapper">
                <div
                    className={`tree-node org-node${selectedOrgId === org.id && !selectedDeptId ? ' selected' : ''}`}
                    onClick={() => { toggleOrg(org.id); selectOrg(org.id); }}
                >
                    <span className={`tree-arrow${expandedOrgs.has(org.id) ? ' open' : ''}`}>›</span>
                    <span className="tree-icon org-icon" aria-hidden="true">
                        <svg viewBox="0 0 20 20" fill="currentColor" width="14" height="14"><path fillRule="evenodd" d="M4 4a2 2 0 012-2h8a2 2 0 012 2v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4zm3 1h6v4H7V5zm-1 7a1 1 0 011-1h1a1 1 0 110 2H7a1 1 0 01-1-1zm5 0a1 1 0 011-1h1a1 1 0 110 2h-1a1 1 0 01-1-1z" clipRule="evenodd"/></svg>
                    </span>
                    <span className="tree-name">{org.name}</span>
                    {org.employeesCount > 0 && (
                        <span className="tree-count">{org.employeesCount}</span>
                    )}
                </div>
                {expandedOrgs.has(org.id) && (
                    <div className="tree-children">
                        {treeByOrg[org.id]
                            ? renderDeptTree(treeByOrg[org.id], org.id)
                            : <div className="tree-loading">Загрузка…</div>
                        }
                    </div>
                )}
            </div>
        ));
    };

    // ─── Хлебные крошки ───

    const breadcrumb = (() => {
        const parts: string[] = [];
        if (selectedOrgId) {
            const org = organizations.find(o => o.id === selectedOrgId);
            if (org) parts.push(org.name);
        }
        if (selectedDeptId) {
            // Ищем имя подразделения в дереве
            const findName = (nodes: DirectoryDepartmentTreeDto[]): string | null => {
                for (const n of nodes) {
                    if (n.id === selectedDeptId) return n.name;
                    const found = findName(n.children);
                    if (found) return found;
                }
                return null;
            };
            if (selectedOrgId && treeByOrg[selectedOrgId]) {
                const name = findName(treeByOrg[selectedOrgId]);
                if (name) parts.push(name);
            }
        }
        if (searchScope === 'global' && search.trim()) parts.push('глобальный поиск');
        return parts.join(' › ');
    })();

    // ─── Рендер карточек ───

    const renderEmployeeCard = (emp: DirectoryEmployeeDto) => {
        const initials = getInitials(emp);
        return (
            <div className="emp-card" key={emp.id}>
                <div className="emp-avatar" aria-hidden="true">
                    {emp.avatarUrl
                        ? <img src={emp.avatarUrl} alt={emp.displayName} className="emp-avatar-img" />
                        : <span className="emp-avatar-initials">{initials}</span>
                    }
                </div>
                <div className="emp-info">
                    <span className="emp-name">{emp.displayName}</span>
                    {emp.position && <span className="emp-position">{emp.position}</span>}
                    <a className="emp-email" href={`mailto:${emp.workEmail}`}>{emp.workEmail}</a>
                    {emp.phone && <span className="emp-phone">{emp.phone}</span>}
                    {emp.departmentName && <span className="emp-dept">{emp.departmentName}</span>}
                </div>
            </div>
        );
    };

    return (
        <div className="contacts-root">
            {/* Панель дерева */}
            <aside className="contacts-tree-panel" aria-label="Оргструктура">
                <div className="tree-header">Оргструктура</div>
                <div className="tree-scroll">
                    {renderTree()}
                </div>
            </aside>

            {/* Панель сотрудников */}
            <section className="contacts-main-panel">
                {/* Строка поиска */}
                <div className="contacts-search-bar">
                    <div className="contacts-search-field">
                        <span className="search-icon" aria-hidden="true">
                            <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" width="16" height="16"><circle cx="8.5" cy="8.5" r="5.5"/><path d="M15 15l-3.5-3.5" strokeLinecap="round"/></svg>
                        </span>
                        <input
                            className="search-input"
                            type="search"
                            placeholder="Поиск по имени, email, должности…"
                            value={search}
                            onChange={e => setSearch(e.target.value)}
                            aria-label="Поиск сотрудников"
                        />
                        {search && (
                            <button className="search-clear" onClick={() => setSearch('')} aria-label="Очистить поиск">×</button>
                        )}
                    </div>
                    {search.trim() && (
                        <div className="search-scope-toggle">
                            <button
                                className={`scope-btn${searchScope === 'current' ? ' active' : ''}`}
                                onClick={() => setSearchScope('current')}
                            >В разделе</button>
                            <button
                                className={`scope-btn${searchScope === 'global' ? ' active' : ''}`}
                                onClick={() => setSearchScope('global')}
                            >Глобально</button>
                        </div>
                    )}
                </div>

                {/* Заголовок раздела */}
                <div className="contacts-section-title">
                    {breadcrumb || 'Контакты'}
                    <span className="contacts-count">
                        {!empLoading && employees.length > 0 ? `${employees.length}` : ''}
                    </span>
                </div>

                {/* Карточки */}
                <div className="emp-grid-scroll">
                    {empLoading && (
                        <div className="emp-status">Загрузка…</div>
                    )}
                    {!empLoading && empError && (
                        <div className="emp-status emp-error">{empError}</div>
                    )}
                    {!empLoading && !empError && employees.length === 0 && (
                        <div className="emp-status emp-empty">
                            {search.trim() ? 'Ничего не найдено' : 'Нет сотрудников'}
                        </div>
                    )}
                    {!empLoading && !empError && employees.length > 0 && (() => {
                        // Группируем сотрудников по подразделению
                        const groups = new Map<string, DirectoryEmployeeDto[]>();
                        for (const emp of employees) {
                            const key = emp.departmentName ?? '';
                            if (!groups.has(key)) groups.set(key, []);
                            groups.get(key)!.push(emp);
                        }
                        const showDividers = groups.size > 1;
                        if (!showDividers) {
                            return (
                                <div className="emp-grid">
                                    {employees.map(renderEmployeeCard)}
                                </div>
                            );
                        }
                        return (
                            <div className="emp-groups">
                                {Array.from(groups.entries()).map(([deptName, emps]) => (
                                    <div key={deptName} className="emp-group">
                                        <div className="emp-group-divider">
                                            <span className="emp-group-name">{deptName || 'Без подразделения'}</span>
                                        </div>
                                        <div className="emp-grid">
                                            {emps.map(renderEmployeeCard)}
                                        </div>
                                    </div>
                                ))}
                            </div>
                        );
                    })()}
                </div>
            </section>
        </div>
    );
}
