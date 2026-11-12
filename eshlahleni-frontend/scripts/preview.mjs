import { createServer, request } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { resolve, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
export async function preview(target, port = 4200) {
  const root = fileURLToPath(new URL('../dist/eshlahleni-frontend/browser/', import.meta.url));
  const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.jpeg': 'image/jpeg', '.ico': 'image/x-icon', '.txt': 'text/plain', '.xml': 'application/xml' };
  const server = createServer(async (req, res) => {
    if (req.url.startsWith('/api/')) {
      if (!target) { res.writeHead(502); return res.end('Start the API and configure ESHL_API_TARGET.'); }
      const proxy = request(new URL(req.url, target), { method: req.method, headers: req.headers }, upstream => { res.writeHead(upstream.statusCode, upstream.headers); upstream.pipe(res); });
      proxy.on('error', () => { if (res.destroyed || res.writableEnded) return; if (!res.headersSent) res.writeHead(502); res.end('API unavailable'); }); req.pipe(proxy); return;
    }
    try {
      let path = resolve(root, '.' + decodeURIComponent(new URL(req.url, 'http://localhost').pathname));
      if (!path.startsWith(resolve(root) + '/') && !path.startsWith(resolve(root) + '\\') && path !== resolve(root)) { res.writeHead(403); return res.end(); }
      try { if (!(await stat(path)).isFile()) path = resolve(root, 'index.html'); } catch { path = resolve(root, 'index.html'); }
      res.writeHead(200, { 'Content-Type': types[extname(path)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' }); res.end(await readFile(path));
    } catch { res.writeHead(500); res.end('Preview unavailable'); }
  });
  await new Promise((done, reject) => { server.once('error', reject); server.listen(port, '127.0.0.1', done); }); return server;
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await preview(process.env.ESHL_API_TARGET, Number(process.env.PORT ?? 4200)); console.log('Local production preview: http://127.0.0.1:' + (process.env.PORT ?? 4200));
}
