// HourNerd Spotify bridge, GPL-3.0-or-later. Local metadata/controls only.
(function(){
'use strict';
const key='__HOURNERD_BRIDGE_KEY__';
let entries=[],error='',running=false,stopped=false;
const safe=(fn,fallback)=>{try{return fn();}catch(e){return fallback;}};
const clean=v=>typeof v==='string'?v.slice(0,500):'';
const valid=v=>typeof v==='string'&&(/^spotify:(track|album|playlist|artist):[A-Za-z0-9]{22}$/.test(v)||v==='spotify:collection:tracks');
function normalize(rows){return (Array.isArray(rows)?rows:[]).map(r=>{
 if(!r)return null;const p=r.playlist||r.item||r;const uri=p.uri||p.link;
 return valid(uri)?{name:clean(p.name||p.title||'Untitled'),detail:clean(p.artists?.map(a=>a.name).join(', ')||p.owner?.display_name||''),uri}:null;
}).filter(Boolean).slice(0,100);}
async function library(){
 if(Spicetify.Platform?.RootlistAPI?.getContents){const r=await Spicetify.Platform.RootlistAPI.getContents({limit:100});entries=normalize(r.items||r.rows||r);}
 else{const r=await Spicetify.CosmosAsync.get('sp://core-playlist/v1/rootlist?from=0&length=100');entries=normalize(r.rows||r.items||[]);}
 entries.unshift({name:'Liked songs',detail:'Your collection',uri:'spotify:collection:tracks'});
}
async function command(c){
 const player=Spicetify.Player;
 switch(c.name){
 case 'toggle':await player.togglePlay();break;
 case 'play':await player.play();break;
 case 'pause':await player.pause();break;
 case 'next':await player.next();break;
 case 'previous':await player.back();break;
 case 'heart':await player.toggleHeart();break;
 case 'playUri':if(valid(c.value))await player.playUri(c.value);break;
 case 'library':await library();break;
 case 'search':{const q=clean(c.value).trim();if(!q)break;const r=await Spicetify.CosmosAsync.get('https://api.spotify.com/v1/search?type=track,playlist&limit=20&q='+encodeURIComponent(q));entries=normalize(r.tracks?.items).concat(normalize(r.playlists?.items));break;}
 }
}
function snapshot(){
 const p=Spicetify.Player,d=p.data||{},m=d.item?.metadata||{};
 let artwork=clean(m.image_xlarge_url||m.image_large_url||'');
 if(artwork.startsWith('spotify:image:'))artwork='https://i.scdn.co/image/'+artwork.split(':').pop();
 if(!/^https:\/\/i\.scdn\.co\/image\/[A-Za-z0-9]+$/.test(artwork))artwork='';
 return {playing:!!safe(()=>p.isPlaying(),false),title:clean(m.title),artist:clean(m.artist_name),album:clean(m.album_title),artwork,position:Math.max(0,safe(()=>p.getProgress(),0)/1000||0),duration:Math.max(0,safe(()=>p.getDuration(),0)/1000||0),entries,error,adblockLoaded:!!document.querySelector('style.adblockify')};
}
async function tick(){
 if(running||stopped)return;
 if(!window.Spicetify?.Player||!Spicetify.CosmosAsync)return;
 running=true;const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),3000);
 try{
  const r=await fetch('http://127.0.0.1:48372/exchange',{method:'POST',headers:{'Content-Type':'application/json','X-HourNerd-Bridge':key},body:JSON.stringify(snapshot()),signal:controller.signal,credentials:'omit',cache:'no-store'});
  if(r.ok){const reply=await r.json();for(const c of (reply.commands||[]).slice(0,20)){try{await command(c);error='';}catch(e){error='Spotify could not complete the '+clean(c.name)+' command. Try again in Spotify.';}}}
 }catch(e){}finally{clearTimeout(timer);running=false;}
}
const timer=setInterval(tick,750);
window.addEventListener('beforeunload',()=>{stopped=true;clearInterval(timer);});
})();
