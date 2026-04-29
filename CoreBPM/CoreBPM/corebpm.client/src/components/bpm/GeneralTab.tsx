import { useState, useEffect } from 'react';
import { getName, setName, getDocumentation, setDocumentation } from './bpmnModdleUtils';

type BpmnModeler = import('bpmn-js/lib/Modeler').default;

interface Props {
    element: { businessObject: Record<string, unknown> };
    modeler: BpmnModeler;
}

/** Вкладка «Основное»: редактирование имени и документации элемента. */
export function GeneralTab({ element, modeler }: Props) {
    const bo = element.businessObject as Parameters<typeof getName>[0];

    const [nameVal, setNameVal] = useState(() => getName(bo));
    const [docVal, setDocVal] = useState(() => getDocumentation(bo));

    // Обновляем локальное состояние при смене выбранного элемента
    useEffect(() => {
        setNameVal(getName(bo));
        setDocVal(getDocumentation(bo));
    }, [bo]);

    const handleNameBlur = () => {
        const current = getName(bo);
        if (nameVal !== current) {
            setName(modeler, bo, nameVal);
        }
    };

    const handleDocBlur = () => {
        const current = getDocumentation(bo);
        if (docVal !== current) {
            setDocumentation(modeler, bo, docVal);
        }
    };

    return (
        <div>
            <div className="bpp-field">
                <label className="bpp-label">Название</label>
                <input
                    className="bpp-input"
                    value={nameVal}
                    onChange={e => setNameVal(e.target.value)}
                    onBlur={handleNameBlur}
                    placeholder="Введите название элемента"
                />
            </div>
            <div className="bpp-field">
                <label className="bpp-label">Описание (Documentation)</label>
                <textarea
                    className="bpp-textarea"
                    value={docVal}
                    onChange={e => setDocVal(e.target.value)}
                    onBlur={handleDocBlur}
                    placeholder="Описание назначения этого элемента (BPMN documentation)"
                    rows={4}
                />
                <p className="bpp-hint">Используется при генерации PDF-регламента процесса.</p>
            </div>
        </div>
    );
}
