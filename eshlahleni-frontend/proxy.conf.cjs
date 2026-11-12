// Local development only. Production should reverse-proxy /api to the shared API.
const target = process.env.ESHL_API_TARGET || 'https://localhost:57972';
const address = new URL(target);
if (!['http:', 'https:'].includes(address.protocol) || !['localhost', '127.0.0.1', '[::1]'].includes(address.hostname)) {
  throw new Error('The development proxy must target a local API. Configure production API hosting separately.');
}
module.exports = {
  '/api/**': {
    target,
    secure: false, // ASP.NET local development certificate; never a production TLS policy.
    changeOrigin: true,
  },
};
