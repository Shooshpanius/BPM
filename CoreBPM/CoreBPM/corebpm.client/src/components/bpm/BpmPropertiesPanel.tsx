import { useState, useEffect, useCallback } from 'react';
import { getElementTypeName } from './bpmnModdleUtils';
import { GeneralTab } from './GeneralTab';
import { UserTaskTab } from './UserTaskTab';
import { ServiceTaskTab } from './ServiceTaskTab';
import { ScriptTaskTab } from './ScriptTaskTab';
import { GatewayTab } from './GatewayTab';
import { TimerEventTab } from './TimerEventTab';
import { SequenceFlowTab } from './SequenceFlowTab';
import { NotificationsTab } from './NotificationsTab';
import { ProcessVariablesTab } from './ProcessVariablesTab';
import { RaciMatrixTab } from './RaciMatrixTab';
import { ProcessSettingsTab } from './ProcessSettingsTab';
import './BpmPropertiesPanel.css';

type BpmnModeler = import('bpmn-js/lib/Modeler').default;

// ─── Типы ────────────────────────────────────────────────────────────────────

interface BpmnShape {
    businessObject: Record<string, unknown>;
    type: string;
    id: string;
}

interface Props {
    /** Инстанс bpmn-js Modeler */
    modeler: BpmnModeler;
    /** ID текущего процесса (из БД) */
    processId: string;
    /** JWT-токен для API-вызовов */
    token: string;
}

// ─── Конфигурация вкладок ─────────────────────────────────────────────────────

type TabId = 'general' | 'execution' | 'notifications' | 'variables' | 'raci' | 'settings';

interface TabDef {
    id: TabId;
    label: string;
}

/** Определяет набор вкладок для данного типа элемента */
function getTabsForElement(elementType: string | null): TabDef[] {
    if (elementType === null) {
        // Ничего не выбрано — показываем уровень процесса
        return [
            { id: 'variables', label: 'Переменные' },
            { id: 'raci', label: 'RACI' },
            { id: 'settings', label: 'Настройки' },
        ];
    }
    if (elementType === 'bpmn:SequenceFlow') {
        return [{ id: 'general', label: 'Основное' }];
    }
    if (
        elementType === 'bpmn:ExclusiveGateway' ||
        elementType === 'bpmn:InclusiveGateway'
    ) {
        return [
            { id: 'general', label: 'Основное' },
            { id: 'execution', label: 'Выполнение' },
        ];
    }
    if (elementType === 'bpmn:UserTask') {
        return [
            { id: 'general', label: 'Основное' },
            { id: 'execution', label: 'Выполнение' },
            { id: 'notifications', label: 'Уведомления' },
        ];
    }
    if (
        elementType === 'bpmn:ServiceTask' ||
        elementType === 'bpmn:ScriptTask' ||
        elementType === 'bpmn:BusinessRuleTask' ||
        elementType === 'bpmn:SendTask' ||
        elementType === 'bpmn:ReceiveTask'
    ) {
        return [
            { id: 'general', label: 'Основное' },
            { id: 'execution', label: 'Выполнение' },
        ];
    }
    if (
        elementType === 'bpmn:StartEvent' ||
        elementType === 'bpmn:IntermediateCatchEvent' ||
        elementType === 'bpmn:BoundaryEvent'
    ) {
        return [
            { id: 'general', label: 'Основное' },
            { id: 'execution', label: 'Выполнение' },
        ];
    }
    // Прочие элементы: только «Основное»
    return [{ id: 'general', label: 'Основное' }];
}

/** Проверяет, является ли событие таймерным */
function isTimerEvent(_elementType: string, businessObject: Record<string, unknown>): boolean {
    const eventDefs = businessObject.eventDefinitions as Array<{ $type: string }> | undefined;
    return Boolean(eventDefs?.some(d => d.$type === 'bpmn:TimerEventDefinition'));
}

// ─── Компонент ───────────────────────────────────────────────────────────────

/**
 * BpmPropertiesPanel — кастомная React-панель свойств для BPMN-дизайнера.
 * Монтируется рядом с bpmn-js холстом и заменяет стандартный bpmn-js-properties-panel.
 */
export function BpmPropertiesPanel({ modeler, processId, token }: Props) {
    const [selectedElement, setSelectedElement] = useState<BpmnShape | null>(null);
    const [activeTab, setActiveTab] = useState<TabId>('variables');

    // Подписываемся на события выбора элемента
    useEffect(() => {
        const handleSelectionChanged = (e: unknown) => {
            const event = e as { newSelection: BpmnShape[] };
            const element = event.newSelection[0] ?? null;
            setSelectedElement(element);

            // Выбираем первую вкладку нового элемента
            const tabs = getTabsForElement(element?.type ?? null);
            setActiveTab(tabs[0]?.id ?? 'general');
        };

        modeler.on('selection.changed', handleSelectionChanged);
        return () => { modeler.off('selection.changed', handleSelectionChanged); };
    }, [modeler]);

    const tabs = getTabsForElement(selectedElement?.type ?? null);

    // Убеждаемся, что activeTab существует в текущем наборе вкладок
    const effectiveTab = tabs.find(t => t.id === activeTab)?.id ?? tabs[0]?.id ?? 'general';

    const renderTabContent = useCallback(() => {
        if (!selectedElement && effectiveTab !== 'variables' && effectiveTab !== 'raci') {
            return renderProcessLevel(effectiveTab, processId, token);
        }
        if (!selectedElement) {
            return renderProcessLevel(effectiveTab, processId, token);
        }

        const bo = selectedElement.businessObject;
        const elType = selectedElement.type;
        const elId = (bo.id as string | undefined) ?? selectedElement.id;

        switch (effectiveTab) {
            case 'general':
                if (elType === 'bpmn:SequenceFlow') {
                    return <SequenceFlowTab element={selectedElement} modeler={modeler} />;
                }
                return <GeneralTab element={selectedElement} modeler={modeler} />;

            case 'execution':
                if (elType === 'bpmn:UserTask') {
                    return <UserTaskTab processId={processId} token={token} elementId={elId} />;
                }
                if (elType === 'bpmn:ServiceTask' || elType === 'bpmn:SendTask' || elType === 'bpmn:ReceiveTask') {
                    return <ServiceTaskTab processId={processId} token={token} elementId={elId} />;
                }
                if (elType === 'bpmn:ScriptTask') {
                    return <ScriptTaskTab processId={processId} token={token} elementId={elId} />;
                }
                if (elType === 'bpmn:ExclusiveGateway' || elType === 'bpmn:InclusiveGateway') {
                    return <GatewayTab element={selectedElement} modeler={modeler} />;
                }
                if (isTimerEvent(elType, bo)) {
                    return <TimerEventTab element={selectedElement} modeler={modeler} />;
                }
                return <GeneralTab element={selectedElement} modeler={modeler} />;

            case 'notifications':
                return <NotificationsTab processId={processId} token={token} elementId={elId} />;

            case 'variables':
                return <ProcessVariablesTab processId={processId} token={token} />;

            case 'raci':
                return <RaciMatrixTab processId={processId} token={token} />;

            case 'settings':
                return <ProcessSettingsTab processId={processId} token={token} />;

            default:
                return null;
        }
    }, [selectedElement, effectiveTab, processId, token, modeler]);

    return (
        <div className="bpp-root">
            {/* Заголовок */}
            <div className="bpp-header">
                <span className="bpp-header-type">
                    {selectedElement
                        ? getElementTypeName(selectedElement.type)
                        : 'Процесс'}
                </span>
                {selectedElement && (
                    <span style={{ fontSize: 10, color: '#9ca3af', fontFamily: 'monospace' }}>
                        {(selectedElement.businessObject.id as string | undefined) ?? selectedElement.id}
                    </span>
                )}
            </div>

            {/* Вкладки */}
            {tabs.length > 1 && (
                <div className="bpp-tabs" role="tablist" aria-label="Вкладки свойств">
                    {tabs.map(tab => (
                        <button
                            key={tab.id}
                            className={`bpp-tab${effectiveTab === tab.id ? ' active' : ''}`}
                            role="tab"
                            aria-selected={effectiveTab === tab.id}
                            onClick={() => setActiveTab(tab.id)}
                        >
                            {tab.label}
                        </button>
                    ))}
                </div>
            )}

            {/* Содержимое */}
            <div className="bpp-content" role="tabpanel">
                {renderTabContent()}
            </div>
        </div>
    );
}

// ─── Рендеринг уровня процесса (нет выбранного элемента) ─────────────────────

function renderProcessLevel(tabId: TabId, processId: string, token: string) {
    if (tabId === 'variables') {
        return <ProcessVariablesTab processId={processId} token={token} />;
    }
    if (tabId === 'raci') {
        return <RaciMatrixTab processId={processId} token={token} />;
    }
    if (tabId === 'settings') {
        return <ProcessSettingsTab processId={processId} token={token} />;
    }
    return (
        <div className="bpp-empty">
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                <circle cx="12" cy="12" r="9" />
                <path d="M12 8v4M12 16h.01" />
            </svg>
            <span>Выберите элемент на диаграмме</span>
            <span style={{ fontSize: 11 }}>для просмотра и редактирования его свойств</span>
        </div>
    );
}
