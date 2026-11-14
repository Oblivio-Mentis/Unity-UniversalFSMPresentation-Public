# Справочник API исполнения

Пространство имён: `OblivioMentis.Presentation`. Сборка: `OblivioMentis.Presentation.Runtime`. Все вызовы выполняются в главном потоке Unity. Сериализуемые поля — данные настройки: заполните их до использования и повторно привяжите драйвер после значимых изменений. Таблицы перечисляют публичный API пакета без унаследованных членов Unity.

## Координация и данные

| Тип | Публичные члены |
|---|---|
| `FsmPresentationDriver` | Поля: controller, profile, animator, audioSource, anchors, particlePools, afterimage, spriteFlash, hitStop, cameraSink. Методы: `void Bind(IUniversalFsmStateChangeSource source, IUniversalFsmSignalBus domainBus = null)`, `void Unbind()`, `void ResetPresentation()`. |
| `FsmPresentationProfile` | `UniversalFsmDefinitionAsset definition`, `PresentationBinding[] bindings`. |
| `PresentationBinding` | trigger, layerId, stateId, toStateId, eventKey, actions. |
| `PresentationEvent` | StateEnter = 0 — вход; StateExit = 1 — выход; Transition = 2 — переход; DomainEvent = 3 — доменное событие. |
| `PresentationCue` | Абстрактный `void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)`; виртуальный `void Stop(FsmPresentationDriver driver)`. |

Источник изменения состояний, шина, сигналы и структура доменных данных принадлежат UniversalFSM. Ручной Bind не инициализирует и не обновляет FSM. Автоматическая привязка проверяет идентичность определения; при ручной привязке это обязанность интеграции.

## Классы эффектов

Каждый конкретный эффект переопределяет Execute и открывает следующие поля настройки:

| Тип | Поля |
|---|---|
| `AnimatorStateCue` | string state; int layer; float fadeSeconds |
| `AnimatorTriggerCue` | string parameter; bool reset |
| `AudioCue` | AudioClip clip; float volume |
| `ParticleCue` | ParticleSystem prefab; string anchor; bool stopOnStateExit |
| `SpriteFlashCue` | Color color; float duration |
| `AfterimageCue` | Color tint; float duration |
| `HitStopCue` | float scale, duration; HitStopDurationSource durationSource; float payloadDurationMultiplier, minimumPayloadDuration, maximumPayloadDuration |
| `CameraImpulseCue` | float amplitude, duration |
| `AttachmentCue` | string anchor; bool visible, renderersOnly |
| `HitStopDurationSource` | Authored = 0 — заданная длительность; SignalPayload = 1 — длительность из сигнала |

[Смысл параметров, исходные значения и примеры](CueReference.md).

## Компоненты сцены

| Тип | Публичные члены |
|---|---|
| `PresentationAnchorRegistry` | `Entry[] entries`; `Entry Resolve(string key)`. |
| `PresentationAnchorRegistry.Entry` | string key; Transform target; Renderer[] renderers. |
| `PresentationParticlePool` | Свойства только для чтения: Prefab, Capacity, ActiveCount. Методы: `void Initialize(ParticleSystem prefab = null, int capacity = 0)`; `bool TryPlay(FsmPresentationDriver owner, Vector3 position, Quaternion rotation, int layerId = -1, int stateId = -1, bool stopOnStateExit = false)`; `void Clear(FsmPresentationDriver owner)`. |
| `PresentationAfterimage` | SpriteRenderer source; ActiveCount только для чтения; `bool Snapshot(Color tint, float duration)`; `void Clear()`. |
| `PresentationSpriteFlash` | SpriteRenderer[] renderers; `void Flash(Color color, float duration)`; `void Clear()`. |
| `PresentationHitStop` | bool scaleFixedDeltaTime; ActiveCount только для чтения; `void RequestStop(FsmPresentationDriver owner, float scale, float duration)`; `void Cancel(FsmPresentationDriver owner)`; `void SetPaused(bool paused)`. |
| `IUniversalFsmCameraImpulseSink` | `void EmitImpulse(float amplitude, float durationSeconds)`. |

Ссылки на сцену и ассеты могут быть null во время настройки, но перед исполнением нужно выполнить требования соответствующих эффектов. Закрытые сериализуемые поля вместимости пулов и силуэтов редактируются в инспекторе. Пул частиц также поддерживает явную инициализацию; после неё менять префаб или вместимость нельзя.

## Инструменты редактора

Пакет предоставляет инспекторы профиля и драйвера, а также интеграцию рабочей области Presentation в редактор UniversalFSM. Это инструменты настройки, а не публичный реестр редакторных расширений. Расширяйте настройку через собственные меню ассетов и скрипты редактора; не вызывайте внутренние типы реализации редактора пакета.

## Совместимость

В Presentation 0.3.x нет старых API директора, дескрипторов, контейнера сервисов, универсальных аренд ресурсов и составных VFX из 0.2.0. Используйте описанные выше драйвер, профиль и эффекты и создайте старые профили заново. [Миграция и ограничения](Troubleshooting.md).

[Оглавление](index.md)

