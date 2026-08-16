FROM docker:27-cli
RUN apk add --no-cache python3
WORKDIR /opt
COPY supervisor.py /opt/supervisor.py
EXPOSE 8800
CMD ["python3", "-u", "/opt/supervisor.py"]
