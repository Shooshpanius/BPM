import { useCallback, useEffect, useState } from 'react';
import * as api from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
}

const DEFAULT_SETTINGS: api.BpmProcessSettingsDto = {
    processId: '',
    launchFromPortalEnabled: true,
    showInStartList: true,
    externalStartEnabled: false,
    externalStartMethods: [],
    instanceNameMode: 'Manual',
    requestInstanceNameOnStart: true,
    dataClassName: '',
    dataTableName: '',
    processMetricsClassName: '',
    processMetricsTableName: '',
    instanceMetricsClassName: '',
    instanceMetricsTableName: '',
    secondRuntimeEnabled: false,
    hasExternalStartToken: false,
};

export function ProcessSettingsTab({ processId, token }: Props) {
    const [settings, setSettings] = useState<api.BpmProcessSettingsDto>(DEFAULT_SETTINGS);
    const [variableNames, setVariableNames] = useState<string[]>([]);
    const [saving, setSaving] = useState(false);
    const [loading, setLoading] = useState(true);
    const [dirty, setDirty] = useState(false);
    const [rotateResult, setRotateResult] = useState<api.RotateExternalTokenResponse | null>(null);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const [settingsDto, variables] = await Promise.all([
                api.getProcessSettings(token, processId),
                api.getVariables(token, processId),
            ]);
            setSettings(settingsDto);
            setVariableNames(variables.map(v => v.name));
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Не удалось загрузить настройки процесса');
        } finally {
            setLoading(false);
        }
    }, [token, processId]);

    useEffect(() => {
        load();
    }, [load]);

    const update = <K extends keyof api.BpmProcessSettingsDto>(key: K, value: api.BpmProcessSettingsDto[K]) => {
        setSettings(prev => ({ ...prev, [key]: value }));
        setDirty(true);
    };

    const toggleMethod = (method: string, enabled: boolean) => {
        const next = enabled
            ? [...settings.externalStartMethods, method]
            : settings.externalStartMethods.filter(v => v !== method);
        update('externalStartMethods', Array.from(new Set(next)));
    };

    const handleSave = async () => {
        setSaving(true);
        setError(null);
        try {
            const result = await api.updateProcessSettings(token, processId, {
                launchFromPortalEnabled: settings.launchFromPortalEnabled,
                showInStartList: settings.showInStartList,
                externalStartEnabled: settings.externalStartEnabled,
                externalStartMethods: settings.externalStartMethods,
                externalStartAllowedIps: settings.externalStartAllowedIps,
                instanceNameMode: settings.instanceNameMode,
                requestInstanceNameOnStart: settings.requestInstanceNameOnStart,
                instanceNameTemplate: settings.instanceNameTemplate,
                keyVariableName: settings.keyVariableName,
                dataClassName: settings.dataClassName,
                dataTableName: settings.dataTableName,
                processMetricsClassName: settings.processMetricsClassName,
                processMetricsTableName: settings.processMetricsTableName,
                instanceMetricsClassName: settings.instanceMetricsClassName,
                instanceMetricsTableName: settings.instanceMetricsTableName,
                secondRuntimeEnabled: settings.secondRuntimeEnabled,
            });
            setSettings(result);
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Не удалось сохранить настройки');
        } finally {
            setSaving(false);
        }
    };

    const handleRotateToken = async () => {
        setError(null);
        try {
            const result = await api.rotateExternalToken(token, processId);
            setRotateResult(result);
            await load();
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Не удалось сгенерировать токен');
        }
    };

    if (loading) return <div className="bpp-empty"><span>Загрузка настроек…</span></div>;

    return (
        <div>
            {error && <div className="bpp-error-box">{error}</div>}
            {rotateResult && (
                <div className="bpp-success-box">
                    <div>Новый токен сгенерирован. Сохраните его сейчас — повторно он не показывается.</div>
                    <code>{rotateResult.token}</code>
                </div>
            )}

            <div className="bpp-group">
                <div className="bpp-group-title">Варианты запуска</div>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={settings.launchFromPortalEnabled}
                        onChange={e => update('launchFromPortalEnabled', e.target.checked)}
                    />
                    <span>Запуск из веб-приложений</span>
                </label>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={settings.showInStartList}
                        onChange={e => update('showInStartList', e.target.checked)}
                    />
                    <span>Видимость в списке процессов</span>
                </label>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={settings.externalStartEnabled}
                        onChange={e => update('externalStartEnabled', e.target.checked)}
                    />
                    <span>Запуск из внешних систем</span>
                </label>
                {settings.externalStartEnabled && (
                    <>
                        <div className="bpp-field">
                            <label className="bpp-label">Методы внешнего запуска</label>
                            <div className="bpp-inline-checks">
                                {['GET', 'POST', 'SOAP'].map(method => (
                                    <label key={method} className="bpp-checkbox-row">
                                        <input
                                            type="checkbox"
                                            checked={settings.externalStartMethods.includes(method)}
                                            onChange={e => toggleMethod(method, e.target.checked)}
                                        />
                                        <span>{method}</span>
                                    </label>
                                ))}
                            </div>
                        </div>
                        <div className="bpp-field">
                            <label className="bpp-label">Ограничения доступа (IP/CIDR)</label>
                            <textarea
                                className="bpp-textarea"
                                rows={3}
                                value={settings.externalStartAllowedIps ?? ''}
                                onChange={e => update('externalStartAllowedIps', e.target.value || undefined)}
                                placeholder="10.0.0.0/24, 192.168.1.10"
                            />
                        </div>
                        <div className="bpp-field">
                            <label className="bpp-label">Токен внешнего запуска</label>
                            <div className="bpp-token-row">
                                <span>{settings.hasExternalStartToken ? (settings.externalStartTokenPreview ?? 'Сгенерирован') : 'Не сгенерирован'}</span>
                                <button className="bpp-btn" onClick={handleRotateToken}>Сгенерировать / ротировать</button>
                            </div>
                        </div>
                    </>
                )}
            </div>

            <div className="bpp-group">
                <div className="bpp-group-title">Схема наименования экземпляров</div>
                <div className="bpp-field">
                    <label className="bpp-label">Режим</label>
                    <select
                        className="bpp-select"
                        value={settings.instanceNameMode}
                        onChange={e => update('instanceNameMode', e.target.value as api.BpmInstanceNameMode)}
                    >
                        <option value="Manual">Ручной ввод</option>
                        <option value="KeyVariable">По ключевой переменной</option>
                        <option value="Template">По шаблону</option>
                    </select>
                </div>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={settings.requestInstanceNameOnStart}
                        onChange={e => update('requestInstanceNameOnStart', e.target.checked)}
                    />
                    <span>Запрашивать название при запуске</span>
                </label>
                {settings.instanceNameMode === 'Template' && (
                    <div className="bpp-field">
                        <label className="bpp-label">Шаблон названия</label>
                        <input
                            className="bpp-input"
                            value={settings.instanceNameTemplate ?? ''}
                            onChange={e => update('instanceNameTemplate', e.target.value || undefined)}
                            placeholder="Договор {{contractNumber}} от {{date}}"
                        />
                    </div>
                )}
                <div className="bpp-field">
                    <label className="bpp-label">Ключевая переменная</label>
                    <select
                        className="bpp-select"
                        value={settings.keyVariableName ?? ''}
                        onChange={e => update('keyVariableName', e.target.value || undefined)}
                    >
                        <option value="">— Не выбрана —</option>
                        {variableNames.map(name => (
                            <option key={name} value={name}>{name}</option>
                        ))}
                    </select>
                </div>
            </div>

            <div className="bpp-group">
                <div className="bpp-group-title">Системные переменные процесса</div>
                <div className="bpp-field">
                    <label className="bpp-label">Имя класса данных</label>
                    <input className="bpp-input" value={settings.dataClassName} onChange={e => update('dataClassName', e.target.value)} />
                </div>
                <div className="bpp-field">
                    <label className="bpp-label">Имя таблицы данных</label>
                    <input className="bpp-input" value={settings.dataTableName} onChange={e => update('dataTableName', e.target.value)} />
                </div>
                <div className="bpp-field">
                    <label className="bpp-label">Имя класса метрик процесса</label>
                    <input className="bpp-input" value={settings.processMetricsClassName} onChange={e => update('processMetricsClassName', e.target.value)} />
                </div>
                <div className="bpp-field">
                    <label className="bpp-label">Имя таблицы метрик процесса</label>
                    <input className="bpp-input" value={settings.processMetricsTableName} onChange={e => update('processMetricsTableName', e.target.value)} />
                </div>
                <div className="bpp-field">
                    <label className="bpp-label">Имя класса метрик экземпляра</label>
                    <input className="bpp-input" value={settings.instanceMetricsClassName} onChange={e => update('instanceMetricsClassName', e.target.value)} />
                </div>
                <div className="bpp-field">
                    <label className="bpp-label">Имя таблицы метрик экземпляра</label>
                    <input className="bpp-input" value={settings.instanceMetricsTableName} onChange={e => update('instanceMetricsTableName', e.target.value)} />
                </div>
            </div>

            <div className="bpp-group">
                <div className="bpp-group-title">Параметры совместимости</div>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={settings.secondRuntimeEnabled}
                        onChange={e => update('secondRuntimeEnabled', e.target.checked)}
                    />
                    <span>Включить второй рантайм</span>
                </label>
                {settings.secondRuntimeUpgradedAt && (
                    <p className="bpp-hint">Процесс обновлён: {new Date(settings.secondRuntimeUpgradedAt).toLocaleString('ru-RU')}</p>
                )}
            </div>

            <div className="bpp-btn-row">
                <button className="bpp-btn bpp-btn-primary" onClick={handleSave} disabled={saving || !dirty}>
                    {saving ? 'Сохранение…' : 'Сохранить'}
                </button>
                {!dirty && <span className="bpp-muted">Сохранено</span>}
            </div>

            {/* PDF-регламент: шаблон администратора (ожидает FR-ADM) */}
            <div className="bpp-group">
                <div className="bpp-group-title">PDF-регламент: шаблон</div>
                <div style={{ background: '#fef3c7', border: '1px solid #fcd34d', borderRadius: 6, padding: '10px 12px', fontSize: 12 }}>
                    <strong>⏳ Ожидает FR-ADM</strong>
                    <p style={{ margin: '6px 0 0', color: '#78350f' }}>
                        Загрузка кастомного Word/.docx-шаблона регламента будет доступна после реализации
                        модуля администрирования (FR-ADM). До этого используется встроенный шаблон
                        при нажатии «Скачать PDF-регламент» в панели версий.
                    </p>
                </div>
            </div>
        </div>
    );
}
