import { useState, useEffect } from 'react';
import {
    getOutgoingFlows,
    getConditionExpression,
    setConditionExpression,
    getDefaultFlow,
    setDefaultFlow,
} from './bpmnModdleUtils';

type BpmnModeler = import('bpmn-js/lib/Modeler').default;

interface FlowRow {
    id: string;
    name: string;
    condition: string;
}

interface Props {
    element: { businessObject: Record<string, unknown> };
    modeler: BpmnModeler;
}

/** Вкладка «Выполнение» для шлюзов XOR/OR: условия переходов + default flow. */
export function GatewayTab({ element, modeler }: Props) {
    const bo = element.businessObject as Parameters<typeof getOutgoingFlows>[0];
    const [flows, setFlows] = useState<FlowRow[]>([]);
    const [defaultFlowId, setDefaultFlowId] = useState<string>('');

    // При смене элемента — перечитываем потоки
    useEffect(() => {
        const outgoing = getOutgoingFlows(bo);
        setFlows(outgoing.map(f => ({
            id: f.id as string,
            name: (f.name as string | undefined) ?? '',
            condition: getConditionExpression(f),
        })));
        setDefaultFlowId(getDefaultFlow(bo));
    }, [bo]);

    const handleConditionBlur = (flowId: string, value: string) => {
        const elementRegistry = modeler.get<{ get: (id: string) => { businessObject: Record<string, unknown> } | undefined }>('elementRegistry');
        const flowEl = elementRegistry.get(flowId);
        if (flowEl) {
            setConditionExpression(modeler, flowEl.businessObject as Parameters<typeof setConditionExpression>[1], value);
        }
    };

    const handleDefaultChange = (flowId: string) => {
        setDefaultFlowId(flowId);
        setDefaultFlow(modeler, bo, flowId || null);
    };

    if (flows.length === 0) {
        return (
            <div className="bpp-empty" style={{ height: 'auto', padding: 16 }}>
                <span>Нет исходящих потоков у этого шлюза.</span>
                <span>Добавьте переходы на диаграмме.</span>
            </div>
        );
    }

    return (
        <div>
            <div className="bpp-group">
                <div className="bpp-group-title">Условия переходов</div>
                <p className="bpp-hint" style={{ marginBottom: 8 }}>
                    Для XOR-шлюза ровно один переход должен срабатывать. Используйте EL-выражения:
                    <code> {`\${amount > 1000}`}</code>
                </p>
                {flows.map(flow => (
                    <div key={flow.id} className="bpp-field">
                        <label className="bpp-label">
                            {flow.name || flow.id}
                            {flow.id === defaultFlowId && (
                                <span className="bpp-badge bpp-badge-green" style={{ marginLeft: 6 }}>default</span>
                            )}
                        </label>
                        <input
                            className="bpp-input"
                            defaultValue={flow.condition}
                            onBlur={e => handleConditionBlur(flow.id, e.target.value)}
                            placeholder="${variable == 'value'}"
                        />
                    </div>
                ))}
            </div>

            <div className="bpp-group">
                <div className="bpp-group-title">Переход по умолчанию</div>
                <p className="bpp-hint" style={{ marginBottom: 8 }}>
                    Активируется, если ни одно из условий не выполнено.
                </p>
                <div className="bpp-field">
                    <select
                        className="bpp-select"
                        value={defaultFlowId}
                        onChange={e => handleDefaultChange(e.target.value)}
                    >
                        <option value="">— не задан —</option>
                        {flows.map(f => (
                            <option key={f.id} value={f.id}>{f.name || f.id}</option>
                        ))}
                    </select>
                </div>
            </div>
        </div>
    );
}
