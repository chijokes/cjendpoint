(function(){
  const appEl = document.getElementById('screen');
  const debugEl = document.getElementById('debug');
  let flow;
  let state = {};
  let current;

  async function loadFlow(){
    try{
      const res = await fetch('../flow.json');
      if(!res.ok) throw new Error('fetch failed');
      flow = await res.json();
    }catch(e){
      debug('Could not load flow.json — ensure preview served from project root');
      throw e;
    }
    current = flow.screens[0].id;
    render();
  }

  function debug(msg){ debugEl.textContent = msg; }

  function getScreen(id){ return flow.screens.find(s=>s.id===id); }

  function render(){
    const screen = getScreen(current);
    if(!screen) return debug('screen not found: '+current);
    appEl.innerHTML = '';

    const card = document.createElement('div'); card.className='card';
    const title = document.createElement('h2'); title.className='screen-title'; title.textContent = screen.title;
    card.appendChild(title);

    const layout = screen.layout || {};
    (layout.children||[]).forEach(child=>{
      const ctrl = renderChild(child, screen.id);
      if(ctrl) card.appendChild(ctrl);
    });

    appEl.appendChild(card);
    debug(`Current: ${current} — state keys: ${Object.keys(state).length}`);
  }

  function renderChild(child, screenId){
    if(child.type === 'RadioButtonsGroup'){
      const wrap = document.createElement('div'); wrap.className='control';
      if(child.label){ const lbl=document.createElement('div'); lbl.textContent=child.label; wrap.appendChild(lbl); }
      const list = document.createElement('div'); list.className='radio-list';
      (child['data-source']||[]).forEach(opt=>{
        const item = document.createElement('label'); item.className='radio-item';
        const input = document.createElement('input'); input.type='radio'; input.name=child.name; input.value = opt.id;
        if(state[child.name] === opt.id) input.checked = true;
        const span = document.createElement('div'); span.innerHTML = `<strong>${opt.title}</strong>` + (opt.description?`<div style="font-size:13px;color:#666">${opt.description}</div>`:'');
        item.appendChild(input); item.appendChild(span);
        list.appendChild(item);
      });
      wrap.appendChild(list);
      return wrap;
    }

    if(child.type === 'TextInput'){
      const wrap = document.createElement('div'); wrap.className='control';
      if(child.label){ const lbl=document.createElement('div'); lbl.textContent=child.label; wrap.appendChild(lbl); }
      const input = document.createElement('input'); input.className='input'; input.name = child.name; input.value = state[child.name] || '';
      wrap.appendChild(input);
      return wrap;
    }

    if(child.type === 'Footer'){
      const row = document.createElement('div'); row.className='footer-row';
      const btn = document.createElement('button'); btn.className='btn'; btn.textContent = child.label || 'Next';
      btn.onclick = ()=> handleFooter(child);
      const back = document.createElement('button'); back.className='btn secondary'; back.textContent='Back';
      back.onclick = ()=> goBack();
      row.appendChild(back); row.appendChild(btn);
      return row;
    }

    return null;
  }

  function collectCurrentInputs(screen){
    const container = appEl.querySelector('.card');
    if(!container) return;
    // radios
    container.querySelectorAll('input[type=radio]').forEach(r=>{
      if(r.checked) state[r.name] = r.value;
    });
    // text
    container.querySelectorAll('input[type=text]').forEach(i=>{
      state[i.name] = i.value;
    });
  }

  const historyStack = [];
  function goTo(next){ historyStack.push(current); current = next; render(); }
  function goBack(){ if(historyStack.length) current = historyStack.pop(); render(); }

  function handleFooter(footer){
    try{
      collectCurrentInputs(current);
      const action = footer['on-click-action'] || { name: 'data_exchange' };
      if(action.name === 'navigate' && action.next && (action.next.type === 'screen')){
        goTo(action.next.name); return;
      }
      if(action.name === 'data_exchange'){
        const nexts = flow.routing_model[current] || [];
        if(nexts.length) { goTo(nexts[0]); return; }
        debug('No next screen defined for data_exchange'); return;
      }
      if(action.name === 'complete'){
        showResult(); return;
      }
      debug('Unhandled action: '+action.name);
    }catch(e){ debug('Action error: '+e.message); }
  }

  function showResult(){
    appEl.innerHTML = '';
    const out = document.createElement('div'); out.className='card';
    const t = document.createElement('h3'); t.textContent='Flow result (collected data)'; out.appendChild(t);
    const pre = document.createElement('pre'); pre.className='result-pre'; pre.textContent = JSON.stringify(state, null, 2); out.appendChild(pre);
    const btn = document.createElement('button'); btn.className='btn'; btn.textContent='Restart'; btn.onclick = ()=>{ state={}; current = flow.screens[0].id; historyStack.length=0; render(); };
    out.appendChild(btn);
    appEl.appendChild(out);
  }

  loadFlow().catch(e=>{ appEl.innerHTML = '<div class="card">Failed to load flow.json. Make sure flow.json is at project root and you opened Live Server from CjEndpoint folder.</div>'; console.error(e); });
})();