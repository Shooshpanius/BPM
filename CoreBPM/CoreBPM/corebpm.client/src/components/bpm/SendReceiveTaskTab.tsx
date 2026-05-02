import { useState } from 'react';

interface Props {
    /** Тип узла: 'bpmn:SendTask' | 'bpmn:ReceiveTask' */
    taskType: 'bpmn:SendTask' | 'bpmn:ReceiveTask';
    /** Код/имя сообщения */
    messageRef: string;
    /** Корреляционные ключи (через запятую) */
    correlationKeys: string;
    /** Тело сообщения (JSON-шаблон) */
    messageBody: string;
    /** Тайм-аут ожидания в секундах (только для ReceiveTask) */
    timeoutSeconds: number;
    onChange: (field: string, value: string | number) => void;
}

/** Вкладка настройки Send Task / Receive Task. */
export default function SendReceiveTaskTab({
    taskType,
    messageRef,
    correlationKeys,
    messageBody,
    timeoutSeconds,
    onChange,
}: Props) {
    const [bodyExpanded, setBodyExpanded] = useState(false);
    const isSend = taskType === 'bpmn:SendTask';

    return (
        <div className="tab-section">
            <div className="prop-row">
                <label className="prop-label">Код сообщения</label>
                <input
                    className="prop-input"
                    type="text"
                    value={messageRef}
                    placeholder="например: order.created"
                    onChange={e => onChange('messageRef', e.target.value)}
                />
            </div>

            <div className="prop-row">
                <label className="prop-label">Корреляционные ключи</label>
                <input
                    className="prop-input"
                    type="text"
                    value={correlationKeys}
                    placeholder="orderId, customerId"
                    onChange={e => onChange('correlationKeys', e.target.value)}
                />
                <span className="prop-hint">Ключи через запятую для связки экземпляров.</span>
            </div>

            {isSend && (
                <div className="prop-row">
                    <label className="prop-label">
                        Тело сообщения (JSON)&nbsp;
                        <button
                            className="btn-link"
                            type="button"
                            onClick={() => setBodyExpanded(v => !v)}
                        >
                            {bodyExpanded ? '▲ свернуть' : '▼ развернуть'}
                        </button>
                    </label>
                    {bodyExpanded && (
                        <textarea
                            className="prop-textarea prop-textarea--code"
                            rows={8}
                            value={messageBody}
                            placeholder={'{\n  "orderId": "{{orderId}}"\n}'}
                            onChange={e => onChange('messageBody', e.target.value)}
                        />
                    )}
                </div>
            )}

            {!isSend && (
                <div className="prop-row">
                    <label className="prop-label">Тайм-аут ожидания (сек.)</label>
                    <input
                        className="prop-input"
                        type="number"
                        min={0}
                        value={timeoutSeconds}
                        onChange={e => onChange('timeoutSeconds', parseInt(e.target.value, 10) || 0)}
                    />
                    <span className="prop-hint">0 — ожидать без ограничений.</span>
                </div>
            )}
        </div>
    );
}
