FROM node:20-alpine AS build
WORKDIR /app

ARG FRONTEND_DIRECT_ORDERS_URL
ARG FRONTEND_AWS_API_GATEWAY_ORDERS_URL
ENV FRONTEND_DIRECT_ORDERS_URL=${FRONTEND_DIRECT_ORDERS_URL}
ENV FRONTEND_AWS_API_GATEWAY_ORDERS_URL=${FRONTEND_AWS_API_GATEWAY_ORDERS_URL}

COPY package*.json ./
RUN npm ci

COPY angular.json ./
COPY tsconfig.json ./
COPY src ./src
COPY scripts ./scripts
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
