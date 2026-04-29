import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
type RetryStrategy = 'None' | 'Linear' | 'Exponential';

interface ServiceTaskConfig {
    httpMethod: HttpMethod;
    url: string;
    headers: string;
    requestBody: string;
    responseMapping: string;
    timeoutSeconds: number;
    maxRetries: number;
    retryStrategy: RetryStrategy;
}

const DEFAULTS: ServiceTaskConfig = {
    httpMethod: 'POST',
    url: '',
    headers: '',
    requestBody: '',
    responseMapping: '',
    timeoutSeconds: 30,
    maxRetries: 0,
    retryStrategy: 'None',
};

/** Вкладка «Выполнение» для ServiceTask (HTTP-вызов). */
export function ServiceTaskTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<ServiceTaskConfig>(DEFAULTS);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);

    const load = useCallback(async () => {
        try {
            const dto = await api.getElementConfig(token, processId, elementId);
            if (dto) {
                try {
                    const parsed = JSON.parse(dto.configJson) as Partial<ServiceTaskConfig>;
                    setConfig({ ...DEFAULTS, ...parsed });
                } catch { /* невалидный JSON */ }
            } else {
                setConfig(DEFAULTS);
            }
            setDirty(false);
        } catch { /* сетевая ошибка */ }
    }, [token, processId, elementId]);

    useEffect(() => { load(); }, [load]);

    const update = <K extends keyof ServiceTaskConfig>(key: K, value: ServiceTaskConfig[K]) => {
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
            {/* HTTP-запрос */}
            <div className="bpp-group">
                <div className="bpp-group-title">HTTP-запрос</div>
                <div className="bpp-field">
                    <label className="bpp-label required">Метод</label>
                    <select
                        className="bpp-select"
                        value={config.httpMethod}
                        onChange={e => update('httpMethod', e.target.value as HttpMethod)}
                    >
                        {(['GET', 'POST', 'PUT', 'DELETE', 'PATCH'] as HttpMethod[]).map(m => (
                            <option key={m} value={m}>{m}</option>
                        ))}
                    </select>
                </div>
                <div className="bpp-field">
                    <label className="bpp-label required">URL</label>
                    <input
                        className="bpp-input"
                        value={config.url}
                        onChange={e => update('url', e.target.value)}
                        placeholder="https://api.example.com/endpoint/${variables.id}"
                    />
                    <p className="bpp-hint">Поддерживаются шаблоны: {'{${variableName}}'}</p>
                </div>
                <div className="bpp-field">
                    <label className="bpp-label">Заголовки (JSON)</label>
                    <textarea
                        className="bpp-textarea"
                        value={config.headers}
                        onChange={e => update('headers', e.target.value)}
                        placeholder={'{\n  "Authorization": "Bearer ${variables.token}",\n  "Content-Type": "application/json"\n}'}
                        rows={4}
                    />
                </div>
                {config.httpMethod !== 'GET' && (
                    <div className="bpp-field">
                        <label className="bpp-label">Тело запроса (JSON-шаблон)</label>
                        <textarea
                            className="bpp-textarea"
                            value={config.requestBody}
                            onChange={e => update('requestBody', e.target.value)}
                            placeholder={'{\n  "name": "${variables.customerName}"\n}'}
                            rows={5}
                        />
                    </div>
                )}
                <div className="bpp-field">
                    <label className="bpp-label">Маппинг ответа в переменные</label>
                    <textarea
                        className="bpp-textarea"
                        value={config.responseMapping}
                        onChange={e => update('responseMapping', e.target.value)}
                        placeholder={'$.orderId → orderId\n$.status → orderStatus'}
                        rows={3}
                    />
                    <p className="bpp-hint">Формат: JSONPath-выражение → имя переменной, по одному на строку.</p>
                </div>
            </div>

            {/* Таймаут и повторы */}
            <div className="bpp-group">
                <div className="bpp-group-title">Надёжность</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                    <div className="bpp-field">
                        <label className="bpp-label">Таймаут (секунды)</label>
                        <input
                            className="bpp-input"
                            type="number"
                            min={1}
                            max={600}
                            value={config.timeoutSeconds}
                            onChange={e => update('timeoutSeconds', Number(e.target.value))}
                        />
                    </div>
                    <div className="bpp-field">
                        <label className="bpp-label">Макс. повторов</label>
                        <input
                            className="bpp-input"
                            type="number"
                            min={0}
                            max={10}
                            value={config.maxRetries}
                            onChange={e => update('maxRetries', Number(e.target.value))}
                        />
                    </div>
                </div>
                {config.maxRetries > 0 && (
                    <div className="bpp-field">
                        <label className="bpp-label">Стратегия повторов</label>
                        <select
                            className="bpp-select"
                            value={config.retryStrategy}
                            onChange={e => update('retryStrategy', e.target.value as RetryStrategy)}
                        >
                            <option value="None">Без задержки</option>
                            <option value="Linear">Линейная задержка</option>
                            <option value="Exponential">Экспоненциальная задержка</option>
                        </select>
                    </div>
                )}
            </div>

            <div className="bpp-btn-row">
                <button className="bpp-btn bpp-btn-primary" onClick={save} disabled={saving || !dirty}>
                    {saving ? 'Сохранение...' : 'Сохранить'}
                </button>
                {!dirty && <span style={{ fontSize: 11, color: '#9ca3af', alignSelf: 'center' }}>Сохранено</span>}
            </div>
        </div>
    );
}
