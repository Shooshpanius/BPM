# Core BPM

<div align="center">

[![GitHub Stars](https://img.shields.io/github/stars/Shooshpanius/BPM?style=flat-square&logo=github&label=Stars&cacheSeconds=3600)](https://github.com/Shooshpanius/BPM/stargazers)
[![GitHub Forks](https://img.shields.io/github/forks/Shooshpanius/BPM?style=flat-square&logo=github&label=Forks&cacheSeconds=3600)](https://github.com/Shooshpanius/BPM/forks)
[![GitHub Issues](https://img.shields.io/github/issues/Shooshpanius/BPM?style=flat-square&logo=github&label=Issues)](https://github.com/Shooshpanius/BPM/issues)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue?style=flat-square)](LICENSE)

![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![React](https://img.shields.io/badge/React_19-61DAFB?style=flat-square&logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?style=flat-square&logo=typescript&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![JWT](https://img.shields.io/badge/JWT-000000?style=flat-square&logo=jsonwebtokens&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat-square&logo=docker&logoColor=white)

</div>

**Core BPM** — корпоративная система управления бизнес-процессами (Business Process Management).  
Система предназначена для моделирования, автоматизации и мониторинга бизнес-процессов предприятия.

---

## Степень готовности подсистем

| # | Подсистема | Прогресс | % |
|---|-----------|----------|---|
| 1 | Аутентификация и авторизация | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 2 | Профиль и оргструктура | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 3 | Главная страница и портал | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 4 | Бизнес-процессы | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 5 | Задачи | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 6 | Документооборот | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 7 | Трудозатраты | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 8 | Календарь | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 9 | CRM (Клиенты) | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 10 | Сообщения и уведомления | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 11 | Справочники (Объектная модель) | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 12 | Бизнес-правила | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 13 | Конструктор интерфейсов (Low-code UI) | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 14 | Интеграции | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 15 | Отчёты и аналитика | `░░░░░░░░░░░░░░░░░░░░` | 0% |
| 16 | Администрирование | `░░░░░░░░░░░░░░░░░░░░` | 0% |

> `█` — выполнено, `░` — в разработке. Прогресс рассчитывается по закрытым пунктам в [todo.md](todo.md).

---

## Возможности системы

- **Аутентификация и авторизация** — JWT, Refresh Token, X.509, OTP/FIDO2 (2FA), SSO (SAML 2.0 / OIDC), LDAP/AD
- **Управление пользователями** — профили, роли (RBAC), организационная структура, делегирование полномочий
- **Бизнес-процессы** — моделирование (BPMN), запуск, мониторинг, история выполнения
- **Задачи** — создание, назначение, согласование, трудозатраты, контроль сроков
- **Документы** — шаблоны, маршруты согласования, электронная подпись
- **Уведомления** — in-app (SignalR), email, настраиваемые подписки
- **Отчёты и аналитика** — дашборды, фильтры, экспорт данных
- **Администрирование** — системные настройки, аудит действий, управление сессиями

---

## Стек технологий

| Слой | Технология |
|------|-----------|
| Backend | C# ASP.NET Core (.NET 10), Web API |
| Realtime | SignalR |
| Frontend | React 19 + TypeScript, Vite |
| Аутентификация | JWT (access token) + Refresh token (HttpOnly cookie) |
| Архитектура | Модульный монолит (REST API + SPA) |

---

## Структура проекта

```
BPM/
├── README.md                          # Этот файл — общая информация о проекте
├── todo.md                            # Функциональные требования (источник правды)
└── CoreBPM/
    ├── .env.example                   # Шаблон переменных окружения
    ├── docker-compose.yml             # Docker Compose — запуск всей системы одной командой
    ├── CoreBPM.slnx                   # Файл решения Visual Studio
    └── CoreBPM/
        ├── .dockerignore
        ├── CoreBPM.Server/            # Серверная часть (Backend, C# ASP.NET Core)
        │   ├── Dockerfile             # Образ для production-сборки бэкенда
        │   └── README.md              # → Документация бэкенда
        └── corebpm.client/            # Клиентская часть (Frontend, React + TypeScript)
            ├── Dockerfile             # Образ для production-сборки фронтенда (Nginx)
            ├── nginx.conf             # Конфигурация Nginx для раздачи SPA
            ├── vite.config.prod.ts    # Конфигурация Vite для production-сборки
            └── README.md              # → Документация фронтенда
```

---

## Быстрый старт

### Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) и npm

### Запуск в режиме разработки

```bash
# Клонирование репозитория
git clone https://github.com/Shooshpanius/BPM.git
cd BPM

# Запуск (бэкенд автоматически запустит фронтенд через SPA Proxy)
cd CoreBPM/CoreBPM/CoreBPM.Server
dotnet run
```

Приложение будет доступно по адресу: `https://localhost:54959`

### Запуск через Docker Compose

Для production-окружения и быстрого развёртывания используйте Docker Compose.

**Требования:** [Docker](https://docs.docker.com/get-docker/) и [Docker Compose](https://docs.docker.com/compose/install/).

```bash
# Клонирование репозитория
git clone https://github.com/Shooshpanius/BPM.git
cd BPM/CoreBPM

# Создание файла с переменными окружения (обязательно!)
cp .env.example .env
# Отредактируйте .env: задайте BPM_S_DB_PASSWORD и BPM_S_Jwt__Secret

# Сборка образов и запуск всех сервисов
docker compose up --build -d
```

После запуска:
- **Фронтенд** доступен на `http://localhost:80` (порт задаётся через `BPM_C_PORT` в `.env`)
- **Бэкенд API** доступен внутри сети Docker по адресу `http://server:8080`

```bash
# Остановка сервисов
docker compose down

# Остановка с удалением данных БД
docker compose down -v
```

---

## Конфигурация (переменные окружения)

Все настройки хранятся в файле `.env` (создаётся из `.env.example`).

| Переменная | Описание | Значение по умолчанию |
|------------|----------|-----------------------|
| `BPM_S_DB_PASSWORD` | Пароль базы данных PostgreSQL | *(обязательно задать)* |
| `BPM_S_DB_NAME` | Имя базы данных | `corebpm` |
| `BPM_S_DB_USER` | Пользователь базы данных | `corebpm` |
| `BPM_S_Jwt__Secret` | Секрет для подписи JWT | *(обязательно задать)* |
| `BPM_S_Jwt__AccessTokenExpirationMinutes` | Время жизни access-токена (мин) | `15` |
| `BPM_S_Jwt__RefreshTokenExpirationDays` | Время жизни refresh-токена (дней) | `7` |
| `BPM_C_PORT` | Порт клиента на хост-машине | `80` |

> **Соглашение об именовании:** переменные `BPM_S_*` — для бэкенда (ASP.NET Core),  
> переменные `BPM_C_*` — для фронтенда (Vite/React, доступны как `import.meta.env.BPM_C_*`).

---

## Документация

| Документ | Описание |
|----------|----------|
| [Функциональные требования](todo.md) | Полный список требований к системе |
| [Шаблон переменных окружения](CoreBPM/.env.example) | Переменные окружения для настройки системы |
| [Docker Compose](CoreBPM/docker-compose.yml) | Конфигурация для развёртывания через Docker |
| [Бэкенд (Server)](CoreBPM/CoreBPM/CoreBPM.Server/README.md) | Настройка и разработка серверной части |
| [Фронтенд (Client)](CoreBPM/CoreBPM/corebpm.client/README.md) | Настройка и разработка клиентской части |

---

## Лицензия

Распространяется под лицензией [LICENSE](LICENSE).
