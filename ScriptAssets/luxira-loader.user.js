// ==UserScript==
// @name         Luxira Store Scripts — Loader
// @namespace    luxira.org
// @version      2.0.2
// @description  Fetches the employee's permitted store script(s) from luxira.org and runs the runtime engine. Carries no business logic of its own.
// @match        https://web.whatsapp.com/*
// @match        https://business.facebook.com/*
// @match        https://business.facebook.com/latest/inbox/*
// @match        https://business.facebook.com/*/inbox/*
// @match        https://business.facebook.com/*/inbox/all*
// @match        https://www.facebook.com/business/inbox/*
// @connect      luxira.org
// @grant        GM_xmlhttpRequest
// @grant        GM_setValue
// @grant        GM_getValue
// @grant        unsafeWindow
// @run-at       document-idle
// ==/UserScript==

/**
 * Layer 1 of the Store Script Platform — see documentation/
 * store-script-platform.md and documentation/seed-script-loader.md.
 *
 * This script is deliberately a URL and a fetch, nothing more: exporting it
 * from Tampermonkey gives a departing employee a recipe that does nothing
 * without a live luxira.org session (documentation/seed-script-loader.md
 * "Why it is built this way"). All category/message/price content — the
 * part actually worth copying — lives server-side and is served per
 * request to an authenticated, permitted CRM user (contract §5).
 *
 * What this file does, and nothing more:
 *   1. Detects platform (WhatsApp / Meta) from location.hostname, and for
 *      Meta reads asset_id / page_id / business_id from the query string.
 *   2. Polls GET /seedscript/manifest every 60 seconds for the scripts this
 *      employee may run here (contract §3). 0 -> idle. 1 -> auto-load.
 *      >1 -> store picker, remembered per asset via GM_setValue.
 *   3. Loads GET /seedscript/engine.js once (blob: object-URL <script>,
 *      since WhatsApp's CSP forbids eval/new Function but allows blob: in
 *      script-src), then fetches GET /seedscript/definition/{id} and hands
 *      it straight to the page context's __luxiraRender(definition)
 *      via unsafeWindow (see pageWindow below).
 *   4. On a revision change, re-fetches the definition and calls
 *      __luxiraRender again (same store, fresh content). If the running
 *      script disappears from the manifest (deactivated, or the store's
 *      permission was revoked), calls __luxiraTeardown() and keeps
 *      polling in case it comes back.
 *   5. Proxies the engine's own luxira.org traffic — the engine runs in
 *      page context via the blob: script and therefore has no GM_* APIs
 *      (documentation/seed-script-loader.md "CSP — why blob, not eval").
 *      NOTE: in this build, the loader itself fetches manifest/engine.js/
 *      definition and passes the definition into __luxiraRender() as a
 *      plain argument, so the engine currently has nothing of its own to
 *      fetch. The bridge below is still implemented per contract §4 (with
 *      the Date.now() collision fixed) so the capability exists if a
 *      future engine change needs it. See the P4 agent report.
 */
(function () {
  'use strict';

  var ORIGIN = 'https://luxira.org';
  // 60s: admin edits (and permission revocations) must reach a running agent
  // within a minute. The poll is one small JSON request; the definition is
  // only re-fetched when RevisionStamp actually moved.
  var POLL_MS = 60 * 1000;
  var LOG = '[luxira-loader]';

  // The engine is injected as a blob: <script>, so it executes in PAGE
  // context and assigns __luxiraRender / __luxiraTeardown to the page's
  // window. Because this loader declares @grant, it runs in Tampermonkey's
  // sandbox, where `window` is a distinct object that does not expose
  // globals created by page scripts — reading window.__luxiraRender here
  // always yields undefined, no matter how well the engine loaded. The
  // handoff must therefore go through unsafeWindow. Falls back to window so
  // the loader still works in a no-sandbox environment (@grant none, or a
  // manager without unsafeWindow).
  var pageWindow = (typeof unsafeWindow !== 'undefined' && unsafeWindow) ? unsafeWindow : window;

  // ---------------------------------------------------------------------
  // Bridge (stays in this sandboxed context, so it keeps GM_* powers). A
  // page-context script can postMessage a request here and this loader
  // proxies it through GM_xmlhttpRequest, which bypasses CORS/CSP, then
  // posts the response back. Protocol unchanged from
  // _scratch/tampermonkey/payload-examples.js:
  //   -> { __luxira: 'req', id, method, url, headers, body }
  //   <- { __luxira: 'res', id, status, body }   // status 0 = network error
  //
  // This relay is id-agnostic: it echoes back whatever `id` the caller sent
  // in the request, unchanged. The Date.now()-collision bug named in
  // contract §4 is therefore NOT in this relay — it is in how the
  // page-context CALLER generates that `id` to correlate its own concurrent
  // requests (payload-examples.js line 20: `const id = Date.now();`, which
  // two requests fired in the same millisecond would share). The fix lives
  // in App_Data/luxira-engine.js's private bridge-request helper, which
  // uses a monotonic counter instead — see that file for details.
  // ---------------------------------------------------------------------
  window.addEventListener('message', function (e) {
    if (e.source !== window || !e.data || e.data.__luxira !== 'req') return;
    GM_xmlhttpRequest({
      method: e.data.method || 'GET',
      url: e.data.url,
      data: e.data.body,
      headers: e.data.headers,
      onload: function (r) {
        window.postMessage({ __luxira: 'res', id: e.data.id, status: r.status, body: r.responseText }, '*');
      },
      onerror: function () {
        window.postMessage({ __luxira: 'res', id: e.data.id, status: 0, body: null }, '*');
      }
    });
  });

  // ---------------------------------------------------------------------
  // Platform / target detection
  // ---------------------------------------------------------------------
  function detectPlatform() {
    var host = location.hostname;
    if (host === 'web.whatsapp.com') return 'WhatsApp';
    if (host === 'business.facebook.com' || host === 'www.facebook.com') return 'Meta';
    return null;
  }

  // Re-read on every poll (not just once at startup) so that if the
  // Business Suite SPA changes asset_id in the URL without a full
  // navigation, the *next* 60-second poll picks it up. Faster in-page
  // switching is not handled — see the P4 agent report.
  function readMetaIds() {
    var q = new URLSearchParams(location.search);
    return {
      assetId: q.get('asset_id') || '',
      pageId: q.get('page_id') || '',
      businessId: q.get('business_id') || ''
    };
  }

  // ---------------------------------------------------------------------
  // Privileged fetch helpers. Every runtime endpoint returns 401 (never a
  // redirect) when the employee is not logged into luxira.org in this
  // browser (contract §5) — a redirect would otherwise be executed as
  // JavaScript by the blob: script loader. anonymous:false sends the
  // browser's luxira.org cookie jar; @connect exempts it from CORS.
  // ---------------------------------------------------------------------
  var authWarned = false;
  var authBannerEl = null;

  // A fixed timeout on every request means a stalled connection always
  // settles the promise (status 0, treated like a network error) instead of
  // hanging forever — important given poll() below guards against
  // overlapping runs and would otherwise wedge permanently.
  var REQUEST_TIMEOUT_MS = 15000;

  function apiRequest(path, opts) {
    opts = opts || {};
    return new Promise(function (resolve) {
      GM_xmlhttpRequest({
        method: opts.method || 'GET',
        url: ORIGIN + path,
        anonymous: false,
        timeout: REQUEST_TIMEOUT_MS,
        headers: Object.assign({ 'Cache-Control': 'no-cache', 'Pragma': 'no-cache' }, opts.headers || {}),
        onload: function (r) { resolve({ status: r.status, body: r.responseText }); },
        onerror: function () { resolve({ status: 0, body: null }); },
        ontimeout: function () { resolve({ status: 0, body: null }); }
      });
    });
  }

  async function apiJson(path) {
    var res = await apiRequest(path);
    if (res.status === 401) { onAuthMissing(); return null; }
    onAuthRestored();
    if (res.status !== 200) {
      console.error(LOG, 'HTTP', res.status, path);
      return null;
    }
    try { return JSON.parse(res.body); }
    catch (e) { console.error(LOG, 'bad JSON from', path, e); return null; }
  }

  // Warn once per "logged-out session", not every 5-minute poll — a
  // repeated blocking alert() would be genuine spam for an all-day tool.
  function onAuthMissing() {
    if (authWarned) return;
    authWarned = true;
    console.warn(LOG, 'not authenticated — log into luxira.org in this browser, then reload this page');
    showAuthBanner();
  }

  function onAuthRestored() {
    if (!authWarned) return;
    authWarned = false;
    hideAuthBanner();
  }

  function showAuthBanner() {
    if (authBannerEl) return;
    authBannerEl = document.createElement('div');
    authBannerEl.textContent = 'Luxira: سجّلي الدخول إلى luxira.org في هذا المتصفح، ثم أعيدي تحميل الصفحة';
    Object.assign(authBannerEl.style, {
      position: 'fixed', bottom: '10px', left: '10px', zIndex: '2147483647',
      background: '#dc2626', color: '#fff', padding: '8px 14px', borderRadius: '8px',
      fontSize: '13px', fontFamily: 'Arial,sans-serif', direction: 'rtl',
      boxShadow: '0 6px 16px rgba(0,0,0,.25)'
    });
    (document.body || document.documentElement).appendChild(authBannerEl);
  }

  function hideAuthBanner() {
    if (!authBannerEl) return;
    authBannerEl.remove();
    authBannerEl = null;
  }

  // ---------------------------------------------------------------------
  // Store picker + "change store" switcher. Pure chrome, no store content:
  // rows are just the storeName strings the server already decided this
  // employee is allowed to see.
  // ---------------------------------------------------------------------
  var pickerEl = null;
  var switcherEl = null;
  var latestScripts = [];
  var latestPickerKey = null;

  function pickerKey(platform, ids) {
    if (platform === 'WhatsApp') return 'luxira_choice_whatsapp';
    var id = ids.assetId || ids.pageId || ids.businessId || 'unknown';
    return 'luxira_choice_meta_' + id;
  }

  function closePicker() {
    if (pickerEl) { pickerEl.remove(); pickerEl = null; }
  }

  function showPicker(scripts, key, onChoose) {
    closePicker();
    var backdrop = document.createElement('div');
    Object.assign(backdrop.style, {
      position: 'fixed', top: '0', left: '0', right: '0', bottom: '0',
      background: 'rgba(0,0,0,.45)', zIndex: '2147483646',
      display: 'flex', alignItems: 'center', justifyContent: 'center'
    });
    var box = document.createElement('div');
    Object.assign(box.style, {
      background: '#fff', borderRadius: '14px', padding: '16px', minWidth: '260px',
      maxWidth: '90vw', maxHeight: '80vh', overflow: 'auto', direction: 'rtl',
      fontFamily: 'Arial,sans-serif', boxShadow: '0 20px 50px rgba(0,0,0,.35)'
    });
    var title = document.createElement('div');
    title.textContent = 'اختاري المتجر';
    Object.assign(title.style, { fontWeight: '700', fontSize: '16px', marginBottom: '10px' });
    box.appendChild(title);
    scripts.forEach(function (s) {
      var row = document.createElement('div');
      row.textContent = s.storeName;
      Object.assign(row.style, {
        padding: '10px 12px', borderRadius: '8px', cursor: 'pointer',
        fontSize: '14px', marginBottom: '4px'
      });
      row.addEventListener('mouseenter', function () { row.style.background = '#f1f5f9'; });
      row.addEventListener('mouseleave', function () { row.style.background = ''; });
      row.addEventListener('click', function () {
        GM_setValue(key, s.scriptId);
        closePicker();
        onChoose(s.scriptId);
      });
      box.appendChild(row);
    });
    backdrop.appendChild(box);
    backdrop.addEventListener('click', function (e) { if (e.target === backdrop) closePicker(); });
    (document.body || document.documentElement).appendChild(backdrop);
    pickerEl = backdrop;
  }

  function openPickerForLatest() {
    if (!latestScripts.length) return;
    showPicker(latestScripts, latestPickerKey, async function (scriptId) {
      if (scriptId === activeScriptId) return;
      teardownEngine();
      activeScriptId = scriptId;
      var s = latestScripts.filter(function (x) { return x.scriptId === scriptId; })[0];
      activeRevision = s ? s.revision : null;
      await loadDefinitionAndRender(scriptId);
    });
  }

  function showSwitcher() {
    if (switcherEl) return;
    switcherEl = document.createElement('div');
    switcherEl.textContent = 'تبديل المتجر';
    Object.assign(switcherEl.style, {
      position: 'fixed', bottom: '10px', right: '10px', zIndex: '2147483645',
      background: '#111', color: '#fff', padding: '6px 12px', borderRadius: '999px',
      fontSize: '12px', fontFamily: 'Arial,sans-serif', cursor: 'pointer',
      direction: 'rtl', opacity: '0.85'
    });
    switcherEl.addEventListener('click', openPickerForLatest);
    (document.body || document.documentElement).appendChild(switcherEl);
  }

  function hideSwitcher() {
    if (!switcherEl) return;
    switcherEl.remove();
    switcherEl = null;
  }

  // ---------------------------------------------------------------------
  // Engine loading + render
  // ---------------------------------------------------------------------
  var engineLoaded = false;

  function loadEngine(scriptId) {
    return new Promise(function (resolve) {
      if (engineLoaded) { resolve(true); return; }
      GM_xmlhttpRequest({
        method: 'GET',
        url: ORIGIN + '/seedscript/engine.js?scriptId=' + encodeURIComponent(scriptId) + '&t=' + Date.now(),
        anonymous: false,
        timeout: REQUEST_TIMEOUT_MS,
        headers: { 'Cache-Control': 'no-cache', 'Pragma': 'no-cache' },
        ontimeout: function () { console.error(LOG, 'engine.js fetch timed out'); resolve(false); },
        onload: function (r) {
          if (r.status === 401) { onAuthMissing(); resolve(false); return; }
          if (r.status !== 200) { console.error(LOG, 'engine.js HTTP', r.status); resolve(false); return; }
          onAuthRestored();
          try {
            // blob: object-URL <script>, not eval/new Function — WhatsApp's
            // CSP forbids the latter but allows the former in script-src.
            var code = r.responseText + '\n//# sourceURL=' + ORIGIN + '/seedscript/engine.js';
            var blob = new Blob([code], { type: 'application/javascript' });
            var url = URL.createObjectURL(blob);
            var s = document.createElement('script');
            s.src = url;
            s.onload = function () { URL.revokeObjectURL(url); engineLoaded = true; resolve(true); };
            s.onerror = function () { URL.revokeObjectURL(url); console.error(LOG, 'engine.js failed to execute'); resolve(false); };
            (document.head || document.documentElement).appendChild(s);
          } catch (e) {
            console.error(LOG, 'engine.js blob injection failed', e);
            resolve(false);
          }
        },
        onerror: function () { console.error(LOG, 'engine.js fetch failed'); resolve(false); }
      });
    });
  }

  async function loadDefinitionAndRender(scriptId) {
    var definition = await apiJson('/seedscript/definition/' + encodeURIComponent(scriptId));
    if (!definition) return false;
    var ok = await loadEngine(scriptId);
    if (!ok) return false;
    // A newer pick (switcher) or poll could have superseded this one while
    // the two awaits above were in flight — bail rather than render a stale
    // definition over the current selection.
    if (scriptId !== activeScriptId) return false;
    if (typeof pageWindow.__luxiraRender !== 'function') {
      console.error(LOG, 'engine.js loaded but did not expose __luxiraRender');
      return false;
    }
    try {
      pageWindow.__luxiraRender(definition);
    } catch (e) {
      console.error(LOG, '__luxiraRender threw', e);
      return false;
    }
    return true;
  }

  function teardownEngine() {
    if (typeof pageWindow.__luxiraTeardown === 'function') {
      try { pageWindow.__luxiraTeardown(); }
      catch (e) { console.error(LOG, '__luxiraTeardown threw', e); }
    }
  }

  // ---------------------------------------------------------------------
  // Poll loop — manifest every 5 minutes (contract §4).
  // ---------------------------------------------------------------------
  var activeScriptId = null;
  var activeRevision = null;
  var pollInFlight = false;

  async function poll() {
    // Every GM_xmlhttpRequest above carries a timeout, so a run of poll()
    // always finishes within bounded time — this guard is safe and just
    // prevents two overlapping runs (e.g. a slow response still pending
    // when the next 5-minute tick fires) from racing on activeScriptId.
    if (pollInFlight) return;
    pollInFlight = true;
    try {
      await pollOnce();
    } finally {
      pollInFlight = false;
    }
  }

  async function pollOnce() {
    var platform = detectPlatform();
    if (!platform) return; // @match should prevent this, but stay defensive

    var ids = platform === 'Meta' ? readMetaIds() : {};
    var qs = new URLSearchParams({ platform: platform });
    if (ids.assetId) qs.set('assetId', ids.assetId);
    if (ids.pageId) qs.set('pageId', ids.pageId);
    if (ids.businessId) qs.set('businessId', ids.businessId);

    var manifest = await apiJson('/seedscript/manifest?' + qs.toString());
    if (manifest === null) return; // 401 or transient failure already handled/logged; try again next poll

    var scripts = Array.isArray(manifest.scripts) ? manifest.scripts : [];
    latestScripts = scripts;
    latestPickerKey = pickerKey(platform, ids);

    // Is the script we're currently running still permitted, and current?
    if (activeScriptId != null) {
      var stillThere = scripts.filter(function (s) { return s.scriptId === activeScriptId; })[0];
      if (!stillThere) {
        // Deactivated, or this store's permission was revoked: full
        // teardown, then keep polling in case it comes back.
        teardownEngine();
        activeScriptId = null;
        activeRevision = null;
      } else if (stillThere.revision !== activeRevision) {
        var okReload = await loadDefinitionAndRender(activeScriptId);
        if (okReload) activeRevision = stillThere.revision;
      }
    }

    if (scripts.length > 1) showSwitcher(); else hideSwitcher();

    if (activeScriptId == null) {
      if (scripts.length === 1) {
        activeScriptId = scripts[0].scriptId;
        activeRevision = scripts[0].revision;
        await loadDefinitionAndRender(activeScriptId);
      } else if (scripts.length > 1) {
        // WhatsApp always asks. Its URL carries no store identifier, so a
        // remembered choice would silently pin the agent to one store across
        // every session; Meta keeps remembering because its key is per-asset.
        var remembered = platform === 'WhatsApp' ? null : GM_getValue(latestPickerKey, null);
        var match = remembered != null ? scripts.filter(function (s) { return s.scriptId === remembered; })[0] : null;
        if (match) {
          activeScriptId = match.scriptId;
          activeRevision = match.revision;
          await loadDefinitionAndRender(activeScriptId);
        } else {
          openPickerForLatest();
        }
      }
      // scripts.length === 0: idle quietly — no bar, no picker, no warning.
    }
  }

  poll();
  setInterval(poll, POLL_MS);
})();
