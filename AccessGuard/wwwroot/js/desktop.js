(() => {
  'use strict';
  const clock=document.getElementById('desktop-clock');
  const updateClock=()=>{if(clock){const now=new Date();clock.textContent=new Intl.DateTimeFormat('fr-FR',{hour:'2-digit',minute:'2-digit'}).format(now);clock.dateTime=now.toISOString();clock.title=new Intl.DateTimeFormat('fr-FR',{dateStyle:'full'}).format(now);}};
  updateClock();let timer=setInterval(updateClock,30000);addEventListener('pagehide',()=>clearInterval(timer));addEventListener('pageshow',e=>{if(e.persisted){updateClock();clearInterval(timer);timer=setInterval(updateClock,30000);}});
  const windowEl=document.getElementById('assistant-window'),stage=document.getElementById('desktop');
  const restore=()=>{if(!windowEl){location.assign('/#desktop');return;}windowEl.hidden=false;document.getElementById('desktop-reopen').hidden=true;document.getElementById('restore-assistant').classList.add('is-active');windowEl.scrollIntoView({block:'nearest',behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'instant':'smooth'});document.getElementById('desktop-command-input').focus({preventScroll:true});};
  document.getElementById('restore-assistant')?.addEventListener('click',restore);
  if(!windowEl)return;
  const hide=()=>{windowEl.hidden=true;document.getElementById('desktop-reopen').hidden=false;document.getElementById('restore-assistant').classList.remove('is-active');document.getElementById('desktop-reopen').focus({preventScroll:true});};
  document.getElementById('minimize-window').onclick=hide;document.getElementById('close-window').onclick=hide;document.getElementById('desktop-reopen').onclick=restore;
  document.getElementById('maximize-window').onclick=e=>{const maximized=windowEl.classList.toggle('is-maximized');stage.classList.toggle('window-maximized',maximized);e.currentTarget.setAttribute('aria-pressed',String(maximized));e.currentTarget.setAttribute('aria-label',maximized?'Restaurer la taille du guide':'Agrandir le guide');};
  const routes=[{words:['utilisateur','compte','identit'],label:'les utilisateurs',href:'/Portal/Users'},{words:['audit','journal','événement','evenement'],label:'le journal d’audit',href:'/Portal/Audit'},{words:['2fa','authenticator','sécurit','securit'],label:'la configuration 2FA',href:'/Portal/Setup2FA'},{words:['permission','rôle','role','rbac'],label:'la matrice des permissions',href:'/Portal/Roles'}];
  const form=document.getElementById('desktop-command');
  function respond(command){const text=command.trim().toLocaleLowerCase('fr');if(!text)return;const route=routes.find(r=>r.words.some(w=>text.includes(w)));const messages=document.getElementById('assistant-messages');messages.replaceChildren();const message=document.createElement('p');message.className='assistant-message';if(route){message.append('Voici le raccourci vers '+route.label+'. ');const link=document.createElement('a');link.href=route.href;link.textContent='Ouvrir →';message.append(link);}else message.textContent='Je suis un guide local. Choisissez utilisateurs, audit, permissions ou 2FA pour ouvrir le bon écran.';messages.append(message);}
  form.onsubmit=e=>{e.preventDefault();respond(form.elements.command.value);};document.querySelectorAll('[data-command]').forEach(b=>b.onclick=()=>{form.elements.command.value=b.dataset.command;respond(b.dataset.command);});
})();
