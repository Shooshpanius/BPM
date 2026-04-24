# Open BPM

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

</div>

**Open BPM** — корпоративная система управления бизнес-процессами (Business Process Management).  
Система предназначена для моделирования, автоматизации и мониторинга бизнес-процессов предприятия.

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
└── OpenBPM/
    ├── OpenBPM.slnx                   # Файл решения Visual Studio
    └── OpenBPM/
        ├── OpenBPM.Server/            # Серверная часть (Backend, C# ASP.NET Core)
        │   └── README.md              # → Документация бэкенда
        └── openbpm.client/            # Клиентская часть (Frontend, React + TypeScript)
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
cd OpenBPM/OpenBPM/OpenBPM.Server
dotnet run
```

Приложение будет доступно по адресу: `https://localhost:54959`

---

## Документация

| Документ | Описание |
|----------|----------|
| [Функциональные требования](todo.md) | Полный список требований к системе |
| [Бэкенд (Server)](OpenBPM/OpenBPM/OpenBPM.Server/README.md) | Настройка и разработка серверной части |
| [Фронтенд (Client)](OpenBPM/OpenBPM/openbpm.client/README.md) | Настройка и разработка клиентской части |

---

## Лицензия

Распространяется под лицензией [LICENSE](LICENSE).
