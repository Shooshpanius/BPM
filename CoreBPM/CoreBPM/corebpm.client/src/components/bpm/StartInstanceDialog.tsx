import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';
import type { BpmProcessListItemDto } from '../../api/bpmApi';

interface Props {
    process: BpmProcessListItemDto;
    token: string;
    onLaunched: (instanceId: string) => void;
    onClose: () => void;
}

const STATE_LABELS: Record<api.BpmInstanceState, string> = {
    Active: 'Выполняется',
    Completed: 'Завершён',
    Cancelled: 'Прерван',
    Suspended: 'Приостановлен',
};

const STATE_COLORS: Record<api.BpmInstanceState, string> = {
    Active: '#2563eb',
    Completed: '#16a34a',
    Cancelled: '#dc2626',
    Suspended: '#d97706',
};

/** Диалог «Запуск процесса» — стартовое окно.
 * Поддерживает варианты: стандартный (поле названия) и автоматическое название (шаблон / ключевая переменная).
 */
export function StartInstanceDialog({ process, token, onLaunched, onClose }: Props) {
    const [settings, setSettings] = useState<api.BpmProcessSettingsDto | null>(null);
    const [variables, setVariables] = useState<api.BpmProcessVariableDto[]>([]);
    const [loadError, setLoadError] = useState<string | null>(null);

    // Поля формы
    const [instanceName, setInstanceName] = useState('');
    const [varValues, setVarValues] = useState<Record<string, string>>({});
    const [launching, setLaunching] = useState(false);
    const [launchError, setLaunchError] = useState<string | null>(null);

    const load = useCallback(async () => {
        try {
            const [s, vars] = await Promise.all([
                api.getProcessSettings(token, process.id),
                api.getVariables(token, process.id),
            ]);
            setSettings(s);
            // Показываем входные переменные (IsInput)
            setVariables(vars.filter(v => v.isInput));
            // Предзаполняем значениями по умолчанию
            const defaults: Record<string, string> = {};
            for (const v of vars.filter(v => v.isInput)) {
                if (v.defaultValue != null) defaults[v.name] = v.defaultValue;
            }
            setVarValues(defaults);
        } catch (e) {
            setLoadError(e instanceof Error ? e.message : 'Ошибка загрузки настроек');
        }
    }, [token, process.id]);

    useEffect(() => { load(); }, [load]);

    const autoName = settings && settings.instanceNameMode !== 'Manual';
    const requestName = settings?.requestInstanceNameOnStart !== false;

    const handleLaunch = async () => {
        if (!autoName && requestName && !instanceName.trim()) {
            setLaunchError('Введите название экземпляра');
            return;
        }
        if (!process.activeVersionNumber) {
            setLaunchError('Процесс не имеет опубликованной версии');
            return;
        }
        setLaunching(true);
        setLaunchError(null);
        try {
            const vars: Record<string, string | null> = {};
            for (const [k, v] of Object.entries(varValues)) {
                vars[k] = v || null;
            }
            const instance = await api.createInstance(token, process.id, {
                name: autoName ? undefined : (instanceName.trim() || undefined),
                variables: Object.keys(vars).length > 0 ? vars : undefined,
            });
            onLaunched(instance.id);
        } catch (e) {
            setLaunchError(e instanceof Error ? e.message : 'Ошибка запуска');
        } finally {
            setLaunching(false);
        }
    };

    return (
        <div className="pp-modal-overlay" onClick={onClose}>
            <div
                className="pp-modal"
                style={{ maxWidth: 520 }}
                onClick={e => e.stopPropagation()}
                role="dialog"
                aria-modal="true"
                aria-labelledby="si-title"
            >
                {/* Заголовок */}
                <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12, marginBottom: 16 }}>
                    <div style={{ flex: 1 }}>
                        <h2 id="si-title" className="pp-modal-title" style={{ marginBottom: 4 }}>
                            ▷ Запуск процесса
                        </h2>
                        <p style={{ margin: 0, fontSize: 13, color: '#6b7280' }}>
                            {process.name}
                        </p>
                        {process.description && (
                            <p style={{ margin: '6px 0 0', fontSize: 12, color: '#9ca3af', lineHeight: 1.5 }}>
                                {process.description}
                            </p>
                        )}
                    </div>
                </div>

                {loadError && (
                    <div className="pp-error" style={{ marginBottom: 12, fontSize: 13 }}>{loadError}</div>
                )}

                {/* Поле названия (только если режим Manual и requestNameOnStart) */}
                {!autoName && requestName && (
                    <div className="pp-modal-field">
                        <label htmlFor="si-name">
                            Название экземпляра *
                        </label>
                        <input
                            id="si-name"
                            className="pp-input"
                            type="text"
                            value={instanceName}
                            onChange={e => setInstanceName(e.target.value)}
                            placeholder="Например: Согласование договора №123"
                            autoFocus
                            onKeyDown={e => e.key === 'Enter' && handleLaunch()}
                        />
                    </div>
                )}

                {/* Информация об автоматическом названии */}
                {autoName && settings && (
                    <div style={{
                        background: '#f0fdf4',
                        border: '1px solid #bbf7d0',
                        borderRadius: 6,
                        padding: '8px 12px',
                        marginBottom: 12,
                        fontSize: 12,
                        color: '#166534',
                    }}>
                        <strong>Название формируется автоматически</strong>
                        {settings.instanceNameMode === 'Template' && settings.instanceNameTemplate && (
                            <span> по шаблону: <code>{settings.instanceNameTemplate}</code></span>
                        )}
                        {settings.instanceNameMode === 'KeyVariable' && settings.keyVariableName && (
                            <span> из переменной: <code>{settings.keyVariableName}</code></span>
                        )}
                    </div>
                )}

                {/* Входные переменные */}
                {variables.length > 0 && (
                    <div>
                        <div style={{ fontSize: 12, fontWeight: 600, color: '#374151', marginBottom: 8 }}>
                            Переменные запуска
                        </div>
                        {variables.map(v => (
                            <div key={v.id} className="pp-modal-field" style={{ marginBottom: 8 }}>
                                <label htmlFor={`si-var-${v.name}`} style={{ marginBottom: 3 }}>
                                    {v.name}
                                    <span style={{ color: '#9ca3af', fontWeight: 400, marginLeft: 6, fontSize: 11 }}>
                                        ({TYPE_LABELS[v.variableType] ?? v.variableType})
                                    </span>
                                </label>
                                <VariableInput
                                    id={`si-var-${v.name}`}
                                    variableType={v.variableType}
                                    value={varValues[v.name] ?? ''}
                                    onChange={val => setVarValues(prev => ({ ...prev, [v.name]: val }))}
                                />
                            </div>
                        ))}
                    </div>
                )}

                {/* Предупреждение о неопубликованном процессе */}
                {!process.activeVersionNumber && (
                    <div style={{
                        background: '#fef9c3',
                        border: '1px solid #fde047',
                        borderRadius: 6,
                        padding: '8px 12px',
                        marginBottom: 12,
                        fontSize: 12,
                        color: '#854d0e',
                    }}>
                        ⚠️ Процесс не имеет опубликованной версии и не может быть запущен.
                        Опубликуйте версию в дизайнере процессов.
                    </div>
                )}

                {launchError && (
                    <div className="pp-error" style={{ marginBottom: 12, fontSize: 13 }}>{launchError}</div>
                )}

                {/* Кнопки */}
                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 16 }}>
                    <button className="pp-btn-secondary" onClick={onClose} disabled={launching}>
                        Отмена
                    </button>
                    <button
                        className="pp-btn-primary"
                        onClick={handleLaunch}
                        disabled={launching || !process.activeVersionNumber}
                    >
                        {launching ? 'Запуск...' : '▷ Запустить'}
                    </button>
                </div>
            </div>
        </div>
    );
}

// ─── Вспомогательные компоненты ───────────────────────────────────────────────

interface VariableInputProps {
    id: string;
    variableType: api.BpmVariableType;
    value: string;
    onChange: (val: string) => void;
}

function VariableInput({ id, variableType, value, onChange }: VariableInputProps) {
    if (variableType === 'Bool') {
        return (
            <select id={id} className="pp-org-select" value={value} onChange={e => onChange(e.target.value)}>
                <option value="">— не задано —</option>
                <option value="true">Да</option>
                <option value="false">Нет</option>
            </select>
        );
    }
    if (variableType === 'Date') {
        return (
            <input id={id} className="pp-input" type="date" value={value} onChange={e => onChange(e.target.value)} />
        );
    }
    if (variableType === 'DateTime') {
        return (
            <input id={id} className="pp-input" type="datetime-local" value={value} onChange={e => onChange(e.target.value)} />
        );
    }
    if (variableType === 'Int') {
        return (
            <input id={id} className="pp-input" type="number" step="1" value={value} onChange={e => onChange(e.target.value)} />
        );
    }
    if (variableType === 'Decimal') {
        return (
            <input id={id} className="pp-input" type="number" step="any" value={value} onChange={e => onChange(e.target.value)} />
        );
    }
    return (
        <input id={id} className="pp-input" type="text" value={value} onChange={e => onChange(e.target.value)} />
    );
}

const TYPE_LABELS: Partial<Record<api.BpmVariableType, string>> = {
    String: 'Строка',
    Int: 'Целое',
    Decimal: 'Число',
    Bool: 'Булево',
    Date: 'Дата',
    DateTime: 'Дата/время',
    Json: 'JSON',
    File: 'Файл',
    User: 'Пользователь',
    List: 'Список',
};

export { STATE_LABELS, STATE_COLORS };
