use std::{io::{self,Write},sync::Arc,time::Duration};
use anyhow::{anyhow,Result};
use serde::Deserialize;
use serde_json::{Value,json};
use tokio::io::{AsyncBufReadExt,BufReader};
use librespot::{core::{SpotifyUri,authentication::Credentials,config::SessionConfig,session::Session},metadata::{Metadata,Playlist},playback::{audio_backend,config::{AudioFormat,PlayerConfig},mixer::NoOpVolume,player::{Player,PlayerEvent}}};
use librespot_oauth::DeviceAuthClientBuilder;
use protobuf::Message;
use hournerd_spotify::{Entry,MusicQueue,valid_uri,collect_context_tracks};

#[derive(Deserialize)] struct Command { name:String, #[serde(default)] value:String }
struct Engine {session:Option<Session>,player:Option<Arc<Player>>,queue:MusicQueue,search_tracks:Vec<String>,snapshot:Value,http:reqwest::Client}
fn emit(value:Value){let mut out=io::stdout().lock();if serde_json::to_writer(&mut out,&value).is_ok(){let _=out.write_all(b"\n");let _=out.flush();}}
impl Engine {
 fn new()->Self {Self{session:None,player:None,queue:MusicQueue::default(),search_tracks:vec![],snapshot:json!({"connected":false,"playing":false,"title":"","artist":"","album":"","artwork":"","duration":0,"position":0,"entries":[],"error":"Sign in to Spotify to begin."}),http:reqwest::Client::builder().timeout(Duration::from_secs(20)).build().unwrap()}}
 fn state(&self){emit(json!({"type":"state","snapshot":self.snapshot}));}
 async fn sign_in(&mut self,refresh:Option<&str>)->Result<()> {
  self.stop();self.snapshot["error"]=json!("Waiting for Spotify sign-in…");self.state();
  let config=SessionConfig::default();
  let client=DeviceAuthClientBuilder::new(&config.client_id,vec!["streaming","playlist-read-private","user-library-read","user-read-private"]).build()?;
  let token=if let Some(token)=refresh {client.refresh_token_async(token).await?} else {
   let pairing=client.request_device_code_async().await?;
   emit(json!({"type":"pairing","url":pairing.url(),"code":pairing.user_code()}));
   tokio::time::timeout(Duration::from_secs(300),client.poll_for_token_async(&pairing)).await??
  };
  let session=Session::new(config,None);
  tokio::time::timeout(Duration::from_secs(45),session.connect(Credentials::with_access_token(token.access_token),false)).await??;
  // Persist only via the host's OS-protected credential store, never a raw cache file.
  if !token.refresh_token.is_empty(){emit(json!({"type":"credentials","refreshToken":token.refresh_token}));}
  let backend=audio_backend::find(None).ok_or_else(||anyhow!("audio backend missing"))?;
  let config=PlayerConfig{normalisation:true,position_update_interval:Some(Duration::from_secs(1)),..Default::default()};
  let player=Player::new(config,session.clone(),Box::new(NoOpVolume),move||backend(None,AudioFormat::default()));
  self.snapshot["connected"]=json!(true);self.snapshot["account"]=json!(session.username());self.snapshot["error"]=json!("");self.session=Some(session);self.player=Some(player);self.state();Ok(())
 }
 fn stop(&mut self){if let Some(p)=self.player.take(){p.stop();}if let Some(s)=self.session.take(){s.shutdown();}self.snapshot["connected"]=json!(false);self.snapshot["playing"]=json!(false);self.snapshot["entries"]=json!([]);self.queue=MusicQueue::default();self.search_tracks.clear();}
 async fn web(&self,method:reqwest::Method,path:&str)->Result<Value>{
  let session=self.session.as_ref().ok_or_else(||anyhow!("not connected"))?;
  let token=session.login5().auth_token().await?;
  let response=self.http.request(method,format!("https://api.spotify.com/v1/{path}")).bearer_auth(token.access_token).send().await?;
  if !response.status().is_success(){return Err(anyhow!("Spotify refused the request"));}
  if response.status()==204{return Ok(json!({}));}Ok(response.json().await?)
 }
 async fn library(&mut self)->Result<()> {
  let session=self.session.as_ref().ok_or_else(||anyhow!("not connected"))?;
  let bytes=session.spclient().get_rootlist(0,Some(100)).await?;
  let list=librespot::protocol::playlist4_external::SelectedListContent::parse_from_bytes(&bytes)?;
  let contents=list.contents.get_or_default();let mut entries=vec![];
  for (index,item) in contents.items.iter().enumerate(){let uri=item.uri();if !valid_uri(uri,"playlist"){continue;}let name=contents.meta_items.get(index).map(|m|m.attributes.get_or_default().name()).filter(|n|!n.is_empty()).unwrap_or("Playlist");entries.push(Entry{name:name.into(),detail:"Playlist".into(),uri:uri.into()});}
  self.snapshot["entries"]=serde_json::to_value(entries)?;self.state();Ok(())
 }
 async fn search(&mut self,query:&str)->Result<()> {
  let query: String=query.trim().chars().take(200).collect();if query.is_empty(){return Ok(());}
  let url=reqwest::Url::parse_with_params("https://api.spotify.com/v1/search",[("q",query.as_str()),("type","track,playlist"),("limit","20")])?;
  let path=format!("search?{}",url.query().unwrap_or_default());let data=self.web(reqwest::Method::GET,&path).await?;
  let mut entries=vec![];self.search_tracks.clear();
  for kind in ["tracks","playlists"] {if let Some(items)=data[kind]["items"].as_array(){for item in items {let uri=item["uri"].as_str().unwrap_or("");if !(valid_uri(uri,"track")||valid_uri(uri,"playlist"))||hournerd_spotify::identified_ad(item){continue;}let name=item["name"].as_str().unwrap_or("Untitled").to_owned();let detail=item["artists"].as_array().map(|a|a.iter().filter_map(|v|v["name"].as_str()).collect::<Vec<_>>().join(", ")).unwrap_or_else(||"Playlist".into());entries.push(Entry{name,detail,uri:uri.into()});if valid_uri(uri,"track"){self.search_tracks.push(uri.into());}}}}
  self.snapshot["entries"]=serde_json::to_value(entries)?;self.state();Ok(())
 }
 fn load(&mut self,uri:&str)->Result<()> {if !valid_uri(uri,"track"){return Err(anyhow!("not a music track"));}let track=SpotifyUri::from_uri(uri)?;let player=self.player.as_ref().ok_or_else(||anyhow!("not connected"))?;player.load(track,true,0);self.snapshot["error"]=json!("");self.snapshot["position"]=json!(0);Ok(())}
 async fn play_uri(&mut self,uri:&str)->Result<()> {
  if valid_uri(uri,"track"){let tracks=if self.search_tracks.iter().any(|v|v==uri){self.search_tracks.clone()}else{vec![uri.into()]};self.queue.replace(tracks,uri);}
  else if valid_uri(uri,"playlist")||valid_uri(uri,"album") {
   let session=self.session.as_ref().ok_or_else(||anyhow!("not connected"))?;let context=session.spclient().get_context(uri).await?;
   let data:Value=serde_json::from_str(&protobuf_json_mapping::print_to_string(&context)?)?;
   let mut tracks=vec![];collect_context_tracks(&data,&mut tracks);
   if tracks.is_empty()&&valid_uri(uri,"playlist"){let playlist=Playlist::get(session,&SpotifyUri::from_uri(uri)?).await?;tracks=playlist.tracks().filter_map(|t|{let u=t.to_uri();valid_uri(&u,"track").then_some(u)}).collect();}
   self.queue.replace(tracks,"");
  }else{return Err(anyhow!("not a supported music URI"));}
  let first=self.queue.current().ok_or_else(||anyhow!("no playable music tracks"))?.to_owned();self.load(&first)
 }
 fn next(&mut self,previous:bool)->Result<()> {let uri=if previous{self.queue.previous()}else{self.queue.advance()};if let Some(uri)=uri{self.load(&uri)?;}else{if let Some(player)=&self.player{player.stop();}self.snapshot["playing"]=json!(false);self.snapshot["error"]=json!("End of this music queue. Choose another playlist or search result.");self.state();}Ok(())}
 async fn command(&mut self,command:Command)->Result<()> {
  match command.name.as_str(){
   "connect"=>{self.sign_in(None).await?;if self.library().await.is_err(){self.snapshot["error"]=json!("Signed in. Playlists could not load; try My playlists or Search.");self.state();}},
   "restore"=>{self.sign_in(Some(&command.value)).await?;let _=self.library().await;},
   "logout"=>{self.stop();self.snapshot["error"]=json!("Signed out of Spotify.");self.state();},
   "library"=>self.library().await?,"search"=>self.search(&command.value).await?,"playUri"=>self.play_uri(&command.value).await?,
   "next"=>self.next(false)?,"previous"=>self.next(true)?,
   "play"=>self.player.as_ref().ok_or_else(||anyhow!("not connected"))?.play(),
   "pause"=>self.player.as_ref().ok_or_else(||anyhow!("not connected"))?.pause(),
   "toggle"=>{let player=self.player.as_ref().ok_or_else(||anyhow!("not connected"))?;if self.snapshot["playing"]==true{player.pause();}else{player.play();}},
   _=>return Err(anyhow!("unsupported command"))
  }Ok(())
 }
 fn event(&mut self,event:PlayerEvent)->Result<()> {
  match event {
   PlayerEvent::Playing{position_ms,..}=>{self.snapshot["playing"]=json!(true);self.snapshot["position"]=json!(position_ms as f64/1000.);self.snapshot["error"]=json!("");},
   PlayerEvent::Paused{position_ms,..}=>{self.snapshot["playing"]=json!(false);self.snapshot["position"]=json!(position_ms as f64/1000.);},
   PlayerEvent::PositionChanged{position_ms,..}|PlayerEvent::PositionCorrection{position_ms,..}=>self.snapshot["position"]=json!(position_ms as f64/1000.),
   PlayerEvent::TrackChanged{audio_item}=>{if !valid_uri(&audio_item.uri,"track"){return self.next(false);}self.snapshot["title"]=json!(audio_item.name);self.snapshot["artist"]=json!(audio_item.unique_fields.artists_display());self.snapshot["album"]=json!(match &audio_item.unique_fields{librespot::metadata::audio::UniqueFields::Track{album,..}=>album.as_str(),_=>""});self.snapshot["duration"]=json!(audio_item.duration_ms as f64/1000.);self.snapshot["artwork"]=json!(audio_item.covers.first().map(|c|c.url.as_str()).unwrap_or(""));},
   PlayerEvent::EndOfTrack{..}=>return self.next(false),
   PlayerEvent::Unavailable{..}=>{self.snapshot["playing"]=json!(false);self.snapshot["error"]=json!("Spotify could not provide this track's audio. This may be an account/key-delivery restriction. Try another track; no retry loop was started.");},
   PlayerEvent::Stopped{..}=>self.snapshot["playing"]=json!(false),_=>{}
  }self.state();Ok(())
 }
}
#[tokio::main] async fn main(){
 let mut engine=Engine::new();engine.state();let mut input=BufReader::new(tokio::io::stdin()).lines();let mut events=None;
 loop{tokio::select!{
  line=input.next_line()=>{let Ok(Some(line))=line else{break;};if line.len()>65536{continue;}let Ok(command)=serde_json::from_str::<Command>(&line)else{continue;};let name=command.name.clone();if engine.command(command).await.is_err(){if name=="connect"||name=="restore"{engine.stop();engine.snapshot["error"]=json!("Spotify sign-in failed or this account was refused. Use Sign in to Spotify to try again.");}else{engine.snapshot["error"]=json!(format!("Spotify could not complete {name}. Try another track or sign in again."));}engine.state();}if (name=="connect"||name=="restore")&&engine.player.is_some(){events=engine.player.as_ref().map(|p|p.get_player_event_channel());}},
  event=async{match &mut events {Some(events)=>events.recv().await,None=>std::future::pending().await}}=>{if let Some(event)=event {let _=engine.event(event);}else{events=None;}},
 }}engine.stop();
}
