#!/usr/bin/env python3
"""Start/stop peered geth nodes for the XSecureBox private chain."""
from __future__ import annotations

import json
import os
import subprocess
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

IMAGE = os.environ.get("GETH_IMAGE", "ethereum/client-go:v1.13.15")
NETWORK = os.environ.get("ETH_NETWORK", "secure-box_backend-network")
ETH_DIR = "/ethereum"
ETH_DIR_HOST = os.environ.get("ETH_DIR_HOST", os.environ.get("ETH_DIR", "/ethereum"))
MAX_NODES = int(os.environ.get("MAX_NODES", "7"))
PREFIX = "securebox-eth-"


def run(cmd: list[str], check: bool = True) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, check=check, capture_output=True, text=True)


def running_indices() -> list[int]:
    out = run(["docker", "ps", "--format", "{{.Names}}"], check=False).stdout.splitlines()
    found = []
    for name in out:
        if name.startswith(PREFIX) and name[len(PREFIX) :].isdigit():
            found.append(int(name[len(PREFIX) :]))
    return sorted(found)


def container_running(name: str) -> bool:
    r = run(["docker", "inspect", "-f", "{{.State.Running}}", name], check=False)
    return r.returncode == 0 and r.stdout.strip() == "true"


def wait_rpc(url: str, timeout: float = 60) -> None:
    import urllib.request

    deadline = time.time() + timeout
    body = json.dumps({"jsonrpc": "2.0", "method": "eth_blockNumber", "params": [], "id": 1}).encode()
    while time.time() < deadline:
        try:
            req = urllib.request.Request(url, data=body, headers={"Content-Type": "application/json"})
            with urllib.request.urlopen(req, timeout=2) as resp:
                if resp.status == 200:
                    return
        except Exception:
            time.sleep(1)
    raise RuntimeError(f"RPC not ready: {url}")


def bootnode_enode() -> str:
    import urllib.request

    payload = json.dumps({"jsonrpc": "2.0", "method": "admin_nodeInfo", "params": [], "id": 1}).encode()
    req = urllib.request.Request(
        "http://securebox-eth-1:8545",
        data=payload,
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=5) as resp:
        data = json.loads(resp.read())
    enode = data["result"]["enode"]
    at = enode.find("@")
    q = enode.find("?", at)
    hostport_end = q if q != -1 else len(enode)
    return enode[: at + 1] + "securebox-eth-1:30303" + (enode[hostport_end:] if q != -1 else "")


def start_node(index: int, bootnode: str | None) -> None:
    name = f"{PREFIX}{index}"
    if container_running(name):
        return
    run(["docker", "rm", "-f", name], check=False)
    cmd = [
        "docker", "run", "-d",
        "--name", name,
        "--network", NETWORK,
        "--network-alias", f"eth-{index}",
        "--restart", "unless-stopped",
        "-e", f"NODE_INDEX={index}",
        "-v", f"securebox-eth-data-{index}:/data",
        "-v", f"{ETH_DIR_HOST}:/ethereum:ro",
        "--entrypoint", "/bin/sh",
    ]
    if index == 1:
        cmd.extend(["-p", "127.0.0.1:8545:8545"])
    if bootnode:
        cmd.extend(["-e", f"BOOTNODE={bootnode}"])
    cmd.extend([IMAGE, "/ethereum/entrypoint.sh"])
    result = run(cmd)
    if result.returncode != 0:
        raise RuntimeError(result.stderr or result.stdout or "docker run failed")
    wait_rpc(f"http://{name}:8545", timeout=180)


def stop_node(index: int) -> None:
    run(["docker", "rm", "-f", f"{PREFIX}{index}"], check=False)


def persist_desired(count: int) -> None:
    path = os.path.join(ETH_DIR, "desired-count")
    try:
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(str(count))
    except OSError as ex:
        print("persist desired failed:", ex)


def read_desired() -> int:
    path = os.path.join(ETH_DIR, "desired-count")
    try:
        return max(1, min(MAX_NODES, int(open(path, encoding="utf-8").read().strip())))
    except Exception:
        return int(os.environ.get("INITIAL_NODES", "1"))


def write_lb_config(count: int) -> None:
    servers = "\n".join(f"        server {PREFIX}{i}:8545 max_fails=2 fail_timeout=5s;" for i in range(1, count + 1))
    conf = f"""upstream eth_cluster {{
        least_conn;
{servers}
}}
server {{
    listen 8545;
    location / {{
        proxy_http_version 1.1;
        proxy_set_header Connection "";
        proxy_set_header Host $host;
        proxy_connect_timeout 3s;
        proxy_read_timeout 30s;
        proxy_pass http://eth_cluster;
    }}
}}
"""
    path = os.path.join(ETH_DIR, "lb.conf")
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(conf)
    run(["docker", "exec", "securebox-eth-lb", "nginx", "-s", "reload"], check=False)


def scale(count: int) -> dict:
    count = max(1, min(MAX_NODES, int(count)))
    print(f"scaling cluster to {count}")
    persist_desired(count)
    start_node(1, None)
    enode = bootnode_enode()
    current = running_indices()
    for i in range(2, count + 1):
        print(f"starting node {i}")
        start_node(i, enode)
    for i in current:
        if i > count:
            print(f"stopping node {i}")
            stop_node(i)
    write_lb_config(count)
    nodes = [{"index": i, "name": f"{PREFIX}{i}", "url": f"http://{PREFIX}{i}:8545"} for i in range(1, count + 1)]
    return {"count": count, "max": MAX_NODES, "bootnode": enode, "loadBalancer": "http://eth-lb:8545", "nodes": nodes}


def status() -> dict:
    idxs = running_indices()
    if not idxs:
        return {"count": 0, "max": MAX_NODES, "nodes": []}
    return {
        "count": len(idxs),
        "max": MAX_NODES,
        "nodes": [{"index": i, "name": f"{PREFIX}{i}", "url": f"http://{PREFIX}{i}:8545"} for i in idxs],
    }


class Handler(BaseHTTPRequestHandler):
    def _json(self, code: int, payload: dict) -> None:
        raw = json.dumps(payload).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def do_GET(self) -> None:  # noqa: N802
        if self.path in ("/health", "/"):
            self._json(200, {"ok": True, **status()})
            return
        if self.path == "/status":
            self._json(200, status())
            return
        self._json(404, {"error": "not found"})

    def do_POST(self) -> None:  # noqa: N802
        if self.path != "/scale":
            self._json(404, {"error": "not found"})
            return
        length = int(self.headers.get("Content-Length", "0"))
        body = json.loads(self.rfile.read(length) or b"{}")
        try:
            result = scale(int(body.get("count", 1)))
            self._json(200, result)
        except Exception as ex:  # noqa: BLE001
            self._json(500, {"error": str(ex)})

    def log_message(self, fmt: str, *args) -> None:
        print("[eth-supervisor]", fmt % args)


if __name__ == "__main__":
    import threading

    port = int(os.environ.get("PORT", "8800"))
    server = ThreadingHTTPServer(("0.0.0.0", port), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    print(f"eth-supervisor listening on {port}, network={NETWORK}")
    try:
        scale(read_desired())
        print("initial cluster ready")
    except Exception as ex:  # noqa: BLE001
        print("initial scale failed:", ex)
    threading.Event().wait()
