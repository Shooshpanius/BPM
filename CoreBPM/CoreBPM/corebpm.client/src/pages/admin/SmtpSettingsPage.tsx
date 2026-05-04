import { useState, useEffect } from 'react';
import {
    getSmtpSettings,
    saveSmtpSettings,
    testSmtpSettings,
    type SmtpSettingsDto,
} from '../../api/notificationsApi';

export default function SmtpSettingsPage() {
    const [form, setForm] = useState<SmtpSettingsDto>({
        host: '',
        port: 587,
        useSsl: true,
        username: null,
        password: null,
        fromAddress: '',
        fromName: 'Core BPM',
    });
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [testing, setTesting] = useState(false);
    const [testResult, setTestResult] = useState<null | boolean>(null);
    const [saved, setSaved] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        getSmtpSettings()
            .then(data => { setForm({ ...data, password: null }); })
            .catch(() => setError('Ошибка загрузки настроек'))
            .finally(() => setLoading(false));
    }, []);

    const handleSave = async () => {
        setSaving(true);
        setError('');
        setSaved(false);
        try {
            await saveSmtpSettings(form);
            setSaved(true);
            setTimeout(() => setSaved(false), 3000);
        } catch {
            setError('Ошибка сохранения настроек');
        } finally {
            setSaving(false);
        }
    };

    const handleTest = async () => {
        setTesting(true);
        setTestResult(null);
        try {
            const ok = await testSmtpSettings();
            setTestResult(ok);
        } finally {
            setTesting(false);
        }
    };

    if (loading) return <p style={{ padding: 24 }}>Загрузка…</p>;

    return (
        <div style={{ maxWidth: 560, margin: '0 auto', padding: '24px 16px' }}>
            <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 24 }}>⚙️ Настройки SMTP</h1>

            {error && (
                <div style={{ background: '#fee2e2', color: '#b91c1c', padding: '10px 14px', borderRadius: 6, marginBottom: 16, fontSize: 13 }}>
                    {error}
                </div>
            )}

            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <Field label="SMTP-сервер (хост)">
                    <input
                        value={form.host}
                        onChange={e => setForm(f => ({ ...f, host: e.target.value }))}
                        placeholder="smtp.example.com"
                        style={inputStyle}
                    />
                </Field>

                <Field label="Порт">
                    <input
                        type="number"
                        value={form.port}
                        onChange={e => setForm(f => ({ ...f, port: Number(e.target.value) }))}
                        style={{ ...inputStyle, width: 120 }}
                    />
                </Field>

                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <input
                        id="smtp-ssl"
                        type="checkbox"
                        checked={form.useSsl}
                        onChange={e => setForm(f => ({ ...f, useSsl: e.target.checked }))}
                    />
                    <label htmlFor="smtp-ssl" style={{ fontSize: 14, color: '#374151' }}>Использовать SSL/TLS</label>
                </div>

                <Field label="Логин (необязательно)">
                    <input
                        value={form.username ?? ''}
                        onChange={e => setForm(f => ({ ...f, username: e.target.value || null }))}
                        autoComplete="off"
                        style={inputStyle}
                    />
                </Field>

                <Field label="Пароль (необязательно; оставьте пустым, чтобы не менять)">
                    <input
                        type="password"
                        value={form.password ?? ''}
                        onChange={e => setForm(f => ({ ...f, password: e.target.value || null }))}
                        autoComplete="new-password"
                        placeholder="••••••••"
                        style={inputStyle}
                    />
                </Field>

                <Field label="Адрес отправителя (From)">
                    <input
                        value={form.fromAddress}
                        onChange={e => setForm(f => ({ ...f, fromAddress: e.target.value }))}
                        placeholder="noreply@example.com"
                        style={inputStyle}
                    />
                </Field>

                <Field label="Имя отправителя">
                    <input
                        value={form.fromName}
                        onChange={e => setForm(f => ({ ...f, fromName: e.target.value }))}
                        placeholder="Core BPM"
                        style={inputStyle}
                    />
                </Field>

                <div style={{ display: 'flex', gap: 10, marginTop: 8 }}>
                    <button
                        onClick={handleSave}
                        disabled={saving}
                        style={{
                            padding: '8px 20px', borderRadius: 6, border: 'none',
                            background: '#3b82f6', color: 'white', fontWeight: 600,
                            cursor: saving ? 'not-allowed' : 'pointer', fontSize: 14,
                        }}
                    >
                        {saving ? 'Сохранение…' : 'Сохранить'}
                    </button>

                    <button
                        onClick={handleTest}
                        disabled={testing}
                        style={{
                            padding: '8px 18px', borderRadius: 6, cursor: testing ? 'not-allowed' : 'pointer',
                            border: '1px solid #d1d5db', background: 'white', fontSize: 14,
                        }}
                    >
                        {testing ? 'Тестирование…' : 'Тест соединения'}
                    </button>
                </div>

                {saved && (
                    <p style={{ color: '#059669', fontSize: 13 }}>✅ Настройки сохранены</p>
                )}
                {testResult !== null && (
                    <p style={{ color: testResult ? '#059669' : '#dc2626', fontSize: 13 }}>
                        {testResult ? '✅ Соединение успешно. Тестовое письмо отправлено.' : '❌ Не удалось подключиться к SMTP-серверу.'}
                    </p>
                )}
            </div>
        </div>
    );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
    return (
        <div>
            <label style={{ display: 'block', fontSize: 13, color: '#374151', marginBottom: 4, fontWeight: 500 }}>
                {label}
            </label>
            {children}
        </div>
    );
}

const inputStyle: React.CSSProperties = {
    width: '100%', padding: '8px 10px', borderRadius: 6,
    border: '1px solid #d1d5db', fontSize: 14, boxSizing: 'border-box',
};
