// BT.709 limited-range NV12 -> bottom-up RGBA, SSE2 (baseline Windows x64).
#pragma once
#include <emmintrin.h>
#include <stdint.h>
#include <ppl.h>
inline void Nv12ToBottomUpRgba(const uint8_t* input,unsigned width,unsigned height,unsigned stride,uint8_t* output) {
    const uint8_t* chroma=input+size_t(stride)*height;
    concurrency::parallel_for(0u,(height+31)/32,[&](unsigned block) {
        const __m128i zero=_mm_setzero_si128(), alpha=_mm_set1_epi8(-1);
        for(unsigned y=block*32;y<height && y<(block+1)*32;y++) {
            const uint8_t* row=input+size_t(stride)*y;
            const uint8_t* uv=chroma+size_t(stride)*(y/2);
            uint8_t* dst=output+size_t(height-y-1)*width*4;
            unsigned x=0;
            for(;x+4<=width;x+=4) {
                __m128i y4=_mm_unpacklo_epi16(_mm_unpacklo_epi8(_mm_cvtsi32_si128(*(const int*)(row+x)),zero),zero);
                __m128i uv4=_mm_unpacklo_epi8(_mm_cvtsi32_si128(*(const int*)(uv+x)),zero);
                __m128 Y=_mm_mul_ps(_mm_sub_ps(_mm_cvtepi32_ps(y4),_mm_set1_ps(16.f)),_mm_set1_ps(1.16438356f));
                __m128 U=_mm_sub_ps(_mm_cvtepi32_ps(_mm_unpacklo_epi16(_mm_shufflelo_epi16(uv4,_MM_SHUFFLE(2,2,0,0)),zero)),_mm_set1_ps(128.f));
                __m128 V=_mm_sub_ps(_mm_cvtepi32_ps(_mm_unpacklo_epi16(_mm_shufflelo_epi16(uv4,_MM_SHUFFLE(3,3,1,1)),zero)),_mm_set1_ps(128.f));
                __m128 R=_mm_add_ps(Y,_mm_mul_ps(V,_mm_set1_ps(1.79274107f)));
                __m128 G=_mm_sub_ps(_mm_sub_ps(Y,_mm_mul_ps(U,_mm_set1_ps(0.21324861f))),_mm_mul_ps(V,_mm_set1_ps(0.53290933f)));
                __m128 B=_mm_add_ps(Y,_mm_mul_ps(U,_mm_set1_ps(2.11240179f)));
                __m128i r=_mm_packus_epi16(_mm_packs_epi32(_mm_cvtps_epi32(R),zero),zero);
                __m128i g=_mm_packus_epi16(_mm_packs_epi32(_mm_cvtps_epi32(G),zero),zero);
                __m128i b=_mm_packus_epi16(_mm_packs_epi32(_mm_cvtps_epi32(B),zero),zero);
                _mm_storeu_si128((__m128i*)(dst+x*4),_mm_unpacklo_epi16(_mm_unpacklo_epi8(r,g),_mm_unpacklo_epi8(b,alpha)));
            }
            for(;x<width;x++) {
                float Y=(row[x]-16)*1.16438356f, U=uv[x&~1u]-128.f, V=uv[(x&~1u)+1]-128.f;
                float colors[]={Y+V*1.79274107f,Y-U*0.21324861f-V*0.53290933f,Y+U*2.11240179f};
                for(unsigned c=0;c<3;c++) dst[x*4+c]=(uint8_t)(colors[c]<0?0:(colors[c]>255?255:colors[c]+.5f));
                dst[x*4+3]=255;
            }
        }
    });
}
