import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useMobile } from '../../hooks/useMobile';
import * as api from '../../api/orgDirectoryApi';
import type {
    DirectoryOrganizationDto,
    DirectoryDepartmentTreeDto,
    DirectoryEmployeeDto,
} from '../../api/orgDirectoryApi';
import { EmployeeCardModal } from '../../components/EmployeeCardModal';
import './ContactsPage.css';

// ─── Вспомогательные функции ───

function getInitials(emp: DirectoryEmployeeDto): string {
    const first = emp.firstName?.[0] ?? '';
    const last = emp.lastName?.[0] ?? '';
    if (first || last) return (first + last).toUpperCase();
    return emp.displayName[0]?.toUpperCase() ?? '?';
}

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

/** Страница адресной книги: дерево оргструктуры + карточки сотрудников + поиск. */
export function ContactsPage() {
    const { accessToken: token } = useAuth();
    const isMobile = useMobile();

    // Мобильное состояние — отображение drawer с деревом
    const [showMobileTree, setShowMobileTree] = useState(false);

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
    const [total, setTotal] = useState(0);
    const [empLoading, setEmpLoading] = useState(false);
    const [empError, setEmpError] = useState<string | null>(null);

    // Поиск
    const [search, setSearch] = useState('');
    const [searchScope, setSearchScope] = useState<'current' | 'global'>('current');
    const searchTimer = useRef<number | null>(null);

    // Фильтры, сортировка, вид
    const [positionFilter, setPositionFilter] = useState('');
    const [sortBy, setSortBy] = useState('displayName');
    const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');
    const [viewMode, setViewMode] = useState<'cards' | 'table'>('cards');
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(25);
    const [exporting, setExporting] = useState(false);

    // Модальное окно карточки сотрудника
    const [selectedEmployee, setSelectedEmployee] = useState<DirectoryEmployeeDto | null>(null);

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
                position: positionFilter.trim() || undefined,
                sortBy,
                sortDir,
                page,
                pageSize,
            })
                .then(r => { setEmployees(r.items); setTotal(r.total); })
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
    }, [token, selectedOrgId, selectedDeptId, search, searchScope, positionFilter, sortBy, sortDir, page, pageSize]); // eslint-disable-line react-hooks/exhaustive-deps

    // ─── Обработчики дерева ───

    const toggleOrg = async (orgId: string) => {
        const isExpanded = expandedOrgs.has(orgId);
        // Аккордеон: только одна организация развёрнута одновременно
        setExpandedOrgs(isExpanded ? new Set() : new Set([orgId]));
        setSelectedOrgId(orgId);
        setSelectedDeptId(null);
        setSearch('');
        setPage(1);
        if (!isExpanded) await loadDepartmentTree(orgId);
    };

    const selectOrg = (orgId: string) => {
        setSelectedOrgId(orgId);
        setSelectedDeptId(null);
        setSearch('');
        setPage(1);
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
        setPage(1);
    };

    const handleExport = async () => {
        if (!token) return;
        setExporting(true);
        try {
            const isGlobal = searchScope === 'global' && search.trim().length > 0;
            const blob = await api.exportDirectoryEmployees(token, {
                organizationId: isGlobal ? undefined : (selectedDeptId ? undefined : selectedOrgId ?? undefined),
                departmentId: isGlobal ? undefined : (selectedDeptId ?? undefined),
                search: search.trim() || undefined,
                position: positionFilter.trim() || undefined,
                sortBy,
            });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'employees.csv';
            a.click();
            URL.revokeObjectURL(url);
        } finally {
            setExporting(false);
        }
    };

    const totalPages = Math.ceil(total / pageSize);

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
            <div className="emp-card" key={emp.id} onClick={() => setSelectedEmployee(emp)} role="button" tabIndex={0} onKeyDown={e => e.key === 'Enter' && setSelectedEmployee(emp)}>
                <div className="emp-avatar" aria-hidden="true">
                    {emp.avatarUrl
                        ? <img src={emp.avatarUrl} alt={emp.displayName} className="emp-avatar-img" />
                        : <span className="emp-avatar-initials">{initials}</span>
                    }
                </div>
                <div className="emp-info">
                    <span className="emp-name">{emp.displayName}</span>
                    {emp.position && <span className="emp-position">{emp.position}</span>}
                    <a className="emp-email" href={`mailto:${emp.workEmail}`} onClick={e => e.stopPropagation()}>{emp.workEmail}</a>
                    {emp.phone && <span className="emp-phone">{emp.phone}</span>}
                    {emp.departmentName && <span className="emp-dept">{emp.departmentName}</span>}
                </div>
            </div>
        );
    };

    // ─── Рендер таблицы ───

    const renderTable = () => (
        <div className="emp-table-wrap">
            <table className="emp-table">
                <thead>
                    <tr>
                        <th>Сотрудник</th>
                        <th>Должность</th>
                        <th>Подразделение</th>
                        <th>Email</th>
                        <th>Телефон</th>
                    </tr>
                </thead>
                <tbody>
                    {employees.map(emp => (
                        <tr key={emp.id} className="emp-table-row" onClick={() => setSelectedEmployee(emp)}>
                            <td>
                                <div className="emp-table-name-cell">
                                    <div className="emp-table-avatar">
                                        {emp.avatarUrl
                                            ? <img src={emp.avatarUrl} alt={emp.displayName} />
                                            : <span>{getInitials(emp)}</span>
                                        }
                                    </div>
                                    <span>{emp.displayName}</span>
                                </div>
                            </td>
                            <td>{emp.position ?? '—'}</td>
                            <td>{emp.departmentName ?? '—'}</td>
                            <td><a href={`mailto:${emp.workEmail}`} onClick={e => e.stopPropagation()}>{emp.workEmail}</a></td>
                            <td>{emp.phone ?? '—'}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );

    return (
        <div className="contacts-root">
            {/* Панель дерева (десктоп) */}
            <aside className="contacts-tree-panel" aria-label="Оргструктура">
                <div className="tree-header">Оргструктура</div>
                <div className="tree-scroll">
                    {renderTree()}
                </div>
            </aside>

            {/* Мобильный drawer с деревом оргструктуры */}
            {isMobile && showMobileTree && (
                <div
                    className="contacts-mobile-overlay"
                    onClick={() => setShowMobileTree(false)}
                    role="dialog"
                    aria-modal="true"
                    aria-label="Оргструктура"
                >
                    <div
                        className="contacts-mobile-drawer"
                        onClick={e => e.stopPropagation()}
                    >
                        <div className="contacts-mobile-drawer-header">
                            <span>Оргструктура</span>
                            <button
                                className="contacts-mobile-drawer-close"
                                onClick={() => setShowMobileTree(false)}
                                aria-label="Закрыть"
                            >
                                ×
                            </button>
                        </div>
                        <div className="tree-scroll">
                            {renderTree()}
                        </div>
                    </div>
                </div>
            )}

            {/* Панель сотрудников */}
            <section className="contacts-main-panel">
                {/* Строка поиска */}
                <div className="contacts-search-bar">
                    {/* Кнопка открытия drawer с деревом (только на мобильных) */}
                    {isMobile && (
                        <button
                            className="contacts-tree-btn"
                            onClick={() => setShowMobileTree(true)}
                            aria-label="Открыть оргструктуру"
                            title="Оргструктура"
                        >
                            <svg viewBox="0 0 20 20" fill="currentColor" width="18" height="18" aria-hidden="true">
                                <path fillRule="evenodd" d="M3 3a1 1 0 000 2h14a1 1 0 100-2H3zm0 6a1 1 0 000 2h10a1 1 0 100-2H3zm0 6a1 1 0 100 2h6a1 1 0 100-2H3z" clipRule="evenodd"/>
                            </svg>
                        </button>
                    )}
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
                        {!empLoading && total > 0 ? total : ''}
                    </span>
                </div>

                {/* Панель инструментов */}
                <div className="contacts-toolbar">
                    <input
                        className="toolbar-position-filter"
                        placeholder="Фильтр по должности…"
                        value={positionFilter}
                        onChange={e => { setPositionFilter(e.target.value); setPage(1); }}
                    />
                    <select className="toolbar-sort" value={`${sortBy}:${sortDir}`} onChange={e => { const [sortByValue, sortDirValue] = e.target.value.split(':'); setSortBy(sortByValue); setSortDir(sortDirValue as 'asc' | 'desc'); setPage(1); }}>
                        <option value="displayName:asc">Имя А→Я</option>
                        <option value="displayName:desc">Имя Я→А</option>
                        <option value="position:asc">Должность А→Я</option>
                        <option value="position:desc">Должность Я→А</option>
                    </select>
                    <select className="toolbar-page-size" value={pageSize} onChange={e => { setPageSize(Number(e.target.value)); setPage(1); }}>
                        {PAGE_SIZE_OPTIONS.map(s => <option key={s} value={s}>{s} / стр.</option>)}
                    </select>
                    <div className="toolbar-view-toggle">
                        <button className={`view-btn${viewMode === 'cards' ? ' active' : ''}`} onClick={() => setViewMode('cards')} title="Карточки">⊞</button>
                        <button className={`view-btn${viewMode === 'table' ? ' active' : ''}`} onClick={() => setViewMode('table')} title="Таблица">☰</button>
                    </div>
                    <button className="toolbar-export-btn" onClick={handleExport} disabled={exporting} title="Экспорт CSV">
                        {exporting ? '…' : '⬇ CSV'}
                    </button>
                </div>

                {/* Список / таблица */}
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
                    {!empLoading && !empError && employees.length > 0 && (
                        viewMode === 'table' ? renderTable() : (() => {
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
                        })()
                    )}
                </div>

                {/* Пагинация */}
                {totalPages > 1 && (
                    <div className="contacts-pagination">
                        <button className="page-btn" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>‹</button>
                        <span className="page-info">{page} / {totalPages}</span>
                        <button className="page-btn" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>›</button>
                    </div>
                )}
            </section>

            {/* Модальное окно карточки сотрудника */}
            {selectedEmployee && (
                <EmployeeCardModal employee={selectedEmployee} onClose={() => setSelectedEmployee(null)} />
            )}
        </div>
    );
}
