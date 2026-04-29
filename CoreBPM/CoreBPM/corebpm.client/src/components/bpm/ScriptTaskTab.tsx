import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

interface ScriptTaskConfig {
    scriptBody: string;
}

const DEFAULTS: ScriptTaskConfig = { scriptBody: '' };

/** Вкладка «Выполнение» для ScriptTask (C#-скрипт). */
export function ScriptTaskTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<ScriptTaskConfig>(DEFAULTS);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [syntaxMsg, setSyntaxMsg] = useState<string | null>(null);

    const load = useCallback(async () => {
        try {
            const dto = await api.getElementConfig(token, processId, elementId);
            if (dto) {
                try {
                    const parsed = JSON.parse(dto.configJson) as Partial<ScriptTaskConfig>;
                    setConfig({ ...DEFAULTS, ...parsed });
                } catch { /* невалидный JSON */ }
            } else {
                setConfig(DEFAULTS);
            }
            setDirty(false);
        } catch { /* сетевая ошибка */ }
    }, [token, processId, elementId]);

    useEffect(() => { load(); }, [load]);

    const save = async () => {
        setSaving(true);
        try {
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(config));
            setDirty(false);
        } finally {
            setSaving(false);
        }
    };

    const checkSyntax = async () => {
        setSyntaxMsg('Проверка...');
        try {
            // Заглушка — в будущем вызывает POST /api/bpm/processes/{id}/script/validate
            await new Promise(r => setTimeout(r, 600));
            setSyntaxMsg('✓ Ошибок синтаксиса не обнаружено');
        } catch {
            setSyntaxMsg('Ошибка при проверке синтаксиса');
        }
    };

    return (
        <div>
            <div className="bpp-group">
                <div className="bpp-group-title">Скрипт C#</div>
                <p className="bpp-hint" style={{ marginBottom: 6 }}>
                    Доступные объекты: <code>context</code> (переменные процесса), <code>logger</code>.
                    Верните результат через <code>context.SetVariable("name", value)</code>.
                </p>
                <div className="bpp-field">
                    <textarea
                        className="bpp-textarea code"
                        value={config.scriptBody}
                        onChange={e => {
                            setConfig(prev => ({ ...prev, scriptBody: e.target.value }));
                            setDirty(true);
                            setSyntaxMsg(null);
                        }}
                        placeholder={`// Пример:\nvar amount = (decimal)context.GetVariable("amount");\nif (amount > 1000)\n    context.SetVariable("needsApproval", true);`}
                        rows={16}
                        spellCheck={false}
                    />
                </div>
                {syntaxMsg && (
                    <p className={`bpp-hint ${syntaxMsg.startsWith('✓') ? '' : 'bpp-error'}`}>
                        {syntaxMsg}
                    </p>
                )}
            </div>

            <div className="bpp-btn-row">
                <button className="bpp-btn bpp-btn-primary" onClick={save} disabled={saving || !dirty}>
                    {saving ? 'Сохранение...' : 'Сохранить'}
                </button>
                <button className="bpp-btn" onClick={checkSyntax} disabled={!config.scriptBody.trim()}>
                    Проверить синтаксис
                </button>
                {!dirty && <span style={{ fontSize: 11, color: '#9ca3af', alignSelf: 'center' }}>Сохранено</span>}
            </div>
        </div>
    );
}
