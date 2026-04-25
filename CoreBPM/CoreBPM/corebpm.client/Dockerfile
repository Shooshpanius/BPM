# =============================================================================
# Stage 1: сборка React-приложения
# =============================================================================
FROM node:lts-alpine AS build

WORKDIR /app

# Установка зависимостей (кэш слоя)
COPY package.json package-lock.json* ./
RUN npm ci

# Копирование исходников и сборка через production-конфиг
COPY . .
RUN npx vite build --config vite.config.prod.ts

# =============================================================================
# Stage 2: раздача статики через Nginx
# =============================================================================
FROM nginx:stable-alpine AS runtime

# Удаляем стандартный конфиг nginx
RUN rm /etc/nginx/conf.d/default.conf

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
