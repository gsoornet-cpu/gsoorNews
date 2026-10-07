FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# NEXT_PUBLIC_* values are inlined at build time. Platforms such as Railway pass service
# variables to the build as build args, so declare it here.
ARG NEXT_PUBLIC_API_BASE_URL=http://localhost:5000
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
# Next's standalone server binds to $HOSTNAME, which Docker sets to the container id;
# force all interfaces so the platform's proxy can reach it. $PORT is honoured by server.js.
ENV HOSTNAME=0.0.0.0
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

RUN addgroup -S jusoor && adduser -S jusoor -G jusoor
# Next.js writes image and prerender caches at runtime. The standalone copy is
# root-owned by default, so grant the unprivileged runtime user ownership of
# the generated build directory before dropping privileges.
RUN chown -R jusoor:jusoor /app/.next
USER jusoor

EXPOSE 3000
CMD ["node", "server.js"]
