#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include "PixelConvert.h"
#include "PhoneFrameMemory.h"
#include <vector>
#include <stdio.h>
#include <math.h>
int main() {
    int failures=0;
    // Width 6 exercises the scalar tail; padded stride tests plane addressing.
    for(unsigned width: {4u,6u,128u}) {
        unsigned height=64,stride=width+8;
        std::vector<uint8_t> input(stride*height*3/2),output(width*height*4+16,0xCD);
        for(size_t i=0;i<input.size();i++) input[i]=(uint8_t)((i*37+13)%256);
        Nv12ToBottomUpRgba(input.data(),width,height,stride,output.data());
        for(unsigned y=0;y<height;y++) for(unsigned x=0;x<width;x++) {
            float Y=(input[y*stride+x]-16)*1.16438356f;
            float U=input[stride*height+(y/2)*stride+(x&~1u)]-128.f;
            float V=input[stride*height+(y/2)*stride+(x&~1u)+1]-128.f;
            float rgb[]={Y+V*1.79274107f,Y-U*.21324861f-V*.53290933f,Y+U*2.11240179f};
            for(unsigned c=0;c<3;c++) {
                int wanted=int(rgb[c]<0?0:(rgb[c]>255?255:rgb[c]+.5f));
                if(abs(int(output[((height-1-y)*width+x)*4+c])-wanted)>1) failures++;
            }
            if(output[((height-1-y)*width+x)*4+3]!=255) failures++;
        }
        for(unsigned i=width*height*4;i<output.size();i++) if(output[i]!=0xCD) failures++;
    }
    // Exercise validation without touching the live shared-memory namespace.
    SharedImageMemory memory(0);
    uint8_t invalid[4]={};
    if(memory.Send(3840,2160,3840,4,SharedImageMemory::FORMAT_UINT8,SharedImageMemory::RESIZEMODE_LINEAR,
        SharedImageMemory::MIRRORMODE_DISABLED,1000,invalid)!=SharedImageMemory::SENDRES_TOOLARGE) failures++;
    printf("%s: pixel colors, bottom-up rows, padded strides, scalar tail, alpha, buffer guards, invalid frame size (%d failures).\n",failures?"FAIL":"PASS",failures);
    return failures?1:0;
}
