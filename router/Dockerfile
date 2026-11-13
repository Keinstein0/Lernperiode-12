# syntax=docker/dockerfile:1

# --------------------------------------------------------------------------
# Test stage: builds the app and runs the unit test suite.
# The final image depends on this stage, so the build fails if any test fails.
# --------------------------------------------------------------------------
FROM python:3.12-slim AS test

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

# Install dependencies first so they cache independently of the source code
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy the application code and the test suite, then run the tests
COPY flaskr ./flaskr
COPY tests ./tests
RUN python -m pytest tests -v

# --------------------------------------------------------------------------
# Runtime stage: only the tested application code is carried over.
# --------------------------------------------------------------------------
FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

# Install dependencies first so they cache independently of the source code
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy the (already tested) application code from the test stage
COPY --from=test /app/flaskr ./flaskr

EXPOSE 5000

# Run the app via its application factory (flaskr/__init__.py)
CMD ["gunicorn", "--bind", "0.0.0.0:5000", "--workers", "2", "flaskr:create_app()"]

