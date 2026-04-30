/// <reference types="vite/client" />

// Объявления типов для пакетов без официальных @types

declare module 'diagram-js-minimap' {
    /** Плагин миникарты для diagram-js / bpmn-js */
    const minimapModule: object;
    export default minimapModule;
}

declare module 'bpmn-auto-layout' {
    /**
     * Автоматическая раскладка BPMN-диаграммы.
     * @param xml BPMN XML строка
     * @returns Promise с расставленным BPMN XML
     */
    export function layoutProcess(xml: string): Promise<string>;
}
