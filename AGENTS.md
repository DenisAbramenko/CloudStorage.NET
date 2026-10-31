# AGENTS.md

> Если правило ниже противоречит явной просьбе пользователя — выполняй просьбу, но предупреди
> о последствиях (например, о breaking change контракта API или потере данных).

---

## 1. Что это за проект

REST API облачного хранилища файлов на ASP.NET Core (.NET 10):

- пользователь регистрируется и входит по email/паролю, получает JWT (HS256, 15 минут);
- авторизованный пользователь загружает, перечисляет, скачивает и удаляет **только свои** файлы;
- метаданные файлов лежат в PostgreSQL (EF Core + Npgsql), содержимое — на локальном диске;
- ошибки возвращаются в едином формате `ProblemDetails` (`application/problem+json`) с `traceId`;
- инфраструктура: Docker, Docker Compose, PostgreSQL 17.

Проект позиционируется как MVP: часть ограничений осознанна (см. раздел 12). Не «исправляй» их
попутно — только по явному запросу.

---

## 2. Быстрые команды

Все команды выполняются из корня репозитория (`C:\Users\Admin\DEV\CloudStorage`).

### Сборка и запуск

```powershell
dotnet restore CloudStorage.sln
dotnet build CloudStorage.sln
dotnet run --project CloudStorage.API          # http://localhost:5197
```

Swagger UI доступен только в окружении `Development`: `http://localhost:5197/swagger`.

### Тесты

```powershell
dotnet test CloudStorage.Tests\CloudStorage.Tests.csproj
dotnet test CloudStorage.Tests\CloudStorage.Tests.csproj --filter FullyQualifiedName~Unit
dotnet test CloudStorage.Tests\CloudStorage.Tests.csproj --filter FullyQualifiedName~Integration
```

Unit-тесты не требуют базы и диска. Интеграционным нужен PostgreSQL (см. раздел 9).

### Миграции EF Core

```powershell
dotnet tool install --global dotnet-ef          # один раз
dotnet ef migrations add <MigrationName> --project CloudStorage.API
dotnet ef database update --project CloudStorage.API
```

Приложение **не применяет миграции при старте** — это делается вручную или в фикстуре тестов.

### Docker

```powershell
docker compose up -d db                          # только база для локальной разработки
docker compose up -d --build                     # API + PostgreSQL, http://localhost:8080
docker compose ps
docker compose logs -f api
docker compose down                              # тома сохраняются
```

`docker compose down -v` удаляет тома `postgres_data` и `uploads` — **деструктивная операция**,
выполняй только с подтверждением пользователя.

### Секреты (локально)

```powershell
dotnet user-secrets set "Jwt:Secret" "<ключ не короче 32 символов>" --project CloudStorage.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cloudstorage;Username=admin;Password=<пароль>" --project CloudStorage.API
```

Для Docker настройки берутся из `.env` (в `.gitignore`): `POSTGRES_DB`, `POSTGRES_USER`,
`POSTGRES_PASSWORD`, `JWT_SECRET`. Композа не стартует без `JWT_SECRET`.

---

## 3. Структура репозитория

```
CloudStorage.sln
├── CloudStorage.API/                 единственный исполняемый проект
│   ├── Program.cs                    composition root: DI, JWT, EF Core, ProblemDetails, endpoints
│   ├── AppDbContext.cs               IdentityDbContext<ApplicationUser> + DbSet<StoredFile>
│   ├── Controllers/                  AuthController, FilesController — тонкий HTTP-слой
│   ├── Services/                     бизнес-сценарии
│   │   ├── IFileStorageService.cs
│   │   ├── FileStorageService.cs
│   │   ├── Auth/                     IJwtTokenService, JwtTokenService
│   │   ├── FileSystem/               ILocalFileStorage, LocalFileStorage
│   │   └── Mapping/                  IStoredFileMapper, StoredFileMapper
│   ├── Repositories/                 IStoredFileRepository, StoredFileRepository
│   ├── Models/                       EF-сущности: ApplicationUser, StoredFile
│   ├── DTOs/                         контракты API: RegisterRequest, LoginRequest, StoredFileDto, FileDownloadResult
│   ├── Errors/                       ApiExceptionHandler — исключение → ProblemDetails
│   ├── Migrations/                   миграции EF Core (генерируются, не пишутся руками)
│   ├── appsettings*.json             конфигурация без секретов
│   └── App_Data/Uploads/             файлы локально (в .gitignore)
├── CloudStorage.Tests/
│   ├── Unit/                         сервисы/контроллеры/маппер на ручных фейках
│   │   └── Fakes/                    FakeFileStorageService, FakeStoredFileRepository, FakeLocalFileStorage, FakeFormFile
│   └── Integration/                  WebApplicationFactory + реальный PostgreSQL
├── Dockerfile                        многоэтапная сборка, non-root, порт 8080
├── docker-compose.yml                api + db + тома postgres_data/uploads
└── README.md                         пользовательская документация (RU)
```

Поток запроса к файлам:

```
HTTP → JWT → FilesController → IFileStorageService ─┬→ IStoredFileRepository → PostgreSQL
                                                    └→ ILocalFileStorage     → диск
```

---

## 4. Архитектурные правила

1. **Слои разделены интерфейсами.** Новые зависимости оформляй интерфейсом и регистрируй в `Program.cs`
   (`AddScoped`, если нет веской причины для другого времени жизни).
2. **Контроллер — только HTTP.** В нём допустимы: валидация входной модели, извлечение `userId` из
   claims, вызов сервиса, преобразование результата в HTTP-ответ. Бизнес-логика — в сервисах.
3. **Сервис — сценарий.** `FileStorageService` оркестрирует репозиторий и файловое хранилище.
   Он не должен знать про `HttpContext`, `IActionResult` и коды ответов.
4. **Репозиторий — доступ к данным.** EF Core-запросы, `AsNoTracking()` для чтения, фильтрация по
   `UserId` выполняется **в SQL**, а не в памяти.
5. **Модели ≠ DTO.** Наружу отдаются только DTO. `StoredFile` содержит `StorageKey` и `UserId` —
   они не должны попадать в ответ API. Преобразование — через `IStoredFileMapper`.
6. **Изоляция пользователей — инвариант.** Любая операция с файлом фильтруется по `UserId` из JWT.
   Чужой или несуществующий файл возвращает `404`, а не `403` (не раскрываем существование).
7. **`userId` берётся только из claims** (`ClaimTypes.NameIdentifier`). Не принимай `userId`
   из тела запроса, query-string или route.
8. **`public partial class Program { }`** в конце `Program.cs` обязателен: он нужен
   `WebApplicationFactory<Program>` в интеграционных тестах. Не удаляй.

---

## 5. Соглашения по C#-коду

- Целевой фреймворк — **net10.0**, `Nullable` и `ImplicitUsings` включены. Не меняй TFM без запроса.
- **File-scoped namespaces**, пространство имён повторяет структуру папок
  (`CloudStorage.API.Services.Auth`).
- Идентификаторы, имена типов, методов и файлов — **на английском**. Сообщения пользователю,
  `ProblemDetails.detail`, комментарии и README — **на русском**.
- Приватные поля — `_camelCase`; локальные переменные и параметры — `camelCase`; константы — `PascalCase`.
- Предпочитай `var` там, где тип очевиден.
- Асинхронность: `async`/`await` без `.Result` и `.Wait()`. `CancellationToken` — последний параметр,
  пробрасывай его во все нижележащие вызовы. Новые асинхронные методы называй с суффиксом `Async`.
- Guard-условия в одну строку допустимы (`if (file == null) return NotFound();`), как в текущем коде.
- DTO-контракты — `record` с `DataAnnotations` (`[Required]`, `[EmailAddress]`, `[MinLength]`).
  Обязательные поля сущностей/DTO — `required` или инициализатор по умолчанию.
- Для null-аргументов на границе используй `ArgumentNullException.ThrowIfNull(...)`.
- Не добавляй сторонние библиотеки (AutoMapper, MediatR, FluentValidation, Serilog и т.п.) без
  явного запроса. Версии Microsoft-пакетов держи в одной линии `10.0.x`.
- Не переформатируй файлы целиком и не делай «попутных» рефакторингов: diff должен быть минимальным
  и относиться только к задаче.
- Стиль допускает и обычные конструкторы, и primary constructors (см. `ApiExceptionHandler`,
  `AppDbContext`). Следуй стилю соседнего файла.

---

## 6. Контракт HTTP API

Текущие endpoints (менять только осознанно, это публичный контракт):

| Метод | Путь | Доступ | Успех | Ошибки |
|---|---|---|---|---|
| POST | `/api/auth/register` | анонимно | `200` | `400` |
| POST | `/api/auth/login` | анонимно | `200` (`{ "access_token": ... }`) | `400`, `401` |
| GET | `/api/files` | JWT | `200` | `401` |
| POST | `/api/files` | JWT | `201` | `400`, `401`, `413` |
| GET | `/api/files/{id:guid}/download` | JWT | `200` | `401`, `404` |
| DELETE | `/api/files/{id:guid}` | JWT | `204` | `401`, `404` |
| GET | `/api/health/db` | анонимно | `200` | `500` |

Правила:

- Ошибки — только `ProblemDetails`. Для ожидаемых случаев используй `Problem(...)` /
  `ValidationProblem(ModelState)`; для исключений — бросай доменное исключение и полагайся на
  `ApiExceptionHandler`.
- `traceId` в `ProblemDetails` добавляется глобально в `Program.cs` (`AddProblemDetails`).
  Не дублируй его вручную.
- Не раскрывай стектрейсы, пути на диске и внутренние сообщения исключений в ответах.
- `access_token` — **snake_case** (анонимный объект в `AuthController`). Не переименовывай:
  это breaking change для клиентов.
- Лимит загрузки — `[RequestSizeLimit(100 * 1024 * 1024)]` на действии; пустой или отсутствующий
  файл отклоняется в контроллере с `400`.
- Новые поля ответа добавляй аддитивно; удаление/переименование полей — только по явному запросу
  с упоминанием breaking change.
- Swagger включён только в `Development`.

---

## 7. База данных и EF Core

- `AppDbContext` наследует `IdentityDbContext<ApplicationUser>`; таблицы Identity +
  `DbSet<StoredFile> StoredFiles`.
- Для чтения — `AsNoTracking()`; для фильтрации используй SQL-выражения (`Where`), не `ToList()` +
  фильтрация в памяти.
- Миграции **генерируются** командой `dotnet ef migrations add`, а не пишутся руками. Не редактируй
  `AppDbContextModelSnapshot.cs` и `*.Designer.cs` вручную.
- Перед коммитом миграции просмотри сгенерированный код: не должно быть неожиданных `DROP`/`ALTER`
  с потерей данных. О потенциально деструктивной миграции предупреди пользователя.
- Автоматическое применение миграций при старте не настроено и добавлять его без запроса не нужно.
- Известный технический долг: у `StoredFiles.UserId` нет индекса и внешнего ключа на `AspNetUsers`.
  Исправление приветствуется, но требует миграции, тестов и обновления README.

---

## 8. Файловое хранилище

- Корень хранения: `Storage:RootPath`, иначе `<ContentRoot>/App_Data/Uploads`.
  В Docker — `/app/App_Data/Uploads` (том `uploads`).
- `StorageKey` — `Guid.NewGuid().ToString("N")`; имя файла пользователя **никогда** не используется
  как путь (защита от path traversal). Для отображаемого имени применяется `Path.GetFileName`.
- Порядок загрузки: сначала запись на диск, затем запись в БД; при ошибке БД файл удаляется
  (`FileStorageService.UploadAsync`). Сохраняй эту согласованность.
- Порядок удаления сейчас: диск → БД (риск рассинхронизации при сбое). Это известное ограничение;
  если меняешь порядок/добавляешь транзакционность — обнови тесты и README.
- При скачивании отсутствие файла на диске при наличии записи в БД — `FileNotFoundException`
  (маппится в `404`).

---

## 9. Тесты

Стек: xUnit, `Microsoft.AspNetCore.Mvc.Testing`, ручные фейки. Два уровня:

### Unit (`CloudStorage.Tests/Unit`)

- Не должны трогать БД, диск и сеть. Зависимости подменяются фейками из `Unit/Fakes`.
- Покрывают `FileStorageService`, `FilesController`, `StoredFileMapper`.
- Именование: `Method_Scenario_ExpectedResult` (например,
  `UploadAsync_WhenDatabaseFails_RemovesAlreadyStoredFile`).
- Тестируемый объект в поле `_sut`. Комментарии-сценарии на русском допустимы.

### Integration (`CloudStorage.Tests/Integration`)

- `ApiFactory` поднимает приложение через `WebApplicationFactory<Program>` с окружением `Testing`
  и временным `Storage__RootPath` (удаляется после тестов).
- `ApiFixture` — общая фикстура коллекции `api-integration`: создаёт БД, применяет миграции,
  делает `TRUNCATE "StoredFiles", "AspNetUsers" CASCADE` перед прогоном.
- Строка подключения: `CLOUDSTORAGE_TEST_CONNECTION` или user-secrets `ConnectionStrings:DefaultConnection`;
  имя базы заменяется на `cloudstorage_tests`.
- **Защита от опасных ошибок:** имя тестовой базы обязано содержать `test` и соответствовать
  `^[A-Za-z_][A-Za-z0-9_]*$`, иначе тесты падают. Никогда не направляй тесты на рабочую БД.
- Интеграционные тесты выполняются последовательно в одной коллекции. Не ломай изоляцию:
  каждый тест создаёт уникальный email; общие данные не переиспользуются.
- Новый endpoint или изменение поведения → добавь/обнови интеграционный тест. Новую ветку
  бизнес-логики → unit-тест.

### Обязательный минимум перед завершением задачи

```powershell
dotnet build CloudStorage.sln
dotnet test CloudStorage.Tests\CloudStorage.Tests.csproj --filter FullyQualifiedName~Unit
```

Если доступен PostgreSQL — прогони и интеграционные тесты. Если нет, явно скажи об этом в отчёте.
Не удаляй и не ослабляй существующие тесты, чтобы «позеленить» сборку.

---

## 10. Конфигурация и секреты

Ключи конфигурации (см. `appsettings.json`, `Program.cs`):

| Ключ | Назначение |
|---|---|
| `Jwt:Issuer`, `Jwt:Audience` | параметры валидации токена |
| `Jwt:Secret` | ключ подписи, **обязателен**, минимум 32 символа |
| `Jwt:AccessTokenExpirationMinutes` | время жизни access-токена (15) |
| `Jwt:RefreshTokenExpirationDays` | зарезервировано, логики refresh нет |
| `ConnectionStrings:DefaultConnection` | PostgreSQL |
| `Storage:RootPath` | корень файлового хранилища |

- Секреты хранятся в **user-secrets** (локально) или `.env`/переменных окружения (Docker).
  Никогда не коммить их и не выводи в ответах/логах/тестах.
- В конфигурации через переменные окружения используется двойное подчёркивание:
  `Jwt__Secret`, `ConnectionStrings__DefaultConnection`, `Storage__RootPath`.
- `.env`, `.env.*`, `bin/`, `obj/`, `App_Data/Uploads/`, `TestResults/`, `.idea/`, `*.user` —
  в `.gitignore`. Не добавляй их в индекс.
- Не ослабляй проверку `Jwt:Secret` и не подставляй дефолтный ключ «чтобы запустилось».

---

## 11. Docker

- `Dockerfile` — многоэтапная сборка (`restore → build → publish → runtime`), финальный образ
  работает от непривилегированного пользователя, слушает `8080`.
- Контекст сборки — корень репозитория; `CloudStorage.Tests`, `.env`, `bin/obj` исключены
  через `.dockerignore`.
- `docker-compose.yml`: API ждёт healthcheck PostgreSQL (`pg_isready`); внутри сети хост базы — `db`,
  а не `localhost`.
- Тома: `postgres_data` (БД) и `uploads` (файлы). Не удаляй их без подтверждения пользователя.
- На свежем томе схему нужно создать отдельно: `dotnet ef database update --project CloudStorage.API`
  с хоста (порт 5432 проброшен).

---

## 12. Известные ограничения MVP

Это осознанный технический долг, а не баги. Не исправляй без явного запроса; при исправлении
обновляй README (раздел «Ограничения MVP и дальнейшие планы»).

- Нет refresh-токенов и отзыва сессий; access-токен живёт 15 минут.
- Нет подтверждения email, восстановления/смены пароля, rate limiting на вход.
- Лимит 100 МБ задан только атрибутом; в бизнес-логике проверки размера нет.
- Нет пагинации и сортировки в `GET /api/files`.
- Хранение только на локальном диске; нет S3, шифрования, проверки содержимого.
- У `StoredFiles.UserId` нет индекса и внешнего ключа.
- Удаление файла: диск раньше БД → возможна рассинхронизация.
- Миграции не применяются автоматически; HTTPS-редирект в контейнере не работает без reverse proxy.
- Нет CI.

Планы (из README): слой `Application`, refresh-токены, пагинация, S3/MinIO за `ILocalFileStorage`,
индексы/FK и транзакционность, CI с тестами и health-проверками.

---

## 13. Работа с git

- Основная ветка — `main`. Не создавай и не переключай ветки без необходимости.
- Коммиты — короткие, на английском, в повелительном наклонении, в стиле текущей истории:
  `add integration tests`, `feat: add centralized error handling`, `fix: ...`.
- Один коммит — одна логическая задача. Не смешивай рефакторинг и поведение.
- Не коммить секреты, артефакты сборки, `App_Data`, `.idea`.
- **Не выполняй без подтверждения:** `git reset --hard`, `git clean -fd`, force-push,
  `git commit --amend` для уже отправленных коммитов, удаление веток/файлов.
- Перед крупными изменениями проверь `git status` — в рабочем каталоге может быть незакоммиченная
  работа пользователя. Не перезаписывай и не удаляй её.

---

## 14. Правила для AI-агента

### Порядок работы

1. Прочитай `README.md` и этот файл, затем — затрагиваемые файлы. Не угадывай API и имена.
2. Для задач, меняющих больше 2–3 файлов, сначала составь короткий план.
3. Вноси минимальные изменения, сохраняя стиль соседнего кода.
4. После правок выполни сборку и unit-тесты; интеграционные — если доступна БД.
5. В отчёте укажи: что изменено, какие команды запущены и с каким результатом, что не проверено.

### Обязательно спроси перед

- удалением или переименованием файлов, папок, публичных типов;
- изменением контракта API (пути, коды, имена полей), формата `ProblemDetails` или `access_token`;
- добавлением NuGet-пакета или сменой целевого фреймворка;
- миграцией, которая удаляет/меняет столбцы или данные;
- `docker compose down -v`, очисткой томов, `git reset --hard`, force-push;
- изменением настроек безопасности (JWT, CORS, лимиты, авторизация).

### Запрещено

- коммитить секреты и печатать содержимое `.env` / user-secrets;
- отключать или ослаблять тесты, проверки JWT, изоляцию пользователей;
- возвращать наружу `StorageKey`, `UserId`, стектрейсы;
- менять `Migrations/*.Designer.cs` и `AppDbContextModelSnapshot.cs` вручную;
- удалять `public partial class Program { }`;
- использовать рабочую БД для тестов;
- «попутно» переформатировать чужие файлы.

---

## 15. Чек-лист завершённой задачи

- [ ] Изменения соответствуют слоям: контроллер → сервис → репозиторий/хранилище.
- [ ] Новые зависимости оформлены интерфейсами и зарегистрированы в `Program.cs`.
- [ ] Публичные ответы используют DTO; `StorageKey`/`UserId` не утекают.
- [ ] Все операции с файлами фильтруются по `UserId` из claims.
- [ ] Ошибки отдаются как `ProblemDetails`, без внутренних деталей.
- [ ] `CancellationToken` проброшен до конца.
- [ ] Есть/обновлены unit-тесты; при изменении поведения — интеграционные.
- [ ] `dotnet build CloudStorage.sln` проходит без новых предупреждений.
- [ ] Тесты проходят; если интеграционные не запускались — это указано в отчёте.
- [ ] При изменении схемы создана миграция.
- [ ] При изменении поведения/ограничений обновлён `README.md`.
- [ ] В diff нет секретов, артефактов сборки и посторонних правок.
