import { useState, useEffect, useRef, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import { getDirectoryEmployees } from '../api/orgDirectoryApi';
import type { DirectoryEmployeeDto } from '../api/orgDirectoryApi';
import './PeoplePicker.css';

export interface PeoplePickerValue {
    id: string;
    userId: string;
    displayName: string;
    workEmail: string;
    avatarUrl?: string;
}

interface PeoplePickerProps {
    value?: PeoplePickerValue[];
    onChange: (value: PeoplePickerValue[]) => void;
    multiple?: boolean;
    placeholder?: string;
    disabled?: boolean;
}

/** Компонент выбора сотрудника из адресной книги (FR-ORG-04.3). */
export function PeoplePicker({ value = [], onChange, multiple = false, placeholder = 'Выберите сотрудника…', disabled = false }: PeoplePickerProps) {
    const { accessToken: token } = useAuth();
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState('');
    const [results, setResults] = useState<DirectoryEmployeeDto[]>([]);
    const [loading, setLoading] = useState(false);
    const containerRef = useRef<HTMLDivElement>(null);
    const searchDebounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

    const doSearch = useCallback((q: string) => {
        if (!token) return;
        setLoading(true);
        getDirectoryEmployees(token, { search: q, pageSize: 20 })
            .then(r => setResults(r.items))
            .catch(() => setResults([]))
            .finally(() => setLoading(false));
    }, [token]);

    useEffect(() => {
        if (!open) return;
        if (searchDebounceTimer.current) clearTimeout(searchDebounceTimer.current);
        searchDebounceTimer.current = setTimeout(() => doSearch(search), 250);
        return () => {
            if (searchDebounceTimer.current) clearTimeout(searchDebounceTimer.current);
        };
    }, [search, open, doSearch]);

    // Закрытие при клике вне компонента
    useEffect(() => {
        const handleClick = (e: MouseEvent) => {
            if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
                setOpen(false);
            }
        };
        document.addEventListener('mousedown', handleClick);
        return () => document.removeEventListener('mousedown', handleClick);
    }, []);

    const handleSelect = (emp: DirectoryEmployeeDto) => {
        const item: PeoplePickerValue = {
            id: emp.id,
            userId: emp.userId,
            displayName: emp.displayName,
            workEmail: emp.workEmail,
            avatarUrl: emp.avatarUrl,
        };
        if (multiple) {
            const already = value.some(v => v.id === item.id);
            onChange(already ? value.filter(v => v.id !== item.id) : [...value, item]);
        } else {
            onChange([item]);
            setOpen(false);
        }
        setSearch('');
    };

    const handleRemove = (id: string) => {
        onChange(value.filter(v => v.id !== id));
    };

    const getInitials = (name: string) => {
        const parts = name.split(' ');
        return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || name[0]?.toUpperCase() || '?';
    };

    return (
        <div className="pp-root" ref={containerRef}>
            {/* Выбранные */}
            <div
                className={`pp-selected${disabled ? ' pp-selected--disabled' : ''}`}
                onClick={() => !disabled && setOpen(true)}
                role="button"
                tabIndex={disabled ? -1 : 0}
                onKeyDown={e => e.key === 'Enter' && !disabled && setOpen(true)}
            >
                {value.length === 0 && (
                    <span className="pp-placeholder">{placeholder}</span>
                )}
                {value.map(v => (
                    <span key={v.id} className="pp-tag">
                        <span className="pp-tag-avatar">
                            {v.avatarUrl
                                ? <img src={v.avatarUrl} alt={v.displayName} />
                                : <span>{getInitials(v.displayName)}</span>
                            }
                        </span>
                        {v.displayName}
                        {!disabled && (
                            <button
                                className="pp-tag-remove"
                                onClick={e => { e.stopPropagation(); handleRemove(v.id); }}
                                aria-label={`Убрать ${v.displayName}`}
                            >×</button>
                        )}
                    </span>
                ))}
                {!disabled && <span className="pp-caret">{open ? '▲' : '▼'}</span>}
            </div>

            {/* Выпадающий список */}
            {open && (
                <div className="pp-dropdown">
                    <input
                        className="pp-search"
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        placeholder="Поиск…"
                        autoFocus
                    />
                    <div className="pp-list">
                        {loading && <div className="pp-status">Поиск…</div>}
                        {!loading && results.length === 0 && (
                            <div className="pp-status">Ничего не найдено</div>
                        )}
                        {results.map(emp => {
                            const selected = value.some(v => v.id === emp.id);
                            return (
                                <div
                                    key={emp.id}
                                    className={`pp-item${selected ? ' pp-item--selected' : ''}`}
                                    onClick={() => handleSelect(emp)}
                                    role="option"
                                    aria-selected={selected}
                                >
                                    <div className="pp-item-avatar">
                                        {emp.avatarUrl
                                            ? <img src={emp.avatarUrl} alt={emp.displayName} />
                                            : <span>{getInitials(emp.displayName)}</span>
                                        }
                                    </div>
                                    <div className="pp-item-info">
                                        <span className="pp-item-name">{emp.displayName}</span>
                                        {emp.position && <span className="pp-item-pos">{emp.position}</span>}
                                    </div>
                                    {selected && <span className="pp-item-check">✓</span>}
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}
        </div>
    );
}
