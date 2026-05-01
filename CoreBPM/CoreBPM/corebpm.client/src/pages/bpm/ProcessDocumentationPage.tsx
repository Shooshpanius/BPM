import { useState, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getMyDocumentation,
    getAllDocumentation,
    downloadProcessDocument,
} from '../../api/bpmApi';
import type {
    ProcessDocumentationItemDto,
    ProcessDocVersionDto,
} from '../../api/bpmApi';
import './ProcessDocumentationPage.css';

type Tab = 'my' | 'all';

export default function ProcessDocumentationPage() {
    const { accessToken, roles } = useAuth();
    const isAdmin = roles?.includes('Admin') ?? false;

    const [tab, setTab] = useState<Tab>('my');
    const [includeDeleted, setIncludeDeleted] = useState(false);
    const [items, setItems] = useState<ProcessDocumentationItemDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());
    const [loadedOnce, setLoadedOnce] = useState(false);

    const [selectedProcess, setSelectedProcess] = useState<string | null>(null);
    const [selectedVersion, setSelectedVersion] = useState<string | null>(null);

    const load = useCallback(async (t: Tab, deleted: boolean) => {
        if (!accessToken) return;
        setLoading(true);
        setError(null);
        try {
            const data = t === 'my'
                ? await getMyDocumentation(accessToken)
                : await getAllDocumentation(accessToken, deleted);
            setItems(data);
            setLoadedOnce(true);
        } catch {
            setError('Не удалось загрузить список документации');
        } finally {
            setLoading(false);
        }
    }, [accessToken]);

    const handleTabChange = (t: Tab) => {
        setTab(t);
        setExpandedIds(new Set());
        setItems([]);
        setLoadedOnce(false);
        load(t, includeDeleted);
    };

    const handleLoad = () => {
        load(tab, includeDeleted);
    };

    const toggleExpand = (processId: string) => {
        setExpandedIds(prev => {
            const next = new Set(prev);
            if (next.has(processId)) next.delete(processId);
            else next.add(processId);
            return next;
        });
    };

    const openSnapshot = (processId: string, versionId: string) => {
        setSelectedProcess(processId);
        setSelectedVersion(versionId);
    };

    const closeSnapshot = () => {
        setSelectedProcess(null);
        setSelectedVersion(null);
    };

    const handleDownloadPdf = async (processId: string) => {
        if (!accessToken) return;
        try {
            const blob = await downloadProcessDocument(accessToken, processId);
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `process-${processId}.pdf`;
            a.click();
            URL.revokeObjectURL(url);
        } catch {
            alert('Не удалось скачать PDF');
        }
    };

    if (selectedProcess && selectedVersion) {
        return (
            <ProcessDocSnapshotViewer
                token={accessToken!}
                processId={selectedProcess}
                versionId={selectedVersion}
                onBack={closeSnapshot}
                onDownloadPdf={() => handleDownloadPdf(selectedProcess)}
            />
        );
    }

    return (
        <div className="proc-doc-page">
            <div className="proc-doc-header">
                <h1 className="proc-doc-title">Документирование процессов</h1>
                <p className="proc-doc-subtitle">
                    Технические документации опубликованных версий процессов
                </p>
            </div>

            <div className="proc-doc-tabs">
                <button
                    className={`proc-doc-tab${tab === 'my' ? ' active' : ''}`}
                    onClick={() => handleTabChange('my')}
                >
                    Мои процессы
                </button>
                {isAdmin && (
                    <button
                        className={`proc-doc-tab${tab === 'all' ? ' active' : ''}`}
                        onClick={() => handleTabChange('all')}
                    >
                        Вся документация
                    </button>
                )}
            </div>

            <div className="proc-doc-toolbar">
                {tab === 'all' && isAdmin && (
                    <label className="proc-doc-check">
                        <input
                            type="checkbox"
                            checked={includeDeleted}
                            onChange={e => setIncludeDeleted(e.target.checked)}
                        />
                        Показать удалённые
                    </label>
                )}
                <button className="proc-doc-btn-load" onClick={handleLoad} disabled={loading}>
                    {loading ? 'Загрузка…' : (loadedOnce ? '↺ Обновить' : 'Загрузить')}
                </button>
            </div>

            {error && <div className="proc-doc-error">{error}</div>}

            {!loadedOnce && !loading && (
                <div className="proc-doc-empty">
                    Нажмите «Загрузить» для отображения документации
                </div>
            )}

            {loadedOnce && !loading && items.length === 0 && (
                <div className="proc-doc-empty">
                    {tab === 'my'
                        ? 'Вы не являетесь Владельцем или Куратором ни одного процесса'
                        : 'Процессы не найдены'}
                </div>
            )}

            {items.length > 0 && (
                <div className="proc-doc-list">
                    {items.map(item => (
                        <ProcessDocCard
                            key={item.processId}
                            item={item}
                            expanded={expandedIds.has(item.processId)}
                            onToggle={() => toggleExpand(item.processId)}
                            onOpenSnapshot={openSnapshot}
                            onDownloadPdf={() => handleDownloadPdf(item.processId)}
                        />
                    ))}
                </div>
            )}
        </div>
    );
}

// ─── Карточка процесса ────────────────────────────────────────────────────────

interface ProcessDocCardProps {
    item: ProcessDocumentationItemDto;
    expanded: boolean;
    onToggle: () => void;
    onOpenSnapshot: (processId: string, versionId: string) => void;
    onDownloadPdf: () => void;
}

function ProcessDocCard({ item, expanded, onToggle, onOpenSnapshot, onDownloadPdf }: ProcessDocCardProps) {
    const published = item.publishedVersions;
    const hasVersions = published.length > 0;

    return (
        <div className={`proc-doc-card${item.isDeleted ? ' deleted' : ''}`}>
            <div className="proc-doc-card-head" onClick={onToggle}>
                <div className="proc-doc-card-info">
                    <span className="proc-doc-card-name">{item.processName}</span>
                    {item.isDeleted && <span className="proc-doc-badge badge-deleted">Удалён</span>}
                    {item.tags.map(tag => (
                        <span key={tag} className="proc-doc-badge badge-tag">{tag}</span>
                    ))}
                    {item.processDescription && (
                        <span className="proc-doc-card-desc">{item.processDescription}</span>
                    )}
                </div>
                <div className="proc-doc-card-meta">
                    <span className="proc-doc-versions-count">
                        {hasVersions ? `${published.length} верс.` : 'Нет публикаций'}
                    </span>
                    <span className="proc-doc-card-chevron">{expanded ? '▲' : '▼'}</span>
                </div>
            </div>

            {expanded && (
                <div className="proc-doc-card-body">
                    {!hasVersions ? (
                        <p className="proc-doc-no-versions">Нет опубликованных версий</p>
                    ) : (
                        <>
                            <div className="proc-doc-pdf-row">
                                <button className="proc-doc-btn-pdf" onClick={onDownloadPdf}>
                                    ⬇ Скачать PDF-регламент (активная версия)
                                </button>
                            </div>
                            <table className="proc-doc-versions-table">
                                <thead>
                                    <tr>
                                        <th>Версия</th>
                                        <th>Дата публикации</th>
                                        <th>Комментарий</th>
                                        <th>Документация</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {published.map(v => (
                                        <VersionRow
                                            key={v.versionId}
                                            processId={item.processId}
                                            version={v}
                                            onOpenSnapshot={onOpenSnapshot}
                                        />
                                    ))}
                                </tbody>
                            </table>
                        </>
                    )}
                </div>
            )}
        </div>
    );
}

// ─── Строка версии ────────────────────────────────────────────────────────────

interface VersionRowProps {
    processId: string;
    version: ProcessDocVersionDto;
    onOpenSnapshot: (processId: string, versionId: string) => void;
}

function VersionRow({ processId, version, onOpenSnapshot }: VersionRowProps) {
    return (
        <tr>
            <td>
                <span className="proc-doc-version-num">v{version.versionNumber}</span>
            </td>
            <td className="proc-doc-cell-date">
                {version.publishedAt
                    ? new Date(version.publishedAt).toLocaleString('ru-RU', {
                        day: '2-digit', month: '2-digit', year: 'numeric',
                        hour: '2-digit', minute: '2-digit',
                    })
                    : '—'}
            </td>
            <td className="proc-doc-cell-notes">
                {version.releaseNotes || <span className="proc-doc-muted">—</span>}
            </td>
            <td>
                {version.hasSnapshot ? (
                    <button
                        className="proc-doc-btn-view"
                        onClick={() => onOpenSnapshot(processId, version.versionId)}
                    >
                        Открыть
                    </button>
                ) : (
                    <span className="proc-doc-muted">Нет снапшота</span>
                )}
            </td>
        </tr>
    );
}

// ─── Просмотр снапшота (встроенный вьюер) ────────────────────────────────────

import { useEffect, useRef } from 'react';
import { getDocSnapshot } from '../../api/bpmApi';
import type { DocSnapshotDto } from '../../api/bpmApi';

interface SnapshotViewerProps {
    token: string;
    processId: string;
    versionId: string;
    onBack: () => void;
    onDownloadPdf: () => void;
}

function ProcessDocSnapshotViewer({ token, processId, versionId, onBack, onDownloadPdf }: SnapshotViewerProps) {
    const [snapshot, setSnapshot] = useState<DocSnapshotDto | null>(null);
    const [loadErr, setLoadErr] = useState<string | null>(null);
    const iframeRef = useRef<HTMLIFrameElement>(null);

    useEffect(() => {
        getDocSnapshot(token, processId, versionId)
            .then(setSnapshot)
            .catch(() => setLoadErr('Не удалось загрузить документацию'));
    }, [token, processId, versionId]);

    useEffect(() => {
        if (snapshot && iframeRef.current) {
            const iframe = iframeRef.current;
            const doc = iframe.contentDocument || iframe.contentWindow?.document;
            if (doc) {
                doc.open();
                doc.write(snapshot.htmlContent);
                doc.close();
            }
        }
    }, [snapshot]);

    return (
        <div className="proc-doc-snapshot-page">
            <div className="proc-doc-snapshot-toolbar">
                <button className="proc-doc-btn-back" onClick={onBack}>← Назад</button>
                {snapshot && (
                    <div className="proc-doc-snapshot-title">
                        <strong>{snapshot.processName}</strong>
                        <span className="proc-doc-snapshot-ver">v{snapshot.versionNumber}</span>
                        <span className="proc-doc-snapshot-date">
                            Сформировано {new Date(snapshot.generatedAt).toLocaleString('ru-RU')}
                        </span>
                    </div>
                )}
                <button className="proc-doc-btn-pdf" onClick={onDownloadPdf}>⬇ PDF-регламент</button>
            </div>

            {loadErr && <div className="proc-doc-error">{loadErr}</div>}

            {!snapshot && !loadErr && (
                <div className="proc-doc-snapshot-loading">Загрузка документации…</div>
            )}

            {snapshot && (
                <iframe
                    ref={iframeRef}
                    className="proc-doc-snapshot-iframe"
                    title="Документация процесса"
                    sandbox="allow-same-origin"
                />
            )}
        </div>
    );
}
