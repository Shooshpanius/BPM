import { useState, useEffect } from 'react';

/** Максимальная ширина экрана (включительно) для мобильного режима отображения. */
const MOBILE_MAX_WIDTH = 767;

/** Хук для определения мобильного режима отображения (ширина экрана ≤ 767px). */
export function useMobile(): boolean {
    const [isMobile, setIsMobile] = useState(
        () => typeof window !== 'undefined' && window.innerWidth <= MOBILE_MAX_WIDTH,
    );

    useEffect(() => {
        const mq = window.matchMedia(`(max-width: ${MOBILE_MAX_WIDTH}px)`);
        const handler = (e: MediaQueryListEvent) => setIsMobile(e.matches);
        mq.addEventListener('change', handler);
        return () => mq.removeEventListener('change', handler);
    }, []);

    return isMobile;
}
