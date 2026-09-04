/**
 * Luxira Store Script Platform — runtime engine (layer 2)
 * ===========================================================================
 * Served by GET /seedscript/engine.js?scriptId=<id> (application/javascript,
 * no-store — agent P2) and run by the loader (App_Data/luxira-loader.user.js)
 * as a `blob:` object-URL <script> in PAGE context. WhatsApp's CSP forbids
 * `eval`/`new Function` but allows `blob:` in script-src — see
 * documentation/seed-script-loader.md for why. Consequence: this file runs
 * with full DOM/window access but NO GM_* APIs and no network privileges of
 * its own; the loader hands it the resolved definition JSON directly as an
 * argument, so it never needs to fetch anything itself (see the loader's
 * header comment for the bridge, kept available per contract §4 even though
 * this engine does not currently exercise it).
 *
 * This file contains NO store-specific content. Every category, message,
 * price, colour and icon it renders comes from the "runtime definition JSON"
 * (artifacts/store-scripts/contract.md §2) passed into __luxiraRender().
 *
 * Public surface — exactly these two globals (contract §4):
 *   window.__luxiraRender(definition)   build or rebuild the UI
 *   window.__luxiraTeardown()           leave the page exactly as found
 *
 * Send mechanics (chunked typing, ack polling/retry, per-platform composer
 * and send DOM) were extracted from two family-A reference scripts:
 *   - artifacts/store-scripts/68-Lotus_Blue.js  (WhatsApp)
 *   - artifacts/store-scripts/64-Lotus_Blue.js  (Meta / Business Suite)
 * See the WHATSAPP_SELECTORS / META_SELECTORS blocks below for exactly
 * which selector came from which file, and the P4 agent report for what
 * could not be verified without a browser.
 * ===========================================================================
 */
(function () {
  'use strict';

  // Guard against engine.js being injected twice into the same page without
  // an intervening teardown (e.g. a loader re-activation edge case). Tear
  // down any previous instance first so we never orphan its observers,
  // timers or DOM nodes — this is also what makes __luxiraRender safe to
  // call repeatedly on a revision bump (see doRender below).
  if (typeof window.__luxiraTeardown === 'function') {
    try { window.__luxiraTeardown(); }
    catch (e) { console.warn('[luxira-engine] cleanup of a prior instance failed', e); }
  }

  var ENGINE_VERSION = '1.0.0';
  var RUN_KEY = '__LUXIRA_ENGINE_ACTIVE__';
  var TWEMOJI_BASE = 'https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/72x72/';
  var SELF_HEAL_INTERVAL_MS = 1500;

  // Generic UI chrome text — search placeholder, "stop", "choose a
  // country"... This is app shell text, identical in spirit across every
  // family in the legacy scripts (e.g. "ابحث", "إيقاف الإرسال"), not
  // business content. All business content (category/message text) comes
  // exclusively from the definition JSON handed to __luxiraRender.
  var CHROME = {
    SEARCH_PLACEHOLDER: 'ابحث',
    STOP: 'إيقاف الإرسال',
    SEND_FAILED: 'فشل الإرسال',
    NOT_READY: 'افتح محادثة أولا',
    BACK: 'رجوع'
  };

  // Engine-default palette, used only when the store's `theme` dict does
  // not define the corresponding conventional token — GOLD / PROGRESS /
  // STOP are the token names contract.md's own §1 example uses, so they're
  // treated as generic semantic roles (accent / progress-fill / stop),
  // never as one store's brand colours. Actual hex values always come from
  // the JSON when present.
  var DEFAULT_THEME = {
    GOLD: '#2563eb',
    PROGRESS: '#16a34a',
    STOP: '#dc2626',
    STOP_HOVER: '#b91c1c'
  };

  // Tuned to read as an expert human typist (~80 WPM) rather than the legacy
  // scripts' "very_fast"/instant preset: small chunk bursts with a per-chunk
  // delay (D/C ~= 150ms per character), extra pauses at word/punctuation
  // boundaries, an occasional longer "thinking" pause every few chunks, and
  // a brief read-over pause (PRE) before Enter plus a natural gap (DELAY)
  // between consecutive messages. Overridable per key via definition.settings.
  var DEFAULT_SETTINGS = {
    TYPE_MIN: 450, TYPE_MAX: 750, TYPE_SPACE: 60, TYPE_PUNCT: 220,
    CHUNK_MIN: 3, CHUNK_MAX: 5, CHUNK_EVERY: 6, CHUNK_BONUS: 350,
    PRE: 350, DELAY: 1200, POST: 150,
    ACK_TIMEOUT: 2000, ACK_POLL: 200, ACK_RETRY: 3,
    INIT_GAP: 500, SCALE: 0.85
  };

  // ---------------------------------------------------------------------
  // Small utilities
  // ---------------------------------------------------------------------
  function sleep(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }
  function randInt(a, b) { return Math.floor(a + Math.random() * (b - a + 1)); }
  function clampPct(p) { return Math.max(0, Math.min(100, Number(p) || 0)); }

  // Generic whitespace/punctuation/mark cleanup only. The legacy scripts'
  // own `sanitize()` also runs a long table of store-specific Arabic typo
  // fixes (e.g. "الفاونديشن" spelling repairs) — that table is Lotus Blue
  // wording, not generic infrastructure, so it is deliberately NOT ported
  // here; carrying it into the generic engine would bake store content into
  // a file that must contain none. See the P4 report.
  function sanitizeGeneric(v) {
    var s = String(v == null ? '' : v).replace(/‏|‎/g, '');
    s = s.replace(/\s{2,}/g, ' ');
    s = s.replace(/\s+([،؛:!?؟…])/g, '$1');
    s = s.replace(/([،؛:!?؟…])\s*/g, '$1 ');
    return s.replace(/\s{2,}/g, ' ').trim();
  }

  // Accepts either already-hex-dash codepoints (contract's `flagHex` shape,
  // e.g. "1f1f1-1f1fe") or a literal emoji character, and returns the
  // twemoji filename stem either way.
  function toTwemojiHex(icon) {
    var s = String(icon || '').trim();
    if (!s) return '';
    if (/^[0-9a-fA-F]{1,8}(-[0-9a-fA-F]{1,8})*$/.test(s)) return s.toLowerCase();
    try {
      return Array.from(s).map(function (ch) { return ch.codePointAt(0).toString(16); }).join('-');
    } catch (e) { return ''; }
  }

  function el(tag, className, attrs) {
    var n = document.createElement(tag);
    if (className) n.className = className;
    if (attrs) {
      for (var k in attrs) {
        if (Object.prototype.hasOwnProperty.call(attrs, k)) n.setAttribute(k, attrs[k]);
      }
    }
    return n;
  }

  // ---------------------------------------------------------------------
  // Bridge client (contract §4) — this engine runs in page context via the
  // loader's blob: script and therefore has no GM_* APIs of its own. Any
  // luxira.org call it needs to make goes through the loader's postMessage
  // relay (App_Data/luxira-loader.user.js), which proxies it through
  // GM_xmlhttpRequest.
  //
  // The current render/send pipeline never calls this: the loader already
  // fetches manifest/engine.js/definition itself and passes the resolved
  // definition straight into __luxiraRender(), so there is nothing left for
  // the engine to fetch on its own today. This helper is kept — private,
  // not part of the two required globals — as the corrected reference
  // implementation for whenever that changes, since it is also where the
  // bug named in contract §4 actually lives: the previous pattern
  // (_scratch/tampermonkey/payload-examples.js) generated its correlation
  // `id` from `Date.now()`, which two requests fired in the same
  // millisecond would share. The loader's relay itself is id-agnostic (it
  // only echoes back whatever id it was given), so the fix has to be here,
  // on the id-generating caller side: a monotonic counter instead.
  // ---------------------------------------------------------------------
  var bridgeRequestCounter = 0;
  var bridgePending = Object.create(null);
  var bridgeListenerInstalled = false;

  function ensureBridgeListener() {
    if (bridgeListenerInstalled) return;
    bridgeListenerInstalled = true;
    // Tracked so teardown can remove it — this is one of the (currently
    // zero, since the helper is unused) document/window listeners the
    // engine would own once something calls luxiraBridgeRequest.
    var handler = function (e) {
      if (e.source !== window || !e.data || e.data.__luxira !== 'res') return;
      var pending = bridgePending[e.data.id];
      if (!pending) return;
      delete bridgePending[e.data.id];
      pending(e.data);
    };
    window.addEventListener('message', handler);
    if (state) state.listeners.push({ target: window, type: 'message', handler: handler });
  }

  function luxiraBridgeRequest(opts) {
    ensureBridgeListener();
    var id = ++bridgeRequestCounter; // monotonic — see the block comment above
    return new Promise(function (resolve) {
      bridgePending[id] = resolve;
      window.postMessage({
        __luxira: 'req',
        id: id,
        method: (opts && opts.method) || 'GET',
        url: opts && opts.url,
        headers: opts && opts.headers,
        body: opts && opts.body
      }, '*');
    });
  }

  // ---------------------------------------------------------------------
  // Engine state — everything a render creates lives here, so teardown has
  // one place to look. Rebuilt fresh on every __luxiraRender call.
  // ---------------------------------------------------------------------
  var state = null;

  function freshState(definition) {
    return {
      definition: definition,
      adapter: null,
      barEl: null,
      styleEl: null,
      observer: null,
      timers: [],            // { id, kind: 'interval' | 'timeout' }
      listeners: [],          // { target, type, handler } — document/window listeners only
      sendLock: false,
      activeToken: null
    };
  }

  function trackInterval(fn, ms) {
    var id = setInterval(fn, ms);
    if (state) state.timers.push({ id: id, kind: 'interval' });
    return id;
  }

  function trackTimeout(fn, ms) {
    var id = setTimeout(fn, ms);
    if (state) state.timers.push({ id: id, kind: 'timeout' });
    return id;
  }

  // ---------------------------------------------------------------------
  // DOM mechanics shared by both adapters — identical in both reference
  // scripts. execCommand still works in both host apps as of the reference
  // scripts' last production use; dispatching a synthetic `input` event
  // after each mutation is what makes the host page's own React state pick
  // up the change (contenteditable + React needs this nudge).
  // ---------------------------------------------------------------------
  function insertTextExec(target, text) {
    target.focus();
    document.execCommand('insertText', false, text);
    target.dispatchEvent(new InputEvent('input', { bubbles: true }));
  }

  function clearEditableExec(target) {
    target.focus();
    try {
      document.execCommand('selectAll', false, null);
      document.execCommand('delete', false, null);
    } catch (e) {
      target.textContent = '';
    }
    target.dispatchEvent(new InputEvent('input', { bubbles: true }));
  }

  function dispatchEnter(target) {
    var opts = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true };
    target.dispatchEvent(new KeyboardEvent('keydown', opts));
    target.dispatchEvent(new KeyboardEvent('keyup', opts));
  }

  // Chunked "human" typing: both reference scripts split the outgoing text
  // into randomly-sized chunks and pause between them (CHUNK_MIN/MAX,
  // TYPE_MIN/MAX/SPACE/PUNCT, CHUNK_EVERY/BONUS) — this exists because both
  // platforms are more likely to drop or garble a message typed in one
  // giant instantaneous insert. Most single messages are under CHUNK_MIN
  // (250) chars, so in practice this is usually one chunk.
  async function humanTypeInto(target, text, cfg, token) {
    target.focus();
    var i = 0, c = 0;
    while (i < text.length) {
      if (token.cancelled) return false;
      var size = randInt(Math.min(cfg.CHUNK_MIN, cfg.CHUNK_MAX), Math.max(cfg.CHUNK_MIN, cfg.CHUNK_MAX));
      var chunk = text.slice(i, i + size);
      insertTextExec(target, chunk);
      i += chunk.length;
      var d = randInt(cfg.TYPE_MIN, cfg.TYPE_MAX);
      if (/\s/.test(chunk)) d += cfg.TYPE_SPACE;
      if (/[,.!?؟…،؛:]/.test(chunk)) d += cfg.TYPE_PUNCT;
      c++;
      if (c % Math.max(1, cfg.CHUNK_EVERY) === 0) d += randInt(0, cfg.CHUNK_BONUS);
      if (d > 0) await sleep(d);
    }
    return !token.cancelled;
  }

  // ===========================================================================
  // WhatsApp adapter — selectors verified against
  // artifacts/store-scripts/68-Lotus_Blue.js
  // ===========================================================================
  var WHATSAPP_SELECTORS = {
    // Message composer (contenteditable div in the chat footer). WhatsApp
    // Web has renumbered its data-tab attributes across releases; each
    // entry below is a looser fallback than the one before it.
    COMPOSER: [
      'div[contenteditable="true"][data-tab="10"]',
      'div[contenteditable="true"][data-tab="1"]',
      'footer div[contenteditable="true"]',
      'div[contenteditable="true"][spellcheck="true"]',
      'div[contenteditable="true"]'
    ],
    // Send button, tried before falling back to a synthetic Enter keypress.
    SEND_BUTTON: [
      'button[data-tab="11"]',
      '[data-testid="send"]',
      'button[aria-label="Send"]',
      'button[aria-label="إرسال"]' // Arabic "Send"
    ],
    // Last-resort: the send icon's own closest <button>, for builds where
    // only the icon carries a stable attribute.
    SEND_ICON: 'span[data-icon="send"]'
  };

  function whatsappFindComposer() {
    for (var i = 0; i < WHATSAPP_SELECTORS.COMPOSER.length; i++) {
      var found = document.querySelector(WHATSAPP_SELECTORS.COMPOSER[i]);
      if (found) return found;
    }
    return null;
  }

  function whatsappFindSendButton() {
    for (var i = 0; i < WHATSAPP_SELECTORS.SEND_BUTTON.length; i++) {
      var found = document.querySelector(WHATSAPP_SELECTORS.SEND_BUTTON[i]);
      if (found) return found;
    }
    var icon = document.querySelector(WHATSAPP_SELECTORS.SEND_ICON);
    return icon ? icon.closest('button') : null;
  }

  var whatsappAdapter = {
    isReady: function () { return !!whatsappFindComposer(); },
    findComposer: function () { return whatsappFindComposer(); },
    setText: async function (text, ctx) {
      if (ctx.token.cancelled) return false;
      var target = whatsappFindComposer();
      if (!target) return false;
      clearEditableExec(target);
      return await humanTypeInto(target, text, ctx.cfg, ctx.token);
    },
    send: async function (ctx) {
      if (ctx.token.cancelled) return false;
      var target = whatsappFindComposer();
      if (!target) return false;
      var btn = whatsappFindSendButton();
      if (btn) { btn.click(); return true; }
      target.focus();
      dispatchEnter(target);
      return true;
    },
    // The reference script never polls for delivery on WhatsApp — it
    // trusts the local echo and moves straight on to the next line. This
    // method is kept meaningful rather than a bare stub, but it always
    // succeeds: that is a faithful match to the reference script's own
    // trust model, not a missing feature. See the P4 report.
    waitForAck: async function () { return true; }
  };

  // ===========================================================================
  // Meta adapter — selectors verified against
  // artifacts/store-scripts/64-Lotus_Blue.js
  // ===========================================================================
  var META_SELECTORS = {
    // Message composer inside the Business Suite inbox conversation pane.
    COMPOSER: [
      'div[contenteditable="true"][role="textbox"]',
      'div[contenteditable="true"][aria-label]',
      'div[contenteditable="true"]'
    ],
    // The reference script never found a reliable send-button selector on
    // Meta — it always dispatches a synthetic Enter keypress (see send()
    // below). Keep it that way unless a stable button selector turns up in
    // production; do not assume one exists.
    //
    // Used only to snapshot the thread's visible text for acknowledgement
    // polling (waitForAck) — never for sending:
    THREAD_TEXT_ROOTS: [
      '[role="main"]',
      '[data-pagelet*="Inbox"]',
      '[aria-label*="Messages"]',
      '[role="grid"]'
    ]
  };

  function metaFindComposer() {
    for (var i = 0; i < META_SELECTORS.COMPOSER.length; i++) {
      var found = document.querySelector(META_SELECTORS.COMPOSER[i]);
      if (found) return found;
    }
    return null;
  }

  // Meta's inbox does not expose a per-message delivery event the way the
  // reference script could observe it, so it confirms a send by snapshotting
  // the thread pane's visible text and checking whether the line we just
  // sent now appears in it (full match, or its first ~90%/12-char prefix as
  // a fallback for platform-side formatting differences).
  function metaSnapshotThreadText() {
    var text = '';
    for (var i = 0; i < META_SELECTORS.THREAD_TEXT_ROOTS.length; i++) {
      var root = document.querySelector(META_SELECTORS.THREAD_TEXT_ROOTS[i]);
      if (!root) continue;
      try {
        var t = (root.innerText || root.textContent || '').trim();
        if (t) { text = t; break; }
      } catch (e) { /* ignore and try the next root */ }
    }
    var composer = metaFindComposer();
    if (composer) {
      var composerText = (composer.innerText || composer.textContent || '').trim();
      if (composerText) text = text.replace(composerText, '');
    }
    return sanitizeGeneric(text);
  }

  function metaHasLine(line) {
    var target = sanitizeGeneric(line);
    if (!target) return true;
    var snapshot = metaSnapshotThreadText();
    if (!snapshot) return false;
    if (snapshot.indexOf(target) !== -1) return true;
    var head = target.slice(0, Math.max(12, Math.floor(target.length * 0.9)));
    return !!(head && snapshot.indexOf(head) !== -1);
  }

  var metaAdapter = {
    isReady: function () { return !!metaFindComposer(); },
    findComposer: function () { return metaFindComposer(); },
    setText: async function (text, ctx) {
      if (ctx.token.cancelled) return false;
      var target = metaFindComposer();
      if (!target) return false;
      clearEditableExec(target);
      return await humanTypeInto(target, text, ctx.cfg, ctx.token);
    },
    send: async function (ctx) {
      if (ctx.token.cancelled) return false;
      var target = metaFindComposer();
      if (!target) return false;
      target.focus();
      dispatchEnter(target);
      return true;
    },
    waitForAck: async function (text, ctx) {
      var timeout = ctx.cfg.ACK_TIMEOUT || DEFAULT_SETTINGS.ACK_TIMEOUT;
      var pollMs = ctx.cfg.ACK_POLL || DEFAULT_SETTINGS.ACK_POLL;
      var end = Date.now() + timeout;
      while (Date.now() < end) {
        if (ctx.token.cancelled) return false;
        if (metaHasLine(text)) return true;
        await sleep(pollMs);
      }
      return false;
    }
  };

  function pickAdapter() {
    var host = location.hostname;
    if (host === 'web.whatsapp.com') return whatsappAdapter;
    if (host === 'business.facebook.com' || host === 'www.facebook.com') return metaAdapter;
    return null;
  }

  // ---------------------------------------------------------------------
  // Settings — definition.settings is a flat string-keyed dict (contract
  // §2). Every key is optional; anything missing or non-numeric falls back
  // to the legacy "very_fast" default above.
  // ---------------------------------------------------------------------
  function buildCfg(settings) {
    var cfg = {};
    var keys = Object.keys(DEFAULT_SETTINGS);
    for (var i = 0; i < keys.length; i++) {
      var k = keys[i];
      var raw = settings ? settings[k] : undefined;
      var n = Number(raw);
      cfg[k] = (raw != null && raw !== '' && !isNaN(n)) ? n : DEFAULT_SETTINGS[k];
    }
    return cfg;
  }

  // ---------------------------------------------------------------------
  // Theme / styling
  // ---------------------------------------------------------------------
  function removeExistingStyle() {
    var prev = document.getElementById('luxira-engine-style');
    if (prev) prev.remove();
  }

  // These four values are concatenated directly into a <style> tag's CSS
  // TEXT below (not assigned to a single element.style property), so an
  // unexpected value could break out of the intended rule and affect the
  // host page, not just our own bar. Restrict to shapes CSS colours
  // actually take; anything else falls back to the engine default. (Values
  // used via element.style.property elsewhere, e.g. a subcategory's
  // colorToken accent, do not need this — the CSSOM already rejects an
  // invalid single-property value without side effects.)
  function safeCssColor(v, fallback) {
    var s = String(v == null ? '' : v).trim();
    if (/^#[0-9a-fA-F]{3,8}$/.test(s)) return s;
    if (/^[a-zA-Z]{3,20}$/.test(s)) return s;
    if (/^(rgb|rgba|hsl|hsla)\(\s*[\d.]+%?\s*(,\s*[\d.]+%?\s*){2,3}\)$/.test(s)) return s;
    return fallback;
  }

  function injectStyles(theme) {
    removeExistingStyle();
    var style = el('style', null, { 'data-luxira-node': '1' });
    style.id = 'luxira-engine-style';
    var gold = safeCssColor(theme.GOLD, DEFAULT_THEME.GOLD);
    var progress = safeCssColor(theme.PROGRESS, DEFAULT_THEME.PROGRESS);
    var stop = safeCssColor(theme.STOP, DEFAULT_THEME.STOP);
    var stopHover = safeCssColor(theme.STOP_HOVER, DEFAULT_THEME.STOP_HOVER);
    style.textContent =
      '#luxira-engine-bar,#luxira-engine-bar *{box-sizing:border-box;font-family:Arial,Tahoma,sans-serif;}' +
      '#luxira-engine-bar{position:fixed;top:8px;left:60px;z-index:2147483000;display:flex;align-items:center;gap:8px;background:#fff;border-radius:18px;box-shadow:0 10px 24px rgba(0,0,0,.18);padding:4px 10px;direction:rtl;}' +
      '.luxira-drag-gap{width:14px;flex:0 0 14px;height:56px;cursor:ew-resize;background:transparent;}' +
      '.luxira-dd{position:relative;width:200px;font-size:14px;}' +
      '.luxira-box{background:#fff;border:1px solid rgba(0,0,0,.12);border-radius:14px;box-shadow:0 6px 14px rgba(0,0,0,.12);}' +
      '.luxira-btn{position:relative;padding:10px 12px;padding-right:48px;cursor:pointer;display:flex;align-items:center;gap:8px;border-radius:14px;color:#000;overflow:hidden;}' +
      '.luxira-btn .luxira-label{font-weight:700;font-size:14px;position:relative;z-index:2;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:140px;}' +
      '.luxira-badge{position:absolute;right:10px;top:50%;transform:translateY(-50%);width:24px;height:24px;border-radius:50%;background:' + gold + ';display:flex;align-items:center;justify-content:center;color:#fff;font-size:13px;z-index:1;overflow:hidden;}' +
      '.luxira-badge img{width:16px;height:16px;object-fit:contain;}' +
      '.luxira-panel{display:none;position:absolute;top:100%;right:0;left:0;margin-top:6px;background:#fff;border:1px solid rgba(0,0,0,.12);border-radius:12px;box-shadow:0 12px 28px rgba(0,0,0,.18);z-index:2147483001;flex-direction:column;overflow:hidden;}' +
      '.luxira-dd.luxira-open .luxira-panel{display:flex;}' +
      '.luxira-search{padding:7px;border-bottom:1px solid #eef1f6;}' +
      '.luxira-search input{width:100%;border:1px solid #ccc;padding:7px;font-size:13px;border-radius:8px;box-sizing:border-box;direction:rtl;}' +
      '.luxira-list{max-height:280px;overflow:auto;}' +
      '.luxira-item{direction:rtl;display:flex;align-items:center;justify-content:space-between;gap:8px;padding:9px 10px;border-bottom:1px solid rgba(0,0,0,.06);cursor:pointer;font-size:14px;}' +
      '.luxira-item:hover{background:rgba(0,0,0,.05);}' +
      '.luxira-item-label{flex:1 1 auto;}' +
      '.luxira-item-icon{width:20px;height:20px;display:flex;align-items:center;justify-content:center;font-size:16px;flex:0 0 auto;}' +
      '.luxira-item-icon img{width:100%;height:100%;object-fit:contain;}' +
      '.luxira-footer{display:none;padding:8px;border-top:1px solid #eef1f6;}' +
      '.luxira-stop{width:100%;border:0;background:' + stop + ';color:#fff;font-weight:700;font-size:14px;height:36px;border-radius:8px;cursor:pointer;}' +
      '.luxira-stop:hover{background:' + stopHover + ';}' +
      '.luxira-btn.luxira-busy{background:linear-gradient(to left,' + progress + ' var(--luxira-pct,0%),#fff var(--luxira-pct,0%));}' +
      '.luxira-btn.luxira-failed .luxira-label{color:' + stop + ';}';
    document.head.appendChild(style);
    if (state) state.styleEl = style;
  }

  // ---------------------------------------------------------------------
  // Icons — iconKind: 'emoji' (render literally) | 'twemoji' (CDN image,
  // same base URL the legacy scripts use) | 'svg' (inline markup, trusted
  // the same way the rest of the definition JSON is: it only ever reaches
  // this engine after P2's own admin/permission gate). Anything else, or a
  // render error, fails soft — never throws into the host page.
  // ---------------------------------------------------------------------
  function renderIcon(container, icon, iconKind) {
    container.textContent = '';
    if (!icon) return;
    try {
      if (iconKind === 'twemoji') {
        var hex = toTwemojiHex(icon);
        if (!hex) { container.textContent = String(icon); return; }
        var img = el('img');
        img.src = TWEMOJI_BASE + hex + '.png';
        img.alt = '';
        container.appendChild(img);
      } else if (iconKind === 'svg') {
        container.innerHTML = String(icon);
      } else {
        container.textContent = String(icon);
      }
    } catch (e) {
      console.warn('[luxira-engine] icon render failed for iconKind=' + iconKind, e);
    }
  }

  function renderFlag(container, flagHex) {
    container.textContent = '';
    if (!flagHex) return;
    var img = el('img');
    img.src = TWEMOJI_BASE + flagHex + '.png';
    img.alt = '';
    container.appendChild(img);
  }

  // ---------------------------------------------------------------------
  // Send pipeline
  // ---------------------------------------------------------------------
  // One message: type it, send it, confirm it. Meta's reference script
  // retries the whole clear-type-send cycle up to ACK_RETRY times if the
  // acknowledgement poll times out ("these platforms drop messages sent too
  // fast"); WhatsApp's adapter always acks immediately so this loop runs
  // once for it in practice. Matches the legacy scripts' own behaviour on
  // final failure: abort rather than skip ahead, so a caller never ends up
  // with phase 2 sent but phase 1 missing.
  async function sendLineViaAdapter(adapter, rawText, cfg, token) {
    var line = sanitizeGeneric(rawText);
    if (!line) return true;
    var attempts = Math.max(1, (cfg.ACK_RETRY | 0) + 1);
    for (var attempt = 0; attempt < attempts; attempt++) {
      if (token.cancelled) return false;
      var ctx = { cfg: cfg, token: token };
      var typed = await adapter.setText(line, ctx);
      if (!typed) return false;
      if (cfg.PRE > 0) { await sleep(cfg.PRE); if (token.cancelled) return false; }
      await new Promise(function (r) { requestAnimationFrame(r); });
      var sent = await adapter.send(ctx);
      if (!sent) return false;
      var acked = await adapter.waitForAck(line, ctx);
      if (acked) {
        if (cfg.POST > 0) { await sleep(cfg.POST); if (token.cancelled) return false; }
        return true;
      }
      await sleep(1);
    }
    return false;
  }

  // Phases in order, steps within a phase in order, INIT_GAP pause between
  // phases — the same shape and gap the legacy scripts used for e.g.
  // FOUNDATIONS' phase1 (opening lines) / phase2 (price + question),
  // generalised from exactly two phases to however many the definition has.
  async function sendSequence(adapter, sequence, cfg, token, onProgress) {
    var phases = (Array.isArray(sequence) ? sequence.slice() : []).sort(function (a, b) {
      return (a.phase || 0) - (b.phase || 0);
    });
    var total = 0;
    phases.forEach(function (p) { total += Array.isArray(p.steps) ? p.steps.length : 0; });
    if (total === 0) { onProgress(100); return true; }
    var done = 0;
    for (var pi = 0; pi < phases.length; pi++) {
      if (token.cancelled) return false;
      var steps = Array.isArray(phases[pi].steps) ? phases[pi].steps : [];
      for (var si = 0; si < steps.length; si++) {
        if (token.cancelled) return false;
        var ok = await sendLineViaAdapter(adapter, steps[si], cfg, token);
        if (!ok) return false;
        done++;
        onProgress(Math.round((done / total) * 100));
        if (cfg.DELAY > 0) { await sleep(cfg.DELAY); if (token.cancelled) return false; }
      }
      if (pi !== phases.length - 1) {
        await sleep(cfg.INIT_GAP);
        if (token.cancelled) return false;
      }
    }
    onProgress(100);
    return true;
  }

  // GENDERS mirrors the legacy per-store scripts' own gender selector (see e.g.
  // artifacts/store-scripts/68-Lotus_Blue.js GENDERS/askGender/byGender) — same two
  // options, same order. Only shown when a button actually has both an "m" and an "f"
  // sequence; a button with a single ungendered sequence never asks.
  var GENDERS = [
    { label: 'مؤنث', genderKey: 'f' },
    { label: 'مذكر', genderKey: 'm' }
  ];

  function sequenceFor(sub, countryCode) {
    var seqs = sub.sequences || {};
    return seqs[countryCode] || seqs['*'] || null;
  }

  function hasGenderVariants(genderedSeq) {
    return !!(genderedSeq && Array.isArray(genderedSeq.m) && Array.isArray(genderedSeq.f));
  }

  function phasesFor(genderedSeq, gender) {
    if (!genderedSeq) return [];
    if (Array.isArray(genderedSeq.single)) return genderedSeq.single;
    if (gender === 'f') return genderedSeq.f || [];
    return genderedSeq.m || [];
  }

  // Which countries this subcategory actually has content for — derived
  // from the sequence keys themselves (contract §2: "sequences is keyed by
  // country code, plus '*' for messages with ScriptCountryId = null"), not
  // a separate list. This generalises the legacy scripts' one-off pattern
  // of e.g. only offering a bank-details country picker for countries that
  // had bank info, without the engine needing to know "bank" is special.
  function countryListForSub(sub) {
    var seqs = sub.sequences || {};
    var keys = Object.keys(seqs).filter(function (k) { return k !== '*'; });
    var byCode = {};
    (state.definition.countries || []).forEach(function (c) { byCode[c.code] = c; });
    return keys.map(function (k) { return byCode[k]; }).filter(Boolean);
  }

  function setBtnPct(btn, pct) {
    btn.style.setProperty('--luxira-pct', clampPct(pct) + '%');
  }

  function flashBtnMessage(btn, msg) {
    var labelEl = btn.querySelector('.luxira-label');
    if (!labelEl) return;
    var orig = labelEl.getAttribute('data-orig') || labelEl.textContent;
    labelEl.textContent = msg;
    btn.classList.add('luxira-failed');
    trackTimeout(function () {
      if (!labelEl.isConnected) return;
      labelEl.textContent = orig;
      btn.classList.remove('luxira-failed');
    }, 2200);
  }

  async function startSend(btn, footer, stopBtn, sub, countryCode, gender) {
    if (!state || state.sendLock) return;
    var adapter = state.adapter;
    if (!adapter || !adapter.isReady()) {
      flashBtnMessage(btn, CHROME.NOT_READY);
      return;
    }
    var cfg = buildCfg(state.definition.settings);
    var token = { cancelled: false };
    state.sendLock = true;
    state.activeToken = token;
    btn.classList.add('luxira-busy');
    footer.style.display = 'block';
    setBtnPct(btn, 0);
    stopBtn.onclick = function (e) {
      e.preventDefault();
      e.stopPropagation();
      token.cancelled = true;
    };

    var seq = phasesFor(sequenceFor(sub, countryCode), gender);
    var ok = false;
    try {
      ok = await sendSequence(adapter, seq, cfg, token, function (pct) { setBtnPct(btn, pct); });
    } catch (e) {
      console.error('[luxira-engine] send failed for subcategory ' + (sub && sub.key), e);
      ok = false;
    }

    footer.style.display = 'none';
    btn.classList.remove('luxira-busy');
    btn.style.removeProperty('--luxira-pct');
    if (state) { state.sendLock = false; state.activeToken = null; }
    if (!ok && !token.cancelled) flashBtnMessage(btn, CHROME.SEND_FAILED);
  }

  // ---------------------------------------------------------------------
  // UI construction — categories render as dropdown buttons (mirrors the
  // legacy scripts' `.hayatSS` box/btn/panel/list/item shape, renamed and
  // recoloured generically); each subcategory is a row inside. A
  // country-scoped subcategory reuses the same panel to show a country
  // picker before sending, exactly like the legacy `openCountrySend`.
  // ---------------------------------------------------------------------
  function buildRow(labelText, iconSpec, accentColor, onClick) {
    var row = el('div', 'luxira-item');
    if (accentColor) row.style.borderRight = '3px solid ' + accentColor;
    var icon = el('span', 'luxira-item-icon');
    if (iconSpec) renderIcon(icon, iconSpec.icon, iconSpec.iconKind);
    var label = el('span', 'luxira-item-label');
    label.textContent = labelText;
    row.appendChild(label);
    row.appendChild(icon);
    row.addEventListener('click', function (e) {
      e.stopPropagation();
      onClick();
    });
    return row;
  }

  function openCountryPicker(list, countries, onPick) {
    list.innerHTML = '';
    countries.forEach(function (country) {
      var row = el('div', 'luxira-item');
      var label = el('span', 'luxira-item-label');
      label.textContent = country.label || country.code;
      var icon = el('span', 'luxira-item-icon');
      renderFlag(icon, country.flagHex);
      row.appendChild(label);
      row.appendChild(icon);
      row.addEventListener('click', function (e) {
        e.stopPropagation();
        onPick(country);
      });
      list.appendChild(row);
    });
  }

  // Same shape as openCountryPicker — only ever shown when the picked node's
  // sequence has both an "m" and an "f" variation (see hasGenderVariants).
  function openGenderPicker(list, onPick) {
    list.innerHTML = '';
    GENDERS.forEach(function (g) {
      var row = el('div', 'luxira-item');
      var label = el('span', 'luxira-item-label');
      label.textContent = g.label;
      row.appendChild(label);
      row.addEventListener('click', function (e) {
        e.stopPropagation();
        onPick(g);
      });
      list.appendChild(row);
    });
  }

  // Note: a dropdown's own `data-orig` (set once when its `label` span is
  // created) is intentionally left alone by picks below. It is the safe
  // value flashBtnMessage() restores to after a failure — it must always be
  // the top button's own label, never the node/country just picked, or a
  // failed send would permanently relabel the button.
  // Runs after the country (if any) is settled: asks male/female only when this
  // node's sequence for that country actually has both variations, exactly like
  // the legacy scripts' askGender step.
  function proceedToSend(dd, listEl, btnEl, footerEl, stopBtnEl, node, countryCode) {
    var seq = sequenceFor(node, countryCode);
    if (hasGenderVariants(seq)) {
      dd.classList.add('luxira-open');
      openGenderPicker(listEl, function (g) {
        startSend(btnEl, footerEl, stopBtnEl, node, countryCode, g.genderKey);
      });
    } else {
      startSend(btnEl, footerEl, stopBtnEl, node, countryCode);
    }
  }

  function onLeafPick(dd, listEl, btnEl, footerEl, stopBtnEl, node) {
    var countries = countryListForSub(node);
    if (node.isCountryScoped && countries.length) {
      dd.classList.add('luxira-open');
      openCountryPicker(listEl, countries, function (country) {
        proceedToSend(dd, listEl, btnEl, footerEl, stopBtnEl, node, country.code);
      });
    } else {
      proceedToSend(dd, listEl, btnEl, footerEl, stopBtnEl, node, '*');
    }
  }

  function buildCategoryDropdown(category, theme) {
    var dd = el('div', 'luxira-dd');
    var box = el('div', 'luxira-box');
    var btn = el('div', 'luxira-btn');
    var label = el('span', 'luxira-label', { 'data-orig': category.label || '' });
    label.textContent = category.label || '';
    var badge = el('span', 'luxira-badge');
    renderIcon(badge, category.icon, category.iconKind);
    btn.appendChild(label);
    btn.appendChild(badge);
    box.appendChild(btn);

    var panel = el('div', 'luxira-panel');
    var searchWrap = el('div', 'luxira-search');
    var search = el('input', null, { placeholder: CHROME.SEARCH_PLACEHOLDER });
    searchWrap.appendChild(search);
    var list = el('div', 'luxira-list');
    var footer = el('div', 'luxira-footer');
    var stopBtn = el('button', 'luxira-stop', { type: 'button' });
    stopBtn.textContent = CHROME.STOP;
    footer.appendChild(stopBtn);
    panel.appendChild(searchWrap);
    panel.appendChild(list);
    panel.appendChild(footer);
    box.appendChild(panel);
    dd.appendChild(box);

    var subCats = Array.isArray(category.subCategories) ? category.subCategories : [];
    var isLeafCategory = subCats.length === 0 && category.sequences;

    // Drill-down stack for nested subcategories (rule #1 — buttons nest to any
    // depth): [] is the top level (subCats). Pushing a node with its own
    // children re-renders that node's list with a "back" row above it, so the
    // panel/search/footer chrome built above is reused at every level.
    var stack = [];

    function currentLevel() {
      return stack.length ? stack[stack.length - 1].subCategories : subCats;
    }

    function renderLevel(filterText) {
      list.innerHTML = '';
      var q = (filterText || '').trim();

      if (stack.length) {
        var backRow = el('div', 'luxira-item luxira-back');
        var backLabel = el('span', 'luxira-item-label');
        backLabel.textContent = CHROME.BACK;
        backRow.appendChild(backLabel);
        backRow.addEventListener('click', function (e) {
          e.stopPropagation();
          stack.pop();
          search.value = '';
          renderLevel('');
        });
        list.appendChild(backRow);
      }

      currentLevel().forEach(function (node) {
        if (q && String(node.label || '').indexOf(q) === -1) return;
        var accent = node.colorToken && theme[node.colorToken] ? theme[node.colorToken] : null;
        var hasChildren = Array.isArray(node.subCategories) && node.subCategories.length > 0;
        var row = buildRow(node.label || '', node, accent, function () {
          if (hasChildren) {
            stack.push(node);
            search.value = '';
            renderLevel('');
          } else {
            onLeafPick(dd, list, btn, footer, stopBtn, node);
          }
        });
        list.appendChild(row);
      });
    }

    search.addEventListener('input', function () { renderLevel(search.value); });

    btn.addEventListener('click', function (e) {
      e.stopPropagation();
      if (isLeafCategory) {
        // Rule #6 — a category with no live children can itself carry a
        // message sequence; clicking it behaves exactly like clicking a leaf
        // row one level down, no drill-down needed.
        onLeafPick(dd, list, btn, footer, stopBtn, category);
        return;
      }
      var isOpen = dd.classList.contains('luxira-open');
      if (isOpen) { dd.classList.remove('luxira-open'); return; }
      stack = [];
      search.value = '';
      renderLevel('');
      dd.classList.add('luxira-open');
    });

    if (!isLeafCategory) { renderLevel(''); }
    return dd;
  }

  function buildBar(definition) {
    var bar = el('div', null, { id: 'luxira-engine-bar', dir: 'rtl', 'data-luxira-node': '1' });
    var cfg = buildCfg(definition.settings);
    bar.style.transform = 'scale(' + (cfg.SCALE || DEFAULT_SETTINGS.SCALE) + ')';
    bar.style.transformOrigin = 'top left';
    var theme = definition.theme || {};
    var categories = Array.isArray(definition.categories) ? definition.categories : [];
    // A grab handle sits before, between and after every dropdown, so the bar can
    // be dragged sideways from the empty space rather than only from an edge.
    function gap() {
      return el('div', 'luxira-drag-gap', { 'data-luxira-node': '1', 'data-drag-gap': '1' });
    }
    bar.appendChild(gap());
    categories.forEach(function (cat) {
      try {
        bar.appendChild(buildCategoryDropdown(cat, theme));
        bar.appendChild(gap());
      } catch (e) {
        console.warn('[luxira-engine] failed to render category ' + (cat && cat.key), e);
      }
    });
    document.body.appendChild(bar);
    if (state) state.barEl = bar;
    wireBarDrag(bar);
  }

  // Drag-to-reposition, ported from the legacy scripts. Listeners go on the
  // document so the drag survives the pointer leaving the thin gap element.
  function wireBarDrag(bar) {
    var dragging = false;
    var offX = 0;
    var top = bar.offsetTop;

    function onDown(e) {
      if (e.button !== 0) { return; }
      dragging = true;
      offX = e.clientX - bar.offsetLeft;
      top = bar.offsetTop;
      e.preventDefault();
      e.stopPropagation();
    }

    function onMove(e) {
      if (!dragging) { return; }
      bar.style.left = (e.clientX - offX) + 'px';
      bar.style.top = top + 'px';
    }

    function onUp() { dragging = false; }

    var gaps = bar.querySelectorAll('[data-drag-gap="1"]');
    for (var i = 0; i < gaps.length; i++) {
      gaps[i].addEventListener('mousedown', onDown);
    }
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
    if (state) {
      state.listeners.push({ target: document, type: 'mousemove', handler: onMove });
      state.listeners.push({ target: document, type: 'mouseup', handler: onUp });
    }
  }

  // ---------------------------------------------------------------------
  // Self-healing presence — both host apps are SPAs that can wipe and
  // rebuild large parts of the DOM on navigation. The legacy scripts kept
  // their bar alive with a MutationObserver plus a monkey-patch of
  // history.pushState/replaceState that is never undone. That patch would
  // violate "leave the page exactly as found" on teardown, so it is
  // deliberately NOT ported: this engine relies solely on a
  // MutationObserver plus a plain tracked interval, both of which teardown
  // can fully and simply reverse. See the P4 report.
  // ---------------------------------------------------------------------
  function startSelfHeal() {
    function ensurePresent() {
      if (!state) return;
      var missingStyle = !state.styleEl || !document.head || !document.head.contains(state.styleEl);
      if (missingStyle) {
        try { injectStyles(state.definition.theme || {}); } catch (e) { /* next tick will retry */ }
      }
      var missingBar = !state.barEl || !document.body || !document.body.contains(state.barEl);
      if (missingBar) {
        try { if (state.barEl) state.barEl.remove(); } catch (e) { /* already gone */ }
        try { buildBar(state.definition); } catch (e) { console.error('[luxira-engine] rebuild failed', e); }
      }
    }
    var mo = new MutationObserver(function () { ensurePresent(); });
    try {
      mo.observe(document.documentElement || document.body, { childList: true, subtree: true });
    } catch (e) { /* nothing to observe yet; the interval below still covers us */ }
    if (state) state.observer = mo;
    trackInterval(ensurePresent, SELF_HEAL_INTERVAL_MS);
  }

  // ---------------------------------------------------------------------
  // Public surface
  // ---------------------------------------------------------------------
  function doRender(definition) {
    try {
      if (!definition || typeof definition !== 'object' || !Array.isArray(definition.categories)) {
        console.error('[luxira-engine] malformed definition, refusing to render', definition);
        return;
      }
      if (definition.engineVersion && definition.engineVersion !== ENGINE_VERSION) {
        console.warn('[luxira-engine] definition targets engineVersion ' + definition.engineVersion + ', this build is ' + ENGINE_VERSION);
      }
      // Idempotent: a revision bump calls __luxiraRender again on an
      // already-active engine. Tear down the previous render first so this
      // is always a clean full rebuild, never a leak-prone patch.
      if (window[RUN_KEY]) doTeardown();

      state = freshState(definition);
      state.adapter = pickAdapter();
      if (!state.adapter) {
        console.error('[luxira-engine] unrecognised host, not rendering: ' + location.hostname);
        state = null;
        return;
      }
      window[RUN_KEY] = true;
      injectStyles(definition.theme || {});
      buildBar(definition);
      startSelfHeal();
    } catch (e) {
      console.error('[luxira-engine] render failed', e);
      try { doTeardown(); } catch (e2) { /* best effort */ }
    }
  }

  function doTeardown() {
    if (!state && !window[RUN_KEY]) return;
    try { if (state && state.activeToken) state.activeToken.cancelled = true; } catch (e) {}
    try { if (state && state.observer) state.observer.disconnect(); } catch (e) {}
    try {
      if (state && state.timers) {
        state.timers.forEach(function (t) {
          try { if (t.kind === 'interval') clearInterval(t.id); else clearTimeout(t.id); } catch (e) {}
        });
      }
    } catch (e) {}
    try {
      if (state && state.listeners) {
        state.listeners.forEach(function (l) {
          try { l.target.removeEventListener(l.type, l.handler); } catch (e) {}
        });
      }
    } catch (e) {}
    bridgeListenerInstalled = false;
    bridgePending = Object.create(null);
    try { if (state && state.barEl) state.barEl.remove(); } catch (e) {}
    try { if (state && state.styleEl) state.styleEl.remove(); } catch (e) {}
    // Defensive sweep in case any reference above was lost (e.g. a render
    // that failed partway through). Every node this engine creates carries
    // this marker.
    try {
      document.querySelectorAll('[data-luxira-node="1"]').forEach(function (n) { n.remove(); });
    } catch (e) {}
    state = null;
    try { window[RUN_KEY] = false; } catch (e) {}
  }

  window.__luxiraRender = doRender;
  window.__luxiraTeardown = doTeardown;
})();
