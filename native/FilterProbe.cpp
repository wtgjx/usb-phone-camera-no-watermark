// Tests a DLL directly, or the real COM registration with --registered.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <dshow.h>
#include <wrl/client.h>
#include <atomic>
#include <stdint.h>
#include <stdio.h>
using Microsoft::WRL::ComPtr;

MIDL_INTERFACE("0579154A-2B53-4994-B0D0-E773148EFF85") ISampleGrabberCB : public IUnknown {
    virtual HRESULT STDMETHODCALLTYPE SampleCB(double,IMediaSample*)=0;
    virtual HRESULT STDMETHODCALLTYPE BufferCB(double,BYTE*,long)=0;
};
MIDL_INTERFACE("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F") ISampleGrabber : public IUnknown {
    virtual HRESULT STDMETHODCALLTYPE SetOneShot(BOOL)=0;
    virtual HRESULT STDMETHODCALLTYPE SetMediaType(const AM_MEDIA_TYPE*)=0;
    virtual HRESULT STDMETHODCALLTYPE GetConnectedMediaType(AM_MEDIA_TYPE*)=0;
    virtual HRESULT STDMETHODCALLTYPE SetBufferSamples(BOOL)=0;
    virtual HRESULT STDMETHODCALLTYPE GetCurrentBuffer(long*,long*)=0;
    virtual HRESULT STDMETHODCALLTYPE GetCurrentSample(IMediaSample**)=0;
    virtual HRESULT STDMETHODCALLTYPE SetCallback(ISampleGrabberCB*,long)=0;
};
const CLSID PhoneSource={0x87493a16,0x2cf8,0x4781,{0x9a,0x51,0x8b,0x06,0x74,0xf1,0x00,0x10}};
const CLSID SampleGrabber={0xc1f400a0,0x3f08,0x11d3,{0x9f,0x0b,0x00,0x60,0x08,0x03,0x9e,0x37}};
const CLSID NullRenderer={0xc1f400a4,0x3f08,0x11d3,{0x9f,0x0b,0x00,0x60,0x08,0x03,0x9e,0x37}};
struct Callback : ISampleGrabberCB {
    std::atomic<long> frames{0},changed{0},badTime{0};
    uint64_t lastHash=0; double lastTime=-1;
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** out) override {
        if(!out) return E_POINTER; *out=NULL;
        if(iid==IID_IUnknown || iid==__uuidof(ISampleGrabberCB)) { *out=this; return S_OK; }
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return 2; }
    ULONG STDMETHODCALLTYPE Release() override { return 1; }
    HRESULT STDMETHODCALLTYPE SampleCB(double,IMediaSample*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE BufferCB(double time,BYTE* data,long size) override {
        uint64_t hash=14695981039346656037ull;
        for(long i=0;i<size;i+=127) { hash^=data[i]; hash*=1099511628211ull; }
        if(hash!=lastHash) changed++;
        if(time<=lastTime) badTime++;
        lastHash=hash; lastTime=time; frames++;
        return S_OK;
    }
};
#define TEST(x) do { HRESULT r=(x); if(FAILED(r)) { printf("FAIL line=%d hr=0x%08lx\n",__LINE__,r); return 1; } } while(0)
int wmain(int argc,wchar_t** argv) {
    if(argc<2) return 2;
    TEST(CoInitializeEx(NULL,COINIT_MULTITHREADED));
    bool registered=wcscmp(argv[1],L"--registered")==0;
    Callback callback;
    HMODULE module=NULL;
    ComPtr<IClassFactory> factory;
    ComPtr<IBaseFilter> source;
    if(registered) { TEST(CoCreateInstance(PhoneSource,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&source))); }
    else {
        module=LoadLibraryExW(argv[1],NULL,LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR|LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if(!module) { printf("LoadLibrary error=%lu\n",GetLastError()); return 1; }
        auto factoryFn=(HRESULT (STDAPICALLTYPE*)(REFCLSID,REFIID,void**))GetProcAddress(module,"DllGetClassObject");
        if(!factoryFn) return 1;
        TEST(factoryFn(PhoneSource,IID_PPV_ARGS(&factory)));
        TEST(factory->CreateInstance(NULL,IID_PPV_ARGS(&source)));
    }
    ComPtr<IGraphBuilder> graph; TEST(CoCreateInstance(CLSID_FilterGraph,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&graph)));
    ComPtr<ICaptureGraphBuilder2> builder; TEST(CoCreateInstance(CLSID_CaptureGraphBuilder2,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&builder)));
    TEST(builder->SetFiltergraph(graph.Get()));
    TEST(graph->AddFilter(source.Get(),L"Phone USB Camera test"));
    if(argc>4) {
        ComPtr<IAMStreamConfig> config;
        TEST(builder->FindInterface(&PIN_CATEGORY_CAPTURE,&MEDIATYPE_Video,source.Get(),IID_IAMStreamConfig,(void**)&config));
        AM_MEDIA_TYPE* type=NULL; TEST(config->GetFormat(&type));
        auto video=(VIDEOINFOHEADER*)type->pbFormat;
        video->bmiHeader.biWidth=_wtoi(argv[3]); video->bmiHeader.biHeight=_wtoi(argv[4]);
        video->bmiHeader.biSizeImage=video->bmiHeader.biWidth*video->bmiHeader.biHeight*video->bmiHeader.biBitCount/8;
        type->lSampleSize=video->bmiHeader.biSizeImage;
        HRESULT set=config->SetFormat(type);
        if(type->cbFormat) CoTaskMemFree(type->pbFormat);
        if(type->pUnk) type->pUnk->Release(); CoTaskMemFree(type);
        TEST(set);
    }
    ComPtr<IBaseFilter> grabberFilter; TEST(CoCreateInstance(SampleGrabber,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&grabberFilter)));
    ComPtr<ISampleGrabber> grabber; TEST(grabberFilter.As(&grabber));
    AM_MEDIA_TYPE type={}; type.majortype=MEDIATYPE_Video; type.subtype=MEDIASUBTYPE_RGB24; type.formattype=FORMAT_VideoInfo;
    TEST(grabber->SetMediaType(&type)); TEST(grabber->SetCallback(&callback,1));
    TEST(graph->AddFilter(grabberFilter.Get(),L"Frame statistics"));
    ComPtr<IBaseFilter> sink; TEST(CoCreateInstance(NullRenderer,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&sink)));
    TEST(graph->AddFilter(sink.Get(),L"Discard video"));
    TEST(builder->RenderStream(&PIN_CATEGORY_CAPTURE,&MEDIATYPE_Video,source.Get(),grabberFilter.Get(),sink.Get()));
    ComPtr<IMediaControl> control; TEST(graph.As(&control)); TEST(control->Run());
    int seconds=argc>2?_wtoi(argv[2]):12;
    long previousChanged=0; bool continuous=true;
    for(int i=0;i<seconds;i++) {
        Sleep(1000);
        printf("second=%d consumerFrames=%ld changed=%ld badTimestamp=%ld\n",i+1,callback.frames.load(),callback.changed.load(),callback.badTime.load());
        fflush(stdout);
        long changedNow=callback.changed.load();
        if(i>=3 && changedNow-previousChanged<20) continuous=false;
        previousChanged=changedNow;
    }
    TEST(control->Stop()); grabber->SetCallback(NULL,1);
    bool pass=continuous && callback.frames>=seconds*24 && callback.changed>=seconds*20 && callback.badTime==0 && callback.lastTime>=seconds-1.5;
    printf("%s: %s DirectShow camera consumer; video not saved.\n",pass?"PASS":"FAIL",registered?"registered":"unregistered");
    // Keep DLL loaded until process exit; graph's COM objects still refer to it.
    return pass?0:1;
}
