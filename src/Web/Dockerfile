FROM node:24.19.0-alpine AS build
WORKDIR /app
COPY src/Web/package*.json ./
RUN npm ci
COPY src/Web/ .
RUN npm run build
FROM nginx:1.28.0-alpine
COPY src/Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
