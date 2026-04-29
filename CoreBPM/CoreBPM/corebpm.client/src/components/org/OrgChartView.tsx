import { useState, useEffect, useRef } from 'react';
import type { OrgChartNodeDto, OrgChartEmployeeDto } from '../../api/orgChartApi';
import './OrgChartView.css';

// ─── Вспомогательные функции ───────────────────────────────────────────────

function getInitials(displayName: string): string {
    const parts = displayName.trim().split(/\s+/);
    if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
    return displayName[0]?.toUpperCase() ?? '?';
}

function collectNodeIds(node: OrgChartNodeDto): Set<string> {
    const ids = new Set<string>([node.id]);
    for (const child of node.children)
        for (const id of collectNodeIds(child))
            ids.add(id);
    return ids;
}

/** Находит путь (ids) от корня до узла, содержащего нужного сотрудника. */
function findPathToEmployee(nodes: OrgChartNodeDto[], userId: string): string[] | null {
    for (const node of nodes) {
        if (node.employees.some(e => e.userId === userId)) return [node.id];
        const childPath = findPathToEmployee(node.children, userId);
        if (childPath) return [node.id, ...childPath];
    }
    return null;
}

// ─── Компоненты ────────────────────────────────────────────────────────────

interface EmployeeCardProps {
    employee: OrgChartEmployeeDto;
    highlighted: boolean;
    extended: boolean;
    onClick: (userId: string) => void;
    cardRef?: React.RefCallback<HTMLDivElement>;
}

function EmployeeCard({ employee, highlighted, extended, onClick, cardRef }: EmployeeCardProps) {
    return (
        <div
            ref={cardRef}
            className={`ocv-card${highlighted ? ' ocv-card--highlighted' : ''}${employee.positionCategory === 'Managerial' ? ' ocv-card--managerial' : ''}`}
            role="button"
            tabIndex={0}
            onClick={() => onClick(employee.userId)}
            onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') onClick(employee.userId); }}
            title={employee.displayName}
        >
            <div className="ocv-card-avatar">
                {employee.avatarUrl
                    ? <img src={employee.avatarUrl} alt={employee.displayName} className="ocv-card-avatar-img" />
                    : <span className="ocv-card-avatar-initials">{getInitials(employee.displayName)}</span>}
            </div>
            <div className="ocv-card-info">
                <span className="ocv-card-name">{employee.displayName}</span>
                {employee.positionName && (
                    <span className="ocv-card-position">{employee.positionName}</span>
                )}
                {extended && employee.rate !== undefined && (
                    <span className="ocv-card-rate">{employee.rate} ставки</span>
                )}
            </div>
        </div>
    );
}

interface DeptNodeProps {
    node: OrgChartNodeDto;
    expanded: Set<string>;
    toggleExpanded: (id: string) => void;
    highlightUserId?: string;
    extended: boolean;
    onEmployeeClick: (userId: string) => void;
    cardRefs: Map<string, HTMLDivElement>;
    depth: number;
}

function DeptNode({
    node, expanded, toggleExpanded, highlightUserId, extended, onEmployeeClick, cardRefs, depth
}: DeptNodeProps) {
    const isExpanded = expanded.has(node.id);
    const hasChildren = node.children.length > 0 || node.employees.length > 0 || node.vacancies.length > 0;

    return (
        <div className={`ocv-dept${depth === 0 ? ' ocv-dept--root' : ''}`}>
            <div
                className={`ocv-dept-header${isExpanded ? ' expanded' : ''}`}
                role="button"
                tabIndex={0}
                onClick={() => toggleExpanded(node.id)}
                onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') toggleExpanded(node.id); }}
                aria-expanded={isExpanded}
            >
                {hasChildren && (
                    <span className="ocv-dept-toggle" aria-hidden="true">
                        {isExpanded ? '▾' : '▸'}
                    </span>
                )}
                <span className="ocv-dept-name">
                    {node.shortName ? `${node.shortName} — ${node.name}` : node.name}
                </span>
                <span className="ocv-dept-count">{node.totalEmployeesCount}</span>
            </div>

            {isExpanded && (
                <div className="ocv-dept-body">
                    {/* Карточки сотрудников */}
                    {node.employees.length > 0 && (
                        <div className="ocv-cards-row">
                            {node.employees.map(emp => (
                                <EmployeeCard
                                    key={emp.userId}
                                    employee={emp}
                                    highlighted={highlightUserId === emp.userId}
                                    extended={extended}
                                    onClick={onEmployeeClick}
                                    cardRef={el => {
                                        if (el) cardRefs.set(emp.userId, el);
                                        else cardRefs.delete(emp.userId);
                                    }}
                                />
                            ))}
                            {/* Вакантные слоты */}
                            {extended && node.vacancies.map(v => (
                                <div key={v.positionId} className="ocv-card ocv-card--vacancy">
                                    <div className="ocv-card-avatar">
                                        <span className="ocv-card-avatar-initials ocv-card-avatar--empty">+</span>
                                    </div>
                                    <div className="ocv-card-info">
                                        <span className="ocv-card-position">{v.positionName}</span>
                                        <span className="ocv-card-rate ocv-card-rate--vacancy">
                                            {v.vacancyCount === 1 ? '1 вакансия' : `${v.vacancyCount} вакансии`}
                                        </span>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}

                    {/* Дочерние подразделения */}
                    {node.children.map(child => (
                        <DeptNode
                            key={child.id}
                            node={child}
                            expanded={expanded}
                            toggleExpanded={toggleExpanded}
                            highlightUserId={highlightUserId}
                            extended={extended}
                            onEmployeeClick={onEmployeeClick}
                            cardRefs={cardRefs}
                            depth={depth + 1}
                        />
                    ))}
                </div>
            )}
        </div>
    );
}

// ─── Основной компонент ────────────────────────────────────────────────────

interface OrgChartViewProps {
    departments: OrgChartNodeDto[];
    unassignedEmployees: OrgChartEmployeeDto[];
    /** Идентификатор сотрудника для zoom-to-node (раскрыть ветку и прокрутить к карточке) */
    highlightEmployeeId?: string;
    extended: boolean;
}

export function OrgChartView({
    departments,
    unassignedEmployees,
    highlightEmployeeId,
    extended,
}: OrgChartViewProps) {
    // Раскрытые узлы — по умолчанию раскрываем первый уровень
    const [expanded, setExpanded] = useState<Set<string>>(() => {
        const initial = new Set<string>();
        for (const root of departments) initial.add(root.id);
        return initial;
    });

    const cardRefs = useRef<Map<string, HTMLDivElement>>(new Map());

    // При смене highlightEmployeeId — раскрываем путь и прокручиваем к карточке
    useEffect(() => {
        if (!highlightEmployeeId) return;

        const path = findPathToEmployee(departments, highlightEmployeeId);
        if (!path) return;

        setExpanded(prev => {
            const next = new Set(prev);
            for (const id of path) next.add(id);
            return next;
        });

        // Ждём рендер, затем прокручиваем
        requestAnimationFrame(() => {
            const el = cardRefs.current.get(highlightEmployeeId);
            el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });
    }, [highlightEmployeeId, departments]);

    // Обновляем раскрытое состояние первого уровня при смене departments
    useEffect(() => {
        setExpanded(prev => {
            const next = new Set(prev);
            for (const root of departments) next.add(root.id);
            return next;
        });
    }, [departments]);

    const toggleExpanded = (id: string) => {
        setExpanded(prev => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id);
            else next.add(id);
            return next;
        });
    };

    const handleExpandAll = () => {
        const all = new Set<string>();
        const collect = (nodes: OrgChartNodeDto[]) => {
            for (const n of nodes) {
                all.add(n.id);
                collect(n.children);
            }
        };
        collect(departments);
        setExpanded(all);
    };

    const handleCollapseAll = () => setExpanded(new Set());

    if (departments.length === 0 && unassignedEmployees.length === 0) {
        return <div className="ocv-empty">Нет данных для отображения</div>;
    }

    return (
        <div className="ocv-root">
            <div className="ocv-toolbar">
                <button className="ocv-btn" onClick={handleExpandAll}>Развернуть все</button>
                <button className="ocv-btn" onClick={handleCollapseAll}>Свернуть все</button>
            </div>

            <div className="ocv-tree">
                {departments.map(dept => (
                    <DeptNode
                        key={dept.id}
                        node={dept}
                        expanded={expanded}
                        toggleExpanded={toggleExpanded}
                        highlightUserId={highlightEmployeeId}
                        extended={extended}
                        onEmployeeClick={() => { /* будущая навигация к профилю */ }}
                        cardRefs={cardRefs.current}
                        depth={0}
                    />
                ))}

                {unassignedEmployees.length > 0 && (
                    <div className="ocv-dept ocv-dept--root ocv-dept--unassigned">
                        <div
                            className={`ocv-dept-header${expanded.has('__unassigned__') ? ' expanded' : ''}`}
                            role="button"
                            tabIndex={0}
                            onClick={() => toggleExpanded('__unassigned__')}
                            onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') toggleExpanded('__unassigned__'); }}
                            aria-expanded={expanded.has('__unassigned__')}
                        >
                            <span className="ocv-dept-toggle" aria-hidden="true">
                                {expanded.has('__unassigned__') ? '▾' : '▸'}
                            </span>
                            <span className="ocv-dept-name">Без подразделения</span>
                            <span className="ocv-dept-count">{unassignedEmployees.length}</span>
                        </div>
                        {expanded.has('__unassigned__') && (
                            <div className="ocv-dept-body">
                                <div className="ocv-cards-row">
                                    {unassignedEmployees.map(emp => (
                                        <EmployeeCard
                                            key={emp.userId}
                                            employee={emp}
                                            highlighted={highlightEmployeeId === emp.userId}
                                            extended={extended}
                                            onClick={() => { /* будущая навигация к профилю */ }}
                                            cardRef={el => {
                                                if (el) cardRefs.current.set(emp.userId, el);
                                                else cardRefs.current.delete(emp.userId);
                                            }}
                                        />
                                    ))}
                                </div>
                            </div>
                        )}
                    </div>
                )}
            </div>
        </div>
    );
}
