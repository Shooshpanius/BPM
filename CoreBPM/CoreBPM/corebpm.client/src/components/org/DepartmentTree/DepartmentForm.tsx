import { useState, useEffect } from 'react';
import type { OrgUnitDto, OrgUnitTreeDto } from '../../../api/unitsApi';
import {
    DEPARTMENT_STATUS_ACTIVE,
    DEPARTMENT_STATUS_ARCHIVED,
    type CreateUnitRequest,
    type UpdateUnitRequest,
    type DepartmentStatus,
} from '../../../api/unitsApi';
import { useModalShake } from '../../../hooks/useModalShake';
import './DepartmentForm.css';

interface DepartmentFormProps {
    /** Режим редактирования: передаётся существующий unit. В режиме создания — null. */
    editUnit?: OrgUnitDto | null;
    /** При создании дочернего узла — id родителя. */
    parentId?: string;
    /** id организации (нужен только в режиме создания). */
    organizationId: string;
    /** Список узлов для выбора родителя (плоский). */
    allUnits: OrgUnitTreeDto[];
    onSave: (data: CreateUnitRequest | UpdateUnitRequest) => Promise<void>;
    onClose: () => void;
}

/** Рекурсивно преобразует дерево в плоский список с отступами для отображения. */
function flattenTree(nodes: OrgUnitTreeDto[], depth = 0): { id: string; label: string; path: string }[] {
    const result: { id: string; label: string; path: string }[] = [];
    for (const n of nodes) {
        result.push({ id: n.id, label: '─'.repeat(depth) + ' ' + n.name, path: n.path });
        result.push(...flattenTree(n.children, depth + 1));
    }
    return result;
}

/** Модальная форма создания / редактирования подразделения. */
export function DepartmentForm({
    editUnit,
    parentId,
    organizationId,
    allUnits,
    onSave,
    onClose,
}: DepartmentFormProps) {
    const isEdit = !!editUnit;

    const [name, setName] = useState(editUnit?.name ?? '');
    const [shortName, setShortName] = useState(editUnit?.shortName ?? '');
    const [code, setCode] = useState(editUnit?.code ?? '');
    const [description, setDescription] = useState(editUnit?.description ?? '');
    const [status, setStatus] = useState<DepartmentStatus>(
        editUnit?.status ?? DEPARTMENT_STATUS_ACTIVE
    );
    const [selectedParentId, setSelectedParentId] = useState<string>(
        editUnit?.parentId ?? parentId ?? ''
    );
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const { shaking, shake } = useModalShake();

    // Плоский список узлов для выбора родителя (исключаем себя и своих потомков при редактировании)
    const editPathPrefix = editUnit ? `${editUnit.path}/` : null;
    const flatOptions = flattenTree(allUnits).filter(
        o => !editUnit || (o.id !== editUnit.id && !o.path.startsWith(editPathPrefix!))
    );

    useEffect(() => {
        const onKeyDown = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKeyDown);
        return () => document.removeEventListener('keydown', onKeyDown);
    }, [onClose]);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!name.trim()) { setError('Название обязательно'); return; }
        setSaving(true);
        setError(null);
        try {
            if (isEdit) {
                const req: UpdateUnitRequest = {
                    name: name.trim(),
                    shortName: shortName.trim() || undefined,
                    code: code.trim() || undefined,
                    description: description.trim() || undefined,
                    status,
                };
                await onSave(req);
            } else {
                const req: CreateUnitRequest = {
                    organizationId,
                    parentId: selectedParentId || undefined,
                    name: name.trim(),
                    shortName: shortName.trim() || undefined,
                    code: code.trim() || undefined,
                    description: description.trim() || undefined,
                };
                await onSave(req);
            }
        } catch (err: unknown) {
            setError(err instanceof Error ? err.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="dept-form-overlay" onClick={(e) => { if (e.target === e.currentTarget) shake(); }}>
            <div className="dept-form-modal" role="dialog" aria-modal="true" aria-label={isEdit ? 'Редактировать подразделение' : 'Новое подразделение'}>
                <div className="dept-form-header">
                    <h2 className="dept-form-title">
                        {isEdit ? 'Редактировать подразделение' : 'Новое подразделение'}
                    </h2>
                    <button className={`dept-form-close${shaking ? ' btn-flash' : ''}`} onClick={onClose} aria-label="Закрыть">×</button>
                </div>

                <form className="dept-form-body" onSubmit={handleSubmit} noValidate>
                    <div className="dept-form-field">
                        <label className="dept-form-label" htmlFor="df-name">Название *</label>
                        <input
                            id="df-name"
                            className="dept-form-input"
                            value={name}
                            onChange={e => setName(e.target.value)}
                            placeholder="Наименование подразделения"
                            autoFocus
                            required
                        />
                    </div>

                    <div className="dept-form-row">
                        <div className="dept-form-field">
                            <label className="dept-form-label" htmlFor="df-short-name">Аббревиатура</label>
                            <input
                                id="df-short-name"
                                className="dept-form-input"
                                value={shortName}
                                onChange={e => setShortName(e.target.value)}
                                placeholder="Напр. ИТ, HR, БУ"
                                maxLength={50}
                            />
                        </div>
                        <div className="dept-form-field">
                            <label className="dept-form-label" htmlFor="df-code">Код</label>
                            <input
                                id="df-code"
                                className="dept-form-input"
                                value={code}
                                onChange={e => setCode(e.target.value)}
                                placeholder="Уникальный код"
                                maxLength={50}
                            />
                        </div>
                    </div>

                    <div className="dept-form-field">
                        <label className="dept-form-label" htmlFor="df-description">Описание</label>
                        <textarea
                            id="df-description"
                            className="dept-form-textarea"
                            value={description}
                            onChange={e => setDescription(e.target.value)}
                            rows={3}
                            placeholder="Описание подразделения"
                        />
                    </div>

                    {/* Выбор родителя — только при создании */}
                    {!isEdit && (
                        <div className="dept-form-field">
                            <label className="dept-form-label" htmlFor="df-parent">Родительское подразделение</label>
                            <select
                                id="df-parent"
                                className="dept-form-select"
                                value={selectedParentId}
                                onChange={e => setSelectedParentId(e.target.value)}
                            >
                                <option value="">— Корневое подразделение —</option>
                                {flatOptions.map(o => (
                                    <option key={o.id} value={o.id}>{o.label}</option>
                                ))}
                            </select>
                        </div>
                    )}

                    {/* Статус — только при редактировании */}
                    {isEdit && (
                        <div className="dept-form-field">
                            <label className="dept-form-label" htmlFor="df-status">Статус</label>
                            <select
                                id="df-status"
                                className="dept-form-select"
                                value={status}
                                onChange={e => setStatus(Number(e.target.value) as DepartmentStatus)}
                            >
                                <option value={DEPARTMENT_STATUS_ACTIVE}>Активное</option>
                                <option value={DEPARTMENT_STATUS_ARCHIVED}>Архивное</option>
                            </select>
                        </div>
                    )}

                    {error && <div className="dept-form-error">{error}</div>}

                    <div className="dept-form-footer">
                        <button type="button" className={`dept-form-btn dept-form-btn--secondary${shaking ? ' btn-flash' : ''}`} onClick={onClose}>
                            Отмена
                        </button>
                        <button type="submit" className={`dept-form-btn dept-form-btn--primary${shaking ? ' btn-flash' : ''}`} disabled={saving}>
                            {saving ? 'Сохранение…' : 'Сохранить'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}
