# base defines a base stage that uses the official python runtime base image
FROM python:3.11-slim

ENV PYTHONUNBUFFERED=1
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY . .
EXPOSE 80
CMD ["gunicorn", "app:app", "-b", "0.0.0.0:80", "--workers", "4"]
