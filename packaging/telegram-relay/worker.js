// Посредник для уведомлений, если api.telegram.org недоступен без VPN. Запускается в Cloudflare Workers.
// Передаёт в Telegram только три запроса, которые нужны приложению, и ничего не хранит.
const TELEGRAM = "https://api.telegram.org";
const ALLOWED_PATH = /^\/bot\d+:[A-Za-z0-9_-]+\/(getMe|getUpdates|sendMessage)$/;

export default {
  async fetch(request) {
    const url = new URL(request.url);
    if (url.pathname === "/") {
      return new Response("Посредник работает");
    }

    if (!ALLOWED_PATH.test(url.pathname)) {
      return new Response("Not found", { status: 404 });
    }

    return fetch(new Request(TELEGRAM + url.pathname + url.search, request));
  },
};
