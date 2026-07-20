---
name: MyRFS.Admin
description: Консоль управления файловым хранилищем MyRFS — файлы, хранилища, мониторинг объёма, целостность, здоровье сервиса
colors:
  ember-orange: "#FF6B2C"
  ember-orange-light: "#FF8C42"
  bg-dark: "#2D2D35"
  panel-dark: "#363640"
  inset-dark: "#1E1E26"
  surface-dark: "#3A3A45"
  border-dark: "rgba(255, 255, 255, 0.08)"
  border-strong-dark: "rgba(255, 255, 255, 0.14)"
  bg-light: "#F0F0F5"
  panel-light: "#FFFFFF"
  inset-light: "#E4E4EC"
  border-light: "rgba(26, 26, 46, 0.10)"
  border-strong-light: "rgba(26, 26, 46, 0.18)"
  text-primary-dark: "#FFFFFF"
  text-primary-light: "#1A1A2E"
  text-secondary: "#8A8A9A"
  text-muted: "#7D7D8A"
  sem-success: "#4ADE80"
  sem-warning: "#FFB02E"
  sem-danger: "#FF4D4D"
  sem-info: "#4DA6FF"
  sem-muted: "#6B7280"
typography:
  title:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 600
    lineHeight: 1.3
  headline:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
    fontSize: "1.25rem"
    fontWeight: 500
    lineHeight: 1.4
  body:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.5
  label:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 500
    lineHeight: 1.4
    letterSpacing: "0.02em"
  mono:
    fontFamily: "'JetBrains Mono', 'Fira Code', 'Cascadia Code', monospace"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.6
rounded:
  sm: "4px"
  md: "8px"
  lg: "16px"
  xl: "24px"
  full: "9999px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  2xl: "48px"
components:
  button-default:
    backgroundColor: "{colors.panel-dark}"
    textColor: "{colors.text-primary-dark}"
    borderColor: "{colors.border-dark}"
    rounded: "{rounded.md}"
    padding: "8px 16px"
  button-accent:
    backgroundColor: "{colors.ember-orange}"
    textColor: "#FFFFFF"
    rounded: "{rounded.md}"
    padding: "8px 16px"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.text-secondary}"
    rounded: "{rounded.md}"
    padding: "8px 16px"
  button-ghost-hover:
    backgroundColor: "rgba(255, 107, 44, 0.12)"
    textColor: "{colors.text-primary-dark}"
    rounded: "{rounded.md}"
    padding: "8px 16px"
  input-default:
    backgroundColor: "{colors.inset-dark}"
    textColor: "{colors.text-primary-dark}"
    borderColor: "{colors.border-dark}"
    rounded: "{rounded.md}"
    padding: "0 16px"
    height: "36px"
  card:
    backgroundColor: "{colors.panel-dark}"
    borderColor: "{colors.border-dark}"
    rounded: "{rounded.lg}"
    padding: "24px"
---

# Design System: MyRFS.Admin

## 1. Overview

**Creative North Star: "The Precision Instrument"**

MyRFS.Admin — диспетчерский пульт файлового хранилища, а не витрина. Как осциллограф или спектральный анализатор, система существует ради данных, которые через неё проходят: файлы, их целостность, хранилища и их заполнение. Визуальный язык сдержан намеренно: плоские тональные поверхности, разделённые тонкими hairline-границами, единственный хроматический акцент — Ember Orange — зарезервирован строго для действий и интерактивных элементов. Всё остальное — нейтрально, чтобы статус файла читался мгновенно.

Система поддерживает два полноценных визуальных режима, отражающих реальные рабочие контексты: тёмная тема для дежурного наблюдения за потоком загрузок; светлая — для разбора инцидентов и построения отчётов. Ни одна из тем не является «по умолчанию» — пользователь выбирает контекст. Оба режима сохраняют полную семантику и иерархию.

Система отвергает четыре паттерна: шаблонные Bootstrap/Material admin-панели с их серо-синей безликостью и нулевым характером; перегруженные дашборды, где каждый пиксель занят виджетами без иерархии; retro-терминальную эстетику с зелёным текстом на чёрном; и neumorphism в любом виде — никаких парных теней, «выдавленных» и «вдавленных» поверхностей. MyRFS.Admin — точный инструмент, не театр данных.

**Key Characteristics:**
- Плоская elevation: глубина создаётся тональными уровнями фона и hairline-границами 1px, тень — только у настоящих overlay (dropdown, диалог, toast)
- Единственный акцент: Ember Orange используется строго для интерактивных элементов
- Семантические цвета: статус файла (Stored, Uploading, Corrupted, Disabled) читается мгновенно по цвету
- Моноширинная типографика для технических значений: путь, размер, CRC32C, GUID, временная метка
- Две полноценные темы с идентичной семантикой

## 2. Colors: The Ember Palette

Монохромные нейтральные поверхности плюс один тёплый акцент и строгая семантическая палитра для статусов файлов. Тёплый оттенок нейтралей (лёгкий пурпурный подтон) перекликается с Ember Orange, не конкурируя с ним.

### Primary
- **Ember Orange** (`#FF6B2C`): Единственный акцент во всей системе. CTA-кнопки, активные состояния навигации, hover ссылок, focus-кольца, кнопка «Проверить целостность». Никогда — фон, декорация или информационный сигнал статуса.
- **Ember Orange Light** (`#FF8C42`): Hover-состояние акцентных элементов. Конечная точка градиента в accent-кнопках (`135deg, #FF6B2C → #FF8C42`).

### Neutral (тёмная тема)
- **Deep Slate** (`#2D2D35`): Основной фон приложения. Базовый тональный уровень, от которого отсчитываются остальные.
- **Elevated Slate** (`#363640`): Панели, карточки, сайдбар. Уровень выше фона (+5 lightness), отделяется hairline-границей.
- **Mid Slate** (`#3A3A45`): Промежуточные поверхности, hover-подложки строк таблицы.
- **Sunken Void** (`#1E1E26`): Поля ввода, вложенные секции, контейнеры метаданных и путей. Самый тёмный тональный уровень.
- **Hairline** (`rgba(255,255,255,0.08)` / strong `rgba(255,255,255,0.14)`): Границы поверхностей, разделители строк, контуры карточек и контролов.

### Neutral (светлая тема)
- **Cool Mist** (`#F0F0F5`): Основной фон в светлой теме. Лёгкий голубоватый подтон.
- **Pure Panel** (`#FFFFFF`): Карточки и основные панели.
- **Recessed** (`#E4E4EC`): Поля ввода и вложенные секции.
- **Hairline** (`rgba(26,26,46,0.10)` / strong `rgba(26,26,46,0.18)`): Границы и разделители в светлой теме.

### Text
- **Stark White** (`#FFFFFF`): Основной текст в тёмной теме.
- **Deep Navy** (`#1A1A2E`): Основной текст в светлой теме.
- **Steel Mist** (`#8A8A9A`): Вторичный текст — метаданные, временные метки, подписи.
- **Faded Ash** (`#7D7D8A`): Приглушённый текст — плейсхолдеры, отключённые состояния.

### Semantic — Status (роли, единый источник истины)
Пять семантических ролей. Каждая роль обслуживает три контекста одним цветом: **статус файла/операции** (основной), **уровень лога** (для просмотра логов сервиса) и **здоровье сервиса/хранилища**. Это держит палитру компактной и предсказуемой.

| Роль | Hex (dark / light) | Файл / операция | Уровень лога | Здоровье |
| --- | --- | --- | --- | --- |
| **sem-success** | `#4ADE80` / `#16A34A` | Stored / Verified (CRC32C совпал) | — | Online / Healthy |
| **sem-warning** | `#FFB02E` / `#D97706` | Хранилище близко к заполнению | WARN | Degraded |
| **sem-danger** | `#FF4D4D` / `#DC2626` | Corrupted (`DATA_LOSS`) / блоб отсутствует | ERROR / FATAL | Offline / Down |
| **sem-info** | `#4DA6FF` / `#2563EB` | Uploading / Receiving (стрим идёт) | INFO | — |
| **sem-muted** | `#6B7280` / `#6B7280` | Storage disabled / архивный | DEBUG / TRACE | Disabled |

### Named Rules
**The One Voice Rule.** Ember Orange — единственный хроматический *брендовый* цвет на любом экране. Его редкость — это его смысл. Любое использование оранжевого сигнализирует: «здесь можно действовать». Если Orange появляется на 10% площади экрана — это уже слишком много.

**The Semantic Immutability Rule.** Пять семантических ролей (success, warning, danger, info, muted) принадлежат исключительно статусам файлов, уровням логов и здоровью сервисов. Нельзя использовать эти цвета для брендинга, декора или произвольных состояний. Статус файла никогда не передаётся одним только цветом — всегда с текстовой меткой.

## 3. Typography

**Body/UI Font:** Inter (с фолбэком: -apple-system, BlinkMacSystemFont, Segoe UI, Roboto, sans-serif)
**Code/Data Font:** JetBrains Mono (с фолбэком: Fira Code, Cascadia Code, monospace)

**Character:** Inter — нейтральный и читаемый при высокой плотности данных, идеален для таблиц и форм. JetBrains Mono — для технических значений, где моноширинность критична для точного чтения путей, размеров и контрольных сумм.

### Hierarchy
- **Title** (semibold 600, 1.5rem, line-height 1.3): Заголовки страниц и крупных секций.
- **Headline** (medium 500, 1.25rem, line-height 1.4): Подзаголовки разделов, названия карточек метрик.
- **Body** (regular 400, 0.875rem, line-height 1.5): Основной текст UI, строки таблицы файлов. В читаемых текстовых блоках — максимум 65-75ch.
- **Label** (medium 500, 0.8125rem, letter-spacing 0.02em): Метки полей, заголовки колонок. Иногда uppercase с letter-spacing для категорий.
- **Small Label** (medium 500, 0.75rem, letter-spacing 0.02em): Бейджи статусов, теги типов файлов, подсказки.
- **Mono** (regular 400, 0.875rem, line-height 1.6): Относительный путь, размер в байтах, CRC32C, file GUID, регион, временная метка с миллисекундами. Всегда JetBrains Mono.

### Named Rules
**The Mono-for-Data Rule.** Любое значение, которое читается посимвольно — относительный путь файла, размер, CRC32C, file/trace GUID, временная метка с миллисекундами — рендерится JetBrains Mono. Никогда не смешивать Inter и JetBrains Mono для одного токена данных в одной строке без явного визуального разделения.

## 4. Elevation

Система использует **плоскую elevation**: глубина создаётся тональными уровнями фона и hairline-границами, а не тенями. Neumorphism (парные светло-тёмные тени, «выдавленные» и «вдавленные» поверхности, inset-тени как материал) запрещён полностью.

Три тональных уровня образуют иерархию поверхности:
- **Base**: фон приложения (`bg-dark` / `bg-light`).
- **Panel**: панели, карточки, сайдбар (`panel-dark` / `panel-light`) — отделяются hairline-границей 1px.
- **Well**: поля ввода, контейнеры данных, вложенные секции (`inset-dark` / `inset-light`) — темнее панели, с hairline-границей.

Тень существует только у элементов, которые действительно парят над контентом:
- **overlay** (`0 8px 24px rgba(0,0,0,0.35)` dark / `0 8px 24px rgba(26,26,46,0.12)` light): диалоги, dropdown-меню, date-picker.
- **overlay-sm** (`0 4px 12px rgba(0,0,0,0.25)` dark / `0 4px 12px rgba(26,26,46,0.10)` light): tooltip, popover, toast.
- **accent-glow** (`0 2px 8px rgba(255,107,44,0.3)`): свечение accent-кнопки; hover усиливает до `0 4px 12px rgba(255,107,44,0.4)`.

### Named Rules
**The Hairline Rule.** Разделение соседних поверхностей — только тональным контрастом фона и границей 1px (`border-dark` / `border-light`). Статичные поверхности (карточки, панели, поля, кнопки в покое) не имеют box-shadow вообще.

**The Overlay-Only Shadow Rule.** Box-shadow допустим только у элементов, временно перекрывающих контент (диалог, dropdown, tooltip, toast), и у accent-glow. Любая другая тень — включая одиночные декоративные и любые inset-тени — нарушение системы.

## 5. Components

### Buttons
- **Shape:** Мягко скруглённые — 8px (md/lg), 4px (sm).
- **Default:** `panel-dark` фон, граница 1px `border-dark`, основной цвет текста. Hover — фон `surface-dark`, граница `border-strong-dark`; active — фон `bg-dark`. Для вторичных действий.
- **Accent:** Градиент `135deg, #FF6B2C → #FF8C42`, белый текст, свечение accent-glow. Hover усиливает свечение, active — `brightness(0.95)`. Только для главных CTA («Проверить целостность», «Скачать файл»).
- **Ghost:** Прозрачный фон, steel-mist текст. Hover — accent-bg подложка (orange 8-12%), текст становится основным; active — подложка 16%. Для вторичных действий рядом с accent.
- **Danger:** Accent-стиль, но Alert Red (`#FF4D4D`). Только для необратимых действий (отключить хранилище). Файлы не удаляются — кнопки удаления файла в системе не существует.
- **Loading state:** Spinner заменяет иконку, `aria-busy="true"`, opacity не меняется.
- **Disabled:** Opacity 0.5, cursor not-allowed.

### Inputs / Fields
- **Style:** `inset-dark` / `inset-light` фон, граница 1px `border-dark` / `border-light`, radius-md (8px), padding `0 16px`, min-height 36px. Без теней.
- **Focus:** Граница становится `1px solid var(--color-accent)`.
- **Icons:** Иконка поиска/фильтра в левом слоте (16px, stroke-based SVG, steel-mist). Кнопка очистки в правом — появляется только при наличии значения.
- **Placeholder:** Faded Ash (`#7D7D8A`). Никогда не заменяет label полностью.
- **Disabled:** Opacity 0.5, pointer-events none.

### Cards / Containers
- **Corner Style:** Гладко скруглённые (16px — radius-lg).
- **Background:** `panel-dark` / `panel-light`.
- **Border:** 1px `border-dark` / `border-light`. Без box-shadow.
- **Internal Padding:** lg (24px).
- **Вложенные данные:** well-секции (`inset-dark` фон + hairline) для метаданных, путей и JSON. Вложенные карточки запрещены.

### Navigation (Sidebar)
- **Structure:** Постоянный левый сайдбар, 260px развёрнут / 64px свёрнут. Переход: 250ms ease. Отделён от контента hairline-границей.
- **Items (покой):** Ghost-стиль — прозрачный фон, steel-mist текст, иконка 20-24px.
- **Items (active):** accent-bg подложка (8-12%), ember-orange текст и иконка.
- **Разделы:** Дашборд, Файлы, Хранилища, Мониторинг, Здоровье, Логи.
- **Status badges:** Alert Red бейдж с количеством повреждённых файлов (`DATA_LOSS`) у раздела «Файлы» в реальном времени.
- **Typography:** 0.875rem, medium (500).

### File Row (Signature Component)
Центральный компонент системы — строка таблицы файлов:
- **Height:** 44px фиксированная (обязательно для виртуального скролла с тысячами строк).
- **Status badge:** Pill-тег с цветом статуса файла (success/warning/danger/info) и тонированным фоном (8-12% opacity). Всегда с текстовой меткой (Stored / Uploading / Corrupted / Disabled).
- **Timestamp:** JetBrains Mono, steel-mist, фиксированная ширина — всегда читаем параллельно с соседними строками.
- **Type:** Тип файла (`images`, `docs`), Label-стиль, steel-mist.
- **Path:** JetBrains Mono, основной/приглушённый текст, truncated с ellipsis — относительный путь `тип/yyyy/MM/dd/регион/{id}`.
- **Region:** Код региона, mono, фиксированная ширина.
- **Size:** Размер файла (`14.2 MB`), mono, фиксированная ширина, выравнивание вправо.
- **CRC32C:** JetBrains Mono, hex-представление; при несовпадении — sem-danger.
- **Expanded state:** Inline-раскрытие под строкой, well-фон, JetBrains Mono для полного пути, GUID и метаданных, история проверок целостности.

## 6. Do's and Don'ts

### Do:
- **Do** использовать Ember Orange (`#FF6B2C`) только для интерактивных элементов: primary-кнопок, активных состояний навигации, focus-колец.
- **Do** рендерить все технические значения (путь, размер, CRC32C, GUID, timestamp с миллисекундами) в JetBrains Mono.
- **Do** разделять поверхности тональным контрастом и hairline-границей 1px; box-shadow — только у overlay-элементов и accent-glow.
- **Do** поддерживать оба визуальных режима (dark/light). Каждый новый компонент обязан использовать CSS-переменные, а не захардкоженные цвета.
- **Do** соблюдать WCAG AA: минимум 4.5:1 для обычного текста, 3:1 для крупного текста и интерактивных элементов.
- **Do** применять семантические роли строго по назначению: sem-danger только для Corrupted/ERROR, sem-warning только для заполнения/WARN. Статус всегда дублировать текстом, не только цветом.
- **Do** использовать `prefers-reduced-motion` — отключать все `transition` и `animation`, сохраняя функциональность.

### Don't:
- **Don't** использовать neumorphism ни в каком виде: парные светло-тёмные тени, inset-тени, «выдавленные»/«вдавленные» поверхности. Это прямой запрет проекта.
- **Don't** использовать `border-left` или `border-right` больше 1px как цветной акцент на карточках или строках файлов. Никогда.
- **Don't** воспроизводить Bootstrap/Material шаблонный подход: серо-синие панели без характера, `#1976D2` primary, скругления везде одинаковые.
- **Don't** перегружать экраны виджетами без иерархии — нарушение принципа «Статус прежде всего» из PRODUCT.md.
- **Don't** применять `background-clip: text` с градиентом. Запрещено.
- **Don't** использовать glassmorphism (backdrop-filter + rgba blur) как декорацию.
- **Don't** использовать sem-success (`#4ADE80`) для чего-либо кроме статуса Stored/Verified и online-здоровья.
- **Don't** создавать вложенные карточки (карточка внутри карточки). Используйте well-секции.
- **Don't** добавлять кнопку удаления файла — хранилище append-only, файлы не удаляются.
- **Don't** добавлять анимированные иллюстрации, яркие градиентные фоны, emoji в UI, скругления >24px на контейнерах — это consumer-style, несовместимый с профессиональным инструментом.
