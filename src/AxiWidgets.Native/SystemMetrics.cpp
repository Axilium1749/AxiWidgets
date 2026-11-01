#include <winsock2.h>
#include <windows.h>
#include <ws2ipdef.h>
#include <iphlpapi.h>
#include <netioapi.h>
#include <pdh.h>
#include <pdhmsg.h>
#include <powrprof.h>
#include <dxgi1_2.h>
#include <winrt/base.h>
#include <vector>
#include <map>
#include <string>
#include <algorithm>
#include <intrin.h>
struct Metrics { double cpu,ghz,ramUsed,ramTotal,gpu,vramUsed,vramTotal,down,up; wchar_t cpuName[128],gpuName[128],ramName[128]; };
struct Monitor {
    PDH_HQUERY query{}; PDH_HCOUNTER engine{},memory{},performance{};
    Metrics info{}; std::wstring luid;
    ULONGLONG idle{},total{},tick{}; double lastDown=-1,lastUp=-1;
    std::map<ULONG64,std::pair<ULONG64,ULONG64>> network;
    Monitor(){
        int regs[4]{}; char brand[49]{}; __cpuid(regs,static_cast<int>(0x80000000));
        if(static_cast<unsigned>(regs[0])>=0x80000004u){for(int i=0;i<3;i++){__cpuid(regs,static_cast<int>(0x80000002u+i));memcpy(brand+i*16,regs,16);}MultiByteToWideChar(CP_ACP,0,brand,-1,info.cpuName,128);}
        wcscpy_s(info.ramName,L"RAM · —");
        const DWORD signature=0x52534d42;UINT size=GetSystemFirmwareTable(signature,0,nullptr,0);
        if(size>=8&&size<1024*1024){std::vector<BYTE> b(size);if(GetSystemFirmwareTable(signature,0,b.data(),size)==size){
            for(size_t p=8;p+4<=b.size();){size_t len=b[p+1];if(len<4||p+len>b.size())break;
                if(b[p]==17&&len>=0x17&&(b[p+12]||b[p+13])){unsigned type=b[p+18];unsigned speed=len>=0x22?b[p+0x20]|(b[p+0x21]<<8):0;
                    const wchar_t* name=type==0x22?L"DDR5":type==0x1a?L"DDR4":type==0x18?L"DDR3":type==0x23?L"LPDDR5":type==0x1e?L"LPDDR4":L"RAM";
                    if(speed&&speed!=65535)swprintf_s(info.ramName,L"%s %u MT/s",name,speed);else swprintf_s(info.ramName,L"%s · —",name);break;}
                p+=len;while(p+1<b.size()&&(b[p]||b[p+1]))++p;p+=2;
            }
        }}
        winrt::com_ptr<IDXGIFactory1> factory;
        if(SUCCEEDED(CreateDXGIFactory1(__uuidof(IDXGIFactory1),factory.put_void()))){DXGI_ADAPTER_DESC1 best{};bool found=false;
            for(UINT i=0;;i++){winrt::com_ptr<IDXGIAdapter1> a;if(factory->EnumAdapters1(i,a.put())!=S_OK)break;DXGI_ADAPTER_DESC1 d{};if(FAILED(a->GetDesc1(&d))||(d.Flags&DXGI_ADAPTER_FLAG_SOFTWARE))continue;if(!found||d.DedicatedVideoMemory>best.DedicatedVideoMemory){best=d;found=true;}}
            if(found){wcsncpy_s(info.gpuName,best.Description,_TRUNCATE);info.vramTotal=static_cast<double>(best.DedicatedVideoMemory);wchar_t id[64];swprintf_s(id,L"luid_0x%08X_0x%08X",static_cast<UINT>(best.AdapterLuid.HighPart),best.AdapterLuid.LowPart);luid=id;}
        }
        if(PdhOpenQueryW(nullptr,0,&query)==ERROR_SUCCESS){PdhAddEnglishCounterW(query,L"\\GPU Engine(*)\\Utilization Percentage",0,&engine);PdhAddEnglishCounterW(query,L"\\GPU Adapter Memory(*)\\Dedicated Usage",0,&memory);PdhAddEnglishCounterW(query,L"\\Processor Information(_Total)\\% Processor Performance",0,&performance);PdhCollectQueryData(query);}
    }
    ~Monitor(){if(query)PdhCloseQuery(query);}
    template<class F> bool Values(PDH_HCOUNTER counter,F fn){if(!counter)return false;DWORD size=0,count=0;if(PdhGetFormattedCounterArrayW(counter,PDH_FMT_DOUBLE|PDH_FMT_NOCAP100,&size,&count,nullptr)!=PDH_MORE_DATA||size>16*1024*1024)return false;std::vector<BYTE> data(size);auto items=reinterpret_cast<PDH_FMT_COUNTERVALUE_ITEM_W*>(data.data());if(PdhGetFormattedCounterArrayW(counter,PDH_FMT_DOUBLE|PDH_FMT_NOCAP100,&size,&count,items)!=ERROR_SUCCESS)return false;bool valid=false;for(DWORD i=0;i<count;i++){auto& v=items[i];if(v.FmtValue.CStatus>1||luid.empty())continue;std::wstring name=v.szName;if(name.find(luid)==std::wstring::npos)continue;fn(name,v.FmtValue.doubleValue);valid=true;}return valid;}
    void Sample(Metrics& m){m=info;m.cpu=m.ghz=m.gpu=m.vramUsed=m.down=m.up=-1;
        FILETIME a{},b{},c{};if(GetSystemTimes(&a,&b,&c)){auto value=[](FILETIME f){return (static_cast<ULONGLONG>(f.dwHighDateTime)<<32)|f.dwLowDateTime;};auto t=value(b)+value(c),id=value(a);if(total&&t>total&&id>=idle)m.cpu=std::clamp(100.0*(1.0-static_cast<double>(id-idle)/(t-total)),0.0,100.0);total=t;idle=id;}
        struct Power {ULONG number,max,current,limit,maxIdle,currentIdle;};SYSTEM_INFO si{};GetSystemInfo(&si);std::vector<Power> power(si.dwNumberOfProcessors);if(!CallNtPowerInformation(ProcessorInformation,nullptr,0,power.data(),static_cast<ULONG>(power.size()*sizeof(Power)))){double sum=0;for(auto p:power)sum+=p.current;if(!power.empty())m.ghz=sum/power.size()/1000.0;}
        MEMORYSTATUSEX mem{};mem.dwLength=sizeof(mem);if(GlobalMemoryStatusEx(&mem)){m.ramTotal=static_cast<double>(mem.ullTotalPhys);m.ramUsed=static_cast<double>(mem.ullTotalPhys-mem.ullAvailPhys);}
        if(query&&PdhCollectQueryData(query)==ERROR_SUCCESS){PDH_FMT_COUNTERVALUE perf{};if(performance&&PdhGetFormattedCounterValue(performance,PDH_FMT_DOUBLE|PDH_FMT_NOCAP100,nullptr,&perf)==ERROR_SUCCESS&&perf.CStatus<=1&&!power.empty())m.ghz=power[0].max/1000.0*perf.doubleValue/100.0;std::map<std::wstring,double> engines;if(Values(engine,[&](auto name,double v){auto p=name.find(L"_eng_");if(p!=std::wstring::npos)engines[name.substr(p)]+=v;})){m.gpu=0;for(auto const& e:engines)m.gpu=std::max(m.gpu,e.second);m.gpu=std::clamp(m.gpu,0.0,100.0);}double used=0;if(Values(memory,[&](auto,double v){used+=v;}))m.vramUsed=used;}
        auto now=GetTickCount64();m.down=lastDown;m.up=lastUp;PMIB_IF_TABLE2 table{};if((!tick||now-tick>=1000)&&GetIfTable2(&table)==NO_ERROR){double down=0,up=0;std::map<ULONG64,std::pair<ULONG64,ULONG64>> next;
            for(ULONG i=0;i<table->NumEntries;i++){auto const& r=table->Table[i];if(r.OperStatus!=IfOperStatusUp||!r.InterfaceAndOperStatusFlags.HardwareInterface)continue;auto id=r.InterfaceLuid.Value;auto old=network.find(id);if(old!=network.end()&&r.InOctets>=old->second.first&&r.OutOctets>=old->second.second){down+=static_cast<double>(r.InOctets-old->second.first);up+=static_cast<double>(r.OutOctets-old->second.second);}next[id]={r.InOctets,r.OutOctets};}FreeMibTable(table);network=std::move(next);if(tick&&now>tick){m.down=down*1000/(now-tick);m.up=up*1000/(now-tick);}lastDown=m.down;lastUp=m.up;tick=now;}
    }
};
extern "C" __declspec(dllexport) HRESULT __cdecl AxiMetrics_Create(Monitor** result) noexcept {try{*result=new Monitor;return S_OK;}catch(...){return E_FAIL;}}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiMetrics_Read(Monitor* monitor,Metrics* result) noexcept {try{monitor->Sample(*result);return S_OK;}catch(...){return E_FAIL;}}
extern "C" __declspec(dllexport) void __cdecl AxiMetrics_Destroy(Monitor* monitor) noexcept {delete monitor;}

