// Phone USB Camera shared-frame transport. MIT License; see ../LICENSE.
// API shape matches the MIT UnityCapture filter; wire format is project-specific.
#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <stdlib.h>
#define MAX_SHARED_IMAGE_SIZE (3840 * 2160 * 4)
#define UCASSERT(cond) ((void)0)

struct SharedImageMemory {
    enum { MAX_CAPNUM = 1, RECEIVE_MAX_WAIT = 200 };
    enum EFormat { FORMAT_UINT8, FORMAT_FP16_GAMMA, FORMAT_FP16_LINEAR };
    enum EResizeMode { RESIZEMODE_DISABLED, RESIZEMODE_LINEAR };
    enum EMirrorMode { MIRRORMODE_DISABLED, MIRRORMODE_HORIZONTALLY };
    enum EReceiveResult { RECEIVERES_CAPTUREINACTIVE, RECEIVERES_NEWFRAME, RECEIVERES_OLDFRAME };
    enum ESendResult { SENDRES_TOOLARGE, SENDRES_WARN_FRAMESKIP, SENDRES_OK };
    typedef void (*ReceiveCallbackFunc)(int,int,int,EFormat,EResizeMode,EMirrorMode,int,uint8_t*,void*);
    struct Header {
        uint32_t magic, width, height, stride, format, resize, mirror, timeout;
        uint64_t sequence, updated;
        uint8_t data[MAX_SHARED_IMAGE_SIZE];
    };
    HANDLE mutex = NULL, mapping = NULL;
    Header* frame = NULL;
    uint64_t previous = 0;
    uint8_t* snapshot = NULL;
    size_t snapshotSize = 0;
    explicit SharedImageMemory(int) {}
    int GetCapNum() const { return 0; }
    ~SharedImageMemory() {
        if (snapshot) free(snapshot);
        if (frame) UnmapViewOfFile(frame);
        if (mapping) CloseHandle(mapping);
        if (mutex) CloseHandle(mutex);
    }
    bool Open() {
        if (frame) return true;
        // Local session and default user DACL: no network, no global service,
        // no granting camera data access to other signed-in users.
        if (!mutex) mutex = CreateMutexW(NULL, FALSE, L"Local\\PhoneUsbCamera.v3.Mutex");
        if (!mutex) return false;
        if (!mapping) mapping = CreateFileMappingW(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, sizeof(Header), L"Local\\PhoneUsbCamera.v3.Frame");
        if (!mapping) return false;
        frame = (Header*)MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(Header));
        return frame != NULL;
    }
    bool Lock() { DWORD r = WaitForSingleObject(mutex, 100); return r == WAIT_OBJECT_0 || r == WAIT_ABANDONED; }
    bool SendIsReady() { return Open(); }
    ESendResult Send(int w,int h,int stride,DWORD size,EFormat format,EResizeMode resize,EMirrorMode mirror,int timeout,const uint8_t* data) {
        if (w <= 0 || h <= 0 || stride < w || format != FORMAT_UINT8 ||
            uint64_t(stride) * h * 4 != size || size > MAX_SHARED_IMAGE_SIZE) return SENDRES_TOOLARGE;
        if (!Open() || !Lock()) return SENDRES_WARN_FRAMESKIP;
        memcpy(frame->data, data, size);
        frame->width=w; frame->height=h; frame->stride=stride; frame->format=format;
        frame->resize=resize; frame->mirror=mirror; frame->timeout=timeout;
        frame->updated=GetTickCount64(); frame->sequence++;
        frame->magic=0x33435550;
        ReleaseMutex(mutex);
        return SENDRES_OK;
    }
    EReceiveResult Receive(ReceiveCallbackFunc callback, void* state) {
        if (!Open()) return RECEIVERES_CAPTUREINACTIVE;
        uint64_t until = GetTickCount64() + RECEIVE_MAX_WAIT;
        do {
            if (!Lock()) return RECEIVERES_CAPTUREINACTIVE;
            bool valid = frame->magic == 0x33435550 && frame->width > 0 && frame->height > 0 &&
                frame->width <= frame->stride && frame->format == FORMAT_UINT8 &&
                uint64_t(frame->stride) * frame->height * 4 <= MAX_SHARED_IMAGE_SIZE &&
                GetTickCount64() - frame->updated < 1000;
            if (!valid) { ReleaseMutex(mutex); return RECEIVERES_CAPTUREINACTIVE; }
            bool fresh = frame->sequence != previous;
            if (fresh || GetTickCount64() >= until) {
                previous=frame->sequence;
                int w=frame->width,h=frame->height,pitch=frame->stride;
                EResizeMode resize=(EResizeMode)frame->resize;
                EMirrorMode mirror=(EMirrorMode)frame->mirror;
                size_t size=size_t(pitch)*h*4;
                if(snapshotSize<size) {
                    uint8_t* allocated=(uint8_t*)realloc(snapshot,size);
                    if(!allocated) { ReleaseMutex(mutex); return RECEIVERES_CAPTUREINACTIVE; }
                    snapshot=allocated; snapshotSize=size;
                }
                memcpy(snapshot,frame->data,size);
                ReleaseMutex(mutex);
                // Do expensive consumer conversion after unlocking. A slow
                // OpenScreen reader must not serialize the phone decoder.
                callback(w,h,pitch,FORMAT_UINT8,resize,mirror,1000,snapshot,state);
                return fresh ? RECEIVERES_NEWFRAME : RECEIVERES_OLDFRAME;
            }
            ReleaseMutex(mutex);
            Sleep(2);
        } while (true);
    }
    void Clear() {
        if (Open() && Lock()) { frame->magic=0; SecureZeroMemory(frame->data,sizeof(frame->data)); ReleaseMutex(mutex); }
    }
};
