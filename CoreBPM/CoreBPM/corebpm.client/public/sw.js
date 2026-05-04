// Service Worker для Web Push уведомлений (FR-MSG-02.1)
// Файл: public/sw.js — регистрируется автоматически через usePushNotifications

self.addEventListener('push', (event) => {
  if (!event.data) return;

  let data = {};
  try {
    data = event.data.json();
  } catch {
    data = { title: 'Core BPM', body: event.data.text() };
  }

  const title = data.title || 'Core BPM';
  const options = {
    body: data.body || '',
    icon: data.icon || '/favicon.svg',
    badge: '/favicon.svg',
    data: { url: data.url || '/notifications' },
    requireInteraction: false,
    tag: 'bpm-notification',
    renotify: true,
  };

  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const url = event.notification.data?.url || '/notifications';

  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clientList) => {
      for (const client of clientList) {
        if (client.url.includes(self.location.origin) && 'focus' in client) {
          client.navigate(url);
          return client.focus();
        }
      }
      if (clients.openWindow) {
        return clients.openWindow(url);
      }
    })
  );
});
