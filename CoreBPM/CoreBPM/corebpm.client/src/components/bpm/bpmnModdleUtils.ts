/**
 * Утилиты для работы с bpmn-js moddle API:
 * чтение и запись стандартных BPMN-свойств элементов (name, documentation,
 * conditionExpression, timerEventDefinition и т.п.).
 */

type BpmnModeler = import('bpmn-js/lib/Modeler').default;

// ─── Типы элементов ──────────────────────────────────────────────────────────

/** Тип BPMN-элемента по его полю $type */
export type BpmnElementType =
    | 'bpmn:UserTask'
    | 'bpmn:ServiceTask'
    | 'bpmn:ScriptTask'
    | 'bpmn:ManualTask'
    | 'bpmn:BusinessRuleTask'
    | 'bpmn:SendTask'
    | 'bpmn:ReceiveTask'
    | 'bpmn:ExclusiveGateway'
    | 'bpmn:InclusiveGateway'
    | 'bpmn:ParallelGateway'
    | 'bpmn:EventBasedGateway'
    | 'bpmn:StartEvent'
    | 'bpmn:EndEvent'
    | 'bpmn:IntermediateCatchEvent'
    | 'bpmn:IntermediateThrowEvent'
    | 'bpmn:BoundaryEvent'
    | 'bpmn:SequenceFlow'
    | 'bpmn:Process'
    | 'bpmn:SubProcess'
    | string;

/** Человекочитаемое название типа элемента */
export function getElementTypeName(type: BpmnElementType): string {
    const names: Partial<Record<BpmnElementType, string>> = {
        'bpmn:UserTask': 'Пользовательская задача',
        'bpmn:ServiceTask': 'Сервисная задача',
        'bpmn:ScriptTask': 'Задача-скрипт',
        'bpmn:ManualTask': 'Ручная задача',
        'bpmn:BusinessRuleTask': 'Бизнес-правило',
        'bpmn:SendTask': 'Отправка сообщения',
        'bpmn:ReceiveTask': 'Ожидание сообщения',
        'bpmn:ExclusiveGateway': 'Шлюз XOR',
        'bpmn:InclusiveGateway': 'Шлюз OR',
        'bpmn:ParallelGateway': 'Шлюз AND',
        'bpmn:EventBasedGateway': 'Событийный шлюз',
        'bpmn:StartEvent': 'Стартовое событие',
        'bpmn:EndEvent': 'Конечное событие',
        'bpmn:IntermediateCatchEvent': 'Промежуточное событие (прием)',
        'bpmn:IntermediateThrowEvent': 'Промежуточное событие (отправка)',
        'bpmn:BoundaryEvent': 'Граничное событие',
        'bpmn:SequenceFlow': 'Переход',
        'bpmn:Process': 'Процесс',
        'bpmn:SubProcess': 'Подпроцесс',
    };
    return names[type] ?? type.replace('bpmn:', '');
}

// ─── Чтение свойств через moddle ─────────────────────────────────────────────

/** Возвращает текстовое содержимое documentation[0] элемента */
export function getDocumentation(element: ModdleElement): string {
    const docs = element.documentation as ModdleElement[] | undefined;
    return (docs?.[0]?.text as string | undefined) ?? '';
}

/** Устанавливает documentation[0] элемента */
export function setDocumentation(modeler: BpmnModeler, element: ModdleElement, text: string): void {
    const modeling = modeler.get<Modeling>('modeling');
    const moddle = modeler.get<Moddle>('moddle');
    const doc = moddle.create('bpmn:Documentation', { text });
    modeling.updateModdleProperties(element, element, {
        documentation: text ? [doc] : [],
    });
}

/** Возвращает имя элемента */
export function getName(element: ModdleElement): string {
    return (element.name as string | undefined) ?? '';
}

/** Устанавливает имя элемента */
export function setName(modeler: BpmnModeler, element: ModdleElement, name: string): void {
    const modeling = modeler.get<Modeling>('modeling');
    modeling.updateLabel(element, name);
}

/** Возвращает conditionExpression текущего SequenceFlow */
export function getConditionExpression(element: ModdleElement): string {
    const cond = element.conditionExpression as ModdleElement | undefined;
    return (cond?.body as string | undefined) ?? '';
}

/** Устанавливает conditionExpression на SequenceFlow */
export function setConditionExpression(modeler: BpmnModeler, element: ModdleElement, expression: string): void {
    const modeling = modeler.get<Modeling>('modeling');
    const moddle = modeler.get<Moddle>('moddle');
    if (expression.trim()) {
        const cond = moddle.create('bpmn:FormalExpression', { body: expression });
        modeling.updateModdleProperties(element, element, { conditionExpression: cond });
    } else {
        modeling.updateModdleProperties(element, element, { conditionExpression: undefined });
    }
}

/** Возвращает default-поток шлюза */
export function getDefaultFlow(element: ModdleElement): string {
    const def = element.default as ModdleElement | undefined;
    return (def?.id as string | undefined) ?? '';
}

/** Устанавливает default-поток шлюза по ID потока */
export function setDefaultFlow(modeler: BpmnModeler, element: ModdleElement, flowId: string | null): void {
    const modeling = modeler.get<Modeling>('modeling');
    const elementRegistry = modeler.get<ElementRegistry>('elementRegistry');
    const flow = flowId ? elementRegistry.get(flowId)?.businessObject ?? null : null;
    modeling.updateModdleProperties(element, element, { default: flow });
}

/** Возвращает исходящие SequenceFlow шлюза (business objects) */
export function getOutgoingFlows(element: ModdleElement): ModdleElement[] {
    return (element.outgoing as ModdleElement[] | undefined) ?? [];
}

// ─── Timer definitions ───────────────────────────────────────────────────────

export type TimerType = 'timeDate' | 'timeDuration' | 'timeCycle' | '';

export interface TimerDefinition {
    type: TimerType;
    value: string;
}

/** Читает определение таймера из StartEvent/IntermediateCatchEvent/BoundaryEvent */
export function getTimerDefinition(element: ModdleElement): TimerDefinition {
    const eventDefs = element.eventDefinitions as ModdleElement[] | undefined;
    const timerDef = eventDefs?.find(d => d.$type === 'bpmn:TimerEventDefinition');
    if (!timerDef) return { type: '', value: '' };

    for (const prop of ['timeDate', 'timeDuration', 'timeCycle'] as TimerType[]) {
        const expr = timerDef[prop] as ModdleElement | undefined;
        if (expr) return { type: prop, value: (expr.body as string | undefined) ?? '' };
    }
    return { type: '', value: '' };
}

/** Записывает определение таймера */
export function setTimerDefinition(modeler: BpmnModeler, element: ModdleElement, def: TimerDefinition): void {
    const modeling = modeler.get<Modeling>('modeling');
    const moddle = modeler.get<Moddle>('moddle');
    const eventDefs = (element.eventDefinitions as ModdleElement[] | undefined) ?? [];
    const timerDef = eventDefs.find(d => d.$type === 'bpmn:TimerEventDefinition');
    if (!timerDef) return;

    const expr = def.value ? moddle.create('bpmn:FormalExpression', { body: def.value }) : undefined;
    const update: Record<string, unknown> = {
        timeDate: undefined,
        timeDuration: undefined,
        timeCycle: undefined,
    };
    if (def.type && expr) update[def.type] = expr;

    modeling.updateModdleProperties(element, timerDef, update);
}

// ─── Типы для bpmn-js API ────────────────────────────────────────────────────

// Минимальные определения типов для bpmn-js API

interface ModdleElement {
    $type: string;
    id?: string;
    name?: string;
    documentation?: ModdleElement[];
    conditionExpression?: ModdleElement;
    default?: ModdleElement;
    outgoing?: ModdleElement[];
    incoming?: ModdleElement[];
    eventDefinitions?: ModdleElement[];
    [key: string]: unknown;
}

interface Modeling {
    updateLabel(element: unknown, name: string): void;
    updateModdleProperties(element: unknown, target: unknown, props: Record<string, unknown>): void;
}

interface Moddle {
    create(type: string, props?: Record<string, unknown>): ModdleElement;
}

interface ElementRegistry {
    get(id: string): { businessObject: ModdleElement } | undefined;
}
