"""שירות AI (Mock) - שרת HTTP/JSON קטן ללא תלויות חיצוניות.

הרצה:  python app.py [--port 8000]
נקודות קצה:
  POST /v1/control-suggestions    {prompt, catalog, ...}  ->  הצעת בקרה
  POST /v1/proactive-suggestions  {catalog, ...}          ->  {suggestions: [...]}
  GET  /health
"""
import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import mock_model

ROUTES = {
    "/v1/control-suggestions": mock_model.suggest_control,
    "/v1/proactive-suggestions": mock_model.suggest_proactive,
}


class Handler(BaseHTTPRequestHandler):
    def _send(self, status: int, payload: dict) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path == "/health":
            self._send(200, {"status": "ok", "model": "mock-rules-v1"})
        else:
            self._send(404, {"error": "not found"})

    def _read_body(self) -> bytes:
        if "chunked" in self.headers.get("Transfer-Encoding", "").lower():
            chunks = []
            while True:
                size = int(self.rfile.readline().split(b";")[0].strip() or b"0", 16)
                if size == 0:
                    self.rfile.readline()
                    break
                chunks.append(self.rfile.read(size))
                self.rfile.readline()
            return b"".join(chunks)
        return self.rfile.read(int(self.headers.get("Content-Length", 0)))

    def do_POST(self):
        handler = ROUTES.get(self.path)
        if handler is None:
            return self._send(404, {"error": "not found"})
        try:
            raw = self._read_body()
            if not raw or len(raw) > 2_000_000:
                return self._send(400, {"error": "invalid body size"})
            request = json.loads(raw.decode("utf-8"))
            if not isinstance(request.get("catalog"), list) or not request["catalog"]:
                return self._send(400, {"error": "catalog is required"})
            self._send(200, handler(request))
        except (json.JSONDecodeError, KeyError) as exc:
            self._send(400, {"error": f"bad request: {exc}"})
        except Exception as exc:  # noqa: BLE001 - שרת הדגמה: לא מפילים את התהליך
            self._send(500, {"error": f"internal error: {exc}"})

    def log_message(self, fmt, *args):
        print("[ai-mock]", fmt % args)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()
    server = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    print(f"AI mock service listening on http://127.0.0.1:{args.port}")
    server.serve_forever()
