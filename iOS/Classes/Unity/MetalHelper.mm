#include "UnityRendering.h"

#import <Metal/Metal.h>
#import <QuartzCore/QuartzCore.h>

#include "UnityAppController.h"
#include "CVTextureCache.h"

#include "ObjCRuntime.h"
#include <libkern/OSAtomic.h>
#include <utility>

extern "C" void InitRenderingMTL()
{
}

static MTLPixelFormat GetColorFormatForSurface(const UnityDisplaySurfaceMTL* surface)
{
    MTLPixelFormat colorFormat = MTLPixelFormatInvalid;

#if PLATFORM_IOS || PLATFORM_VISIONOS
    if (surface->hdr)
    {
        // 0 = 10 bit, 1 = 16bit
        if (@available(iOS 16.0, *))
            colorFormat = UnityHDRSurfaceDepth() == 0 ? MTLPixelFormatRGB10A2Unorm : MTLPixelFormatRGBA16Float;
    }
#endif

    if(colorFormat == MTLPixelFormatInvalid && surface->wideColor)
    {
        // at some point we tried using MTLPixelFormatBGR10_XR formats, but it seems that apple CoreImage have issues with that
        //   and we are not alone here, see for example https://forums.developer.apple.com/forums/thread/66166
        // when application goes to background the colors are changed (more white-ish?)
        // no matter what we tried, the issue persists
        // NOTE: the most funny thing is when we set color space to be P3 we get same whitish colors always
        // NOTE: but this time they become normal when going to background
        // in all, it seems that using rgba f16 is the most robust option here, so we are back to it again
        colorFormat = MTLPixelFormatRGBA16Float;
    }

    if(colorFormat == MTLPixelFormatInvalid)
        colorFormat = surface->srgb ? MTLPixelFormatBGRA8Unorm_sRGB : MTLPixelFormatBGRA8Unorm;

    return colorFormat;
}

static uint32_t GetCVPixelFormatForSurface(const UnityDisplaySurfaceMTL* surface)
{
    // this makes sense only for ios (at least we dont support this on macos)
    uint32_t colorFormat = kCVPixelFormatType_32BGRA;
#if PLATFORM_IOS || PLATFORM_TVOS || PLATFORM_VISIONOS
    if (surface->wideColor && UnityIsWideColorSupported())
        colorFormat = kCVPixelFormatType_30RGB;
#endif

    return colorFormat;
}

extern "C" void CreateSystemRenderingSurfaceMTL(UnityDisplaySurfaceMTL* surface)
{
    DestroySystemRenderingSurfaceMTL(surface);

    MTLPixelFormat colorFormat = GetColorFormatForSurface(surface);
    surface->swapchain.layer.presentsWithTransaction = NO;
    surface->swapchain.layer.drawsAsynchronously = YES;

    if (UnityPreserveFramebufferAlpha())
    {
        const CGFloat components[] = {1.0f, 1.0f, 1.0f, 0.0f};
        CGColorSpaceRef colorSpace = CGColorSpaceCreateDeviceRGB();
        CGColorRef color = CGColorCreate(colorSpace, components);
        surface->swapchain.layer.opaque = NO;
        surface->swapchain.layer.backgroundColor = color;
        CGColorRelease(color);
        CGColorSpaceRelease(colorSpace);
    }

    CGColorSpaceRef colorSpaceRef = nil;
    if (surface->hdr)
    {
        if (@available(iOS 16.0, *))
            colorSpaceRef = UnityHDRSurfaceDepth() == 0 ? CGColorSpaceCreateWithName(CFSTR("kCGColorSpaceITUR_2100_PQ")) : CGColorSpaceCreateWithName(CFSTR("kCGColorSpaceExtendedLinearITUR_2020"));
    }
    if(colorSpaceRef == nil)
    {
        if (surface->wideColor)
            colorSpaceRef = CGColorSpaceCreateWithName(surface->srgb ? kCGColorSpaceExtendedLinearSRGB : kCGColorSpaceExtendedSRGB);
        else
            colorSpaceRef = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
    }

    surface->swapchain.layer.colorspace = colorSpaceRef;
    CGColorSpaceRelease(colorSpaceRef);

    surface->swapchain.layer.device = surface->device;
    surface->swapchain.layer.pixelFormat = colorFormat;
    surface->swapchain.layer.framebufferOnly = (surface->framebufferOnly != 0);
    surface->colorFormat = (unsigned)colorFormat;
}

extern "C" void CreateRenderingSurfaceMTL(UnityDisplaySurfaceMTL* surface)
{
    DestroyRenderingSurfaceMTL(surface);

    MTLPixelFormat colorFormat = GetColorFormatForSurface(surface);

    const int w = surface->targetW, h = surface->targetH;

    if (w != surface->systemW || h != surface->systemH || surface->useCVTextureCache)
    {
        if (surface->useCVTextureCache)
            surface->cvTextureCache = CreateCVTextureCache();

        if (surface->cvTextureCache)
        {
            surface->cvTextureCacheTexture = CreateReadableRTFromCVTextureCache2(surface->cvTextureCache, surface->targetW, surface->targetH,
                GetCVPixelFormatForSurface(surface), colorFormat, &surface->cvPixelBuffer);
            surface->targetColorRT = GetMetalTextureFromCVTextureCache(surface->cvTextureCacheTexture);
        }
        else
        {
            MTLTextureDescriptor* txDesc = [MTLTextureDescriptor new];
            txDesc.textureType = MTLTextureType2D;
            txDesc.width = w;
            txDesc.height = h;
            txDesc.depth = 1;
            txDesc.pixelFormat = colorFormat;
            txDesc.arrayLength = 1;
            txDesc.mipmapLevelCount = 1;
            txDesc.usage = MTLTextureUsageRenderTarget | MTLTextureUsageShaderRead;
            surface->targetColorRT = [surface->device newTextureWithDescriptor: txDesc];
        }
        surface->targetColorRT.label = @"targetColorRT";

        UnityRegisterExternalRenderSurfaceTextureForMemoryProfiler(surface->targetColorRT);
    }

    if (surface->msaaSamples > 1)
    {
        MTLTextureDescriptor* txDesc = [MTLTextureDescriptor new];
        txDesc.textureType = MTLTextureType2DMultisample;
        txDesc.width = w;
        txDesc.height = h;
        txDesc.depth = 1;
        txDesc.pixelFormat = colorFormat;
        txDesc.arrayLength = 1;
        txDesc.mipmapLevelCount = 1;
        txDesc.sampleCount = surface->msaaSamples;
        txDesc.resourceOptions = MTLResourceStorageModePrivate;
        txDesc.usage = MTLTextureUsageRenderTarget | MTLTextureUsageShaderRead;
        if (![surface->device supportsTextureSampleCount: txDesc.sampleCount])
            txDesc.sampleCount = 4;
        surface->targetAAColorRT = [surface->device newTextureWithDescriptor: txDesc];
        surface->targetAAColorRT.label = @"targetAAColorRT";

        UnityRegisterExternalRenderSurfaceTextureForMemoryProfiler(surface->targetAAColorRT);
    }
}

extern "C" void DestroyRenderingSurfaceMTL(UnityDisplaySurfaceMTL* surface)
{
    UnityUnregisterMetalTextureForMemoryProfiler(surface->targetColorRT);
    surface->targetColorRT = nil;
    UnityUnregisterMetalTextureForMemoryProfiler(surface->targetAAColorRT);
    surface->targetAAColorRT = nil;

    if (surface->cvTextureCacheTexture)
        CFRelease(surface->cvTextureCacheTexture);
    if (surface->cvPixelBuffer)
        CFRelease(surface->cvPixelBuffer);
    if (surface->cvTextureCache)
        CFRelease(surface->cvTextureCache);
    surface->cvTextureCache = 0;
}

extern "C" void CreateSharedDepthbufferMTL(UnityDisplaySurfaceMTL* surface)
{
    DestroySharedDepthbufferMTL(surface);
}

extern "C" void DestroySharedDepthbufferMTL(UnityDisplaySurfaceMTL* surface)
{
    UnityUnregisterMetalTextureForMemoryProfiler(surface->depthRB);
    surface->depthRB = nil;
    surface->stencilRB = nil;
}

extern "C" void CreateUnityRenderBuffersMTL(UnityDisplaySurfaceMTL* surface)
{
    if (surface->targetColorRT)
    {
        if (surface->targetAAColorRT)   // render to interim AA RT and resolve to interim RT
            surface->unityColorBuffer   = UnityCreateAABackbufferFromTexture2D(surface->unityColorBuffer, surface->targetAAColorRT, surface->targetColorRT);
        else                            // render to interim RT
            surface->unityColorBuffer   = UnityCreateBackbufferFromTexture2D(surface->unityColorBuffer, surface->targetColorRT);
    }
    else
    {
        if (surface->targetAAColorRT)   // render to AA RT and resolve to backbuffer
            surface->unityColorBuffer   = UnityCreateAABackbufferResolveToSwapchain(surface->unityColorBuffer, surface->targetAAColorRT, &surface->swapchain);
        else                            // render directly to backbuffer
            surface->unityColorBuffer   = UnityCreateBackbufferFromSwapchain(surface->unityColorBuffer, &surface->swapchain);
    }

    surface->unityDepthBuffer  = UnityCreateDepthForBackbuffer(surface->unityDepthBuffer, surface->unityColorBuffer);
    surface->systemColorBuffer = UnityCreateBackbufferFromSwapchain(surface->systemColorBuffer, &surface->swapchain);
    surface->systemDepthBuffer = nullptr;
}

extern "C" void DestroySystemRenderingSurfaceMTL(UnityDisplaySurfaceMTL* surface)
{
    // before we needed to nil surface->systemColorRB (to release drawable we get from the view)
    // but after we switched to proxy rt this is no longer needed
    // even more it is harmful when running rendering on another thread (as is default now)
    // as on render thread we do StartFrameRenderingMTL/AcquireDrawableMTL/EndFrameRenderingMTL
    // and DestroySystemRenderingSurfaceMTL comes on main thread so we might end up with race condition for no reason
}

extern "C" void DestroyUnityRenderBuffersMTL(UnityDisplaySurfaceMTL* surface)
{
    UnityDestroyExternalSurface(surface->unityColorBuffer);
    UnityDestroyExternalSurface(surface->systemColorBuffer);
    surface->unityColorBuffer = surface->systemColorBuffer = 0;

    UnityDestroyExternalSurface(surface->unityDepthBuffer);
    UnityDestroyExternalSurface(surface->systemDepthBuffer);
    surface->unityDepthBuffer = surface->systemDepthBuffer = 0;
}

extern "C" void PreparePresentMTL(UnityDisplaySurfaceMTL* surface)
{
    if (surface->targetColorRT)
        UnityBlitToBackbuffer(surface->unityColorBuffer, surface->systemColorBuffer, surface->systemDepthBuffer);
    APP_CONTROLLER_RENDER_PLUGIN_METHOD(onFrameResolved);
}

extern "C" void PresentMTL(UnityDisplaySurfaceMTL* surface)
{
    // CODE ARCHEOLOGY: we used to present using [MTLCommandBuffer presentDrawable:afterMinimumDuration:]
    //   however that was found to sometimes cause 0.5-1 seconds hangs when acquiring drawable after surface rebuild, or presenting hanging completely (UUM-9480)
    //   after some further investigation we found that using the more complex present logic didn't actually yield much benefit
    //   current implementation is made to align with our macOS present logic
    UnityViewSwapchain* swapchain = &surface->swapchain;
    if (swapchain->drawable)
    {
        id<CAMetalDrawable> drawable = swapchain->drawable;
        [UnityCurrentMTLCommandBuffer() addScheduledHandler:^(id<MTLCommandBuffer> commandBuffer) {
            [drawable present];
        }];
    }
    surface->calledPresentDrawable = 1;
}

extern "C" MTLTextureRef AcquireSwapchainDrawable(UnityViewSwapchain* swapchain)
{
    // check if have acquired the backbuffer texture already
    if (swapchain->drawableTexture)
        return swapchain->drawableTexture;

    // this is coming from CAMetalDisplayLinkUpdate
    if (swapchain->nextDrawable)
        swapchain->drawable = swapchain->nextDrawable;

    // this is coming from CADisplayLink: query next drawable
    if (!swapchain->drawable)
        swapchain->drawable = [swapchain->layer nextDrawable];

    id<MTLTexture> drawableTex = [swapchain->drawable texture];
    if (drawableTex)
    {
        UnityUnregisterMetalTextureForMemoryProfiler(swapchain->drawableTexture);
        swapchain->drawableTexture = drawableTex;
        UnityRegisterExternalRenderSurfaceTextureForMemoryProfiler(drawableTex);
    }

#if UNITY_DISPLAY_SURFACE_MTL_BACKWARD_COMPATIBILITY
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
#pragma clang diagnostic ignored "-Winvalid-offsetof"

    const uintptr_t surfacePtr = (uintptr_t)swapchain - offsetof(UnityDisplaySurfaceMTL, swapchain);
    UnityDisplaySurfaceMTL* surface = (UnityDisplaySurfaceMTL*)surfacePtr;
    surface->layer          = swapchain->layer;
    surface->nextDrawable   = swapchain->nextDrawable;
    surface->drawable       = swapchain->drawable;
    surface->drawableTex    = swapchain->drawableTexture;

#pragma clang diagnostic pop
#endif

    return drawableTex;

}

extern "C" MTLTextureRef AcquireDrawableMTL(UnityDisplaySurfaceMTL* surface)
{
    if (!surface)
        return nil;

    return AcquireSwapchainDrawable(&surface->swapchain);
}

extern "C" int UnityCommandQueueMaxCommandBufferCountMTL()
{
    // customizable argument to pass towards [MTLDevice newCommandQueueWithMaxCommandBufferCount:],
    // the default value is 64 but with Parallel Render Encoder workloads, it might need to be increased

    return 256;
}

extern "C" void StartFrameRenderingMTL(UnityDisplaySurfaceMTL* surface)
{
    surface->systemColorBuffer = UnityCreateBackbufferFromSwapchain(surface->systemColorBuffer, &surface->swapchain);
    if (surface->targetColorRT == nil)
    {
        if (surface->targetAAColorRT)
            surface->unityColorBuffer = UnityCreateAABackbufferResolveToSwapchain(surface->unityColorBuffer, surface->targetAAColorRT, &surface->swapchain);
        else
            surface->unityColorBuffer = UnityCreateBackbufferFromSwapchain(surface->unityColorBuffer, &surface->swapchain);
    }
}

extern "C" void EndFrameRenderingMTL(UnityDisplaySurfaceMTL* surface)
{
    UnityViewSwapchain* swapchain = &surface->swapchain;

    @autoreleasepool
    {
        if (swapchain->drawableTexture)
            UnityUnregisterMetalTextureForMemoryProfiler(swapchain->drawableTexture);

        swapchain->drawable = nil;
        swapchain->drawableTexture = nil;

    #if UNITY_DISPLAY_SURFACE_MTL_BACKWARD_COMPATIBILITY

    #pragma clang diagnostic push
    #pragma clang diagnostic ignored "-Wdeprecated-declarations"

        surface->drawable       = nil;
        surface->drawableTex    = nil;

    #pragma clang diagnostic pop

    #endif
    }
}

extern "C" void PreparePresentNonMainScreenMTL(UnityDisplaySurfaceMTL* surface)
{
    UnityViewSwapchain* swapchain = &surface->swapchain;
    if (swapchain->drawable)
        [UnityCurrentMTLCommandBuffer() presentDrawable: swapchain->drawable];
}
