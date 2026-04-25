# Open BPM — Клиентская часть (Frontend)

<div align="center">

[![GitHub Stars](https://img.shields.io/github/stars/Shooshpanius/BPM?style=flat-square&logo=github&label=Stars&cacheSeconds=3600)](https://github.com/Shooshpanius/BPM/stargazers)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue?style=flat-square)](../../../../LICENSE)

![React](https://img.shields.io/badge/React_19-61DAFB?style=flat-square&logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?style=flat-square&logo=typescript&logoColor=white)
![Vite](https://img.shields.io/badge/Vite_8-646CFF?style=flat-square&logo=vite&logoColor=white)
![ESLint](https://img.shields.io/badge/ESLint_10-4B32C3?style=flat-square&logo=eslint&logoColor=white)

</div>

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
├── vite.config.ts        # Конфигурация Vite (proxy → ASP.NET Core, для разработки)
├── vite.config.prod.ts   # Конфигурация Vite для production-сборки (Docker)
├── nginx.conf            # Конфигурация Nginx для раздачи SPA в Docker
├── Dockerfile            # Многоэтапная сборка: Node.js (build) → Nginx (runtime)
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

> При сборке в Docker используется `vite.config.prod.ts` — конфигурация без dev-server настроек  
> (HTTPS-сертификаты, SPA Proxy), которые требуют наличия `dotnet dev-certs`.

### Запуск в Docker

Клиент поставляется с многоэтапным `Dockerfile` (сборка на `node:lts-alpine`, runtime на `nginx:stable-alpine`).  
Рекомендуемый способ развёртывания — через **Docker Compose** из корня репозитория (см. [корневой README](../../../../README.md)).

```bash
# Сборка образа вручную (из каталога openbpm.client/)
docker build -t openbpm-client .

# Запуск контейнера
docker run -p 80:80 openbpm-client
```

> Nginx раздаёт статику SPA и перенаправляет все пути на `index.html` (client-side routing).

---

## Переменные окружения

Клиент читает переменные с префиксом `BPM_C_` во время **сборки** (не в runtime).  
В коде они доступны как `import.meta.env.BPM_C_*`.

| Переменная | Описание | Значение по умолчанию |
|------------|----------|-----------------------|
| `BPM_C_PORT` | Порт клиента на хост-машине (в docker-compose) | `80` |

Полный список переменных — в [`OpenBPM/.env.example`](../../../.env.example).

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
