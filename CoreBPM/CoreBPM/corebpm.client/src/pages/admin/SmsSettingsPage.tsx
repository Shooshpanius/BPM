import { useEffect, useState } from 'react';

interface SmsSettings {
  providerUrl: string;
  apiKey: string;
  fromNumber: string;
  isEnabled: boolean;
  phoneParamName: string;
  messageParamName: string;
  apiKeyParamName: string;
}

const DEFAULT: SmsSettings = {
  providerUrl: '',
  apiKey: '',
  fromNumber: '',
  isEnabled: false,
  phoneParamName: 'to',
  messageParamName: 'msg',
  apiKeyParamName: 'api_id',
};

export default function SmsSettingsPage() {
  const [form, setForm] = useState<SmsSettings>(DEFAULT);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [msg, setMsg] = useState('');
  const [testResult, setTestResult] = useState<string | null>(null);

  useEffect(() => {
    fetch('/api/admin/settings/sms')
      .then((r) => r.json())
      .then((d) => setForm({ ...DEFAULT, ...d }))
      .catch(() => {});
  }, []);

  async function handleSave() {
    setSaving(true);
    setMsg('');
    try {
      const res = await fetch('/api/admin/settings/sms', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(form),
      });
      if (!res.ok) throw new Error();
      setMsg('✅ Настройки сохранены');
    } catch {
      setMsg('❌ Ошибка сохранения');
    } finally {
      setSaving(false);
    }
  }

  async function handleTest() {
    setTesting(true);
    setTestResult(null);
    try {
      const res = await fetch('/api/admin/settings/sms/test', { method: 'POST' });
      const data = await res.json();
      setTestResult(data.success
        ? `✅ ${data.message}`
        : `❌ ${data.message}`);
    } catch {
      setTestResult('❌ Ошибка подключения');
    } finally {
      setTesting(false);
    }
  }

  function set(key: keyof SmsSettings, val: string | boolean) {
    setForm((f) => ({ ...f, [key]: val }));
  }

  return (
    <div style={{ maxWidth: 640, padding: 24 }}>
      <h1 style={{ margin: '0 0 4px', fontSize: 22, fontWeight: 700 }}>Настройки SMS</h1>
      <p style={{ margin: '0 0 24px', color: '#6b7280', fontSize: 14 }}>
        Configurable HTTP SMS-провайдер. Уведомления отправляются на номер телефона пользователя,
        если он заполнен в профиле.
      </p>

      <Section title="Провайдер">
        <Field label="URL API провайдера">
          <input
            value={form.providerUrl}
            onChange={(e) => set('providerUrl', e.target.value)}
            placeholder="https://api.smsru.ru/sms/send"
            style={inputStyle}
          />
        </Field>
        <Field label="API-ключ">
          <input
            type="password"
            value={form.apiKey}
            onChange={(e) => set('apiKey', e.target.value)}
            placeholder="••••••••"
            style={inputStyle}
          />
        </Field>
        <Field label="Номер отправителя (From)">
          <input
            value={form.fromNumber}
            onChange={(e) => set('fromNumber', e.target.value)}
            placeholder="+79001234567"
            style={inputStyle}
          />
        </Field>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8 }}>
          <input
            type="checkbox"
            id="isEnabled"
            checked={form.isEnabled}
            onChange={(e) => set('isEnabled', e.target.checked)}
          />
          <label htmlFor="isEnabled" style={{ fontSize: 14, cursor: 'pointer' }}>
            SMS-уведомления включены
          </label>
        </div>
      </Section>

      <Section title="Параметры HTTP-запроса">
        <p style={{ margin: '0 0 12px', color: '#6b7280', fontSize: 13 }}>
          Имена параметров для POST-запроса к провайдеру (Form-encoded).
        </p>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
          <Field label="Параметр телефона">
            <input
              value={form.phoneParamName}
              onChange={(e) => set('phoneParamName', e.target.value)}
              placeholder="to"
              style={inputStyle}
            />
          </Field>
          <Field label="Параметр сообщения">
            <input
              value={form.messageParamName}
              onChange={(e) => set('messageParamName', e.target.value)}
              placeholder="msg"
              style={inputStyle}
            />
          </Field>
          <Field label="Параметр API-ключа">
            <input
              value={form.apiKeyParamName}
              onChange={(e) => set('apiKeyParamName', e.target.value)}
              placeholder="api_id"
              style={inputStyle}
            />
          </Field>
        </div>
      </Section>

      {msg && (
        <div style={{ padding: '8px 12px', borderRadius: 6, marginBottom: 16,
          background: msg.startsWith('✅') ? '#f0fdf4' : '#fef2f2',
          color: msg.startsWith('✅') ? '#166534' : '#991b1b', fontSize: 14 }}>
          {msg}
        </div>
      )}

      {testResult && (
        <div style={{ padding: '8px 12px', borderRadius: 6, marginBottom: 16,
          background: testResult.startsWith('✅') ? '#f0fdf4' : '#fef2f2',
          color: testResult.startsWith('✅') ? '#166534' : '#991b1b', fontSize: 14 }}>
          {testResult}
        </div>
      )}

      <div style={{ display: 'flex', gap: 10 }}>
        <button onClick={handleSave} disabled={saving} style={btnPrimary}>
          {saving ? 'Сохранение...' : '💾 Сохранить'}
        </button>
        <button onClick={handleTest} disabled={testing} style={btnSecondary}>
          {testing ? 'Проверяем...' : '🔌 Тест соединения'}
        </button>
      </div>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ marginBottom: 24, padding: '20px 24px', border: '1px solid #e5e7eb', borderRadius: 8, background: '#fff' }}>
      <h2 style={{ margin: '0 0 16px', fontSize: 15, fontWeight: 600 }}>{title}</h2>
      {children}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div style={{ marginBottom: 12 }}>
      <label style={{ display: 'block', marginBottom: 4, fontSize: 13, fontWeight: 500, color: '#374151' }}>
        {label}
      </label>
      {children}
    </div>
  );
}

const inputStyle: React.CSSProperties = {
  width: '100%', padding: '8px 10px', border: '1px solid #d1d5db',
  borderRadius: 6, fontSize: 14, boxSizing: 'border-box',
};
const btnPrimary: React.CSSProperties = {
  padding: '9px 18px', background: '#3b82f6', color: '#fff', border: 'none',
  borderRadius: 6, cursor: 'pointer', fontSize: 14, fontWeight: 500,
};
const btnSecondary: React.CSSProperties = {
  padding: '9px 18px', background: '#f3f4f6', color: '#374151', border: 'none',
  borderRadius: 6, cursor: 'pointer', fontSize: 14, fontWeight: 500,
};
