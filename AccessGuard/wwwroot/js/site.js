(async () => {
  'use strict';
  const shell = document.querySelector('[data-page]');
  if (!shell) return;
  const page = shell.dataset.page;
  const content = document.getElementById('page-content');
  const roles = ['Administrateur','Auditeur','Employé'];
  try{localStorage.removeItem('accessguard-demo-v1');}catch{}
  const permissions = ['Consulter les utilisateurs','Gérer les utilisateurs','Attribuer des rôles','Consulter le journal','Exporter le journal','Gérer les permissions'];
  let token='',db={users:[],logs:[],matrix:[],profile:{name:'',email:''},permissions:[],meta:{}};
  async function api(url,method='GET',body){const response=await fetch(url,{method,credentials:'same-origin',headers:{...(body?{'Content-Type':'application/json'}:{}),...(method!=='GET'?{'X-CSRF-TOKEN':token}:{})},...(body?{body:JSON.stringify(body)}:{})});const data=await response.json().catch(()=>({}));if(!response.ok){if(response.status===401&&!url.startsWith('/api/auth/'))location.assign('/Portal/Login');throw new Error(data.message||Object.values(data.errors||{}).flat().join(' ')||'Cette action est indisponible.');}return data;}
  try{const session=await api('/api/auth/session');token=session.token;window.AccessGuardSession=session;
    if(session.bootstrap&&page!=='initialize'&&page!=='status'){location.replace('/Portal/Initialize');return;}
    if(!session.bootstrap&&page==='initialize'){location.replace('/Portal/Login');return;}
    if(!session.authenticated&&!['login','initialize','status'].includes(page)){location.replace('/Portal/Login');return;}
    if(session.authenticated){if(['login','initialize'].includes(page)){location.replace('/');return;}const start=performance.now();db=await api('/api/workspace');db.latency=Math.round(performance.now()-start);}
  }catch(e){content.innerHTML='<p class="form-message" role="alert">Connexion au serveur impossible. Rechargez la page.</p>';return;}
  window.AccessGuardState=db;
  const can=p=>db.permissions.includes(p);
  async function reload(){db=await api('/api/workspace');window.AccessGuardState=db;dispatchEvent(new Event('accessguard:datachange'));}
  const esc = value => String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const normalizeQuery = value => String(value).normalize('NFD').replace(/\p{Diacritic}/gu,'').trim().replace(/\s+/g,' ').toLocaleLowerCase('fr');
  const initials = name => name.trim().split(/\s+/).slice(0,2).map(x=>x[0]).join('').toUpperCase();
  let toastTimer;
  function toast(message){const t=document.getElementById('toast');t.textContent=message;t.classList.add('visible');clearTimeout(toastTimer);toastTimer=setTimeout(()=>t.classList.remove('visible'),4000);}
  const date = t => new Intl.DateTimeFormat('fr-FR',{dateStyle:'short',timeStyle:'short',timeZone:'UTC'}).format(new Date(t));
  const badge = (text,style) => `<span class="badge ${style}">${esc(text)}</span>`;
  const resultBadge = text => badge(text,text==='Succès'?'success':(text==='Échec'||text==='Bloqué')?'danger':'warning');
  const person = u => `<div class="person"><span class="avatar">${esc(initials(u.name))}</span><div><strong>${esc(u.name)}</strong><small>${esc(u.email)}</small></div></div>`;
  const heading = (title,sub,action='') => `<div class="page-heading"><div><p class="section-label">ACCESSGUARD / GESTION DES IDENTITÉS</p><h1>${title}</h1><p class="subtitle">${sub}</p></div>${action}</div>`;
  function auditTable(logs){return `<div class="table-wrap"><table><thead><tr><th>Utilisateur</th><th>Événement</th><th>Ressource</th><th>Date et heure UTC</th><th>Résultat</th><th>Adresse IP</th></tr></thead><tbody>${logs.length?logs.map(l=>`<tr class="audit-row" data-result="${esc(l.result)}"><td><strong>${esc(l.user)}</strong></td><td>${esc(l.action)}</td><td>${esc(l.resource)}</td><td class="timestamp">${date(l.time)}</td><td>${resultBadge(l.result)}</td><td class="mono">${esc(l.ip||'—')}</td></tr>`).join(''):'<tr><td colspan="6" class="empty">Aucun événement ne correspond aux filtres.</td></tr>'}</tbody></table></div>`;}
  function dashboard(){
    const active=db.users.filter(u=>u.status==='Actif'),missing=active.filter(u=>!u.mfa),issues=db.logs.filter(l=>l.result!=='Succès'),coverage=active.length?Math.round((active.length-missing.length)/active.length*100):0;
    content.innerHTML=`<div class="page-heading"><div><p class="section-label">VOTRE ORGANISATION, EN UN REGARD</p><h1>Vue d’ensemble</h1><p class="subtitle">Les identités à suivre. Les accès à comprendre. Les décisions à prendre.</p></div><a class="button primary" href="/Portal/Users">Gérer les utilisateurs <span aria-hidden="true">↗</span></a></div>
    <dl class="overview-summary"><div><dt>Comptes actifs</dt><dd>${active.length}<small>sur ${db.users.length} utilisateurs</small></dd></div><div><dt>Rôles définis</dt><dd>${roles.length}<small>profils de permissions</small></dd></div><div><dt>Couverture 2FA</dt><dd>${coverage}<span>%</span><small>${active.length-missing.length} comptes actifs configurés</small></dd></div><div><dt>Événements à examiner</dt><dd class="attention-number">${issues.length}<small>échecs, blocages et refus</small></dd></div></dl>
    <div class="overview-grid"><section class="panel priority-panel"><div class="panel-header"><div><h2>Vos priorités</h2><p>Commencez par les points qui demandent une revue.</p></div><span class="priority-count">${[missing.length,issues.length].filter(n=>n>0).length} revues</span></div><div class="priority-body"><a class="priority-item" href="/Portal/Users?review=mfa"><span class="priority-symbol">◉</span><span><strong>${missing.length} compte(s) sans 2FA</strong><small>Comptes actifs · configuration 2FA</small></span><span class="arrow">↗</span></a><a class="priority-item" href="/Portal/Audit?review=unsuccessful"><span class="priority-symbol rose-symbol">!</span><span><strong>${issues.length} événement(s) non abouti(s)</strong><small>Examiner les traces du journal</small></span><span class="arrow">↗</span></a><div class="priority-context"><span class="context-dot"></span><p>Une revue commence par le contexte.<br><strong>Ces indicateurs reflètent les comptes visibles selon vos permissions.</strong></p></div></div></section>
    <section class="panel recent-panel"><div class="panel-header"><div><h2>Activité récente</h2><p>Dernières traces enregistrées dans votre organisation.</p></div><a href="/Portal/Audit" class="text-link">Tout voir ↗</a></div><div class="event-list">${db.logs.slice(0,5).map(l=>`<div class="event-item"><span class="event-dot ${l.result==='Succès'?'good':'alert'}"></span><div><strong>${esc(l.action)}</strong><small>${esc(l.user)} <span>·</span> ${date(l.time)}</small></div>${resultBadge(l.result)}</div>`).join('')}</div></section></div>
    <section class="review-band"><div class="review-band-icon" aria-hidden="true">↳</div><div><h2>Une règle claire pour chaque rôle.</h2><p>Vérifiez la matrice des permissions avant d’attribuer de nouveaux accès.</p></div><a class="button secondary" href="/Portal/Roles">Examiner les permissions ↗</a></section>`;
  }
  function users(){content.innerHTML=heading('Utilisateurs','Gérez les comptes, leurs rôles et leur statut.',`<button class="button primary" id="add-user">Ajouter un utilisateur</button>`)+`<section class="panel"><div class="toolbar"><input id="user-search" type="search" placeholder="Rechercher un nom ou un e-mail…" aria-label="Rechercher un utilisateur"><select id="role-filter" aria-label="Filtrer par rôle"><option value="">Tous les rôles</option>${roles.map(r=>`<option>${r}</option>`).join('')}</select><select id="status-filter" aria-label="Filtrer par statut"><option value="">Tous les statuts</option><option>Actif</option><option>Désactivé</option></select><select id="mfa-filter" aria-label="Filtrer par 2FA"><option value="">Toute configuration 2FA</option><option value="missing">Sans 2FA</option></select><span class="search-count" id="user-count" aria-live="polite"></span><button type="button" class="text-button user-clear" id="clear-users">Effacer les filtres</button></div><div class="table-wrap"><table><thead><tr><th>Utilisateur</th><th>Rôle</th><th>Statut</th><th>2FA</th><th>Action</th></tr></thead><tbody id="user-rows"></tbody></table></div></section><p class="notice-text">Les comptes et leurs autorisations sont enregistrés sur le serveur.</p>`;
    const params=new URLSearchParams(location.search);
    if(params.get('review')==='mfa'){document.getElementById('mfa-filter').value='missing';document.getElementById('status-filter').value='Actif';}
    if(params.get('status')==='disabled')document.getElementById('status-filter').value='Désactivé';
    const refresh=()=>{const q=normalizeQuery(document.getElementById('user-search').value),r=document.getElementById('role-filter').value,s=document.getElementById('status-filter').value;const found=db.users.filter(u=>normalizeQuery(u.name+' '+u.email).includes(q)&&(!r||u.role===r)&&(!s||u.status===s)&&(document.getElementById('mfa-filter').value!=='missing'||!u.mfa));document.getElementById('user-count').textContent=`${found.length} utilisateur(s)`;document.getElementById('user-rows').innerHTML=found.length?found.map(u=>`<tr><td>${person(u)}</td><td>${badge(u.role,'neutral')}</td><td>${badge(u.status,u.status==='Actif'?'success':'danger')}</td><td>${badge(u.mfa?'Activée':'Non activée',u.mfa?'success':'warning')}</td><td><button class="table-action" data-edit="${esc(u.id)}" ${can('users.write')?'':'hidden'} aria-label="Modifier ${esc(u.name)}">Modifier</button></td></tr>`).join(''):'<tr><td colspan="5" class="empty">Aucun utilisateur trouvé.</td></tr>';};
    ['user-search','role-filter','status-filter','mfa-filter'].forEach(id=>document.getElementById(id).addEventListener('input',refresh));document.getElementById('clear-users').onclick=()=>{for(const id of ['user-search','role-filter','status-filter','mfa-filter'])document.getElementById(id).value='';history.replaceState(null,'',location.pathname);refresh();};document.getElementById('add-user').onclick=()=>openUser();document.getElementById('user-rows').onclick=e=>{const b=e.target.closest('[data-edit]');if(b)openUser(db.users.find(u=>u.id===b.dataset.edit));};refresh();
  }
  const dialog=document.getElementById('user-dialog'), form=document.getElementById('user-form');
  function openUser(u){form.reset();form.elements.id.value=u?.id||'';form.elements.name.value=u?.name||'';form.elements.email.value=u?.email||'';form.elements.role.value=u?.role||'Employé';form.elements.status.value=u?.status||'Actif';form.elements.password.value='';form.elements.password.required=!u;document.getElementById('initial-password-label').hidden=!!u;form.elements.role.disabled=!can('roles.assign');document.getElementById('dialog-title').textContent=u?'Modifier un utilisateur':'Ajouter un utilisateur';document.getElementById('form-error').textContent='';dialog.showModal();form.elements.name.focus();}
  ['close-dialog','cancel-dialog'].forEach(id=>document.getElementById(id).onclick=()=>dialog.close());
  form.onsubmit=async e=>{e.preventDefault();const id=form.elements.id.value;const data={name:form.elements.name.value.trim().replace(/\s+/g,' '),email:form.elements.email.value.trim().toLowerCase(),role:form.elements.role.value,status:form.elements.status.value,password:form.elements.password.value||null,version:id?db.users.find(u=>u.id===id)?.version:null};const submit=form.querySelector('[type=submit]');submit.disabled=true;try{await api(id?'/api/users/'+encodeURIComponent(id):'/api/users',id?'PUT':'POST',data);await reload();dialog.close();users();applyPermissions();toast('Utilisateur enregistré.');}catch(error){document.getElementById('form-error').textContent=error.message;}finally{submit.disabled=false;}};
  function rolePage(){content.innerHTML=heading('Rôles et permissions','Définissez les responsabilités avec une matrice RBAC lisible.')+`<div class="role-list">${roles.map((r,i)=>`<section class="role-description"><h2>${r}</h2><p>${['Administration des identités et des permissions.','Consultation et export des événements d’audit.','Accès à son espace personnel.'][i]}</p>${badge(db.users.filter(u=>u.role===r).length+' utilisateur(s)','neutral')}</section>`).join('')}</div><section class="panel"><div class="panel-header"><div><h2>Matrice des permissions</h2><p>Modifiez les cases pour préparer vos règles d’accès.</p></div><button id="save-roles" class="button primary">Enregistrer la matrice</button></div><div class="table-wrap"><table class="permission-table"><thead><tr><th>Permission</th>${roles.map(r=>`<th>${r}</th>`).join('')}</tr></thead><tbody>${permissions.map((p,j)=>`<tr><td><strong>${p}</strong></td>${roles.map((r,i)=>`<td><input type="checkbox" data-role="${i}" data-permission="${j}" aria-label="${p} : ${r}" ${db.matrix[i][j]?'checked':''}></td>`).join('')}</tr>`).join('')}</tbody></table></div></section><p class="notice-text">Ces permissions sont vérifiées sur le serveur à chaque requête.</p>`;document.getElementById('save-roles').onclick=async e=>{const matrix=db.matrix.map(r=>[...r]);document.querySelectorAll('[data-permission]').forEach(c=>matrix[Number(c.dataset.role)][Number(c.dataset.permission)]=c.checked);e.currentTarget.disabled=true;try{await api('/api/roles','PUT',{matrix});await reload();toast('Permissions enregistrées sur le serveur.');}catch(error){toast(error.message);}finally{document.getElementById('save-roles').disabled=false;}};document.querySelectorAll('[data-role="0"]').forEach(c=>c.disabled=true);}
  const incidentStatuses = {nouveau:'Nouveau', 'en-cours':'En cours', resolu:'Résolu'};
  const incidentSeverities = {faible:'Faible', moyenne:'Moyenne', elevee:'Élevée', critique:'Critique'};
  const incidentSeverityBadge = severity => badge(incidentSeverities[severity], ({faible:'neutral', moyenne:'warning', elevee:'danger', critique:'danger'})[severity]);
  const incidentStatusBadge = status => badge(incidentStatuses[status], ({nouveau:'neutral', 'en-cours':'warning', resolu:'success'})[status]);
  const statusMessage = error => error instanceof TypeError
    ? 'Le serveur AccessGuard ne répond pas. Vérifiez qu’il est lancé puis réessayez.' : error.message;

  function incidentReview(review){
    if(!review)return '';
    const options = (labels,current) => Object.entries(labels).map(([value,label]) =>
      `<option value="${value}" ${value===current?'selected':''}>${label}</option>`).join('');
    const history = review.history.length ? `<ol class="incident-timeline">${review.history.map(item =>
      `<li><header><strong>${esc(item.actor)}</strong><time>${date(item.time)} UTC</time></header><div class="incident-chip-row">${incidentStatusBadge(item.status)}${incidentSeverityBadge(item.severity)}<span>${esc(item.owner||'Sans responsable')}</span></div>${item.note?`<p>${esc(item.note)}</p>`:''}</li>`).join('')}</ol>`
      : '<p class="muted">Aucune action de triage enregistrée.</p>';
    const form = review.canManage ? `<form id="incident-review-form" class="incident-review-form">
      <div class="security-filters"><label>Statut <select name="status">${options(incidentStatuses,review.status)}</select></label>
      <label>Gravité <select name="severity">${options(incidentSeverities,review.severity)}</select></label>
      <label>Affectation <select name="assignment"><option value="keep">Conserver</option>${review.isMine?'':'<option value="claim">Me l’attribuer</option>'}${review.owner?'<option value="release">Retirer le responsable</option>':''}</select></label></div>
      <label>Note de triage <textarea name="note" rows="3" maxlength="1000" placeholder="Une note est requise pour résoudre l’incident."></textarea></label>
      <div class="incident-form-actions"><button type="submit" class="button primary">Enregistrer le triage</button><p id="incident-review-status" role="status"></p></div>
      </form>` : '';
    return `<section class="incident-review"><h3>Triage</h3><div class="incident-chip-row">${incidentStatusBadge(review.status)}${incidentSeverityBadge(review.severity)}<span>${esc(review.owner||'Sans responsable')}</span></div>${form}
      <details class="incident-history"><summary>Historique du triage (${review.history.length})</summary>${history}</details></section>`;
  }

  function bindIncidents(){
    const panel = document.getElementById('incident-panel');
    const blockTable = document.getElementById('active-block-table');
    const blockRows = document.getElementById('active-block-rows');
    const recent = document.getElementById('recent-incident-rows');
    const empty = document.getElementById('no-active-block');
    const dialog = document.getElementById('incident-detail');
    const detail = document.getElementById('incident-detail-body');
    const refreshButton = document.getElementById('refresh-incidents');
    const filters = document.getElementById('incident-filters');
    const previous = document.getElementById('incident-previous');
    const next = document.getElementById('incident-next');
    const queueStatus = document.getElementById('incident-page-status');
    const status = document.getElementById('incident-status');
    let serverTime = 0, receivedAt = 0, detailRequest = 0, queueRequest = 0, currentPage = 1;

    function updateCountdowns(){
      if(!serverTime)return;
      const now = serverTime + performance.now() - receivedAt;
      let active = 0;
      for(const countdown of blockRows.querySelectorAll('[data-expires]')){
        const seconds = Math.max(0, Math.ceil((Date.parse(countdown.dataset.expires) - now) / 1000));
        countdown.closest('tr').hidden = seconds === 0;
        if(seconds>0)active++;
        countdown.textContent = `${Math.floor(seconds / 60)} min ${String(seconds % 60).padStart(2,'0')} s`;
      }
      blockTable.hidden = active === 0;
      empty.hidden = active !== 0;
    }

    const detailsButton = path => `<button type="button" class="text-button" data-incident-path="${esc(path)}">Voir le détail</button>`;
    async function refreshBlocks(){
      status.textContent = 'Chargement des blocages…';
      try{
        const {checkedAt,blocks} = await api('/api/audit/incidents');
        serverTime = Date.parse(checkedAt);
        receivedAt = performance.now();
        blockRows.innerHTML = blocks.map(block => `<tr><td><strong>${esc(block.target)}</strong></td><td>${esc(block.reason)}</td><td class="timestamp">${date(block.expiresAt)}</td><td class="mono" data-expires="${esc(block.expiresAt)}"></td><td>${detailsButton(block.incidentId ?? `accounts/${encodeURIComponent(block.accountId)}`)}</td></tr>`).join('');
        status.textContent = '';
        updateCountdowns();
      }catch(error){status.textContent = statusMessage(error);}
    }

    async function refreshQueue(page = currentPage){
      const sequence = ++queueRequest;
      const params = new URLSearchParams(new FormData(filters));
      params.set('page', page);
      queueStatus.textContent = 'Chargement des incidents…';
      try{
        const queue = await api(`/api/audit/queue?${params}`);
        if(sequence!==queueRequest)return;
        currentPage = queue.page;
        recent.innerHTML = queue.items.length ? queue.items.map(item => `<tr><td><strong>${esc(item.reason)}</strong><small class="security-table-meta">${date(item.time)}</small></td><td>${esc(item.target)}</td><td>${incidentSeverityBadge(item.severity)}</td><td>${incidentStatusBadge(item.status)}</td><td>${esc(item.owner||'Sans responsable')}</td><td>${detailsButton(item.id)}</td></tr>`).join('') : '<tr><td colspan="6" class="empty">Aucun incident pour ces filtres.</td></tr>';
        queueStatus.textContent = `${queue.total} incident(s) · Page ${queue.page} sur ${queue.pages}`;
        previous.disabled = queue.page<=1;
        next.disabled = queue.page>=queue.pages;
      }catch(error){if(sequence===queueRequest)queueStatus.textContent = statusMessage(error);}
    }

    async function refresh(){
      refreshButton.disabled = true;
      await Promise.all([refreshBlocks(),refreshQueue()]);
      refreshButton.disabled = false;
    }

    async function openDetail(path){
      const sequence = ++detailRequest;
      detail.innerHTML = '<p role="status">Chargement du détail…</p>';
      if(!dialog.open)dialog.showModal();
      try{
        const {incident,total,ips,accounts,attempts,review} = await api(`/api/audit/incidents/${path}`);
        if(sequence!==detailRequest || !dialog.open)return;
        const targets = accounts.map(esc).join(', ') || 'Aucun compte identifié';
        const addresses = ips.map(esc).join(', ') || 'Aucune IP enregistrée';
        const count = total > attempts.length ? `${attempts.length} sur ${total} tentatives` : `${total} tentative(s)`;
        detail.innerHTML = `<h3>${esc(incident.reason)}</h3><p>${esc(incident.target)} · ${date(incident.time)} UTC</p><dl class="incident-facts"><div><dt>Comptes identifiés</dt><dd>${targets}</dd></div><div><dt>IP observées</dt><dd>${addresses}</dd></div></dl>${incidentReview(review)}<h3>Tentatives dans le journal</h3><p>${count}</p>${auditTable(attempts)}`;
        document.getElementById('incident-review-form')?.addEventListener('submit', async event => {
          event.preventDefault();
          const form = event.currentTarget;
          const submit = form.querySelector('button[type="submit"]');
          const message = document.getElementById('incident-review-status');
          submit.disabled = true;
          message.textContent = 'Enregistrement…';
          try{
            const values = Object.fromEntries(new FormData(form));
            const result = await api(`/api/audit/incidents/${path}/review`, 'POST', {...values,version:review.version});
            if(sequence===detailRequest && dialog.open)await openDetail(path);
            toast(result.message);
            await refreshQueue();
          }catch(error){message.textContent = statusMessage(error);}
          finally{submit.disabled = false;}
        });
      }catch(error){
        if(sequence!==detailRequest || !dialog.open)return;
        detail.innerHTML = `<p role="alert">${esc(statusMessage(error))}</p><button class="button secondary" id="retry-incident" type="button">Réessayer</button>`;
        document.getElementById('retry-incident').onclick = () => openDetail(path);
      }
    }

    panel.addEventListener('click', event => {
      const button = event.target.closest('[data-incident-path]');
      if(button)openDetail(button.dataset.incidentPath);
    });
    filters.addEventListener('submit', event => {event.preventDefault();refreshQueue(1);});
    previous.addEventListener('click', () => refreshQueue(currentPage-1));
    next.addEventListener('click', () => refreshQueue(currentPage+1));
    refreshButton.addEventListener('click', refresh);
    setInterval(updateCountdowns, 1000);
    refresh();
  }

  async function loadSecurityOverview(){
    const indicators = document.getElementById('security-indicators');
    const chart = document.getElementById('security-chart');
    const rules = document.getElementById('security-policy');
    try{
      const {days,coverage,policy} = await api('/api/audit/metrics');
      const total = key => days.reduce((sum,day) => sum + day[key], 0);
      const stat = (label,value,detail) => `<div class="security-stat"><span>${label}</span><strong>${value}</strong><small>${detail}</small></div>`;
      indicators.innerHTML = stat('Connexions réussies',total('accepted'),'7 derniers jours')
        + stat('Refus journalisés',total('refused'),'7 derniers jours')
        + stat('Alertes de connexion',total('alerts'),'7 derniers jours')
        + stat('Comptes protégés par 2FA',`${coverage.protected}/${coverage.active}`,`${coverage.protectedAdmins}/${coverage.admins} administrateurs protégés`);
      const max = Math.max(1,...days.map(day => day.accepted+day.refused));
      const label = day => new Intl.DateTimeFormat('fr-FR',{weekday:'short',day:'numeric',month:'short',timeZone:'UTC'}).format(new Date(day.date+'T00:00:00Z'));
      const bar = (value,name) => `<span class="security-bar-${name}" style="--bar-size:${value/max*100}%"></span>`;
      chart.innerHTML = `<div class="table-wrap"><table class="security-activity"><thead><tr><th>Jour</th><th>Connexions</th><th>Réussites</th><th>Refus</th><th>Alertes</th></tr></thead><tbody>${days.map(day => `<tr><th scope="row">${label(day)}</th><td><div class="security-bar" aria-hidden="true">${bar(day.accepted,'success')}${bar(day.refused,'blocked')}</div></td><td>${day.accepted}</td><td>${day.refused}</td><td>${day.alerts}</td></tr>`).join('')}</tbody></table></div>`;
      const duration = seconds => seconds%60===0 ? `${seconds/60} min` : `${seconds} s`;
      rules.innerHTML = `<dl class="security-rules"><div><dt>Verrouillage d’un compte</dt><dd>${policy.accountFailures} échecs · ${duration(policy.accountBlockSeconds)} de blocage</dd></div><div><dt>Brute force par IP</dt><dd>${policy.ipFailures} échecs en ${duration(policy.windowSeconds)} · ${duration(policy.blockSeconds)} de blocage de l’IP</dd></div><div><dt>Brute force distribuée</dt><dd>${policy.distributedFailures} échecs depuis ${policy.distributedIps} IP en ${duration(policy.windowSeconds)} · blocage du compte pour ${duration(policy.blockSeconds)}</dd></div><div><dt>Password spraying suspecté</dt><dd>${policy.sprayAccounts} comptes visés par une même IP en ${duration(policy.windowSeconds)} · blocage de l’IP pour ${duration(policy.blockSeconds)}</dd></div></dl>`;
    }catch(error){chart.textContent = statusMessage(error);rules.textContent = 'Paramètres indisponibles.';}
  }

  function audit(){
    const search = document.getElementById('audit-search');
    const result = document.getElementById('result-filter');
    const from = document.getElementById('audit-from');
    const to = document.getElementById('audit-to');
    const count = document.getElementById('audit-count');
    const exportButton = document.getElementById('export-audit');
    let auditSequence = 0;
    const auditParams = () => new URLSearchParams({query:search.value.trim(),result:result.value,from:from.value,to:to.value});
    const refresh = async () => {
      const sequence = ++auditSequence;
      try{
        const response = await api('/api/audit?'+auditParams());
        if(sequence!==auditSequence)return;
        document.getElementById('audit-table').innerHTML = auditTable(response.logs);
        count.textContent = `${response.total} événement(s) · 500 derniers au maximum`;
      }catch(error){if(sequence===auditSequence)count.textContent = statusMessage(error);}
    };
    document.addEventListener('keydown',event => {
      if(event.key==='/'&&!document.querySelector('dialog[open]')&&!['INPUT','TEXTAREA','SELECT'].includes(document.activeElement.tagName)){
        event.preventDefault();search.focus();
      }
    });
    if(new URLSearchParams(location.search).get('review')==='unsuccessful')result.value = 'unsuccessful';
    document.getElementById('audit-filters').addEventListener('submit',event => {event.preventDefault();refresh();});
    document.getElementById('clear-audit').onclick = () => {
      document.getElementById('audit-filters').reset();
      history.replaceState(null,'',location.pathname);
      refresh();
    };
    exportButton.hidden = !can('audit.export');
    exportButton.onclick = () => {location.assign('/api/audit/export?'+auditParams());};
    bindIncidents();
    loadSecurityOverview();
    refresh();
  }
  function account(){content.innerHTML=heading('Mon compte','Votre profil et les options de sécurité du compte.')+`<div class="account-grid"><section class="panel"><div class="panel-header"><h2>Informations personnelles</h2>${badge('Compte Identity','neutral')}</div><div class="panel-body"><span class="avatar large-avatar">${esc(initials(db.profile.name))}</span><form id="profile-form" class="profile-form"><label>Nom complet<input name="name" required maxlength="80" value="${esc(db.profile.name)}"></label><label>E-mail<input name="email" type="email" required maxlength="120" value="${esc(db.profile.email)}"></label><label>Rôle<input value="${esc(db.profile.role||'Employé')}" readonly></label><label>Mot de passe actuel<input name="password" type="password" required autocomplete="current-password"></label><p id="profile-error" class="form-message" role="alert"></p><button class="button primary">Enregistrer le profil</button><button type="button" id="logout" class="button secondary">Se déconnecter</button></form></div></section><section class="panel"><div class="panel-header"><h2>Authentification à deux facteurs</h2>${badge(db.users[0].mfa?'Activée · Identity':'Désactivée · Identity',db.users[0].mfa?'success':'warning')}</div><div class="panel-body"><h2>Un second niveau de vérification</h2><p class="muted">Associez Google Authenticator pour sécuriser votre connexion.</p><div class="security-note">Le second facteur est vérifié par Identity sur le serveur. Conservez vos codes de récupération.</div><a href="/Portal/Setup2FA" class="button primary">Configurer Google Authenticator ↗</a><p class="notice-text">QR code et vérification TOTP côté serveur.</p><a href="/Portal/Login" class="text-link">Tester la connexion →</a></div></section></div>`;
    document.getElementById('profile-form').onsubmit=async e=>{e.preventDefault();const f=e.target;try{await api('/api/account/profile','POST',{name:f.elements.name.value.trim(),email:f.elements.email.value.trim(),password:f.elements.password.value});await reload();account();toast('Profil enregistré.');}catch(error){document.getElementById('profile-error').textContent=error.message;}};
    document.getElementById('logout').onclick=async()=>{try{await api('/api/auth/logout','POST',{});location.assign('/Portal/Login');}catch(error){toast(error.message);}};
  }
  function login(){
    const loginForm=document.getElementById('login-form'),code=document.getElementById('login-code');let challenge=false;
    code.required=false;code.hidden=true;document.getElementById('login-code-label').hidden=true;
    document.getElementById('lamp-toggle').onclick=e=>{const lit=!shell.classList.toggle('lamp-off');e.currentTarget.setAttribute('aria-pressed',String(lit));e.currentTarget.setAttribute('aria-label',lit?'Éteindre la lampe':'Allumer la lampe');};
    document.getElementById('show-password').onclick=e=>{const input=document.getElementById('login-password');input.type=input.type==='password'?'text':'password';e.currentTarget.textContent=input.type==='password'?'Voir':'Masquer';};
    loginForm.onsubmit=async e=>{e.preventDefault();const error=document.getElementById('login-error'),submit=loginForm.querySelector('[type=submit]');error.textContent='';submit.disabled=true;try{if(!challenge){const result=await api('/api/auth/login','POST',{email:loginForm.elements.email.value.trim(),password:loginForm.elements.password.value});if(result.requiresTwoFactor){challenge=true;code.hidden=false;code.required=true;document.getElementById('login-code-label').hidden=false;loginForm.elements.email.readOnly=true;loginForm.elements.password.value='';loginForm.elements.password.required=false;loginForm.elements.password.disabled=true;submit.textContent='Vérifier le code';document.getElementById('login-demo-note').textContent='Saisissez le code de votre application, ou cochez le mode récupération.';const label=document.createElement('label');label.innerHTML='<input type="checkbox" id="use-recovery"> Utiliser un code de récupération';loginForm.insertBefore(label,submit);document.getElementById('use-recovery').onchange=e=>{code.classList.toggle('code-field',!e.target.checked);code.pattern=e.target.checked?'.{6,80}':'[0-9]{6}';code.maxLength=e.target.checked?80:6;code.inputMode=e.target.checked?'text':'numeric';code.value='';};code.focus();return;}}else{await api('/api/auth/twofactor','POST',{code:code.value,recovery:document.getElementById('use-recovery')?.checked||false});}location.assign('/');}catch(err){error.textContent=err.message;}finally{submit.disabled=false;}};
  }
  function initialize(){document.getElementById('bootstrap-form').onsubmit=async e=>{e.preventDefault();const f=e.target,b=f.querySelector('button');b.disabled=true;try{await api('/api/auth/bootstrap','POST',{name:f.elements.name.value.trim(),email:f.elements.email.value.trim(),password:f.elements.password.value});location.assign('/Portal/Login');}catch(error){document.getElementById('bootstrap-error').textContent=error.message;}finally{b.disabled=false;}};}
  async function setup(){
    let key=null,uri=null;const user=db.users.find(u=>u.id===db.profile.id);
    const state=document.getElementById('setup-state'),error=document.getElementById('setup-error'),prepare=document.getElementById('regenerate-secret');
    document.getElementById('setup-form').querySelector('button').disabled=!!user.mfa;
    if(user.mfa){state.textContent='La 2FA est activée sur votre compte.';prepare.textContent='2FA déjà activée';prepare.disabled=true;}
    prepare.onclick=async()=>{error.textContent='';try{const result=await api('/api/account/twofactor/prepare','POST',{password:document.getElementById('setup-password').value});key=result.secret;uri=result.uri;document.getElementById('setup-secret').textContent=key.match(/.{1,4}/g).join(' ');await window.AccessGuardQR.toCanvas(document.getElementById('setup-qr'),uri,{width:228,margin:4,errorCorrectionLevel:'M',color:{dark:'#0b0f19',light:'#e2d9cc'}});document.getElementById('setup-qr').hidden=false;document.getElementById('qr-placeholder').hidden=true;state.textContent='Scannez la clé puis confirmez le code.';}catch(err){error.textContent=err.message;}};
    document.getElementById('copy-secret').onclick=async()=>{if(!key){toast('Préparez votre clé en confirmant votre mot de passe.');return;}try{await navigator.clipboard.writeText(key);toast('Clé copiée.');}catch{toast('Sélectionnez la clé pour la copier.');}};
    document.getElementById('setup-form').onsubmit=async e=>{e.preventDefault();if(!key){error.textContent='Confirmez d’abord votre mot de passe pour préparer la clé.';return;}try{const result=await api('/api/account/twofactor/enable','POST',{code:e.target.elements.code.value});key=null;uri=null;document.getElementById('setup-password').value='';document.getElementById('setup-secret').textContent='Clé masquée après activation';document.getElementById('setup-qr').hidden=true;document.getElementById('qr-placeholder').hidden=false;document.getElementById('qr-placeholder').textContent='2FA activée';const box=document.getElementById('recovery-codes');box.hidden=false;box.replaceChildren();const title=document.createElement('h2');title.textContent='Codes de récupération — à conserver maintenant';box.append(title);const note=document.createElement('p');note.textContent='Chaque code est utilisable une fois. Ils ne seront plus affichés après rechargement.';box.append(note);const pre=document.createElement('pre');pre.textContent=result.recoveryCodes.join('\n');box.append(pre);state.textContent='Double authentification activée.';await reload();e.target.querySelector('button').disabled=true;prepare.disabled=true;toast('2FA activée sur le serveur.');}catch(err){error.textContent=err.message;}};
    if(user.mfa){const disable=document.createElement('form');disable.className='disable-2fa';disable.innerHTML='<h2>Désactiver la double authentification</h2><label>Code actuel<input name="code" class="code-field" pattern="[0-9]{6}" maxlength="6" required inputmode="numeric"></label><button class="button secondary">Désactiver la 2FA</button>';state.after(disable);disable.onsubmit=async e=>{e.preventDefault();try{await api('/api/account/twofactor/disable','POST',{password:document.getElementById('setup-password').value,code:disable.elements.code.value});location.reload();}catch(err){error.textContent=err.message;}};}
  }
  function applyPermissions(){
    const routes={users:'users.read',roles:'roles.write',audit:'audit.read'};
    document.querySelectorAll('[data-dock-id]').forEach(a=>{const p=routes[a.dataset.dockId];a.hidden=!!p&&!can(p);});
    const add=document.getElementById('add-user');if(add&&!can('users.write'))add.hidden=true;
    document.querySelectorAll('[data-edit]').forEach(b=>b.hidden=!can('users.write'));
    const exportButton=document.getElementById('export-audit');if(exportButton)exportButton.hidden=!can('audit.export');
    if(page==='dashboard'){if(!can('users.read'))document.querySelectorAll('a[href*="/Portal/Users"]').forEach(a=>a.hidden=true);if(!can('audit.read'))document.querySelectorAll('a[href*="/Portal/Audit"]').forEach(a=>a.hidden=true);if(!can('roles.write'))document.querySelectorAll('a[href*="/Portal/Roles"]').forEach(a=>a.hidden=true);if(!can('roles.write')){const band=document.querySelector('.review-band');if(band)band.hidden=true;}if(can('users.read')&&!can('users.write')){const action=document.querySelector('.page-heading a[href="/Portal/Users"]');if(action)action.textContent='Consulter les utilisateurs';}}
  }
  function syncProfile(){document.querySelectorAll('.nav-account .avatar').forEach(el=>el.textContent=initials(db.profile.name));const label=document.querySelector('.user-card .identity-name');if(label)label.textContent=db.profile.name;}
  function render(){syncProfile();({initialize,status:()=>{},desktop:()=>{},dashboard,users,roles:rolePage,audit,account,login,setup}[page]||audit)();}render();applyPermissions();dispatchEvent(new Event('accessguard:datachange'));
})();
