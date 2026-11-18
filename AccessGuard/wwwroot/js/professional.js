/* AccessGuard motion system: stable hit areas, bounded springs, no scroll interception. */
(() => {
 'use strict';
 const reduced=matchMedia('(prefers-reduced-motion: reduce)'),fine=matchMedia('(hover: hover) and (pointer: fine)');
 const normalize=s=>String(s).normalize('NFD').replace(/\p{Diacritic}/gu,'').trim().replace(/\s+/g,' ').toLocaleLowerCase('fr');
 const dock=document.querySelector('.magnetic-dock');
 const entries=[...document.querySelectorAll('.dock-item')].map(link=>({link,tile:link.querySelector('.dock-tile'),s:1,vs:0,y:0,vy:0,x:0,vx:0,targetS:1,targetY:0,targetX:0}));
 let frame=0,last=0;
 const motionAllowed=()=>!reduced.matches&&fine.matches;
 const request=()=>{if(!frame&&motionAllowed())frame=requestAnimationFrame(step);};
 function step(now){frame=0;const dt=last?Math.min((now-last)/1000,.024):1/60;last=now;let moving=false;
   for(const e of entries){
     // Spring: mass 1, stiffness 650, damping 40. Slight follow-through; settles in under 300ms.
     for(const [position,velocity,target] of [['s','vs','targetS'],['y','vy','targetY'],['x','vx','targetX']]){
       const acceleration=650*(e[target]-e[position])-40*e[velocity];e[velocity]+=acceleration*dt;e[position]+=e[velocity]*dt;
       const tolerance=position==='s'?.002:.04;
       if(Math.abs(e[target]-e[position])>tolerance||Math.abs(e[velocity])>tolerance*15)moving=true;else{e[position]=e[target];e[velocity]=0;}
     }
     e.tile.style.setProperty('--dock-x',e.x.toFixed(2)+'px');e.tile.style.setProperty('--dock-scale',e.s.toFixed(4));e.tile.style.setProperty('--dock-y',e.y.toFixed(2)+'px');
   }
   if(moving)request();else last=0;
 }
 function resetTargets(){for(const e of entries){e.targetS=1;e.targetY=e.targetX=0;}request();}
 function magnify(x){if(!motionAllowed())return;for(const e of entries){const r=e.link.getBoundingClientRect(),distance=Math.abs(x-r.left-r.width/2),weight=Math.exp(-(distance*distance)/(2*66*66));const compact=innerWidth<=700;e.targetS=1+(compact?.15:.38)*weight;e.targetY=7*weight;e.targetX=Math.sign(r.left+r.width/2-x)*weight*(compact?0:3);}request();}
 dock?.addEventListener('pointermove',e=>{if(e.pointerType==='mouse')magnify(e.clientX);},{passive:true});
 dock?.addEventListener('pointerleave',resetTargets);
 dock?.addEventListener('focusin',e=>{const link=e.target.closest('.dock-item');if(link){const r=link.getBoundingClientRect();magnify(r.left+r.width/2);}});
 dock?.addEventListener('focusout',()=>{if(!dock.contains(document.activeElement))resetTargets();});
 function clearMotion(){if(!motionAllowed()){cancelAnimationFrame(frame);frame=0;last=0;for(const e of entries){e.s=e.targetS=1;e.y=e.targetY=e.x=e.targetX=e.vx=e.vs=e.vy=0;e.tile.style.removeProperty('--dock-x');e.tile.style.removeProperty('--dock-scale');e.tile.style.removeProperty('--dock-y');}}}
 reduced.addEventListener('change',clearMotion);fine.addEventListener('change',clearMotion);addEventListener('blur',resetTargets);addEventListener('resize',resetTargets,{passive:true});
 document.addEventListener('pointerdown',e=>{const link=e.target.closest('.dock-item');if(!link||reduced.matches)return;link.querySelector('svg').animate([{transform:'translateY(0) scale(1)'},{transform:'translateY(2px) scale(1.08,.91)',offset:.35},{transform:'translateY(-1px) scale(.98,1.04)',offset:.75},{transform:'none'}],{duration:220,easing:'cubic-bezier(.2,.8,.2,1)'});});
 // Rare moments get more expression; repeated navigation and table filtering stay quiet.
 const toast=document.getElementById('toast');if(toast)new MutationObserver(()=>{if(toast.classList.contains('visible')&&!reduced.matches){toast.getAnimations().forEach(a=>a.cancel());toast.animate([{opacity:0,transform:'translate(7px,5px) scale(.98)'},{opacity:1,transform:'translate(-1px,-1px) scale(1.005)',offset:.75},{opacity:1,transform:'none'}],{duration:230,easing:'cubic-bezier(.2,.8,.2,1)'});}}).observe(toast,{attributes:true,attributeFilter:['class']});
 document.addEventListener('invalid',e=>{if(e.target.matches('input,select')&&!reduced.matches)e.target.animate([{transform:'translateX(0)'},{transform:'translateX(-3px)',offset:.25},{transform:'translateX(2px)',offset:.6},{transform:'none'}],{duration:160,easing:'ease-out'});},true);
 // Tolerate pasted formatting for codes without ever saving passwords.
 document.addEventListener('paste',e=>{if(e.target.matches('.code-field')){e.preventDefault();e.target.value=(e.clipboardData.getData('text')||'').replace(/\D/g,'').slice(0,6);e.target.dispatchEvent(new Event('input',{bubbles:true}));}});
 document.addEventListener('input',e=>{if(e.target.matches('.code-field'))e.target.value=e.target.value.replace(/\D/g,'').slice(0,6);});
 document.addEventListener('focusout',e=>{if(e.target.matches('input[type=email]'))e.target.value=e.target.value.trim().toLowerCase();});
 function updateBadges(){const state=window.AccessGuardState;const badge=document.getElementById('dock-alerts');if(!badge)return;const count=state?.meta?.alerts||0;badge.hidden=!count;badge.textContent=count>99?'99+':String(count);badge.closest('a').setAttribute('aria-label',`Audit, ${count} événement(s) à examiner`);}
 updateBadges();addEventListener('accessguard:datachange',updateBadges);
 const dialog=document.getElementById('command-palette');
 if(dialog){
  const destinations=[['Vue d’ensemble','/','Accueil et priorités'],['Utilisateurs','/Portal/Users','Comptes et identités'],['Permissions','/Portal/Roles','Rôles et matrice RBAC'],['Journal d’audit','/Portal/Audit','Événements et export CSV'],['Sécurité 2FA','/Portal/Setup2FA','Google Authenticator'],['Mon compte','/Portal/Account','Profil et sécurité'],['Connexion','/Portal/Login','Accès au portail']];
  const search=document.getElementById('command-search'),results=document.getElementById('command-results');let selected=0;
  function select(index){const links=[...results.querySelectorAll('a')];selected=(index+links.length)%Math.max(links.length,1);links.forEach((a,i)=>{a.classList.toggle('selected',i===selected);a.setAttribute('aria-selected',String(i===selected));});if(links[selected])search.setAttribute('aria-activedescendant',links[selected].id);else search.removeAttribute('aria-activedescendant');}
  function renderResults(){results.replaceChildren();const q=normalize(search.value);const found=destinations.filter(d=>{const required={'/Portal/Users':'users.read','/Portal/Roles':'roles.write','/Portal/Audit':'audit.read'}[d[1]];return !required||window.AccessGuardState?.permissions?.includes(required);}).filter(d=>normalize(d[0]+' '+d[2]).includes(q));for(const d of found){const a=document.createElement('a');a.className='command-result';a.id='command-option-'+destinations.indexOf(d);a.setAttribute('role','option');a.tabIndex=-1;a.href=d[1];const title=document.createElement('span');title.textContent=d[0];const detail=document.createElement('small');detail.textContent=d[2];a.append(title,detail);results.append(a);}if(!found.length){const p=document.createElement('p');p.className='empty';p.textContent='Aucun écran trouvé. Essayez « audit » ou « utilisateurs ».';results.append(p);}select(0);}
  function openCommand(){renderResults();dialog.showModal();search.focus();}
  document.getElementById('open-command').onclick=openCommand;document.querySelectorAll('[data-open-search]').forEach(b=>b.onclick=openCommand);document.getElementById('close-command').onclick=()=>dialog.close();
  dialog.addEventListener('close',()=>search.removeAttribute('aria-activedescendant'));search.addEventListener('input',renderResults);search.addEventListener('keydown',e=>{if(e.key==='ArrowDown'){e.preventDefault();select(selected+1);}if(e.key==='ArrowUp'){e.preventDefault();select(selected-1);}if(e.key==='Enter'){e.preventDefault();results.querySelector('a.selected')?.click();}});
  document.addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='k'){if(document.documentElement.classList.contains('first-entry'))return;e.preventDefault();if(dialog.open)dialog.close();else{search.value='';openCommand();}}});
 }
 document.querySelectorAll('#navigation a').forEach(a=>a.addEventListener('click',()=>{document.getElementById('navigation').classList.remove('open');document.getElementById('menu-toggle').setAttribute('aria-expanded','false');}));
 function improveTables(){document.querySelectorAll('.table-wrap').forEach((wrap,index)=>{const overflow=wrap.scrollWidth>wrap.clientWidth+1;wrap.tabIndex=overflow?0:-1;wrap.setAttribute('role','region');wrap.setAttribute('aria-label','Tableau de données'+(overflow?' : défilement horizontal disponible':''));let hint=wrap.previousElementSibling;if(!hint?.classList.contains('table-scroll-hint')){hint=document.createElement('p');hint.className='table-scroll-hint';hint.textContent='Faites défiler le tableau horizontalement pour voir toutes les colonnes ↔';wrap.before(hint);}hint.hidden=!overflow;});}
 const pageContent=document.getElementById('page-content');if(pageContent)new MutationObserver(improveTables).observe(pageContent,{childList:true,subtree:true});addEventListener('resize',improveTables,{passive:true});improveTables();
 const marker=document.querySelector('.entry-screen');if(marker){
   const shell=document.querySelector('.app-shell'),dock=document.querySelector('.magnetic-dock'),skip=document.querySelector('.skip-link');let done=false;const timers=[];
   const finish=()=>{if(done)return;done=true;timers.forEach(clearTimeout);shell.inert=false;if(dock)dock.inert=false;if(skip)skip.inert=false;document.body.classList.remove('entry-running');marker.remove();document.documentElement.classList.remove('first-entry');document.getElementById('main-content').focus({preventScroll:true});};
   if(!document.documentElement.classList.contains('first-entry')||reduced.matches){marker.remove();document.documentElement.classList.remove('first-entry');}
   else{
     shell.inert=true;if(dock)dock.inert=true;if(skip)skip.inert=true;document.body.classList.add('entry-running');marker.focus({preventScroll:true});
     const stage=document.getElementById('entry-stage');
     for(const [delay,text] of [[900,'IDENTITÉS & COMPTES'],[2250,'RÔLES & PERMISSIONS'],[3600,'ACTIVITÉ & TRAÇABILITÉ']])timers.push(setTimeout(()=>{stage.textContent=text;if(!reduced.matches)stage.animate([{opacity:.3,transform:'translateY(3px)'},{opacity:1,transform:'none'}],{duration:180,easing:'ease-out'});},delay));
     marker.addEventListener('animationend',e=>{if(e.target===marker&&e.animationName==='entry-out')finish();});timers.push(setTimeout(finish,5200));
     reduced.addEventListener('change',()=>{if(reduced.matches)finish();},{once:true});
   }
 }
})();
