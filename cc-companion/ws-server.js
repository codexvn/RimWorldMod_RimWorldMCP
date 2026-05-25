/**
 * WebSocket Server — 接收 RimWorldMCP CCClient 连接，接收游戏事件
 *
 * 协议:
 *   RimWorld → 发送 hello: { type:"hello", client:{...}, auth:{token:"..."} }
 *   Server  → 回复 hello-ok: { type:"hello-ok" }
 *   RimWorld → 发送事件: { type:"event", event:"rimworld.alert", payload:{...} }
 *   Server  ↔ 心跳: { type:"ping" } / { type:"pong" }
 */

import { WebSocketServer } from 'ws';

export function createWSServer(port, token, onEvent, onStatusChange) {
  const wss = new WebSocketServer({ port, host: '127.0.0.1' });

  wss.on('listening', () => {
    console.log(`[cc-companion] WebSocket 服务器已启动: ws://127.0.0.1:${port}`);
    onStatusChange?.({ status: 'listening', port });
  });

  wss.on('error', (err) => {
    console.error(`[cc-companion] WebSocket 服务器错误: ${err.message}`);
    onStatusChange?.({ status: 'error', error: err.message });
  });

  wss.on('connection', (ws, req) => {
    const clientIp = req.socket.remoteAddress;
    console.log(`[cc-companion] 新连接: ${clientIp}`);

    let authenticated = token ? false : true;

    ws.on('message', (data) => {
      try {
        const msg = JSON.parse(data.toString());
        handleMessage(ws, msg);
      } catch (err) {
        console.warn(`[cc-companion] 无效 JSON: ${data.toString().substring(0, 200)}`);
        sendJson(ws, { type: 'error', error: 'invalid json' });
      }
    });

    ws.on('close', (code, reason) => {
      console.log(`[cc-companion] 连接断开: ${clientIp} (code=${code}, reason=${reason?.toString() || 'none'})`);
      onStatusChange?.({ status: 'disconnected', client: clientIp });
    });

    ws.on('error', (err) => {
      console.warn(`[cc-companion] 连接错误: ${clientIp}: ${err.message}`);
    });

    function handleMessage(ws, msg) {
      switch (msg.type) {
        case 'hello':
          if (token && msg.auth?.token !== token) {
            sendJson(ws, { type: 'error', error: 'auth failed: invalid token' });
            console.warn(`[cc-companion] 认证失败: ${clientIp}`);
            ws.close();
            return;
          }
          authenticated = true;
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

        default:
          console.debug(`[cc-companion] 未知消息类型: ${msg.type}`);
      }
    }
  });

  return {
    close() {
      console.log('[cc-companion] WebSocket 服务器关闭中...');
      wss.close();
    },
    getClients() {
      return [...wss.clients].filter(c => c.readyState === 1);
    }
  };
}

function sendJson(ws, obj) {
  if (ws.readyState !== 1) return;
  ws.send(JSON.stringify(obj));
}
