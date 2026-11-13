"""Local demo agent implementing the node side of interactions.md.

No LLM calls, Git writes, or external credentials. Reports are simulated.
"""
import json
import os
import threading
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.error import URLError
from urllib.request import Request, urlopen

NODE_ID = os.getenv('NODE_ID', 'demo-agent-01')
ROUTER_URL = os.getenv('ROUTER_URL', 'http://router:5000')
METADATA = {'name': 'Demo agent', 'id': NODE_ID,
            'host': f"{os.getenv('NODE_HOST', 'demo-node')}:{os.getenv('NODE_PORT', '5556')}",
            'type': 'unisk:demo-agent-1.0'}
lock = threading.Lock()
active = None
reports = {}


def now():
    return datetime.now(timezone.utc).isoformat()


def status():
    with lock:
        if active is None:
            return {**METADATA, 'status': 'idle', 'task-id': None, 'report': None}
        task_id, ticket, started = active
        if time.monotonic() - started < 7:
            return {**METADATA, 'status': 'busy', 'task-id': task_id, 'report': None}
        report = reports.get(task_id)
        if not report:
            report = {'finished-at': now(), 'finished-by-node-id': NODE_ID,
                      'git-repo-url': ticket.get('git-repo-url', ''),
                      'git-branch-id': ticket.get('git-branch-id') or f'node/{NODE_ID}/{task_id}',
                      'files-of-interest': ticket.get('files-of-interest', []),
                      'description': f"Demo agent processed: {ticket['description']}"}
            reports[task_id] = report
        return {**METADATA, 'status': 'done', 'task-id': task_id, 'report': report}


class Handler(BaseHTTPRequestHandler):
    def send_json(self, code, obj):
        body = json.dumps(obj).encode()
        self.send_response(code)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path == '/health':
            return self.send_json(200, {'ok': True})
        if self.path == '/status':
            return self.send_json(200, status())
        if self.path.startswith('/reports/'):
            task_id = self.path.split('/reports/', 1)[1]
            with lock:
                report = reports.get(task_id)
            return self.send_json(200, {'status': 'done', 'report': report}) if report else self.send_json(404, {'error': 'not found'})
        return self.send_json(404, {'error': 'not found'})

    def do_POST(self):
        if self.path != '/ticket':
            return self.send_json(404, {'error': 'not found'})
        try:
            length = int(self.headers.get('Content-Length', '0'))
            body = json.loads(self.rfile.read(length))
        except (ValueError, json.JSONDecodeError):
            return self.send_json(400, {'error': 'invalid json'})
        if any(body.get(key) != value for key, value in METADATA.items()):
            return self.send_json(403, {'error': 'identity mismatch'})
        task_id, ticket = body.get('task-id'), body.get('ticket')
        if not isinstance(task_id, str) or not isinstance(ticket, dict) or not ticket.get('description'):
            return self.send_json(422, {'error': 'invalid ticket'})
        global active
        with lock:
            if active and time.monotonic() - active[2] < 7:
                return self.send_json(409, {'reason': 'busy', 'current-task-id': active[0]})
            active = (task_id, ticket, time.monotonic())
        return self.send_json(202, {'task-id': task_id, 'status': 'busy'})


def register():
    payload = json.dumps(METADATA).encode()
    while True:
        try:
            req = Request(ROUTER_URL + '/register/', data=payload,
                          headers={'Content-Type': 'application/json'}, method='POST')
            with urlopen(req, timeout=4) as response:
                if response.status in (200, 201):
                    print('Demo node registered with router', flush=True)
                    break
        except (OSError, URLError) as error:
            print(f'Registration retry: {error}', flush=True)
        time.sleep(3)


if __name__ == '__main__':
    server = ThreadingHTTPServer(('0.0.0.0', int(os.getenv('NODE_PORT', '5556'))), Handler)
    threading.Thread(target=register, daemon=True).start()
    print(f"Demo node listening on {METADATA['host']}", flush=True)
    server.serve_forever()
