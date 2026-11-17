const vm=require('vm'),fs=require('fs'),assert=require('assert');
(async()=>{
 let tick,commands=[],snapshot,next=0,paused=0,played=[];
 const source=fs.readFileSync(__dirname+'/hournerd-bridge.js','utf8').replace('__HOURNERD_BRIDGE_KEY__','a'.repeat(64));
 const p={data:{item:{metadata:{title:'Fixture song',artist_name:'Fixture artist',album_title:'Fixture album',image_xlarge_url:'spotify:image:abcdef'}}},isPlaying:()=>true,getProgress:()=>1500,getDuration:()=>200000,next:()=>next++,back:()=>{},pause:()=>paused++,play:()=>{},togglePlay:()=>{},toggleHeart:()=>{},playUri:uri=>played.push(uri)};
 const context={window:{},Spicetify:{Player:p,Platform:{RootlistAPI:{getContents:async()=>({items:[{name:'Fixture playlist',uri:'spotify:playlist:1234567890123456789012'}]})}},CosmosAsync:{get:async()=>({tracks:{items:[{name:'Search song',uri:'spotify:track:1234567890123456789012',artists:[{name:'Test'}]}]},playlists:{items:[null]}})}},document:{querySelector:()=>({})},setInterval:fn=>{tick=fn;return 1;},clearInterval:()=>{},setTimeout:()=>1,clearTimeout:()=>{},AbortController,fetch:async(url,options)=>{
  assert.strictEqual(url,'http://127.0.0.1:48372/exchange');assert.strictEqual(options.headers['X-HourNerd-Bridge'],'a'.repeat(64));snapshot=JSON.parse(options.body);const batch=commands;commands=[];return {ok:true,json:async()=>({commands:batch})};
 }};context.window=context;context.addEventListener=()=>{};
 vm.runInNewContext(source,context);
 commands=Array.from({length:20},()=>({name:'next'}));await tick();commands=Array.from({length:20},()=>({name:'next'}));await tick();assert.strictEqual(next,40);assert.strictEqual(snapshot.title,'Fixture song');assert.strictEqual(snapshot.artwork,'https://i.scdn.co/image/abcdef');assert(!('token' in snapshot));console.log('PASS forty next commands without a local skip counter; metadata only');
 commands=[{name:'library'}];await tick();await tick();assert(snapshot.entries.some(e=>e.name==='Fixture playlist'));assert(snapshot.entries.some(e=>e.uri==='spotify:collection:tracks'));console.log('PASS playlist normalization and liked songs');
 commands=[{name:'search',value:'Fixture'}];await tick();await tick();assert.strictEqual(snapshot.entries[0].name,'Search song');console.log('PASS search results and null playlist rows');
 commands=[{name:'playUri',value:'file:///secret'},{name:'playUri',value:'spotify:track:1234567890123456789012'},{name:'pause'}];await tick();assert.strictEqual(played.length,1);assert.strictEqual(paused,1);console.log('PASS URI allowlist and pause handoff');
 p.data=null;await tick();console.log('PASS connection is possible before a track has loaded');
})().catch(error=>{console.error(error);process.exitCode=1;});
