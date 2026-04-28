import { useState } from 'react';
import type { OrgUnitTreeDto } from '../../../api/unitsApi';
import { DepartmentNode } from './DepartmentNode';
import './DepartmentTree.css';

interface DepartmentTreeProps {
    nodes: OrgUnitTreeDto[];
    searchQuery: string;
    canManage: boolean;
    onSelect: (id: string) => void;
    onAddChild: (parentId: string) => void;
    onEdit: (id: string) => void;
    onArchive: (id: string) => void;
    onMove: (unitId: string, newParentId: string | null) => void;
}

/** Рекурсивно собирает все id из дерева. */
function collectAllIds(nodes: OrgUnitTreeDto[]): Set<string> {
    const ids = new Set<string>();
    const visit = (arr: OrgUnitTreeDto[]) => {
        for (const n of arr) {
            ids.add(n.id);
            if (n.children.length) visit(n.children);
        }
    };
    visit(nodes);
    return ids;
}

/** Корневой компонент дерева подразделений с expand/collapse и drag-and-drop. */
export function DepartmentTree({
    nodes,
    searchQuery,
    canManage,
    onSelect,
    onAddChild,
    onEdit,
    onArchive,
    onMove,
}: DepartmentTreeProps) {
    // При наличии поиска автоматически разворачиваем все узлы
    const [expandedIds, setExpandedIds] = useState<Set<string>>(() => new Set());
    const [dragSourceId, setDragSourceId] = useState<string | null>(null);
    const [dragOverId, setDragOverId] = useState<string | null>(null);

    const effectiveExpanded: Set<string> = searchQuery
        ? collectAllIds(nodes)
        : expandedIds;

    const toggleNode = (id: string) => {
        setExpandedIds(prev => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id); else next.add(id);
            return next;
        });
    };

    const handleDragStart = (id: string) => setDragSourceId(id);
    const handleDragOver = (id: string) => setDragOverId(id);
    const handleDragLeave = () => setDragOverId(null);

    const handleDrop = (targetId: string) => {
        setDragOverId(null);
        if (dragSourceId && dragSourceId !== targetId) {
            onMove(dragSourceId, targetId);
        }
        setDragSourceId(null);
    };

    // Обработчик дропа «в пустую зону» — перемещение на корневой уровень
    const handleRootDrop = (e: React.DragEvent) => {
        e.preventDefault();
        setDragOverId(null);
        if (dragSourceId) {
            onMove(dragSourceId, null);
            setDragSourceId(null);
        }
    };

    if (nodes.length === 0) {
        return (
            <div className="dept-tree-empty">
                {searchQuery ? 'Ничего не найдено' : 'Нет подразделений'}
            </div>
        );
    }

    const renderNodes = (arr: OrgUnitTreeDto[], depth: number): React.ReactNode =>
        arr.map(node => (
            <div key={node.id} className="dept-tree__node-wrapper">
                <DepartmentNode
                    node={node}
                    depth={depth}
                    isExpanded={effectiveExpanded.has(node.id)}
                    isDragOver={dragOverId === node.id}
                    searchQuery={searchQuery}
                    onToggle={toggleNode}
                    onSelect={onSelect}
                    onAddChild={onAddChild}
                    onEdit={onEdit}
                    onArchive={onArchive}
                    onDragStart={handleDragStart}
                    onDragOver={handleDragOver}
                    onDragLeave={handleDragLeave}
                    onDrop={handleDrop}
                    canManage={canManage}
                />
                {effectiveExpanded.has(node.id) && node.children.length > 0 && (
                    <div className="dept-tree__children">
                        {renderNodes(node.children, depth + 1)}
                    </div>
                )}
            </div>
        ));

    return (
        <div
            className="dept-tree"
            onDragOver={canManage ? (e) => e.preventDefault() : undefined}
            onDrop={canManage ? handleRootDrop : undefined}
        >
            {renderNodes(nodes, 0)}
        </div>
    );
}
