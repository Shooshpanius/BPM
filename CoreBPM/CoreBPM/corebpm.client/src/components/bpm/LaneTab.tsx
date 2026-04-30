import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

type DocumentAccessLevel = 'Read' | 'Write' | 'Full';

interface LaneConfig {
    /** Ответственным за экземпляр становится первый взявший задачу в этой ЗО */
    changeResponsibleOnEnter: boolean;
    /** Оповещать нового ответственного при смене */
    notifyNewResponsible: boolean;
    /** Запрет замещения: игнорировать системные настройки замещения для задач в этой ЗО */
    disableSubstitution: boolean;
    /** Автоматически выдавать права на документы экземпляра исполнителю задачи в ЗО */
    autoGrantDocumentAccess: boolean;
    /** Уровень доступа к документам при автовыдаче */
    documentAccessLevel: DocumentAccessLevel;
    /** Разрешить исполнителю самостоятельно назначать права на документы */
    allowAssignDocumentAccess: boolean;
}

const DEFAULTS: LaneConfig = {
    changeResponsibleOnEnter: false,
    notifyNewResponsible: true,
    disableSubstitution: false,
    autoGrantDocumentAccess: false,
    documentAccessLevel: 'Read',
    allowAssignDocumentAccess: false,
};

const ACCESS_LEVEL_LABELS: Record<DocumentAccessLevel, string> = {
    Read: 'Только чтение',
    Write: 'Редактирование',
    Full: 'Полный доступ',
};

/** Вкладка «Общие» для дорожки (Lane) — настройки зоны ответственности. */
export function LaneTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<LaneConfig>(DEFAULTS);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        try {
            const dto = await api.getElementConfig(token, processId, elementId);
            if (dto) {
                try {
                    const parsed = JSON.parse(dto.configJson) as Partial<LaneConfig>;
                    setConfig({ ...DEFAULTS, ...parsed });
                } catch { /* невалидный JSON */ }
            } else {
                setConfig(DEFAULTS);
            }
            setDirty(false);
        } catch { /* сетевая ошибка */ }
    }, [token, processId, elementId]);

    useEffect(() => { load(); }, [load]);

    const update = <K extends keyof LaneConfig>(key: K, value: LaneConfig[K]) => {
        setConfig(prev => ({ ...prev, [key]: value }));
        setDirty(true);
    };

    const save = async () => {
        setSaving(true);
        setError(null);
        try {
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(config));
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div>
            {error && <p className="bpp-error">{error}</p>}

            {/* ─── Смена ответственного ─── */}
            <div className="bpp-group">
                <div className="bpp-group-title">Смена ответственного</div>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={config.changeResponsibleOnEnter}
                        onChange={e => update('changeResponsibleOnEnter', e.target.checked)}
                    />
                    <span>Ответственным за экземпляр становится первый взявший задачу в этой ЗО</span>
                </label>
                {config.changeResponsibleOnEnter && (
                    <label className="bpp-checkbox-row" style={{ marginTop: 6 }}>
                        <input
                            type="checkbox"
                            checked={config.notifyNewResponsible}
                            onChange={e => update('notifyNewResponsible', e.target.checked)}
                        />
                        <span>Оповестить нового ответственного</span>
                    </label>
                )}
            </div>

            {/* ─── Замещение ─── */}
            <div className="bpp-group">
                <div className="bpp-group-title">Замещение</div>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={config.disableSubstitution}
                        onChange={e => update('disableSubstitution', e.target.checked)}
                    />
                    <span>Запретить замещение в этой ЗО</span>
                </label>
                <p className="bpp-hint">
                    При установке флага системные настройки замещения будут игнорироваться для задач этой дорожки.
                </p>
            </div>

            {/* ─── Права на документы ─── */}
            <div className="bpp-group">
                <div className="bpp-group-title">Права на документы</div>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={config.autoGrantDocumentAccess}
                        onChange={e => update('autoGrantDocumentAccess', e.target.checked)}
                    />
                    <span>Автоматически выдавать исполнителю права на документы экземпляра</span>
                </label>

                {config.autoGrantDocumentAccess && (
                    <>
                        <div className="bpp-field" style={{ marginTop: 8 }}>
                            <label className="bpp-label">Уровень доступа</label>
                            <select
                                className="bpp-select"
                                value={config.documentAccessLevel}
                                onChange={e => update('documentAccessLevel', e.target.value as DocumentAccessLevel)}
                            >
                                {(Object.entries(ACCESS_LEVEL_LABELS) as [DocumentAccessLevel, string][]).map(([val, label]) => (
                                    <option key={val} value={val}>{label}</option>
                                ))}
                            </select>
                        </div>
                        <label className="bpp-checkbox-row" style={{ marginTop: 6 }}>
                            <input
                                type="checkbox"
                                checked={config.allowAssignDocumentAccess}
                                onChange={e => update('allowAssignDocumentAccess', e.target.checked)}
                            />
                            <span>Разрешить исполнителю самостоятельно назначать права на документы</span>
                        </label>
                    </>
                )}
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
