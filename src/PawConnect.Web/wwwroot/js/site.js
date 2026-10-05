/* PawConnect front-end behaviour.
   - No inline event handlers: everything is wired up here with data-* attributes, so the
     Content-Security-Policy can forbid inline script (script-src 'self').
   - Pages work without JavaScript; these scripts only make them nicer (progressive enhancement).
   - User-supplied text is always inserted with textContent, never innerHTML (XSS safe). */
(function () {
  'use strict';

  var csrfToken = (document.querySelector('meta[name="csrf-token"]') || {}).content || '';

  function $(sel, root) { return (root || document).querySelector(sel); }
  function $all(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }
  function el(tag, attrs, text) {
    var e = document.createElement(tag);
    if (attrs) Object.keys(attrs).forEach(function (k) {
      if (k === 'class') e.className = attrs[k]; else e.setAttribute(k, attrs[k]);
    });
    if (text != null) e.textContent = text;
    return e;
  }
  function debounce(fn, ms) {
    var t; return function () { var args = arguments, self = this; clearTimeout(t); t = setTimeout(function () { fn.apply(self, args); }, ms); };
  }
  function storage() { try { return window.localStorage; } catch (e) { return null; } }

  /** fetch wrapper for our API: sends the login cookie + CSRF header and parses ProblemDetails errors. */
  function api(url, options) {
    options = options || {};
    var headers = { 'Accept': 'application/json' };
    if (options.body) headers['Content-Type'] = 'application/json';
    if (options.method && options.method !== 'GET') headers['X-CSRF-TOKEN'] = csrfToken;
    return fetch(url, { method: options.method || 'GET', headers: headers, credentials: 'same-origin', body: options.body ? JSON.stringify(options.body) : undefined })
      .then(function (res) {
        if (res.status === 204) return null;
        return res.json().catch(function () { return null; }).then(function (data) {
          if (!res.ok) {
            var msg = (data && (data.title || data.detail)) || ('Request failed (' + res.status + ')');
            if (data && data.errors) msg = Object.keys(data.errors).map(function (k) { return data.errors[k].join(' '); }).join(' ');
            var err = new Error(msg); err.status = res.status; throw err;
          }
          return data;
        });
      });
  }

  // ---------- Navigation ----------
  var navToggle = $('[data-toggle-nav]');
  if (navToggle) {
    var setNav = function (open) {
      document.getElementById('navlinks').classList.toggle('open', open);
      navToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    };
    navToggle.addEventListener('click', function () {
      setNav(navToggle.getAttribute('aria-expanded') !== 'true');
    });
    // Escape closes the mobile menu and returns focus to its button.
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && navToggle.getAttribute('aria-expanded') === 'true') { setNav(false); navToggle.focus(); }
    });
  }

  // ---------- Toast ----------
  // Confirmations fade after 6 s (paused while hovered or focused); errors stay until dismissed.
  var toast = document.getElementById('toast');
  if (toast) {
    var hideToast = function () { toast.classList.add('hide'); };
    var closeBtn = $('[data-dismiss-toast]', toast);
    if (closeBtn) closeBtn.addEventListener('click', hideToast);
    if (toast.getAttribute('data-sticky') !== 'true') {
      var timer = setTimeout(hideToast, 6000);
      var pause = function () { clearTimeout(timer); };
      var resume = function () { timer = setTimeout(hideToast, 3000); };
      toast.addEventListener('mouseenter', pause); toast.addEventListener('mouseleave', resume);
      toast.addEventListener('focusin', pause); toast.addEventListener('focusout', resume);
    }
  }

  // ---------- Pending state on form submit ----------
  // Disables the submit button and shows a spinner so a double-click can't send a form twice
  // (e.g. two donation pledges). Skipped when another handler cancelled the submit.
  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (e.defaultPrevented || !(form instanceof HTMLFormElement) || form.hasAttribute('data-no-busy')) return;
    if ((form.getAttribute('method') || 'get').toLowerCase() !== 'post') return;
    var button = e.submitter || $('button[type=submit], button:not([type])', form);
    if (!button) return;
    if (form.getAttribute('data-submitting') === 'true') { e.preventDefault(); return; }
    form.setAttribute('data-submitting', 'true');
    // Disable on the next tick so the clicked button's own name/value is still sent.
    setTimeout(function () {
      button.disabled = true;
      button.setAttribute('aria-busy', 'true');
      var busyText = button.getAttribute('data-busy-text');
      if (busyText) { button.setAttribute('data-label', button.textContent); button.textContent = busyText; }
    }, 0);
  });
  // Coming back with the browser's Back button restores the page from cache: re-enable it.
  window.addEventListener('pageshow', function () {
    $all('form[data-submitting]').forEach(function (form) {
      form.removeAttribute('data-submitting');
      $all('[aria-busy="true"]', form).forEach(function (b) {
        b.disabled = false; b.removeAttribute('aria-busy');
        if (b.hasAttribute('data-label')) b.textContent = b.getAttribute('data-label');
      });
    });
  });

  // ---------- Generic helpers ----------
  $all('form[data-confirm]').forEach(function (form) {
    form.addEventListener('submit', function (e) { if (!window.confirm(form.getAttribute('data-confirm'))) e.preventDefault(); });
  });
  $all('[data-print]').forEach(function (b) { b.addEventListener('click', function () { window.print(); }); });

  // ---------- Profile photo gallery ----------
  $all('[data-gallery] [data-photo]').forEach(function (thumb, i, thumbs) {
    thumb.addEventListener('click', function () {
      var main = $('.gallery-main img');
      if (!main) return;
      main.src = thumb.getAttribute('data-photo');
      main.alt = thumb.getAttribute('data-alt') || main.alt;
      thumbs.forEach(function (t) { t.setAttribute('aria-pressed', t === thumb ? 'true' : 'false'); });
    });
  });

  // ---------- Adoption application: steps, review and autosave ----------
  var applyForm = $('form[data-apply]');
  if (applyForm) {
    applyForm.classList.add('js-steps');
    var draftKey = applyForm.getAttribute('data-draft-key');
    var store = storage();
    var fields = $all('input[name^="Form."]:not([type=hidden]), select[name^="Form."], textarea[name^="Form."]', applyForm);
    var status = $('[data-autosave-status]', applyForm);

    // Restore a saved draft into empty fields (server-filled values such as your name win).
    if (store && draftKey) {
      try {
        var draft = JSON.parse(store.getItem(draftKey) || 'null');
        // Drafts hold personal details, so they expire after 7 days on this device.
        if (draft && (!draft._savedAt || Date.now() - draft._savedAt > 7 * 24 * 3600 * 1000)) { store.removeItem(draftKey); draft = null; }
        if (draft) {
          fields.forEach(function (f) { if (draft[f.name] != null && (!f.value || f.tagName === 'SELECT')) f.value = draft[f.name]; });
          if (status) status.textContent = 'Draft restored';
        }
      } catch (e) { /* ignore a corrupt draft */ }
    }
    var saveDraft = debounce(function () {
      if (!store || !draftKey) return;
      var data = { _savedAt: Date.now() };
      fields.forEach(function (f) { data[f.name] = f.value; });
      try { store.setItem(draftKey, JSON.stringify(data)); if (status) status.textContent = 'Draft saved'; } catch (e) { }
    }, 500);
    fields.forEach(function (f) { f.addEventListener('input', saveDraft); f.addEventListener('change', saveDraft); });

    var goStep = function (n) {
      var current = $('.step.active', applyForm);
      // Only move forward if the fields on the current step are valid (uses built-in browser validation messages).
      if (current && n > Number(current.getAttribute('data-step'))) {
        var invalid = $all('input, select, textarea', current).filter(function (f) { return !f.checkValidity(); });
        if (invalid.length) { invalid[0].reportValidity(); return; }
      }
      $all('.step', applyForm).forEach(function (s) { s.classList.toggle('active', Number(s.getAttribute('data-step')) === n); });
      [1, 2, 3].forEach(function (i) {
        var tab = document.getElementById('step-tab-' + i);
        if (!tab) return;
        tab.classList.toggle('active', i === n);
        tab.classList.toggle('done', i < n);
      });
      if (n === 3) buildReview();
      var heading = $('.step.active', applyForm);
      if (heading) heading.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };
    var buildReview = function () {
      var dl = document.getElementById('reviewSummary');
      if (!dl) return;
      dl.textContent = '';
      var val = function (id) { var e = document.getElementById(id); return e && e.value ? e.value : '—'; };
      [['Applicant', val('f_name')], ['Phone', val('f_phone')], ['Email', val('f_email')],
       ['Animal', applyForm.getAttribute('data-animal-name')], ['Home', val('f_home') + ', ' + val('f_own').toLowerCase()],
       ['Other pets', val('f_pets')], ['Why', val('f_why')]].forEach(function (row) {
        dl.appendChild(el('dt', null, row[0]));
        dl.appendChild(el('dd', null, row[1]));
      });
    };
    $all('[data-go-step]', applyForm).forEach(function (b) {
      b.addEventListener('click', function () { goStep(Number(b.getAttribute('data-go-step'))); });
    });
    // After a server-side rejection, open the first step that has an error and focus that field.
    var firstError = $('[aria-invalid="true"], .field-validation-error', applyForm);
    var errorStep = firstError && firstError.closest('.step');
    if (errorStep) {
      var n = Number(errorStep.getAttribute('data-step'));
      $all('.step', applyForm).forEach(function (s) { s.classList.toggle('active', Number(s.getAttribute('data-step')) === n); });
      [1, 2, 3].forEach(function (i) { var t = document.getElementById('step-tab-' + i); if (t) { t.classList.toggle('active', i === n); t.classList.toggle('done', i < n); } });
      var field = $('[aria-invalid="true"]', errorStep);
      if (field) field.focus();
    } else {
      goStep(1);
    }
  }
  // Clear the draft once the application has been accepted.
  var clearDraft = $('[data-clear-draft]');
  if (clearDraft && storage()) { try { storage().removeItem(clearDraft.getAttribute('data-clear-draft')); } catch (e) { } }

  // ---------- Donate: amount chips + impact text ----------
  var donateForm = $('form[data-donate]');
  if (donateForm) {
    var amountInput = document.getElementById('amountInput');
    var updateDonateUI = function () {
      var amt = Number(amountInput.value) || 0;
      var monthly = document.getElementById('freq-monthly');
      var suffix = monthly && monthly.checked ? '/mo' : '';
      var setText = function (id, text) { var e = document.getElementById(id); if (e) e.textContent = text; };
      var shown = amt % 1 === 0 ? String(amt) : amt.toFixed(2);
      setText('donateBtnAmt', shown); setText('donateBtnFreq', suffix);
      setText('impactAmt', shown); setText('impactFreqLbl', suffix);
      var text = 'R' + shown + ' helps cover food, bedding and basic care.';
      if (amt >= 1000) text = 'R' + shown + ' covers a full course of vaccinations and sterilisation for one animal.';
      else if (amt >= 500) text = 'R' + shown + ' covers a month of food for one kennel.';
      else if (amt >= 250) text = 'R' + shown + ' covers two weeks of food and bedding for one kennel.';
      if (suffix) text += ' Given monthly, that adds up fast.';
      setText('impactText', text);
      $all('.amount-chip', donateForm).forEach(function (c) { c.classList.toggle('active', Number(c.getAttribute('data-amount')) === amt); });
    };
    $all('.amount-chip', donateForm).forEach(function (chip) {
      chip.addEventListener('click', function () { amountInput.value = chip.getAttribute('data-amount'); updateDonateUI(); });
    });
    amountInput.addEventListener('input', updateDonateUI);
    $all('input[name="Form.Frequency"]', donateForm).forEach(function (r) { r.addEventListener('change', updateDonateUI); });
    updateDonateUI();
  }

  // ---------- Browse: live filtering through GET /api/animals ----------
  var filterForm = $('form[data-live-filter]');
  if (filterForm) {
    var grid = document.getElementById('animalGrid');
    var count = document.getElementById('resultCount');
    var empty = document.getElementById('emptyHint');
    var lastRequest = 0;

    var renderCard = function (a) {
      var card = el('article', { 'class': 'tag-card' });
      card.appendChild(el('span', { 'class': 'kennel-no' }, 'NO. ' + a.kennelNumber));
      var photo;
      if (a.photoUrl) {
        photo = el('div', { 'class': 'photo-block has-photo' });
        photo.appendChild(el('img', { src: a.photoUrl, alt: 'Photo of ' + a.name, loading: 'lazy' }));
      } else {
        photo = el('div', { 'class': 'photo-block', role: 'img', 'aria-label': a.name + ' (no photo yet)' }, a.emoji);
        photo.style.background = 'linear-gradient(135deg,' + a.colorFrom + ',' + a.colorTo + ')';
      }
      card.appendChild(photo);
      card.appendChild(el('span', { 'class': 'stamp stamp-' + a.status.toLowerCase() }, a.status));
      card.appendChild(el('h3', null, a.name));
      card.appendChild(el('div', { 'class': 'tag-meta' }, (a.breed || a.species) + ' · ' + a.age + ' · ' + a.size));
      var traits = el('div', { 'class': 'traits' });
      if (a.isVaccinated) traits.appendChild(el('span', { 'class': 'trait' }, 'Vaccinated'));
      if (a.goodWithKids) traits.appendChild(el('span', { 'class': 'trait' }, 'Good with kids'));
      if (a.goodWithOtherPets) traits.appendChild(el('span', { 'class': 'trait' }, 'Good with pets'));
      card.appendChild(traits);
      card.appendChild(el('a', { 'class': 'btn btn-ink btn-sm', href: a.profileUrl, 'aria-label': "View " + a.name + "'s profile" }, 'View profile'));
      return card;
    };

    var refresh = function () {
      var params = new URLSearchParams(new FormData(filterForm));
      if (params.get('species') === 'all') params.delete('species');
      if (params.get('size') === 'all') params.delete('size');
      if (!params.get('search')) params.delete('search');
      var pageQuery = params.toString();
      var id = ++lastRequest;
      grid.setAttribute('aria-busy', 'true');
      api('/api/animals?' + params.toString()).then(function (animals) {
        if (id !== lastRequest) return; // a newer request has started; ignore this stale answer
        grid.textContent = '';
        animals.forEach(function (a) { grid.appendChild(renderCard(a)); });
        count.textContent = animals.length + (animals.length === 1 ? ' animal' : ' animals') + ' found';
        empty.hidden = animals.length > 0;
        history.replaceState(null, '', '/Home/Browse' + (pageQuery ? '?' + pageQuery : ''));
      }).catch(function () { filterForm.submit(); }) // fall back to a normal page load
        .then(function () { grid.removeAttribute('aria-busy'); });
    };
    filterForm.addEventListener('change', refresh);
    var searchBox = document.getElementById('f-search');
    if (searchBox) searchBox.addEventListener('input', debounce(refresh, 300));
    filterForm.addEventListener('submit', function (e) { e.preventDefault(); refresh(); });
  }

  // ---------- Medical history (staff only): GET /api/animals/{id}?include=history, POST /api/medical-records ----------
  var medical = $('[data-medical]');
  if (medical) {
    var animalId = medical.getAttribute('data-animal-id');
    var list = document.getElementById('medicalList');
    var form = document.getElementById('medicalForm');
    var errorBox = document.getElementById('medicalError');

    var fmtDate = function (iso) {
      if (!iso) return '';
      var d = new Date(iso + 'T00:00:00');
      return d.toLocaleDateString('en-ZA', { day: 'numeric', month: 'short', year: 'numeric' });
    };
    var renderRecord = function (r) {
      var li = el('li');
      li.appendChild(el('div', { 'class': 'tl-date mono' }, fmtDate(r.recordDate)));
      var body = el('div');
      var title = el('strong', null, r.title);
      body.appendChild(title);
      body.appendChild(el('span', { 'class': 'badge' }, r.recordType));
      if (r.details) body.appendChild(el('div', { 'class': 'pre' }, r.details));
      var meta = [];
      if (r.vetName) meta.push('Vet: ' + r.vetName);
      if (r.recordedBy) meta.push('Logged by ' + r.recordedBy);
      if (r.nextDueDate) meta.push('Next due ' + fmtDate(r.nextDueDate));
      if (meta.length) body.appendChild(el('div', { 'class': 'hint' }, meta.join(' · ')));
      li.appendChild(body);
      return li;
    };
    var load = function () {
      api('/api/animals/' + animalId + '?include=history').then(function (data) {
        list.textContent = '';
        var records = data.medicalHistory || [];
        if (!records.length) list.appendChild(el('li', { 'class': 'hint' }, 'No medical records yet.'));
        records.forEach(function (r) { list.appendChild(renderRecord(r)); });
      }).catch(function (err) {
        list.textContent = '';
        list.appendChild(el('li', { 'class': 'hint' }, 'Could not load medical history: ' + err.message));
      });
    };
    load();

    form.addEventListener('submit', function (e) {
      e.preventDefault();
      if (!form.checkValidity()) { form.reportValidity(); return; }
      errorBox.hidden = true;
      var v = function (name) { var f = form.elements[name]; return f && f.value ? f.value : null; };
      var button = $('button[type=submit]', form);
      button.disabled = true;
      api('/api/medical-records', {
        method: 'POST',
        body: { animalId: animalId, recordType: v('recordType'), title: v('title'), details: v('details'), vetName: v('vetName'), recordDate: v('recordDate'), nextDueDate: v('nextDueDate') }
      }).then(function () {
        form.reset();
        form.elements.recordDate.value = form.elements.recordDate.max;
        load();
      }).catch(function (err) {
        errorBox.textContent = err.message;
        errorBox.hidden = false;
      }).then(function () { button.disabled = false; });
    });
  }
})();
