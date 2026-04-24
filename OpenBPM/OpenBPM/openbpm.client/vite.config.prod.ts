import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';

// Production-конфиг для сборки в Docker-контейнере.
// Не содержит dev-server настроек (HTTPS-сертификаты, SPA-proxy),
// которые требуют наличия dotnet dev-certs.
export default defineConfig({
    plugins: [plugin()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    // Переменные окружения с префиксом BPM_C_ доступны в коде как import.meta.env.BPM_C_*
    envPrefix: 'BPM_C_',
    build: {
        outDir: 'dist',
        emptyOutDir: true,
    },
});
