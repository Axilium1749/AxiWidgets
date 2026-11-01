#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <ksmedia.h>
#include <audioclientactivationparams.h>
#include <tlhelp32.h>
#include <appmodel.h>
#include <atomic>
#include <shobjidl.h>
#include <shellapi.h>
#include <propkey.h>
#include <dwmapi.h>
#include <dispatcherqueue.h>
#include <windows.ui.composition.interop.h>
#include <windows.graphics.effects.interop.h>
#include <d2d1effects.h>
#include <windows.graphics.interop.h>
#include <d2d1_1.h>
#include <d3d11.h>
#include <dwrite.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Graphics.Effects.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <string>
#include <memory>
#include <vector>
#include <map>
#include <set>
#include <chrono>
#include <cmath>
#include <algorithm>
#include <unordered_map>

using namespace winrt;
using namespace Windows::UI::Composition;
using namespace Windows::Graphics::Effects;
using namespace Windows::Foundation;
using ABI::Windows::Graphics::Effects::IGraphicsEffectD2D1Interop;
using ABI::Windows::Graphics::Effects::GRAPHICS_EFFECT_PROPERTY_MAPPING;

struct BlurEffect : implements<BlurEffect,IGraphicsEffect,IGraphicsEffectSource,IGraphicsEffectD2D1Interop>
{
    hstring Name(){return L"Blur";} void Name(hstring const&){}
    IGraphicsEffectSource source{CompositionEffectSourceParameter(L"desktop")};
    HRESULT __stdcall GetEffectId(GUID* id) noexcept override {*id=CLSID_D2D1GaussianBlur;return S_OK;}
    HRESULT __stdcall GetNamedPropertyMapping(LPCWSTR name,UINT* index,GRAPHICS_EFFECT_PROPERTY_MAPPING* mapping) noexcept override {
        if(wcscmp(name,L"BlurAmount"))return E_INVALIDARG;*index=0;*mapping=ABI::Windows::Graphics::Effects::GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT;return S_OK;
    }
    HRESULT __stdcall GetPropertyCount(UINT* count) noexcept override {*count=3;return S_OK;}
    HRESULT __stdcall GetProperty(UINT index,ABI::Windows::Foundation::IPropertyValue** result) noexcept override {
        try {
            winrt::Windows::Foundation::IInspectable value{nullptr};
            switch(index){case 0:value=PropertyValue::CreateSingle(6.f);break;case 1:value=PropertyValue::CreateUInt32(D2D1_GAUSSIANBLUR_OPTIMIZATION_BALANCED);break;case 2:value=PropertyValue::CreateUInt32(D2D1_BORDER_MODE_HARD);break;default:return E_INVALIDARG;}
            *result=reinterpret_cast<ABI::Windows::Foundation::IPropertyValue*>(detach_abi(value));return S_OK;
        }catch(...){return to_hresult();}
    }
    HRESULT __stdcall GetSourceCount(UINT* count) noexcept override {*count=1;return S_OK;}
    HRESULT __stdcall GetSource(UINT index,ABI::Windows::Graphics::Effects::IGraphicsEffectSource** result) noexcept override {
        if(index)return E_INVALIDARG;*result=reinterpret_cast<ABI::Windows::Graphics::Effects::IGraphicsEffectSource*>(detach_abi(IGraphicsEffectSource(source)));return S_OK;
    }
};

// Retained D2D path gives the compositor an exact clockwise 270-degree arc.
struct GaugePath : implements<GaugePath,Windows::Graphics::IGeometrySource2D,ABI::Windows::Graphics::IGeometrySource2DInterop> {
    com_ptr<ID2D1PathGeometry> path;
    explicit GaugePath(com_ptr<ID2D1PathGeometry> value):path(std::move(value)){}
    HRESULT __stdcall GetGeometry(ID2D1Geometry** value) noexcept override {return path->QueryInterface(__uuidof(ID2D1Geometry),reinterpret_cast<void**>(value));}
    HRESULT __stdcall TryGetGeometryUsingFactory(ID2D1Factory*,ID2D1Geometry** value) noexcept override {*value=nullptr;return S_OK;}
};

struct DrawCommand {int kind;float x,y,w,h,r,size;UINT color;int align;wchar_t text[256];};
struct Renderer
{
    Windows::System::DispatcherQueueController queue{nullptr};
    Compositor compositor{nullptr};
    CompositionGraphicsDevice graphics{nullptr};CompositionEffectFactory blur{nullptr};
    com_ptr<ID3D11Device> d3d;com_ptr<ID2D1Factory1> factory;com_ptr<ID2D1Device> d2d;
    std::map<int,com_ptr<IDWriteTextFormat>> formats;
    com_ptr<IDWriteFactory> write;com_ptr<IDWriteTextFormat> timeFormat,dateFormat;
    Renderer(){
        init_apartment(apartment_type::single_threaded);
        if(!Windows::System::DispatcherQueue::GetForCurrentThread()){
            DispatcherQueueOptions options{sizeof options,DQTYPE_THREAD_CURRENT,DQTAT_COM_STA};
            check_hresult(CreateDispatcherQueueController(options,reinterpret_cast<ABI::Windows::System::IDispatcherQueueController**>(put_abi(queue))));
        }
        compositor=Compositor();blur=compositor.CreateEffectFactory(make<BlurEffect>());
        check_hresult(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,nullptr,0,D3D11_SDK_VERSION,d3d.put(),nullptr,nullptr));
        check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED,__uuidof(ID2D1Factory1),nullptr,factory.put_void()));
        check_hresult(factory->CreateDevice(d3d.as<IDXGIDevice>().get(),d2d.put()));
        check_hresult(compositor.as<ABI::Windows::UI::Composition::ICompositorInterop>()->CreateGraphicsDevice(d2d.get(),reinterpret_cast<ABI::Windows::UI::Composition::ICompositionGraphicsDevice**>(put_abi(graphics))));
        check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED,__uuidof(IDWriteFactory),reinterpret_cast<::IUnknown**>(write.put())));
        check_hresult(write->CreateTextFormat(L"Segoe UI",nullptr,DWRITE_FONT_WEIGHT_LIGHT,DWRITE_FONT_STYLE_NORMAL,DWRITE_FONT_STRETCH_NORMAL,54,L"ru-RU",timeFormat.put()));
        check_hresult(write->CreateTextFormat(L"Segoe UI",nullptr,DWRITE_FONT_WEIGHT_NORMAL,DWRITE_FONT_STYLE_NORMAL,DWRITE_FONT_STRETCH_NORMAL,14,L"ru-RU",dateFormat.put()));
        for(auto format:{timeFormat.get(),dateFormat.get()}){format->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);}
    }
    ~Renderer(){
        formats.clear();timeFormat=nullptr;dateFormat=nullptr;write=nullptr;graphics=nullptr;
        d2d=nullptr;factory=nullptr;d3d=nullptr;blur=nullptr;compositor=nullptr;queue=nullptr;
        uninit_apartment();
    }
};
#include "AudioReactive.h"
std::unique_ptr<Renderer> renderer;
struct Card;
std::unordered_map<HWND,std::unique_ptr<Card>> cards;

LRESULT CALLBACK CardProc(HWND hwnd,UINT msg,WPARAM w,LPARAM l){
    switch(msg){
    case WM_MOUSEACTIVATE:return MA_NOACTIVATE;
    case WM_NCHITTEST:return IsWindowEnabled(hwnd)?HTCLIENT:HTTRANSPARENT;
    case WM_ERASEBKGND:return 1;
    }
    return DefWindowProc(hwnd,msg,w,l);
}

struct Card
{
    HWND hwnd{};float scale{};float width=300,height=160;std::vector<DrawCommand> commands;
    Windows::UI::Composition::Desktop::DesktopWindowTarget target{nullptr};
    ContainerVisual root{nullptr};CompositionDrawingSurface surface{nullptr};CompositionSurfaceBrush textBrush{nullptr};
    CompositionColorBrush tint{nullptr},border{nullptr};
    CompositionLineGeometry vertical{nullptr},horizontal{nullptr};CompositionSpriteShape verticalShape{nullptr},horizontalShape{nullptr};
    struct Row {SpriteVisual visual{nullptr};CompositionDrawingSurface surface{nullptr};float y{},dpi{};};
    std::map<std::wstring,Row> rows;
    SpriteVisual scrollbar{nullptr};float scrollMaximum=0,scrollbarX=341,scrollbarTop=98,scrollbarTrack=196;
    ContainerVisual viewport{nullptr},scroller{nullptr};float scrollTarget=0;bool listReady=false;
    SpriteVisual rowPress{nullptr},headerPress{nullptr};
    SpriteVisual textVisual{nullptr},mediaTimeline{nullptr};CompositionDrawingSurface mediaTimeSurface{nullptr};float mediaTimeDpi=0;CompositionColorBrush mediaPlayBrush{nullptr};
    CompositionPropertySet mediaEnvelope{nullptr};Vector3KeyFrameAnimation mediaAudioTween{nullptr};LinearEasingFunction mediaAudioLinear{nullptr};
    std::vector<CompositionRoundedRectangleGeometry> mediaBars;
    std::vector<CompositionColorBrush> mediaBrushes;
    SpriteVisual mediaProgress{nullptr},mediaThumb{nullptr};
    SpriteVisual mediaFeedback{nullptr};int mediaFeedbackId=-2,mediaFeedbackPressed=-1;bool mediaActive=false;UINT mediaColor=0,mediaPaletteSeed=1;
    double mediaProgressFraction=-1,mediaProgressSpeed=0;std::chrono::steady_clock::time_point mediaProgressAt{};
    struct Gauge {CompositionPathGeometry geometry{nullptr};float target=-1;};
    std::map<int,Gauge> systemGauges;
    SpriteVisual systemVram{nullptr},systemGraph{nullptr};float systemVramTarget=-1,systemGraphDpi=0;
    CompositionDrawingSurface systemGraphSurface{nullptr};std::vector<DrawCommand> systemGraphCommands;
    std::wstring time,date;
    void SystemGauge(DrawCommand const& cmd){
        auto c=renderer->compositor;auto& gauge=systemGauges[static_cast<int>(cmd.x)];bool fresh=!gauge.geometry;
        float value=std::clamp(cmd.h/100.f,0.f,1.f);
        if(fresh){
            com_ptr<ID2D1PathGeometry> path;check_hresult(renderer->factory->CreatePathGeometry(path.put()));
            com_ptr<ID2D1GeometrySink> sink;check_hresult(path->Open(sink.put()));
            constexpr float diagonal=.70710678118f;
            sink->BeginFigure({cmd.x-cmd.w*diagonal,cmd.y+cmd.w*diagonal},D2D1_FIGURE_BEGIN_HOLLOW);
            sink->AddArc(D2D1::ArcSegment({cmd.x+cmd.w*diagonal,cmd.y+cmd.w*diagonal},{cmd.w,cmd.w},0,D2D1_SWEEP_DIRECTION_CLOCKWISE,D2D1_ARC_SIZE_LARGE));
            sink->EndFigure(D2D1_FIGURE_END_OPEN);check_hresult(sink->Close());
            gauge.geometry=c.CreatePathGeometry();gauge.geometry.Path(CompositionPath(make<GaugePath>(path)));gauge.geometry.TrimEnd(value);
            auto shape=c.CreateSpriteShape(gauge.geometry);shape.StrokeThickness(cmd.size);shape.StrokeStartCap(CompositionStrokeCap::Round);shape.StrokeEndCap(CompositionStrokeCap::Round);
            shape.StrokeBrush(c.CreateColorBrush({static_cast<BYTE>(cmd.color>>24),static_cast<BYTE>(cmd.color>>16),static_cast<BYTE>(cmd.color>>8),static_cast<BYTE>(cmd.color)}));
            auto visual=c.CreateShapeVisual();visual.Size({width,height});visual.Shapes().Append(shape);root.Children().InsertAbove(visual,textVisual);
        }else if(gauge.target!=value){
            auto a=c.CreateScalarKeyFrameAnimation();a.Duration(std::chrono::milliseconds(600));a.InsertExpressionKeyFrame(0,L"this.StartingValue");a.InsertKeyFrame(1,value,c.CreateCubicBezierEasingFunction({.25f,.1f},{.25f,1.f}));gauge.geometry.StartAnimation(L"TrimEnd",a);
        }
        gauge.target=value;
    }
    void SystemVram(DrawCommand const& cmd){
        if(!systemVram){auto c=renderer->compositor;systemVram=c.CreateSpriteVisual();systemVram.Size({cmd.w,cmd.h});systemVram.Offset({cmd.x,cmd.y,0});
            systemVram.Brush(c.CreateColorBrush({255,255,85,98}));auto clip=c.CreateRectangleClip();clip.Right(cmd.w);clip.Bottom(cmd.h);auto right=c.CreateExpressionAnimation(L"bar.Size.X");right.SetReferenceParameter(L"bar",systemVram);clip.StartAnimation(L"Right",right);clip.TopLeftRadius({cmd.r,cmd.r});clip.TopRightRadius({cmd.r,cmd.r});clip.BottomLeftRadius({cmd.r,cmd.r});clip.BottomRightRadius({cmd.r,cmd.r});systemVram.Clip(clip);root.Children().InsertAbove(systemVram,textVisual);
        }else if(systemVramTarget!=cmd.w)Animate(systemVram,L"Size.X",cmd.w,600);
        systemVramTarget=cmd.w;
    }
    void SystemGraph(std::vector<DrawCommand> content,DrawCommand const& bounds){
        bool changed=content.size()!=systemGraphCommands.size();
        if(!changed)for(size_t i=0;i<content.size();i++)if(content[i].y!=systemGraphCommands[i].y||content[i].h!=systemGraphCommands[i].h||content[i].x!=systemGraphCommands[i].x){changed=true;break;}
        bool fresh=!systemGraph||systemGraphDpi!=scale;
        if(!changed&&!fresh)return;
        auto c=renderer->compositor;
        if(!systemGraph){auto viewportVisual=c.CreateContainerVisual();viewportVisual.Size({width,height});auto clip=c.CreateRectangleClip();clip.Left(bounds.x);clip.Top(bounds.y);clip.Right(bounds.x+bounds.w);clip.Bottom(bounds.y+bounds.h);viewportVisual.Clip(clip);root.Children().InsertAbove(viewportVisual,textVisual);systemGraph=c.CreateSpriteVisual();systemGraph.Size({width,height});viewportVisual.Children().InsertAtTop(systemGraph);}
        if(fresh){systemGraphDpi=scale;systemGraphSurface=renderer->graphics.CreateDrawingSurface({width*scale,height*scale},Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);systemGraph.Brush(c.CreateSurfaceBrush(systemGraphSurface));}
        systemGraphCommands=content;auto savedSurface=surface;auto savedCommands=std::move(commands);surface=systemGraphSurface;commands=std::move(content);
        try{Draw();}catch(...){surface=savedSurface;commands=std::move(savedCommands);throw;}surface=savedSurface;commands=std::move(savedCommands);
        systemGraph.StopAnimation(L"Offset.X");systemGraph.Offset({fresh?0.f:95.f/29,0,0});if(!fresh)Animate(systemGraph,L"Offset.X",0,600);
    }
    void Animate(Visual const& visual,wchar_t const* property,float value,int duration){
        auto animation=renderer->compositor.CreateScalarKeyFrameAnimation();
        animation.Duration(std::chrono::milliseconds(duration));
        animation.InsertExpressionKeyFrame(0,L"this.StartingValue");
        animation.InsertKeyFrame(1,value,renderer->compositor.CreateCubicBezierEasingFunction({.2f,.8f},{.2f,1.f}));
        visual.StartAnimation(property,animation);
    }
    void SetCommands(std::vector<DrawCommand> const& incoming){
        std::vector<DrawCommand> background;std::set<std::wstring> active;
        for(size_t i=0;i<incoming.size();i++){
            auto const& cmd=incoming[i];
            if(cmd.kind==11){SystemGauge(cmd);continue;}
            if(cmd.kind==12){SystemVram(cmd);continue;}
            if(cmd.kind==14){std::vector<DrawCommand> graph;while(++i<incoming.size()&&incoming[i].kind!=15)graph.push_back(incoming[i]);SystemGraph(std::move(graph),cmd);continue;}
            if(cmd.kind==6){
                if(!viewport){
                    auto c=renderer->compositor;viewport=c.CreateContainerVisual();viewport.Size({width,height});
                    auto clip=c.CreateRectangleClip();clip.Left(cmd.w>0?cmd.x:27);clip.Top(cmd.w>0?cmd.r:92);clip.Right(cmd.w>0?cmd.x+cmd.w:337);clip.Bottom(cmd.w>0?cmd.r+cmd.h:300);viewport.Clip(clip);
                    scroller=c.CreateContainerVisual();scroller.Size({width,height});viewport.Children().InsertAtTop(scroller);root.Children().InsertAtTop(viewport);
                }
                float next=-cmd.y;
                if(!listReady){scroller.Offset({0,next,0});scrollTarget=next;}
                // Once initialized, scrolling is owned by SetScroll / AnimateScroll.
                // Content refills must not interrupt the running compositor animation.
                continue;
            }
            if(cmd.kind==7){
                bool changed=scrollMaximum!=cmd.r;scrollbarX=cmd.x;scrollbarTop=cmd.y;scrollbarTrack=cmd.size>1?cmd.size:196;
                if(!scrollbar){changed=true;scrollbar=renderer->compositor.CreateSpriteVisual();scrollbar.Brush(renderer->compositor.CreateColorBrush({96,255,255,255}));root.Children().InsertAtTop(scrollbar);}
                scrollbar.Size({cmd.w,cmd.h});scrollMaximum=cmd.r;scrollbar.Opacity(cmd.r>0?1.f:0.f);
                if(changed)scrollbar.Offset({cmd.x,scrollbarTop+(scrollbarTrack-cmd.h)*(cmd.r>0?std::clamp(-scrollTarget/cmd.r,0.f,1.f):0.f),0});continue;
            }
            if(cmd.kind!=4){background.push_back(cmd);continue;}
            std::wstring id(cmd.text);active.insert(id);bool fresh=rows.find(id)==rows.end();auto& row=rows[id];
            if(fresh){auto c=renderer->compositor;row.visual=c.CreateSpriteVisual();row.visual.Size({width,cmd.h});scroller.Children().InsertAtTop(row.visual);}
            if(!row.surface||row.dpi!=scale){row.dpi=scale;row.surface=renderer->graphics.CreateDrawingSurface({width*scale,cmd.h*scale},Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);row.visual.Brush(renderer->compositor.CreateSurfaceBrush(row.surface));}
            std::vector<DrawCommand> content;
            while(++i<incoming.size()&&incoming[i].kind!=5){auto local=incoming[i];local.y-=cmd.y;content.push_back(local);}
            auto savedSurface=surface;auto savedCommands=std::move(commands);surface=row.surface;commands=std::move(content);
            try{Draw();}catch(...){surface=savedSurface;commands=std::move(savedCommands);throw;}
            surface=savedSurface;commands=std::move(savedCommands);
            if(fresh){
                row.visual.Offset({0,cmd.y-(listReady&&cmd.color?38.f:0.f),0});
                if(listReady&&cmd.color){row.visual.Opacity(0);Animate(row.visual,L"Offset.Y",cmd.y,320);Animate(row.visual,L"Opacity",1,220);}
            }else if(row.y!=cmd.y)Animate(row.visual,L"Offset.Y",cmd.y,320);
            row.y=cmd.y;
        }
        for(auto it=rows.begin();it!=rows.end();){if(!active.count(it->first)){scroller.Children().Remove(it->second.visual);it=rows.erase(it);}else ++it;}
        commands=std::move(background);Draw();if(viewport)listReady=true;
    }
    ~Card(){if(target)target.Root(nullptr);if(hwnd&&IsWindow(hwnd))DestroyWindow(hwnd);}
    void Create(float w=300,float h=160,float radius=30){
        width=w;height=h;
        HINSTANCE module=GetModuleHandle(nullptr);WNDCLASS cls{};cls.hInstance=module;cls.lpfnWndProc=CardProc;cls.lpszClassName=L"AxiWidgets.NativeCard";RegisterClass(&cls);
        hwnd=CreateWindowEx(WS_EX_NOREDIRECTIONBITMAP|WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW,cls.lpszClassName,L"AxiWidgets — Digital Clock",WS_POPUP,0,0,300,160,nullptr,nullptr,module,nullptr);
        if(!hwnd)throw_last_error();
        BOOL enabled=TRUE;check_hresult(DwmSetWindowAttribute(hwnd,DWMWA_USE_HOSTBACKDROPBRUSH,&enabled,sizeof enabled));
        DWORD none=0xfffffffe;DwmSetWindowAttribute(hwnd,DWMWA_BORDER_COLOR,&none,sizeof none);
        DWM_WINDOW_CORNER_PREFERENCE square=DWMWCP_DONOTROUND;DwmSetWindowAttribute(hwnd,DWMWA_WINDOW_CORNER_PREFERENCE,&square,sizeof square);
        auto& r=*renderer;auto c=r.compositor;
        check_hresult(c.as<ABI::Windows::UI::Composition::Desktop::ICompositorDesktopInterop>()->CreateDesktopWindowTarget(hwnd,FALSE,reinterpret_cast<ABI::Windows::UI::Composition::Desktop::IDesktopWindowTarget**>(put_abi(target))));
        root=c.CreateContainerVisual();root.Size({width,height});target.Root(root);
        auto clip=c.CreateRectangleClip();clip.Left(0);clip.Top(0);clip.Right(width);clip.Bottom(height);clip.TopLeftRadius({radius,radius});clip.TopRightRadius({radius,radius});clip.BottomLeftRadius({radius,radius});clip.BottomRightRadius({radius,radius});root.Clip(clip);
        auto effect=r.blur.CreateBrush();effect.SetSourceParameter(L"desktop",c.CreateHostBackdropBrush());
        auto glass=c.CreateSpriteVisual();glass.Size({width,height});glass.Brush(effect);root.Children().InsertAtBottom(glass);
        tint=c.CreateColorBrush({140,30,30,34});auto shade=c.CreateSpriteVisual();shade.Size({width,height});shade.Brush(tint);root.Children().InsertAtTop(shade);
        textBrush=c.CreateSurfaceBrush();auto text=c.CreateSpriteVisual();textVisual=text;text.Size({width,height});text.Brush(textBrush);root.Children().InsertAtTop(text);
        auto geometry=c.CreateRoundedRectangleGeometry();geometry.Size({width-.6f,height-.6f});geometry.Offset({.3f,.3f});geometry.CornerRadius({radius-.3f,radius-.3f});
        border=c.CreateColorBrush({44,255,255,255});auto line=c.CreateSpriteShape(geometry);line.StrokeThickness(.6f);line.StrokeBrush(border);
        auto outline=c.CreateShapeVisual();outline.Size({width,height});outline.Shapes().Append(line);
        vertical=c.CreateLineGeometry();horizontal=c.CreateLineGeometry();verticalShape=c.CreateSpriteShape(vertical);horizontalShape=c.CreateSpriteShape(horizontal);
        for(auto shape:{verticalShape,horizontalShape}){shape.StrokeThickness(1);shape.StrokeBrush(c.CreateColorBrush({255,0,191,255}));outline.Shapes().Append(shape);}
        root.Children().InsertAtTop(outline);Dpi(1.f);
    }
    void Dpi(float value){
        if(value==scale)return;scale=value;root.Scale({scale,scale,1});
        surface=renderer->graphics.CreateDrawingSurface({width*scale,height*scale},Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
        textBrush.Surface(surface);Draw();
    }
    void Draw(){
        auto drawing=surface.as<ABI::Windows::UI::Composition::ICompositionDrawingSurfaceInterop>();com_ptr<ID2D1DeviceContext> context;POINT offset{};
        check_hresult(drawing->BeginDraw(nullptr,__uuidof(ID2D1DeviceContext),context.put_void(),&offset));
        try {
            context->SetDpi(96*scale,96*scale);context->SetTransform(D2D1::Matrix3x2F::Translation(offset.x/scale,offset.y/scale));context->Clear(D2D1::ColorF(0,0,0,0));
            com_ptr<ID2D1SolidColorBrush> brush;check_hresult(context->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White),brush.put()));
            if(commands.empty()) {
            context->DrawTextW(time.c_str(),static_cast<UINT32>(time.size()),renderer->timeFormat.get(),D2D1::RectF(0,date.empty()?0.f:24.f,300,date.empty()?160.f:110.f),brush.get());
            if(!date.empty()){brush->SetOpacity(.65f);context->DrawTextW(date.c_str(),static_cast<UINT32>(date.size()),renderer->dateFormat.get(),D2D1::RectF(0,108,300,132),brush.get());}
            } else for(auto const& cmd:commands){
                brush->SetColor(D2D1::ColorF(cmd.color&0xffffff,((cmd.color>>24)&255)/255.f));brush->SetOpacity(1);
                auto rect=D2D1::RectF(cmd.x,cmd.y,cmd.x+cmd.w,cmd.y+cmd.h);
                auto rr=D2D1::RoundedRect(rect,cmd.r,cmd.r);
                if(cmd.kind==0)context->FillRoundedRectangle(rr,brush.get());
                else if(cmd.kind==1)context->DrawRoundedRectangle(rr,brush.get(),cmd.size);
                else if(cmd.kind==10){
 com_ptr<ID2D1PathGeometry> path;check_hresult(renderer->factory->CreatePathGeometry(path.put()));com_ptr<ID2D1GeometrySink> sink;check_hresult(path->Open(sink.put()));
 float base=cmd.align?cmd.x:cmd.x+cmd.w,tip=cmd.align?cmd.x+cmd.w:cmd.x;
 sink->BeginFigure({base,cmd.y},D2D1_FIGURE_BEGIN_FILLED);sink->AddLine({tip,cmd.y+cmd.h/2});sink->AddLine({base,cmd.y+cmd.h});sink->EndFigure(D2D1_FIGURE_END_CLOSED);check_hresult(sink->Close());context->FillGeometry(path.get(),brush.get());
 }
 else if(cmd.kind==9){
                    constexpr float radians=3.14159265358979323846f/180.f;
                    float a=cmd.r*radians,b=(cmd.r+cmd.h)*radians;
                    com_ptr<ID2D1PathGeometry> path;check_hresult(renderer->factory->CreatePathGeometry(path.put()));
                    com_ptr<ID2D1GeometrySink> sink;check_hresult(path->Open(sink.put()));
                    sink->BeginFigure({cmd.x+cmd.w*std::cos(a),cmd.y+cmd.w*std::sin(a)},D2D1_FIGURE_BEGIN_HOLLOW);
                    sink->AddArc(D2D1::ArcSegment({cmd.x+cmd.w*std::cos(b),cmd.y+cmd.w*std::sin(b)},{cmd.w,cmd.w},0,D2D1_SWEEP_DIRECTION_CLOCKWISE,cmd.h>180?D2D1_ARC_SIZE_LARGE:D2D1_ARC_SIZE_SMALL));
                    sink->EndFigure(D2D1_FIGURE_END_OPEN);check_hresult(sink->Close());context->DrawGeometry(path.get(),brush.get(),cmd.size);
                }
                else if(cmd.kind==3)context->DrawLine({cmd.x,cmd.y},{cmd.x+cmd.w,cmd.y+cmd.h},brush.get(),cmd.size);
                else {
                    int key=static_cast<int>(cmd.size*10)+(cmd.kind==8?10000:0);
                    auto& format=renderer->formats[key];
                    if(!format){check_hresult(renderer->write->CreateTextFormat(L"Segoe UI",nullptr,cmd.kind==8?DWRITE_FONT_WEIGHT_SEMI_BOLD:DWRITE_FONT_WEIGHT_NORMAL,DWRITE_FONT_STYLE_NORMAL,DWRITE_FONT_STRETCH_NORMAL,cmd.size,L"",format.put()));format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);format->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);}
                    format->SetTextAlignment(static_cast<DWRITE_TEXT_ALIGNMENT>(cmd.align));
                    context->DrawTextW(cmd.text,static_cast<UINT32>(wcsnlen(cmd.text,256)),format.get(),rect,brush.get(),D2D1_DRAW_TEXT_OPTIONS_CLIP);
                }
            }
        }catch(...){drawing->EndDraw();throw;}
        check_hresult(drawing->EndDraw());
    }
};

template<class F> HRESULT Guard(F action) noexcept {try{action();return S_OK;}catch(...){return to_hresult();}}
Card& GetCard(HWND hwnd){auto found=cards.find(hwnd);if(found==cards.end())throw hresult_invalid_argument();return *found->second;}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_Create(HWND* result) noexcept {
    if(!result)return E_POINTER;*result=nullptr;
    return Guard([&]{if(!renderer)renderer=std::make_unique<Renderer>();auto card=std::make_unique<Card>();card->Create();*result=card->hwnd;cards.emplace(card->hwnd,std::move(card));});
}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_SetText(HWND hwnd,wchar_t const* time,wchar_t const* date) noexcept {
    return Guard([&]{auto& card=GetCard(hwnd);std::wstring t=time?time:L"",d=date?date:L"";if(t!=card.time||d!=card.date){card.time=t;card.date=d;card.Draw();}});
}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_SetDpi(HWND hwnd,float scale) noexcept {return Guard([&]{if(scale<.5f||scale>8.f)throw hresult_invalid_argument();GetCard(hwnd).Dpi(scale);});}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_SetStyle(HWND hwnd,BYTE alpha,UINT rgb,BOOL editing) noexcept {
    return Guard([&]{auto& card=GetCard(hwnd);card.tint.Color({alpha,static_cast<BYTE>(rgb>>16),static_cast<BYTE>(rgb>>8),static_cast<BYTE>(rgb)});card.border.Color(editing?Windows::UI::Color{100,130,190,255}:Windows::UI::Color{44,255,255,255});});
}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_SetGuides(HWND hwnd,float x,float y) noexcept {
    return Guard([&]{auto& card=GetCard(hwnd);card.vertical.Start({x,3});card.vertical.End({x,card.height-3});card.verticalShape.StrokeThickness(x<0?0.f:1.f);card.horizontal.Start({3,y});card.horizontal.End({card.width-3,y});card.horizontalShape.StrokeThickness(y<0?0.f:1.f);});
}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_Destroy(HWND hwnd) noexcept {return Guard([&]{AudioReactive::Enable(hwnd,false);cards.erase(hwnd);});}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_Shutdown() noexcept {return Guard([]{for(auto const& entry:cards)AudioReactive::Enable(entry.first,false);cards.clear();renderer.reset();});}


extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_CreateWidget(HWND* result,float width,float height,float radius) noexcept {
    if(!result)return E_POINTER;*result=nullptr;
    return Guard([&]{if(!renderer)renderer=std::make_unique<Renderer>();auto card=std::make_unique<Card>();card->Create(width,height,radius);*result=card->hwnd;cards.emplace(card->hwnd,std::move(card));});
}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_SetContent(HWND hwnd,DrawCommand const* commands,int count) noexcept {
    return Guard([&]{if(count<0||count>1000||(!commands&&count))throw hresult_invalid_argument();auto& card=GetCard(hwnd);card.SetCommands(std::vector<DrawCommand>(commands,commands+count));});
}

// Transient feedback is played by the compositor, without a manager render timer.
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_Press(HWND hwnd,wchar_t const* id,int state) noexcept {
    return Guard([&]{
        auto& card=GetCard(hwnd);auto c=renderer->compositor;
        if(state==0){for(auto shade:{card.rowPress,card.headerPress})if(shade)card.Animate(shade,L"Opacity",0,180);return;}
        for(auto shade:{card.rowPress,card.headerPress})if(shade){shade.StopAnimation(L"Opacity");shade.Opacity(0);}
        bool header=id&&std::wstring(id)==L"+";
        auto found=card.rows.find(id?id:L"");if(!header&&(found==card.rows.end()||!card.scroller))return;
        auto& shade=header?card.headerPress:card.rowPress;
        if(!shade){shade=c.CreateSpriteVisual();shade.Brush(c.CreateColorBrush({255,210,210,210}));
            auto clip=c.CreateRectangleClip();clip.Left(0);clip.Top(0);clip.Right(header?30.f:340.f);clip.Bottom(header?28.f:38.f);
            for(auto corner:{0,1,2,3}){if(corner==0)clip.TopLeftRadius({7,7});else if(corner==1)clip.TopRightRadius({7,7});else if(corner==2)clip.BottomLeftRadius({7,7});else clip.BottomRightRadius({7,7});}
            shade.Clip(clip);(header?card.root:card.scroller).Children().InsertAtTop(shade);
        }
        shade.Size(header?Windows::Foundation::Numerics::float2{30,28}:Windows::Foundation::Numerics::float2{340,38});
        shade.Offset({header?322.f:8.f,header?7.f:found->second.y+1.f,0});shade.Brush().as<CompositionColorBrush>().Color(state==1?Windows::UI::Color{255,65,65,65}:Windows::UI::Color{255,210,210,210});
        card.Animate(shade,L"Opacity",state==1?.45f:.18f,90);
        TRACKMOUSEEVENT track{sizeof(TRACKMOUSEEVENT),TME_LEAVE,hwnd,0};TrackMouseEvent(&track);
    });
}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_SetScroll(HWND hwnd,float position) noexcept {
    return Guard([&]{auto& card=GetCard(hwnd);if(card.scroller){card.scroller.StopAnimation(L"Offset.Y");if(card.scrollbar)card.scrollbar.StopAnimation(L"Offset.Y");card.scroller.Offset({0,-position,0});card.scrollTarget=-position;if(card.scrollbar&&card.scrollMaximum>0)card.scrollbar.Offset({card.scrollbarX,card.scrollbarTop+(card.scrollbarTrack-card.scrollbar.Size().y)*std::clamp(position/card.scrollMaximum,0.f,1.f),0});}});
}

extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_AnimateScroll(HWND hwnd,float const* positions,int count,float interval) noexcept {
    return Guard([&]{
        if(!positions||count<2||count>2400||interval<=0)throw hresult_invalid_argument();
        auto& card=GetCard(hwnd);if(!card.scroller)return;
        auto c=renderer->compositor;auto movement=c.CreateScalarKeyFrameAnimation();auto thumb=c.CreateScalarKeyFrameAnimation();
        auto duration=std::chrono::duration_cast<winrt::Windows::Foundation::TimeSpan>(std::chrono::duration<double>((count-1)*interval));
        movement.Duration(duration);thumb.Duration(duration);auto linear=c.CreateLinearEasingFunction();
        for(int i=0;i<count;i++){
            float progress=static_cast<float>(i)/(count-1);movement.InsertKeyFrame(progress,-positions[i],linear);
            if(card.scrollbar&&card.scrollMaximum>0)thumb.InsertKeyFrame(progress,card.scrollbarTop+(card.scrollbarTrack-card.scrollbar.Size().y)*std::clamp(positions[i]/card.scrollMaximum,0.f,1.f),linear);
        }
        card.scroller.StartAnimation(L"Offset.Y",movement);
        if(card.scrollbar&&card.scrollMaximum>0)card.scrollbar.StartAnimation(L"Offset.Y",thumb);
    });
}

#include "MediaSession.h"
// Artwork-derived analogous palette. Lift luminance instead of displaying dark saturated blue.
float MediaHue(UINT rgb){
 float r=((rgb>>16)&255)/255.f,g=((rgb>>8)&255)/255.f,b=(rgb&255)/255.f;
 float hi=std::max({r,g,b}),lo=std::min({r,g,b}),delta=hi-lo;
 if(delta<.05f)return 205.f;
 float hue=hi==r?(g-b)/delta:hi==g?2.f+(b-r)/delta:4.f+(r-g)/delta;
 return std::fmod(hue*60.f+360.f,360.f);
}
Windows::UI::Color MediaTone(float hue){
 hue=std::fmod(hue+360.f,360.f);constexpr float saturation=.62f,value=.96f;
 float chroma=value*saturation,x=chroma*(1-std::abs(std::fmod(hue/60.f,2.f)-1)),m=value-chroma;
 float r=0,g=0,b=0;
 if(hue<60){r=chroma;g=x;}else if(hue<120){r=x;g=chroma;}else if(hue<180){g=chroma;b=x;}else if(hue<240){g=x;b=chroma;}else if(hue<300){r=x;b=chroma;}else{r=chroma;b=x;}
 return {255,BYTE((r+m)*255),BYTE((g+m)*255),BYTE((b+m)*255)};
}
// Randomize placement once per track, never on animation frames.
UINT MediaMix(UINT value){value^=value>>16;value*=0x7feb352d;value^=value>>15;value*=0x846ca68b;return value^(value>>16);}
Windows::UI::Color MediaRingColor(UINT rgb,int segment,UINT seed=1){
 UINT choice=MediaMix(seed^MediaMix(static_cast<UINT>(segment)+1));
 auto tone=MediaTone(MediaHue(rgb)+((choice&1)?42.f:0.f));
 float brightness=.82f+.18f*((choice>>8)&255)/255.f;
 return {255,BYTE(tone.R*brightness),BYTE(tone.G*brightness),BYTE(tone.B*brightness)};
}
extern "C" __declspec(dllexport) UINT __cdecl AxiMedia_Palette(UINT rgb,int segment) noexcept {
 auto color=segment<0?MediaTone(MediaHue(rgb)):MediaRingColor(rgb,segment%40);
 return 0xff000000|(UINT(color.R)<<16)|(UINT(color.G)<<8)|color.B;
}

extern "C" __declspec(dllexport) HRESULT __cdecl AxiMedia_Visual(HWND hwnd,BOOL active,UINT color,int feedback,int pressed,UINT paletteSeed) noexcept {
 return Guard([&]{auto& card=GetCard(hwnd);auto c=renderer->compositor;
 if(!IsWindowVisible(hwnd)&&card.mediaProgress){card.mediaProgress.StopAnimation(L"Size.X");card.mediaThumb.StopAnimation(L"Offset.X");card.mediaProgressFraction=-1;}
 if(card.mediaBars.empty()){
 card.mediaEnvelope=c.CreatePropertySet();card.mediaEnvelope.InsertScalar(L"Strength",0);card.mediaEnvelope.InsertVector3(L"Signal",{0,0,0});card.mediaEnvelope.InsertVector3(L"BandsA",{0,0,0});card.mediaEnvelope.InsertVector3(L"BandsB",{0,0,0});card.mediaAudioLinear=c.CreateLinearEasingFunction();card.mediaAudioTween=c.CreateVector3KeyFrameAnimation();card.mediaAudioTween.Duration(std::chrono::milliseconds(35));card.mediaAudioTween.InsertExpressionKeyFrame(0,L"this.StartingValue");
 auto ring=c.CreateShapeVisual();ring.Size({364,162});card.root.Children().InsertBelow(ring,card.textVisual);
 auto play=c.CreateEllipseGeometry();play.Center({182,81});play.Radius({20,20});auto playShape=c.CreateSpriteShape(play);card.mediaPlayBrush=c.CreateColorBrush({255,112,191,255});playShape.FillBrush(card.mediaPlayBrush);ring.Shapes().Append(playShape);
 for(int i=0;i<40;i++){auto g=c.CreateRoundedRectangleGeometry();g.Size({2.6f,2.6f});g.CornerRadius({1.3f,1.3f});g.Offset({-1.3f,-33.3f});g.Properties().InsertScalar(L"Height",0);g.Properties().InsertScalar(L"Energy",0);auto size=c.CreateExpressionAnimation(L"Vector2(2.6, 2.6 + envelope.Strength * Min(22, .5 + wave.Height * .07 + wave.Energy * (10 + wave.Height * .65)))");size.SetReferenceParameter(L"envelope",card.mediaEnvelope);size.SetReferenceParameter(L"wave",g.Properties());g.StartAnimation(L"Size",size);auto offset=c.CreateExpressionAnimation(L"Vector2(-1.3, -33.3 - 0.70 * envelope.Strength * Min(22, .5 + wave.Height * .07 + wave.Energy * (10 + wave.Height * .65)))");offset.SetReferenceParameter(L"envelope",card.mediaEnvelope);offset.SetReferenceParameter(L"wave",g.Properties());g.StartAnimation(L"Offset",offset);auto shape=c.CreateSpriteShape(g);auto brush=c.CreateColorBrush({255,100,200,255});shape.FillBrush(brush);shape.RotationAngleInDegrees(i*9.f);shape.Offset({182,81});ring.Shapes().Append(shape);card.mediaBars.push_back(g);card.mediaBrushes.push_back(brush);}
 card.mediaFeedback=c.CreateSpriteVisual();card.mediaFeedback.Size({32,32});card.mediaFeedback.Brush(c.CreateColorBrush({255,255,255,255}));auto clip=c.CreateRectangleClip();clip.Right(32);clip.Bottom(32);clip.TopLeftRadius({16,16});clip.TopRightRadius({16,16});clip.BottomLeftRadius({16,16});clip.BottomRightRadius({16,16});card.mediaFeedback.Clip(clip);card.mediaFeedback.Opacity(0);card.root.Children().InsertAtTop(card.mediaFeedback);
 }
 if(card.mediaColor!=color||(paletteSeed!=0&&card.mediaPaletteSeed!=paletteSeed)){card.mediaColor=color;if(paletteSeed!=0)card.mediaPaletteSeed=paletteSeed;auto playColor=c.CreateColorKeyFrameAnimation();playColor.Duration(std::chrono::milliseconds(400));playColor.InsertExpressionKeyFrame(0,L"this.StartingValue");playColor.InsertKeyFrame(1,MediaTone(MediaHue(color)));card.mediaPlayBrush.StartAnimation(L"Color",playColor);for(int i=0;i<40;i++){auto a=c.CreateColorKeyFrameAnimation();a.Duration(std::chrono::milliseconds(400));a.InsertExpressionKeyFrame(0,L"this.StartingValue");a.InsertKeyFrame(1,MediaRingColor(color,i,card.mediaPaletteSeed));card.mediaBrushes[i].StartAnimation(L"Color",a);
 UINT assignment=MediaMix(card.mediaPaletteSeed+static_cast<UINT>(i)*17);int band=static_cast<int>(assignment%6),neighbor=(band+1)%6;
 auto channel=[](int index){return std::wstring(index<3?L"envelope.BandsA.":L"envelope.BandsB.")+L"XYZ"[index%3];};
 auto energy=c.CreateExpressionAnimation(L".82 * "+channel(band)+L" + .18 * "+channel(neighbor));energy.SetReferenceParameter(L"envelope",card.mediaEnvelope);card.mediaBars[i].Properties().StartAnimation(L"Energy",energy);
}}
 if(card.mediaActive!=bool(active)){
 AudioReactive::Enable(hwnd,active!=FALSE);card.mediaActive=active;auto batch=c.CreateScopedBatch(CompositionBatchTypes::Animation);
 auto strength=c.CreateScalarKeyFrameAnimation();strength.Duration(std::chrono::milliseconds(300));strength.InsertExpressionKeyFrame(0,L"this.StartingValue");strength.InsertKeyFrame(1,active?1.f:0.f,c.CreateCubicBezierEasingFunction({.25f,.1f},{.25f,1.f}));card.mediaEnvelope.StartAnimation(L"Strength",strength);
 // Different long, seamless periods avoid the synchronized six-second pumping pattern.
 if(active){for(int i=0;i<40;i++){
 auto a=c.CreateScalarKeyFrameAnimation();int seconds=24+i%7;int samples=seconds*30;
 a.Duration(std::chrono::seconds(seconds));a.IterationBehavior(AnimationIterationBehavior::Forever);auto linear=c.CreateLinearEasingFunction();
 float angle=i*6.2831853f/40;
 for(int f=0;f<=samples;f++){
 float t=f/static_cast<float>(samples)*6.2831853f;
 float slow=.5f+.5f*std::sin(t+angle*2);
 float wave=.50f+.23f*std::sin(t*5-angle*3+.45f*std::sin(t*2))+.17f*std::sin(t*9+angle*5)+.10f*std::sin(t*13-angle*7);
 float h=3.f+14.f*wave*(.55f+.45f*slow);
 a.InsertKeyFrame(f/static_cast<float>(samples),h,linear);
 }
 card.mediaBars[i].Properties().StartAnimation(L"Height",a);
 }}
 else batch.Completed([hwnd](auto const&,auto const&){auto found=cards.find(hwnd);if(found!=cards.end()&&!found->second->mediaActive)for(auto const& g:found->second->mediaBars)g.Properties().StopAnimation(L"Height");});
 batch.End();
 }
 float x=feedback==0?166.f:feedback==1?49.f:feedback==2?283.f:feedback==3?97.f:235.f;
 if(card.mediaFeedbackId!=feedback||card.mediaFeedbackPressed!=pressed){card.mediaFeedbackId=feedback;card.mediaFeedbackPressed=pressed;card.mediaFeedback.Offset({x,65,0});card.Animate(card.mediaFeedback,L"Opacity",feedback<0?0.f:pressed?.22f:.10f,100);}
 TRACKMOUSEEVENT track{sizeof(TRACKMOUSEEVENT),TME_LEAVE,hwnd,0};if(feedback>=0)TrackMouseEvent(&track);
 });
}


// A separate small surface keeps the static title and controls untouched by the clock.
extern "C" __declspec(dllexport) HRESULT __cdecl AxiMedia_Timeline(HWND hwnd,DrawCommand const* commands,int count,float fraction,double speed,BOOL reset) noexcept {
 return Guard([&]{if(!commands||count<1||count>20)throw hresult_invalid_argument();auto& card=GetCard(hwnd);auto c=renderer->compositor;
 if(!card.mediaTimeline){card.mediaTimeline=c.CreateSpriteVisual();card.mediaTimeline.Size({364,28});card.mediaTimeline.Offset({0,125,0});card.root.Children().InsertAtTop(card.mediaTimeline);}
 if(!card.mediaTimeSurface||card.mediaTimeDpi!=card.scale){card.mediaTimeDpi=card.scale;card.mediaTimeSurface=renderer->graphics.CreateDrawingSurface({364*card.scale,28*card.scale},Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);card.mediaTimeline.Brush(c.CreateSurfaceBrush(card.mediaTimeSurface));}
 if(card.mediaPlayBrush){if(!card.mediaProgress){card.mediaProgress=c.CreateSpriteVisual();card.mediaProgress.Offset({61,138,0});card.mediaProgress.Brush(card.mediaPlayBrush);card.root.Children().InsertAtTop(card.mediaProgress);card.mediaThumb=c.CreateSpriteVisual();card.mediaThumb.Size({7,7});card.mediaThumb.Brush(card.mediaPlayBrush);auto clip=c.CreateRectangleClip();clip.Right(7);clip.Bottom(7);clip.TopLeftRadius({3.5f,3.5f});clip.TopRightRadius({3.5f,3.5f});clip.BottomLeftRadius({3.5f,3.5f});clip.BottomRightRadius({3.5f,3.5f});card.mediaThumb.Clip(clip);card.root.Children().InsertAtTop(card.mediaThumb);}auto now=std::chrono::steady_clock::now();
 double predicted=card.mediaProgressFraction+card.mediaProgressSpeed*std::chrono::duration<double>(now-card.mediaProgressAt).count();
 // One linear compositor animation runs to the end. One-second text ticks do not restart it.
 bool resync=reset||card.mediaProgressFraction<0||std::abs(speed-card.mediaProgressSpeed)>1e-9||std::abs(std::clamp(predicted,0.,1.)-fraction)>std::max(.0001,speed*.5);
 if(resync){
 card.mediaProgress.StopAnimation(L"Size.X");card.mediaThumb.StopAnimation(L"Offset.X");
 float length=242*std::clamp(fraction,0.f,1.f);card.mediaProgress.Size({length,3});card.mediaThumb.Offset({58+length,136,0});
 card.mediaProgressFraction=fraction;card.mediaProgressSpeed=speed;card.mediaProgressAt=now;
 if(speed>0&&fraction>=0&&fraction<1){double seconds=std::clamp((1-fraction)/speed,.001,86400.);
 auto line=c.CreateScalarKeyFrameAnimation();auto thumb=c.CreateScalarKeyFrameAnimation();auto linear=c.CreateLinearEasingFunction();
 auto duration=std::chrono::duration_cast<Windows::Foundation::TimeSpan>(std::chrono::duration<double>(seconds));line.Duration(duration);thumb.Duration(duration);
 float target=242*static_cast<float>(std::min(1.,fraction+speed*seconds));line.InsertKeyFrame(0,length);line.InsertKeyFrame(1,target,linear);thumb.InsertKeyFrame(0,58+length);thumb.InsertKeyFrame(1,58+target,linear);
 card.mediaProgress.StartAnimation(L"Size.X",line);card.mediaThumb.StartAnimation(L"Offset.X",thumb);
 }
 }
 card.mediaThumb.Opacity(fraction<0?0.f:1.f);}
 auto saved=card.surface;auto content=std::move(card.commands);card.surface=card.mediaTimeSurface;card.commands.assign(commands,commands+count);
 try{card.Draw();}catch(...){card.surface=saved;card.commands=std::move(content);throw;}card.surface=saved;card.commands=std::move(content);
 });
}

struct AudioLevels {float level,bass,hit;HRESULT error;uint64_t packets;DWORD process;HRESULT fallback;};
extern "C" __declspec(dllexport) HRESULT __cdecl AxiMedia_AudioPulse(HWND hwnd,AudioLevels* result) noexcept {
 return Guard([&]{auto& card=GetCard(hwnd);auto sample=AudioReactive::Read();if(result)*result={sample.level,sample.bass,sample.hit,sample.error,sample.packets,sample.process,sample.fallback};
 if(!card.mediaActive||!card.mediaEnvelope)return;
 card.mediaAudioTween.InsertKeyFrame(1,{sample.level,sample.bass,sample.hit},card.mediaAudioLinear);card.mediaEnvelope.StartAnimation(L"Signal",card.mediaAudioTween);
 card.mediaAudioTween.InsertKeyFrame(1,{sample.bands[0],sample.bands[1],sample.bands[2]},card.mediaAudioLinear);card.mediaEnvelope.StartAnimation(L"BandsA",card.mediaAudioTween);
 card.mediaAudioTween.InsertKeyFrame(1,{sample.bands[3],sample.bands[4],sample.bands[5]},card.mediaAudioLinear);card.mediaEnvelope.StartAnimation(L"BandsB",card.mediaAudioTween);
 });
}

extern "C" __declspec(dllexport) HRESULT __cdecl AxiMedia_AudioSelfTest() noexcept {
 return Guard([]{
 auto energy=[](double hz){AudioReactive::BandFilter f;double sum=0;for(int i=0;i<48000;i++){double y=f.Process(.5*std::sin(6.283185307179586*hz*i/48000),.99543,.02177);if(i>=24000)sum+=y*y;}return sum;};
 double bass=energy(60),treble=energy(1000);if(bass<treble*25||!std::isfinite(bass))throw hresult_error(E_FAIL,L"Bass filter did not reject the high-frequency tone");
 for(int tone=0;tone<AudioReactive::BandCount;tone++){
 double bandEnergy[AudioReactive::BandCount]{};
 for(int band=0;band<AudioReactive::BandCount;band++){AudioReactive::FrequencyBand filter(AudioReactive::Centers[band]);for(int i=0;i<12000;i++){double y=filter.Process(std::sin(6.283185307179586*AudioReactive::Centers[tone]*i/48000));if(i>6000)bandEnergy[band]+=y*y;}}
 for(int band=0;band<AudioReactive::BandCount;band++)if(band!=tone&&bandEnergy[band]>=bandEnergy[tone])throw hresult_error(E_FAIL,L"Frequency bands did not separate the test tones");
 }
 AudioReactive::BandFilter silence;for(int i=0;i<1000;i++)if(silence.Process(0,.99543,.02177)!=0)throw hresult_error(E_FAIL,L"Silence generated false bass");
 });
}
