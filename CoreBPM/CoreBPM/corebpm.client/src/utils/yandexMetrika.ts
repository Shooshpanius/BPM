/**
 * Инициализация счётчика Яндекс.Метрики.
 *
 * ID счётчика берётся из переменной окружения BPM_C_YANDEX_METRIKA_ID.
 * Если переменная не задана — скрипт не загружается.
 */
export function initYandexMetrika(): void {
    const counterId = import.meta.env.BPM_C_YANDEX_METRIKA_ID;

    if (!counterId) {
        return;
    }

    const id = Number(counterId);

    if (isNaN(id) || id <= 0) {
        console.warn(`[YandexMetrika] Некорректный ID счётчика: ${counterId}`);
        return;
    }

    // Объявляем ym на window
    type YmFunction = (...args: unknown[]) => void;
    const w = window as Window & { ym?: YmFunction & { a?: unknown[]; l?: number } };

    w.ym = w.ym ?? function (...args: unknown[]) {
        (w.ym!.a = w.ym!.a ?? []).push(args);
    };
    w.ym.l = 1 * (new Date() as unknown as number);

    // Вставляем тег скрипта Яндекс.Метрики
    const script = document.createElement('script');
    script.type = 'text/javascript';
    script.async = true;
    script.src = 'https://mc.yandex.ru/metrika/tag.js';
    const firstScript = document.getElementsByTagName('script')[0];
    firstScript.parentNode!.insertBefore(script, firstScript);

    // Инициализируем счётчик
    w.ym(id, 'init', {
        clickmap: true,
        trackLinks: true,
        accurateTrackBounce: true,
        webvisor: false,
    });

    // noscript-пиксель для браузеров без JS
    const noscript = document.createElement('noscript');
    const img = document.createElement('img');
    img.src = `https://mc.yandex.ru/watch/${id}`;
    img.style.cssText = 'position:absolute; left:-9999px;';
    img.alt = '';
    noscript.appendChild(img);
    document.body.appendChild(noscript);
}
