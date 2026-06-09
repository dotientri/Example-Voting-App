FROM node:18-slim

WORKDIR /usr/src/app
COPY package*.json ./
RUN npm ci --only=production && npm cache clean --force
COPY . .
ENV PORT=80
EXPOSE 80
CMD ["node", "server.js"]
