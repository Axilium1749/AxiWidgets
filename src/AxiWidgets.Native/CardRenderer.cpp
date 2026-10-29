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
    std::wstring time,date;
    ~Card(){if(target)target.Root(nullptr);if(hwnd&&IsWindow(hwnd))DestroyWindow(hwnd);}
    void Create(float w=300,float h=160){
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
        auto clip=c.CreateRectangleClip();clip.Left(0);clip.Top(0);clip.Right(width);clip.Bottom(height);clip.TopLeftRadius({30,30});clip.TopRightRadius({30,30});clip.BottomLeftRadius({30,30});clip.BottomRightRadius({30,30});root.Clip(clip);
        auto effect=r.blur.CreateBrush();effect.SetSourceParameter(L"desktop",c.CreateHostBackdropBrush());
        auto glass=c.CreateSpriteVisual();glass.Size({width,height});glass.Brush(effect);root.Children().InsertAtBottom(glass);
        tint=c.CreateColorBrush({140,30,30,34});auto shade=c.CreateSpriteVisual();shade.Size({width,height});shade.Brush(tint);root.Children().InsertAtTop(shade);
        textBrush=c.CreateSurfaceBrush();auto text=c.CreateSpriteVisual();text.Size({width,height});text.Brush(textBrush);root.Children().InsertAtTop(text);
        auto geometry=c.CreateRoundedRectangleGeometry();geometry.Size({width-.6f,height-.6f});geometry.Offset({.3f,.3f});geometry.CornerRadius({29.7f,29.7f});
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
                else if(cmd.kind==3)context->DrawLine({cmd.x,cmd.y},{cmd.x+cmd.w,cmd.y+cmd.h},brush.get(),cmd.size);
                else {
                    int key=static_cast<int>(cmd.size*10);
                    auto& format=renderer->formats[key];
                    if(!format){check_hresult(renderer->write->CreateTextFormat(L"Segoe UI",nullptr,DWRITE_FONT_WEIGHT_NORMAL,DWRITE_FONT_STYLE_NORMAL,DWRITE_FONT_STRETCH_NORMAL,cmd.size,L"",format.put()));format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);format->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);}
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


extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_CreateWidget(HWND* result,float width,float height) noexcept {
    if(!result)return E_POINTER;*result=nullptr;
    return Guard([&]{if(!renderer)renderer=std::make_unique<Renderer>();auto card=std::make_unique<Card>();card->Create(width,height);*result=card->hwnd;cards.emplace(card->hwnd,std::move(card));});
}
extern "C" __declspec(dllexport) HRESULT __cdecl AxiCard_SetContent(HWND hwnd,DrawCommand const* commands,int count) noexcept {
    return Guard([&]{if(count<0||count>1000||(!commands&&count))throw hresult_invalid_argument();auto& card=GetCard(hwnd);card.commands.assign(commands,commands+count);card.Draw();});
}
