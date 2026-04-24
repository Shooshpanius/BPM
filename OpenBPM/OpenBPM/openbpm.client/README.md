# Open BPM — Клиентская часть (Frontend)

SPA-приложение на **React 19 + TypeScript**, собираемое с помощью **Vite**.  
Взаимодействует с серверной частью через REST API и SignalR.

---

## Стек технологий

| Инструмент | Версия | Назначение |
|------------|--------|-----------|
| React | 19 | UI-фреймворк |
| TypeScript | ~6.0 | Статическая типизация |
| Vite | 8 | Сборщик и dev-сервер |
| ESLint | 10 | Линтер кода |

---

## Структура каталогов

```
openbpm.client/
├── public/               # Статические файлы (favicon и т.п.)
├── src/
│   ├── api/              # Сервисные функции для обращения к REST API
│   ├── assets/           # Изображения, иконки и прочие ресурсы
│   ├── components/       # Переиспользуемые UI-компоненты
│   ├── pages/            # Страницы приложения (роутинг)
│   ├── hooks/            # Кастомные React-хуки
│   ├── store/            # Глобальное состояние (Zustand / Context)
│   ├── utils/            # Вспомогательные функции
│   ├── App.tsx           # Корневой компонент
│   └── main.tsx          # Точка входа
├── index.html
├── vite.config.ts        # Конфигурация Vite (proxy → ASP.NET Core)
├── tsconfig.json
├── eslint.config.js
└── package.json
```

---

## Быстрый старт

### Требования

- [Node.js 20+](https://nodejs.org/) и npm
- Запущенный бэкенд (`OpenBPM.Server`) — для работы API-прокси

### Установка зависимостей

```bash
cd OpenBPM/OpenBPM/openbpm.client
npm install
```

### Запуск в режиме разработки

```bash
npm run dev
```

Dev-сервер запустится на `https://localhost:54959`.  
Все запросы к API проксируются на ASP.NET Core backend.

> **Примечание:** При первом запуске Vite автоматически создаёт HTTPS-сертификат разработчика через `dotnet dev-certs`.

### Сборка для production

```bash
npm run build
```

Артефакты будут помещены в папку `dist/`.

### Линтинг

```bash
npm run lint
```

---

## Соглашения по разработке

- Компоненты — **функциональные**, с хуками; никаких классовых компонентов.
- Серверные данные — **TanStack Query (React Query)**; UI-состояние — **Zustand / Context**.
- Стили — **CSS Modules** или **Tailwind CSS**; inline-стили использовать только в крайних случаях.
- Именование файлов: `PascalCase` для компонентов (`UserProfile.tsx`), `camelCase` для утилит (`formatDate.ts`).
- Типизация: всегда явно типизировать props и возвращаемые значения; избегать `any`.
- API-запросы — через сервисные файлы в `src/api/` с использованием `axios` или обёртки над `fetch`.

---

## Связанные ресурсы

- [Документация проекта (корневой README)](../../../../README.md)
- [Бэкенд (OpenBPM.Server)](../OpenBPM.Server/README.md)
- [Функциональные требования](../../../../todo.md)
