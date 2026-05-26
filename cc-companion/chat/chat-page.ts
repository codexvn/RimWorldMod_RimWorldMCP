/**
 * 聊天页面 HTML 生成 — 单文件自包含，内联 CSS + JS
 */

export interface ChatPageConfig {
  token: string;
  modelName: string;
  projectPath: string;
}

export function getChatPageHtml(config: ChatPageConfig): string {
  const escapedToken = JSON.stringify(config.token);
  const modelName = escHtml(config.modelName);
  const projectPath = escHtml(config.projectPath);

  return `<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>RimWorld AI 助手</title>
<style>
  /* ===== CSS Variables ===== */
  :root {
    --bg: #12141a;
    --bg-soft: #181b22;
    --card: #1e2028;
    --card-hover: #262a35;
    --text: #e4e4e7;
    --text-strong: #fafafa;
    --muted: #71717a;
    --border: #27272a;
    --border-strong: #3f3f46;
    --accent: #ff5c5c;
    --accent-subtle: rgba(255,92,92,0.12);
    --secondary: #1a1c23;
    --ok: #22c55e;
    --ok-muted: rgba(34,197,94,0.75);
    --warn: #f59e0b;
    --danger: #ef4444;
    --info: #3b82f6;
    --info-bg: rgba(59,130,246,0.12);
    --mono: "Cascadia Code","Fira Code","Consolas",monospace;
    --font: system-ui,-apple-system,"Microsoft YaHei","Segoe UI",sans-serif;
    --radius-sm: 6px;
    --radius-md: 8px;
    --radius-lg: 12px;
    --radius-full: 9999px;
    color-scheme: dark;
  }

  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
  html, body { height: 100%; overflow: hidden; }
  body {
    font-family: var(--font);
    background: var(--bg); color: var(--text);
    display: flex; flex-direction: column;
  }
  #app {
    display: flex; flex-direction: column;
    height: 100vh; max-width: 960px; margin: 0 auto; width: 100%;
  }

  /* ===== Global Scrollbar ===== */
  ::-webkit-scrollbar { width: 8px; height: 8px; }
  ::-webkit-scrollbar-track { background: transparent; }
  ::-webkit-scrollbar-thumb { background: var(--border); border-radius: var(--radius-full); }
  ::-webkit-scrollbar-thumb:hover { background: var(--border-strong); }

  /* ===== Header ===== */
  header {
    display: flex; align-items: center; gap: 10px;
    padding: 12px 16px; background: var(--bg-soft);
    border-bottom: 1px solid var(--border);
    flex-shrink: 0; position: relative;
  }
  header h1 { font-size: 15px; font-weight: 600; color: var(--text-strong); flex: 1; }
  .status-dot {
    width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0;
    transition: background 0.3s, box-shadow 0.3s, transform 0.3s;
  }
  .status-dot.connected { background: var(--ok); box-shadow: 0 0 8px var(--ok-muted); }
  .status-dot.connecting {
    background: var(--warn); box-shadow: 0 0 8px rgba(245,158,11,0.66);
    animation: dot-pulse 1s infinite;
  }
  .status-dot.disconnected { background: var(--danger); box-shadow: 0 0 8px rgba(239,68,68,0.66); }
  @keyframes dot-pulse {
    0%,100% { opacity: 0.6; transform: scale(1); }
    50% { opacity: 1; transform: scale(1.15); }
  }
  #status-text { font-size: 13px; color: var(--muted); }

  /* ===== Info button & panel ===== */
  #info-btn {
    background: none; border: 1px solid var(--border); color: var(--muted);
    border-radius: 4px; padding: 2px 8px; cursor: pointer;
    font-size: 13px; transition: background 0.2s, color 0.2s;
  }
  #info-btn:hover { background: var(--card-hover); color: var(--accent); }

  #info-overlay {
    display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%;
    z-index: 100; background: rgba(0,0,0,0.5);
  }
  #info-panel {
    position: absolute; top: 52px; left: 16px; right: 16px;
    background: var(--card); border: 1px solid var(--border-strong);
    border-radius: var(--radius-md); padding: 16px;
    box-shadow: var(--shadow-md, 0 4px 12px rgba(0,0,0,0.3));
    max-width: 600px;
  }
  #info-close {
    float: right; background: none; border: none; color: var(--muted);
    cursor: pointer; font-size: 18px; line-height: 1;
  }
  #info-close:hover { color: var(--danger); }
  #info-panel h2 { font-size: 14px; color: var(--accent); margin-bottom: 10px; }
  .info-table { width: 100%; border-collapse: collapse; font-size: 13px; }
  .info-table th, .info-table td {
    text-align: left; padding: 6px 8px; border-bottom: 1px solid var(--border);
  }
  .info-table th { color: var(--muted); width: 80px; font-weight: 400; }
  .info-table td { color: var(--text); word-break: break-all; }

  /* ===== Message Thread ===== */
  #messages {
    flex: 1; overflow-y: auto; padding: 16px 12px;
    display: flex; flex-direction: column; gap: 4px;
    scroll-behavior: smooth;
  }

  /* ===== Message Groups ===== */
  .chat-group {
    display: flex; gap: 10px; align-items: flex-start;
    margin-bottom: 2px;
  }
  .chat-group.user { flex-direction: row-reverse; }

  .chat-avatar {
    width: 34px; height: 34px; border-radius: var(--radius-md);
    display: flex; align-items: center; justify-content: center;
    flex-shrink: 0; font-size: 13px; font-weight: 600;
    margin-top: 2px; user-select: none;
  }
  .chat-avatar.assistant {
    background: var(--secondary); color: var(--accent); border: 1px solid var(--border);
  }
  .chat-avatar.user {
    background: var(--accent-subtle); color: var(--accent); border: 1px solid transparent;
  }
  .chat-avatar.system {
    background: var(--secondary); color: var(--info); border: 1px solid var(--border);
  }

  .chat-group-messages {
    display: flex; flex-direction: column; gap: 2px;
    max-width: 85%; min-width: 0;
  }
  .chat-group.user .chat-group-messages { align-items: flex-end; }

  .chat-group-footer {
    display: flex; gap: 6px; margin-top: 2px;
    font-size: 11px; color: var(--muted); padding: 0 2px;
  }
  .chat-group.user .chat-group-footer { justify-content: flex-end; }
  .chat-group-footer__name { font-weight: 500; }
  .chat-group-footer__time { opacity: 0.7; }

  /* ===== Chat Bubbles ===== */
  .chat-bubble {
    display: inline-block; max-width: 100%;
    padding: 8px 12px; border-radius: var(--radius-lg);
    background: var(--card); border: 1px solid var(--border);
    color: var(--text); font-size: 14px; line-height: 1.55;
    word-wrap: break-word; white-space: pre-wrap;
    transition: background 0.15s, border-color 0.15s;
  }
  .chat-group.user .chat-bubble {
    background: var(--accent-subtle);
    border-color: transparent;
  }
  .chat-bubble.fade-in {
    animation: chat-fade-in 200ms ease-out both;
  }
  .chat-bubble.streaming {
    animation: chat-pulse-border 1.5s ease-out infinite;
  }

  @keyframes chat-fade-in {
    from { opacity: 0; transform: translateY(4px); }
    to { opacity: 1; transform: translateY(0); }
  }
  @keyframes chat-pulse-border {
    0%,100% { border-color: var(--border); }
    50% { border-color: var(--accent); }
  }

  /* ===== Tool Cards ===== */
  .chat-tool-card {
    border: 1px solid var(--border); border-radius: var(--radius-md);
    margin-top: 6px; background: var(--secondary);
    cursor: pointer; overflow: hidden; max-height: 120px;
    transition: max-height 0.25s ease, border-color 0.2s;
  }
  .chat-tool-card:hover { border-color: var(--border-strong); }
  .chat-tool-card.expanded { max-height: none; }

  .chat-tool-card__header {
    display: flex; align-items: center; gap: 6px;
    padding: 8px 10px; font-size: 13px; font-weight: 600;
  }
  .chat-tool-card__icon { flex-shrink: 0; font-size: 14px; }
  .chat-tool-card__name { flex: 1; color: var(--text-strong); }
  .chat-tool-card__expand { font-size: 11px; color: var(--muted); }

  .chat-tool-card__detail {
    padding: 0 10px 4px;
    font-size: 12px; color: var(--muted);
    white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
  }

  .chat-tool-card__output {
    padding: 6px 10px 8px;
    font-family: var(--mono); font-size: 11px;
    color: var(--text); white-space: pre-wrap; word-break: break-word;
  }
  .chat-tool-card__output.inline {
    max-height: none;
  }
  .chat-tool-card__output.truncated {
    max-height: 44px; overflow: hidden;
    position: relative;
  }
  .chat-tool-card__output.truncated::after {
    content: ""; position: absolute; bottom: 0; left: 0; right: 0;
    height: 16px;
    background: linear-gradient(to bottom, transparent, var(--secondary));
    pointer-events: none;
  }

  .chat-tool-card__status {
    padding: 4px 10px 8px; font-size: 11px;
  }
  .chat-tool-card__status.done { color: var(--ok); }
  .chat-tool-card__status.pending { color: var(--muted); }
  .chat-tool-card__status.error { color: var(--danger); }

  /* ===== Reading Indicator ===== */
  .chat-reading {
    display: inline-flex; align-items: center; gap: 4px;
    padding: 12px 16px; border-radius: var(--radius-lg);
    background: var(--card); border: 1px solid var(--border);
  }
  .chat-reading__dot {
    width: 7px; height: 7px; border-radius: 50%;
    background: var(--muted);
    animation: reading-bounce 1.4s ease-in-out infinite both;
  }
  .chat-reading__dot:nth-child(1) { animation-delay: 0s; }
  .chat-reading__dot:nth-child(2) { animation-delay: 0.2s; }
  .chat-reading__dot:nth-child(3) { animation-delay: 0.4s; }

  @keyframes reading-bounce {
    0%,60%,100% { opacity: 0.3; transform: scale(0.8); }
    30% { opacity: 1; transform: scale(1); }
  }

  /* ===== Divider (result / compaction) ===== */
  .chat-divider {
    display: flex; align-items: center; gap: 10px;
    margin: 12px 8px;
  }
  .chat-divider__line { flex: 1; height: 1px; background: var(--border); }
  .chat-divider__label {
    padding: 3px 12px; border-radius: var(--radius-full);
    border: 1px solid var(--border);
    font-size: 11px; color: var(--muted); white-space: nowrap;
    user-select: none;
  }
  .chat-divider__label.ok {
    border-color: var(--ok-muted); color: var(--ok);
    background: rgba(34,197,94,0.08);
  }
  .chat-divider__label.error {
    border-color: rgba(239,68,68,0.5); color: var(--danger);
    background: rgba(239,68,68,0.08);
  }
  .chat-divider__label.warn {
    border-color: rgba(245,158,11,0.5); color: var(--warn);
    background: rgba(245,158,11,0.08);
  }

  /* ===== New Messages Pill ===== */
  #new-msg-pill {
    display: none; align-self: center;
    align-items: center; gap: 4px;
    padding: 5px 14px; font-size: 12px; cursor: pointer;
    color: var(--text); background: var(--card);
    border: 1px solid var(--border); border-radius: var(--radius-full);
    margin: 4px auto; transition: border-color 0.2s; z-index: 10;
  }
  #new-msg-pill:hover { border-color: var(--accent); }

  /* ===== Compose Area ===== */
  .chat-compose {
    position: sticky; bottom: 0; flex-shrink: 0;
    display: flex; flex-direction: column;
    padding: 16px 16px 12px; gap: 8px;
    background: linear-gradient(to bottom, transparent, var(--bg) 25%);
    z-index: 10;
  }
  .chat-compose__row {
    display: flex; gap: 8px; align-items: flex-end;
  }
  #chat-input {
    flex: 1; resize: none; overflow-y: auto;
    padding: 10px 12px; border-radius: var(--radius-md);
    background: var(--card); color: var(--text);
    border: 1px solid var(--border); outline: none;
    font-family: var(--font); font-size: 14px; line-height: 1.5;
    min-height: 40px; max-height: 150px;
    transition: border-color 0.2s;
  }
  #chat-input:focus { border-color: var(--accent); }
  #chat-input::placeholder { color: var(--muted); }
  #send-btn {
    padding: 10px 18px; border-radius: var(--radius-md); border: none;
    background: var(--accent); color: #fff; font-size: 14px;
    cursor: pointer; transition: opacity 0.2s;
    white-space: nowrap; align-self: flex-end;
    display: flex; align-items: center; gap: 4px;
  }
  #send-btn:hover { opacity: 0.85; }
  #send-btn:disabled { opacity: 0.35; cursor: not-allowed; }
  #send-btn kbd {
    display: inline-block; padding: 1px 5px; margin-left: 2px;
    font-family: var(--mono); font-size: 11px;
    border-radius: 3px;
    background: rgba(0,0,0,0.25);
    border: 1px solid rgba(255,255,255,0.15);
    vertical-align: middle;
  }
</style>
</head>
<body>
<div id="app">
  <header>
    <span class="status-dot disconnected" id="status-dot"></span>
    <span id="status-text">未连接</span>
    <h1>RimWorld AI 助手</h1>
    <button id="info-btn">ℹ️</button>
  </header>

  <!-- Info panel overlay -->
  <div id="info-overlay">
    <div id="info-panel">
      <button id="info-close">&times;</button>
      <h2>SDK 信息</h2>
      <table class="info-table">
        <tr><th>模型</th><td><span id="info-model">${modelName}</span></td></tr>
        <tr><th>认证</th><td><span id="info-key-source">-</span></td></tr>
        <tr><th>目录</th><td><span id="info-cwd">${projectPath}</span></td></tr>
        <tr><th>版本</th><td><span id="info-version">-</span></td></tr>
        <tr><th>会话</th><td><span id="info-session">-</span></td></tr>
        <tr><th>权限</th><td><span id="info-permission">-</span></td></tr>
      </table>
    </div>
  </div>

  <div id="messages"></div>
  <button id="new-msg-pill" onclick="scrollToBottom()">↓ 回到底部</button>
  <div class="chat-compose">
    <div class="chat-compose__row">
      <textarea id="chat-input" placeholder="输入消息..." rows="1"></textarea>
      <button id="send-btn" disabled>发送<kbd>↵</kbd></button>
    </div>
  </div>
</div>
<script>
(function() {
  const TOKEN = ${escapedToken};
  const RECONNECT_BASE = 5000;
  const RECONNECT_MAX = 30000;

  const messagesEl = document.getElementById('messages');
  const inputEl = document.getElementById('chat-input');
  const sendBtn = document.getElementById('send-btn');
  const statusDot = document.getElementById('status-dot');
  const statusText = document.getElementById('status-text');
  const newMsgPill = document.getElementById('new-msg-pill');

  // Info panel
  const infoOverlay = document.getElementById('info-overlay');
  const infoBtn = document.getElementById('info-btn');
  const infoClose = document.getElementById('info-close');

  infoBtn.addEventListener('click', function() { infoOverlay.style.display = 'block'; });
  infoClose.addEventListener('click', function() { infoOverlay.style.display = 'none'; });
  infoOverlay.addEventListener('click', function(e) {
    if (e.target === infoOverlay) infoOverlay.style.display = 'none';
  });

  let ws = null;
  let reconnectTimer = null;
  let reconnectDelay = RECONNECT_BASE;
  let connected = false;

  // ===== Message Group State =====
  let lastRole = null;
  let currentGroupEl = null;
  let awaitingResponse = false;
  let readingIndicatorEl = null;
  let streamingTimer = null;

  // ===== WebSocket =====
  function connect() {
    if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) return;
    setStatus('connecting', '连接中...');
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = protocol + '//' + location.host;
    ws = new WebSocket(url);

    ws.onopen = () => {
      reconnectDelay = RECONNECT_BASE;
      const hello = { type: 'hello', client: { name: 'ChatPage', version: '1.0' } };
      if (TOKEN) hello.auth = { token: TOKEN };
      ws.send(JSON.stringify(hello));
    };

    ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data);
        handleMessage(msg);
      } catch {}
    };

    ws.onclose = () => {
      connected = false;
      setStatus('disconnected', '已断开');
      sendBtn.disabled = true;
      hideReadingIndicator();
      scheduleReconnect();
    };

    ws.onerror = () => {
      if (ws) ws.close();
    };
  }

  function scheduleReconnect() {
    if (reconnectTimer) return;
    reconnectTimer = setTimeout(() => {
      reconnectTimer = null;
      reconnectDelay = Math.min(reconnectDelay * 1.5, RECONNECT_MAX);
      connect();
    }, reconnectDelay);
  }

  function setStatus(state, text) {
    statusDot.className = 'status-dot ' + state;
    statusText.textContent = text;
  }

  // ===== Message handling =====
  function handleMessage(msg) {
    switch (msg.type) {
      case 'hello-ok':
        connected = true;
        setStatus('connected', '已连接');
        sendBtn.disabled = false;
        hideReadingIndicator();
        break;

      case 'assistant':
      case 'user': {
        const isAssistant = msg.type === 'assistant';
        const roleLabel = isAssistant ? 'AI' : '事件';
        const content = msg.message?.content;
        if (!content) return;
        if (isAssistant) hideReadingIndicator();
        addMessage(isAssistant ? 'assistant' : 'user', roleLabel, content);
        break;
      }

      case 'system': {
        if (msg.subtype === 'init') updateSdkInfo(msg);
        break;
      }

      case 'result': {
        const sub = msg.subtype || 'unknown';
        const label = sub === 'success' ? '✓ 执行成功'
                     : sub === 'aborted' ? '⏹ 已中断'
                     : '✗ 执行失败';
        const cls = sub === 'success' ? 'ok' : sub === 'aborted' ? 'warn' : 'error';
        hideReadingIndicator();
        finalizeCurrentGroup(null);
        addResult(cls, label);
        break;
      }
    }
  }

  function updateSdkInfo(msg) {
    var el;
    el = document.getElementById('info-model'); if (el) el.textContent = msg.model || '?';
    el = document.getElementById('info-version'); if (el) el.textContent = msg.claude_code_version || '?';
    el = document.getElementById('info-session'); if (el) el.textContent = msg.session_id || '?';
    el = document.getElementById('info-cwd'); if (el) el.textContent = msg.cwd || '?';
    el = document.getElementById('info-permission'); if (el) el.textContent = msg.permissionMode || '?';
    el = document.getElementById('info-key-source'); if (el) el.textContent = msg.apiKeySource || '?';
    if (msg.mcp_servers) {
      msg.mcp_servers.forEach(function(s) {
        var span = document.querySelector('.mcp-status[data-name="' + s.name.replace(/"/g, '') + '"]');
        if (span) span.textContent = s.status;
      });
    }
  }

  // ===== Utils =====
  function formatTime(d) {
    return d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
  }

  function getAvatarText(role) {
    if (role === 'assistant') return 'AI';
    if (role === 'user') return '你';
    return '系';
  }

  function getGroupName(role) {
    if (role === 'assistant') return 'AI';
    if (role === 'user') return '你';
    return '系统';
  }

  // ===== Group Management =====
  function finalizeCurrentGroup(newRole) {
    if (!currentGroupEl) return;
    var tsEl = currentGroupEl.querySelector('.chat-group-footer__time');
    if (tsEl) tsEl.textContent = formatTime(new Date());
    currentGroupEl = null;
  }

  function openNewGroup(role) {
    var group = document.createElement('div');
    group.className = 'chat-group' + (role === 'user' ? ' user' : '');

    var avatar = document.createElement('div');
    avatar.className = 'chat-avatar ' + role;
    avatar.textContent = getAvatarText(role);

    var col = document.createElement('div');
    col.className = 'chat-group-messages';

    var footer = document.createElement('div');
    footer.className = 'chat-group-footer';
    footer.innerHTML = '<span class="chat-group-footer__name">' + getGroupName(role) + '</span>'
      + '<span class="chat-group-footer__time">' + formatTime(new Date()) + '</span>';

    col.appendChild(footer);

    group.appendChild(avatar);
    group.appendChild(col);

    messagesEl.appendChild(group);
    currentGroupEl = group;
  }

  // ===== Tool Card =====
  function createToolCard(block) {
    var card = document.createElement('div');
    card.className = 'chat-tool-card';

    var header = document.createElement('div');
    header.className = 'chat-tool-card__header';

    var icon = document.createElement('span');
    icon.className = 'chat-tool-card__icon';
    icon.textContent = block.type === 'tool_result' ? '📋' : '🔧';
    header.appendChild(icon);

    var name = document.createElement('span');
    name.className = 'chat-tool-card__name';
    name.textContent = block.name || (block.type === 'tool_result' ? '工具结果' : '工具调用');
    header.appendChild(name);

    var expandHint = document.createElement('span');
    expandHint.className = 'chat-tool-card__expand';
    expandHint.textContent = '展开';
    header.appendChild(expandHint);

    card.appendChild(header);

    // Detail line: first arg or summary
    if (block.input) {
      var keys = Object.keys(block.input);
      if (keys.length > 0) {
        var detail = document.createElement('div');
        detail.className = 'chat-tool-card__detail';
        detail.textContent = keys[0] + ': ' + String(block.input[keys[0]]).slice(0, 80);
        card.appendChild(detail);
      }
    }

    // Output section
    var output = document.createElement('div');
    output.className = 'chat-tool-card__output';

    if (block.type === 'tool_result') {
      var content = '';
      if (typeof block.content === 'string') {
        content = block.content;
      } else if (Array.isArray(block.content)) {
        content = block.content.map(function(c) { return c.text || ''; }).join('\n');
      } else if (block.content) {
        try { content = JSON.stringify(block.content, null, 2); } catch { content = String(block.content); }
      }

      if (content.length > 80) {
        output.className += ' truncated';
        output.textContent = content.slice(0, 100) + '...';
        card.classList.add('has-long-output');
        card.addEventListener('click', function() {
          card.classList.toggle('expanded');
          var isExp = card.classList.contains('expanded');
          expandHint.textContent = isExp ? '收起' : '展开';
          var out = card.querySelector('.chat-tool-card__output');
          if (out) {
            out.className = 'chat-tool-card__output' + (isExp ? '' : ' truncated');
            if (isExp) { out.textContent = content; }
            else { out.textContent = content.slice(0, 100) + '...'; }
          }
        });
      } else {
        output.className += ' inline';
        output.textContent = content || '(空)';
      }

      var status = document.createElement('div');
      status.className = 'chat-tool-card__status done';
      status.textContent = block.isError ? '✗ 执行失败' : '✓ 已完成';
      card.appendChild(status);
    } else {
      // tool_use — show args as JSON
      var argsStr = '';
      try { argsStr = JSON.stringify(block.input, null, 2); } catch { argsStr = String(block.input || ''); }
      if (argsStr.length > 80) {
        output.className += ' truncated';
        output.textContent = argsStr.slice(0, 100) + '...';
        card.classList.add('has-long-output');
        card.addEventListener('click', function() {
          card.classList.toggle('expanded');
          var isExp = card.classList.contains('expanded');
          expandHint.textContent = isExp ? '收起' : '展开';
          var out = card.querySelector('.chat-tool-card__output');
          if (out) {
            out.className = 'chat-tool-card__output' + (isExp ? '' : ' truncated');
            if (isExp) out.textContent = argsStr;
            else out.textContent = argsStr.slice(0, 100) + '...';
          }
        });
      } else {
        output.className += ' inline';
        output.textContent = argsStr || '(无参数)';
      }

      var status = document.createElement('div');
      status.className = 'chat-tool-card__status pending';
      status.textContent = '⏳ 等待执行...';
      card.appendChild(status);
    }

    card.appendChild(output);
    return card;
  }

  // ===== Bubble Builder =====
  function buildBubbles(type, content) {
    var bubbles = [];

    if (typeof content === 'string') {
      var bubble = document.createElement('div');
      bubble.className = 'chat-bubble fade-in';
      bubble.textContent = content;
      bubbles.push(bubble);
    } else if (Array.isArray(content)) {
      var textParts = [];
      var hasToolBlock = false;

      for (var i = 0; i < content.length; i++) {
        var block = content[i];
        if (block.type === 'text') {
          textParts.push(block.text || '');
        } else if (block.type === 'tool_use' || block.type === 'tool_result') {
          hasToolBlock = true;
          // Flush accumulated text first
          if (textParts.length > 0) {
            var bubble = document.createElement('div');
            bubble.className = 'chat-bubble fade-in';
            bubble.textContent = textParts.join('\n');
            bubbles.push(bubble);
            textParts = [];
          }
          // Create tool card bubble
          var toolBubble = document.createElement('div');
          toolBubble.className = 'chat-bubble fade-in';
          toolBubble.style.padding = '0';
          toolBubble.style.background = 'none';
          toolBubble.style.border = 'none';
          toolBubble.appendChild(createToolCard(block));
          bubbles.push(toolBubble);
        }
      }

      // Remaining text
      if (textParts.length > 0) {
        var bubble = document.createElement('div');
        bubble.className = 'chat-bubble fade-in';
        bubble.textContent = textParts.join('\n');
        bubbles.push(bubble);
      }
    }

    return bubbles;
  }

  // ===== Add Message (with grouping) =====
  function addMessage(type, label, content) {
    var normRole = type === 'assistant' ? 'assistant'
                 : type === 'user' ? 'user'
                 : type === 'system' ? 'system'
                 : 'tool';

    // Open new group if role changed or no group exists
    if (normRole !== lastRole || !currentGroupEl) {
      finalizeCurrentGroup(normRole);
      openNewGroup(normRole);
      lastRole = normRole;
    }

    // Build bubbles
    var bubbles = buildBubbles(type, content);

    // Find messages container within current group (last child before footer)
    var msgContainer = currentGroupEl.querySelector('.chat-group-messages');

    // Insert bubbles before the footer
    var footer = msgContainer.querySelector('.chat-group-footer');
    for (var i = 0; i < bubbles.length; i++) {
      msgContainer.insertBefore(bubbles[i], footer);
    }

    // Update timestamp
    var tsEl = msgContainer.querySelector('.chat-group-footer__time');
    if (tsEl) tsEl.textContent = formatTime(new Date());

    // Streaming animation: if assistant and awaitingResponse, pulse the last bubble
    if (normRole === 'assistant' && awaitingResponse) {
      if (streamingTimer) clearTimeout(streamingTimer);
      // Mark all bubbles in this group as streaming
      var bubblesInGroup = msgContainer.querySelectorAll('.chat-bubble.streaming');
      for (var j = 0; j < bubblesInGroup.length; j++) {
        bubblesInGroup[j].classList.remove('streaming');
      }
      // Only mark the new bubbles
      for (var k = 0; k < bubbles.length; k++) {
        bubbles[k].classList.add('streaming');
      }
      streamingTimer = setTimeout(function() {
        var allBubbles = msgContainer.querySelectorAll('.chat-bubble.streaming');
        for (var l = 0; l < allBubbles.length; l++) {
          allBubbles[l].classList.remove('streaming');
        }
      }, 2000);
    }

    // Remove fade-in class after animation
    for (var m = 0; m < bubbles.length; m++) {
      bubbles[m].addEventListener('animationend', function() {
        this.classList.remove('fade-in');
      });
    }

    checkScroll();
  }

  // ===== Add Result (divider) =====
  function addResult(cls, label) {
    var div = document.createElement('div');
    div.className = 'chat-divider';
    div.innerHTML = '<span class="chat-divider__line"></span>'
      + '<span class="chat-divider__label ' + cls + '">' + label + '</span>'
      + '<span class="chat-divider__line"></span>';
    messagesEl.appendChild(div);
    lastRole = null;
    checkScroll();
  }

  // ===== Reading Indicator =====
  function showReadingIndicator() {
    if (readingIndicatorEl) return;
    var group = document.createElement('div');
    group.className = 'chat-group assistant';

    var avatar = document.createElement('div');
    avatar.className = 'chat-avatar assistant';
    avatar.textContent = 'AI';

    var col = document.createElement('div');
    col.className = 'chat-group-messages';

    var dots = document.createElement('div');
    dots.className = 'chat-reading';
    dots.innerHTML = '<span class="chat-reading__dot"></span>'
      + '<span class="chat-reading__dot"></span>'
      + '<span class="chat-reading__dot"></span>';

    col.appendChild(dots);
    group.appendChild(avatar);
    group.appendChild(col);

    messagesEl.appendChild(group);
    readingIndicatorEl = group;
    checkScroll();
  }

  function hideReadingIndicator() {
    if (readingIndicatorEl) {
      readingIndicatorEl.remove();
      readingIndicatorEl = null;
    }
    awaitingResponse = false;
  }

  // ===== Auto-scroll =====
  let userScrolledUp = false;

  function checkScroll() {
    const threshold = 100;
    const diff = messagesEl.scrollHeight - messagesEl.clientHeight - messagesEl.scrollTop;
    userScrolledUp = diff > threshold;
    newMsgPill.style.display = userScrolledUp ? 'inline-flex' : 'none';
    if (!userScrolledUp) messagesEl.scrollTop = messagesEl.scrollHeight;
  }

  function scrollToBottom() {
    messagesEl.scrollTop = messagesEl.scrollHeight;
    userScrolledUp = false;
    newMsgPill.style.display = 'none';
  }
  window.scrollToBottom = scrollToBottom;

  messagesEl.addEventListener('scroll', checkScroll);

  // ===== Send message =====
  function sendMessage() {
    const text = inputEl.value.trim();
    if (!text || !connected) return;
    ws.send(JSON.stringify({ type: 'event', event: 'chat', payload: { text: text } }));
    inputEl.value = '';
    adjustTextareaHeight();
    awaitingResponse = true;
    showReadingIndicator();
  }

  function adjustTextareaHeight() {
    inputEl.style.height = 'auto';
    var newH = Math.min(Math.max(inputEl.scrollHeight, 40), 150);
    inputEl.style.height = newH + 'px';
  }

  sendBtn.addEventListener('click', sendMessage);
  inputEl.addEventListener('keydown', function(e) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  });

  inputEl.addEventListener('input', function() {
    adjustTextareaHeight();
    sendBtn.disabled = !connected || !inputEl.value.trim();
  });

  // ===== Init =====
  connect();
  inputEl.focus();
})();
</script>
</body>
</html>`;
}

function escHtml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
          .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
}
