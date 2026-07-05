# syntax=docker/dockerfile:1

FROM node:24-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build
RUN if [ -d dist/academic-registration-web/browser ]; then \
      cp -r dist/academic-registration-web/browser /site; \
    elif [ -d dist/academic-registration-web ]; then \
      cp -r dist/academic-registration-web /site; \
    else \
      echo "No se encontro el build de Angular en dist/academic-registration-web" && exit 1; \
    fi

FROM nginx:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /site /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://127.0.0.1/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
