import type { OrgUnitTreeDto } from '../../../api/unitsApi';
import {
    DEPARTMENT_STATUS_ACTIVE,
    DEPARTMENT_STATUS_ARCHIVED,
} from '../../../api/unitsApi';
import './DepartmentNode.css';

interface DepartmentNodeProps {
    node: OrgUnitTreeDto;
    depth: number;
    isExpanded: boolean;
    isDragOver: boolean;
    searchQuery: string;
    onToggle: (id: string) => void;
    onSelect: (id: string) => void;
    onAddChild: (parentId: string) => void;
    onEdit: (id: string) => void;
    onArchive: (id: string) => void;
    onDragStart: (id: string) => void;
    onDragOver: (id: string) => void;
    onDragLeave: () => void;
    onDrop: (targetId: string) => void;
    canManage: boolean;
}

/** Подсвечивает часть строки, совпадающую с поисковым запросом. */
function HighlightText({ text, query }: { text: string; query: string }) {
    if (!query) return <>{text}</>;
    const idx = text.toLowerCase().indexOf(query.toLowerCase());
    if (idx < 0) return <>{text}</>;
    return (
        <>
            {text.slice(0, idx)}
            <mark className="dept-highlight">{text.slice(idx, idx + query.length)}</mark>
            {text.slice(idx + query.length)}
        </>
    );
}

/** Узел дерева подразделений. */
export function DepartmentNode({
    node,
    depth,
    isExpanded,
    isDragOver,
    searchQuery,
    onToggle,
    onSelect,
    onAddChild,
    onEdit,
    onArchive,
    onDragStart,
    onDragOver,
    onDragLeave,
    onDrop,
    canManage,
}: DepartmentNodeProps) {
    const isArchived = node.status === DEPARTMENT_STATUS_ARCHIVED;
    const hasChildren = node.children.length > 0;

    const handleDragOver = (e: React.DragEvent) => {
        e.preventDefault();
        e.stopPropagation();
        onDragOver(node.id);
    };

    const handleDrop = (e: React.DragEvent) => {
        e.preventDefault();
        e.stopPropagation();
        onDrop(node.id);
    };

    return (
        <div
            className={[
                'dept-node',
                isArchived ? 'dept-node--archived' : '',
                isDragOver ? 'dept-node--drag-over' : '',
            ].filter(Boolean).join(' ')}
            style={{ paddingLeft: `${8 + depth * 18}px` }}
            draggable={canManage}
            onDragStart={canManage ? (e) => { e.stopPropagation(); onDragStart(node.id); } : undefined}
            onDragOver={canManage ? handleDragOver : undefined}
            onDragLeave={canManage ? (e) => { e.stopPropagation(); onDragLeave(); } : undefined}
            onDrop={canManage ? handleDrop : undefined}
        >
            {/* Кнопка expand/collapse */}
            <button
                className={`dept-node__toggle${hasChildren ? '' : ' dept-node__toggle--leaf'}`}
                onClick={() => { if (hasChildren) onToggle(node.id); else onSelect(node.id); }}
                aria-label={isExpanded ? 'Свернуть' : 'Развернуть'}
                aria-expanded={hasChildren ? isExpanded : undefined}
                tabIndex={-1}
            >
                {hasChildren ? (
                    <span className={`dept-node__arrow${isExpanded ? ' dept-node__arrow--open' : ''}`}>›</span>
                ) : (
                    <span className="dept-node__arrow-placeholder" />
                )}
            </button>

            {/* Иконка */}
            <span className="dept-node__icon" aria-hidden="true">
                {isArchived ? (
                    <svg viewBox="0 0 20 20" fill="currentColor" width="14" height="14">
                        <path d="M4 3h12a1 1 0 0 1 1 1v2H3V4a1 1 0 0 1 1-1zm-1 5h14v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V8zm5 2v2H7l3 3 3-3h-2v-2h-2z"/>
                    </svg>
                ) : (
                    <svg viewBox="0 0 20 20" fill="currentColor" width="14" height="14">
                        <path d="M3 4a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V4zm0 6a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-6zm10 0a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1h-2a1 1 0 0 1-1-1v-6z"/>
                    </svg>
                )}
            </span>

            {/* Название и код */}
            <span className="dept-node__content" onClick={() => onSelect(node.id)}>
                <span className="dept-node__name">
                    <HighlightText text={node.name} query={searchQuery} />
                    {node.shortName && (
                        <span className="dept-node__short-name"> ({node.shortName})</span>
                    )}
                </span>
                {node.code && (
                    <span className="dept-node__code">
                        <HighlightText text={node.code} query={searchQuery} />
                    </span>
                )}
            </span>

            {/* Счётчики сотрудников */}
            {node.totalEmployeesCount > 0 && (
                <span className="dept-node__count" title="Сотрудников всего / прямых">
                    {node.totalEmployeesCount}
                    {node.totalEmployeesCount !== node.directEmployeesCount && (
                        <span className="dept-node__count-direct"> ({node.directEmployeesCount})</span>
                    )}
                </span>
            )}

            {/* Кнопки действий (только для Admin/HR) */}
            {canManage && (
                <span className="dept-node__actions">
                    <button
                        className="dept-node__action-btn"
                        onClick={(e) => { e.stopPropagation(); onAddChild(node.id); }}
                        title="Добавить дочернее подразделение"
                        aria-label="Добавить дочернее подразделение"
                    >
                        <svg viewBox="0 0 16 16" fill="currentColor" width="12" height="12">
                            <path d="M8 2a.75.75 0 0 1 .75.75v4.5h4.5a.75.75 0 0 1 0 1.5h-4.5v4.5a.75.75 0 0 1-1.5 0v-4.5h-4.5a.75.75 0 0 1 0-1.5h4.5v-4.5A.75.75 0 0 1 8 2z"/>
                        </svg>
                    </button>
                    <button
                        className="dept-node__action-btn"
                        onClick={(e) => { e.stopPropagation(); onEdit(node.id); }}
                        title="Редактировать"
                        aria-label="Редактировать подразделение"
                    >
                        <svg viewBox="0 0 16 16" fill="currentColor" width="12" height="12">
                            <path d="M11.013 1.427a1.75 1.75 0 0 1 2.474 0l1.086 1.086a1.75 1.75 0 0 1 0 2.474l-8.61 8.61c-.21.21-.47.364-.756.445l-3.251.93a.75.75 0 0 1-.927-.928l.929-3.25c.081-.286.235-.547.445-.758l8.61-8.61zm1.414 1.06a.25.25 0 0 0-.354 0L10.811 3.75l1.439 1.44 1.263-1.263a.25.25 0 0 0 0-.354l-1.086-1.086zM11.189 6.25 9.75 4.81 3.23 11.33c-.03.03-.052.068-.063.11l-.652 2.278 2.278-.651c.042-.012.08-.034.11-.063L11.19 6.25z"/>
                        </svg>
                    </button>
                    {node.status === DEPARTMENT_STATUS_ACTIVE && (
                        <button
                            className="dept-node__action-btn dept-node__action-btn--danger"
                            onClick={(e) => { e.stopPropagation(); onArchive(node.id); }}
                            title="Архивировать"
                            aria-label="Архивировать подразделение"
                        >
                            <svg viewBox="0 0 16 16" fill="currentColor" width="12" height="12">
                                <path d="M1.75 2.5h12.5a.75.75 0 0 1 .75.75v2.5a.75.75 0 0 1-.75.75H1.75A.75.75 0 0 1 1 5.75v-2.5a.75.75 0 0 1 .75-.75zM2 7.25v5a.75.75 0 0 0 .75.75h10.5a.75.75 0 0 0 .75-.75v-5H2zm5.25 1h1.5a.75.75 0 0 1 0 1.5h-1.5a.75.75 0 0 1 0-1.5z"/>
                            </svg>
                        </button>
                    )}
                </span>
            )}
        </div>
    );
}
