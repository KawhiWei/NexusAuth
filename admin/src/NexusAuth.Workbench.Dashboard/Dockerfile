FROM node:22-alpine AS build
WORKDIR /app
ARG NPM_REGISTRY=https://registry.npmjs.org

COPY admin/src/NexusAuth.Workbench.Dashboard/package.json ./
RUN npm install --registry="$NPM_REGISTRY"

COPY admin/src/NexusAuth.Workbench.Dashboard/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY admin/src/NexusAuth.Workbench.Dashboard/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80

HEALTHCHECK --interval=10s --timeout=5s --start-period=10s --retries=6 \
    CMD wget --quiet --spider http://127.0.0.1/ || exit 1
