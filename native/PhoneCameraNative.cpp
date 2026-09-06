// Windows built-in H.264 decoder -> independent camera shared memory.
// No OBS, window capture, scrcpy desktop client, or external decoder process.
#include "PhoneFrameMemory.h"
#include <mfapi.h>
#include <mfidl.h>
#include <mftransform.h>
#include <mferror.h>
#include <wmcodecdsp.h>
#include <codecapi.h>
#include <wrl/client.h>
#include <vector>
#include <new>
#include "PixelConvert.h"
using Microsoft::WRL::ComPtr;
#define CHECK(x) do { HRESULT r=(x); if(FAILED(r)) return r; } while(0)

struct Decoder {
    ComPtr<IMFTransform> transform;
    SharedImageMemory output{0};
    unsigned width=0, height=0;
    LONG stride=0;
    bool co=false, mf=false;
    std::vector<uint8_t> rgba;
    std::vector<uint8_t> transformed;
    unsigned outputWidth=0, outputHeight=0;
    int rotation=0, mirror=0;
    const uint8_t* Pixels() const { return rotation || mirror ? transformed.data() : rgba.data(); }
    void Transform() {
        outputWidth=(rotation==90 || rotation==270)?height:width;
        outputHeight=(rotation==90 || rotation==270)?width:height;
        if(!rotation && !mirror) return;
        transformed.resize(rgba.size());
        concurrency::parallel_for(0u,outputHeight,[&](unsigned y) {
            for(unsigned x=0;x<outputWidth;x++) {
                unsigned dx=mirror?outputWidth-1-x:x, sx=dx,sy=y;
                if(rotation==90) { sx=y; sy=height-1-dx; }
                else if(rotation==180) { sx=width-1-dx; sy=height-1-y; }
                else if(rotation==270) { sx=width-1-y; sy=dx; }
                ((uint32_t*)transformed.data())[(outputHeight-1-y)*outputWidth+x]=
                    ((uint32_t*)rgba.data())[(height-1-sy)*width+sx];
            }
        });
    }
    uint64_t fingerprint=0;
    uint64_t convertMs=0, publishMs=0, totalMs=0, frameCount=0;
    ~Decoder() { output.Clear(); transform.Reset(); if(mf) MFShutdown(); if(co) CoUninitialize(); }
    HRESULT Initialize(unsigned w, unsigned h) {
        HRESULT hr=CoInitializeEx(NULL,COINIT_MULTITHREADED); co=SUCCEEDED(hr);
        if (FAILED(hr) && hr!=RPC_E_CHANGED_MODE) return hr;
        CHECK(MFStartup(MF_VERSION)); mf=true;
        CHECK(CoCreateInstance(CLSID_CMSH264DecoderMFT,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&transform)));
        ComPtr<IMFAttributes> attr;
        if(SUCCEEDED(transform->GetAttributes(&attr))) attr->SetUINT32(MF_LOW_LATENCY,TRUE);
        ComPtr<ICodecAPI> codec;
        if(SUCCEEDED(transform.As(&codec))) {
            VARIANT workers; VariantInit(&workers); workers.vt=VT_UI4; workers.ulVal=4;
            codec->SetValue(&CODECAPI_AVDecNumWorkerThreads,&workers);
        }
        ComPtr<IMFMediaType> input; CHECK(MFCreateMediaType(&input));
        CHECK(input->SetGUID(MF_MT_MAJOR_TYPE,MFMediaType_Video));
        CHECK(input->SetGUID(MF_MT_SUBTYPE,MFVideoFormat_H264));
        CHECK(input->SetUINT32(MF_MT_INTERLACE_MODE,MFVideoInterlace_Progressive));
        CHECK(MFSetAttributeSize(input.Get(),MF_MT_FRAME_SIZE,w,h));
        CHECK(MFSetAttributeRatio(input.Get(),MF_MT_FRAME_RATE,30,1));
        CHECK(transform->SetInputType(0,input.Get(),0));
        CHECK(ConfigureOutput());
        CHECK(transform->ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING,0));
        CHECK(transform->ProcessMessage(MFT_MESSAGE_NOTIFY_START_OF_STREAM,0));
        return S_OK;
    }
    HRESULT ConfigureOutput() {
        for(DWORD index=0;;index++) {
            ComPtr<IMFMediaType> type; CHECK(transform->GetOutputAvailableType(0,index,&type));
            GUID subtype; CHECK(type->GetGUID(MF_MT_SUBTYPE,&subtype));
            if(subtype!=MFVideoFormat_NV12) continue;
            CHECK(transform->SetOutputType(0,type.Get(),0));
            CHECK(MFGetAttributeSize(type.Get(),MF_MT_FRAME_SIZE,&width,&height));
            if(!width || !height || width%2 || height%2 || uint64_t(width)*height*4>MAX_SHARED_IMAGE_SIZE) return E_INVALIDARG;
            UINT32 pitch=0;
            stride=SUCCEEDED(type->GetUINT32(MF_MT_DEFAULT_STRIDE,&pitch)) ? LONG(pitch) : LONG(width);
            rgba.resize(size_t(width)*height*4);
            return S_OK;
        }
    }
    static uint8_t Clip(int c) { return (uint8_t)(c<0?0:(c>255?255:c)); }
    HRESULT Convert(IMFSample* sample) {
        ComPtr<IMFMediaBuffer> buffer; CHECK(sample->ConvertToContiguousBuffer(&buffer));
        BYTE* data=NULL; DWORD size=0;
        CHECK(buffer->Lock(&data,NULL,&size));
        LONG pitch=stride;
        if(pitch<LONG(width) || uint64_t(pitch)*height*3/2>size) { buffer->Unlock(); return E_UNEXPECTED; }
        Nv12ToBottomUpRgba(data,width,height,pitch,rgba.data());
        buffer->Unlock();
        fingerprint=14695981039346656037ull;
        for(size_t offset=0;offset<rgba.size();offset+=128) { fingerprint^=rgba[offset]; fingerprint*=1099511628211ull; }
        return S_OK;
    }
    HRESULT Feed(const uint8_t* data, int count, int64_t pts, int* frames) {
        uint64_t started=GetTickCount64();
        *frames=0;
        ComPtr<IMFSample> input; ComPtr<IMFMediaBuffer> buffer;
        CHECK(MFCreateSample(&input)); CHECK(MFCreateMemoryBuffer(count,&buffer));
        BYTE* bytes; CHECK(buffer->Lock(&bytes,NULL,NULL)); memcpy(bytes,data,count); buffer->Unlock();
        CHECK(buffer->SetCurrentLength(count)); CHECK(input->AddBuffer(buffer.Get()));
        CHECK(input->SetSampleTime(pts*10)); CHECK(input->SetSampleDuration(10000000/30));
        CHECK(transform->ProcessInput(0,input.Get(),0));
        for(;;) {
            MFT_OUTPUT_STREAM_INFO info; CHECK(transform->GetOutputStreamInfo(0,&info));
            ComPtr<IMFSample> sample;
            if(!(info.dwFlags&MFT_OUTPUT_STREAM_PROVIDES_SAMPLES)) {
                ComPtr<IMFMediaBuffer> out; CHECK(MFCreateSample(&sample));
                CHECK(MFCreateMemoryBuffer(info.cbSize,&out)); CHECK(sample->AddBuffer(out.Get()));
            }
            MFT_OUTPUT_DATA_BUFFER outputBuffer={0,sample.Get(),0,NULL}; DWORD status=0;
            HRESULT result=transform->ProcessOutput(0,1,&outputBuffer,&status);
            if(outputBuffer.pEvents) outputBuffer.pEvents->Release();
            if(result==MF_E_TRANSFORM_STREAM_CHANGE) { CHECK(ConfigureOutput()); continue; }
            if(result==MF_E_TRANSFORM_NEED_MORE_INPUT) { totalMs+=GetTickCount64()-started; return S_OK; }
            CHECK(result);
            if(!sample) sample.Attach(outputBuffer.pSample);
            uint64_t step=GetTickCount64();
            CHECK(Convert(sample.Get())); convertMs+=GetTickCount64()-step;
            step=GetTickCount64();
            Transform();
            output.Send(outputWidth,outputHeight,outputWidth,(DWORD)rgba.size(),SharedImageMemory::FORMAT_UINT8,
                SharedImageMemory::RESIZEMODE_LINEAR,SharedImageMemory::MIRRORMODE_DISABLED,1000,Pixels());
            publishMs+=GetTickCount64()-step; frameCount++;
            (*frames)++;
        }
    }
};
extern "C" __declspec(dllexport) HRESULT __cdecl PhoneDecoderCreate(int w,int h,void** result) {
    if(!result || w<=0 || h<=0 || w%2 || h%2 || uint64_t(w)*h*4>MAX_SHARED_IMAGE_SIZE) return E_INVALIDARG;
    *result=NULL;
    Decoder* decoder=new(std::nothrow) Decoder(); if(!decoder) return E_OUTOFMEMORY;
    HRESULT hr=decoder->Initialize(w,h); if(FAILED(hr)) { delete decoder; return hr; }
    *result=decoder; return S_OK;
}
extern "C" __declspec(dllexport) HRESULT __cdecl PhoneDecoderFeed(void* handle,const uint8_t* packet,int size,int64_t pts,int* frames) {
    if(!handle || !packet || size<=0 || size>32*1024*1024 || !frames) return E_INVALIDARG;
    return ((Decoder*)handle)->Feed(packet,size,pts,frames);
}
extern "C" __declspec(dllexport) void __cdecl PhoneDecoderClose(void* handle) { delete (Decoder*)handle; }
extern "C" __declspec(dllexport) uint64_t __cdecl PhoneDecoderFingerprint(void* handle) { return handle ? ((Decoder*)handle)->fingerprint : 0; }
extern "C" __declspec(dllexport) void __cdecl PhoneDecoderSetTransform(void* handle,int rotation,int mirror) {
    if(!handle || (rotation!=0 && rotation!=90 && rotation!=180 && rotation!=270)) return;
    Decoder* d=(Decoder*)handle;
    if(d->rotation!=rotation || d->mirror!=mirror) { d->rotation=rotation; d->mirror=mirror!=0; d->Transform(); }
}
extern "C" __declspec(dllexport) void __cdecl PhoneDecoderFrameSize(void* handle,int* w,int* h) {
    if(!handle || !w || !h) return;
    Decoder* d=(Decoder*)handle; *w=d->outputWidth; *h=d->outputHeight;
}
extern "C" __declspec(dllexport) HRESULT __cdecl PhoneDecoderCopyPreview(void* handle,uint8_t* pixels,int w,int h,int stride) {
    if(!handle || !pixels || w<1 || h<1 || w>1920 || h>1920 || stride<w*4) return E_INVALIDARG;
    Decoder* d=(Decoder*)handle; if(!d->outputWidth || !d->outputHeight) return E_UNEXPECTED;
    const uint8_t* input=d->Pixels();
    for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
        unsigned sx=uint64_t(x)*d->outputWidth/w,sy=uint64_t(y)*d->outputHeight/h;
        const uint8_t* src=input+(size_t(d->outputHeight-1-sy)*d->outputWidth+sx)*4;
        uint8_t* dst=pixels+size_t(y)*stride+x*4;
        dst[0]=src[2]; dst[1]=src[1]; dst[2]=src[0]; dst[3]=255;
    }
    return S_OK;
}
extern "C" __declspec(dllexport) void __cdecl PhoneDecoderTimings(void* handle,double* times) {
    Decoder* d=(Decoder*)handle;
    if(!d || !times || !d->frameCount) return;
    times[0]=double(d->totalMs)/d->frameCount; times[1]=double(d->convertMs)/d->frameCount; times[2]=double(d->publishMs)/d->frameCount;
}
