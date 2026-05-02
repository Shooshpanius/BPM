import { useCallback, useEffect, useRef, useState } from 'react';
import BpmnModeler from 'bpmn-js/lib/Modeler';
import minimapModule from 'diagram-js-minimap';
import { layoutProcess } from 'bpmn-auto-layout';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/bpmApi';
import type {
    AcquireLockResponse,
    BpmDebugSessionDto,
    BpmDiagramDto,
    BpmProcessDto,
    BpmProcessVersionInfoDto,
    BpmValidationResultDto,
    BpmVersionDiffDto,
    DiagramLockDto,
} from '../../api/bpmApi';
import { BpmPropertiesPanel } from '../../components/bpm/BpmPropertiesPanel';
import './BpmnDesignerPage.css';

const DEFAULT_START_EVENT_X = 156;
const DEFAULT_START_EVENT_Y = 182;
const DEFAULT_START_EVENT_SIZE = 36;

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
        <dc:Bounds x="${DEFAULT_START_EVENT_X}" y="${DEFAULT_START_EVENT_Y}" width="${DEFAULT_START_EVENT_SIZE}" height="${DEFAULT_START_EVENT_SIZE}" />
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn2:definitions>`;

type SaveStatus = 'saved' | 'unsaved' | 'saving' | 'error';

interface BpmnDesignerPageProps {
    processId: string;
    onBack: () => void;
}

const STATUS_LABELS: Record<SaveStatus, string> = {
    saved: 'Сохранено',
    unsaved: 'Не сохранено',
    saving: 'Сохранение...',
    error: 'Ошибка сохранения',
};

const VERSION_STATUS_LABELS: Record<string, string> = {
    Draft: 'Черновик',
    Active: 'Активная',
    Obsolete: 'Устаревшая',
};

export function BpmnDesignerPage({ processId, onBack }: BpmnDesignerPageProps) {
    const { accessToken: token } = useAuth();

    const containerRef = useRef<HTMLDivElement>(null);
    const modelerRef = useRef<InstanceType<typeof BpmnModeler> | null>(null);
    const autoSaveTimer = useRef<number | null>(null);
    const versionMarkerIdsRef = useRef<string[]>([]);
    const debugMarkerRef = useRef<string | null>(null);
    const isReadOnlyRef = useRef(true);

    const [process, setProcess] = useState<BpmProcessDto | null>(null);
    const [versions, setVersions] = useState<BpmProcessVersionInfoDto[]>([]);
    const [currentDiagram, setCurrentDiagram] = useState<BpmDiagramDto | null>(null);
    const [saveStatus, setSaveStatus] = useState<SaveStatus>('saved');
    const [saveError, setSaveError] = useState<string | null>(null);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [modelerReady, setModelerReady] = useState(false);
    const [showVersions, setShowVersions] = useState(false);
    const [diffTargetVersionId, setDiffTargetVersionId] = useState<string>('');
    const [diffResult, setDiffResult] = useState<BpmVersionDiffDto | null>(null);
    const [validationResult, setValidationResult] = useState<BpmValidationResultDto | null>(null);
    const [validating, setValidating] = useState(false);
    const [publishing, setPublishing] = useState(false);
    const [showPublishDialog, setShowPublishDialog] = useState(false);
    const [publishReleaseNotes, setPublishReleaseNotes] = useState('');
    const [showDebug, setShowDebug] = useState(false);
    const [debugSession, setDebugSession] = useState<BpmDebugSessionDto | null>(null);
    const [debugError, setDebugError] = useState<string | null>(null);
    const [busyDocument, setBusyDocument] = useState(false);

    // ─── Экспорт / Импорт ────────────────────────────────────────────────────────
    const [showExportMenu, setShowExportMenu] = useState(false);
    const importFileRef = useRef<HTMLInputElement>(null);

    // ─── Миникарта ───────────────────────────────────────────────────────────
    const [minimapVisible, setMinimapVisible] = useState(true);

    // ─── Поиск по диаграмме ───────────────────────────────────────────────────
    const [showSearch, setShowSearch] = useState(false);
    const [searchQuery, setSearchQuery] = useState('');
    const [searchResults, setSearchResults] = useState<{ id: string; name: string; type: string }[]>([]);
    const [searchIndex, setSearchIndex] = useState(0);
    const searchInputRef = useRef<HTMLInputElement>(null);

    // ─── Авто-раскладка ───────────────────────────────────────────────────────
    const [layouting, setLayouting] = useState(false);

    // ─── Блокировка диаграммы ─────────────────────────────────────────────────
    const [lockInfo, setLockInfo] = useState<DiagramLockDto | null>(null);
    const [lockAcquired, setLockAcquired] = useState(false);
    const lockHeartbeatRef = useRef<number | null>(null);
    const lockAcquiredRef = useRef(false);
    useEffect(() => { lockAcquiredRef.current = lockAcquired; }, [lockAcquired]);

    const latestVersionId = versions[0]?.id;
    const isReadOnly = !currentDiagram || currentDiagram.status !== 'Draft' || currentDiagram.versionId !== latestVersionId;
    useEffect(() => { isReadOnlyRef.current = isReadOnly; }, [isReadOnly]);

    // ─── Попытка захвата блокировки после загрузки данных ─────────────────────
    const acquireLock = useCallback(async () => {
        if (!token) return;
        try {
            const result: AcquireLockResponse = await api.acquireDiagramLock(token, processId);
            setLockAcquired(result.isAcquired);
            if (!result.isAcquired && result.lock) {
                setLockInfo(result.lock);
            } else {
                setLockInfo(null);
            }
        } catch { /* игнорируем — блокировка не критична */ }
    }, [token, processId]);

    const releaseLock = useCallback(async () => {
        if (!token || !lockAcquired) return;
        try {
            await api.releaseDiagramLock(token, processId);
            setLockAcquired(false);
        } catch { /* игнорируем */ }
    }, [token, processId, lockAcquired]);

    // Захватываем блокировку при монтировании страницы
    useEffect(() => {
        acquireLock();
    }, [acquireLock]);

    // Heartbeat — продлеваем блокировку каждые 30 секунд
    useEffect(() => {
        if (!token || !lockAcquired) return;
        lockHeartbeatRef.current = window.setInterval(async () => {
            try {
                const ok = await api.acquireDiagramLock(token, processId);
                if (!ok.isAcquired) setLockAcquired(false);
            } catch { /* игнорируем */ }
        }, 30000);
        return () => {
            if (lockHeartbeatRef.current) clearInterval(lockHeartbeatRef.current);
        };
    }, [token, processId, lockAcquired]);

    // Освобождаем блокировку при размонтировании страницы.
    // Пустой массив зависимостей намеренен: cleanup должен выполниться только при размонтировании.
    // Актуальные значения token/processId/lockAcquired читаются через ref-ы, чтобы избежать stale closure.
    useEffect(() => {
        return () => {
            if (token && lockAcquiredRef.current) {
                // fire-and-forget
                api.releaseDiagramLock(token, processId).catch(() => {/* игнорируем */ });
            }
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps -- cleanup при размонтировании, ref-ы обеспечивают актуальные значения
    }, []);

    const clearMarkers = useCallback(() => {
        const canvas = modelerRef.current?.get<{ removeMarker: (elementId: string, marker: string) => void }>('canvas');
        if (!canvas) return;
        versionMarkerIdsRef.current.forEach(id => canvas.removeMarker(id, 'bpd-diff-marker'));
        versionMarkerIdsRef.current = [];
        if (debugMarkerRef.current) {
            canvas.removeMarker(debugMarkerRef.current, 'bpd-debug-marker');
            debugMarkerRef.current = null;
        }
    }, []);

    const focusElement = useCallback((elementId?: string, marker?: 'bpd-diff-marker' | 'bpd-debug-marker') => {
        if (!elementId || !modelerRef.current) return;
        const elementRegistry = modelerRef.current.get<{ get: (id: string) => unknown }>('elementRegistry');
        const selection = modelerRef.current.get<{ select: (element: unknown) => void }>('selection');
        const canvas = modelerRef.current.get<{
            zoom: (value: string, point?: { x: number; y: number }) => void;
            addMarker: (elementId: string, marker: string) => void;
            removeMarker: (elementId: string, marker: string) => void;
        }>('canvas');
        const element = elementRegistry.get(elementId);
        if (!element) return;

        selection.select(element);
        if (marker) {
            if (marker === 'bpd-debug-marker' && debugMarkerRef.current && debugMarkerRef.current !== elementId) {
                canvas.removeMarker(debugMarkerRef.current, 'bpd-debug-marker');
            }
            canvas.addMarker(elementId, marker);
            if (marker === 'bpd-diff-marker' && !versionMarkerIdsRef.current.includes(elementId)) {
                versionMarkerIdsRef.current.push(elementId);
            }
            if (marker === 'bpd-debug-marker') {
                debugMarkerRef.current = elementId;
            }
        }
        canvas.zoom('fit-viewport');
    }, []);

    const loadDiagramIntoCanvas = useCallback(async (diagram: BpmDiagramDto) => {
        if (!modelerRef.current) return;
        clearMarkers();
        await modelerRef.current.importXML(diagram.diagramXml ?? EMPTY_DIAGRAM);
        const canvas = modelerRef.current.get<{ zoom: (value: string) => void }>('canvas');
        canvas.zoom('fit-viewport');
        setCurrentDiagram(diagram);
        setSaveStatus('saved');
    }, [clearMarkers]);

    const refreshVersions = useCallback(async () => {
        if (!token) return [];
        const data = await api.getProcessVersions(token, processId);
        setVersions(data);
        return data;
    }, [token, processId]);

    const saveDiagram = useCallback(async () => {
        if (!token || !modelerRef.current || isReadOnly) return;
        setSaveStatus('saving');
        setSaveError(null);
        try {
            const { xml } = await modelerRef.current.saveXML({ format: true });
            const saved = await api.saveDiagram(token, processId, xml);
            const refreshed = await refreshVersions();
            setCurrentDiagram(saved);
            setSaveStatus('saved');
            if (!diffTargetVersionId && refreshed.length > 1) setDiffTargetVersionId(refreshed[1].id);
        } catch (e) {
            setSaveStatus('error');
            setSaveError(e instanceof Error ? e.message : 'Ошибка сохранения');
        }
    }, [token, processId, isReadOnly, refreshVersions, diffTargetVersionId]);

    const saveDiagramRef = useRef(saveDiagram);
    useEffect(() => { saveDiagramRef.current = saveDiagram; }, [saveDiagram]);

    const loadInitialData = useCallback(async () => {
        if (!token) return;
        setLoadError(null);
        try {
            const [processDto, versionsData, diagram] = await Promise.all([
                api.getProcess(token, processId),
                api.getProcessVersions(token, processId),
                api.getDiagram(token, processId),
            ]);
            setProcess(processDto);
            setVersions(versionsData);
            if (versionsData.length > 1) setDiffTargetVersionId(versionsData[1].id);
            await loadDiagramIntoCanvas(diagram);
        } catch (e) {
            setLoadError(e instanceof Error ? e.message : 'Ошибка загрузки диаграммы');
        }
    }, [token, processId, loadDiagramIntoCanvas]);

    useEffect(() => {
        if (!containerRef.current || !token) return;

        const modeler = new BpmnModeler({
            container: containerRef.current,
            keyboard: { bindTo: document },
            additionalModules: [minimapModule],
        });
        modelerRef.current = modeler;

        const onChanged = () => {
            if (isReadOnlyRef.current) return;
            setSaveStatus('unsaved');
            if (autoSaveTimer.current) clearTimeout(autoSaveTimer.current);
            autoSaveTimer.current = window.setTimeout(() => saveDiagramRef.current(), 30000);
        };

        modeler.on('commandStack.changed', onChanged);
        setModelerReady(true);
        loadInitialData();

        return () => {
            if (autoSaveTimer.current) clearTimeout(autoSaveTimer.current);
            clearMarkers();
            modeler.off('commandStack.changed', onChanged);
            modeler.destroy();
            modelerRef.current = null;
            setModelerReady(false);
        };
    }, [token, loadInitialData, clearMarkers]);

    useEffect(() => {
        const beforeUnload = (e: BeforeUnloadEvent) => {
            if (saveStatus === 'unsaved') {
                e.preventDefault();
                e.returnValue = '';
            }
        };
        window.addEventListener('beforeunload', beforeUnload);
        return () => window.removeEventListener('beforeunload', beforeUnload);
    }, [saveStatus]);

    useEffect(() => {
        if (debugSession?.currentElementId) {
            focusElement(debugSession.currentElementId, 'bpd-debug-marker');
        }
    }, [debugSession?.currentElementId, focusElement]);

    const handleLoadVersion = async (versionId: string) => {
        if (!token) return;
        try {
            const version = await api.getProcessVersion(token, processId, versionId);
            await loadDiagramIntoCanvas(version);
            setValidationResult(null);
            setDiffResult(null);
            setShowVersions(false);
        } catch (e) {
            setLoadError(e instanceof Error ? e.message : 'Ошибка загрузки версии');
        }
    };

    const handleValidate = async () => {
        if (!token) return;
        setValidating(true);
        try {
            const result = await api.validateProcess(token, processId, currentDiagram?.versionId);
            setValidationResult(result);
        } catch (e) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка валидации');
        } finally {
            setValidating(false);
        }
    };

    const handlePublish = async () => {
        if (!token || !currentDiagram) return;
        // Сначала валидируем
        setPublishing(true);
        setSaveError(null);
        try {
            const validation = await api.validateProcess(token, processId, currentDiagram.versionId);
            setValidationResult(validation);
            if (validation.issues.some(i => i.severity === 'Error')) return;
        } finally {
            setPublishing(false);
        }
        // Открываем диалог комментария
        setPublishReleaseNotes('');
        setShowPublishDialog(true);
    };

    const handlePublishConfirm = async () => {
        if (!token || !currentDiagram) return;
        setShowPublishDialog(false);
        setPublishing(true);
        setSaveError(null);
        try {
            await api.publishProcessVersion(token, processId, currentDiagram.versionId, publishReleaseNotes || undefined);
            const refreshed = await refreshVersions();
            const latest = refreshed.find(v => v.id === currentDiagram.versionId);
            if (latest) setCurrentDiagram(prev => prev ? { ...prev, status: latest.status, publishedAt: latest.publishedAt } : prev);
        } catch (e) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка публикации');
        } finally {
            setPublishing(false);
        }
    };

    const handleRollback = async (versionId: string) => {
        if (!token) return;
        try {
            const version = await api.rollbackProcessVersion(token, processId, versionId);
            await refreshVersions();
            await loadDiagramIntoCanvas(version);
            setShowVersions(false);
        } catch (e) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка отката');
        }
    };

    const handleDiff = async () => {
        if (!token || !currentDiagram || !diffTargetVersionId) return;
        try {
            const result = await api.diffProcessVersions(token, processId, diffTargetVersionId, currentDiagram.versionId);
            setDiffResult(result);
            clearMarkers();
            result.elements
                .filter(change => change.changeType !== 'Removed')
                .forEach(change => focusElement(change.elementId, 'bpd-diff-marker'));
        } catch (e) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка сравнения версий');
        }
    };

    const handleStartDebug = async () => {
        if (!token || !currentDiagram) return;
        setDebugError(null);
        try {
            const session = await api.startDebugSession(token, processId, { versionId: currentDiagram.versionId });
            setDebugSession(session);
            setShowDebug(true);
        } catch (e) {
            setDebugError(e instanceof Error ? e.message : 'Не удалось запустить debug-режим');
        }
    };

    const handleDebugAction = async (action: 'step' | 'complete' | 'skip') => {
        if (!token || !debugSession) return;
        try {
            const next = action === 'step'
                ? await api.stepDebugSession(token, processId, debugSession.sessionId)
                : action === 'complete'
                    ? await api.completeDebugTask(token, processId, debugSession.sessionId)
                    : await api.skipDebugTask(token, processId, debugSession.sessionId);
            setDebugSession(next);
        } catch (e) {
            setDebugError(e instanceof Error ? e.message : 'Ошибка debug-шага');
        }
    };

    const handleDownloadDocument = async () => {
        if (!token) return;
        setBusyDocument(true);
        try {
            const blob = await api.downloadProcessDocument(token, processId);
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `${process?.name ?? 'process'}.pdf`;
            link.click();
            URL.revokeObjectURL(url);
        } catch (e) {
            setSaveError(e instanceof Error ? e.message : 'Не удалось скачать регламент');
        } finally {
            setBusyDocument(false);
        }
    };

    const handleBackClick = () => {
        if (saveStatus === 'unsaved' && !window.confirm('Есть несохранённые изменения. Покинуть страницу?')) return;
        releaseLock();
        onBack();
    };

    const handleExportSVG = async () => {
        if (!modelerRef.current) return;
        setShowExportMenu(false);
        try {
            const { svg } = await modelerRef.current.saveSVG();
            const blob = new Blob([svg], { type: 'image/svg+xml' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `${process?.name ?? 'diagram'}.svg`;
            link.click();
            URL.revokeObjectURL(url);
        } catch (e) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка экспорта SVG');
        }
    };

    const handleExportPNG = async () => {
        if (!modelerRef.current) return;
        setShowExportMenu(false);
        try {
            const { svg } = await modelerRef.current.saveSVG();
            const img = new Image();
            const svgBlob = new Blob([svg], { type: 'image/svg+xml;charset=utf-8' });
            const url = URL.createObjectURL(svgBlob);
            img.onload = () => {
                const canvas = document.createElement('canvas');
                canvas.width = img.width || 1200;
                canvas.height = img.height || 800;
                const ctx = canvas.getContext('2d');
                if (!ctx) return;
                ctx.fillStyle = '#ffffff';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
                ctx.drawImage(img, 0, 0);
                URL.revokeObjectURL(url);
                canvas.toBlob(blob => {
                    if (!blob) return;
                    const pngUrl = URL.createObjectURL(blob);
                    const link = document.createElement('a');
                    link.href = pngUrl;
                    link.download = `${process?.name ?? 'diagram'}.png`;
                    link.click();
                    URL.revokeObjectURL(pngUrl);
                }, 'image/png');
            };
            img.src = url;
        } catch (e) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка экспорта PNG');
        }
    };

    const handleImportBpmn = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file || !token) return;
        try {
            const text = await file.text();
            const imported = await api.importDiagram(token, processId, text);
            await refreshVersions();
            await loadDiagramIntoCanvas(imported);
        } catch (err) {
            setSaveError(err instanceof Error ? err.message : 'Ошибка импорта');
        } finally {
            if (importFileRef.current) importFileRef.current.value = '';
        }
    };

    // ─── Авто-раскладка ───────────────────────────────────────────────────────

    const handleAutoLayout = async () => {
        if (!modelerRef.current || isReadOnly) return;
        setLayouting(true);
        try {
            const { xml: currentXml } = await modelerRef.current.saveXML({ format: true });
            const laidOutXml = await layoutProcess(currentXml);
            await modelerRef.current.importXML(laidOutXml);
            const canvas = modelerRef.current.get<{ zoom: (v: string) => void }>('canvas');
            canvas.zoom('fit-viewport');
            setSaveStatus('unsaved');
        } catch { /* не критично */ }
        finally { setLayouting(false); }
    };

    // ─── Миникарта: синхронизация видимости ───────────────────────────────────

    useEffect(() => {
        if (!modelerReady || !modelerRef.current) return;
        try {
            const minimap = modelerRef.current.get<{ open: () => void; close: () => void } | null>('minimap');
            if (minimap) {
                if (minimapVisible) minimap.open(); else minimap.close();
            }
        } catch { /* миникарта может быть недоступна */ }
    }, [minimapVisible, modelerReady]);

    // ─── Поиск по диаграмме ───────────────────────────────────────────────────

    const handleSearch = (query: string) => {
        setSearchQuery(query);
        setSearchIndex(0);
        if (!query.trim() || !modelerRef.current) {
            setSearchResults([]);
            return;
        }
        try {
            const elementRegistry = modelerRef.current.get<{
                getAll: () => Array<{ id: string; type: string; businessObject?: { name?: string; $type?: string } }>;
            }>('elementRegistry');
            const q = query.toLowerCase();
            const results = elementRegistry.getAll()
                .filter(el => {
                    const name = el.businessObject?.name ?? '';
                    const type = el.type ?? '';
                    return name.toLowerCase().includes(q) || type.toLowerCase().includes(q);
                })
                .map(el => ({
                    id: el.id,
                    name: el.businessObject?.name ?? el.id,
                    type: el.type,
                }));
            setSearchResults(results);
        } catch { setSearchResults([]); }
    };

    const navigateSearch = (delta: number) => {
        if (searchResults.length === 0) return;
        const next = (searchIndex + delta + searchResults.length) % searchResults.length;
        setSearchIndex(next);
        focusElement(searchResults[next].id);
    };

    // ─── Глобальный Ctrl+F ────────────────────────────────────────────────────

    useEffect(() => {
        const handler = (e: KeyboardEvent) => {
            if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
                e.preventDefault();
                setShowSearch(prev => !prev);
                setTimeout(() => searchInputRef.current?.focus(), 50);
            }
            if (e.key === 'Escape' && showSearch) {
                setShowSearch(false);
                setSearchResults([]);
            }
        };
        window.addEventListener('keydown', handler);
        return () => window.removeEventListener('keydown', handler);
    }, [showSearch]);

    // ─── Закрытие меню экспорта по клику вне ──────────────────────────────────

    useEffect(() => {
        if (!showExportMenu) return;
        const handler = () => setShowExportMenu(false);
        document.addEventListener('click', handler, { once: true });
        return () => document.removeEventListener('click', handler);
    }, [showExportMenu]);

    const statusLabel = isReadOnly ? 'Просмотр версии' : STATUS_LABELS[saveStatus];

    return (
        <div className="bpd-root">
            <div className="bpd-toolbar" role="toolbar" aria-label="Панель инструментов дизайнера">
                <button className="bpd-back-btn" onClick={handleBackClick}>← Процессы</button>
                <div className="bpd-toolbar-sep" role="separator" />
                <span className="bpd-process-name">
                    {process?.name ?? '...'}
                    {currentDiagram && <span className="bpd-version-badge">v{currentDiagram.versionNumber}</span>}
                    {isReadOnly && <span className="bpd-readonly-badge">read-only</span>}
                </span>
                <div className="bpd-toolbar-spacer" />
                <div className="bpd-toolbar-group">
                    <button className="bpd-tool-btn bpd-tool-btn--wide" onClick={() => setShowVersions(v => !v)}>Версии</button>
                    <button className="bpd-tool-btn bpd-tool-btn--wide" onClick={handleValidate} disabled={validating}>
                        {validating ? 'Проверка…' : 'Проверить'}
                    </button>
                    <button className="bpd-tool-btn bpd-tool-btn--wide" onClick={handlePublish} disabled={publishing || !currentDiagram}>
                        {publishing ? 'Публикация…' : 'Опубликовать'}
                    </button>
                    <button className="bpd-tool-btn bpd-tool-btn--wide" onClick={handleDiff} disabled={!diffTargetVersionId || !currentDiagram}>Diff</button>
                    <button className="bpd-tool-btn bpd-tool-btn--wide" onClick={handleStartDebug} disabled={!currentDiagram}>Debug</button>
                    <button className="bpd-tool-btn bpd-tool-btn--wide" onClick={handleDownloadDocument} disabled={busyDocument}>
                        {busyDocument ? 'PDF…' : 'PDF'}
                    </button>
                    <button
                        className="bpd-tool-btn bpd-tool-btn--wide"
                        onClick={handleAutoLayout}
                        disabled={layouting || isReadOnly}
                        title="Автоматически упорядочить элементы схемы"
                    >
                        {layouting ? 'Раскладка…' : '⊞ Упорядочить'}
                    </button>
                    <button
                        className={`bpd-tool-btn bpd-tool-btn--wide${minimapVisible ? ' active' : ''}`}
                        onClick={() => setMinimapVisible(v => !v)}
                        title="Показать / скрыть миникарту"
                    >
                        🗺 Миникарта
                    </button>
                    <button
                        className={`bpd-tool-btn bpd-tool-btn--wide${showSearch ? ' active' : ''}`}
                        onClick={() => { setShowSearch(v => !v); setTimeout(() => searchInputRef.current?.focus(), 50); }}
                        title="Поиск по диаграмме (Ctrl+F)"
                    >
                        🔍 Поиск
                    </button>
                    {/* Экспорт */}
                    <div style={{ position: 'relative', display: 'inline-block' }}>
                        <button
                            className="bpd-tool-btn bpd-tool-btn--wide"
                            onClick={e => { e.stopPropagation(); setShowExportMenu(v => !v); }}
                            title="Экспорт диаграммы"
                        >
                            ↑ Экспорт
                        </button>
                        {showExportMenu && (
                            <div className="bpd-export-menu" role="menu">
                                <button className="bpd-export-menu-item" onClick={handleExportSVG}>SVG</button>
                                <button className="bpd-export-menu-item" onClick={handleExportPNG}>PNG</button>
                            </div>
                        )}
                    </div>
                    {/* Импорт */}
                    <button
                        className="bpd-tool-btn bpd-tool-btn--wide"
                        onClick={() => importFileRef.current?.click()}
                        disabled={isReadOnly}
                        title="Импорт BPMN XML"
                    >
                        ↓ Импорт
                    </button>
                    <input
                        ref={importFileRef}
                        type="file"
                        accept=".bpmn,.xml"
                        style={{ display: 'none' }}
                        onChange={handleImportBpmn}
                    />
                </div>
                <div className="bpd-toolbar-sep" role="separator" />
                <button
                    className={`bpd-save-btn bpd-save-btn--${saveStatus}`}
                    onClick={() => saveDiagram()}
                    disabled={saveStatus === 'saving' || isReadOnly}
                    title={isReadOnly ? 'Для выбранной версии доступен только просмотр' : 'Сохранить черновик'}
                >
                    {saveStatus === 'saving' ? '↻ Сохранение...' : '↓ Сохранить'}
                </button>
                <div className={`bpd-status bpd-status--${saveStatus}`} aria-live="polite">{statusLabel}</div>
            </div>

            {loadError && <div className="bpd-load-error">Не удалось загрузить диаграмму: {loadError}</div>}
            {saveError && <div className="bpd-save-error">{saveError}</div>}

            {/* Панель поиска по диаграмме */}
            {showSearch && (
                <div className="bpd-search-bar" role="search">
                    <input
                        ref={searchInputRef}
                        className="bpd-search-input"
                        type="text"
                        placeholder="Поиск по имени или типу элемента…"
                        value={searchQuery}
                        onChange={e => handleSearch(e.target.value)}
                    />
                    <span className="bpd-search-count">
                        {searchResults.length > 0 ? `${searchIndex + 1} / ${searchResults.length}` : searchQuery ? '0 / 0' : ''}
                    </span>
                    <button className="bpd-mini-btn" onClick={() => navigateSearch(-1)} disabled={searchResults.length === 0} title="Предыдущее">↑</button>
                    <button className="bpd-mini-btn" onClick={() => navigateSearch(1)} disabled={searchResults.length === 0} title="Следующее">↓</button>
                    <button className="bpd-mini-btn" onClick={() => { setShowSearch(false); setSearchQuery(''); setSearchResults([]); }}>✕</button>
                    {searchResults.length > 0 && (
                        <span style={{ fontSize: 11, color: '#6b7280', marginLeft: 8 }}>
                            {searchResults[searchIndex]?.name ?? searchResults[searchIndex]?.id}
                        </span>
                    )}
                </div>
            )}

            {/* Предупреждение о конкурентном редактировании */}
            {lockInfo && !lockAcquired && (
                <div className="bpd-lock-warning" role="alert">
                    ⚠️ Диаграмма сейчас редактируется пользователем <strong>{lockInfo.lockedByDisplayName}</strong>.
                    Ваши изменения могут быть перезаписаны. Сохранение заблокировано.
                    <button className="bpd-mini-btn" style={{ marginLeft: 12 }} onClick={acquireLock}>
                        Попробовать перехватить
                    </button>
                </div>
            )}

            {validationResult && (
                <div className="bpd-panel">
                    <div className="bpd-panel-title">Результаты валидации v{validationResult.versionNumber}</div>
                    {validationResult.issues.length === 0 && <div className="bpd-panel-empty">Ошибок и предупреждений не найдено.</div>}
                    {validationResult.issues.map((issue, index) => (
                        <button
                            key={`${issue.code}-${index}`}
                            className={`bpd-issue bpd-issue--${issue.severity.toLowerCase()}`}
                            onClick={() => focusElement(issue.elementId)}
                        >
                            <span>{issue.severity === 'Error' ? 'Ошибка' : 'Предупреждение'}</span>
                            <span>{issue.message}</span>
                            {issue.elementId && <code>{issue.elementId}</code>}
                        </button>
                    ))}
                </div>
            )}

            {diffResult && (
                <div className="bpd-panel">
                    <div className="bpd-panel-title">Diff версий</div>
                    <div className="bpd-diff-grid">
                        <div>
                            <h4>Элементы</h4>
                            {diffResult.elements.map(change => (
                                <button key={`${change.changeType}-${change.elementId}`} className="bpd-diff-item" onClick={() => focusElement(change.elementId, 'bpd-diff-marker')}>
                                    <strong>{change.changeType}</strong> {change.name ?? change.elementId}
                                </button>
                            ))}
                        </div>
                        <div>
                            <h4>Свойства</h4>
                            {diffResult.properties.map((change, index) => (
                                <div key={`${change.targetId}-${change.propertyName}-${index}`} className="bpd-diff-prop">
                                    <strong>{change.targetId}</strong> · {change.propertyName}
                                    <div>{change.leftValue ?? '—'} → {change.rightValue ?? '—'}</div>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
            )}

            <div className="bpd-workspace">
                <div className="bpd-canvas-wrap">
                    {showVersions && (
                        <div className="bpd-sidepanel">
                            <div className="bpd-sidepanel-title">История версий</div>
                            <div className="bpd-sidepanel-list">
                                {versions.map(v => (
                                    <div key={v.id} className={`bpd-version-item${currentDiagram?.versionId === v.id ? ' active' : ''}`}>
                                        <button className="bpd-version-main" onClick={() => handleLoadVersion(v.id)}>
                                            <span>v{v.versionNumber}</span>
                                            <span>{VERSION_STATUS_LABELS[v.status]}</span>
                                        </button>
                                        {v.releaseNotes && (
                                            <div style={{ padding: '2px 8px 4px 8px', fontSize: 11, color: '#6b7280', fontStyle: 'italic' }}>
                                                {v.releaseNotes}
                                            </div>
                                        )}
                                        <div className="bpd-version-actions">
                                            <button className="bpd-mini-btn" onClick={() => handleLoadVersion(v.id)}>Открыть</button>
                                            <button className="bpd-mini-btn" onClick={() => handleRollback(v.id)}>Откатить</button>
                                        </div>
                                    </div>
                                ))}
                            </div>
                            <div className="bpd-field-inline">
                                <label htmlFor="bpd-diff-version">Сравнить с:</label>
                                <select id="bpd-diff-version" value={diffTargetVersionId} onChange={e => setDiffTargetVersionId(e.target.value)}>
                                    <option value="">— Выберите версию —</option>
                                    {versions.filter(v => v.id !== currentDiagram?.versionId).map(v => (
                                        <option key={v.id} value={v.id}>v{v.versionNumber} · {VERSION_STATUS_LABELS[v.status]}</option>
                                    ))}
                                </select>
                            </div>
                        </div>
                    )}
                    <div ref={containerRef} className="bpd-canvas" />
                </div>

                {showDebug && (
                    <div className="bpd-debug-panel">
                        <div className="bpd-sidepanel-title">Debug-режим</div>
                        {debugError && <div className="bpd-error-inline">{debugError}</div>}
                        {debugSession && (
                            <>
                                <div className="bpd-debug-current">
                                    Текущий узел: <strong>{debugSession.currentElementId ?? '—'}</strong>
                                </div>
                                <div className="bpd-debug-actions">
                                    <button className="bpd-mini-btn" onClick={() => handleDebugAction('step')} disabled={debugSession.isCompleted}>Шаг</button>
                                    <button className="bpd-mini-btn" onClick={() => handleDebugAction('complete')} disabled={debugSession.isCompleted}>Завершить</button>
                                    <button className="bpd-mini-btn" onClick={() => handleDebugAction('skip')} disabled={debugSession.isCompleted}>Пропустить</button>
                                </div>
                                <div className="bpd-debug-block">
                                    <h4>Переменные</h4>
                                    {Object.entries(debugSession.variables).map(([key, value]) => (
                                        <div key={key} className="bpd-debug-kv"><strong>{key}</strong>: {value}</div>
                                    ))}
                                    {Object.keys(debugSession.variables).length === 0 && <div className="bpd-panel-empty">Переменные отсутствуют.</div>}
                                </div>
                                <div className="bpd-debug-block">
                                    <h4>Трассировка</h4>
                                    {debugSession.events.map((event, index) => (
                                        <button key={`${event.timestamp}-${index}`} className="bpd-debug-event" onClick={() => focusElement(event.elementId, 'bpd-debug-marker')}>
                                            <strong>{event.eventType}</strong>
                                            <span>{event.message}</span>
                                        </button>
                                    ))}
                                </div>
                            </>
                        )}
                    </div>
                )}

                <aside className="bpd-properties-panel" aria-label="Панель свойств элемента">
                    {modelerReady && modelerRef.current && token && (
                        <BpmPropertiesPanel modeler={modelerRef.current} processId={processId} token={token} />
                    )}
                </aside>
            </div>

            {/* Диалог публикации версии */}
            {showPublishDialog && (
                <div className="bpd-overlay" role="dialog" aria-modal="true" aria-label="Публикация версии">
                    <div className="bpd-modal">
                        <h3 className="bpd-modal-title">Публикация версии {currentDiagram?.versionNumber}</h3>
                        <p className="bpd-modal-hint">Опционально укажите, что изменилось в этой версии.</p>
                        <textarea
                            className="bpd-input"
                            rows={4}
                            placeholder="Что изменилось…"
                            value={publishReleaseNotes}
                            onChange={e => setPublishReleaseNotes(e.target.value)}
                            style={{ width: '100%', marginBottom: 12 }}
                        />
                        <div className="bpd-modal-actions">
                            <button className="bpd-tool-btn" onClick={() => setShowPublishDialog(false)}>Отмена</button>
                            <button className="bpd-tool-btn bpd-tool-btn--primary" onClick={handlePublishConfirm}>
                                Опубликовать
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
