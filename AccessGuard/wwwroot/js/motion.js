/* Pointer-driven interactions. Native cursor, scrolling and form controls stay intact. */
(() => {
  'use strict';
  const fine=matchMedia('(hover: hover) and (pointer: fine)');
  const reduce=matchMedia('(prefers-reduced-motion: reduce)');
  const nav=document.getElementById('navigation');
  const light=document.createElement('div');light.className='ambient-light';light.setAttribute('aria-hidden','true');document.body.prepend(light);
  const glider=document.createElement('span');glider.className='nav-glider';glider.setAttribute('aria-hidden','true');nav?.prepend(glider);
  let enabled=false,frame=0,surface=null,button=null,navTarget=null;
  const current={x:innerWidth/2,y:innerHeight/3,rx:0,ry:0,mx:0,my:0,lamp:0};
  const goal={...current};
  let localX=0,localY=0,rect=null;
  const clamp=(n,min,max)=>Math.min(max,Math.max(min,n));
  function schedule(){if(enabled&&!frame)frame=requestAnimationFrame(update);}
  function update(){frame=0;let moving=false;
    for(const key of Object.keys(current)){const delta=goal[key]-current[key];if(Math.abs(delta)>.025){current[key]+=delta*.15;moving=true;}else current[key]=goal[key];}
    light.style.setProperty('--cursor-x',current.x+'px');light.style.setProperty('--cursor-y',current.y+'px');
    if(surface){surface.style.setProperty('--light-x',localX+'px');surface.style.setProperty('--light-y',localY+'px');surface.style.setProperty('--tilt-x',current.rx+'deg');surface.style.setProperty('--tilt-y',current.ry+'deg');}
    if(button){button.style.setProperty('--mag-x',current.mx+'px');button.style.setProperty('--mag-y',current.my+'px');}
    document.documentElement.style.setProperty('--lamp-x',current.lamp+'px');
    if(moving)schedule();
  }
  function clearSurface(){if(surface){surface.classList.remove('pointer-inside');for(const k of ['--light-x','--light-y','--tilt-x','--tilt-y'])surface.style.removeProperty(k);}surface=null;rect=null;goal.rx=goal.ry=current.rx=current.ry=0;}
  function clearButton(){if(button){button.style.removeProperty('--mag-x');button.style.removeProperty('--mag-y');}button=null;goal.mx=goal.my=current.mx=current.my=0;}
  function moveGlider(link){if(!enabled||!nav||getComputedStyle(nav).flexDirection==='column'){glider.classList.remove('visible');return;}const item=link||nav.querySelector('[aria-current="page"]');if(!item){glider.classList.remove('visible');return;}const a=nav.getBoundingClientRect(),b=item.getBoundingClientRect();glider.style.width=b.width+'px';glider.style.height=b.height+'px';glider.style.transform=`translate(${b.left-a.left-nav.clientLeft}px,${b.top-a.top-nav.clientTop}px)`;glider.classList.add('visible');}
  function configure(){enabled=fine.matches&&!reduce.matches;document.documentElement.classList.toggle('pointer-motion',enabled);if(!enabled){cancelAnimationFrame(frame);frame=0;clearSurface();clearButton();light.classList.remove('visible');document.documentElement.style.removeProperty('--lamp-x');}moveGlider();}
  document.addEventListener('pointermove',e=>{
    if(!enabled||e.pointerType!=='mouse')return;
    goal.x=e.clientX;goal.y=e.clientY;goal.lamp=clamp((e.clientX-innerWidth/2)/innerWidth*18,-9,9);light.classList.add('visible');
    const next=e.target.closest('.kpi-card,.hanging-card,.setup-card,.role-description,.panel');
    if(next!==surface){clearSurface();surface=next;if(surface){rect=surface.getBoundingClientRect();surface.classList.add('pointer-inside');}}
    if(surface){rect=surface.getBoundingClientRect();localX=e.clientX-rect.left;localY=e.clientY-rect.top;if(surface.classList.contains('hanging-card')&&!surface.matches(':focus-within')){goal.rx=clamp((.5-localY/rect.height)*3,-1.5,1.5);goal.ry=clamp((localX/rect.width-.5)*4,-2,2);}}
    const nextButton=e.target.closest('.button,.lamp-toggle');if(nextButton!==button){clearButton();button=nextButton;}
    if(button){const r=button.getBoundingClientRect();goal.mx=clamp((e.clientX-r.left-r.width/2)*.07,-3,3);goal.my=clamp((e.clientY-r.top-r.height/2)*.1,-2,2);}
    const link=e.target.closest('#navigation .nav-link');if(link!==navTarget){navTarget=link;moveGlider(link);}
    schedule();
  },{passive:true});
  document.documentElement.addEventListener('pointerleave',()=>{clearSurface();clearButton();goal.lamp=0;light.classList.remove('visible');navTarget=null;moveGlider();schedule();});
  nav?.addEventListener('pointerleave',()=>{navTarget=null;moveGlider();});
  nav?.addEventListener('focusin',e=>moveGlider(e.target.closest('.nav-link')));
  nav?.addEventListener('focusout',()=>requestAnimationFrame(()=>moveGlider(nav.contains(document.activeElement)?document.activeElement.closest('.nav-link'):null)));
  addEventListener('scroll',()=>{clearSurface();clearButton();light.classList.remove('visible');},{passive:true});
  addEventListener('blur',()=>{clearSurface();clearButton();light.classList.remove('visible');});
  addEventListener('resize',()=>{clearSurface();clearButton();moveGlider();},{passive:true});
  fine.addEventListener('change',configure);reduce.addEventListener('change',configure);
  document.fonts?.ready.then(()=>moveGlider());configure();
})();
