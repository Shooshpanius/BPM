// Минимальные ambient-декларации для bpmn-js (официальные @types отсутствуют)
declare module 'bpmn-js/lib/Modeler' {
    interface ImportXMLResult {
        warnings: string[];
    }
    interface SaveXMLResult {
        xml: string;
    }
    interface SaveSVGResult {
        svg: string;
    }
    interface BpmnModelerOptions {
        container: HTMLElement;
        additionalModules?: unknown[];
        propertiesPanel?: { parent: HTMLElement };
        keyboard?: { bindTo: HTMLElement | Document };
    }
    class BpmnModeler {
        constructor(options: BpmnModelerOptions);
        importXML(xml: string): Promise<ImportXMLResult>;
        saveXML(options?: { format?: boolean }): Promise<SaveXMLResult>;
        saveSVG(): Promise<SaveSVGResult>;
        get<T = unknown>(name: string): T;
        on(event: string, callback: (e: unknown) => void): void;
        off(event: string, callback?: (e: unknown) => void): void;
        destroy(): void;
    }
    export default BpmnModeler;
}

declare module 'bpmn-js-properties-panel' {
    const BpmnPropertiesPanelModule: unknown;
    const BpmnPropertiesProviderModule: unknown;
    export { BpmnPropertiesPanelModule, BpmnPropertiesProviderModule };
}

declare module '@bpmn-io/properties-panel/dist/assets/properties-panel.css' {
    const _: string;
    export default _;
}
