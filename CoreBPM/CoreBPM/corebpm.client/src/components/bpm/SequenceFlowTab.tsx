import { useState, useEffect } from 'react';
import { getConditionExpression, setConditionExpression } from './bpmnModdleUtils';

type BpmnModeler = import('bpmn-js/lib/Modeler').default;

interface Props {
    element: { businessObject: Record<string, unknown> };
    modeler: BpmnModeler;
}

/** Вкладка «Основное» для SequenceFlow: отображение и редактирование conditionExpression. */
export function SequenceFlowTab({ element, modeler }: Props) {
    const bo = element.businessObject as Parameters<typeof getConditionExpression>[0];
    const [condition, setCondition] = useState(() => getConditionExpression(bo));

    useEffect(() => {
        setCondition(getConditionExpression(bo));
    }, [bo]);

    const handleBlur = (value: string) => {
        setConditionExpression(modeler, bo, value);
    };

    return (
        <div>
            <div className="bpp-group">
                <div className="bpp-group-title">Условие перехода</div>
                <div className="bpp-field">
                    <label className="bpp-label">EL-выражение</label>
                    <input
                        className="bpp-input"
                        value={condition}
                        onChange={e => setCondition(e.target.value)}
                        onBlur={e => handleBlur(e.target.value)}
                        placeholder="${amount > 1000 && status == 'approved'}"
                    />
                    <p className="bpp-hint">
                        Выражение вычисляется в контексте переменных процесса.
                        Оставьте пустым для безусловного перехода.
                    </p>
                </div>
            </div>
        </div>
    );
}
