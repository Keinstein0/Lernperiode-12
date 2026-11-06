FROM python:3.12-slim

WORKDIR /app

ENV WORKSPACE_DIR=/work
ENV DATA_DIR=/data

RUN apt-get update && apt-get install -y --no-install-recommends \
    git \
    && rm -rf /var/lib/apt/lists/*
	
COPY requirements.txt .

RUN pip install --no-cache-dir -r requirements.txt

COPY node.py .

EXPOSE 5556

CMD ["python", "node.py"]