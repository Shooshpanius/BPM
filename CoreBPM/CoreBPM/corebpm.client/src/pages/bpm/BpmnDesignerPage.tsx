import { useEffect, useRef, useState, useCallback } from 'react';
import BpmnModeler from 'bpmn-js/lib/Modeler';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/bpmApi';
import type { BpmProcessDto } from '../../api/bpmApi';
import { BpmPropertiesPanel } from '../../components/bpm/BpmPropertiesPanel';
import './BpmnDesignerPage.css';

// XML пустой диаграммы BPMN 2.0 по умолчанию
const EMPTY_DIAGRAM = `<?xml version="1.0" encoding="UTF-8"?>
<bpmn2:definitions
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xmlns:bpmn2="http://www.omg.org/spec/BPMN/20100524/MODEL"
  xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"
  xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"
  xmlns:di="http://www.omg.org/spec/DD/20100524/DI"
  id="sample-diagram"
  targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn2:process id="Process_1" isExecutable="false">
    <bpmn2:startEvent id="StartEvent_1" name="Начало"/>
  </bpmn2:process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_1">
    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="Process_1">
      <bpmndi:BPMNShape id="_BPMNShape_StartEvent_2" bpmnElement="StartEvent_1">
        <dc:Bounds x="156" y="182" width="36" height="36" />
        <bpmndi:BPMNLabel>
          <dc:Bounds x="150" y="225" width="48" height="14" />
        </bpmndi:BPMNLabel>
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn2:definitions>`;

type SaveStatus = 'saved' | 'unsaved' | 'saving' | 'error';

interface BpmnDesignerPageProps {
    processId: string;
    onBack: () => void;
}

/** Страница BPMN-дизайнера: холст bpmn-js + тулбар + автосохранение. */
export function BpmnDesignerPage({ processId, onBack }: BpmnDesignerPageProps) {
    const { accessToken: token } = useAuth();

    const containerRef = useRef<HTMLDivElement>(null);
    const modelerRef = useRef<InstanceType<typeof BpmnModeler> | null>(null);
    const autoSaveTimer = useRef<number | null>(null);

    const [process, setProcess] = useState<BpmProcessDto | null>(null);
    const [saveStatus, setSaveStatus] = useState<SaveStatus>('saved');
    const [saveError, setSaveError] = useState<string | null>(null);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [modelerReady, setModelerReady] = useState(false);

    // ─── Сохранение диаграммы ───

    const saveDiagram = useCallback(async () => {
        if (!token || !modelerRef.current) return;
        setSaveStatus('saving');
        setSaveError(null);
        try {
            const { xml } = await modelerRef.current.saveXML({ format: true });
            await api.saveDiagram(token, processId, xml);
            setSaveStatus('saved');
        } catch (e) {
            setSaveStatus('error');
            setSaveError(e instanceof Error ? e.message : 'Ошибка сохранения');
        }
    }, [token, processId]);

    // ─── Ref для saveDiagram, чтобы замыкание в onChanged всегда вызывало актуальную версию ───

    const saveDiagramRef = useRef(saveDiagram);
    useEffect(() => { saveDiagramRef.current = saveDiagram; }, [saveDiagram]);

    // ─── Инициализация модельера bpmn-js ───

    useEffect(() => {
        if (!containerRef.current || !token) return;

        const modeler = new BpmnModeler({
            container: containerRef.current,
            keyboard: { bindTo: document },
        });
        modelerRef.current = modeler;

        // При любом изменении — помечаем как «не сохранено» и ставим автосохранение
        const onChanged = () => {
            setSaveStatus('unsaved');
            if (autoSaveTimer.current) clearTimeout(autoSaveTimer.current);
            autoSaveTimer.current = window.setTimeout(() => {
                saveDiagramRef.current();
            }, 30000); // автосохранение через 30 с
        };

        modeler.on('commandStack.changed', onChanged);

        // Загружаем диаграмму
        const loadDiagram = async () => {
            try {
                const proc = await api.getProcess(token, processId);
                setProcess(proc);

                const diagramData = await api.getDiagram(token, processId);
                const xml = diagramData.diagramXml ?? EMPTY_DIAGRAM;
                await modeler.importXML(xml);

                // Масштабируем по содержимому
                const canvas = modeler.get<{ zoom: (val: string) => void }>('canvas');
                canvas.zoom('fit-viewport');

                setSaveStatus('saved');
                setModelerReady(true);
            } catch (e) {
                setLoadError(e instanceof Error ? e.message : 'Ошибка загрузки диаграммы');
            }
        };

        loadDiagram();

        // Предупреждение при уходе со страницы с несохранёнными изменениями
        const beforeUnload = (e: BeforeUnloadEvent) => {
            if (saveStatus === 'unsaved') {
                e.preventDefault();
                e.returnValue = '';
            }
        };
        window.addEventListener('beforeunload', beforeUnload);

        return () => {
            if (autoSaveTimer.current) clearTimeout(autoSaveTimer.current);
            modeler.off('commandStack.changed', onChanged);
            window.removeEventListener('beforeunload', beforeUnload);
            modeler.destroy();
            modelerRef.current = null;
            setModelerReady(false);
        };
    }, [token, processId]);

    // ─── Операции тулбара ───

    const handleZoomIn = () => {
        const canvas = modelerRef.current?.get<{ zoom: (v: number, c?: string) => void }>('canvas');
        if (canvas) {
            const zoomScroll = modelerRef.current?.get<{ stepZoom: (v: number) => void }>('zoomScroll');
            zoomScroll?.stepZoom(1);
        }
    };

    const handleZoomOut = () => {
        const zoomScroll = modelerRef.current?.get<{ stepZoom: (v: number) => void }>('zoomScroll');
        zoomScroll?.stepZoom(-1);
    };

    const handleFit = () => {
        const canvas = modelerRef.current?.get<{ zoom: (v: string) => void }>('canvas');
        canvas?.zoom('fit-viewport');
    };

    const handleUndo = () => {
        const commandStack = modelerRef.current?.get<{ undo: () => void }>('commandStack');
        commandStack?.undo();
    };

    const handleRedo = () => {
        const commandStack = modelerRef.current?.get<{ redo: () => void }>('commandStack');
        commandStack?.redo();
    };

    const handleSave = () => { saveDiagram(); };

    // ─── Индикатор статуса сохранения ───

    const statusLabel: Record<SaveStatus, string> = {
        saved: 'Сохранено',
        unsaved: 'Не сохранено',
        saving: 'Сохранение...',
        error: 'Ошибка сохранения',
    };

    const handleBackClick = () => {
        if (saveStatus === 'unsaved') {
            if (!window.confirm('Есть несохранённые изменения. Покинуть страницу?')) return;
        }
        onBack();
    };

    return (
        <div className="bpd-root">
            {/* Тулбар */}
            <div className="bpd-toolbar" role="toolbar" aria-label="Панель инструментов дизайнера">
                <button className="bpd-back-btn" onClick={handleBackClick} title="Назад к списку процессов" aria-label="Назад">
                    ← Процессы
                </button>
                <div className="bpd-toolbar-sep" role="separator" />
                <span className="bpd-process-name">{process?.name ?? '...'}</span>
                <div className="bpd-toolbar-spacer" />
                <div className="bpd-toolbar-group" role="group" aria-label="История изменений">
                    <button className="bpd-tool-btn" onClick={handleUndo} title="Отменить (Ctrl+Z)" aria-label="Отменить">
                        <UndoIcon />
                    </button>
                    <button className="bpd-tool-btn" onClick={handleRedo} title="Повторить (Ctrl+Y)" aria-label="Повторить">
                        <RedoIcon />
                    </button>
                </div>
                <div className="bpd-toolbar-sep" role="separator" />
                <div className="bpd-toolbar-group" role="group" aria-label="Масштаб">
                    <button className="bpd-tool-btn" onClick={handleZoomOut} title="Уменьшить (Ctrl+−)" aria-label="Уменьшить">
                        <ZoomOutIcon />
                    </button>
                    <button className="bpd-tool-btn" onClick={handleFit} title="По размеру (Ctrl+Shift+H)" aria-label="По размеру">
                        <FitIcon />
                    </button>
                    <button className="bpd-tool-btn" onClick={handleZoomIn} title="Увеличить (Ctrl+=)" aria-label="Увеличить">
                        <ZoomInIcon />
                    </button>
                </div>
                <div className="bpd-toolbar-sep" role="separator" />
                <button
                    className={`bpd-save-btn bpd-save-btn--${saveStatus}`}
                    onClick={handleSave}
                    disabled={saveStatus === 'saving'}
                    title="Сохранить диаграмму"
                >
                    {saveStatus === 'saving' ? '↻ Сохранение...' : '↓ Сохранить'}
                </button>
                <div className={`bpd-status bpd-status--${saveStatus}`} aria-live="polite">
                    {statusLabel[saveStatus]}
                </div>
            </div>

            {/* Ошибка загрузки */}
            {loadError && (
                <div className="bpd-load-error">
                    Не удалось загрузить диаграмму: {loadError}
                </div>
            )}

            {/* Ошибка сохранения */}
            {saveError && (
                <div className="bpd-save-error" aria-live="assertive">
                    {saveError}
                </div>
            )}

            {/* Основная область */}
            <div className="bpd-workspace">
                {/* Холст bpmn-js */}
                <div ref={containerRef} className="bpd-canvas" />
                {/* Кастомная панель свойств */}
                <aside className="bpd-properties-panel" aria-label="Панель свойств элемента">
                    {modelerReady && modelerRef.current && token && (
                        <BpmPropertiesPanel
                            modeler={modelerRef.current}
                            processId={processId}
                            token={token}
                        />
                    )}
                </aside>
            </div>
        </div>
    );
}

// ─── Иконки тулбара ───

function UndoIcon() {
    return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M3 7v6h6"/><path d="M3 13A9 9 0 1 0 5.7 6.7L3 13"/>
        </svg>
    );
}

function RedoIcon() {
    return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M21 7v6h-6"/><path d="M21 13A9 9 0 1 1 18.3 6.7L21 13"/>
        </svg>
    );
}

function ZoomInIcon() {
    return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35M11 8v6M8 11h6"/>
        </svg>
    );
}

function ZoomOutIcon() {
    return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35M8 11h6"/>
        </svg>
    );
}

function FitIcon() {
    return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M3 8V5a2 2 0 0 1 2-2h3"/><path d="M16 3h3a2 2 0 0 1 2 2v3"/>
            <path d="M21 16v3a2 2 0 0 1-2 2h-3"/><path d="M8 21H5a2 2 0 0 1-2-2v-3"/>
        </svg>
    );
}
