import http from "node:http";
import { readFile, stat } from "node:fs/promises";
import path from "node:path";
const root = path.resolve("dist/ntm-greenpulse-frontend/browser");
const port = Number(process.argv[process.argv.indexOf("--port") + 1]) || 4301;
const types = {
  ".html": "text/html",
  ".js": "application/javascript",
  ".css": "text/css",
  ".json": "application/json",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".ico": "image/x-icon",
};
http
  .createServer(async (req, res) => {
    try {
      const pathname = decodeURIComponent(
        new URL(req.url, "http://localhost").pathname,
      );
      if (pathname.startsWith("/api/")) {
        res.writeHead(502, { "Content-Type": "text/plain" });
        res.end("API proxy not configured in static preview.");
        return;
      }
      let target = path.resolve(root, "." + pathname);
      if (!target.startsWith(root + path.sep) && target !== root) {
        res.writeHead(403);
        res.end();
        return;
      }
      try {
        if (!(await stat(target)).isFile())
          target = path.join(root, "index.html");
      } catch {
        target = path.join(root, "index.html");
      }
      res.writeHead(200, {
        "Content-Type":
          types[path.extname(target)] || "application/octet-stream",
        "Cache-Control": "no-store",
      });
      res.end(await readFile(target));
    } catch {
      res.writeHead(500);
      res.end("Preview unavailable.");
    }
  })
  .listen(port, "127.0.0.1", () =>
    console.log(`NTM preview: http://127.0.0.1:${port}`),
  );
