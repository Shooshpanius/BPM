import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/bpmApi';
import type {
    BpmInstanceDto,
    BpmInstanceHistoryEntryDto,
    BpmInstanceParticipantDto,
    BpmHistoryEventType,
    BpmProcessVersionInfoDto,
    BpmTokenDto,
} from '../../api/bpmApi';
import { getDirectoryEmployees } from '../../api/orgDirectoryApi';
import './InstancePage.css';

// ─── Вспомогательные функции ─────────────────────────────────────────────────

function formatDateTime(iso: string): string {
    try {
        return new Date(iso).toLocaleString('ru-RU', {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit',
        });
    } catch { return iso; }
}

function formatTimeAgo(iso: string): string {
    try {
        const diff = Date.now() - new Date(iso).getTime();
        if (diff < 60_000) return 'только что';
        if (diff < 3_600_000) return `${Math.floor(diff / 60_000)} мин назад`;
        if (diff < 86_400_000) return `${Math.floor(diff / 3_600_000)} ч назад`;
        return formatDateTime(iso);
    } catch { return iso; }
}

const STATE_LABELS: Record<api.BpmInstanceState, string> = {
    Active: 'Выполняется',
    Completed: 'Завершён',
    Cancelled: 'Прерван',
    Suspended: 'Приостановлен',
    Faulted: 'Ошибка',
};

const STATE_CLASS: Record<api.BpmInstanceState, string> = {
    Active: 'inst-state--active',
    Completed: 'inst-state--completed',
    Cancelled: 'inst-state--cancelled',
    Suspended: 'inst-state--suspended',
    Faulted: 'inst-state--faulted',
};

const HISTORY_ICONS: Record<BpmHistoryEventType, string> = {
    Started: '▶',
    Cancelled: '✕',
    Completed: '✓',
    Suspended: '⏸',
    Resumed: '▶',
    ResponsibleChanged: '👤',
    CommentAdded: '💬',
    QuestionAdded: '❓',
    VariableUpdated: '✏️',
    ParticipantAdded: '➕',
    ParticipantRemoved: '➖',
    NodeExecuted: '⚙️',
    NodeFailed: '⚠️',
};

const HISTORY_LABELS: Record<BpmHistoryEventType, string> = {
    Started: 'Запуск процесса',
    Cancelled: 'Экземпляр прерван',
    Completed: 'Экземпляр завершён',
    Suspended: 'Приостановлено',
    Resumed: 'Возобновлено',
    ResponsibleChanged: 'Ответственный изменён',
    CommentAdded: 'Комментарий',
    QuestionAdded: 'Вопрос',
    VariableUpdated: 'Переменная изменена',
    ParticipantAdded: 'Участник добавлен',
    ParticipantRemoved: 'Участник удалён',
    NodeExecuted: 'Узел выполнен',
    NodeFailed: 'Ошибка узла',
};

type TabId = 'overview' | 'variables' | 'history' | 'participants';

// ─── Пропсы ───────────────────────────────────────────────────────────────────

interface Props {
    instanceId: string;
    onBack: () => void;
}

// ─── Компонент ────────────────────────────────────────────────────────────────

/**
 * InstancePage — страница экземпляра бизнес-процесса.
 * Вкладки: Обзор, Переменные, История, Участники.
 * Поддерживает: прерывание, приостановку/возобновление, смену ответственного,
 * редактирование переменных, добавление комментариев/вопросов, управление участниками.
 */
export function InstancePage({ instanceId, onBack }: Props) {
    const { accessToken: token, userId } = useAuth();

    const [activeTab, setActiveTab] = useState<TabId>('overview');
    const [instance, setInstance] = useState<BpmInstanceDto | null>(null);
    const [history, setHistory] = useState<BpmInstanceHistoryEntryDto[]>([]);
    const [participants, setParticipants] = useState<BpmInstanceParticipantDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // ─── Диалог прерывания ────────────────────────────────────────────────────
    const [showCancel, setShowCancel] = useState(false);
    const [cancelReason, setCancelReason] = useState('');
    const [cancelling, setCancelling] = useState(false);
    const [cancelError, setCancelError] = useState<string | null>(null);

    // ─── Диалог смены ответственного ─────────────────────────────────────────
    const [showResponsible, setShowResponsible] = useState(false);
    const [newResponsibleId, setNewResponsibleId] = useState<string | null>(null);
    const [newResponsibleName, setNewResponsibleName] = useState<string>('');
    const [responsibleSaving, setResponsibleSaving] = useState(false);

    // ─── Inline-редактирование переменных ────────────────────────────────────
    const [editingVar, setEditingVar] = useState<string | null>(null);
    const [editVarValue, setEditVarValue] = useState('');
    const [savingVar, setSavingVar] = useState(false);

    // ─── Комментарий ─────────────────────────────────────────────────────────
    const [commentText, setCommentText] = useState('');
    const [isQuestion, setIsQuestion] = useState(false);
    const [sendingComment, setSendingComment] = useState(false);

    // ─── Добавление участника ────────────────────────────────────────────────
    const [newParticipantId, setNewParticipantId] = useState<string | null>(null);
    const [newParticipantName, setNewParticipantName] = useState('');
    const [addingParticipant, setAddingParticipant] = useState(false);

    // ─── Переключение версии ─────────────────────────────────────────────────
    const [showSwitchVersion, setShowSwitchVersion] = useState(false);
    const [versions, setVersions] = useState<BpmProcessVersionInfoDto[]>([]);
    const [selectedVersionId, setSelectedVersionId] = useState('');
    const [switchingVersion, setSwitchingVersion] = useState(false);
    const [switchVersionError, setSwitchVersionError] = useState<string | null>(null);

    // ─── Токены выполнения ────────────────────────────────────────────────────
    const [tokens, setTokens] = useState<BpmTokenDto[]>([]);
    const [completingToken, setCompletingToken] = useState<string | null>(null);

    const isActive = instance?.state === 'Active';
    const isSuspended = instance?.state === 'Suspended';
    const canManage = instance?.state !== 'Cancelled' && instance?.state !== 'Completed';

    // ─── Загрузка данных ──────────────────────────────────────────────────────

    const loadInstance = useCallback(async () => {
        if (!token) return;
        try {
            const inst = await api.getInstance(token, instanceId);
            setInstance(inst);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки экземпляра');
        }
    }, [token, instanceId]);

    const loadHistory = useCallback(async () => {
        if (!token) return;
        try {
            setHistory(await api.getInstanceHistory(token, instanceId));
        } catch { /* не критично */ }
    }, [token, instanceId]);

    const loadParticipants = useCallback(async () => {
        if (!token) return;
        try {
            setParticipants(await api.getParticipants(token, instanceId));
        } catch { /* не критично */ }
    }, [token, instanceId]);

    const loadTokens = useCallback(async () => {
        if (!token) return;
        try {
            setTokens(await api.getTokens(token, instanceId));
        } catch { /* не критично */ }
    }, [token, instanceId]);

    const loadAll = useCallback(async () => {
        setLoading(true);
        setError(null);
        await Promise.all([loadInstance(), loadHistory(), loadParticipants(), loadTokens()]);
        setLoading(false);
    }, [loadInstance, loadHistory, loadParticipants, loadTokens]);

    useEffect(() => { loadAll(); }, [loadAll]);

    // ─── Действия ─────────────────────────────────────────────────────────────

    const handleCancel = async () => {
        if (!token || !cancelReason.trim()) { setCancelError('Введите причину'); return; }
        setCancelling(true);
        setCancelError(null);
        try {
            const updated = await api.cancelInstance(token, instanceId, { reason: cancelReason.trim() });
            setInstance(updated);
            await loadHistory();
            setShowCancel(false);
            setCancelReason('');
        } catch (e) {
            setCancelError(e instanceof Error ? e.message : 'Ошибка');
        } finally { setCancelling(false); }
    };

    const handleSuspend = async () => {
        if (!token) return;
        try {
            const updated = await api.suspendInstance(token, instanceId);
            setInstance(updated);
            await loadHistory();
        } catch (e) { setError(e instanceof Error ? e.message : 'Ошибка'); }
    };

    const handleResume = async () => {
        if (!token) return;
        try {
            const updated = await api.resumeInstance(token, instanceId);
            setInstance(updated);
            await loadHistory();
        } catch (e) { setError(e instanceof Error ? e.message : 'Ошибка'); }
    };

    const handleChangeResponsible = async () => {
        if (!token || !newResponsibleId) return;
        setResponsibleSaving(true);
        try {
            const updated = await api.changeResponsible(token, instanceId, { newResponsibleUserId: newResponsibleId });
            setInstance(updated);
            await loadHistory();
            setShowResponsible(false);
            setNewResponsibleId(null);
            setNewResponsibleName('');
        } catch (e) { setError(e instanceof Error ? e.message : 'Ошибка'); }
        finally { setResponsibleSaving(false); }
    };

    const handleSaveVar = async (varName: string) => {
        if (!token) return;
        setSavingVar(true);
        try {
            const updated = await api.updateInstanceVariable(token, instanceId, varName, { valueJson: editVarValue || null });
            setInstance(prev => prev ? {
                ...prev,
                variables: prev.variables.map(v => v.name === varName ? { ...v, valueJson: updated.valueJson } : v),
            } : null);
            await loadHistory();
            setEditingVar(null);
        } catch (e) { setError(e instanceof Error ? e.message : 'Ошибка сохранения'); }
        finally { setSavingVar(false); }
    };

    const handleAddComment = async () => {
        if (!token || !commentText.trim()) return;
        setSendingComment(true);
        try {
            await api.addComment(token, instanceId, { text: commentText.trim(), isQuestion });
            setCommentText('');
            setIsQuestion(false);
            await loadHistory();
        } catch (e) { setError(e instanceof Error ? e.message : 'Ошибка'); }
        finally { setSendingComment(false); }
    };

    const handleAddParticipant = async () => {
        if (!token || !newParticipantId) return;
        setAddingParticipant(true);
        try {
            const p = await api.addParticipant(token, instanceId, { userId: newParticipantId });
            setParticipants(prev => [...prev, p]);
            await loadHistory();
            setNewParticipantId(null);
            setNewParticipantName('');
        } catch (e) { setError(e instanceof Error ? e.message : 'Ошибка'); }
        finally { setAddingParticipant(false); }
    };

    const handleRemoveParticipant = async (pUserId: string) => {
        if (!token) return;
        try {
            await api.removeParticipant(token, instanceId, pUserId);
            setParticipants(prev => prev.filter(p => p.userId !== pUserId));
            await loadHistory();
        } catch (e) { setError(e instanceof Error ? e.message : 'Ошибка'); }
    };

    // ─── Завершение UserTask ──────────────────────────────────────────────────

    const handleCompleteToken = async (elementId: string) => {
        if (!token) return;
        setCompletingToken(elementId);
        try {
            await api.completeToken(token, instanceId, elementId, {});
            await loadAll();
        } catch (e) { setError(e instanceof Error ? e.message : 'Ошибка выполнения задания'); }
        finally { setCompletingToken(null); }
    };

    // ─── Переключение версии ─────────────────────────────────────────────────

    const handleOpenSwitchVersion = async () => {
        if (!token || !instance) return;
        try {
            const vers = await api.getProcessVersions(token, instance.processId);
            // Показываем только опубликованные (Active) версии, кроме текущей
            const active = vers.filter(v => v.status === 'Active' && v.id !== instance.processVersionId);
            setVersions(active);
            setSelectedVersionId(active.length > 0 ? active[0].id : '');
            setSwitchVersionError(null);
            setShowSwitchVersion(true);
        } catch (e) { setError(e instanceof Error ? e.message : 'Ошибка загрузки версий'); }
    };

    const handleSwitchVersion = async () => {
        if (!token || !selectedVersionId) return;
        setSwitchingVersion(true);
        setSwitchVersionError(null);
        try {
            const updated = await api.switchInstanceVersion(token, instanceId, { targetVersionId: selectedVersionId });
            setInstance(updated);
            await loadHistory();
            setShowSwitchVersion(false);
        } catch (e) {
            setSwitchVersionError(e instanceof Error ? e.message : 'Ошибка переключения версии');
        } finally { setSwitchingVersion(false); }
    };

    // ─── Рендер ───────────────────────────────────────────────────────────────

    if (loading) return <div className="inst-root"><div className="inst-loading">Загрузка…</div></div>;
    if (!instance) return <div className="inst-root"><div className="inst-error">{error ?? 'Экземпляр не найден'}</div></div>;

    return (
        <div className="inst-root">
            {/* Шапка */}
            <div className="inst-header">
                <button className="inst-back-btn" onClick={onBack}>← Назад</button>
                <div className="inst-header-title">
                    <p className="inst-title">{instance.name}</p>
                    <p className="inst-subtitle">{instance.processName} · v{instance.processVersionNumber}</p>
                </div>
                <span className={`inst-state ${STATE_CLASS[instance.state]}`}>
                    {STATE_LABELS[instance.state]}
                </span>
                {/* Действия */}
                {canManage && isActive && (
                    <button className="inst-btn-secondary" onClick={handleSuspend} title="Приостановить">
                        ⏸ Приостановить
                    </button>
                )}
                {canManage && isSuspended && (
                    <button className="inst-btn-primary" onClick={handleResume} title="Возобновить">
                        ▶ Возобновить
                    </button>
                )}
                {canManage && (
                    <button className="inst-btn-secondary" onClick={() => setShowResponsible(true)}>
                        👤 Ответственный
                    </button>
                )}
                {canManage && (
                    <button className="inst-btn-secondary" onClick={handleOpenSwitchVersion} title="Переключить версию процесса">
                        🔀 Версия
                    </button>
                )}
                {canManage && (
                    <button className="inst-btn-danger" onClick={() => setShowCancel(true)}>
                        ✕ Прервать
                    </button>
                )}
            </div>

            {/* Вкладки */}
            <div className="inst-tabs" role="tablist">
                {(['overview', 'variables', 'history', 'participants'] as TabId[]).map(tab => (
                    <button
                        key={tab}
                        className={`inst-tab${activeTab === tab ? ' inst-tab--active' : ''}`}
                        onClick={() => setActiveTab(tab)}
                        role="tab"
                        aria-selected={activeTab === tab}
                    >
                        {TAB_LABELS[tab]}
                        {tab === 'history' && history.length > 0 && (
                            <span style={{ marginLeft: 6, fontSize: 11, background: '#e5e7eb', borderRadius: 99, padding: '1px 6px' }}>
                                {history.length}
                            </span>
                        )}
                        {tab === 'participants' && participants.length > 0 && (
                            <span style={{ marginLeft: 6, fontSize: 11, background: '#e5e7eb', borderRadius: 99, padding: '1px 6px' }}>
                                {participants.length}
                            </span>
                        )}
                    </button>
                ))}
            </div>

            {/* Контент */}
            <div className="inst-body">
                {error && <div className="inst-error">{error}</div>}

                {activeTab === 'overview' && (
                    <OverviewTab
                        instance={instance}
                        tokens={tokens}
                        onCompleteToken={canManage ? handleCompleteToken : undefined}
                        completingToken={completingToken}
                    />
                )}

                {activeTab === 'variables' && (
                    <VariablesTab
                        instance={instance}
                        canEdit={canManage}
                        editingVar={editingVar}
                        editVarValue={editVarValue}
                        savingVar={savingVar}
                        onStartEdit={(name, val) => { setEditingVar(name); setEditVarValue(val ?? ''); }}
                        onCancelEdit={() => setEditingVar(null)}
                        onSave={handleSaveVar}
                        onChangeValue={setEditVarValue}
                    />
                )}

                {activeTab === 'history' && (
                    <HistoryTab
                        history={history}
                        commentText={commentText}
                        isQuestion={isQuestion}
                        sendingComment={sendingComment}
                        canComment={canManage}
                        onCommentChange={setCommentText}
                        onQuestionToggle={() => setIsQuestion(q => !q)}
                        onSendComment={handleAddComment}
                    />
                )}

                {activeTab === 'participants' && (
                    <ParticipantsTab
                        participants={participants}
                        currentUserId={userId ?? undefined}
                        newParticipantId={newParticipantId}
                        newParticipantName={newParticipantName}
                        addingParticipant={addingParticipant}
                        canManage={canManage}
                        token={token ?? ''}
                        onSelectUser={(id, name) => { setNewParticipantId(id); setNewParticipantName(name); }}
                        onAddParticipant={handleAddParticipant}
                        onRemoveParticipant={handleRemoveParticipant}
                    />
                )}
            </div>

            {/* Диалог прерывания */}
            {showCancel && (
                <div className="inst-modal-overlay" onClick={() => setShowCancel(false)}>
                    <div className="inst-modal" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
                        <h2 className="inst-modal-title">Прервать экземпляр</h2>
                        <div className="inst-modal-field">
                            <label htmlFor="cancel-reason">Причина прерывания *</label>
                            <textarea
                                id="cancel-reason"
                                className="inst-modal-input"
                                rows={3}
                                value={cancelReason}
                                onChange={e => setCancelReason(e.target.value)}
                                placeholder="Укажите причину прерывания процесса…"
                                autoFocus
                                style={{ resize: 'vertical', fontFamily: 'inherit' }}
                            />
                        </div>
                        {cancelError && <div className="inst-error">{cancelError}</div>}
                        <div className="inst-modal-actions">
                            <button className="inst-btn-secondary" onClick={() => setShowCancel(false)} disabled={cancelling}>Отмена</button>
                            <button className="inst-btn-danger" onClick={handleCancel} disabled={cancelling || !cancelReason.trim()}>
                                {cancelling ? 'Прерывание…' : 'Прервать'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог смены ответственного */}
            {showResponsible && (
                <div className="inst-modal-overlay" onClick={() => setShowResponsible(false)}>
                    <div className="inst-modal" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
                        <h2 className="inst-modal-title">Изменить ответственного</h2>
                        <div className="inst-modal-field">
                            <label>Новый ответственный</label>
                            {token && (
                                <UserSearch
                                    token={token}
                                    value={newResponsibleName}
                                    onSelect={(id, name) => { setNewResponsibleId(id); setNewResponsibleName(name); }}
                                    placeholder="Начните вводить имя…"
                                />
                            )}
                        </div>
                        <div className="inst-modal-actions">
                            <button className="inst-btn-secondary" onClick={() => setShowResponsible(false)} disabled={responsibleSaving}>Отмена</button>
                            <button
                                className="inst-btn-primary"
                                onClick={handleChangeResponsible}
                                disabled={responsibleSaving || !newResponsibleId}
                            >
                                {responsibleSaving ? 'Сохранение…' : 'Сохранить'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог переключения версии */}
            {showSwitchVersion && (
                <div className="inst-modal-overlay" onClick={() => setShowSwitchVersion(false)}>
                    <div className="inst-modal" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
                        <h2 className="inst-modal-title">Переключить версию процесса</h2>
                        {versions.length === 0 ? (
                            <p style={{ color: '#6b7280', marginBottom: 16 }}>
                                Нет других опубликованных версий для переключения.
                            </p>
                        ) : (
                            <div className="inst-modal-field">
                                <label htmlFor="switch-version-select">
                                    Выберите целевую версию
                                </label>
                                <select
                                    id="switch-version-select"
                                    className="inst-modal-input"
                                    value={selectedVersionId}
                                    onChange={e => setSelectedVersionId(e.target.value)}
                                >
                                    {versions.map(v => (
                                        <option key={v.id} value={v.id}>
                                            v{v.versionNumber}
                                            {v.releaseNotes ? ` — ${v.releaseNotes}` : ''}
                                            {v.publishedAt ? ` (${new Date(v.publishedAt).toLocaleDateString('ru-RU')})` : ''}
                                        </option>
                                    ))}
                                </select>
                                <p style={{ fontSize: 12, color: '#9ca3af', marginTop: 6 }}>
                                    Текущая версия: v{instance.processVersionNumber}
                                </p>
                            </div>
                        )}
                        {switchVersionError && <div className="inst-error">{switchVersionError}</div>}
                        <div className="inst-modal-actions">
                            <button className="inst-btn-secondary" onClick={() => setShowSwitchVersion(false)} disabled={switchingVersion}>
                                Отмена
                            </button>
                            {versions.length > 0 && (
                                <button
                                    className="inst-btn-primary"
                                    onClick={handleSwitchVersion}
                                    disabled={switchingVersion || !selectedVersionId}
                                >
                                    {switchingVersion ? 'Переключение…' : 'Переключить'}
                                </button>
                            )}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

const TAB_LABELS: Record<TabId, string> = {
    overview: 'Обзор',
    variables: 'Переменные',
    history: 'История',
    participants: 'Участники',
};

// ─── Вкладка «Обзор» ─────────────────────────────────────────────────────────

const TOKEN_STATUS_LABELS: Record<api.BpmTokenStatus, string> = {
    Active: 'Активен',
    WaitingUserAction: 'Ожидает действия',
    WaitingSignal: 'Ожидает сигнал',
    WaitingMessage: 'Ожидает сообщение',
    Completed: 'Завершён',
};

const TOKEN_STATUS_CLASS: Record<api.BpmTokenStatus, string> = {
    Active: 'inst-token-active',
    WaitingUserAction: 'inst-token-waiting',
    WaitingSignal: 'inst-token-signal',
    WaitingMessage: 'inst-token-message',
    Completed: 'inst-token-completed',
};

interface OverviewTabProps {
    instance: BpmInstanceDto;
    tokens: BpmTokenDto[];
    onCompleteToken?: (elementId: string) => void;
    completingToken: string | null;
}

function OverviewTab({ instance, tokens, onCompleteToken, completingToken }: OverviewTabProps) {
    return (
        <div className="inst-overview-grid">
            <div className="inst-info-card">
                <p className="inst-info-card-title">Основная информация</p>
                <div className="inst-info-row">
                    <span className="inst-info-label">Название</span>
                    <span className="inst-info-value">{instance.name}</span>
                </div>
                <div className="inst-info-row">
                    <span className="inst-info-label">Процесс</span>
                    <span className="inst-info-value">{instance.processName}</span>
                </div>
                <div className="inst-info-row">
                    <span className="inst-info-label">Версия</span>
                    <span className="inst-info-value">v{instance.processVersionNumber}</span>
                </div>
                <div className="inst-info-row">
                    <span className="inst-info-label">Источник запуска</span>
                    <span className="inst-info-value">{LAUNCH_LABELS[instance.launchSource] ?? instance.launchSource}</span>
                </div>
                {instance.parentInstanceId && (
                    <div className="inst-info-row">
                        <span className="inst-info-label">Родительский экземпляр</span>
                        <span className="inst-info-value" style={{ fontSize: 11, fontFamily: 'monospace' }}>
                            {instance.parentInstanceId.slice(0, 16)}…
                        </span>
                    </div>
                )}
                {instance.externalReference && (
                    <div className="inst-info-row">
                        <span className="inst-info-label">Внешний ID</span>
                        <span className="inst-info-value">{instance.externalReference}</span>
                    </div>
                )}
                {instance.cancelReason && (
                    <div className="inst-info-row">
                        <span className="inst-info-label">Причина прерывания</span>
                        <span className="inst-info-value" style={{ color: '#dc2626' }}>{instance.cancelReason}</span>
                    </div>
                )}
            </div>

            <div className="inst-info-card">
                <p className="inst-info-card-title">Участники и сроки</p>
                <div className="inst-info-row">
                    <span className="inst-info-label">Инициатор</span>
                    <span className="inst-info-value">{instance.initiatorDisplayName ?? '—'}</span>
                </div>
                <div className="inst-info-row">
                    <span className="inst-info-label">Ответственный</span>
                    <span className="inst-info-value">{instance.responsibleDisplayName ?? '—'}</span>
                </div>
                <div className="inst-info-row">
                    <span className="inst-info-label">Запущен</span>
                    <span className="inst-info-value">{formatDateTime(instance.startedAt)}</span>
                </div>
                {instance.completedAt && (
                    <div className="inst-info-row">
                        <span className="inst-info-label">Завершён</span>
                        <span className="inst-info-value">{formatDateTime(instance.completedAt)}</span>
                    </div>
                )}
                {instance.cancelledAt && (
                    <div className="inst-info-row">
                        <span className="inst-info-label">Прерван</span>
                        <span className="inst-info-value" style={{ color: '#dc2626' }}>{formatDateTime(instance.cancelledAt)}</span>
                    </div>
                )}
                <div className="inst-info-row">
                    <span className="inst-info-label">Обновлён</span>
                    <span className="inst-info-value">{formatDateTime(instance.updatedAt)}</span>
                </div>
            </div>

            {/* Блок активных токенов */}
            {tokens.length > 0 && (
                <div className="inst-info-card inst-tokens-card">
                    <p className="inst-info-card-title">Активные токены</p>
                    <div className="inst-tokens-list">
                        {tokens.map(t => (
                            <div key={t.id} className="inst-token-row">
                                <div className="inst-token-info">
                                    <span className={`inst-token-badge ${TOKEN_STATUS_CLASS[t.status]}`}>
                                        {TOKEN_STATUS_LABELS[t.status]}
                                    </span>
                                    <span className="inst-token-name">
                                        {t.elementName ?? t.elementId}
                                    </span>
                                    <span className="inst-token-type" title={t.elementId}>
                                        {t.elementType}
                                    </span>
                                </div>
                                {t.status === 'WaitingUserAction' && onCompleteToken && (
                                    <button
                                        className="inst-btn-primary"
                                        style={{ padding: '4px 12px', fontSize: 12 }}
                                        onClick={() => onCompleteToken(t.elementId)}
                                        disabled={completingToken === t.elementId}
                                        title="Выполнить задание"
                                    >
                                        {completingToken === t.elementId ? '…' : '✓ Выполнить'}
                                    </button>
                                )}
                            </div>
                        ))}
                    </div>
                </div>
            )}
        </div>
    );
}

const LAUNCH_LABELS: Record<api.BpmInstanceLaunchSource, string> = {
    Manual: 'Вручную',
    Webhook: 'Вебхук',
    Scheduler: 'Расписание',
    Message: 'Сообщение',
    Signal: 'Сигнал',
    CallActivity: 'Call Activity',
    Batch: 'Пакетный',
};

// ─── Вкладка «Переменные» ─────────────────────────────────────────────────────

interface VariablesTabProps {
    instance: BpmInstanceDto;
    canEdit: boolean;
    editingVar: string | null;
    editVarValue: string;
    savingVar: boolean;
    onStartEdit: (name: string, val: string | undefined) => void;
    onCancelEdit: () => void;
    onSave: (name: string) => void;
    onChangeValue: (val: string) => void;
}

function VariablesTab({ instance, canEdit, editingVar, editVarValue, savingVar, onStartEdit, onCancelEdit, onSave, onChangeValue }: VariablesTabProps) {
    if (instance.variables.length === 0) {
        return <div className="inst-empty">Переменные не заданы</div>;
    }

    return (
        <table className="inst-variables-table">
            <thead>
                <tr>
                    <th>Переменная</th>
                    <th>Значение</th>
                    {canEdit && <th style={{ width: 100 }}>Действия</th>}
                </tr>
            </thead>
            <tbody>
                {instance.variables.map(v => (
                    <tr key={v.id}>
                        <td style={{ fontFamily: 'monospace', fontSize: 13 }}>{v.name}</td>
                        <td>
                            {editingVar === v.name ? (
                                <input
                                    className="inst-var-edit-input"
                                    value={editVarValue}
                                    onChange={e => onChangeValue(e.target.value)}
                                    onKeyDown={e => { if (e.key === 'Enter') onSave(v.name); if (e.key === 'Escape') onCancelEdit(); }}
                                    autoFocus
                                />
                            ) : (
                                <code style={{ fontSize: 12, wordBreak: 'break-all' }}>
                                    {v.valueJson ?? <span style={{ color: '#9ca3af', fontStyle: 'italic' }}>null</span>}
                                </code>
                            )}
                        </td>
                        {canEdit && (
                            <td>
                                {editingVar === v.name ? (
                                    <div style={{ display: 'flex', gap: 4 }}>
                                        <button className="inst-btn-primary" style={{ padding: '3px 10px', fontSize: 12 }}
                                            onClick={() => onSave(v.name)} disabled={savingVar}>✓</button>
                                        <button className="inst-btn-secondary" style={{ padding: '3px 10px', fontSize: 12 }}
                                            onClick={onCancelEdit}>✕</button>
                                    </div>
                                ) : (
                                    <button className="inst-btn-icon" onClick={() => onStartEdit(v.name, v.valueJson ?? '')}>
                                        ✏️
                                    </button>
                                )}
                            </td>
                        )}
                    </tr>
                ))}
            </tbody>
        </table>
    );
}

// ─── Вкладка «История» ────────────────────────────────────────────────────────

interface HistoryTabProps {
    history: BpmInstanceHistoryEntryDto[];
    commentText: string;
    isQuestion: boolean;
    sendingComment: boolean;
    canComment: boolean;
    onCommentChange: (t: string) => void;
    onQuestionToggle: () => void;
    onSendComment: () => void;
}

function HistoryTab({ history, commentText, isQuestion, sendingComment, canComment, onCommentChange, onQuestionToggle, onSendComment }: HistoryTabProps) {
    return (
        <div>
            {/* Форма комментария */}
            {canComment && (
                <div className="inst-comment-form">
                    <textarea
                        className="inst-comment-textarea"
                        value={commentText}
                        onChange={e => onCommentChange(e.target.value)}
                        placeholder={isQuestion ? 'Задайте вопрос по экземпляру процесса…' : 'Добавьте комментарий…'}
                    />
                    <div className="inst-comment-actions">
                        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, cursor: 'pointer' }}>
                            <input type="checkbox" checked={isQuestion} onChange={onQuestionToggle} />
                            Это вопрос
                        </label>
                        <div style={{ flex: 1 }} />
                        <button
                            className="inst-btn-primary"
                            onClick={onSendComment}
                            disabled={sendingComment || !commentText.trim()}
                            style={{ padding: '5px 14px' }}
                        >
                            {sendingComment ? 'Отправка…' : isQuestion ? 'Задать вопрос' : 'Добавить комментарий'}
                        </button>
                    </div>
                </div>
            )}

            {/* Лента событий */}
            {history.length === 0 ? (
                <div className="inst-empty">История событий пуста</div>
            ) : (
                <div className="inst-history-list">
                    {[...history].reverse().map(entry => (
                        <div key={entry.id} className="inst-history-item">
                            <div className="inst-history-icon">
                                {HISTORY_ICONS[entry.eventType] ?? '•'}
                            </div>
                            <div className="inst-history-content">
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                                    <div>
                                        <span className="inst-history-event-name">{HISTORY_LABELS[entry.eventType] ?? entry.eventType}</span>
                                        {entry.actorDisplayName && (
                                            <span className="inst-history-actor"> · {entry.actorDisplayName}</span>
                                        )}
                                    </div>
                                    <span className="inst-history-time" title={formatDateTime(entry.occurredAt)}>
                                        {formatTimeAgo(entry.occurredAt)}
                                    </span>
                                </div>
                                {entry.text && (
                                    <div className="inst-history-text">{entry.text}</div>
                                )}
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

// ─── Вкладка «Участники» ──────────────────────────────────────────────────────

interface ParticipantsTabProps {
    participants: BpmInstanceParticipantDto[];
    currentUserId?: string;
    newParticipantId: string | null;
    newParticipantName: string;
    addingParticipant: boolean;
    canManage: boolean;
    token: string;
    onSelectUser: (id: string, name: string) => void;
    onAddParticipant: () => void;
    onRemoveParticipant: (userId: string) => void;
}

function ParticipantsTab({
    participants, currentUserId, newParticipantId, newParticipantName,
    addingParticipant, canManage, token, onSelectUser, onAddParticipant, onRemoveParticipant,
}: ParticipantsTabProps) {
    return (
        <div>
            {/* Добавление участника */}
            {canManage && (
                <div style={{ display: 'flex', gap: 8, marginBottom: 20, alignItems: 'flex-end' }}>
                    <div style={{ flex: 1 }}>
                        <label style={{ fontSize: 12, color: '#6b7280', display: 'block', marginBottom: 4 }}>
                            Добавить участника
                        </label>
                        <UserSearch
                            token={token}
                            value={newParticipantName}
                            onSelect={onSelectUser}
                            placeholder="Начните вводить имя…"
                        />
                    </div>
                    <button
                        className="inst-btn-primary"
                        onClick={onAddParticipant}
                        disabled={addingParticipant || !newParticipantId}
                        style={{ padding: '8px 16px' }}
                    >
                        {addingParticipant ? 'Добавление…' : '+ Добавить'}
                    </button>
                </div>
            )}

            {/* Список участников */}
            {participants.length === 0 ? (
                <div className="inst-empty">Нет участников</div>
            ) : (
                <div className="inst-participants-list">
                    {participants.map(p => (
                        <div key={p.id} className="inst-participant-row">
                            <div className="inst-participant-avatar">
                                {(p.displayName ?? '?')[0]?.toUpperCase()}
                            </div>
                            <div style={{ flex: 1 }}>
                                <div className="inst-participant-name">{p.displayName ?? p.userId}</div>
                                {p.addedByDisplayName && (
                                    <div className="inst-participant-meta">Добавил: {p.addedByDisplayName} · {formatTimeAgo(p.addedAt)}</div>
                                )}
                            </div>
                            {canManage && p.userId !== currentUserId && (
                                <button
                                    className="inst-btn-icon"
                                    title="Удалить участника"
                                    onClick={() => onRemoveParticipant(p.userId)}
                                    style={{ color: '#dc2626' }}
                                >
                                    ✕
                                </button>
                            )}
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

// ─── Инлайн-компонент поиска пользователя ────────────────────────────────────

interface UserSearchProps {
    token: string;
    value: string;
    onSelect: (id: string, name: string) => void;
    placeholder?: string;
}

function UserSearch({ token, value, onSelect, placeholder }: UserSearchProps) {
    const [query, setQuery] = useState(value || '');
    const [results, setResults] = useState<{ id: string; name: string }[]>([]);
    const [open, setOpen] = useState(false);
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    useEffect(() => {
        if (!query || query.length < 2) { setResults([]); setOpen(false); return; }
        if (timerRef.current) clearTimeout(timerRef.current);
        timerRef.current = setTimeout(async () => {
            try {
                const emps = await getDirectoryEmployees(token, { search: query });
                setResults(emps.map(e => ({ id: e.userId, name: e.displayName })));
                setOpen(true);
            } catch { setResults([]); }
        }, 300);
    }, [query, token]);

    const handleSelect = (id: string, name: string) => {
        setQuery(name);
        setOpen(false);
        setResults([]);
        onSelect(id, name);
    };

    return (
        <div style={{ position: 'relative' }}>
            <input
                className="inst-modal-input"
                value={query}
                onChange={e => { setQuery(e.target.value); if (!e.target.value) onSelect('', ''); }}
                placeholder={placeholder}
                onBlur={() => setTimeout(() => setOpen(false), 150)}
                autoComplete="off"
            />
            {open && results.length > 0 && (
                <div style={{
                    position: 'absolute', top: '100%', left: 0, right: 0,
                    background: '#fff', border: '1px solid #dde1e7', borderRadius: 6,
                    boxShadow: '0 4px 12px rgba(0,0,0,.1)', zIndex: 100, maxHeight: 200, overflowY: 'auto',
                }}>
                    {results.map(r => (
                        <div
                            key={r.id}
                            style={{ padding: '8px 12px', fontSize: 14, cursor: 'pointer' }}
                            onMouseDown={() => handleSelect(r.id, r.name)}
                            onMouseOver={e => (e.currentTarget.style.background = '#f3f4f6')}
                            onMouseOut={e => (e.currentTarget.style.background = '')}
                        >
                            {r.name}
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}
