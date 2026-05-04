import { useCallback, useEffect, useState } from 'react';

const API = '/api';

/** Конвертирует Base64url-строку в Uint8Array для VAPID public key. */
function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = atob(base64);
  return Uint8Array.from([...rawData].map((c) => c.charCodeAt(0)));
}

export type PushPermission = 'default' | 'granted' | 'denied' | 'unsupported';

/**
 * Хук управления Web Push подпиской браузера (FR-MSG-02.1).
 *
 * Использование:
 *   const { permission, subscribed, subscribe, unsubscribe } = usePushNotifications();
 */
export function usePushNotifications() {
  const [permission, setPermission] = useState<PushPermission>('default');
  const [subscribed, setSubscribed] = useState(false);
  const [loading, setLoading] = useState(false);

  const isSupported =
    typeof window !== 'undefined' &&
    'serviceWorker' in navigator &&
    'PushManager' in window;

  useEffect(() => {
    if (!isSupported) {
      setPermission('unsupported');
      return;
    }
    setPermission(Notification.permission as PushPermission);
    // Проверяем текущую подписку
    navigator.serviceWorker.ready.then((reg) => {
      reg.pushManager.getSubscription().then((sub) => {
        setSubscribed(!!sub);
      });
    });
  }, [isSupported]);

  /** Зарегистрировать Service Worker и подписаться на push-уведомления. */
  const subscribe = useCallback(async (): Promise<boolean> => {
    if (!isSupported) return false;
    setLoading(true);
    try {
      // Получаем VAPID public key
      const keyRes = await fetch(`${API}/notifications/vapid-public-key`);
      if (!keyRes.ok) {
        console.warn('[Push] VAPID ключ не настроен');
        return false;
      }
      const { publicKey } = await keyRes.json();

      // Регистрируем Service Worker
      const reg = await navigator.serviceWorker.register('/sw.js', { scope: '/' });
      await navigator.serviceWorker.ready;

      // Запрашиваем разрешение
      const perm = await Notification.requestPermission();
      setPermission(perm as PushPermission);
      if (perm !== 'granted') return false;

      // Создаём подписку
      const sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey),
      });

      const subJson = sub.toJSON();
      const keys = subJson.keys as { p256dh: string; auth: string };

      // Сохраняем на сервере
      await fetch(`${API}/users/me/push-subscription`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          endpoint: sub.endpoint,
          p256dh: keys.p256dh,
          auth: keys.auth,
        }),
      });

      setSubscribed(true);
      return true;
    } catch (err) {
      console.error('[Push] Ошибка подписки:', err);
      return false;
    } finally {
      setLoading(false);
    }
  }, [isSupported]);

  /** Отписаться от push-уведомлений. */
  const unsubscribe = useCallback(async (): Promise<void> => {
    if (!isSupported) return;
    setLoading(true);
    try {
      const reg = await navigator.serviceWorker.ready;
      const sub = await reg.pushManager.getSubscription();
      if (sub) {
        await fetch(`${API}/users/me/push-subscription`, {
          method: 'DELETE',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ endpoint: sub.endpoint }),
        });
        await sub.unsubscribe();
      }
      setSubscribed(false);
    } catch (err) {
      console.error('[Push] Ошибка отписки:', err);
    } finally {
      setLoading(false);
    }
  }, [isSupported]);

  return { permission, subscribed, loading, isSupported, subscribe, unsubscribe };
}
