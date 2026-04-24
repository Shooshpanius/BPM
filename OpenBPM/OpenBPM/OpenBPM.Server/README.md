# Open BPM — Серверная часть (Backend)

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
OpenBPM.Server/
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
└── OpenBPM.Server.csproj
```

---

## Быстрый старт

### Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Запуск в режиме разработки

```bash
cd OpenBPM/OpenBPM/OpenBPM.Server
dotnet run
```

- API будет доступен на порту, указанном в `launchSettings.json` (по умолчанию `http://localhost:5071`).
- При наличии проекта `openbpm.client` бэкенд автоматически запустит фронтенд через **SPA Proxy** (`https://localhost:54959`).
- Документация API (OpenAPI): `https://localhost:{port}/openapi/v1.json` (только в режиме Development).

### Сборка

```bash
dotnet build
```

### Публикация

```bash
dotnet publish -c Release -o ./publish
```

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

---

## Аутентификация

- **Access Token** (JWT): короткое время жизни (15–60 мин), передаётся в заголовке `Authorization: Bearer <token>`.
- **Refresh Token**: длинное время жизни (7–30 дней), передаётся в HttpOnly cookie.
- Эндпоинты: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`.

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
- [Фронтенд (openbpm.client)](../openbpm.client/README.md)
- [Функциональные требования](../../../../todo.md)
