import { writeFileSync } from 'node:fs';
const value = process.argv[2] || process.env.SITE_ORIGIN;
if (!value) throw new Error('Provide the confirmed public origin: npm run sitemap -- https://your-confirmed-domain');
const url = new URL(value);
if (url.protocol !== 'https:' || url.username || url.password || url.pathname !== '/' || url.search || url.hash) {
  throw new Error('Use a confirmed HTTPS origin without credentials, path, query or fragment.');
}
const origin = url.origin;
const routes = ['/', '/about', '/membership', '/join', '/faq', '/contact'];
const xml = '<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' +
  routes.map(route => `  <url><loc>${origin}${route}</loc></url>`).join('\n') + '\n</urlset>\n';
writeFileSync(new URL('../public/sitemap.xml', import.meta.url), xml);
writeFileSync(new URL('../public/robots.txt', import.meta.url), `User-agent: *\nDisallow: /admin\nSitemap: ${origin}/sitemap.xml\n`);
console.log('Sitemap generated for the confirmed public origin. Rebuild before deployment.');
