FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

# Install dependencies first so they cache independently of the source code
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy the application code
COPY flaskr ./flaskr

EXPOSE 5000

# Run the app via its application factory (flaskr/__init__.py)
CMD ["gunicorn", "--bind", "0.0.0.0:5000", "--workers", "2", "flaskr:create_app()"]
