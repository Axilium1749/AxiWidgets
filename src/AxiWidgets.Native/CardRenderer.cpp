#include <windows.h>
#include <dwmapi.h>
#include <dispatcherqueue.h>
#include <windows.ui.composition.interop.h>
#include <windows.graphics.effects.interop.h>
#include <d2d1effects.h>
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
    std::wstring time,date;
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
        textBrush=c.CreateSurfaceBrush();auto text=c.CreateSpriteVisual();text.Size({width,height});text.Brush(textBrush);root.Children().InsertAtTop(text);
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
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_Destroy(HWND hwnd) noexcept {return Guard([&]{cards.erase(hwnd);});}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_Shutdown() noexcept {return Guard([]{cards.clear();renderer.reset();});}


extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_CreateWidget(HWND* result,float width,float height,float radius) noexcept {
    if(!result)return E_POINTER;*result=nullptr;
    return Guard([&]{if(!renderer)renderer=std::make_unique<Renderer>();auto card=std::make_unique<Card>();card->Create(width,height,radius);*result=card->hwnd;cards.emplace(card->hwnd,std::move(card));});
}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_SetContent(HWND hwnd,DrawCommand const* commands,int count) noexcept {
    return Guard([&]{if(count<0||count>1000||(!commands&&count))throw hresult_invalid_argument();auto& card=GetCard(hwnd);card.SetCommands(std::vector<DrawCommand>(commands,commands+count));});
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
