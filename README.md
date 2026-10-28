# CloudStorage

Учебный REST API для облачного хранения файлов: пользователь регистрируется, входит по JWT и работает только со своими файлами — загружает, смотрит список, скачивает и удаляет. Метаданные хранятся в PostgreSQL, содержимое файлов — на диске.

## 1. Описание проекта

- Аутентификация по email и паролю, авторизация через JWT (ASP.NET Core Identity + JWT Bearer).
- Каждый запрос к файлам фильтруется по идентификатору пользователя из токена: чужие файлы недоступны и возвращают `404`.
- Загрузка через `multipart/form-data`, лимит 100 МБ, пустой файл отклоняется.
- Единый формат ошибок — `ProblemDetails` (`application/problem+json`) с `traceId`; необработанные исключения не раскрывают стектрейсы.
- Файл на диске и запись в базе создаются/удаляются согласованно: при ошибке записи в БД загруженный файл удаляется.

## 2. Архитектура и назначение проектов

```
CloudStorage.sln
├── CloudStorage.API          приложение
│   ├── Controllers           AuthController, FilesController — HTTP-слой
│   ├── Services              FileStorageService (сценарии работы с файлами),
│   │   ├── Auth              JwtTokenService
│   │   ├── FileSystem        LocalFileStorage — физическое хранение файлов
│   │   └── Mapping           StoredFileMapper — модель → DTO
│   ├── Repositories          StoredFileRepository — доступ к данным
│   ├── Models, DTOs          сущности EF Core и контракты API
│   ├── Errors                ApiExceptionHandler — исключение → ProblemDetails
│   └── Migrations            миграции EF Core
├── CloudStorage.Tests        тесты
│   ├── Unit                  сервисы и контроллеры на фейках, без БД и диска
│   └── Integration           WebApplicationFactory + реальный PostgreSQL
├── Dockerfile                многоэтапная сборка (restore → build → publish → runtime)
└── docker-compose.yml        API + PostgreSQL + постоянные тома
```

Поток запроса к файлам:

```
HTTP → JWT → FilesController → IFileStorageService
                                   ├── IStoredFileRepository → PostgreSQL (метаданные)
                                   └── ILocalFileStorage     → диск (содержимое)
```

**CloudStorage.API** — единственный исполняемый проект. Слои разделены интерфейсами (`IFileStorageService`, `IStoredFileRepository`, `ILocalFileStorage`, `IStoredFileMapper`), все зависимости регистрируются в `Program.cs`.

**CloudStorage.Tests** — два вида тестов:

- *Unit* — `FileStorageService`, `FilesController`, `StoredFileMapper` с ручными фейками; не требуют ни базы, ни диска.
- *Integration* — приложение целиком через `WebApplicationFactory` на отдельной базе PostgreSQL (`cloudstorage_tests`), с миграциями и очисткой данных перед прогоном.

## 3. Технологический стек

| Слой | Технологии |
|---|---|
| Платформа | .NET 10, ASP.NET Core MVC |
| Данные | EF Core 10, Npgsql, PostgreSQL 17 |
| Аутентификация | ASP.NET Core Identity Core, JWT Bearer (HS256) |
| Ошибки и документация | ProblemDetails, Swashbuckle (Swagger) |
| Тесты | xUnit, Microsoft.AspNetCore.Mvc.Testing, Npgsql |
| Инфраструктура | Docker, Docker Compose, многоэтапная сборка |

## 4. Запуск

### Локально

Нужны .NET SDK 10 и Docker (для PostgreSQL).

```powershell
# 1. База данных
docker compose up -d db

# 2. Секреты (в репозиторий не попадают)
dotnet user-secrets set "Jwt:Secret" "<ключ не короче 32 символов>" --project CloudStorage.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cloudstorage;Username=admin;Password=<пароль>" --project CloudStorage.API

# 3. Миграции
dotnet tool install --global dotnet-ef
dotnet ef database update --project CloudStorage.API

# 4. Запуск
dotnet run --project CloudStorage.API
```

Swagger UI доступен только в окружении Development: `http://localhost:5197/swagger`.

### Docker Compose

Настройки читаются из `.env` (файл в `.gitignore`):

| Переменная | Назначение |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | параметры базы |
| `JWT_SECRET` | ключ подписи токенов, не короче 32 символов |

```powershell
docker compose up -d --build
docker compose ps                 # db должен стать healthy, api — running
docker compose logs -f api
```

API слушает `http://localhost:8080`. Внутри сети Compose API подключается к базе по имени сервиса (`Host=db`), а не по `localhost`; запуск API ждёт, пока healthcheck базы (`pg_isready`) сообщит о готовности принимать подключения.

Тома: `postgres_data` — данные PostgreSQL, `uploads` — загруженные файлы.

На свежем томе схему нужно создать отдельно: `dotnet ef database update --project CloudStorage.API` с хоста (порт 5432 проброшен) — приложение миграции при старте не выполняет.

## 5. Endpoints

| Метод | Путь | Доступ | Описание | Ответы |
|---|---|---|---|---|
| POST | `/api/auth/register` | анонимно | Регистрация по email и паролю | `200`, `400` |
| POST | `/api/auth/login` | анонимно | Вход, возвращает JWT | `200`, `400`, `401` |
| GET | `/api/files` | JWT | Список файлов текущего пользователя | `200`, `401` |
| POST | `/api/files` | JWT | Загрузка файла, поле формы `file`, до 100 МБ | `201`, `400`, `401`, `413` |
| GET | `/api/files/{id}/download` | JWT | Скачивание своего файла | `200`, `401`, `404` |
| DELETE | `/api/files/{id}` | JWT | Удаление своего файла | `204`, `401`, `404` |
| GET | `/api/health/db` | анонимно | Проверка подключения к базе | `200`, `500` |
| GET | `/swagger` | только Development | Swagger UI | — |

Ошибки возвращаются в формате `ProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Неверный email или пароль.",
  "traceId": "0HN7..."
}
```

## 6. Пример: регистрация, вход и загрузка

Дальше используется `curl` (в Windows доступен как `curl.exe`). Для `bash` перенос строки — `\`, для PowerShell — обратная кавычка.

Примеры рассчитаны на запуск в Docker (`http://localhost:8080`); при локальном запуске замените порт на `5197`.

### Регистрация

```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Passw0rd!23"}'
```

```text
User register successfully
```

Пароль должен удовлетворять политике Identity: не меньше 8 символов, заглавная, строчная, цифра и спецсимвол.

### Вход

```bash
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Passw0rd!23"}'
```

```json
{ "access_token": "eyJhbGciOiJIUzI1NiIs..." }
```

```bash
TOKEN=<access_token из ответа>
```

### Загрузка файла

```bash
curl -X POST http://localhost:8080/api/files \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@report.pdf;type=application/pdf"
```

```json
{
  "id": "0f0c8a2e-6c1d-4a3e-9f0b-7f1f0b8c7a11",
  "fileName": "report.pdf",
  "contentType": "application/pdf",
  "size": 24576,
  "createdAtUtc": "2026-10-28T20:15:03.421Z"
}
```

### Остальные операции

```bash
curl http://localhost:8080/api/files -H "Authorization: Bearer $TOKEN"
curl -OJ http://localhost:8080/api/files/<id>/download -H "Authorization: Bearer $TOKEN"
curl -X DELETE http://localhost:8080/api/files/<id> -H "Authorization: Bearer $TOKEN" -i
```

Без токена защищённые методы отвечают `401`; чужой файл по `id` — `404`.

## 7. Тесты

```powershell
dotnet test CloudStorage.Tests\CloudStorage.Tests.csproj
```

Интеграционным тестам нужен PostgreSQL. Строка подключения берётся из переменной окружения `CLOUDSTORAGE_TEST_CONNECTION` либо из user-secrets API; имя базы заменяется на `cloudstorage_tests`, база создаётся автоматически, миграции применяются в фикстуре. Рабочая база не затрагивается.

Запуск только одного вида тестов:

```powershell
dotnet test CloudStorage.Tests\CloudStorage.Tests.csproj --filter FullyQualifiedName~Unit
dotnet test CloudStorage.Tests\CloudStorage.Tests.csproj --filter FullyQualifiedName~Integration
```

## 8. Ограничения MVP и дальнейшие планы

Ограничения текущей версии:

- Нет refresh-токенов: `Jwt:RefreshTokenExpirationDays` есть в конфигурации, но логики обновления нет — access-токен живёт 15 минут.
- Нет подтверждения email, восстановления пароля, смены пароля и rate limiting на вход.
- Лимит 100 МБ задан атрибутом `[RequestSizeLimit]`; в бизнес-логике проверки размера нет, пустой файл проверяется в контроллере.
- Нет пагинации и сортировки в `GET /api/files` — возвращается весь список.
- Файлы хранятся на локальном диске; нет S3-совместимого хранилища, шифрования и проверки содержимого.
- У `StoredFiles.UserId` нет индекса и внешнего ключа; удаление файла с диска выполняется до удаления записи из БД.
- Миграции не применяются автоматически при старте; HTTPS-редирект в контейнере не работает (нужен reverse proxy).

Планы:

- Выделить слой `Application`: перенести проверки файла и сценарии из контроллера/сервиса, убрать `IFormFile` из бизнес-логики.
- Refresh-токены и отзыв сессий, ограничение частоты запросов.
- Пагинация, поиск и сортировка списка файлов.
- Хранилище за интерфейсом `ILocalFileStorage` → реализация для S3/MinIO.
- Индексы и внешние ключи, транзакционная согласованность файла и записи.
- CI с unit- и интеграционными тестами, автоматические миграции, health-проверки API.
