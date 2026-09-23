# Multi-stage Nuxt build. The runtime image runs the Node server at
# .output/server/index.mjs on 0.0.0.0:3000. Browser API calls stay
# same-origin when NUXT_PUBLIC_API_BASE is blank, ".", or "/".

FROM node:22-bookworm-slim AS build
WORKDIR /app
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
RUN corepack enable && corepack prepare pnpm@10.33.3 --activate
COPY . .
RUN pnpm install --frozen-lockfile \
    && pnpm build

FROM node:22-bookworm-slim AS run
WORKDIR /app
ENV HOST=0.0.0.0 \
    PORT=3000 \
    NODE_ENV=production
EXPOSE 3000
COPY --from=build --chown=node:node /app/.output ./.output
USER node
CMD ["node", ".output/server/index.mjs"]
