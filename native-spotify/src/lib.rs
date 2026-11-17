//! Platform-independent queue and content selection. No account-tier or skip quota.
use serde::{Deserialize, Serialize};
use serde_json::Value;

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq)]
pub struct Entry { pub name: String, pub detail: String, pub uri: String }

pub fn valid_uri(uri: &str, kind: &str) -> bool {
    let prefix=format!("spotify:{kind}:");
    uri.strip_prefix(&prefix).is_some_and(|id| id.len()==22 && id.bytes().all(|c| c.is_ascii_alphanumeric()))
}
pub fn identified_ad(value: &Value) -> bool {
    value.as_object().is_some_and(|map| map.iter().any(|(key,value)| {
        let key=key.to_ascii_lowercase();
        (matches!(key.as_str(),"is_ad"|"isad"|"advertisement"|"is_advertisement") && (value==true || value=="true" || value=="1")) ||
        (matches!(key.as_str(),"type"|"content_type"|"media_type") && matches!(value.as_str(),Some("ad"|"advertisement"|"commercial"))) ||
        matches!(key.as_str(),"metadata"|"attributes"|"item") && identified_ad(value)
    }))
}
pub fn music_uri(value: &Value) -> Option<String> {
    if identified_ad(value) { return None; }
    let uri=value.get("uri").or_else(||value.get("link"))?.as_str()?;
    valid_uri(uri,"track").then(||uri.to_owned())
}
pub fn collect_context_tracks(value: &Value, output: &mut Vec<String>) {
    if output.len()>=5000 || identified_ad(value) { return; }
    if let Some(uri)=music_uri(value) { output.push(uri); return; }
    match value {
        Value::Array(items)=>for item in items {collect_context_tracks(item,output);},
        Value::Object(map)=>for key in ["pages","tracks","items","item","contents"] {if let Some(v)=map.get(key){collect_context_tracks(v,output);}},
        _=>{}
    }
}
#[derive(Default)]
pub struct MusicQueue { tracks: Vec<String>, index: Option<usize> }
impl MusicQueue {
    pub fn replace(&mut self, tracks: Vec<String>, selected: &str) {
        self.tracks=tracks.into_iter().filter(|u|valid_uri(u,"track")).take(5000).collect();
        self.index=self.tracks.iter().position(|u|u==selected).or(if self.tracks.is_empty(){None}else{Some(0)});
    }
    pub fn current(&self)->Option<&str>{self.index.and_then(|i|self.tracks.get(i)).map(String::as_str)}
    pub fn advance(&mut self)->Option<String>{self.index=self.index.and_then(|i|(i+1<self.tracks.len()).then_some(i+1));self.current().map(str::to_owned)}
    pub fn previous(&mut self)->Option<String>{self.index=self.index.map(|i|i.saturating_sub(1));self.current().map(str::to_owned)}
}
#[cfg(test)] mod tests {
    use super::*;use serde_json::json;
    fn uri(i:usize)->String {format!("spotify:track:{i:022}")}
    #[test] fn skips_have_no_counter_or_time_quota(){let mut q=MusicQueue::default();q.replace((0..250).map(uri).collect(),&uri(0));for i in 1..250{assert_eq!(q.advance(),Some(uri(i)));}assert_eq!(q.advance(),None);}
    #[test] fn ads_and_non_music_never_enter_queue(){let input=json!({"pages":[{"tracks":[{"uri":uri(1)},{"uri":uri(2),"metadata":{"is_ad":"true"}},{"uri":"spotify:ad:1234567890123456789012"},{"uri":"spotify:episode:1234567890123456789012"},{"uri":uri(3),"type":"commercial"}]}]});let mut v=vec![];collect_context_tracks(&input,&mut v);assert_eq!(v,vec![uri(1)]);}
    #[test] fn legitimate_titles_are_not_ad_flags(){assert_eq!(music_uri(&json!({"uri":uri(1),"name":"Advertisement"})),Some(uri(1)));}
    #[test] fn playlists_urls_files_and_bad_ids_cannot_play_as_tracks(){for u in ["file:///secret","https://example.com","spotify:track:bad","spotify:playlist:1234567890123456789012"]{assert!(!valid_uri(u,"track"));}}
    #[test] fn exhausted_queue_stops_without_refetching(){let mut q=MusicQueue::default();q.replace(vec![uri(0)],&uri(0));for _ in 0..100{assert_eq!(q.advance(),None);}}
}
