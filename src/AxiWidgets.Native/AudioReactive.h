#pragma once
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <ksmedia.h>
#include <mutex>
#include <thread>
#include <set>

namespace AudioReactive {
struct BandFilter {
 double previous=0,hp=0,low1=0,low2=0;
 double Process(double sample,double highPass,double lowPass){
  hp=highPass*(hp+sample-previous);previous=sample;
  low1+=lowPass*(hp-low1);low2+=lowPass*(low1-low2);return low2;
 }
};
constexpr int BandCount=6;
// Visual gain only: 50% input reaches the previous full-level sensitivity.
// Apply before adaptive normalization, so loud tracks keep their dynamic range.
constexpr double SensitivityGain=2.0;
constexpr double Centers[BandCount]{60,160,450,1200,3200,8000};
// Low-frequency RMS is naturally smaller; a common .008 floor hid quiet bass.
constexpr double BandFloors[BandCount]{.0015,.0025,.008,.008,.008,.008};
struct FrequencyBand {
 double b0=0,b2=0,a1=0,a2=0,z1=0,z2=0;
 explicit FrequencyBand(double hz=60){double w=6.283185307179586*hz/48000,alpha=std::sin(w)/(2*1.1),a0=1+alpha;b0=alpha/a0;b2=-b0;a1=-2*std::cos(w)/a0;a2=(1-alpha)/a0;}
 double Process(double x){double y=b0*x+z1;z1=-a1*y+z2;z2=b2*x-a2*y;return y;}
};
struct Snapshot {float bands[BandCount]{};float level=0,bass=0,hit=0;HRESULT error=S_OK;uint64_t packets=0;std::chrono::steady_clock::time_point at{};};
std::mutex gate;Snapshot latest;std::set<HWND> users;std::thread worker;
HANDLE stopEvent=nullptr,deviceEvent=nullptr;
struct DeviceChanges:winrt::implements<DeviceChanges,IMMNotificationClient>{
 HRESULT __stdcall OnDefaultDeviceChanged(EDataFlow flow,ERole role,LPCWSTR) noexcept override {if(flow==eRender&&role==eMultimedia)SetEvent(deviceEvent);return S_OK;}
 HRESULT __stdcall OnDeviceStateChanged(LPCWSTR,DWORD) noexcept override {return S_OK;}
 HRESULT __stdcall OnDeviceAdded(LPCWSTR) noexcept override {SetEvent(deviceEvent);return S_OK;}
 HRESULT __stdcall OnDeviceRemoved(LPCWSTR) noexcept override {return S_OK;}
 HRESULT __stdcall OnPropertyValueChanged(LPCWSTR,PROPERTYKEY) noexcept override {return S_OK;}
};
void Run(){
 winrt::init_apartment(winrt::apartment_type::multi_threaded);
 try{
 auto enumerator=winrt::create_instance<IMMDeviceEnumerator>(__uuidof(MMDeviceEnumerator));
 auto notifications=winrt::make_self<DeviceChanges>();winrt::check_hresult(enumerator->RegisterEndpointNotificationCallback(notifications.get()));
 winrt::handle samples{CreateEvent(nullptr,FALSE,FALSE,nullptr)};
 if(!samples)winrt::throw_last_error();
 while(WaitForSingleObject(stopEvent,0)!=WAIT_OBJECT_0){
 bool restart=false;
 try{
 winrt::com_ptr<IMMDevice> device;winrt::check_hresult(enumerator->GetDefaultAudioEndpoint(eRender,eMultimedia,device.put()));
 winrt::com_ptr<IAudioClient> client;winrt::check_hresult(device->Activate(__uuidof(IAudioClient),CLSCTX_ALL,nullptr,client.put_void()));
 // Ask the shared audio engine for a fixed float format; no per-packet format conversion/allocations.
 WAVEFORMATEX format{WAVE_FORMAT_IEEE_FLOAT,2,48000,384000,8,32,0};
 winrt::check_hresult(client->Initialize(AUDCLNT_SHAREMODE_SHARED,AUDCLNT_STREAMFLAGS_LOOPBACK|AUDCLNT_STREAMFLAGS_EVENTCALLBACK|AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM|AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY,0,0,&format,nullptr));
 winrt::check_hresult(client->SetEventHandle(samples.get()));winrt::com_ptr<IAudioCaptureClient> capture;winrt::check_hresult(client->GetService(__uuidof(IAudioCaptureClient),capture.put_void()));
 winrt::check_hresult(client->Start());BandFilter filters[2];FrequencyBand bank[2][BandCount];double baseline[BandCount]{};float response[BandCount]{};
 for(auto& channel:bank)for(int band=0;band<BandCount;band++)channel[band]=FrequencyBand(Centers[band]);
 double baselineBass=.005,baselinePeak=.02;
 constexpr double hp=0.99543,lp=0.02177; // 35 Hz high-pass + two 170 Hz low-pass stages at 48 kHz.
 HANDLE events[]{stopEvent,deviceEvent,samples.get()};
 for(;;){DWORD event=WaitForMultipleObjects(3,events,FALSE,INFINITE);if(event==WAIT_OBJECT_0||event==WAIT_OBJECT_0+1){restart=event==WAIT_OBJECT_0+1;break;}if(event!=WAIT_OBJECT_0+2)winrt::throw_last_error();
 UINT frames=0;winrt::check_hresult(capture->GetNextPacketSize(&frames));
 while(frames){BYTE* bytes=nullptr;DWORD flags=0;winrt::check_hresult(capture->GetBuffer(&bytes,&frames,&flags,nullptr,nullptr));
 double peak=0,energy=0;double bandEnergy[BandCount]{};bool silent=(flags&AUDCLNT_BUFFERFLAGS_SILENT)!=0;
 if(flags&AUDCLNT_BUFFERFLAGS_DATA_DISCONTINUITY){filters[0]={};filters[1]={};for(auto& channel:bank)for(int band=0;band<BandCount;band++)channel[band]=FrequencyBand(Centers[band]);}
 auto data=reinterpret_cast<float const*>(bytes);
 for(UINT i=0;i<frames;i++)for(int channel=0;channel<2;channel++){
 double sample=silent?0:static_cast<double>(data[i*2+channel]);if(!std::isfinite(sample))sample=0;
 peak=std::max(peak,std::abs(sample));double bass=filters[channel].Process(sample,hp,lp);energy+=bass*bass;for(int band=0;band<BandCount;band++){double y=bank[channel][band].Process(sample);bandEnergy[band]+=y*y;}
 }
 winrt::check_hresult(capture->ReleaseBuffer(frames));
 peak*=SensitivityGain;
 double bass=frames?std::sqrt(energy/(frames*2.))*SensitivityGain:0;
 double dt=frames/48000.;double follow=1-std::exp(-dt/.45);
 // Bounded adaptive gain keeps quieter tracks responsive without lifting near-silence.
 double gateLevel=std::clamp((peak-.0015)/.006,0.,1.);
 double lowGate=std::clamp((peak-.00015)/.0015,0.,1.);
 double level=gateLevel*std::clamp(peak/std::max(.035,baselinePeak*1.8),0.,1.);
 double bassLevel=lowGate*std::clamp(bass/std::max(.004,baselineBass*2.0),0.,1.);
 double hit=gateLevel*(std::max(0.,bass-baselineBass*1.25)/std::max(.012,baselineBass*1.5)*.9+std::max(0.,peak-baselinePeak*1.35)/std::max(.025,baselinePeak*1.5)*.6);
 for(int band=0;band<BandCount;band++){
 double rms=frames?std::sqrt(bandEnergy[band]/(frames*2.))*SensitivityGain:0;
 bool low=band<2;
 double normal=rms/std::max(BandFloors[band],baseline[band]*(low?1.8:2.2));
 double onsetThreshold=low?1.2:1.4;
 double onset=std::max(0.,rms-baseline[band]*onsetThreshold)/std::max(BandFloors[band],baseline[band]*onsetThreshold);
 float target=static_cast<float>((low?lowGate:gateLevel)*std::clamp(normal*.65+onset*.7,0.,1.25));
 // Separate attack/release envelopes: short transients survive the 30 Hz visual updates.
 float smoothing=static_cast<float>(1-std::exp(-dt/(target>response[band]?.012:.16)));
 response[band]+=(target-response[band])*smoothing;baseline[band]+=(rms-baseline[band])*(1-std::exp(-dt/.7));
 }
 baselineBass+=(bass-baselineBass)*follow;baselinePeak+=(peak-baselinePeak)*follow;
 {std::lock_guard lock(gate);for(int band=0;band<BandCount;band++)latest.bands[band]=response[band];latest.level=static_cast<float>(level);latest.bass=static_cast<float>(bassLevel);latest.hit=std::max(static_cast<float>(std::exp(-dt/.16))*latest.hit,static_cast<float>(std::clamp(hit,0.,1.)));latest.error=S_OK;latest.at=std::chrono::steady_clock::now();++latest.packets;}
 winrt::check_hresult(capture->GetNextPacketSize(&frames));
 }
 }
 client->Stop();
 }catch(winrt::hresult_error const& e){std::lock_guard lock(gate);latest.error=e.code();latest.level=latest.bass=latest.hit=0;for(auto& band:latest.bands)band=0;}
 if(WaitForSingleObject(stopEvent,0)==WAIT_OBJECT_0)break;
 if(restart)continue;
 // Failed endpoints stay idle until a device notification; no retry/poll loop.
 HANDLE wait[]{stopEvent,deviceEvent};if(WaitForMultipleObjects(2,wait,FALSE,INFINITE)==WAIT_OBJECT_0)break;
 }
 enumerator->UnregisterEndpointNotificationCallback(notifications.get());
 }catch(winrt::hresult_error const& e){std::lock_guard lock(gate);latest.error=e.code();}
 winrt::uninit_apartment();
}
void Enable(HWND hwnd,bool active){
 // Called only on the UI thread; the gate protects samples, not lifecycle operations.
 if(active){if(!users.insert(hwnd).second)return;if(users.size()>1)return;
 stopEvent=CreateEvent(nullptr,TRUE,FALSE,nullptr);deviceEvent=CreateEvent(nullptr,FALSE,FALSE,nullptr);
 if(!stopEvent||!deviceEvent){if(stopEvent)CloseHandle(stopEvent);if(deviceEvent)CloseHandle(deviceEvent);users.erase(hwnd);winrt::throw_last_error();}
 {std::lock_guard lock(gate);latest={};}worker=std::thread(Run);
 }else{users.erase(hwnd);if(!users.empty()||!worker.joinable())return;SetEvent(stopEvent);worker.join();CloseHandle(stopEvent);CloseHandle(deviceEvent);stopEvent=deviceEvent=nullptr;}
}
Snapshot Read(){std::lock_guard lock(gate);auto result=latest;double age=std::chrono::duration<double>(std::chrono::steady_clock::now()-result.at).count();float decay=static_cast<float>(std::exp(-std::max(0.,age-.04)/.12));for(auto& band:result.bands)band*=decay;result.level*=decay;result.bass*=decay;result.hit*=decay;return result;}
}
