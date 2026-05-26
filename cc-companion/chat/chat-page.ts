/**
 * 聊天页面 HTML 生成 — 单文件自包含，内联 CSS + JS
 * 现代简约 UI 风格
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
<title>RimWorld Bridge Agent</title>
<style>
  /* ===== Design Tokens ===== */
  :root {
    --bg: #0b0d12;
    --card: #12151e;
    --surface: #181c28;
    --hover: #1e2332;
    --text: #d1d3da;
    --text-strong: #edf0f5;
    --muted: #636879;
    --muted-subtle: #424657;
    --border: #1e2230;
    --border-strong: #2a3040;
    --accent: #ff5c5c;
    --blue: #4a8cf7;
    --blue-bg: rgba(74,140,247,0.07);
    --blue-border: rgba(74,140,247,0.15);
    --green: #3ecf6e;
    --green-bg: rgba(62,207,110,0.06);
    --green-border: rgba(62,207,110,0.20);
    --amber: #f0a840;
    --amber-bg: rgba(240,168,64,0.06);
    --amber-border: rgba(240,168,64,0.20);
    --red: #f44b4b;
    --cyan: #3cc8c8;
    --mono: "Cascadia Code","Fira Code","JetBrains Mono","Consolas",monospace;
    --font: system-ui,-apple-system,"Microsoft YaHei","PingFang SC","Segoe UI",sans-serif;
    --radius-sm: 6px;
    --radius: 10px;
    --radius-lg: 14px;
    color-scheme: dark;
  }

  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
  html, body { height: 100%; overflow: hidden; background: var(--bg); }
  body {
    font-family: var(--font); color: var(--text);
    font-size: 14px; line-height: 1.5;
    -webkit-font-smoothing: antialiased;
  }
  #app {
    display: flex; flex-direction: column;
    height: 100vh; max-width: 860px; margin: 0 auto; width: 100%;
  }

  /* Scrollbar */
  ::-webkit-scrollbar { width: 5px; }
  ::-webkit-scrollbar-track { background: transparent; }
  ::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 3px; }
  ::-webkit-scrollbar-thumb:hover { background: var(--muted-subtle); }

  /* ===== Header ===== */
  #header {
    flex-shrink: 0;
    padding: 14px 20px 10px;
    border-bottom: 1px solid var(--border);
    background: var(--bg);
  }
  .header-row {
    display: flex; align-items: center; gap: 10px;
  }
  #status-dot {
    width: 7px; height: 7px; border-radius: 50%; flex-shrink: 0;
    transition: background 0.3s, box-shadow 0.3s;
  }
  #status-dot.connected { background: var(--green); box-shadow: 0 0 5px rgba(62,207,110,0.4); }
  #status-dot.connecting { background: var(--amber); animation: dot-blink 1.2s ease-in-out infinite; }
  #status-dot.disconnected { background: var(--red); }
  @keyframes dot-blink { 0%,100% { opacity: 0.3; } 50% { opacity: 1; } }

  .header-title {
    font-size: 14px; font-weight: 600; color: var(--text-strong);
    letter-spacing: -0.2px;
  }
  .header-spacer { flex: 1; }
  .header-colony {
    font-size: 12px; color: var(--muted); font-weight: 500;
    white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 200px;
  }
  #info-btn {
    background: none; border: 1px solid var(--border-strong); color: var(--muted);
    border-radius: 4px; width: 22px; height: 22px; cursor: pointer;
    font-size: 11px; font-weight: 600; font-family: var(--mono);
    display: flex; align-items: center; justify-content: center;
    flex-shrink: 0; transition: all 0.15s;
  }
  #info-btn:hover { background: var(--surface); color: var(--text); border-color: var(--muted-subtle); }

  /* Stats */
  .header-stats {
    display: flex; align-items: center; gap: 6px;
    margin-top: 8px; font-size: 12px;
  }
  .header-stats .stat {
    display: inline-flex; align-items: center; gap: 3px;
    padding: 2px 8px; border-radius: 4px;
    background: var(--surface); color: var(--muted);
  }
  .header-stats .stat-label { color: var(--muted); }
  .header-stats .stat-value { color: var(--text); font-weight: 500; font-variant-numeric: tabular-nums; }
  .header-stats .stat-value.danger { color: var(--red); }
  .header-stats .stat-value.warning { color: var(--amber); }
  .header-stats .stat-value.ok { color: var(--green); }

  /* ===== Info Overlay ===== */
  #info-overlay {
    display: none; position: fixed; inset: 0;
    z-index: 100; background: rgba(0,0,0,0.55);
    backdrop-filter: blur(2px);
  }
  #info-panel {
    position: absolute; top: 50%; left: 50%; transform: translate(-50%,-50%);
    background: var(--card); border: 1px solid var(--border-strong);
    border-radius: var(--radius); padding: 18px 20px;
    box-shadow: 0 8px 32px rgba(0,0,0,0.5);
    width: 380px; max-width: 90vw;
  }
  #info-close {
    float: right; background: none; border: none; color: var(--muted);
    cursor: pointer; font-size: 18px; line-height: 1; padding: 0 2px;
  }
  #info-close:hover { color: var(--text); }
  #info-panel h2 {
    font-size: 13px; font-weight: 600; color: var(--text-strong);
    margin-bottom: 12px; letter-spacing: -0.2px;
  }
  .info-table { width: 100%; border-collapse: collapse; font-size: 12px; }
  .info-table th, .info-table td {
    text-align: left; padding: 5px 0; border-bottom: 1px solid var(--border);
  }
  .info-table th { color: var(--muted); width: 68px; font-weight: 400; }
  .info-table td { color: var(--text); word-break: break-all; font-family: var(--mono); font-size: 11px; }

  /* ===== Messages ===== */
  #messages {
    flex: 1; overflow-y: auto; padding: 14px 20px;
    display: flex; flex-direction: column; gap: 10px;
    scroll-behavior: smooth;
  }

  /* Base message */
  .msg {
    animation: msg-in 200ms ease-out both;
    position: relative;
  }
  @keyframes msg-in {
    from { opacity: 0; transform: translateY(6px); }
    to { opacity: 1; transform: translateY(0); }
  }
  .msg-label {
    font-size: 10px; font-weight: 600; text-transform: uppercase;
    letter-spacing: 0.6px; margin-bottom: 4px;
    user-select: none;
  }

  /* --- USER --- */
  .msg-user {
    align-self: flex-end; max-width: 72%;
  }
  .msg-user .msg-label { color: var(--blue); text-align: right; }
  .msg-user .msg-body {
    background: var(--blue-bg);
    border: 1px solid var(--blue-border);
    border-radius: var(--radius-lg) 4px var(--radius-lg) var(--radius-lg);
    padding: 10px 14px;
    font-size: 14px; line-height: 1.6;
    white-space: pre-wrap; word-wrap: break-word;
    color: var(--text);
  }

  /* --- AGENT --- */
  .msg-agent {
    align-self: flex-start; max-width: 88%;
  }
  .msg-agent .msg-label { color: var(--green); padding-left: 12px; }
  .msg-agent .msg-body {
    background: var(--card);
    border: 1px solid var(--border-strong);
    border-left: 3px solid var(--green-border);
    border-radius: 4px var(--radius) var(--radius) 4px;
    padding: 10px 14px;
    font-size: 14px; line-height: 1.65;
    white-space: pre-wrap; word-wrap: break-word;
    color: var(--text);
  }

  /* --- TOOL --- */
  .msg-tool {
    align-self: stretch;
  }
  .msg-tool .msg-header {
    display: flex; align-items: center; gap: 8px;
    padding: 7px 12px;
    background: var(--card);
    border: 1px solid var(--border-strong);
    border-left: 3px solid var(--amber-border);
    border-radius: var(--radius-sm) var(--radius-sm) 0 0;
    font-size: 12px;
  }
  .msg-tool .msg-header .msg-label {
    color: var(--amber); margin-bottom: 0; font-size: 10px;
  }
  .msg-tool .msg-header .tool-name {
    font-family: var(--mono); font-size: 12px; font-weight: 500; color: var(--text);
    flex: 1;
  }
  .msg-tool .msg-header .tool-timing {
    font-family: var(--mono); font-size: 11px; color: var(--green);
    white-space: nowrap;
  }
  .msg-tool .msg-header .tool-timing.error { color: var(--red); }
  .msg-tool .msg-body {
    background: var(--card);
    border: 1px solid var(--border-strong);
    border-top: none;
    border-left: 3px solid var(--amber-border);
    border-radius: 0 0 var(--radius-sm) var(--radius-sm);
    padding: 8px 12px;
    font-family: var(--mono); font-size: 12px; line-height: 1.55;
    white-space: pre-wrap; word-wrap: break-word;
    color: var(--text);
  }

  /* Tool expand/collapse */
  .tool-output-wrap {
    max-height: 200px; overflow: hidden; position: relative;
    transition: max-height 0.25s ease;
  }
  .tool-output-wrap.expanded { max-height: none; }
  .tool-output-wrap.truncated::after {
    content: ""; position: absolute; bottom: 0; left: 0; right: 0;
    height: 32px;
    background: linear-gradient(to bottom, transparent, var(--card));
    pointer-events: none;
  }
  .tool-expand-btn {
    display: block; width: 100%; padding: 4px;
    background: var(--surface); border: none; border-top: 1px solid var(--border-strong);
    color: var(--muted); font-size: 11px; cursor: pointer;
    font-family: var(--font); transition: all 0.15s;
    border-radius: 0 0 var(--radius-sm) var(--radius-sm);
  }
  .tool-expand-btn:hover { color: var(--text); background: var(--hover); }

  /* --- Result divider --- */
  .msg-result {
    display: flex; align-items: center; gap: 10px;
    margin: 0 8px;
  }
  .msg-result .result-line { flex: 1; height: 1px; background: var(--border-strong); }
  .msg-result .result-label {
    font-size: 11px; font-weight: 500; padding: 2px 10px;
    border-radius: 4px; border: 1px solid var(--border-strong);
    white-space: nowrap; user-select: none;
    font-family: var(--mono);
  }
  .msg-result .result-label.ok { color: var(--green); border-color: rgba(62,207,110,0.25); }
  .msg-result .result-label.error { color: var(--red); border-color: rgba(244,75,75,0.25); }
  .msg-result .result-label.warn { color: var(--amber); border-color: rgba(240,168,64,0.25); }

  /* --- Typing indicator --- */
  .typing-indicator {
    align-self: flex-start; display: flex; align-items: center; gap: 4px;
    padding: 10px 14px; margin-left: 4px;
  }
  .typing-dot {
    width: 5px; height: 5px; border-radius: 50%;
    background: var(--muted);
    animation: typing-bounce 1.2s ease-in-out infinite;
  }
  .typing-dot:nth-child(2) { animation-delay: 0.15s; }
  .typing-dot:nth-child(3) { animation-delay: 0.3s; }
  @keyframes typing-bounce {
    0%,60%,100% { opacity: 0.2; transform: translateY(0); }
    30% { opacity: 1; transform: translateY(-3px); }
  }

  /* ===== New Messages Pill ===== */
  #new-msg-pill {
    display: none; align-self: center;
    padding: 5px 14px; font-size: 11px; cursor: pointer;
    color: var(--muted); background: var(--card);
    border: 1px solid var(--border-strong); border-radius: 20px;
    margin: 2px auto; z-index: 10;
    font-weight: 500; transition: all 0.15s;
    box-shadow: 0 2px 8px rgba(0,0,0,0.3);
  }
  #new-msg-pill:hover { border-color: var(--muted-subtle); color: var(--text); }

  /* ===== Compose ===== */
  .compose {
    flex-shrink: 0; display: flex; align-items: center; gap: 8px;
    padding: 10px 20px; background: var(--bg);
    border-top: 1px solid var(--border);
  }
  .compose .prompt {
    font-family: var(--mono); font-size: 15px; color: var(--muted);
    user-select: none; flex-shrink: 0;
  }
  #chat-input {
    flex: 1; background: var(--surface); border: 1px solid var(--border-strong);
    border-radius: 8px; outline: none;
    color: var(--text); font-family: var(--font); font-size: 14px;
    line-height: 1.5; padding: 7px 12px;
    min-height: 36px; max-height: 130px; resize: none;
    transition: border-color 0.15s;
  }
  #chat-input:focus { border-color: var(--muted-subtle); }
  #chat-input::placeholder { color: var(--muted); }
  #send-btn {
    background: var(--surface); border: 1px solid var(--border-strong);
    border-radius: 8px; color: var(--muted); cursor: pointer;
    padding: 7px 14px; font-size: 12px; font-weight: 500;
    transition: all 0.15s; flex-shrink: 0;
  }
  #send-btn:hover:not(:disabled) {
    background: var(--blue); border-color: var(--blue); color: #fff;
  }
  #send-btn:disabled { opacity: 0.3; cursor: not-allowed; }
</style>
</head>
<body>
<div id="app">
  <!-- Header -->
  <div id="header">
    <div class="header-row">
      <span id="status-dot" class="disconnected"></span>
      <span class="header-title">RimWorld Bridge Agent</span>
      <span class="header-spacer"></span>
      <span class="header-colony" id="colony-name">--</span>
      <button id="info-btn" title="SDK 信息">i</button>
    </div>
    <div class="header-stats" id="header-stats" style="display:none">
      <span class="stat"><span class="stat-label">Pawns</span> <span class="stat-value" id="stat-pawns">--</span></span>
      <span class="stat"><span class="stat-label">Mood</span> <span class="stat-value" id="stat-mood">--</span></span>
      <span class="stat"><span class="stat-label">Food</span> <span class="stat-value" id="stat-food">--</span></span>
    </div>
  </div>

  <!-- Info Overlay -->
  <div id="info-overlay">
    <div id="info-panel">
      <button id="info-close">&times;</button>
      <h2>SDK 信息</h2>
      <table class="info-table">
        <tr><th>Model</th><td><span id="info-model">${modelName}</span></td></tr>
        <tr><th>Project</th><td>${projectPath}</td></tr>
        <tr><th>Session</th><td><span id="info-session">-</span></td></tr>
        <tr><th>Version</th><td><span id="info-version">-</span></td></tr>
        <tr><th>Permission</th><td><span id="info-permission">-</span></td></tr>
      </table>
    </div>
  </div>

  <div id="messages"></div>
  <button id="new-msg-pill" onclick="scrollToBottom()">↓ 回到底部</button>

  <!-- Compose -->
  <div class="compose">
    <span class="prompt">&gt;</span>
    <textarea id="chat-input" placeholder="输入消息..." rows="1"></textarea>
    <button id="send-btn" disabled>Send</button>
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
  const colonyNameEl = document.getElementById('colony-name');
  const newMsgPill = document.getElementById('new-msg-pill');
  const headerStats = document.getElementById('header-stats');
  const statPawns = document.getElementById('stat-pawns');
  const statMood = document.getElementById('stat-mood');
  const statFood = document.getElementById('stat-food');

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
  var toolStartTimes = {};
  var readingEl = null;

  // ===== WebSocket =====
  function connect() {
    if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) return;
    setStatus('connecting');
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
      try { const msg = JSON.parse(event.data); handleMessage(msg); } catch {}
    };

    ws.onclose = () => {
      connected = false;
      setStatus('disconnected');
      sendBtn.disabled = true;
      hideReading();
      scheduleReconnect();
    };

    ws.onerror = () => { if (ws) ws.close(); };
  }

  function scheduleReconnect() {
    if (reconnectTimer) return;
    reconnectTimer = setTimeout(() => {
      reconnectTimer = null;
      reconnectDelay = Math.min(reconnectDelay * 1.5, RECONNECT_MAX);
      connect();
    }, reconnectDelay);
  }

  function setStatus(state) {
    statusDot.className = state;
  }

  // ===== Colony Stats =====
  function updateColonyStats(data) {
    if (data.colonyName) {
      colonyNameEl.textContent = data.colonyName;
    }
    if (data.colonistCount !== undefined) {
      statPawns.textContent = data.colonistCount;
    }
    if (data.avgMood !== undefined) {
      var mood = Number(data.avgMood);
      statMood.textContent = mood + '%';
      statMood.className = 'stat-value' + (mood < 30 ? ' danger' : mood < 50 ? ' warning' : ' ok');
    }
    if (data.foodDays !== undefined) {
      var fd = Number(data.foodDays);
      statFood.textContent = fd + 'd';
      statFood.className = 'stat-value' + (fd < 1 ? ' danger' : fd < 3 ? ' warning' : ' ok');
    }
    headerStats.style.display = 'flex';
  }

  // ===== Message handling =====
  function handleMessage(msg) {
    switch (msg.type) {
      case 'hello-ok':
        connected = true;
        setStatus('connected');
        sendBtn.disabled = false;
        hideReading();
        break;

      case 'colony-stats':
        updateColonyStats(msg);
        break;

      case 'assistant':
      case 'user': {
        const isAssistant = msg.type === 'assistant';
        const content = msg.message?.content;
        if (!content) return;
        if (isAssistant) hideReading();
        addMessage(isAssistant, content, msg.agent_type);
        break;
      }

      case 'system': {
        if (msg.subtype === 'init') updateSdkInfo(msg);
        break;
      }

      case 'result': {
        const sub = msg.subtype || 'unknown';
        const label = sub === 'success' ? '✓ Done'
                     : sub === 'aborted' ? '⏹ Aborted'
                     : '✗ Failed';
        const cls = sub === 'success' ? 'ok' : sub === 'aborted' ? 'warn' : 'error';
        hideReading();
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
    el = document.getElementById('info-permission'); if (el) el.textContent = msg.permissionMode || '?';
  }

  // ===== Panel builders =====
  function makeUserPanel(text) {
    var panel = document.createElement('div');
    panel.className = 'msg msg-user';

    var label = document.createElement('div');
    label.className = 'msg-label';
    label.textContent = 'USER';
    panel.appendChild(label);

    var body = document.createElement('div');
    body.className = 'msg-body';
    body.textContent = text;
    panel.appendChild(body);

    messagesEl.appendChild(panel);
    return panel;
  }

  function makeAgentPanel(text, agentType) {
    var panel = document.createElement('div');
    panel.className = 'msg msg-agent';
    if (agentType) panel.classList.add('sub-agent');

    var label = document.createElement('div');
    label.className = 'msg-label';
    label.textContent = agentType ? agentType.toUpperCase() : 'AGENT';
    panel.appendChild(label);

    var body = document.createElement('div');
    body.className = 'msg-body';
    body.textContent = text;
    panel.appendChild(body);

    messagesEl.appendChild(panel);
    return panel;
  }

  function makeToolUsePanel(name, input) {
    var panel = document.createElement('div');
    panel.className = 'msg msg-tool';

    var header = document.createElement('div');
    header.className = 'msg-header';

    var hlabel = document.createElement('span');
    hlabel.className = 'msg-label';
    hlabel.textContent = 'TOOL';
    header.appendChild(hlabel);

    var hname = document.createElement('span');
    hname.className = 'tool-name';
    hname.textContent = name || '?';
    header.appendChild(hname);

    panel.appendChild(header);

    if (input) {
      var body = document.createElement('div');
      body.className = 'msg-body';
      var argsStr = '';
      try { argsStr = JSON.stringify(input, null, 2); } catch(e) { argsStr = String(input || ''); }
      body.textContent = argsStr || '(no args)';
      panel.appendChild(body);
    }

    messagesEl.appendChild(panel);
    return panel;
  }

  function makeToolResultPanel(block, timing) {
    var panel = document.createElement('div');
    panel.className = 'msg msg-tool';

    var header = document.createElement('div');
    header.className = 'msg-header';

    var hlabel = document.createElement('span');
    hlabel.className = 'msg-label';
    hlabel.textContent = 'TOOL';
    header.appendChild(hlabel);

    var hname = document.createElement('span');
    hname.className = 'tool-name';
    hname.textContent = block.name || '?';
    header.appendChild(hname);

    if (timing !== undefined && timing !== null) {
      var htime = document.createElement('span');
      htime.className = 'tool-timing' + (block.isError ? ' error' : '');
      htime.textContent = (block.isError ? '✗ ' : '✓ ') + timing + 'ms';
      header.appendChild(htime);
    }

    panel.appendChild(header);

    // Extract content
    var content = '';
    if (typeof block.content === 'string') content = block.content;
    else if (Array.isArray(block.content)) content = block.content.map(function(c) { return c.text || ''; }).join('\\n');
    else if (block.content) { try { content = JSON.stringify(block.content, null, 2); } catch(e) { content = String(block.content); } }

    var body = document.createElement('div');
    body.className = 'msg-body';

    if (content) {
      var wrap = document.createElement('div');
      wrap.className = 'tool-output-wrap';

      if (block.isError) {
        wrap.style.color = 'var(--red)';
        wrap.textContent = content;
      } else if (content.length > 180) {
        wrap.classList.add('truncated');
        wrap.textContent = content;
        wrap.dataset.full = content;

        var btn = document.createElement('button');
        btn.className = 'tool-expand-btn';
        btn.textContent = '展开全部';
        btn.addEventListener('click', function() {
          var isExp = wrap.classList.toggle('expanded');
          wrap.classList.toggle('truncated', !isExp);
          btn.textContent = isExp ? '收起' : '展开全部';
          if (isExp) { wrap.textContent = wrap.dataset.full; }
          checkScroll();
        });
        panel.appendChild(btn);
      } else {
        wrap.textContent = content;
      }

      body.appendChild(wrap);
    } else {
      body.textContent = '(empty)';
    }

    panel.appendChild(body);
    messagesEl.appendChild(panel);
    return panel;
  }

  // ===== Add Message =====
  var lastAgentBody = null;

  function addMessage(isAssistant, content, agentType) {
    if (typeof content === 'string') {
      if (isAssistant && !agentType && lastAgentBody) {
        // Append to existing agent panel (streaming)
        lastAgentBody.textContent += '\\n' + content;
      } else if (isAssistant) {
        var p = makeAgentPanel(content, agentType);
        lastAgentBody = p.querySelector('.msg-body');
      } else {
        makeUserPanel(content);
        lastAgentBody = null;
      }
    } else if (Array.isArray(content)) {
      for (var i = 0; i < content.length; i++) {
        var block = content[i];
        if (block.type === 'text') {
          // Consecutive text blocks merge into the same agent panel
          var lastMsg = messagesEl.lastElementChild;
          if (lastMsg && lastMsg.classList.contains('msg-agent') && !lastMsg.classList.contains('sub-agent') && lastAgentBody) {
            lastAgentBody.textContent += '\\n' + (block.text || '');
          } else {
            var p = makeAgentPanel(block.text || '', null);
            lastAgentBody = p.querySelector('.msg-body');
          }
        } else if (block.type === 'tool_use') {
          toolStartTimes[block.id] = Date.now();
          makeToolUsePanel(block.name, block.input);
          lastAgentBody = null;
        } else if (block.type === 'tool_result') {
          var elapsed = null;
          if (block.id && toolStartTimes[block.id] !== undefined) {
            elapsed = Date.now() - toolStartTimes[block.id];
            delete toolStartTimes[block.id];
          }
          makeToolResultPanel(block, elapsed);
          lastAgentBody = null;
        }
      }
    }
    checkScroll();
  }

  // ===== Result divider =====
  function addResult(cls, label) {
    var div = document.createElement('div');
    div.className = 'msg-result';
    div.innerHTML = '<span class="result-line"></span>'
      + '<span class="result-label ' + cls + '">' + label + '</span>'
      + '<span class="result-line"></span>';
    messagesEl.appendChild(div);
    checkScroll();
  }

  // ===== Typing indicator =====
  function showReading() {
    if (readingEl) return;
    readingEl = document.createElement('div');
    readingEl.className = 'typing-indicator';
    readingEl.innerHTML = '<span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span>';
    messagesEl.appendChild(readingEl);
    checkScroll();
  }

  function hideReading() {
    if (readingEl) { readingEl.remove(); readingEl = null; }
  }

  // ===== Auto-scroll =====
  var userScrolledUp = false;

  function checkScroll() {
    const threshold = 80;
    const diff = messagesEl.scrollHeight - messagesEl.clientHeight - messagesEl.scrollTop;
    userScrolledUp = diff > threshold;
    newMsgPill.style.display = userScrolledUp ? 'block' : 'none';
    if (!userScrolledUp) messagesEl.scrollTop = messagesEl.scrollHeight;
  }

  function scrollToBottom() {
    messagesEl.scrollTop = messagesEl.scrollHeight;
    userScrolledUp = false;
    newMsgPill.style.display = 'none';
  }
  window.scrollToBottom = scrollToBottom;
  messagesEl.addEventListener('scroll', checkScroll);

  // ===== Send =====
  function sendMessage() {
    const text = inputEl.value.trim();
    if (!text || !connected) return;
    ws.send(JSON.stringify({
      type: 'event', event: 'chat',
      payload: { text: text }
    }));
    inputEl.value = '';
    adjustHeight();
    showReading();
  }

  function adjustHeight() {
    inputEl.style.height = 'auto';
    var newH = Math.min(Math.max(inputEl.scrollHeight, 36), 130);
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
    adjustHeight();
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
