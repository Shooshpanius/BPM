# Core BPM — Серверная часть (Backend)

<div align="center">

[![GitHub Stars](https://img.shields.io/github/stars/Shooshpanius/BPM?style=flat-square&logo=github&label=Stars&cacheSeconds=3600)](https://github.com/Shooshpanius/BPM/stargazers)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue?style=flat-square)](../../../../LICENSE)

![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![JWT](https://img.shields.io/badge/JWT-000000?style=flat-square&logo=jsonwebtokens&logoColor=white)
![Swagger](https://img.shields.io/badge/Swagger-85EA2D?style=flat-square&logo=swagger&logoColor=black)
![Serilog](https://img.shields.io/badge/Serilog-CC2936?style=flat-square&logo=serilog&logoColor=white)

</div>

Web API на **C# ASP.NET Core (.NET 10)**.  
Предоставляет REST API для клиентского SPA, а также realtime-уведомления через **SignalR**.

---

## Стек технологий

| Инструмент | Назначение |
|------------|-----------|
| ASP.NET Core (.NET 10) | Web API фреймворк |
| SignalR | Realtime-уведомления (задачи, смена статусов процессов) |
| JWT + Refresh Token | Аутентификация (access token + HttpOnly cookie) |
| OpenAPI / Swagger | Документация API (`/openapi/v1.json` в режиме разработки) |
| FluentValidation | Валидация входных данных |
| AutoMapper | Маппинг между доменными объектами и DTO |
| Serilog | Структурированное логирование |

---

## Структура каталогов

```
CoreBPM.Server/
├── Controllers/          # API-контроллеры (маршруты /api/*)
├── Domain/               # Доменные сущности и интерфейсы репозиториев
├── Application/          # Бизнес-логика, сервисы, DTO, маппинг
├── Infrastructure/       # Реализация репозиториев, работа с БД, внешние сервисы
├── Middleware/           # Глобальная обработка ошибок, логирование запросов
├── Properties/
│   └── launchSettings.json
├── appsettings.json      # Конфигурация (порты, строки подключения и т.п.)
├── appsettings.Development.json
├── Program.cs            # Точка входа и настройка DI / middleware pipeline
└── CoreBPM.Server.csproj
```

---

## Быстрый старт

### Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Запуск в режиме разработки

```bash
cd CoreBPM/CoreBPM/CoreBPM.Server
dotnet run
```

- API будет доступен на порту, указанном в `launchSettings.json` (по умолчанию `http://localhost:5071`).
- При наличии проекта `corebpm.client` бэкенд автоматически запустит фронтенд через **SPA Proxy** (`https://localhost:54959`).
- Документация API (OpenAPI): `https://localhost:{port}/openapi/v1.json` (только в режиме Development).

### Сборка

```bash
dotnet build
```

### Публикация

```bash
dotnet publish -c Release -o ./publish
```

### Запуск в Docker

Бэкенд поставляется с многоэтапным `Dockerfile` (сборка на `dotnet/sdk:10.0`, runtime на `dotnet/aspnet:10.0`).  
Рекомендуемый способ развёртывания — через **Docker Compose** из корня репозитория (см. [корневой README](../../../../README.md)).

```bash
# Сборка образа вручную (из каталога CoreBPM/CoreBPM/)
docker build -f CoreBPM.Server/Dockerfile -t corebpm-server .

# Запуск контейнера
docker run -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e BPM_S_ConnectionStrings__DefaultConnection="Host=localhost;..." \
  corebpm-server
```

> Порт `8080` — рекомендуемый порт ASP.NET Core в контейнерах (без TLS, за reverse proxy).

---

## Переменные окружения

Бэкенд читает переменные с префиксом `BPM_S_` (настраивается через `AddEnvironmentVariables("BPM_S_")` в `Program.cs`).  
После удаления префикса имя переменной маппируется на конфигурацию ASP.NET Core (разделитель `__` → `:`).

| Переменная | Конфигурационный ключ | Описание |
|------------|----------------------|----------|
| `BPM_S_DB_PASSWORD` | — | Пароль PostgreSQL (используется в строке подключения) |
| `BPM_S_ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | Полная строка подключения к БД |
| `BPM_S_Jwt__Secret` | `Jwt:Secret` | Секрет для подписи JWT |
| `BPM_S_Jwt__AccessTokenExpirationMinutes` | `Jwt:AccessTokenExpirationMinutes` | Время жизни access-токена (мин) |
| `BPM_S_Jwt__RefreshTokenExpirationDays` | `Jwt:RefreshTokenExpirationDays` | Время жизни refresh-токена (дней) |

Полный список переменных — в [`/.env.example`](../../../../.env.example).

---

## API

Все эндпоинты следуют конвенции REST:

| Метод | Маршрут | Описание |
|-------|---------|----------|
| `POST` | `/api/{resource}` | Создание ресурса |
| `GET` | `/api/{resource}/{id}` | Получение ресурса по ID |
| `PUT` | `/api/{resource}/{id}` | Обновление ресурса |
| `DELETE` | `/api/{resource}/{id}` | Удаление ресурса |

Все эндпоинты защищены атрибутом `[Authorize]` по умолчанию.  
Формат ошибок: `{ "error": "...", "details": [...] }`.

### Аутентификация

| Метод | Маршрут | Описание |
|-------|---------|----------|
| `POST` | `/api/auth/login` | Вход по логину и паролю |
| `POST` | `/api/auth/refresh` | Обновление access-токена по refresh cookie |
| `POST` | `/api/auth/logout` | Выход (инвалидация refresh-токена) |

### Административная панель (`[Authorize(Roles = "Admin")]`)

#### Организации

| Метод | Маршрут | Описание |
|-------|---------|----------|
| `GET` | `/api/admin/organizations` | Список всех организаций |
| `POST` | `/api/admin/organizations` | Создать организацию |
| `PUT` | `/api/admin/organizations/{id}` | Обновить организацию |
| `DELETE` | `/api/admin/organizations/{id}` | Удалить организацию |
| `POST` | `/api/admin/organizations/{id}/set-primary` | Назначить организацию основной |

#### Пользователи

| Метод | Маршрут | Описание |
|-------|---------|----------|
| `GET` | `/api/admin/users` | Список пользователей системы |
| `POST` | `/api/admin/users` | Создать пользователя (OrgUser + AuthAccount) |
| `PUT` | `/api/admin/users/{id}` | Обновить профиль пользователя |
| `DELETE` | `/api/admin/users/{id}` | Деактивировать пользователя (мягкое удаление) |
| `GET` | `/api/admin/users/{id}/employees` | Список записей сотрудника для данного пользователя |

#### Сотрудники

| Метод | Маршрут | Описание |
|-------|---------|----------|
| `GET` | `/api/admin/employees` | Список сотрудников (фильтр `?organizationId=`) |
| `POST` | `/api/admin/employees` | Создать запись сотрудника (привязать пользователя к организации) |
| `PUT` | `/api/admin/employees/{id}` | Обновить должность / статус сотрудника |
| `DELETE` | `/api/admin/employees/{id}` | Удалить запись сотрудника |

---

## Аутентификация

- **Access Token** (JWT): короткое время жизни (15–60 мин), передаётся в заголовке `Authorization: Bearer <token>`.
- **Refresh Token**: длинное время жизни (7–30 дней), передаётся в HttpOnly cookie.
- При первом запуске автоматически создаётся пользователь **admin** с паролем из `Admin:Password` (переменная окружения `BPM_S_Admin__Password`).

---

## Конфигурация для разработки

Файл `appsettings.Development.json` содержит настройки, пригодные для локального запуска.  
Перед стартом убедитесь, что PostgreSQL доступен по адресу `localhost:5432` с пользователем `postgres` и паролем `postgres`, либо скорректируйте строку подключения.

| Ключ | Описание | Значение по умолчанию (dev) |
|------|----------|----------------------------|
| `ConnectionStrings:DefaultConnection` | Строка подключения к PostgreSQL | `Host=localhost;Port=5432;Database=corebpm_dev;...` |
| `Jwt:SecretKey` | Секрет для подписи JWT (≥ 32 символа) | dev-ключ (только для разработки) |
| `Jwt:Issuer` | Издатель JWT | `CoreBPM` |
| `Jwt:Audience` | Аудитория JWT | `CoreBPM` |
| `Jwt:AccessTokenLifetimeMinutes` | Время жизни access-токена (мин) | `60` |
| `Jwt:RefreshTokenLifetimeDays` | Время жизни refresh-токена (дней) | `30` |
| `Auth:MaxFailedAttempts` | Попыток входа до блокировки аккаунта | `10` |
| `Auth:LockoutMinutes` | Время блокировки аккаунта (мин) | `5` |
| `Admin:Password` | Пароль автоматически создаваемого пользователя admin | `Admin1234!` |

> **Важно:** не используйте значения из `appsettings.Development.json` в production.  
> Для production задавайте все секреты через переменные окружения с префиксом `BPM_S_` (см. [`.env.example`](../../../../.env.example)).

---

## Соглашения по разработке

- Использовать `async/await` повсеместно; никогда не блокировать поток (`.Result`, `.Wait()`).
- Репозитории — через интерфейсы (`IUserRepository`, `IProcessRepository` и т.д.), внедрение через конструктор.
- Никогда не возвращать доменные сущности напрямую из API — только DTO.
- Доменные исключения: `NotFoundException`, `ValidationException`, `ForbiddenException` — перехватываются глобальным middleware.
- Именование: `PascalCase` для классов и методов, `_camelCase` для приватных полей.
- XML-комментарии на всех публичных методах и контроллерах (для Swagger/OpenAPI).

---

## Связанные ресурсы

- [Документация проекта (корневой README)](../../../../README.md)
- [Фронтенд (corebpm.client)](../corebpm.client/README.md)
- [Функциональные требования](../../../../todo.md)
