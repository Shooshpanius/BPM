import { useState, useMemo } from 'react';
import type { OrgChartNodeDto, OrgChartEmployeeDto } from '../../api/orgChartApi';
import './OrgChartTable.css';

// ─── Вспомогательные типы ─────────────────────────────────────────────────

interface TableRow {
    userId?: string;
    departmentName: string;
    positionName: string;
    displayName: string;
    phone?: string;
    workEmail?: string;
    rate?: number;
    isVacancy: boolean;
}

type SortField = 'departmentName' | 'positionName' | 'displayName' | 'phone' | 'workEmail' | 'rate';
type SortDir = 'asc' | 'desc';

// ─── Функция сборки плоского списка из дерева ─────────────────────────────

function flattenTree(
    nodes: OrgChartNodeDto[],
    unassigned: OrgChartEmployeeDto[],
    extended: boolean
): TableRow[] {
    const rows: TableRow[] = [];

    const processNode = (node: OrgChartNodeDto) => {
        for (const emp of node.employees) {
            rows.push({
                userId: emp.userId,
                departmentName: node.name,
                positionName: emp.positionName ?? '—',
                displayName: emp.displayName,
                phone: emp.phone,
                workEmail: emp.workEmail,
                rate: emp.rate,
                isVacancy: false,
            });
        }

        if (extended) {
            for (const vac of node.vacancies) {
                rows.push({
                    departmentName: node.name,
                    positionName: vac.positionName,
                    displayName: `Вакансия × ${vac.vacancyCount}`,
                    isVacancy: true,
                });
            }
        }

        for (const child of node.children) processNode(child);
    };

    for (const node of nodes) processNode(node);

    for (const emp of unassigned) {
        rows.push({
            userId: emp.userId,
            departmentName: '—',
            positionName: emp.positionName ?? '—',
            displayName: emp.displayName,
            phone: emp.phone,
            workEmail: emp.workEmail,
            rate: emp.rate,
            isVacancy: false,
        });
    }

    return rows;
}

// ─── Компонент ────────────────────────────────────────────────────────────

interface OrgChartTableProps {
    departments: OrgChartNodeDto[];
    unassignedEmployees: OrgChartEmployeeDto[];
    search: string;
    extended: boolean;
}

export function OrgChartTable({ departments, unassignedEmployees, search, extended }: OrgChartTableProps) {
    const [sortField, setSortField] = useState<SortField>('departmentName');
    const [sortDir, setSortDir] = useState<SortDir>('asc');

    const allRows = useMemo(
        () => flattenTree(departments, unassignedEmployees, extended),
        [departments, unassignedEmployees, extended]
    );

    const filteredRows = useMemo(() => {
        if (!search) return allRows;
        const pattern = search.toUpperCase();
        return allRows.filter(r =>
            r.displayName.toUpperCase().includes(pattern) ||
            r.positionName.toUpperCase().includes(pattern) ||
            r.departmentName.toUpperCase().includes(pattern) ||
            (r.workEmail?.toUpperCase().includes(pattern) ?? false) ||
            (r.phone?.includes(pattern) ?? false)
        );
    }, [allRows, search]);

    const sortedRows = useMemo(() => {
        const dir = sortDir === 'asc' ? 1 : -1;
        return [...filteredRows].sort((a, b) => {
            const va = String(a[sortField] ?? '');
            const vb = String(b[sortField] ?? '');
            return va.localeCompare(vb, 'ru') * dir;
        });
    }, [filteredRows, sortField, sortDir]);

    const handleSort = (field: SortField) => {
        if (field === sortField) {
            setSortDir(d => d === 'asc' ? 'desc' : 'asc');
        } else {
            setSortField(field);
            setSortDir('asc');
        }
    };

    const sortIcon = (field: SortField) => {
        if (sortField !== field) return <span className="oct-sort-icon oct-sort-icon--inactive">↕</span>;
        return <span className="oct-sort-icon">{sortDir === 'asc' ? '↑' : '↓'}</span>;
    };

    if (sortedRows.length === 0) {
        return <div className="oct-empty">Нет данных для отображения</div>;
    }

    return (
        <div className="oct-root">
            <div className="oct-info">{sortedRows.length} записей</div>
            <div className="oct-scroll">
                <table className="oct-table">
                    <thead>
                        <tr>
                            <th className="oct-th oct-th--clickable" onClick={() => handleSort('departmentName')}>
                                Подразделение {sortIcon('departmentName')}
                            </th>
                            <th className="oct-th oct-th--clickable" onClick={() => handleSort('positionName')}>
                                Должность {sortIcon('positionName')}
                            </th>
                            <th className="oct-th oct-th--clickable" onClick={() => handleSort('displayName')}>
                                ФИО {sortIcon('displayName')}
                            </th>
                            <th className="oct-th oct-th--clickable" onClick={() => handleSort('phone')}>
                                Телефон {sortIcon('phone')}
                            </th>
                            <th className="oct-th oct-th--clickable" onClick={() => handleSort('workEmail')}>
                                Email {sortIcon('workEmail')}
                            </th>
                            {extended && (
                                <th className="oct-th oct-th--clickable" onClick={() => handleSort('rate')}>
                                    Ставка {sortIcon('rate')}
                                </th>
                            )}
                        </tr>
                    </thead>
                    <tbody>
                        {sortedRows.map((row, idx) => (
                            <tr
                                key={row.userId ?? `vacancy-${idx}`}
                                className={`oct-row${row.isVacancy ? ' oct-row--vacancy' : ''}`}
                            >
                                <td className="oct-td">{row.departmentName}</td>
                                <td className="oct-td">{row.positionName}</td>
                                <td className="oct-td oct-td--name">{row.displayName}</td>
                                <td className="oct-td">{row.phone ?? '—'}</td>
                                <td className="oct-td">{row.workEmail ?? '—'}</td>
                                {extended && (
                                    <td className="oct-td">
                                        {row.isVacancy ? '—' : (row.rate ?? '—')}
                                    </td>
                                )}
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
