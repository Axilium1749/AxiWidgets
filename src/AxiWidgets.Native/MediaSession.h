#include <winrt/Windows.Media.Control.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.Storage.Streams.h>
#include <mutex>
#include <condition_variable>
#include <thread>
#include <deque>
using namespace Windows::Media::Control;
struct MediaSnapshot {wchar_t title[256]{},artist[256]{};double position{},duration{},rate{1};int playing{},available{},controls{};UINT color{0xff70bfff};int error{};DWORD process{};};
namespace Media {
std::mutex mutex;std::condition_variable wake;std::set<HWND> listeners;std::thread worker;
bool quit=false,dirty=true,metadata=true;std::deque<std::pair<int,double>> requests;MediaSnapshot snapshot;
void Signal(bool art=false){std::lock_guard lock(mutex);dirty=true;metadata|=art;wake.notify_one();}
void Failure(hresult_error const& e){std::lock_guard lock(mutex);snapshot.error=e.code();for(auto hwnd:listeners)PostMessage(hwnd,WM_APP+71,0,0);}
void Run(){
 init_apartment(apartment_type::multi_threaded);
 GlobalSystemMediaTransportControlsSessionManager manager{nullptr};GlobalSystemMediaTransportControlsSession session{nullptr};
 event_token current{},properties{},playback{},timeline{};std::map<uint64_t,UINT> cache;
 auto detach=[&]{if(session){session.MediaPropertiesChanged(properties);session.PlaybackInfoChanged(playback);session.TimelinePropertiesChanged(timeline);session=nullptr;}};
 try{
 manager=GlobalSystemMediaTransportControlsSessionManager::RequestAsync().get();current=manager.CurrentSessionChanged([](auto&&,auto&&){Signal(true);});
 for(;;){bool art;std::deque<std::pair<int,double>> commands;{std::unique_lock lock(mutex);wake.wait(lock,[]{return quit||dirty||!requests.empty();});if(quit)break;dirty=false;art=metadata;metadata=false;commands.swap(requests);}
 try{
 auto next=manager.GetCurrentSession();if(next!=session){detach();session=next;art=true;if(session){properties=session.MediaPropertiesChanged([](auto&&,auto&&){Signal(true);});playback=session.PlaybackInfoChanged([](auto&&,auto&&){Signal();});timeline=session.TimelinePropertiesChanged([](auto&&,auto&&){Signal();});}}
 MediaSnapshot value;{std::lock_guard lock(mutex);value=snapshot;}value.error=0;
 if(!session){value=MediaSnapshot{};AudioReactive::SetSource(0);}
 else{
 for(auto [action,pos]:commands){bool ok=false;switch(action){case 0:ok=session.TryTogglePlayPauseAsync().get();break;case 1:ok=session.TrySkipPreviousAsync().get();break;case 2:ok=session.TrySkipNextAsync().get();break;case 3:ok=session.TryChangePlaybackPositionAsync(static_cast<int64_t>(pos*10000000)).get();break;}if(!ok)OutputDebugStringW(L"AxiWidgets: media command rejected by session\n");}
 auto info=session.GetPlaybackInfo();auto time=session.GetTimelineProperties();value.available=1;value.playing=info.PlaybackStatus()==GlobalSystemMediaTransportControlsSessionPlaybackStatus::Playing;
 value.position=time.Position().count()/10000000.;value.duration=time.EndTime().count()/10000000.;value.rate=info.PlaybackRate()?info.PlaybackRate().Value():1;
 if(value.playing)value.position+=std::max(0.,(winrt::clock::now()-time.LastUpdatedTime()).count()/10000000.)*value.rate;
 auto c=info.Controls();value.controls=(c.IsPlayPauseToggleEnabled()?1:0)|(c.IsPreviousEnabled()?2:0)|(c.IsNextEnabled()?4:0)|(c.IsPlaybackPositionEnabled()?8:0);
 if(art){value.process=AudioReactive::ResolveSource(session.SourceAppUserModelId().c_str());AudioReactive::SetSource(value.process);auto p=session.TryGetMediaPropertiesAsync().get();wcsncpy_s(value.title,p.Title().c_str(),_TRUNCATE);wcsncpy_s(value.artist,p.Artist().c_str(),_TRUNCATE);
 DWORD accent=0;BOOL opaque=false;DwmGetColorizationColor(&accent,&opaque);value.color=0xff000000|(accent&0xffffff);
 try{if(p.Thumbnail()){auto stream=p.Thumbnail().OpenReadAsync().get();auto decoder=Windows::Graphics::Imaging::BitmapDecoder::CreateAsync(stream).get();Windows::Graphics::Imaging::BitmapTransform transform;transform.ScaledWidth(32);transform.ScaledHeight(32);
 auto data=decoder.GetPixelDataAsync(Windows::Graphics::Imaging::BitmapPixelFormat::Rgba8,Windows::Graphics::Imaging::BitmapAlphaMode::Straight,transform,Windows::Graphics::Imaging::ExifOrientationMode::IgnoreExifOrientation,Windows::Graphics::Imaging::ColorManagementMode::DoNotColorManage).get().DetachPixelData();
 uint64_t hash=1469598103934665603ull;for(auto b:data)hash=(hash^b)*1099511628211ull;
 if(cache.contains(hash))value.color=cache[hash];else{double best=0;for(uint32_t i=0;i+3<data.size();i+=4){double r=data[i],g=data[i+1],b=data[i+2],hi=std::max({r,g,b}),lo=std::min({r,g,b});double score=(hi-lo)*(1-std::abs(hi-195)/255);if(data[i+3]>200&&hi>95&&hi-lo>55&&score>best){best=score;double gain=std::max(1.,190/hi);value.color=0xff000000|(UINT(std::min(255.,r*gain))<<16)|(UINT(std::min(255.,g*gain))<<8)|UINT(std::min(255.,b*gain));}}if(cache.size()>=32)cache.erase(cache.begin());cache[hash]=value.color;}
 }}catch(hresult_error const& e){Failure(e);}
 }
 }
 {std::lock_guard lock(mutex);snapshot=value;for(auto hwnd:listeners)PostMessage(hwnd,WM_APP+71,0,0);}
 }catch(hresult_error const& e){Failure(e);}
 }
 detach();manager.CurrentSessionChanged(current);
 }catch(hresult_error const& e){Failure(e);}
 uninit_apartment();
}
}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiMedia_Attach(HWND hwnd,BOOL attach) noexcept{return Guard([&]{
 bool stop=false;{std::lock_guard lock(Media::mutex);if(attach){Media::listeners.insert(hwnd);Media::dirty=Media::metadata=true;Media::quit=false;if(!Media::worker.joinable())Media::worker=std::thread(Media::Run);Media::wake.notify_one();}else{Media::listeners.erase(hwnd);if(Media::listeners.empty()){Media::quit=true;stop=true;Media::wake.notify_one();}}}if(stop&&Media::worker.joinable())Media::worker.join();
});}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiMedia_Read(MediaSnapshot* value) noexcept{if(!value)return E_POINTER;std::lock_guard lock(Media::mutex);*value=Media::snapshot;return S_OK;}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiMedia_Command(int action,double position) noexcept{std::lock_guard lock(Media::mutex);if(Media::requests.size()<16)Media::requests.emplace_back(action,position);Media::wake.notify_one();return S_OK;}
