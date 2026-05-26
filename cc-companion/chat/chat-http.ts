/**
 * HTTP 路由 — 提供聊天页面（与 WebSocket 共享同一端口）
 */

import type { Server } from 'http';

export function setupChatHttp(httpServer: Server, chatPageHtml: string): void {
  httpServer.on('request', (req, res) => {
    if (req.method === 'GET' && (req.url === '/' || req.url === '/chat')) {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      res.end(chatPageHtml);
    } else {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      res.end('404 Not Found');
    }
  });
}
