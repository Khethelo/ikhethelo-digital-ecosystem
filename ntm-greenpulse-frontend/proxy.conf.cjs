const target = process.env.NTM_API_TARGET || "https://localhost:57972";
const url = new URL(target);
if (
  !["http:", "https:"].includes(url.protocol) ||
  !["localhost", "127.0.0.1", "[::1]"].includes(url.hostname)
)
  throw new Error("The development proxy requires a local API target.");
module.exports = { "/api/**": { target, secure: false, changeOrigin: true } };
