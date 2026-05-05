import { useState, useCallback, useRef } from 'react';

/**
 * Хук для реализации поведения модального диалога:
 * клик мимо окна не закрывает диалог, а на секунду подсвечивает кнопки действий.
 *
 * Возвращает:
 * - `shaking` — true пока идёт анимация подсветки (900 мс)
 * - `shake` — вызвать при клике на оверлей
 */
export function useModalShake() {
    const [shaking, setShaking] = useState(false);
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    const shake = useCallback(() => {
        if (timerRef.current) clearTimeout(timerRef.current);
        setShaking(true);
        timerRef.current = setTimeout(() => setShaking(false), 900);
    }, []);

    return { shaking, shake };
}
