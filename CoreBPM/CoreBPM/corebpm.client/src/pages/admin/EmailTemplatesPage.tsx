import { useEffect, useState } from 'react';

interface EmailTemplate {
  id: string;
  eventType: string;
  subject: string;
  htmlTemplate: string;
  isActive: boolean;
  updatedAt: string;
}

interface UpsertForm {
  eventType: string;
  subject: string;
  htmlTemplate: string;
  isActive: boolean;
}

const DEFAULT_EVENTS = [
  'TaskAssigned', 'TaskDone', 'TaskReminder', 'ResolutionTaskDone',
  'ChannelInvite', 'NewChannelPost', 'ImprovementStatusChanged',
];

export default function EmailTemplatesPage() {
  const [templates, setTemplates] = useState<EmailTemplate[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [form, setForm] = useState<UpsertForm>({
    eventType: '',
    subject: '',
    htmlTemplate: '',
    isActive: true,
  });
  const [preview, setPreview] = useState<{ subject: string; html: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState('');

  useEffect(() => {
    fetch('/api/admin/email-templates')
      .then((r) => r.json())
      .then(setTemplates)
      .catch(() => {});
  }, []);

  const existingTypes = new Set(templates.map((t) => t.eventType));
  const allTypes = [...new Set([...DEFAULT_EVENTS, ...existingTypes])];

  function selectType(eventType: string) {
    setSelected(eventType);
    const existing = templates.find((t) => t.eventType === eventType);
    if (existing) {
      setForm({
        eventType: existing.eventType,
        subject: existing.subject,
        htmlTemplate: existing.htmlTemplate,
        isActive: existing.isActive,
      });
    } else {
      setForm({
        eventType,
        subject: getDefaultSubject(eventType),
        htmlTemplate: getDefaultTemplate(eventType),
        isActive: true,
      });
    }
    setPreview(null);
    setMsg('');
  }

  async function handleSave() {
    setSaving(true);
    setMsg('');
    try {
      const res = await fetch(`/api/admin/email-templates/${form.eventType}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(form),
      });
      if (!res.ok) throw new Error();
      const updated = await res.json();
      setTemplates((prev) => {
        const idx = prev.findIndex((t) => t.eventType === form.eventType);
        return idx >= 0
          ? prev.map((t, i) => (i === idx ? updated : t))
          : [...prev, updated];
      });
      setMsg('✅ Шаблон сохранён');
    } catch {
      setMsg('❌ Ошибка сохранения');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    if (!selected) return;
    if (!confirm(`Сбросить шаблон «${selected}» к дефолтному?`)) return;
    await fetch(`/api/admin/email-templates/${selected}`, { method: 'DELETE' });
    setTemplates((prev) => prev.filter((t) => t.eventType !== selected));
    setSelected(null);
    setMsg('');
  }

  async function handlePreview() {
    const res = await fetch(`/api/admin/email-templates/${form.eventType}/preview`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        title: getDefaultSubject(form.eventType),
        body: 'Тестовое уведомление для предпросмотра шаблона.',
        link: '/tasks/00000000-0000-0000-0000-000000000001',
      }),
    });
    if (res.ok) {
      const data = await res.json();
      setPreview(data);
    }
  }

  return (
    <div style={{ display: 'flex', gap: 16, height: 'calc(100vh - 80px)' }}>
      {/* Левая панель — список типов */}
      <div style={{ width: 240, borderRight: '1px solid #e5e7eb', overflowY: 'auto', padding: 12 }}>
        <h3 style={{ margin: '0 0 12px', fontSize: 14, color: '#6b7280', textTransform: 'uppercase' }}>
          Типы событий
        </h3>
        {allTypes.map((eventType) => {
          const hasCustom = existingTypes.has(eventType);
          return (
            <div
              key={eventType}
              onClick={() => selectType(eventType)}
              style={{
                padding: '8px 10px',
                borderRadius: 6,
                cursor: 'pointer',
                background: selected === eventType ? '#eff6ff' : 'transparent',
                color: selected === eventType ? '#1d4ed8' : '#111827',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: 2,
              }}
            >
              <span style={{ fontSize: 13 }}>{eventType}</span>
              {hasCustom && (
                <span style={{ fontSize: 10, background: '#dbeafe', color: '#1d4ed8', padding: '1px 6px', borderRadius: 4 }}>
                  custom
                </span>
              )}
            </div>
          );
        })}
      </div>

      {/* Правая панель — редактор */}
      {selected ? (
        <div style={{ flex: 1, overflow: 'auto', padding: '0 8px' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
            <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600 }}>
              Шаблон: {selected}
            </h2>
            <div style={{ display: 'flex', gap: 8 }}>
              <button onClick={handlePreview} style={btnStyle('secondary')}>👁 Предпросмотр</button>
              {existingTypes.has(selected) && (
                <button onClick={handleDelete} style={btnStyle('danger')}>🗑 Сбросить</button>
              )}
              <button onClick={handleSave} disabled={saving} style={btnStyle('primary')}>
                {saving ? 'Сохранение...' : '💾 Сохранить'}
              </button>
            </div>
          </div>

          {msg && (
            <div style={{ padding: '8px 12px', borderRadius: 6, marginBottom: 12,
              background: msg.startsWith('✅') ? '#f0fdf4' : '#fef2f2',
              color: msg.startsWith('✅') ? '#166534' : '#991b1b', fontSize: 14 }}>
              {msg}
            </div>
          )}

          <div style={{ marginBottom: 12 }}>
            <label style={labelStyle}>Тема письма</label>
            <input
              value={form.subject}
              onChange={(e) => setForm((f) => ({ ...f, subject: e.target.value }))}
              style={inputStyle}
              placeholder="Введите тему письма. Поддерживает {{title}}, {{body}}"
            />
          </div>

          <div style={{ marginBottom: 12 }}>
            <label style={labelStyle}>
              HTML-шаблон{' '}
              <span style={{ color: '#9ca3af', fontWeight: 400 }}>
                (переменные: {'{{title}}'}, {'{{body}}'}, {'{{link}}'}, {'{{linkHtml}}'}, {'{{actions}}'})
              </span>
            </label>
            <textarea
              value={form.htmlTemplate}
              onChange={(e) => setForm((f) => ({ ...f, htmlTemplate: e.target.value }))}
              rows={20}
              style={{ ...inputStyle, fontFamily: 'monospace', fontSize: 12, resize: 'vertical' }}
            />
          </div>

          <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 8 }}>
            <input
              type="checkbox"
              id="isActive"
              checked={form.isActive}
              onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))}
            />
            <label htmlFor="isActive" style={{ fontSize: 14, cursor: 'pointer' }}>
              Шаблон активен (если отключён — используется дефолтный HTML)
            </label>
          </div>

          {/* Предпросмотр */}
          {preview && (
            <div style={{ borderTop: '1px solid #e5e7eb', paddingTop: 16, marginTop: 16 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
                <h3 style={{ margin: 0, fontSize: 15 }}>Предпросмотр</h3>
                <button onClick={() => setPreview(null)} style={btnStyle('secondary')}>✕ Закрыть</button>
              </div>
              <div style={{ fontSize: 12, color: '#6b7280', marginBottom: 8 }}>
                Тема: <strong>{preview.subject}</strong>
              </div>
              <iframe
                srcDoc={preview.html}
                style={{ width: '100%', height: 400, border: '1px solid #e5e7eb', borderRadius: 6 }}
                title="preview"
              />
            </div>
          )}
        </div>
      ) : (
        <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#9ca3af' }}>
          Выберите тип события для редактирования шаблона
        </div>
      )}
    </div>
  );
}

const labelStyle: React.CSSProperties = {
  display: 'block', marginBottom: 4, fontSize: 13, fontWeight: 500, color: '#374151',
};
const inputStyle: React.CSSProperties = {
  width: '100%', padding: '8px 10px', border: '1px solid #d1d5db',
  borderRadius: 6, fontSize: 14, boxSizing: 'border-box',
};
function btnStyle(variant: 'primary' | 'secondary' | 'danger'): React.CSSProperties {
  const base: React.CSSProperties = { padding: '7px 14px', borderRadius: 6, border: 'none', cursor: 'pointer', fontSize: 13, fontWeight: 500 };
  if (variant === 'primary') return { ...base, background: '#3b82f6', color: '#fff' };
  if (variant === 'danger') return { ...base, background: '#fee2e2', color: '#dc2626' };
  return { ...base, background: '#f3f4f6', color: '#374151' };
}

function getDefaultSubject(eventType: string) {
  const map: Record<string, string> = {
    TaskAssigned: 'Вам назначена задача',
    TaskDone: 'Задача выполнена',
    TaskReminder: 'Напоминание по задаче',
    ResolutionTaskDone: 'Резолюция выполнена',
    ChannelInvite: 'Приглашение в канал',
    NewChannelPost: 'Новая публикация в канале',
    ImprovementStatusChanged: 'Статус предложения изменён',
  };
  return map[eventType] ?? eventType;
}

function getDefaultTemplate(eventType: string): string {
  return `<!DOCTYPE html>
<html>
<head><meta charset="utf-8"/></head>
<body style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:0;background:#f9fafb">
  <div style="background:#3b82f6;padding:24px 32px">
    <h1 style="color:#fff;margin:0;font-size:18px">Core BPM</h1>
  </div>
  <div style="background:#fff;padding:32px">
    <h2 style="color:#111827;margin:0 0 12px">{{title}}</h2>
    <p style="color:#374151">{{body}}</p>
    {{actions}}
    {{linkHtml}}
  </div>
  <div style="background:#f3f4f6;padding:16px 32px;text-align:center">
    <p style="color:#9ca3af;font-size:12px;margin:0">Core BPM — автоматическое уведомление</p>
  </div>
</body>
</html>`;
}
