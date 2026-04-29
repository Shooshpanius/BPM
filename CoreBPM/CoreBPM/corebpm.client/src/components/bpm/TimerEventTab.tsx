import { useState, useEffect } from 'react';
import { getTimerDefinition, setTimerDefinition } from './bpmnModdleUtils';
import type { TimerType } from './bpmnModdleUtils';

type BpmnModeler = import('bpmn-js/lib/Modeler').default;

interface Props {
    element: { businessObject: Record<string, unknown> };
    modeler: BpmnModeler;
}

const TIMER_TYPE_LABELS: Record<TimerType, string> = {
    timeDate: 'Конкретная дата (timeDate)',
    timeDuration: 'Длительность (timeDuration)',
    timeCycle: 'Цикл (timeCycle)',
    '': '— не задан —',
};

const PLACEHOLDERS: Record<TimerType, string> = {
    timeDate: '2025-12-31T18:00:00Z или ${variables.startDate}',
    timeDuration: 'PT1H30M или P2D',
    timeCycle: 'R3/PT1H или 0 0 * * MON-FRI',
    '': '',
};

/** Вкладка «Выполнение» для Timer Event. */
export function TimerEventTab({ element, modeler }: Props) {
    const bo = element.businessObject as Parameters<typeof getTimerDefinition>[0];
    const [timerType, setTimerType] = useState<TimerType>('');
    const [timerValue, setTimerValue] = useState('');

    useEffect(() => {
        const def = getTimerDefinition(bo);
        setTimerType(def.type);
        setTimerValue(def.value);
    }, [bo]);

    const handleTypeChange = (type: TimerType) => {
        setTimerType(type);
        setTimerDefinition(modeler, bo, { type, value: timerValue });
    };

    const handleValueBlur = (value: string) => {
        setTimerDefinition(modeler, bo, { type: timerType, value });
    };

    return (
        <div>
            <div className="bpp-group">
                <div className="bpp-group-title">Тип таймера</div>
                <div className="bpp-field">
                    <select
                        className="bpp-select"
                        value={timerType}
                        onChange={e => handleTypeChange(e.target.value as TimerType)}
                    >
                        {(['', 'timeDate', 'timeDuration', 'timeCycle'] as TimerType[]).map(t => (
                            <option key={t} value={t}>{TIMER_TYPE_LABELS[t]}</option>
                        ))}
                    </select>
                </div>

                {timerType && (
                    <div className="bpp-field">
                        <label className="bpp-label">Значение</label>
                        <input
                            className="bpp-input"
                            defaultValue={timerValue}
                            onBlur={e => handleValueBlur(e.target.value)}
                            onChange={e => setTimerValue(e.target.value)}
                            placeholder={PLACEHOLDERS[timerType]}
                        />
                        <p className="bpp-hint">
                            {timerType === 'timeDate' && 'ISO 8601 дата/время. Поддерживаются переменные: ${variables.date}'}
                            {timerType === 'timeDuration' && 'ISO 8601 длительность: PT1H = 1 час, P2D = 2 дня, P1DT4H = 1 день 4 часа'}
                            {timerType === 'timeCycle' && 'ISO 8601 цикл (R3/PT1H = 3 раза через час) или cron (0 8 * * MON = каждый понедельник в 8:00)'}
                        </p>
                    </div>
                )}
            </div>
        </div>
    );
}
