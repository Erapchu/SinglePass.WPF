[English](../passkeys.md) | **Русский**

# Passkeys (спецификация)

Статус: **не начато**. Документ описывает, как Erapkey хранит passkeys (обнаруживаемые учётные данные WebAuthn) и как сайты до них добираются: через расширение Erapkey для браузера и, в Windows 11, через Erapkey как поставщика passkeys операционной системы.

Планируемый код: `Erapkey.Core/Models/PasskeyData.cs`, `Erapkey.Core/Services/Passkeys/` (аутентификатор, правила rpId, CBOR и COSE), `Erapkey.Core/Services/Browser/` (новые запросы), `Erapkey.Desktop/Services/Passkeys/` (подтверждение, поставщик Windows), `extension/src/webauthn/`. Тесты: `Erapkey.Core.Tests/PasskeyAuthenticatorTests.cs`, `PasskeyMergeTests.cs`, `RpIdTests.cs` и interop-тесты расширения.

## Цели

- Создавать и использовать passkeys на сайтах в Chrome и Edge; закрытые ключи лежат в хранилище (vault) и синхронизируются вместе с ним.
- Хранить passkey вместе с паролем и TOTP того же аккаунта, в одной записи-логине.
- Работать в Windows 10 (через расширение) и показываться в системном окне passkeys Windows 11 (поставщик-плагин), с одной реализацией аутентификатора для обоих путей.
- Никогда не отдавать закрытый ключ: сайты и расширение получают только подписи.

Не входит в первую версию: passkeys для небраузерных приложений в Windows 10, гибридный вход (QR-код, телефон как аутентификатор), расширения `prf`/`hmac-secret`, `largeBlob` и `credBlob`, аттестация кроме `none`, импорт и экспорт (см. [Позже](#позже)).

## Термины

| Термин | Значение |
|---|---|
| RP, rpId | Relying party — сайт. Его ID — домен, например `github.com`. |
| Credential ID | Случайные байты, которыми сайт называет один passkey. |
| User handle | Непрозрачный ID аккаунта на сайте (до 64 байт), возвращается при входе. |
| UP, UV | Присутствие пользователя (он что-то сделал) и проверка пользователя (PIN, биометрия или пароль). |
| Обнаруживаемый (discoverable) | Passkey, который аутентификатор может перечислить для rpId без того, чтобы сайт его назвал. Все passkeys Erapkey обнаруживаемые. |
| Поставщик (provider) | Программа, которая хранит passkey: Erapkey, Windows Hello, телефон, ключ безопасности. |

## Модель данных

Passkey принадлежит **записи-логину**, а не новому типу записи. Пароль, TOTP и passkey одного аккаунта — одна запись, как в других менеджерах паролей; слияние, корзина, группы и резервные копии работают без изменений. Passkey, для которого нет подходящего логина, создаёт новый логин без пароля.

`LoginData` получает список:

```jsonc
"Login": {
  "LoginField": {...}, "PasswordField": {...}, "SiteField": {...}, "Totp": {...} | null, "PasswordHistory": [...],
  "Passkeys": [
    {
      "CredentialId": "<base64url>",        // 16 случайных байт
      "RpId": "github.com",
      "RpName": "GitHub",
      "UserHandle": "<base64url>",
      "UserName": "alice",
      "UserDisplayName": "Alice",
      "Algorithm": -7,                       // COSE: -7 ES256; -8 EdDSA и -257 RS256 позже
      "PrivateKey": "<base64 PKCS#8>",       // null, если Deleted
      "CreationTime": "<UTC>",
      "Deleted": false,
      "ModifiedAt": "<UTC>"
    }
  ]
}
```

- У большинства логинов `Passkeys` нет или он пуст. Допускается больше одного элемента (аккаунт с passkeys для двух rpId или passkey, пересозданный сайтом), хотя обычно интерфейс показывает один на аккаунт.
- `PrivateKey` — единственный секрет. Он никогда не покидает `Erapkey.exe`: ни в расширение, ни в Windows, ни в буфер обмена.
- Счётчик подписей всегда **0** и не хранится. Синхронизируемые passkeys не могут держать счётчик согласованным между устройствами, спецификация WebAuthn разрешает 0, а счётчик, меняющийся при каждом входе, превращал бы каждое использование в изменение хранилища для синхронизации.
- Удалённый passkey остаётся надгробием (tombstone): `Deleted: true`, `PrivateKey: null`, остальные поля сохраняются, чтобы интерфейс мог показать, что удалено. См. [Слияние](#слияние).
- `CredentialId`, `UserHandle` и `RpId` в редакторе только для чтения. Пользователь может менять `UserName` и `UserDisplayName` (сайт их не видит) и удалить passkey.

### Версия хранилища

`PasswordVault.CurrentVersion` становится **7**: старая сборка потеряла бы `Passkeys` при следующей записи. В таблицу в [vault.md](vault.md#содержимое) добавляется строка «7 — добавляет passkeys: `Login.Passkeys`», а `LoginData.Clone` копирует список.

### Слияние

Сейчас запись выигрывает или проигрывает целиком по `LastModifiedTime` ([vault.md](vault.md#слияние-merge)). Для passkeys этого мало: passkey, созданный на одном устройстве, и пароль, изменённый на другом до синхронизации, привели бы к потере passkey, и пользователь мог бы больше не войти на сайт. Поэтому, как и история паролей, passkeys обеих версий объединяются:

1. Выигравшая запись выбирается как сейчас.
2. Её `Passkeys` становятся объединением обеих версий по `CredentialId`.
3. Для ID, который есть в обеих, выигрывает элемент с более поздним `ModifiedAt`; при равном `ModifiedAt` надгробие выигрывает у живого элемента.
4. Надгробия старше `PasswordVault.TrashRetention` отбрасываются при записи.

Passkey, перенесённый в другую запись (см. [Интерфейс](#интерфейс-erapkey)), — это надгробие в старой записи и новый элемент с тем же `CredentialId` в новой; при поиске по credential ID учитывается только живой элемент.

## Аутентификатор

`Erapkey.Core/Services/Passkeys/PasskeyAuthenticator.cs` реализует две операции аутентификатора CTAP2 независимо от канала. Это чистый .NET (без API Windows), поэтому позже он сможет работать и в macOS.

### Создание (`authenticatorMakeCredential`)

Вход: `rpId`, `rpName`, пользователь (handle, имя, отображаемое имя), `clientDataHash`, список допустимых алгоритмов, список исключений, была ли проверка UV.

1. Если в хранилище есть живой passkey для этого `rpId`, чей `CredentialId` есть в списке исключений: после подтверждения пользователем — ошибка `exists` (сайт получит `InvalidStateError`).
2. Если ни один допустимый алгоритм не поддерживается (в первой версии — ES256): `not_supported`.
3. Сгенерировать ключ P-256 (`ECDsa.Create(ECCurve.NamedCurves.nistP256)`) и 16 случайных байт для credential ID.
4. Если есть живой passkey с тем же `rpId` и `UserHandle`, он заменяется (сайт пересоздал его); иначе passkey попадает в логин, выбранный в окне подтверждения, или в новый логин (`NameField` = `rpName`, `SiteField` = `https://<rpId>`, `LoginField` = имя пользователя).
5. Вернуть объект аттестации в формате `none`:

```
attestationObject = CBOR { "fmt": "none", "attStmt": {}, "authData": authenticatorData }

authenticatorData =
  SHA-256(rpId)                  32
  flags                           1   UP 0x01 | UV 0x04 (только если была) | BE 0x08 | BS 0x10 | AT 0x40
  signCount = 0                   4   big-endian
  AAGUID                         16   Erapkey, см. ниже
  длина credentialId              2   big-endian
  credentialId                   16
  credentialPublicKey             COSE_Key, канонический CBOR CTAP2:
                                      { 1: 2, 3: -7, -1: 1, -2: x (32 байта), -3: y (32 байта) }
```

BE и BS (может быть в резервной копии, находится в резервной копии) установлены всегда: passkey синхронизируется вместе с хранилищем.

### Вход (`authenticatorGetAssertion`)

Вход: `rpId`, `clientDataHash`, список разрешённых (может быть пустым), выбранный passkey, была ли проверка UV.

1. Кандидаты: живые passkeys, у которых `RpId` в точности равен запрошенному `rpId` (сайт просит свой rpId; проверка origin делается раньше, см. [Правила rpId](#правила-rpid)). При непустом списке разрешённых — только эти ID.
2. Пользователь выбирает один в окне подтверждения (или уже выбрал во встроенном списке); кандидатов нет — `not_found`.
3. `authenticatorData` = `SHA-256(rpId)`, флаги `UP | UV? | BE | BS`, счётчик 0.
4. `signature` = ECDSA P-256 SHA-256 от `authenticatorData || clientDataHash`, в DER (`DSASignatureFormat.Rfc3279DerSequence`).
5. Вернуть credential ID, `authenticatorData`, `signature` и user handle.

### AAGUID

У Erapkey один постоянный случайный AAGUID, константа в `PasskeyAuthenticator`. Сайты вроде аккаунтов Google и Microsoft показывают имя и значок поставщика по AAGUID из общего списка [passkey-authenticator-aaguids](https://github.com/passkeydeveloper/passkey-authenticator-aaguids); Erapkey добавляется туда со своим значком после выпуска. Поставщик Windows регистрирует тот же AAGUID в своём `authenticatorGetInfo`.

### CBOR

`System.Formats.Cbor` в каноническом режиме CTAP2 (`CborConformanceMode.Ctap2Canonical`). Путь через браузер требует только кодирования (ключ COSE, объект аттестации); путь Windows получает функции разбора от самой Windows (см. ниже).

### Правила rpId

`Erapkey.Core/Services/Passkeys/RpId.cs`, для пути через браузер (Windows проверяет origin сама):

1. Origin должен быть `https` или `http://localhost` (а также `*.localhost`) для разработки.
2. По умолчанию rpId — хост origin. Переданный rpId должен совпадать с хостом или быть его родительским доменом (`login.example.com` может использовать `example.com`); сравнение по меткам после перевода IDN в punycode и нижнего регистра, как в `WebsiteMatcher`.
3. rpId не может быть публичным суффиксом (`co.uk`, `github.io`): решает `PublicSuffixList`.
4. IP-адреса допускаются только как точный хост.

Эти правила совпадают с проверкой «registrable domain suffix» из WebAuthn, которую делают браузеры.

### Тесты

Результат аутентификатора проверяется настоящей библиотекой relying party WebAuthn ([Fido2NetLib](https://github.com/passwordless-lib/fido2-net-lib)), на которую ссылается только `Erapkey.Core.Tests`: регистрация через `MakeCredential`, затем проверка `GetAssertion` для того же passkey. Плюс фиксированные векторы для ключа COSE и `authenticatorData`, правила rpId и правила слияния.

## Согласие и проверка пользователя

Каждое создание и каждый вход требуют действия пользователя в Erapkey или во встроенном списке; тихой подписи нет. Флаг UV ставится, только если пользователь действительно прошёл проверку:

| Канал | UV |
|---|---|
| Расширение браузера | Жест Windows Hello через существующий `NgcKey` (подпись случайного вызова ключом разблокировки), если настроена разблокировка через Windows Hello. Иначе мастер-пароль в окне подтверждения, если сайт требует UV (`userVerification: "required"`). |
| Поставщик Windows | `WebAuthNPluginPerformUserVerification`: Windows показывает своё окно Windows Hello с именем Erapkey. |

Заблокированное хранилище сначала показывает окно разблокировки, как запрос SSH-агента (`SshAgentKeyStore`), и ждёт до минуты. Разблокировка считается проверкой UV для этого запроса.

## Канал 1: расширение браузера

Работает в Windows 10 и 11, в любом браузере на основе Chromium с расширением, позже в Firefox. Опирается на существующие сопряжение, сеанс и канал (pipe) ([browser-extension.md](browser-extension.md)).

### Перехват

```
страница ── navigator.credentials.create/get
  │  webauthn-main.ts   MAIN world, document_start, все фреймы: подменяет два метода
  │  window.postMessage
  ▼
webauthn-relay.ts       content script в изолированном мире
  │  chrome.runtime.sendMessage
  ▼
background              берёт origin из sender.origin, никогда со страницы
  │  сеанс (существующий), запросы ниже
  ▼
Erapkey.exe             проверка rpId, окно подтверждения, PasskeyAuthenticator
```

- Скрипт в MAIN world подменяет `navigator.credentials.create` и `get` только для запросов `publicKey`, а также `PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable`, `isConditionalMediationAvailable` и `getClientCapabilities` (все возвращают true, пока Erapkey доступен).
- Origin берётся из `sender.origin` сообщения relay. Страница, которая шлёт relay поддельные сообщения, может запросить только свой собственный origin.
- Background не собирает ничего секретного: Erapkey собирает `clientDataJSON` из origin и challenge, хеширует, подписывает и возвращает JSON, чтобы страница получила ровно те байты, что были подписаны. `clientDataJSON` = `{"type":"webauthn.create"|"webauthn.get","challenge":"<base64url>","origin":"<origin>","crossOrigin":false}`, именно в этом порядке.
- Ответ превращается в объект с прототипом `PublicKeyCredential` и `AuthenticatorAttestationResponse` / `AuthenticatorAssertionResponse`, с `getTransports()` (`["internal"]`), `getAuthenticatorData()`, `getPublicKey()` (SPKI), `getPublicKeyAlgorithm()`, `getClientExtensionResults()` (`credProps: { rk: true }`, если запрошено) и `toJSON()`, чтобы работали библиотеки сайтов, которые проверяют типы или вызывают эти методы.
- Ошибки превращаются в `DOMException`: `cancelled` → `NotAllowedError`, `exists` → `InvalidStateError`, `not_supported` → `NotSupportedError`.
- **Передача браузеру**: исходные методы сохраняются. Они вызываются, когда Erapkey не запущен, браузер не сопряжён, страница — фрейм с другим origin (первая версия), запрос с `mediation: "conditional"` и пользователь ничего не выбрал, или пользователь нажал *Другое устройство или ключ безопасности* в окне подтверждения. Тогда страница видит собственное окно браузера, как без расширения.
- `signal` (`AbortSignal`) запроса отменяет его: background отправляет `cancel { requestId }`, и Erapkey закрывает своё окно.

### Условный запрос (автозаполнение)

Когда страница вызывает `get` с `mediation: "conditional"`, расширение запрашивает `getPasskeys` для origin и добавляет passkeys во встроенное меню поля логина (content script из [browser-extension.md](browser-extension.md#расширение), пока не сделан). Выбор отправляет `passkeyGet` с этим credential ID; клик — это согласие, как при подстановке логина. Пока встроенного меню нет, условные запросы уходят браузеру.

### Запросы

Добавляются в существующий сеанс. Версия протокола остаётся `1`; `status` и `hello` получают `features: ["passkeys"]`, чтобы новое расширение понимало, поддерживает ли запущенный Erapkey passkeys, и отдавало запрос браузеру, если нет. Версия проверяется на равенство, поэтому её повышение сломало бы все старые расширения.

| Запрос | Ответ | Примечания |
|---|---|---|
| `passkeyCreate { requestId, origin, rp, user, challenge, pubKeyCredParams, excludeCredentials, authenticatorSelection, extensions }` | `{ credentialId, clientDataJSON, attestationObject, authenticatorData, publicKey, publicKeyAlgorithm }` | Все двоичные значения в base64url. Показывает окно подтверждения. |
| `passkeyGet { requestId, origin, rpId, challenge, allowCredentials, userVerification, credentialId? }` | `{ credentialId, clientDataJSON, authenticatorData, signature, userHandle }` | `credentialId` передаётся, когда пользователь уже выбрал во встроенном списке. |
| `getPasskeys { origin, rpId }` | `passkeys: [{ credentialId, entryId, userName, userDisplayName }]` | Для встроенного списка. Без ключей. Только после проверки rpId. |
| `cancel { requestId }` | `{}` | Закрывает окно подтверждения этого запроса. |

Новые ошибки: `cancelled`, `exists`, `not_supported`, `fallback` (пользователь выбрал другое устройство: расширение вызывает браузер).

`IBrowserBackend` получает `ConfirmPasskeyCreateAsync` и `ConfirmPasskeyGetAsync` (окно) и способ сохранить новый passkey в хранилище.

### Манифест

Перехвату нужны content scripts на каждой странице:

```json
"content_scripts": [
  { "matches": ["https://*/*", "http://localhost/*"], "js": ["webauthn-main.js"], "run_at": "document_start", "all_frames": true, "world": "MAIN" },
  { "matches": ["https://*/*", "http://localhost/*"], "js": ["webauthn-relay.js"], "run_at": "document_start", "all_frames": true }
]
```

Это гораздо более широкое разрешение, чем нынешний `activeTab`; пользователь видит его при установке и обновлении, его же смотрит проверка в магазине. Скрипты не содержат данных хранилища и только пересылают вызовы WebAuthn.

### Окно подтверждения

`PasskeyConfirmWindow` в `Erapkey.Desktop`, поверх других окон, как `SshAgentConfirmWindow`:

- **Создание**: *Сохранить passkey для github.com?* Имя пользователя от сайта, браузер и куда сохранить: логины, чей сайт подходит под rpId (лучшие первыми), или *Новый логин*. Кнопки: **Сохранить**, **Другое устройство**, **Отмена**.
- **Вход**: *Вход на github.com* со списком подходящих passkeys (имя пользователя, имя записи). Кнопки: **Войти**, **Другое устройство**, **Отмена**.
- Когда нужна UV, **Сохранить** / **Войти** сначала просят Windows Hello или мастер-пароль.

## Канал 2: поставщик passkeys Windows

Windows 11 позволяет сторонней программе быть поставщиком passkeys через API плагина-аутентификатора (`webauthnplugin.h`, `pluginauthenticator.idl`). Chrome и Edge в Windows вызывают WebAuthn API Windows, поэтому с включённым Erapkey они показывают его в системном окне passkeys и в автозаполнении без расширения.

### Требования

- Windows 11 24H2 (сборка 26100) или 25H2 (26200), ревизия 6725 или новее (обновление ноября 2025). В Windows 10 недоступно.
- Windows SDK 10.0.26100.7175 или новее — ради заголовков, которые переносятся в C#.
- **Package identity.** API рассчитан на упакованные приложения: COM-сервер объявляется в манифесте пакета, а вызов из неупакованного процесса, по сообщениям, завершается ошибкой `0x80073D54` (`APPMODEL_ERROR_NO_PACKAGE`). Erapkey ставится через Velopack без пакета, поэтому ему нужен **sparse package** (пакет с внешним расположением), см. ниже.
- **Подпись кода.** Sparse package должен быть подписан сертификатом, которому доверяет компьютер. Самоподписанный сертификат потребовал бы прав администратора для установки, поэтому релизу нужен настоящий сертификат подписи кода (или сервис подписи), а CI подписывает `Erapkey.exe` и пакет. Заодно это помогает с SmartScreen для установщика.

Первый шаг — прототип в Windows 11, который регистрирует sparse package и вызывает `WebAuthNPluginAddAuthenticator`; если sparse package окажется недостаточно, поставщику нужна полная сборка MSIX, которую Velopack не делает, и этот канал откладывается.

### Sparse package

`Erapkey.Desktop/Package/AppxManifest.xml`, CI упаковывает его в `Erapkey.Identity.msix` (только манифест и логотипы, без двоичных файлов) и подписывает:

- `Identity` с издателем из сертификата подписи; `uap10:AllowExternalContent`; одно `Application` с `Executable="Erapkey.exe"`, `uap10:TrustLevel="mediumIL"`, `uap10:RuntimeBehavior="win32App"`.
- `com:Extension Category="windows.comServer"` с `com:ExeServer Executable="Erapkey.exe" Arguments="-Embedding"` и `com:Class Id="<CLSID плагина Erapkey>"`.
- Capability `runFullTrust`.

`Erapkey.exe` получает манифест приложения с `<msix publisher="…" packageName="…" applicationId="…"/>`, чтобы `Erapkey.exe`, запущенный из папки установки, работал с package identity.

Регистрация — в хуках установки и обновления Velopack (`Program.Main`), только для установленной сборки (не portable и не Debug):

1. `PackageManager.AddPackageByUriAsync(msix, new AddPackageOptions { ExternalLocationUri = <папка current\ Velopack> })`. `current\` сохраняет путь между обновлениями.
2. Снятие регистрации в хуке удаления: `WebAuthNPluginRemoveAuthenticator`, затем `PackageManager.RemovePackageAsync`.

`Erapkey.Desktop` переходит на целевую платформу с Windows SDK (`net11.0-windows10.0.26100.0`) ради `PackageManager` или вызывает его через небольшой WinRT interop.

### Регистрация поставщика

При запуске, когда включён **Поставщик passkeys Windows** и у процесса есть package identity:

1. `WebAuthNPluginGetAuthenticatorState(clsid)`; если не зарегистрирован — `WebAuthNPluginAddAuthenticator2` с:
   - именем `Erapkey`, CLSID, светлым и тёмным логотипами SVG (base64);
   - `authenticatorGetInfo` в CBOR: версии `FIDO_2_0`, `FIDO_2_1`; AAGUID; опции `rk: true`, `up: true`, `uv: true`; алгоритм ES256;
   - без списка поддерживаемых rpId (все сайты).
2. `WebAuthNPluginGetOperationSigningPublicKey` даёт ключ, которым Windows подписывает запросы. Он читается заново при каждом запуске и не хранится.
3. Пока пользователь не включит Erapkey в **Параметры Windows → Учётные записи → Ключи доступа → Дополнительные параметры**, состояние — *выключен*. Настройки Erapkey показывают состояние и кнопку, открывающую эту страницу (URI `ms-settings:`, уточнить в Windows 11).
4. `WebAuthNPluginRegisterStatusChangeCallback` обновляет состояние в настройках.

Выключение настройки вызывает `WebAuthNPluginRemoveAuthenticator` и очищает кэш учётных данных.

### COM-сервер

COM-сервер — сам `Erapkey.exe`: хранилище остаётся в одном процессе, второй посредник не нужен.

- При запуске (если поставщик включён) Erapkey вызывает `CoRegisterClassObject(clsid, factory, CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE)`. Если Erapkey не запущен, Windows запускает `Erapkey.exe -Embedding`: `Program.Main` считает это запуском в трей без главного окна, затем регистрирует объект класса. Мьютекс единственного экземпляра и межпроцессное окно работают как сейчас.
- Класс реализует `IPluginAuthenticator` (из `pluginauthenticator.idl`): создание, вход, отмена операции и состояние блокировки. Точный список методов и структуры переносятся из заголовков SDK; interop через `[GeneratedComInterface]` и `ComWrappers`, как допускают Native AOT и trimming.
- Вызовы приходят в поток MTA и передаются диспетчеру WPF для окон, владельцем которых становится дескриптор окна из запроса.
- **Подпись каждого запроса проверяется** ключом подписи операций прежде всего остального; запрос, не прошедший проверку, отклоняется и пишется в журнал. COM-класс может активировать любой процесс пользователя, и только эта проверка не даёт им просить Erapkey о подписи.
- Запрос — CBOR CTAP2. `WebAuthNDecodeMakeCredentialRequest` / `WebAuthNDecodeGetAssertionRequest` разбирают его; `PasskeyAuthenticator` выполняет работу; `WebAuthNEncodeMakeCredentialResponse` / `WebAuthNEncodeGetAssertionResponse` кодируют ответ. Windows сама сверяет origin с rpId и передаёт только `clientDataHash`.
- Состояние блокировки: *заблокирован*, пока заблокировано хранилище. Запрос при заблокированном хранилище показывает окно разблокировки (как запрос из браузера).
- UV через `WebAuthNPluginPerformUserVerification` с именем пользователя в подсказке; согласие и выбор passkey — в `PasskeyConfirmWindow`, в сокращённом виде, если Windows уже показала свой список.

### Кэш учётных данных

Windows показывает passkeys поставщика в своём окне и в автозаполнении браузера из кэша метаданных, который заполняет поставщик: `WebAuthNPluginAuthenticatorAddCredentials`, `RemoveCredentials`, `RemoveAllCredentials`. Каждый элемент содержит credential ID, rpId, имя сайта, user handle, имя пользователя и отображаемое имя — без ключа.

- Erapkey заменяет кэш (удалить всё, добавить все живые passkeys) после разблокировки и после каждого изменения хранилища, затрагивающего passkeys, включая синхронизацию.
- Кэш остаётся, пока хранилище заблокировано, чтобы Windows могла показать passkeys и запустить Erapkey, который затем попросит разблокировку. Поэтому имена сайтов и аккаунтов видны Windows, пока Erapkey заблокирован; настройка **Показывать passkeys при блокировке** (включена по умолчанию), если её выключить, очищает кэш при блокировке.
- Выключение поставщика или удаление программы очищает кэш.

### Оба канала сразу

При включённом поставщике Windows браузер и так показывает Erapkey через Windows, а расширение добавило бы второе окно. Поэтому `status` сообщает `passkeys: "extension" | "windows" | "off"`, и расширение перехватывает запросы только при `extension`. Erapkey отвечает `windows`, когда поставщик зарегистрирован и включён в Windows; пользователь может принудительно выбрать расширение в настройках (например, для браузера, который не использует API Windows).

## Интерфейс Erapkey

- **Просмотр логина**: строка *Passkey* для каждого живого passkey: rpId, имя пользователя, дата создания и меню с **Удалить** и **Перенести в другой логин**. Копирования нет.
- **Редактор логина**: passkeys показаны только для чтения с **Удалить**; имя пользователя и отображаемое имя можно менять.
- Список: значок passkey у логинов, где он есть; поиск ищет по `RpId` и `UserName`.
- **Новый элемент** не создаёт passkeys: они появляются только от сайта.
- Удаление логина с passkey предупреждает, что сайт может больше не пустить пользователя по нему.
- **Настройки → Безопасность → Passkeys**: *Сохранять passkeys из браузера* (канал расширения), *Поставщик passkeys Windows* с состоянием (не поддерживается этой Windows / не зарегистрирован / выключен в Windows → **Открыть параметры Windows** / включён), *Показывать passkeys при блокировке*.

## Безопасность

- Закрытые ключи остаются в хранилище и в `Erapkey.exe`; подпись выполняется там.
- Канал браузера доверяет origin из `sender.origin` и проверяет rpId в Erapkey; страница не влияет ни на то, ни на другое. Скомпрометированное расширение может просить подписи только для сайтов, которые посещает пользователь, и после подтверждения в Erapkey — так же, как с паролями.
- Канал Windows доверяет Windows проверку origin и проверяет подпись каждого запроса.
- Тихих операций нет: каждое создание и вход — действие пользователя. Passkeys не используются, пока хранилище заблокировано.
- UV никогда не заявляется без проверки.
- Passkeys защищены так же, как хранилище: любой, у кого есть мастер-пароль (и файл хранилища), может войти. Это цена синхронизируемых passkeys, одинаковая для всех менеджеров паролей.

## Разработка

- Аутентификатор и канал браузера проверяются в любой Windows на [webauthn.io](https://webauthn.io) с отладочным экземпляром (`Erapkey.Dev`, `com.erapkey.host.dev`).
- Каналу Windows нужна Windows 11 24H2 или новее (достаточно виртуальной машины) и тестовый сертификат подписи, которому доверяет эта машина. Образец для регистрации и COM-сервера — [Passkey Manager sample](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/passkeymanager/) от Microsoft. Отладочные сборки используют свои CLSID и имя пакета, чтобы не заменять установленного поставщика.

## Порядок реализации

1. **Core**: `PasskeyData`, версия хранилища 7, слияние passkeys, `PasskeyAuthenticator`, правила rpId, тесты с Fido2NetLib.
2. **Интерфейс Desktop**: passkeys в просмотре и редакторе логина (просмотр, удаление, перенос), поиск, раздел настроек.
3. **Канал браузера**: запросы и `features`, `PasskeyConfirmWindow`, UV через Windows Hello, скрипты MAIN world и relay в расширении, передача браузеру. Затем условный запрос со встроенным меню.
4. **Подпись кода** в CI (сертификат или сервис подписи).
5. **Прототип канала Windows** в Windows 11: sparse package и `WebAuthNPluginAddAuthenticator`. Решение, делать ли остальное.
6. **Канал Windows**: COM-сервер, проверка подписи запросов, кэш учётных данных, настройки, переключение между каналами.
7. Обновить README и [browser-extension.md](browser-extension.md).

## Позже

- `prf` / `hmac-secret`: случайный секрет на каждый passkey, чтобы работали сайты, которые шифруют данные с помощью passkeys.
- EdDSA (`-8`) через BouncyCastle, RS256 (`-257`).
- Импорт и экспорт в формате FIDO Credential Exchange Format (CXF); хранимые поля выше соответствуют его элементу passkey.
- Firefox, вместе с шагом 7 расширения для браузера.
- Поставщики для macOS и iOS (AuthenticationServices) на том же `PasskeyAuthenticator`.
- Гибридный вход (через телефон), если Windows откроет его для плагинов.

## Открытые вопросы

- Достаточно ли sparse package для `WebAuthNPluginAddAuthenticator` и нужна ли кроме `runFullTrust` какая-то capability? Ответит шаг 5.
- Какой вариант подписи кода: OV-сертификат на аппаратном токене или облачный сервис подписи, и что из этого доступно частному издателю.
- Примут ли Chrome Web Store и Edge Add-ons более широкое разрешение для content scripts без долгой дополнительной проверки.
