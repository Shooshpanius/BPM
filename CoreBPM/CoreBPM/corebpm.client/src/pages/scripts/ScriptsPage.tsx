import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/scriptsApi';
import type {
    BpmProcessVersionScriptInfoDto,
    BpmScriptModuleDto,
    BpmDesignerExtensionDto,
    BpmGlobalModuleDto,
    BpmGlobalModuleFileDto,
} from '../../api/scriptsApi';
import './ScriptsPage.css';

type ScriptsTab = 'processes' | 'extensions' | 'global-modules' | 'live-instances' | 'objects';

const VERSION_STATUS_LABELS: Record<string, string> = {
    Draft: 'Черновик',
    Active: 'Активная',
    Obsolete: 'Устаревшая',
};

/** Главная страница раздела «Сценарии» с тремя вкладками. */
export function ScriptsPage() {
    const { accessToken: token } = useAuth();
    const [tab, setTab] = useState<ScriptsTab>('processes');
    const [organizations, setOrganizations] = useState<{ id: string; name: string }[]>([]);
    const [selectedOrgId, setSelectedOrgId] = useState<string | null>(null);

    // Загрузка организаций
    useEffect(() => {
        if (!token) return;
        fetch('/api/org/directory/organizations', {
            headers: { Authorization: `Bearer ${token}` },
        })
            .then(r => r.ok ? r.json() : Promise.reject(new Error(`HTTP ${r.status}`)))
            .then((orgs: { id: string; name: string }[]) => {
                setOrganizations(orgs);
                if (orgs.length > 0) setSelectedOrgId(orgs[0].id);
            })
            .catch((e: unknown) => {
                console.error('Не удалось загрузить список организаций:', e);
            });
    }, [token]);

    return (
        <div className="scripts-root">
            <div className="scripts-header">
                <h1 className="scripts-title">Сценарии</h1>
                {organizations.length > 1 && (
                    <select
                        className="scripts-org-select"
                        value={selectedOrgId ?? ''}
                        onChange={e => setSelectedOrgId(e.target.value)}
                    >
                        {organizations.map(o => (
                            <option key={o.id} value={o.id}>{o.name}</option>
                        ))}
                    </select>
                )}
            </div>

            <div className="scripts-tabs">
                <button
                    className={`scripts-tab${tab === 'processes' ? ' active' : ''}`}
                    onClick={() => setTab('processes')}
                >
                    Процессы
                </button>
                <button
                    className={`scripts-tab${tab === 'extensions' ? ' active' : ''}`}
                    onClick={() => setTab('extensions')}
                >
                    Расширения
                </button>
                <button
                    className={`scripts-tab${tab === 'global-modules' ? ' active' : ''}`}
                    onClick={() => setTab('global-modules')}
                >
                    Глобальные модули
                </button>
                <button
                    className={`scripts-tab${tab === 'objects' ? ' active' : ''}`}
                    onClick={() => setTab('objects')}
                >
                    Объекты
                </button>
                <button
                    className={`scripts-tab${tab === 'live-instances' ? ' active' : ''}`}
                    onClick={() => setTab('live-instances')}
                >
                    Запущенные экземпляры
                </button>
            </div>

            <div className="scripts-content">
                {tab === 'processes' && selectedOrgId && (
                    <ProcessesScriptsTab orgId={selectedOrgId} />
                )}
                {tab === 'extensions' && selectedOrgId && (
                    <ExtensionsTab orgId={selectedOrgId} />
                )}
                {tab === 'global-modules' && selectedOrgId && (
                    <GlobalModulesTab orgId={selectedOrgId} />
                )}
                {tab === 'objects' && <DictObjectsGuideTab />}
                {tab === 'live-instances' && <LiveInstancesGuideTab />}
                {!selectedOrgId && tab !== 'live-instances' && tab !== 'objects' && (
                    <p className="scripts-empty">Выберите организацию</p>
                )}
            </div>
        </div>
    );
}

// ─── Вкладка «Процессы» ──────────────────────────────────────────────────────

interface ProcessesScriptsTabProps { orgId: string; }

function ProcessesScriptsTab({ orgId }: ProcessesScriptsTabProps) {
    const { accessToken: token } = useAuth();
    const [items, setItems] = useState<BpmProcessVersionScriptInfoDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [selectedVersion, setSelectedVersion] = useState<BpmProcessVersionScriptInfoDto | null>(null);
    const [script, setScript] = useState<BpmScriptModuleDto | null>(null);
    const [scriptBody, setScriptBody] = useState('');
    const [scriptLoading, setScriptLoading] = useState(false);
    const [saving, setSaving] = useState(false);
    const [publishing, setPublishing] = useState(false);
    const [actionMsg, setActionMsg] = useState<string | null>(null);
    // Раскрытые процессы
    const [expandedProcesses, setExpandedProcesses] = useState<Set<string>>(new Set());

    const load = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.listProcessVersionScripts(token, orgId);
            setItems(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, orgId]);

    useEffect(() => { load(); }, [load]);

    const openVersion = async (item: BpmProcessVersionScriptInfoDto) => {
        if (!token) return;
        setSelectedVersion(item);
        setScript(null);
        setScriptBody('');
        setActionMsg(null);
        setScriptLoading(true);
        try {
            const s = await api.getScript(token, item.processId, item.versionId);
            setScript(s);
            setScriptBody(s.scriptBody);
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка загрузки сценария');
        } finally {
            setScriptLoading(false);
        }
    };

    const handleSave = async () => {
        if (!token || !selectedVersion) return;
        setSaving(true);
        setActionMsg(null);
        try {
            const s = await api.saveScript(token, selectedVersion.processId, selectedVersion.versionId, {
                scriptBody,
                language: script?.language ?? 'CSharp',
            });
            setScript(s);
            setActionMsg('Сценарий сохранён');
            load();
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handlePublish = async () => {
        if (!token || !selectedVersion) return;
        setPublishing(true);
        setActionMsg(null);
        try {
            const s = await api.publishScript(token, selectedVersion.processId, selectedVersion.versionId);
            setScript(s);
            setActionMsg(`Сценарий опубликован ${new Date(s.publishedAt!).toLocaleString('ru-RU')}`);
            load();
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка публикации');
        } finally {
            setPublishing(false);
        }
    };

    const toggleProcess = (processId: string) => {
        setExpandedProcesses(prev => {
            const next = new Set(prev);
            if (next.has(processId)) next.delete(processId);
            else next.add(processId);
            return next;
        });
    };

    // Группируем по процессам
    const grouped = items.reduce<Record<string, { name: string; versions: BpmProcessVersionScriptInfoDto[] }>>((acc, item) => {
        if (!acc[item.processId]) acc[item.processId] = { name: item.processName, versions: [] };
        acc[item.processId].versions.push(item);
        return acc;
    }, {});

    return (
        <div className="scripts-pane">
            <div className="scripts-list-col">
                {loading && <p className="scripts-loading">Загрузка…</p>}
                {error && <p className="scripts-error">{error}</p>}
                {!loading && !error && Object.keys(grouped).length === 0 && (
                    <p className="scripts-empty">Нет процессов</p>
                )}
                {Object.entries(grouped).map(([processId, { name, versions }]) => (
                    <div key={processId} className="scripts-process-group">
                        <button
                            className="scripts-process-header"
                            onClick={() => toggleProcess(processId)}
                        >
                            <span className={`scripts-chevron${expandedProcesses.has(processId) ? ' open' : ''}`}>▶</span>
                            <span className="scripts-process-name">{name}</span>
                        </button>
                        {expandedProcesses.has(processId) && (
                            <div className="scripts-version-list">
                                {versions.map(v => (
                                    <button
                                        key={v.versionId}
                                        className={`scripts-version-item${selectedVersion?.versionId === v.versionId ? ' active' : ''}`}
                                        onClick={() => openVersion(v)}
                                    >
                                        <span className="scripts-ver-num">v{v.versionNumber}</span>
                                        <span className={`scripts-ver-status status-${v.versionStatus.toLowerCase()}`}>
                                            {VERSION_STATUS_LABELS[v.versionStatus] ?? v.versionStatus}
                                        </span>
                                        {v.hasScript && (
                                            <span className="scripts-has-script" title="Есть сценарий">{'</>'}</span>
                                        )}
                                    </button>
                                ))}
                            </div>
                        )}
                    </div>
                ))}
            </div>

            <div className="scripts-editor-col">
                {!selectedVersion && (
                    <p className="scripts-empty">Выберите версию процесса для редактирования сценария</p>
                )}
                {selectedVersion && (
                    <>
                        <div className="scripts-editor-header">
                            <span className="scripts-editor-title">
                                {selectedVersion.processName} — v{selectedVersion.versionNumber}
                            </span>
                            {script?.publishedAt && (
                                <span className="scripts-published-badge">
                                    Опубликован: {new Date(script.publishedAt).toLocaleString('ru-RU')}
                                </span>
                            )}
                        </div>
                        {scriptLoading ? (
                            <p className="scripts-loading">Загрузка сценария…</p>
                        ) : (
                            <textarea
                                className="scripts-code-editor"
                                value={scriptBody}
                                onChange={e => setScriptBody(e.target.value)}
                                spellCheck={false}
                                placeholder="// C#-сценарий версии процесса"
                            />
                        )}
                        {actionMsg && <p className="scripts-action-msg">{actionMsg}</p>}
                        <div className="scripts-editor-actions">
                            <button
                                className="scripts-btn-save"
                                onClick={handleSave}
                                disabled={saving || scriptLoading}
                            >
                                {saving ? 'Сохранение…' : 'Сохранить'}
                            </button>
                            <button
                                className="scripts-btn-publish"
                                onClick={handlePublish}
                                disabled={publishing || scriptLoading}
                            >
                                {publishing ? 'Публикация…' : 'Опубликовать'}
                            </button>
                        </div>
                    </>
                )}
            </div>
        </div>
    );
}

// ─── Вкладка «Расширения» ────────────────────────────────────────────────────

interface ExtensionsTabProps { orgId: string; }

type ExtFormMode = 'create' | 'edit';

function ExtensionsTab({ orgId }: ExtensionsTabProps) {
    const { accessToken: token } = useAuth();
    const [items, setItems] = useState<BpmDesignerExtensionDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [expandedFolders, setExpandedFolders] = useState<Set<string>>(new Set(['__root__']));

    // Форма создания/редактирования
    const [formMode, setFormMode] = useState<ExtFormMode>('create');
    const [editItem, setEditItem] = useState<BpmDesignerExtensionDto | null>(null);
    const [showForm, setShowForm] = useState(false);
    const [formName, setFormName] = useState('');
    const [formDesc, setFormDesc] = useState('');
    const [formFolder, setFormFolder] = useState('');
    const [formScript, setFormScript] = useState('');
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);

    const [deleteId, setDeleteId] = useState<string | null>(null);
    const [deleting, setDeleting] = useState(false);
    const [actionMsg, setActionMsg] = useState<string | null>(null);
    const extensionImportRef = useRef<HTMLInputElement>(null);

    const load = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.listExtensions(token, orgId);
            setItems(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, orgId]);

    useEffect(() => { load(); }, [load]);

    const openCreate = () => {
        setFormMode('create');
        setEditItem(null);
        setFormName('');
        setFormDesc('');
        setFormFolder('');
        setFormScript('');
        setFormError(null);
        setShowForm(true);
    };

    const openEdit = (item: BpmDesignerExtensionDto) => {
        setFormMode('edit');
        setEditItem(item);
        setFormName(item.name);
        setFormDesc(item.description ?? '');
        setFormFolder(item.folderPath ?? '');
        setFormScript(item.scriptBody);
        setFormError(null);
        setShowForm(true);
    };

    const handleSave = async () => {
        if (!token) return;
        setSaving(true);
        setFormError(null);
        try {
            if (formMode === 'create') {
                await api.createExtension(token, {
                    organizationId: orgId,
                    name: formName,
                    description: formDesc || undefined,
                    folderPath: formFolder || undefined,
                    scriptBody: formScript,
                });
            } else if (editItem) {
                await api.updateExtension(token, editItem.id, {
                    name: formName,
                    description: formDesc || undefined,
                    folderPath: formFolder || undefined,
                    scriptBody: formScript,
                });
            }
            setShowForm(false);
            load();
        } catch (e) {
            setFormError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async () => {
        if (!token || !deleteId) return;
        setDeleting(true);
        try {
            await api.deleteExtension(token, deleteId);
            setDeleteId(null);
            load();
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка удаления');
        } finally {
            setDeleting(false);
        }
    };

    const handlePublish = async (id: string) => {
        if (!token) return;
        setActionMsg(null);
        try {
            await api.publishExtension(token, id);
            setActionMsg('Расширение опубликовано');
            load();
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка публикации');
        }
    };

    const handleCopy = async (id: string) => {
        if (!token) return;
        setActionMsg(null);
        try {
            await api.copyExtension(token, id);
            setActionMsg('Копия создана');
            load();
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка копирования');
        }
    };

    const toggleFolder = (folder: string) => {
        setExpandedFolders(prev => {
            const next = new Set(prev);
            if (next.has(folder)) next.delete(folder);
            else next.add(folder);
            return next;
        });
    };

    const handleExportExtensions = async () => {
        if (!token) return;
        try {
            const blob = await api.exportExtensions(token, orgId);
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `extensions-${orgId}.json`;
            link.click();
            URL.revokeObjectURL(url);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка экспорта');
        }
    };

    const handleImportExtensions = async (ev: React.ChangeEvent<HTMLInputElement>) => {
        const file = ev.target.files?.[0];
        if (!file || !token) return;
        try {
            const imported = await api.importExtensions(token, orgId, file);
            setItems(imported);
            setActionMsg(`Импортировано ${imported.length} расширений`);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка импорта');
        } finally {
            if (extensionImportRef.current) extensionImportRef.current.value = '';
        }
    };

    // Группировка по папкам
    const grouped = items.reduce<Record<string, BpmDesignerExtensionDto[]>>((acc, item) => {
        const key = item.folderPath ?? '__root__';
        if (!acc[key]) acc[key] = [];
        acc[key].push(item);
        return acc;
    }, {});

    return (
        <div className="scripts-full-col">
            <div className="scripts-toolbar">
                <button className="scripts-btn-primary" onClick={openCreate}>+ Создать расширение</button>
                <button className="scripts-btn-secondary" onClick={handleExportExtensions} title="Экспорт расширений">↑ Экспорт</button>
                <button className="scripts-btn-secondary" onClick={() => extensionImportRef.current?.click()} title="Импорт расширений">↓ Импорт</button>
                <input ref={extensionImportRef} type="file" accept=".json" style={{ display: 'none' }} onChange={handleImportExtensions} />
                {actionMsg && <span className="scripts-action-msg">{actionMsg}</span>}
            </div>

            {loading && <p className="scripts-loading">Загрузка…</p>}
            {error && <p className="scripts-error">{error}</p>}

            <div className="scripts-ext-list">
                {Object.entries(grouped).map(([folder, exts]) => (
                    <div key={folder} className="scripts-folder-group">
                        <button
                            className="scripts-folder-header"
                            onClick={() => toggleFolder(folder)}
                        >
                            <span className={`scripts-chevron${expandedFolders.has(folder) ? ' open' : ''}`}>▶</span>
                            <span>{folder === '__root__' ? '(без папки)' : folder}</span>
                            <span className="scripts-folder-count">{exts.length}</span>
                        </button>
                        {expandedFolders.has(folder) && (
                            <div className="scripts-ext-items">
                                {exts.map(ext => (
                                    <div key={ext.id} className="scripts-ext-item">
                                        <div className="scripts-ext-main">
                                            <span className="scripts-ext-name">{ext.name}</span>
                                            {ext.isPublished && <span className="scripts-badge-published">Опубликовано</span>}
                                        </div>
                                        {ext.description && <p className="scripts-ext-desc">{ext.description}</p>}
                                        <div className="scripts-ext-actions">
                                            <button className="scripts-btn-sm" onClick={() => openEdit(ext)}>Редактировать</button>
                                            {!ext.isPublished && (
                                                <button className="scripts-btn-sm scripts-btn-publish" onClick={() => handlePublish(ext.id)}>Опубликовать</button>
                                            )}
                                            <button className="scripts-btn-sm" onClick={() => handleCopy(ext.id)}>Копировать</button>
                                            <button className="scripts-btn-sm scripts-btn-danger" onClick={() => setDeleteId(ext.id)}>Удалить</button>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                ))}
                {!loading && items.length === 0 && (
                    <p className="scripts-empty">Нет расширений. Создайте первое.</p>
                )}
            </div>

            {/* Модальная форма создания/редактирования */}
            {showForm && (
                <div className="scripts-modal-overlay" onClick={() => setShowForm(false)}>
                    <div className="scripts-modal" onClick={e => e.stopPropagation()}>
                        <h2 className="scripts-modal-title">
                            {formMode === 'create' ? 'Создать расширение' : 'Редактировать расширение'}
                        </h2>
                        <label className="scripts-label">Название *</label>
                        <input
                            className="scripts-input"
                            value={formName}
                            onChange={e => setFormName(e.target.value)}
                            placeholder="Название расширения"
                        />
                        <label className="scripts-label">Описание</label>
                        <input
                            className="scripts-input"
                            value={formDesc}
                            onChange={e => setFormDesc(e.target.value)}
                            placeholder="Краткое описание"
                        />
                        <label className="scripts-label">Папка (группировка)</label>
                        <input
                            className="scripts-input"
                            value={formFolder}
                            onChange={e => setFormFolder(e.target.value)}
                            placeholder="Например: Интеграции/1С"
                        />
                        <label className="scripts-label">Сценарий C#</label>
                        <textarea
                            className="scripts-code-editor scripts-modal-editor"
                            value={formScript}
                            onChange={e => setFormScript(e.target.value)}
                            spellCheck={false}
                            placeholder="// C#-код расширения"
                        />
                        {formError && <p className="scripts-error">{formError}</p>}
                        <div className="scripts-modal-actions">
                            <button className="scripts-btn-primary" onClick={handleSave} disabled={saving}>
                                {saving ? 'Сохранение…' : 'Сохранить'}
                            </button>
                            <button className="scripts-btn-cancel" onClick={() => setShowForm(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Подтверждение удаления */}
            {deleteId && (
                <div className="scripts-modal-overlay" onClick={() => setDeleteId(null)}>
                    <div className="scripts-modal" onClick={e => e.stopPropagation()}>
                        <h2 className="scripts-modal-title">Удалить расширение?</h2>
                        <p>Это действие нельзя отменить.</p>
                        <div className="scripts-modal-actions">
                            <button className="scripts-btn-danger" onClick={handleDelete} disabled={deleting}>
                                {deleting ? 'Удаление…' : 'Удалить'}
                            </button>
                            <button className="scripts-btn-cancel" onClick={() => setDeleteId(null)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

// ─── Вкладка «Глобальные модули» ─────────────────────────────────────────────

interface GlobalModulesTabProps { orgId: string; }

function GlobalModulesTab({ orgId }: GlobalModulesTabProps) {
    const { accessToken: token } = useAuth();
    const [modules, setModules] = useState<BpmGlobalModuleDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [selectedModule, setSelectedModule] = useState<BpmGlobalModuleDto | null>(null);
    const [files, setFiles] = useState<BpmGlobalModuleFileDto[]>([]);
    const [filesLoading, setFilesLoading] = useState(false);
    const [selectedFile, setSelectedFile] = useState<BpmGlobalModuleFileDto | null>(null);
    const [fileBody, setFileBody] = useState('');
    const [saving, setSaving] = useState(false);
    const [actionMsg, setActionMsg] = useState<string | null>(null);

    // Форма модуля
    const [showModuleForm, setShowModuleForm] = useState(false);
    const [moduleFormMode, setModuleFormMode] = useState<'create' | 'edit'>('create');
    const [moduleName, setModuleName] = useState('');
    const [moduleDesc, setModuleDesc] = useState('');
    const [moduleFormError, setModuleFormError] = useState<string | null>(null);
    const [savingModule, setSavingModule] = useState(false);

    const [deleteModuleId, setDeleteModuleId] = useState<string | null>(null);
    const [deletingModule, setDeletingModule] = useState(false);
    const [newFileName, setNewFileName] = useState('');
    const [addingFile, setAddingFile] = useState(false);
    const [deleteFileId, setDeleteFileId] = useState<string | null>(null);
    const [deletingFile, setDeletingFile] = useState(false);
    const moduleImportRef = useRef<HTMLInputElement>(null);

    const loadModules = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await api.listGlobalModules(token, orgId);
            setModules(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, orgId]);

    useEffect(() => { loadModules(); }, [loadModules]);

    const loadFiles = useCallback(async (moduleId: string) => {
        if (!token) return;
        setFilesLoading(true);
        setFiles([]);
        setSelectedFile(null);
        setFileBody('');
        try {
            const data = await api.listGlobalModuleFiles(token, moduleId);
            setFiles(data);
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка загрузки файлов');
        } finally {
            setFilesLoading(false);
        }
    }, [token]);

    const openModule = (mod: BpmGlobalModuleDto) => {
        setSelectedModule(mod);
        setActionMsg(null);
        loadFiles(mod.id);
    };

    const openFile = (file: BpmGlobalModuleFileDto) => {
        setSelectedFile(file);
        setFileBody(file.scriptBody);
    };

    const handleSaveFile = async () => {
        if (!token || !selectedModule || !selectedFile) return;
        setSaving(true);
        setActionMsg(null);
        try {
            const updated = await api.updateGlobalModuleFile(token, selectedModule.id, selectedFile.id, {
                fileName: selectedFile.fileName,
                scriptBody: fileBody,
            });
            setSelectedFile(updated);
            setFiles(prev => prev.map(f => f.id === updated.id ? updated : f));
            setActionMsg('Файл сохранён');
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handlePublishModule = async () => {
        if (!token || !selectedModule) return;
        setActionMsg(null);
        try {
            const updated = await api.publishGlobalModule(token, selectedModule.id);
            setSelectedModule(updated);
            setModules(prev => prev.map(m => m.id === updated.id ? updated : m));
            setActionMsg(`Модуль опубликован ${updated.publishedAt ? new Date(updated.publishedAt).toLocaleString('ru-RU') : ''}`);
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка публикации');
        }
    };

    const handleAddFile = async () => {
        if (!token || !selectedModule || !newFileName.trim()) return;
        setAddingFile(true);
        setActionMsg(null);
        try {
            const file = await api.addGlobalModuleFile(token, selectedModule.id, {
                fileName: newFileName.trim(),
                scriptBody: '',
            });
            setFiles(prev => [...prev, file]);
            setNewFileName('');
            openFile(file);
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка добавления файла');
        } finally {
            setAddingFile(false);
        }
    };

    const handleDeleteFile = async () => {
        if (!token || !selectedModule || !deleteFileId) return;
        setDeletingFile(true);
        try {
            await api.deleteGlobalModuleFile(token, selectedModule.id, deleteFileId);
            setFiles(prev => prev.filter(f => f.id !== deleteFileId));
            if (selectedFile?.id === deleteFileId) {
                setSelectedFile(null);
                setFileBody('');
            }
            setDeleteFileId(null);
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка удаления файла');
        } finally {
            setDeletingFile(false);
        }
    };

    const openCreateModule = () => {
        setModuleFormMode('create');
        setModuleName('');
        setModuleDesc('');
        setModuleFormError(null);
        setShowModuleForm(true);
    };

    const openEditModule = (mod: BpmGlobalModuleDto) => {
        setModuleFormMode('edit');
        setModuleName(mod.name);
        setModuleDesc(mod.description ?? '');
        setModuleFormError(null);
        setShowModuleForm(true);
    };

    const handleSaveModule = async () => {
        if (!token) return;
        setSavingModule(true);
        setModuleFormError(null);
        try {
            if (moduleFormMode === 'create') {
                const created = await api.createGlobalModule(token, {
                    organizationId: orgId,
                    name: moduleName,
                    description: moduleDesc || undefined,
                });
                setModules(prev => [...prev, created]);
                setShowModuleForm(false);
                openModule(created);
            } else if (selectedModule) {
                const updated = await api.updateGlobalModule(token, selectedModule.id, {
                    name: moduleName,
                    description: moduleDesc || undefined,
                });
                setModules(prev => prev.map(m => m.id === updated.id ? updated : m));
                setSelectedModule(updated);
                setShowModuleForm(false);
            }
        } catch (e) {
            setModuleFormError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSavingModule(false);
        }
    };

    const handleDeleteModule = async () => {
        if (!token || !deleteModuleId) return;
        setDeletingModule(true);
        try {
            await api.deleteGlobalModule(token, deleteModuleId);
            setModules(prev => prev.filter(m => m.id !== deleteModuleId));
            if (selectedModule?.id === deleteModuleId) {
                setSelectedModule(null);
                setFiles([]);
                setSelectedFile(null);
            }
            setDeleteModuleId(null);
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка удаления');
        } finally {
            setDeletingModule(false);
        }
    };

    const handleExportModules = async () => {
        if (!token) return;
        try {
            const blob = await api.exportGlobalModules(token, orgId);
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `global-modules-${orgId}.json`;
            link.click();
            URL.revokeObjectURL(url);
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка экспорта');
        }
    };

    const handleImportModules = async (ev: React.ChangeEvent<HTMLInputElement>) => {
        const file = ev.target.files?.[0];
        if (!file || !token) return;
        try {
            const imported = await api.importGlobalModules(token, orgId, file);
            setModules(imported);
            setActionMsg(`Импортировано ${imported.length} модулей`);
        } catch (e) {
            setActionMsg(e instanceof Error ? e.message : 'Ошибка импорта');
        } finally {
            if (moduleImportRef.current) moduleImportRef.current.value = '';
        }
    };

    return (
        <div className="scripts-pane scripts-three-col">
            {/* Список модулей */}
            <div className="scripts-list-col scripts-modules-col">
                <div className="scripts-toolbar">
                    <button className="scripts-btn-primary" onClick={openCreateModule}>+ Создать модуль</button>
                    <button className="scripts-btn-secondary" onClick={handleExportModules} title="Экспорт модулей">↑ Экспорт</button>
                    <button className="scripts-btn-secondary" onClick={() => moduleImportRef.current?.click()} title="Импорт модулей">↓ Импорт</button>
                    <input ref={moduleImportRef} type="file" accept=".json" style={{ display: 'none' }} onChange={handleImportModules} />
                    {actionMsg && <span className="scripts-action-msg">{actionMsg}</span>}
                </div>
                {loading && <p className="scripts-loading">Загрузка…</p>}
                {error && <p className="scripts-error">{error}</p>}
                {!loading && modules.length === 0 && (
                    <p className="scripts-empty">Нет глобальных модулей</p>
                )}
                {modules.map(mod => (
                    <button
                        key={mod.id}
                        className={`scripts-module-item${selectedModule?.id === mod.id ? ' active' : ''}`}
                        onClick={() => openModule(mod)}
                    >
                        <span className="scripts-module-name">{mod.name}</span>
                        <span className="scripts-module-meta">
                            {mod.filesCount} файл(ов)
                            {mod.isPublished && <span className="scripts-badge-published"> • Опубл.</span>}
                        </span>
                    </button>
                ))}
            </div>

            {/* Список файлов модуля */}
            <div className="scripts-list-col scripts-files-col">
                {!selectedModule && <p className="scripts-empty">Выберите модуль</p>}
                {selectedModule && (
                    <>
                        <div className="scripts-module-toolbar">
                            <span className="scripts-module-selected-name">{selectedModule.name}</span>
                            <div className="scripts-module-actions">
                                <button className="scripts-btn-sm" onClick={() => openEditModule(selectedModule)}>Переим.</button>
                                <button className="scripts-btn-sm scripts-btn-publish" onClick={handlePublishModule}>Опубликовать</button>
                                <button className="scripts-btn-sm scripts-btn-danger" onClick={() => setDeleteModuleId(selectedModule.id)}>Удалить</button>
                            </div>
                        </div>
                        {selectedModule.publishedAt && (
                            <p className="scripts-published-badge">Опубликован: {new Date(selectedModule.publishedAt).toLocaleString('ru-RU')}</p>
                        )}
                        {filesLoading && <p className="scripts-loading">Загрузка файлов…</p>}
                        {files.map(file => (
                            <button
                                key={file.id}
                                className={`scripts-file-item${selectedFile?.id === file.id ? ' active' : ''}`}
                                onClick={() => openFile(file)}
                            >
                                <span>{file.fileName}</span>
                                <button
                                    className="scripts-btn-icon scripts-btn-danger"
                                    onClick={e => { e.stopPropagation(); setDeleteFileId(file.id); }}
                                    title="Удалить файл"
                                >✕</button>
                            </button>
                        ))}
                        <div className="scripts-add-file-row">
                            <input
                                className="scripts-input scripts-input-sm"
                                value={newFileName}
                                onChange={e => setNewFileName(e.target.value)}
                                placeholder="Helpers.cs"
                                onKeyDown={e => e.key === 'Enter' && handleAddFile()}
                            />
                            <button className="scripts-btn-sm scripts-btn-primary" onClick={handleAddFile} disabled={addingFile || !newFileName.trim()}>
                                {addingFile ? '…' : '+ Файл'}
                            </button>
                        </div>
                    </>
                )}
            </div>

            {/* Редактор файла */}
            <div className="scripts-editor-col">
                {!selectedFile && <p className="scripts-empty">Выберите файл для редактирования</p>}
                {selectedFile && (
                    <>
                        <div className="scripts-editor-header">
                            <span className="scripts-editor-title">{selectedFile.fileName}</span>
                        </div>
                        <textarea
                            className="scripts-code-editor"
                            value={fileBody}
                            onChange={e => setFileBody(e.target.value)}
                            spellCheck={false}
                            placeholder="// C#-код файла глобального модуля"
                        />
                        {actionMsg && <p className="scripts-action-msg">{actionMsg}</p>}
                        <div className="scripts-editor-actions">
                            <button className="scripts-btn-save" onClick={handleSaveFile} disabled={saving}>
                                {saving ? 'Сохранение…' : 'Сохранить файл'}
                            </button>
                        </div>
                    </>
                )}
                {!selectedFile && actionMsg && <p className="scripts-action-msg">{actionMsg}</p>}
            </div>

            {/* Форма модуля */}
            {showModuleForm && (
                <div className="scripts-modal-overlay" onClick={() => setShowModuleForm(false)}>
                    <div className="scripts-modal" onClick={e => e.stopPropagation()}>
                        <h2 className="scripts-modal-title">
                            {moduleFormMode === 'create' ? 'Создать глобальный модуль' : 'Переименовать модуль'}
                        </h2>
                        <label className="scripts-label">Название *</label>
                        <input className="scripts-input" value={moduleName} onChange={e => setModuleName(e.target.value)} placeholder="Название модуля" />
                        <label className="scripts-label">Описание</label>
                        <input className="scripts-input" value={moduleDesc} onChange={e => setModuleDesc(e.target.value)} placeholder="Описание" />
                        {moduleFormError && <p className="scripts-error">{moduleFormError}</p>}
                        <div className="scripts-modal-actions">
                            <button className="scripts-btn-primary" onClick={handleSaveModule} disabled={savingModule}>
                                {savingModule ? 'Сохранение…' : 'Сохранить'}
                            </button>
                            <button className="scripts-btn-cancel" onClick={() => setShowModuleForm(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Подтверждение удаления модуля */}
            {deleteModuleId && (
                <div className="scripts-modal-overlay" onClick={() => setDeleteModuleId(null)}>
                    <div className="scripts-modal" onClick={e => e.stopPropagation()}>
                        <h2 className="scripts-modal-title">Удалить глобальный модуль?</h2>
                        <p>Все файлы модуля будут удалены. Действие нельзя отменить.</p>
                        <div className="scripts-modal-actions">
                            <button className="scripts-btn-danger" onClick={handleDeleteModule} disabled={deletingModule}>
                                {deletingModule ? 'Удаление…' : 'Удалить'}
                            </button>
                            <button className="scripts-btn-cancel" onClick={() => setDeleteModuleId(null)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Подтверждение удаления файла */}
            {deleteFileId && (
                <div className="scripts-modal-overlay" onClick={() => setDeleteFileId(null)}>
                    <div className="scripts-modal" onClick={e => e.stopPropagation()}>
                        <h2 className="scripts-modal-title">Удалить файл?</h2>
                        <div className="scripts-modal-actions">
                            <button className="scripts-btn-danger" onClick={handleDeleteFile} disabled={deletingFile}>
                                {deletingFile ? 'Удаление…' : 'Удалить'}
                            </button>
                            <button className="scripts-btn-cancel" onClick={() => setDeleteFileId(null)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

// ─── Вкладка «Запущенные экземпляры» ────────────────────────────────────────

/**
 * LiveInstancesGuideTab — инструкция по редактированию сценариев запущенных экземпляров.
 * Будет расширена после реализации FR-BPM-02.5 (движок выполнения процессов).
 */
function LiveInstancesGuideTab() {
    return (
        <div className="scripts-live-guide">
            <div className="scripts-live-guide-header">
                <h2>Редактирование сценариев запущенных экземпляров</h2>
                <span className="scripts-live-guide-badge">Ожидает FR-BPM-02.5</span>
            </div>

            <div className="scripts-live-guide-section">
                <h3>Что это?</h3>
                <p>
                    Данная функциональность позволит вносить исправления в сценарии (скрипты C#)
                    непосредственно во время работы запущенных экземпляров процесса — без
                    необходимости создавать новую версию и публиковать её.
                </p>
            </div>

            <div className="scripts-live-guide-section">
                <h3>Как это будет работать</h3>
                <ol className="scripts-live-guide-list">
                    <li>
                        Откройте вкладку «Процессы» и выберите версию с запущенными экземплярами.
                    </li>
                    <li>
                        В редакторе кода нажмите «Применить к запущенным экземплярам» — изменения
                        применяются только к экземплярам, ещё не перешедшим через Script Task.
                    </li>
                    <li>
                        Система создаёт снапшот «патча» для экземпляров: при следующем
                        выполнении Script Task будет использован обновлённый код.
                    </li>
                    <li>
                        История патчей доступна на странице конкретного экземпляра (FR-BPM-02).
                    </li>
                </ol>
            </div>

            <div className="scripts-live-guide-section">
                <h3>Зависимости</h3>
                <ul className="scripts-live-guide-list">
                    <li>
                        <strong>FR-BPM-02.5</strong> — движок выполнения процессов должен поддерживать
                        механизм «патчей» (горячая замена кода без перезапуска экземпляра).
                    </li>
                    <li>
                        <strong>FR-BPM-02</strong> — необходима реализация хранилища и API экземпляров
                        для отображения информации о состоянии.
                    </li>
                </ul>
            </div>

            <div className="scripts-live-guide-section scripts-live-guide-section--info">
                <p>
                    До реализации FR-BPM-02.5 для внесения изменений создавайте новую версию процесса
                    через раздел «Процессы» → «Версии» → «Откат» или публикацию нового черновика.
                </p>
            </div>
        </div>
    );
}

/**
 * DictObjectsGuideTab — вкладка «Объекты» (просмотр типов объектов из FR-DICT).
 * Отображает информацию о доступных типах объектов для использования в сценариях.
 * Полная интеграция будет реализована после FR-DICT.
 */
function DictObjectsGuideTab() {
    return (
        <div className="scripts-live-guide">
            <div className="scripts-live-guide-header">
                <h2>Объекты (справочники)</h2>
                <span className="scripts-live-guide-badge">Ожидает FR-DICT</span>
            </div>

            <div className="scripts-live-guide-section scripts-live-guide-section--info">
                <p>
                    Вкладка «Объекты» позволит просматривать типы объектов из модуля справочников
                    (FR-DICT) и использовать их в сценариях процессов.
                </p>
            </div>

            <div className="scripts-live-guide-section">
                <h3>Что будет доступно</h3>
                <ul className="scripts-live-guide-list">
                    <li>
                        <strong>Просмотр типов объектов</strong> — список всех типов объектов,
                        доступных в системе: название, код, поля, связи.
                    </li>
                    <li>
                        <strong>Генерация C#-классов</strong> — автоматическое создание типизированных
                        C# DTO для использования в скриптах процессов.
                    </li>
                    <li>
                        <strong>CRUD-операции в сценариях</strong> — примеры кода для чтения,
                        создания и обновления экземпляров объектов через SDK сценариев.
                    </li>
                    <li>
                        <strong>Ссылочные переменные</strong> — переменные процесса типа
                        <code> Object</code> могут ссылаться на экземпляр объекта справочника.
                    </li>
                </ul>
            </div>

            <div className="scripts-live-guide-section">
                <h3>Пример использования (предварительный)</h3>
                <pre className="scripts-live-guide-code">{`// Получение экземпляра объекта в сценарии
var client = await Context.Objects
    .GetAsync<CrmClient>(Variables["ClientId"]);

// Обновление поля
client.Status = "Active";
await Context.Objects.SaveAsync(client);

// Запись результата в переменную
Variables["ClientName"] = client.Name;`}</pre>
            </div>

            <div className="scripts-live-guide-section">
                <h3>Зависимости</h3>
                <ul className="scripts-live-guide-list">
                    <li>
                        <strong>FR-DICT</strong> — модуль справочников (объектная модель) должен быть
                        реализован для получения списка типов и их схем.
                    </li>
                    <li>
                        <strong>FR-BPM SDK</strong> — методы <code>Context.Objects.*</code> будут
                        добавлены в SDK сценариев при реализации движка выполнения.
                    </li>
                </ul>
            </div>
        </div>
    );
}
