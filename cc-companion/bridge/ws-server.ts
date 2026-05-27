/**
 * WebSocket Server — 接收 RimWorldMCP CCClient 连接，接收游戏事件
 */

import { WebSocketServer, WebSocket } from 'ws';
import { createServer } from 'http';
import type { IncomingMessage } from 'http';
import { CONFIG } from '../companion/config.js';

export interface WsMessage {
  type: string;
  event?: string;
  payload?: Record<string, unknown>;
  auth?: { token?: string };
  client?: { name?: string; version?: string };
  budget?: { limit: number; used: number; action: string };
}

export interface StatusChange {
  status: 'listening' | 'connected' | 'disconnected' | 'error';
  port?: number;
  client?: { name?: string; version?: string } | string;
  error?: string;
}

type EventCallback = (msg: WsMessage) => void;
type StatusCallback = (status: StatusChange) => void;
type AbortCallback = () => void;

export function createWSServer(
  port: number,
  host: string,
  token: string,
  onEvent: EventCallback,
  onStatusChange: StatusCallback,
  onAbort?: AbortCallback,
) {
  const httpServer = createServer();
  httpServer.on('error', (err: Error) => {
    console.error(`[cc-companion] HTTP 服务器错误: ${err.message}`);
  });
  httpServer.on('listening', () => {
    // 设置 SO_REUSEADDR 允许 TIME_WAIT 端口立即重用
    const addr = httpServer.address();
    if (addr && typeof addr === 'object') {
      console.log(`[cc-companion] WebSocket 服务器已启动: ws://${addr.address}:${addr.port}`);
    }
    onStatusChange?.({ status: 'listening', port });
  });

  const wss = new WebSocketServer({ server: httpServer });
  httpServer.listen(port, host);

  wss.on('error', (err: Error) => {
    console.error(`[cc-companion] WebSocket 服务器错误: ${err.message}`);
    onStatusChange?.({ status: 'error', error: err.message });
  });

  wss.on('connection', (ws: WebSocket, req: IncomingMessage) => {
    const clientIp = req.socket.remoteAddress;
    console.log(`[cc-companion] 新连接: ${clientIp}`);

    let authenticated = token ? false : true;

    ws.on('message', (data: Buffer) => {
      try {
        const msg = JSON.parse(data.toString()) as WsMessage;
        handleMessage(ws, msg);
      } catch {
        console.warn(`[cc-companion] 无效 JSON: ${data.toString().substring(0, 200)}`);
        sendJson(ws, { type: 'error', error: 'invalid json' });
      }
    });

    ws.on('close', (code: number, reason: Buffer) => {
      console.log(`[cc-companion] 连接断开: ${clientIp} (code=${code}, reason=${reason?.toString() || 'none'})`);
      onStatusChange?.({ status: 'disconnected', client: clientIp });
    });

    ws.on('error', (err: Error) => {
      console.warn(`[cc-companion] 连接错误: ${clientIp}: ${err.message}`);
    });

    function handleMessage(ws: WebSocket, msg: WsMessage) {
      switch (msg.type) {
        case 'hello':
          if (token && msg.auth?.token !== token) {
            sendJson(ws, { type: 'error', error: 'auth failed: invalid token' });
            console.warn(`[cc-companion] 认证失败: ${clientIp}`);
            ws.close();
            return;
          }
          authenticated = true;
          // 解析 Token 预算
          if (msg.budget) {
            CONFIG.tokenBudgetLimit = msg.budget.limit || 0;
            CONFIG.tokenBudgetUsed = msg.budget.used || 0;
            CONFIG.tokenBudgetAction = msg.budget.action || 'Block';
            console.log(`[cc-companion] Token 预算: ${CONFIG.tokenBudgetUsed}/${CONFIG.tokenBudgetLimit} (${CONFIG.tokenBudgetAction})`);
          }
          console.log(`[cc-companion] 握手完成: ${msg.client?.name || 'unknown'} v${msg.client?.version || '?'}`);
          sendJson(ws, { type: 'hello-ok' });
          onStatusChange?.({ status: 'connected', client: msg.client });
          break;

        case 'event':
          if (!authenticated) {
            sendJson(ws, { type: 'error', error: 'not authenticated' });
            return;
          }
          onEvent?.(msg);
          break;

        case 'ping':
          sendJson(ws, { type: 'pong' });
          break;

        case 'abort':
          if (!authenticated) {
            sendJson(ws, { type: 'error', error: 'not authenticated' });
            return;
          }
          console.log('[cc-companion] 收到中断请求');
          onAbort?.();
          sendJson(ws, { type: 'aborted' });
          break;

        default:
          console.debug(`[cc-companion] 未知消息类型: ${msg.type}`);
      }
    }
  });

  return {
    broadcast(data: string) {
      const msg = typeof data === 'string' ? data : JSON.stringify(data);
      for (const c of wss.clients) {
        if (c.readyState === 1) c.send(msg);
      }
    },
    httpServer,
  };
}

function sendJson(ws: WebSocket, obj: Record<string, unknown>): void {
  if (ws.readyState !== 1) return;
  ws.send(JSON.stringify(obj));
}
