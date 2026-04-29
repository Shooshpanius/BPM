import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

/** Тип исполнителя пользовательской задачи */
type AssigneeType = 'Role' | 'User' | 'Expression';

/** Тип срока выполнения */
type DueDateType = 'Fixed' | 'Relative' | 'Variable';

/** Приоритет задачи */
type PriorityLevel = 'Low' | 'Normal' | 'High' | 'Critical';

interface UserTaskConfig {
    assigneeType: AssigneeType;
    assigneeValue: string;
    dueDateType: DueDateType;
    dueDateValue: string;
    priority: PriorityLevel;
    allowDelegation: boolean;
    supervisorControl: boolean;
}

const DEFAULTS: UserTaskConfig = {
    assigneeType: 'Role',
    assigneeValue: '',
    dueDateType: 'Relative',
    dueDateValue: '',
    priority: 'Normal',
    allowDelegation: false,
    supervisorControl: false,
};

/** Вкладка «Выполнение» для UserTask. */
export function UserTaskTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<UserTaskConfig>(DEFAULTS);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);

    // Загружаем конфигурацию из API
    const load = useCallback(async () => {
        try {
            const dto = await api.getElementConfig(token, processId, elementId);
            if (dto) {
                try {
                    const parsed = JSON.parse(dto.configJson) as Partial<UserTaskConfig>;
                    setConfig({ ...DEFAULTS, ...parsed });
                } catch { /* невалидный JSON — оставляем defaults */ }
            } else {
                setConfig(DEFAULTS);
            }
            setDirty(false);
        } catch { /* сетевая ошибка — оставляем defaults */ }
    }, [token, processId, elementId]);

    useEffect(() => { load(); }, [load]);

    const update = <K extends keyof UserTaskConfig>(key: K, value: UserTaskConfig[K]) => {
        setConfig(prev => ({ ...prev, [key]: value }));
        setDirty(true);
    };

    const save = async () => {
        setSaving(true);
        try {
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(config));
            setDirty(false);
        } finally {
            setSaving(false);
        }
    };

    return (
        <div>
            {/* Исполнитель */}
            <div className="bpp-group">
                <div className="bpp-group-title">Исполнитель</div>
                <div className="bpp-field">
                    <label className="bpp-label">Тип исполнителя</label>
                    <div className="bpp-radio-group">
                        {(['Role', 'User', 'Expression'] as AssigneeType[]).map(type => (
                            <label key={type} className="bpp-radio-row">
                                <input
                                    type="radio"
                                    checked={config.assigneeType === type}
                                    onChange={() => update('assigneeType', type)}
                                />
                                {type === 'Role' ? 'Роль' : type === 'User' ? 'Конкретный пользователь' : 'EL-выражение из переменной'}
                            </label>
                        ))}
                    </div>
                </div>
                <div className="bpp-field">
                    <label className="bpp-label">
                        {config.assigneeType === 'Role' ? 'Название роли' :
                            config.assigneeType === 'User' ? 'Email / ID пользователя' :
                                'Выражение (например: ${initiator})'}
                    </label>
                    <input
                        className="bpp-input"
                        value={config.assigneeValue}
                        onChange={e => update('assigneeValue', e.target.value)}
                        placeholder={
                            config.assigneeType === 'Role' ? 'например: Manager' :
                                config.assigneeType === 'User' ? 'например: user@company.com' :
                                    '${variables.assignee}'
                        }
                    />
                </div>
            </div>

            {/* Срок */}
            <div className="bpp-group">
                <div className="bpp-group-title">Срок выполнения</div>
                <div className="bpp-field">
                    <label className="bpp-label">Тип срока</label>
                    <select
                        className="bpp-select"
                        value={config.dueDateType}
                        onChange={e => update('dueDateType', e.target.value as DueDateType)}
                    >
                        <option value="Fixed">Фиксированная дата</option>
                        <option value="Relative">Относительный (от даты старта)</option>
                        <option value="Variable">Из переменной процесса</option>
                    </select>
                </div>
                <div className="bpp-field">
                    <label className="bpp-label">
                        {config.dueDateType === 'Fixed' ? 'Дата (ISO 8601, например: 2025-12-31T18:00:00Z)' :
                            config.dueDateType === 'Relative' ? 'Длительность (например: P2D = 2 дня, PT8H = 8 часов)' :
                                'Имя переменной'}
                    </label>
                    <input
                        className="bpp-input"
                        value={config.dueDateValue}
                        onChange={e => update('dueDateValue', e.target.value)}
                        placeholder={
                            config.dueDateType === 'Fixed' ? '2025-12-31T18:00:00Z' :
                                config.dueDateType === 'Relative' ? 'P2D' :
                                    'dueDate'
                        }
                    />
                </div>
            </div>

            {/* Приоритет */}
            <div className="bpp-group">
                <div className="bpp-group-title">Параметры задачи</div>
                <div className="bpp-field">
                    <label className="bpp-label">Приоритет</label>
                    <select
                        className="bpp-select"
                        value={config.priority}
                        onChange={e => update('priority', e.target.value as PriorityLevel)}
                    >
                        <option value="Low">Низкий</option>
                        <option value="Normal">Обычный</option>
                        <option value="High">Высокий</option>
                        <option value="Critical">Критический</option>
                    </select>
                </div>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={config.allowDelegation}
                        onChange={e => update('allowDelegation', e.target.checked)}
                    />
                    <span>Разрешить делегирование</span>
                </label>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={config.supervisorControl}
                        onChange={e => update('supervisorControl', e.target.checked)}
                    />
                    <span>Контроль руководителем</span>
                </label>
            </div>

            <div className="bpp-btn-row">
                <button
                    className="bpp-btn bpp-btn-primary"
                    onClick={save}
                    disabled={saving || !dirty}
                >
                    {saving ? 'Сохранение...' : 'Сохранить'}
                </button>
                {!dirty && <span style={{ fontSize: 11, color: '#9ca3af', alignSelf: 'center' }}>Сохранено</span>}
            </div>
        </div>
    );
}
