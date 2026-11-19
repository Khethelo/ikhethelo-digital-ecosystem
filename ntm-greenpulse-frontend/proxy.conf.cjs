const target = process.env.NTM_API_TARGET || "https://localhost:57972";
const url = new URL(target);
const local = ["http:", "https:"].includes(url.protocol) &&
  ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname);
const render = url.origin === "https://ikhethelo-digital-api.onrender.com";
if ((!local && !render) || url.username || url.password || url.pathname !== "/" || url.search || url.hash)
  throw new Error("The development proxy requires a local API or the approved Render API origin.");
module.exports = { "/api/**": { target: url.origin, secure: !local, changeOrigin: true } };
