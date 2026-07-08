---
title: "Скачать / версии"
weight: 1
type: "docs"
description: "История изменений VkLiveService и ссылки на файлы импорта для Streamer.bot."
slug: "download"
url: "download/"
---

**Последняя версия:** 4.1.0 (2026-07-06) — см. раздел ниже.

После скачивания следуйте [инструкции по установке]({{< relref "INSTALLATION.md" >}}).

---

### Версия 4.1.0 (Public)

**Дата:** 2026-07-06  
{{< cards cols="1" >}}
  {{< card link="/files/VkLiveService/VkLiveService_4.1.0.txt" title="Скачать VkLiveService_4.1.0.txt" subtitle="Файл импорта Streamer.bot" icon="download" tag="Public" tagColor="green" tagBorder=false >}}
{{< /cards >}}

**Основные изменения:**

- Добавлен метод `GetRewards`:
  - получает полный список наград канала из API (включая отключённые);
  - обновляет кэш `VkLiveRewardsCache`;
  - записывает результат в аргументы `rewardNames`, `rewardsCount` и `minichat.Service` (`VKVideoLive`).

Полный список изменений см. в [`CHANGELOG.md`](https://github.com/NuboHeimer-for-streamers/-Streamer.Bot-VkVideoLiveService/blob/dev/CHANGELOG.md#410----2026-07-06).

---

### Версия 4.0.0 (Public)

**Дата:** 2026-04-14  
{{< cards cols="1" >}}
  {{< card link="/files/VkLiveService/VkLiveService_4.0.0.txt" title="Скачать VkLiveService_4.0.0.txt" subtitle="Файл импорта Streamer.bot" icon="download" tag="Public" tagColor="green" tagBorder=false >}}
{{< /cards >}}

**Кратко:**

- Единый префикс `VkLive*` для глобальных переменных.
- Оптимизация списков сегодняшних зрителей (`HashSet`).
- Рефакторинг обработчиков `CPHInline` под единый паттерн.
- Новое окно авторизации VK Видео Live (`VkAuthWindow`) и сервис `VkOAuthService`.
- Триггер `VKVideoLive_PresentViewers` и метод `GetInOutViewers`.

Подробнее — в [`CHANGELOG.md`](https://github.com/NuboHeimer-for-streamers/-Streamer.Bot-VkVideoLiveService/blob/dev/CHANGELOG.md#400--2026-14-04).

---

### Версия 3.2.0 (Public)

**Дата:** 2025-02-07  
{{< cards cols="1" >}}
  {{< card link="/files/VkLiveService/VkLiveService_3.2.0.txt" title="Скачать VkLiveService_3.2.0.txt" subtitle="Файл импорта Streamer.bot" icon="download" tag="Public" tagColor="green" tagBorder=false >}}
{{< /cards >}}

**Кратко:**

- `GetViewers` больше не создаёт `user1`, `user2`, … — список только в аргументе `users`.
- Дополнительный рефакторинг `GetViewers`, обновлён `README`.

---

### Версия 3.1.0 (Public)

**Дата:** 2025-02-07  
{{< cards cols="1" >}}
  {{< card link="/files/VkLiveService/VkLiveService_3.1.0.txt" title="Скачать VkLiveService_3.1.0.txt" subtitle="Файл импорта Streamer.bot" icon="download" tag="Public" tagColor="green" tagBorder=false >}}
{{< /cards >}}

**Кратко:**

- Возможность записывать список текущих зрителей в строковую переменную.
- Расширение поведения `GetViewers` до аналогии со встроенным `PresentViewers`.

---

### Версия 3.0.0 (Public)

**Дата:** 2024-11-29  
{{< cards cols="1" >}}
  {{< card link="/files/VkLiveService/VkLiveService_3.0.0.txt" title="Скачать VkLiveService_3.0.0.txt" subtitle="Файл импорта Streamer.bot" icon="download" tag="Public" tagColor="green" tagBorder=false >}}
{{< /cards >}}

**Кратко:**

- Переход с `VkPlay` на `VKVideoLive`.
- Улучшенное логирование и обновлённый `README`.

---

### Версия 2.3.0 (Public)

**Дата:** 2024-11-28  
{{< cards cols="1" >}}
  {{< card link="/files/VkLiveService/VkLiveService_2.3.0.txt" title="Скачать VkLiveService_2.3.0.txt" subtitle="Файл импорта Streamer.bot" icon="download" tag="Public" tagColor="green" tagBorder=false >}}
{{< /cards >}}

**Кратко:**

- Базовый рабочий код интеграции с VK Video Live:
  - новый зритель;
  - менеджер наград;
  - статистика трансляции и среднее количество зрителей.

---

Полный журнал изменений: [`CHANGELOG.md`](https://github.com/NuboHeimer-for-streamers/-Streamer.Bot-VkVideoLiveService/blob/dev/CHANGELOG.md) в репозитории.
