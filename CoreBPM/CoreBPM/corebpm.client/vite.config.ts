import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import { env } from 'process';

const target = env.ASPNETCORE_URLS
    ? env.ASPNETCORE_URLS.split(';')[0]
    : 'http://localhost:5071';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    // Переменные окружения с префиксом BPM_C_ доступны в коде как import.meta.env.BPM_C_*
    envPrefix: 'BPM_C_',
    server: {
        proxy: {
            '^/api': {
                target,
                secure: false
            }
        },
        port: parseInt(env.DEV_SERVER_PORT || '54959'),
    }
})
