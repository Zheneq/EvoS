# evos.admin web app: Create React App build served as static files by nginx.
# Published to ghcr.io/zheneq/evos-admin — see .github/workflows/docker-webadmin.yml.
# The app calls the API with relative URLs (src/lib/Evos.ts baseUrl = ""), so the
# bundled internal-nginx routes /api and /api/admin to the lobby.

# ---- build ----
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# CI=false: CRA treats lint warnings as errors when CI is set (as it is on GitHub
# Actions), which would fail the image build on harmless warnings.
ENV CI=false
RUN npm run build

# ---- runtime ----
FROM nginx:alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/build /usr/share/nginx/html
EXPOSE 80