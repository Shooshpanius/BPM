import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getOrgChart } from '../../api/orgChartApi';
import { getDirectoryOrganizations } from '../../api/orgDirectoryApi';
import type { OrgChartEmployeeDto } from '../../api/orgChartApi';
import './MyColleaguesWidget.css';

const MAX_VISIBLE = 6;

function getInitials(name: string): string {
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
    return name[0]?.toUpperCase() ?? '?';
}

interface MiniAvatarProps {
    employee: OrgChartEmployeeDto;
    isManager?: boolean;
}

function MiniAvatar({ employee, isManager }: MiniAvatarProps) {
    return (
        <div
            className={`mcw-avatar${isManager ? ' mcw-avatar--manager' : ''}`}
            title={`${employee.displayName}${employee.positionName ? ` — ${employee.positionName}` : ''}`}
        >
            {employee.avatarUrl
                ? <img src={employee.avatarUrl} alt={employee.displayName} className="mcw-avatar-img" />
                : <span className="mcw-avatar-initials">{getInitials(employee.displayName)}</span>}
        </div>
    );
}

/** Виджет «Мои коллеги» для главной страницы. */
export function MyColleaguesWidget() {
    const { accessToken: token, userId: currentUserId } = useAuth();
    const [manager, setManager] = useState<OrgChartEmployeeDto | null>(null);
    const [colleagues, setColleagues] = useState<OrgChartEmployeeDto[]>([]);
    const [extra, setExtra] = useState(0);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        if (!token || !currentUserId) { setLoading(false); return; }

        const load = async () => {
            try {
                // Находим первую доступную организацию
                const orgs = await getDirectoryOrganizations(token);
                if (orgs.length === 0) { setLoading(false); return; }

                const orgId = orgs[0].id;

                // Загружаем всё дерево, затем ищем подразделение текущего пользователя
                const chart = await getOrgChart(token, { organizationId: orgId });

                // Ищем узел, в котором есть текущий пользователь
                let deptEmployees: OrgChartEmployeeDto[] = [];

                const findDept = (nodes: typeof chart.departments): boolean => {
                    for (const node of nodes) {
                        if (node.employees.some(e => e.userId === currentUserId)) {
                            deptEmployees = node.employees;
                            return true;
                        }
                        if (findDept(node.children)) return true;
                    }
                    return false;
                };

                findDept(chart.departments);

                if (deptEmployees.length === 0) { setLoading(false); return; }

                const mgr = deptEmployees.find(
                    e => e.positionCategory === 'Managerial' && e.userId !== currentUserId
                ) ?? null;

                const peers = deptEmployees.filter(
                    e => e.userId !== currentUserId && e.userId !== mgr?.userId
                );

                const visible = peers.slice(0, MAX_VISIBLE);
                setManager(mgr);
                setColleagues(visible);
                setExtra(Math.max(0, peers.length - MAX_VISIBLE));
            } catch {
                // Виджет не должен ломать страницу
            } finally {
                setLoading(false);
            }
        };

        load();
    }, [token, currentUserId]);

    if (loading) {
        return (
            <div className="mcw-root">
                <div className="mcw-title">Мои коллеги</div>
                <div className="mcw-loading">Загрузка…</div>
            </div>
        );
    }

    if (!manager && colleagues.length === 0) return null;

    return (
        <div className="mcw-root">
            <div className="mcw-title">Мои коллеги</div>
            <div className="mcw-list">
                {manager && (
                    <div className="mcw-group">
                        <span className="mcw-group-label">Руководитель</span>
                        <MiniAvatar employee={manager} isManager />
                    </div>
                )}
                {colleagues.length > 0 && (
                    <div className="mcw-group">
                        <span className="mcw-group-label">Коллеги</span>
                        <div className="mcw-avatars">
                            {colleagues.map(e => (
                                <MiniAvatar key={e.userId} employee={e} />
                            ))}
                            {extra > 0 && (
                                <div className="mcw-avatar mcw-avatar--extra">
                                    <span className="mcw-avatar-initials">+{extra}</span>
                                </div>
                            )}
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
