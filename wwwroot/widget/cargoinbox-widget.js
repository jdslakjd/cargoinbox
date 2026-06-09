(function () {
  'use strict';

  var script = document.currentScript;
  if (!script) return;

  var widgetKey = script.getAttribute('data-widget-key');
  if (!widgetKey) {
    console.error('[CargoInbox] data-widget-key is required');
    return;
  }

  var apiBase = script.src.replace(/\/widget\/cargoinbox-widget\.js.*$/, '');
  var primaryColor = '#4f46e5';
  var position = 'right';
  var welcomeMessage = 'Hi! How can we help you today?';
  var widgetName = 'Chat';
  var sessionToken = null;
  var pollTimer = null;
  var lastPollAt = null;
  var visitorId = localStorage.getItem('ci_visitor_id');
  if (!visitorId) {
    visitorId = 'v_' + Math.random().toString(36).slice(2) + Date.now().toString(36);
    localStorage.setItem('ci_visitor_id', visitorId);
  }

  var root = document.createElement('div');
  root.id = 'cargoinbox-widget-root';
  document.body.appendChild(root);

  var styles = document.createElement('style');
  styles.textContent = [
    '#cargoinbox-widget-root{font-family:system-ui,-apple-system,sans-serif;font-size:14px;z-index:999999}',
    '#ci-launcher{position:fixed;bottom:24px;width:56px;height:56px;border-radius:50%;border:none;cursor:pointer;box-shadow:0 8px 24px rgba(0,0,0,.18);color:#fff;font-size:22px;display:flex;align-items:center;justify-content:center}',
    '#ci-panel{position:fixed;bottom:92px;width:360px;max-width:calc(100vw - 32px);height:480px;max-height:calc(100vh - 120px);background:#fff;border-radius:16px;box-shadow:0 12px 40px rgba(0,0,0,.2);display:none;flex-direction:column;overflow:hidden}',
    '#ci-panel.open{display:flex}',
    '#ci-header{padding:16px;color:#fff;font-weight:600}',
    '#ci-messages{flex:1;overflow-y:auto;padding:12px;background:#f8fafc}',
    '.ci-msg{max-width:85%;margin-bottom:8px;padding:8px 12px;border-radius:12px;line-height:1.4;word-break:break-word}',
    '.ci-msg.visitor{background:#e2e8f0;color:#1e293b;margin-left:auto;border-bottom-right-radius:4px}',
    '.ci-msg.agent{background:#fff;border:1px solid #e2e8f0;color:#1e293b;border-bottom-left-radius:4px}',
    '.ci-meta{font-size:10px;color:#94a3b8;margin-bottom:2px}',
    '#ci-footer{display:flex;gap:8px;padding:12px;border-top:1px solid #e2e8f0;background:#fff}',
    '#ci-input{flex:1;border:1px solid #cbd5e1;border-radius:8px;padding:8px 10px;resize:none;height:40px;font:inherit}',
    '#ci-send{border:none;border-radius:8px;color:#fff;padding:0 14px;cursor:pointer;font-weight:600}'
  ].join('');
  document.head.appendChild(styles);

  var launcher = document.createElement('button');
  launcher.id = 'ci-launcher';
  launcher.innerHTML = '💬';
  launcher.title = 'Chat with us';

  var panel = document.createElement('div');
  panel.id = 'ci-panel';
  panel.innerHTML =
    '<div id="ci-header"></div>' +
    '<div id="ci-messages"></div>' +
    '<div id="ci-footer">' +
    '<textarea id="ci-input" placeholder="Type a message..." rows="1"></textarea>' +
    '<button id="ci-send">Send</button>' +
    '</div>';

  root.appendChild(launcher);
  root.appendChild(panel);

  var header = panel.querySelector('#ci-header');
  var messagesEl = panel.querySelector('#ci-messages');
  var input = panel.querySelector('#ci-input');
  var sendBtn = panel.querySelector('#ci-send');

  function applyPosition() {
    var edge = position === 'left' ? 'left:24px' : 'right:24px';
    launcher.style.cssText = edge + ';background:' + primaryColor;
    panel.style.cssText = edge + ';background:#fff';
    header.style.background = primaryColor;
    sendBtn.style.background = primaryColor;
  }

  function appendMessage(text, isAgent, agentName) {
    var wrap = document.createElement('div');
    if (isAgent && agentName) {
      var meta = document.createElement('div');
      meta.className = 'ci-meta';
      meta.textContent = agentName;
      wrap.appendChild(meta);
    }
    var bubble = document.createElement('div');
    bubble.className = 'ci-msg ' + (isAgent ? 'agent' : 'visitor');
    bubble.textContent = text;
    wrap.appendChild(bubble);
    messagesEl.appendChild(wrap);
    messagesEl.scrollTop = messagesEl.scrollHeight;
  }

  function showWelcome() {
    messagesEl.innerHTML = '';
    appendMessage(welcomeMessage, true, widgetName);
  }

  function api(path, options) {
    return fetch(apiBase + path, options).then(function (r) {
      if (!r.ok) throw new Error('Request failed');
      return r.json();
    });
  }

  function startSession() {
    return api('/api/livechat/widget/' + encodeURIComponent(widgetKey) + '/session', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ visitorId: visitorId })
    }).then(function (data) {
      sessionToken = data.sessionToken;
      widgetName = data.widget.name || 'Chat';
      welcomeMessage = data.widget.welcomeMessage || welcomeMessage;
      primaryColor = data.widget.primaryColor || primaryColor;
      position = data.widget.position || position;
      header.textContent = widgetName;
      applyPosition();
      showWelcome();
      (data.messages || []).forEach(function (m) {
        appendMessage(m.text, m.isAgent, m.agentName);
      });
      lastPollAt = new Date().toISOString();
      startPolling();
    });
  }

  function sendMessage(text) {
    if (!sessionToken || !text.trim()) return Promise.resolve();
    appendMessage(text.trim(), false);
    input.value = '';
    return api('/api/livechat/sessions/' + encodeURIComponent(sessionToken) + '/messages', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: text.trim() })
    });
  }

  function pollMessages() {
    if (!sessionToken) return;
    var url = '/api/livechat/sessions/' + encodeURIComponent(sessionToken) + '/messages';
    if (lastPollAt) url += '?since=' + encodeURIComponent(lastPollAt);
    api(url).then(function (res) {
      (res.data || []).forEach(function (m) {
        if (m.isAgent) appendMessage(m.text, true, m.agentName || 'Support');
      });
      if ((res.data || []).length) lastPollAt = new Date().toISOString();
    }).catch(function () {});
  }

  function startPolling() {
    if (pollTimer) clearInterval(pollTimer);
    pollTimer = setInterval(pollMessages, 3000);
  }

  launcher.addEventListener('click', function () {
    var open = panel.classList.toggle('open');
    if (open && !sessionToken) startSession().catch(function () {
      alert('Unable to start chat. Please try again later.');
    });
  });

  sendBtn.addEventListener('click', function () {
    sendMessage(input.value).catch(function () {});
  });

  input.addEventListener('keydown', function (e) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage(input.value).catch(function () {});
    }
  });

  api('/api/livechat/widget/' + encodeURIComponent(widgetKey))
    .then(function (cfg) {
      welcomeMessage = cfg.welcomeMessage || welcomeMessage;
      primaryColor = cfg.primaryColor || primaryColor;
      position = cfg.position || position;
      widgetName = cfg.name || widgetName;
      applyPosition();
    })
    .catch(function () {
      applyPosition();
    });
})();
