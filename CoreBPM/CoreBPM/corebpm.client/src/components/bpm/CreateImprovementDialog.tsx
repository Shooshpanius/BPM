import { useState, useEffect, useCallback } from 'react';
import { getDirectoryEmployees } from '../../api/orgDirectoryApi';
import type { DirectoryEmployeeDto } from '../../api/orgDirectoryApi';
import * as api from '../../api/improvementsApi';
import './CreateImprovementDialog.css';

interface Props {
    token: string;
    /** Если передан — предложение создаётся для конкретного процесса (вариант Б). */
    processId?: string;
    processName?: string;
    /** Тема по умолчанию (например, название экземпляра). */
    defaultSubject?: string;
    sourceInstanceId?: string;
    /** Список процессов для выбора (вариант А). */
    processes?: { id: string; name: string }[];
    onCreated: (dto: api.ImprovementDto) => void;
    onClose: () => void;
}

/** Диалог создания предложения по улучшению бизнес-процесса (FR-BPM-03.1).
 *
 * Вариант А: processId не передан — пользователь выбирает процесс из списка.
 * Вариант Б: processId передан — тема заполняется автоматически, пользователь добавляет описание.
 */
export function CreateImprovementDialog({
    token,
    processId: fixedProcessId,
    processName: fixedProcessName,
    defaultSubject = '',
    sourceInstanceId,
    processes = [],
    onCreated,
    onClose,
}: Props) {
    const [selectedProcessId, setSelectedProcessId] = useState(fixedProcessId ?? '');
    const [subject, setSubject] = useState(defaultSubject);
    const [description, setDescription] = useState('');
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Если список процессов не передан и processId не фиксирован — загружаем из API
    const [orgProcesses, setOrgProcesses] = useState<{ id: string; name: string }[]>(processes);
    const [processesLoaded, setProcessesLoaded] = useState(processes.length > 0 || !!fixedProcessId);
    const [employees, setEmployees] = useState<DirectoryEmployeeDto[]>([]);

    const load = useCallback(async () => {
        if (!fixedProcessId && processes.length === 0) {
            try {
                const res = await fetch('/api/bpm/processes?orgId=&search=', {
                    headers: { Authorization: `Bearer ${token}` },
                });
                if (res.ok) {
                    const data: { id: string; name: string }[] = await res.json();
                    setOrgProcesses(data);
                }
            } catch {
                // игнорируем — пользователь введёт processId вручную
            } finally {
                setProcessesLoaded(true);
            }
        }
        try {
            const emps = await getDirectoryEmployees(token, {});
            setEmployees(emps.items);
        } catch {
            // необязательно
        }
    }, [token, fixedProcessId, processes]);

    useEffect(() => { load(); }, [load]);
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    void employees; // используется только если нужен выбор пользователя в будущем

    const handleSubmit = async () => {
        if (!selectedProcessId) {
            setError('Выберите процесс');
            return;
        }
        if (!subject.trim()) {
            setError('Введите тему предложения');
            return;
        }
        setSaving(true);
        setError(null);
        try {
            const dto = await api.createImprovement(token, selectedProcessId, {
                subject: subject.trim(),
                description: description.trim() || undefined,
                sourceInstanceId,
            });
            onCreated(dto);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка при создании предложения');
        } finally {
            setSaving(false);
        }
    };

    const processLabel = fixedProcessName ?? orgProcesses.find(p => p.id === selectedProcessId)?.name ?? '';

    return (
        <div className="cid-overlay" role="dialog" aria-modal="true" aria-label="Создание предложения по улучшению">
            <div className="cid-modal">
                <div className="cid-header">
                    <h2 className="cid-title">Предложение по улучшению</h2>
                    <button className="cid-close" onClick={onClose} aria-label="Закрыть">✕</button>
                </div>

                <div className="cid-body">
                    {/* Выбор процесса (вариант А) */}
                    {!fixedProcessId && (
                        <div className="cid-field">
                            <label className="cid-label" htmlFor="cid-process">Процесс *</label>
                            {processesLoaded && orgProcesses.length > 0 ? (
                                <select
                                    id="cid-process"
                                    className="cid-select"
                                    value={selectedProcessId}
                                    onChange={e => setSelectedProcessId(e.target.value)}
                                >
                                    <option value="">— выберите процесс —</option>
                                    {orgProcesses.map(p => (
                                        <option key={p.id} value={p.id}>{p.name}</option>
                                    ))}
                                </select>
                            ) : (
                                <div className="cid-loading">Загрузка процессов…</div>
                            )}
                        </div>
                    )}

                    {/* Фиксированный процесс (вариант Б) */}
                    {fixedProcessId && (
                        <div className="cid-field">
                            <label className="cid-label">Процесс</label>
                            <div className="cid-process-name">{processLabel}</div>
                        </div>
                    )}

                    {/* Тема */}
                    <div className="cid-field">
                        <label className="cid-label" htmlFor="cid-subject">Тема *</label>
                        <input
                            id="cid-subject"
                            className="cid-input"
                            type="text"
                            value={subject}
                            onChange={e => setSubject(e.target.value)}
                            placeholder="Краткая формулировка предложения"
                            maxLength={500}
                        />
                    </div>

                    {/* Описание */}
                    <div className="cid-field">
                        <label className="cid-label" htmlFor="cid-desc">Описание</label>
                        <textarea
                            id="cid-desc"
                            className="cid-textarea"
                            value={description}
                            onChange={e => setDescription(e.target.value)}
                            placeholder="Подробное описание предложения по улучшению"
                            rows={5}
                        />
                    </div>

                    {error && <div className="cid-error">{error}</div>}
                </div>

                <div className="cid-footer">
                    <button className="cid-btn cid-btn--secondary" onClick={onClose} disabled={saving}>
                        Отмена
                    </button>
                    <button className="cid-btn cid-btn--primary" onClick={handleSubmit} disabled={saving}>
                        {saving ? 'Отправка…' : 'Отправить'}
                    </button>
                </div>
            </div>
        </div>
    );
}
