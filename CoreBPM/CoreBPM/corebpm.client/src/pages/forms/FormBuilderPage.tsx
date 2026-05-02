import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/formsApi';
import type {
    FormVersionDto,
    FormVersionInfoDto,
    FormSchema,
    FormSection,
    FormRow,
    FormField,
    FormFieldType,
} from '../../api/formsApi';
import { EMPTY_SCHEMA } from '../../api/formsApi';
import {
    DndContext,
    DragOverlay,
    closestCenter,
    PointerSensor,
    useSensor,
    useSensors,
    type DragStartEvent,
    type DragEndEvent,
} from '@dnd-kit/core';
import {
    SortableContext,
    useSortable,
    verticalListSortingStrategy,
    arrayMove,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import './FormBuilderPage.css';

// ─── Палитра компонентов ─────────────────────────────────────────────────────

interface PaletteItem {
    type: FormFieldType;
    label: string;
    icon: string;
    category: 'input' | 'display' | 'special';
}

const PALETTE_ITEMS: PaletteItem[] = [
    // Ввод
    { type: 'text',        label: 'Текстовое поле',       icon: '✏️', category: 'input' },
    { type: 'textarea',    label: 'Многострочный текст',  icon: '📝', category: 'input' },
    { type: 'number',      label: 'Число',                icon: '🔢', category: 'input' },
    { type: 'datetime',    label: 'Дата / Время',         icon: '📅', category: 'input' },
    { type: 'checkbox',    label: 'Чекбокс',              icon: '☑️', category: 'input' },
    { type: 'radio',       label: 'Переключатель',        icon: '🔘', category: 'input' },
    { type: 'select',      label: 'Выпадающий список',    icon: '▼',  category: 'input' },
    { type: 'user-picker', label: 'Выбор пользователя',   icon: '👤', category: 'input' },
    { type: 'file-upload', label: 'Загрузка файла',       icon: '📎', category: 'input' },
    // Отображение
    { type: 'label',          label: 'Текстовая метка',   icon: '🏷️', category: 'display' },
    { type: 'section-header', label: 'Заголовок секции',  icon: 'H',  category: 'display' },
    { type: 'divider',        label: 'Разделитель',       icon: '—',  category: 'display' },
    { type: 'html-block',     label: 'HTML-блок',         icon: '<>', category: 'display' },
    // Специальные
    { type: 'approval', label: 'Согласование', icon: '✅', category: 'special' },
    // Контейнеры
    { type: 'tab-container',       label: 'Табы',            icon: '📑', category: 'special' },
    { type: 'accordion-container', label: 'Аккордеон',       icon: '🗂️', category: 'special' },
    // Вложенная форма
    { type: 'subform', label: 'Вложенная форма', icon: '📋', category: 'special' },
];

const CATEGORY_LABELS: Record<string, string> = {
    input: 'Ввод',
    display: 'Отображение',
    special: 'Специальные',
};

// ─── Утилиты ─────────────────────────────────────────────────────────────────

const uid = () => crypto.randomUUID();

function makeField(type: FormFieldType): FormField {
    return {
        id: uid(),
        type,
        label: PALETTE_ITEMS.find(p => p.type === type)?.label ?? type,
    };
}

function makeRow(field?: FormField): FormRow {
    return { id: uid(), fields: field ? [field] : [] };
}

function makeSection(): FormSection {
    return { id: uid(), columns: 1, rows: [] };
}

// ─── Компонент SortableField ──────────────────────────────────────────────────

function SortableField({
    field,
    selected,
    onSelect,
    onRemove,
}: {
    field: FormField;
    selected: boolean;
    onSelect: () => void;
    onRemove: () => void;
}) {
    const { attributes, listeners, setNodeRef, transform, transition, isDragging } =
        useSortable({ id: field.id });

    const style: React.CSSProperties = {
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.4 : 1,
    };

    return (
        <div
            ref={setNodeRef}
            style={style}
            className={`fb-field-chip${selected ? ' fb-field-chip--selected' : ''}`}
            onClick={onSelect}
        >
            <span className="fb-field-drag" {...attributes} {...listeners} title="Перетащить">⠿</span>
            <span className="fb-field-icon">
                {PALETTE_ITEMS.find(p => p.type === field.type)?.icon ?? '?'}
            </span>
            <span className="fb-field-label">{field.label || field.type}</span>
            <button
                className="fb-field-remove"
                title="Удалить"
                onClick={e => { e.stopPropagation(); onRemove(); }}
            >×</button>
        </div>
    );
}

// ─── Компонент FieldPreview (только чтение, предпросмотр) ────────────────────

function FieldPreview({ field }: { field: FormField }) {
    switch (field.type) {
        case 'text':
            return <input className="fb-prev-input" placeholder={field.placeholder || field.label} disabled />;
        case 'textarea':
            return <textarea className="fb-prev-input" placeholder={field.placeholder || field.label} rows={3} disabled />;
        case 'number':
            return <input className="fb-prev-input" type="number" placeholder={field.placeholder || field.label} disabled />;
        case 'datetime':
            return <input className="fb-prev-input" type="datetime-local" disabled />;
        case 'checkbox':
            return <label className="fb-prev-checkbox"><input type="checkbox" disabled /> {field.label}</label>;
        case 'radio':
            return (
                <div className="fb-prev-radios">
                    {(field.options ?? [{ value: '1', label: 'Вариант 1' }, { value: '2', label: 'Вариант 2' }]).map(o => (
                        <label key={o.value} className="fb-prev-radio-row">
                            <input type="radio" disabled /> {o.label}
                        </label>
                    ))}
                </div>
            );
        case 'select':
            return (
                <select className="fb-prev-input" disabled>
                    <option>— Выберите —</option>
                    {(field.options ?? []).map(o => <option key={o.value}>{o.label}</option>)}
                </select>
            );
        case 'user-picker':
            return <input className="fb-prev-input" placeholder="Выбор пользователя…" disabled />;
        case 'file-upload':
            return <button className="fb-prev-file-btn" disabled>📎 Прикрепить файл</button>;
        case 'label':
            return <p className="fb-prev-label">{field.label}</p>;
        case 'section-header':
            return <h3 className="fb-prev-section-header">{field.label}</h3>;
        case 'divider':
            return <hr className="fb-prev-divider" />;
        case 'html-block':
            return <div className="fb-prev-html">{field.label}</div>;
        case 'approval':
            return (
                <div className="fb-prev-approval">
                    <button className="fb-prev-approve" disabled>✓ Согласовать</button>
                    <button className="fb-prev-reject" disabled>✗ Отклонить</button>
                    <textarea className="fb-prev-input" placeholder="Комментарий…" rows={2} disabled />
                </div>
            );
        case 'tab-container':
            return (
                <div className="fb-prev-tabs">
                    <div className="fb-prev-tab-header">
                        {(field.children ?? [{ id: '1', label: 'Вкладка 1', type: 'label' }]).map(c => (
                            <span key={c.id} className="fb-prev-tab-label">{c.label}</span>
                        ))}
                    </div>
                </div>
            );
        case 'accordion-container':
            return (
                <div className="fb-prev-accordion">
                    {(field.children ?? [{ id: '1', label: 'Секция', type: 'label' }]).map(c => (
                        <div key={c.id} className="fb-prev-accordion-item">▶ {c.label}</div>
                    ))}
                </div>
            );
        case 'subform':
            return (
                <div className="fb-prev-subform">
                    📋 Вложенная форма: <em>{field.extra?.formId ? String(field.extra.formId) : 'не выбрана'}</em>
                </div>
            );
        default:
            return <input className="fb-prev-input" placeholder={field.label} disabled />;
    }
}

// ─── Компонент PropertiesPanel ────────────────────────────────────────────────

interface PropertiesPanelProps {
    field: FormField;
    onChange: (updated: FormField) => void;
}

function PropertiesPanel({ field, onChange }: PropertiesPanelProps) {
    const upd = <K extends keyof FormField>(key: K, value: FormField[K]) =>
        onChange({ ...field, [key]: value });

    return (
        <div className="fb-props">
            <div className="fb-props-title">Свойства поля</div>

            <div className="fb-props-field">
                <label>Метка</label>
                <input className="fb-props-input" value={field.label} onChange={e => upd('label', e.target.value)} />
            </div>

            {['text', 'textarea', 'number', 'datetime', 'select', 'user-picker'].includes(field.type) && (
                <div className="fb-props-field">
                    <label>Подсказка (placeholder)</label>
                    <input className="fb-props-input" value={field.placeholder ?? ''} onChange={e => upd('placeholder', e.target.value)} />
                </div>
            )}

            {!['label', 'section-header', 'divider', 'html-block'].includes(field.type) && (
                <>
                    <label className="fb-props-checkbox">
                        <input type="checkbox" checked={!!field.required} onChange={e => upd('required', e.target.checked)} />
                        Обязательное поле
                    </label>
                    <label className="fb-props-checkbox">
                        <input type="checkbox" checked={!!field.readOnly} onChange={e => upd('readOnly', e.target.checked)} />
                        Только для чтения
                    </label>
                </>
            )}

            <div className="fb-props-field">
                <label>Привязка к переменной</label>
                <input className="fb-props-input" value={field.variableName ?? ''} onChange={e => upd('variableName', e.target.value || undefined)} placeholder="имяПеременной" />
            </div>

            <div className="fb-props-field">
                <label>Условная видимость (EL)</label>
                <input className="fb-props-input" value={field.visibilityExpression ?? ''} onChange={e => upd('visibilityExpression', e.target.value || undefined)} placeholder="${переменная == 'значение'}" />
            </div>

            {['text', 'number'].includes(field.type) && (
                <>
                    <div className="fb-props-field">
                        <label>Маска ввода</label>
                        <input className="fb-props-input" value={field.inputMask ?? ''} onChange={e => upd('inputMask', e.target.value || undefined)} placeholder="+7 (###) ###-##-##" />
                    </div>
                    <div className="fb-props-field">
                        <label>Regex-валидация</label>
                        <input className="fb-props-input" value={field.validationRegex ?? ''} onChange={e => upd('validationRegex', e.target.value || undefined)} placeholder="^[0-9]+$" />
                    </div>
                    {field.validationRegex && (
                        <div className="fb-props-field">
                            <label>Сообщение при ошибке</label>
                            <input className="fb-props-input" value={field.validationMessage ?? ''} onChange={e => upd('validationMessage', e.target.value || undefined)} placeholder="Введите корректное значение" />
                        </div>
                    )}
                </>
            )}

            {['select', 'radio'].includes(field.type) && (
                <div className="fb-props-field">
                    <label>Варианты (ключ = значение, по одному на строку)</label>
                    <textarea
                        className="fb-props-input"
                        rows={4}
                        value={(field.options ?? []).map(o => `${o.value}=${o.label}`).join('\n')}
                        onChange={e => {
                            const opts = e.target.value.split('\n').map(line => {
                                const [value, ...rest] = line.split('=');
                                return { value: value.trim(), label: rest.join('=').trim() || value.trim() };
                            }).filter(o => o.value);
                            upd('options', opts);
                        }}
                        placeholder="да=Да&#10;нет=Нет"
                    />
                </div>
            )}

            {['select', 'radio'].includes(field.type) && (
                <div className="fb-props-field">
                    <label>Варианты из переменной</label>
                    <input className="fb-props-input" value={field.optionsFrom ?? ''} onChange={e => upd('optionsFrom', e.target.value || undefined)} placeholder="имяПеременной" />
                    <span className="fb-props-hint">Если задано — статичные варианты игнорируются.</span>
                </div>
            )}

            {!['label', 'section-header', 'divider', 'html-block'].includes(field.type) && (
                <div className="fb-props-field">
                    <label>Условная обязательность (JS)</label>
                    <input className="fb-props-input" value={field.requiredWhen ?? ''} onChange={e => upd('requiredWhen', e.target.value || undefined)} placeholder="values.status === 'active'" />
                </div>
            )}

            {field.type === 'subform' && (
                <div className="fb-props-field">
                    <label>ID вложенной формы</label>
                    <input className="fb-props-input" value={String(field.extra?.formId ?? '')} onChange={e => upd('extra', { ...field.extra, formId: e.target.value || undefined })} placeholder="uuid формы" />
                </div>
            )}
        </div>
    );
}

// ─── Основной компонент FormBuilderPage ──────────────────────────────────────

interface FormBuilderPageProps {
    formId: string;
    onBack: () => void;
}

/** Конструктор форм задач (FR-BPM-01.4). */
export function FormBuilderPage({ formId, onBack }: FormBuilderPageProps) {
    const { accessToken: token } = useAuth();

    const [formName, setFormName] = useState('');
    const [schema, setSchema] = useState<FormSchema>(EMPTY_SCHEMA);
    const [versions, setVersions] = useState<FormVersionInfoDto[]>([]);
    const [currentVersion, setCurrentVersion] = useState<FormVersionDto | null>(null);
    const [selectedFieldId, setSelectedFieldId] = useState<string | null>(null);
    const [showPreview, setShowPreview] = useState(false);
    const [saving, setSaving] = useState(false);
    const [publishing, setPublishing] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [dirty, setDirty] = useState(false);

    // Режим устройства предпросмотра
    const [previewDevice, setPreviewDevice] = useState<'desktop' | 'tablet' | 'mobile'>('desktop');

    // ref для импорта файла
    const importFormRef = useRef<HTMLInputElement>(null);

    // DnD: активный тип из палитры
    const [activePaletteType, setActivePaletteType] = useState<FormFieldType | null>(null);

    const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }));

    // ─── Загрузка ────────────────────────────────────────────────────────────

    const loadForm = useCallback(async () => {
        if (!token) return;
        try {
            const [formDto, versionsData] = await Promise.all([
                api.getForm(token, formId),
                api.getFormVersions(token, formId),
            ]);
            setFormName(formDto.name);
            setVersions(versionsData);

            // Загружаем последнюю версию (по убыванию номера — первая в списке)
            if (versionsData.length > 0) {
                const latest = versionsData[0];
                const vDto = await api.getFormVersion(token, formId, latest.id);
                setCurrentVersion(vDto);
                setSchema(vDto.schema ?? EMPTY_SCHEMA);
            }
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        }
    }, [token, formId]);

    useEffect(() => { loadForm(); }, [loadForm]);

    // ─── Мутации схемы ───────────────────────────────────────────────────────

    const addSection = () => {
        setSchema(prev => ({ sections: [...prev.sections, makeSection()] }));
        setDirty(true);
    };

    const removeSection = (sectionId: string) => {
        setSchema(prev => ({ sections: prev.sections.filter(s => s.id !== sectionId) }));
        setDirty(true);
    };

    const setSectionColumns = (sectionId: string, cols: 1 | 2 | 3) => {
        setSchema(prev => ({
            sections: prev.sections.map(s =>
                s.id === sectionId ? { ...s, columns: cols } : s
            ),
        }));
        setDirty(true);
    };

    const setSectionTitle = (sectionId: string, title: string) => {
        setSchema(prev => ({
            sections: prev.sections.map(s =>
                s.id === sectionId ? { ...s, title } : s
            ),
        }));
        setDirty(true);
    };

    const addFieldToSection = (sectionId: string, type: FormFieldType) => {
        const newField = makeField(type);
        setSchema(prev => ({
            sections: prev.sections.map(s => {
                if (s.id !== sectionId) return s;
                // Добавляем поле в последнюю строку или создаём новую
                const rows = [...s.rows];
                const lastRow = rows[rows.length - 1];
                if (!lastRow || lastRow.fields.length >= s.columns) {
                    rows.push(makeRow(newField));
                } else {
                    rows[rows.length - 1] = { ...lastRow, fields: [...lastRow.fields, newField] };
                }
                return { ...s, rows };
            }),
        }));
        setSelectedFieldId(newField.id);
        setDirty(true);
    };

    const removeField = (sectionId: string, rowId: string, fieldId: string) => {
        setSchema(prev => ({
            sections: prev.sections.map(s => {
                if (s.id !== sectionId) return s;
                const rows = s.rows
                    .map(r => r.id === rowId
                        ? { ...r, fields: r.fields.filter(f => f.id !== fieldId) }
                        : r)
                    .filter(r => r.fields.length > 0);
                return { ...s, rows };
            }),
        }));
        if (selectedFieldId === fieldId) setSelectedFieldId(null);
        setDirty(true);
    };

    const updateField = (sectionId: string, rowId: string, updated: FormField) => {
        setSchema(prev => ({
            sections: prev.sections.map(s => {
                if (s.id !== sectionId) return s;
                return {
                    ...s,
                    rows: s.rows.map(r => r.id === rowId
                        ? { ...r, fields: r.fields.map(f => f.id === updated.id ? updated : f) }
                        : r
                    ),
                };
            }),
        }));
        setDirty(true);
    };

    // ─── DnD перестановка полей внутри секции ────────────────────────────────

    const handleDragStart = (event: DragStartEvent) => {
        const t = event.active.data.current?.paletteType as FormFieldType | undefined;
        if (t) setActivePaletteType(t);
    };

    const handleDragEnd = (event: DragEndEvent) => {
        setActivePaletteType(null);
        const { active, over } = event;
        if (!over || active.id === over.id) return;

        // Перестановка полей внутри строки секции
        setSchema(prev => ({
            sections: prev.sections.map(s => ({
                ...s,
                rows: s.rows.map(r => {
                    const ids = r.fields.map(f => f.id);
                    if (!ids.includes(String(active.id))) return r;
                    const oldIdx = ids.indexOf(String(active.id));
                    const newIdx = ids.indexOf(String(over.id));
                    if (newIdx < 0) return r;
                    return { ...r, fields: arrayMove(r.fields, oldIdx, newIdx) };
                }),
            })),
        }));
        setDirty(true);
    };

    // ─── Нахождение выбранного поля ──────────────────────────────────────────

    const findSelectedField = (): { section: FormSection; row: FormRow; field: FormField } | null => {
        if (!selectedFieldId) return null;
        for (const section of schema.sections) {
            for (const row of section.rows) {
                const field = row.fields.find(f => f.id === selectedFieldId);
                if (field) return { section, row, field };
            }
        }
        return null;
    };

    const selectedCtx = findSelectedField();

    // ─── Версионирование ─────────────────────────────────────────────────────

    const handleSaveDraft = async () => {
        if (!token) return;
        setSaving(true);
        setError(null);
        try {
            const vDto = await api.saveFormDraft(token, formId, { schema });
            setCurrentVersion(vDto);
            const versionsData = await api.getFormVersions(token, formId);
            setVersions(versionsData);
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handlePublish = async () => {
        if (!token || !currentVersion) return;
        setPublishing(true);
        setError(null);
        try {
            await api.publishFormVersion(token, formId, currentVersion.id);
            const versionsData = await api.getFormVersions(token, formId);
            setVersions(versionsData);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка публикации');
        } finally {
            setPublishing(false);
        }
    };

    const handleRollback = async (versionId: string) => {
        if (!token) return;
        try {
            const vDto = await api.rollbackFormVersion(token, formId, versionId);
            setCurrentVersion(vDto);
            setSchema(vDto.schema ?? EMPTY_SCHEMA);
            const versionsData = await api.getFormVersions(token, formId);
            setVersions(versionsData);
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка отката');
        }
    };

    const handleLoadVersion = async (versionId: string) => {
        if (!token) return;
        try {
            const vDto = await api.getFormVersion(token, formId, versionId);
            setCurrentVersion(vDto);
            setSchema(vDto.schema ?? EMPTY_SCHEMA);
            setSelectedFieldId(null);
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки версии');
        }
    };

    const handleExportForm = async () => {
        if (!token || !currentVersion) return;
        try {
            const blob = await api.exportFormVersion(token, formId, currentVersion.id);
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `form-${formId}-v${currentVersion.versionNumber}.json`;
            link.click();
            URL.revokeObjectURL(url);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка экспорта');
        }
    };

    const handleImportForm = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file || !token) return;
        try {
            const text = await file.text();
            const json = JSON.parse(text);
            const vDto = await api.importFormVersion(token, formId, json);
            setCurrentVersion(vDto);
            setSchema(vDto.schema ?? EMPTY_SCHEMA);
            const versionsData = await api.getFormVersions(token, formId);
            setVersions(versionsData);
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка импорта');
        } finally {
            if (importFormRef.current) importFormRef.current.value = '';
        }
    };

    const STATUS_LABELS: Record<string, string> = { Draft: 'Черновик', Published: 'Опубликована', Archived: 'Архив' };

    // ─── Рендер ──────────────────────────────────────────────────────────────

    return (
        <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragStart={handleDragStart}
            onDragEnd={handleDragEnd}
        >
            <div className="fb-root">
                {/* Шапка */}
                <div className="fb-topbar">
                    <button className="fb-back-btn" onClick={onBack} title="Вернуться к списку">
                        ← Назад
                    </button>
                    <span className="fb-form-name">{formName}</span>
                    <div className="fb-topbar-actions">
                        <button
                            className="fb-btn-secondary"
                            onClick={() => setShowPreview(p => !p)}
                        >
                            {showPreview ? '✏️ Редактор' : '👁 Предпросмотр'}
                        </button>
                        <button
                            className="fb-btn-primary"
                            onClick={handleSaveDraft}
                            disabled={saving || !dirty}
                        >
                            {saving ? 'Сохранение…' : 'Сохранить черновик'}
                        </button>
                        <button
                            className="fb-btn-publish"
                            onClick={handlePublish}
                            disabled={publishing || !currentVersion || currentVersion.status === 'Published'}
                        >
                            {publishing ? 'Публикация…' : 'Опубликовать'}
                        </button>
                        <button
                            className="fb-btn-secondary"
                            onClick={handleExportForm}
                            disabled={!currentVersion}
                            title="Экспортировать форму в JSON"
                        >
                            ↑ Экспорт
                        </button>
                        <button
                            className="fb-btn-secondary"
                            onClick={() => importFormRef.current?.click()}
                            title="Импортировать форму из JSON"
                        >
                            ↓ Импорт
                        </button>
                        <input
                            ref={importFormRef}
                            type="file"
                            accept=".json"
                            style={{ display: 'none' }}
                            onChange={handleImportForm}
                        />
                    </div>
                </div>

                {error && <div className="fb-error">{error}</div>}

                {showPreview && (
                    /* Панель переключения устройства */
                    <div className="fb-device-bar">
                        {(['desktop', 'tablet', 'mobile'] as const).map(d => (
                            <button
                                key={d}
                                className={`fb-device-btn${previewDevice === d ? ' active' : ''}`}
                                onClick={() => setPreviewDevice(d)}
                            >
                                {d === 'desktop' ? '🖥 Десктоп' : d === 'tablet' ? '📱 Планшет' : '📱 Мобильный'}
                            </button>
                        ))}
                    </div>
                )}

                {showPreview ? (
                    /* ─── Предпросмотр ─────────────────────────────────────── */
                    <div className="fb-preview-root">
                    <div className={`fb-preview-panel fb-preview-panel--${previewDevice}`}>
                            <h2 className="fb-preview-title">{formName}</h2>
                            {schema.sections.length === 0 && (
                                <p className="fb-preview-empty">Форма пустая. Добавьте секции и поля в редакторе.</p>
                            )}
                            {schema.sections.map(section => (
                                <div key={section.id} className="fb-preview-section">
                                    {section.title && <h3 className="fb-preview-section-title">{section.title}</h3>}
                                    {section.rows.map(row => (
                                        <div
                                            key={row.id}
                                            className="fb-preview-row"
                                            style={{ gridTemplateColumns: `repeat(${section.columns}, 1fr)` }}
                                        >
                                            {row.fields.map(field => (
                                                <div key={field.id} className="fb-preview-field">
                                                    {!['label', 'section-header', 'divider', 'html-block', 'approval', 'checkbox'].includes(field.type) && (
                                                        <label className="fb-preview-label">
                                                            {field.label}{field.required && <span className="fb-preview-req">*</span>}
                                                        </label>
                                                    )}
                                                    <FieldPreview field={field} />
                                                </div>
                                            ))}
                                        </div>
                                    ))}
                                </div>
                            ))}
                        </div>
                    </div>
                ) : (
                    /* ─── Редактор ─────────────────────────────────────────── */
                    <div className="fb-editor">
                        {/* Левая панель: палитра */}
                        <aside className="fb-palette">
                            {(['input', 'display', 'special'] as const).map(cat => (
                                <div key={cat} className="fb-palette-group">
                                    <div className="fb-palette-group-title">{CATEGORY_LABELS[cat]}</div>
                                    {PALETTE_ITEMS.filter(p => p.category === cat).map(item => (
                                        <div
                                            key={item.type}
                                            className="fb-palette-item"
                                            draggable
                                            onDragStart={e => {
                                                e.dataTransfer.setData('fieldType', item.type);
                                            }}
                                            title={`Перетащите «${item.label}» на холст`}
                                        >
                                            <span className="fb-palette-icon">{item.icon}</span>
                                            <span className="fb-palette-label">{item.label}</span>
                                        </div>
                                    ))}
                                </div>
                            ))}
                        </aside>

                        {/* Центральная панель: холст */}
                        <main className="fb-canvas">
                            {schema.sections.length === 0 && (
                                <div className="fb-canvas-empty">
                                    <div className="fb-canvas-empty-icon">📋</div>
                                    <p>Форма пустая</p>
                                    <p className="fb-canvas-empty-sub">Нажмите «+ Секция» чтобы начать</p>
                                </div>
                            )}

                            {schema.sections.map(section => (
                                <div
                                    key={section.id}
                                    className="fb-section"
                                    onDragOver={e => e.preventDefault()}
                                    onDrop={e => {
                                        e.preventDefault();
                                        const t = e.dataTransfer.getData('fieldType') as FormFieldType;
                                        if (t) addFieldToSection(section.id, t);
                                    }}
                                >
                                    {/* Заголовок секции */}
                                    <div className="fb-section-header">
                                        <input
                                            className="fb-section-title-input"
                                            value={section.title ?? ''}
                                            onChange={e => setSectionTitle(section.id, e.target.value)}
                                            placeholder="Заголовок секции (необязательно)"
                                        />
                                        <div className="fb-section-cols-row">
                                            {([1, 2, 3] as const).map(c => (
                                                <button
                                                    key={c}
                                                    className={`fb-section-col-btn${section.columns === c ? ' active' : ''}`}
                                                    onClick={() => setSectionColumns(section.id, c)}
                                                    title={`${c} колонки`}
                                                >
                                                    {c}к
                                                </button>
                                            ))}
                                        </div>
                                        <button
                                            className="fb-section-remove"
                                            onClick={() => removeSection(section.id)}
                                            title="Удалить секцию"
                                        >×</button>
                                    </div>

                                    {/* Строки и поля */}
                                    <SortableContext
                                        items={section.rows.flatMap(r => r.fields.map(f => f.id))}
                                        strategy={verticalListSortingStrategy}
                                    >
                                        {section.rows.map(row => (
                                            <div
                                                key={row.id}
                                                className="fb-row"
                                                style={{ gridTemplateColumns: `repeat(${section.columns}, 1fr)` }}
                                            >
                                                {row.fields.map(field => (
                                                    <SortableField
                                                        key={field.id}
                                                        field={field}
                                                        selected={field.id === selectedFieldId}
                                                        onSelect={() => setSelectedFieldId(field.id)}
                                                        onRemove={() => removeField(section.id, row.id, field.id)}
                                                    />
                                                ))}
                                            </div>
                                        ))}
                                    </SortableContext>

                                    {section.rows.length === 0 && (
                                        <div className="fb-section-drop-hint">
                                            Перетащите компонент из палитры сюда
                                        </div>
                                    )}
                                </div>
                            ))}

                            <button className="fb-add-section-btn" onClick={addSection}>
                                + Секция
                            </button>
                        </main>

                        {/* Правая панель: свойства */}
                        <aside className="fb-props-panel">
                            {selectedCtx ? (
                                <PropertiesPanel
                                    field={selectedCtx.field}
                                    onChange={updated =>
                                        updateField(selectedCtx.section.id, selectedCtx.row.id, updated)
                                    }
                                />
                            ) : (
                                <div className="fb-props-empty">
                                    <p>Выберите поле для редактирования его свойств</p>
                                </div>
                            )}
                        </aside>
                    </div>
                )}

                {/* Нижняя панель: версии */}
                <div className="fb-versions">
                    <div className="fb-versions-title">Версии</div>
                    <div className="fb-versions-list">
                        {versions.map(v => (
                            <div
                                key={v.id}
                                className={`fb-version-chip${currentVersion?.id === v.id ? ' fb-version-chip--active' : ''}`}
                            >
                                <span
                                    className="fb-version-num"
                                    role="button"
                                    tabIndex={0}
                                    onClick={() => handleLoadVersion(v.id)}
                                    onKeyDown={e => e.key === 'Enter' && handleLoadVersion(v.id)}
                                    title="Загрузить версию"
                                >
                                    v{v.versionNumber}
                                </span>
                                <span className={`fb-version-status fb-version-status--${v.status.toLowerCase()}`}>
                                    {STATUS_LABELS[v.status]}
                                </span>
                                {v.status !== 'Published' && (
                                    <button
                                        className="fb-version-rollback"
                                        title="Откатить к этой версии"
                                        onClick={() => handleRollback(v.id)}
                                    >
                                        ↩
                                    </button>
                                )}
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            {/* DnD Overlay */}
            <DragOverlay>
                {activePaletteType && (
                    <div className="fb-field-chip fb-field-chip--dragging">
                        <span className="fb-field-icon">
                            {PALETTE_ITEMS.find(p => p.type === activePaletteType)?.icon}
                        </span>
                        <span className="fb-field-label">
                            {PALETTE_ITEMS.find(p => p.type === activePaletteType)?.label}
                        </span>
                    </div>
                )}
            </DragOverlay>
        </DndContext>
    );
}
