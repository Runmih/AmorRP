FROM python:3.13-slim@sha256:e48f59cdba69a3cbd9ed2cf0f348d90d45a8d350d98134cf1070bc106ee9ed90
WORKDIR /app
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
COPY src/AmorRP.DiscordBot/requirements.txt .
RUN pip install --no-cache-dir --only-binary=:all: -r requirements.txt
COPY src/AmorRP.DiscordBot/bot.py .
USER 10001
ENTRYPOINT ["python", "/app/bot.py"]
