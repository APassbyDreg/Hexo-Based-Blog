---
title: Dynamic Diffuse Global Illumination - GDC 2018
date: 2022-04-16 12:52:53
categories:
- programming
- reading notes
tags:
- CG
- render
- GDC talks
toc: true
---

本文源自 GDC 2018 中 NVIDIA 的分享：[https://morgan3d.github.io/articles/2019-04-01-ddgi/index.html](https://morgan3d.github.io/articles/2019-04-01-ddgi/index.html)

# Glossy GI

## 环境贴图

Glossy GI 贡献了光滑表面上可辨识的反射，从 19 世纪 70 年代起，其就被使用环境贴图来近似。随着时间的演进，它的核心算法也被视差扰动和可变表面粗糙度的方法而改进。这一类算法一般被称为环境映射、环境探针、辐照度探针、光照探针等。

大部分在实时渲染中使用的光照探针都是预计算的，这使得动态的物体和光源都无法影响反射光照。虽然在很多游戏中使用了各自 tricks 来提供高效的全局光照，但它们都无法实现真正通用的实时 Glossy GI 。

## 光线追踪

最早出现于 CryEngine 3 的屏幕空间反射技术在近年来被广泛用于近距离物体的反射计算。[这篇文章](https://jcgt.org/published/0003/04/04/)详细分析了这一算法。进一步地，为了处理不在屏幕空间内的表面的反射，需要进行真实几何的光线追踪。当前的许多游戏都使用了上述的一种或多种算法。

## 算法

在完美平滑表面的 Glossy GI 的实现相当直观，对于镜面而言光线击中的着色值就是对应的 GI 值。而对于模糊的粗糙反射则存在两种方法：要么使用随机光线采样，并模糊采样的结果；要么直接采样完美反射方向，并模糊其反射内容。

<center style="margin-bottom: 10px"><img src="Untitled.png" style="max-height: 30vh; margin: 10px 0"/></center>

上图展示了这个流程：

1. 在只有一半的垂直分辨率的 GBuffer 上进行光线追踪，每个像素追踪一个完美反射方向，然后使用常用的延迟渲染方法对光线追踪的结果位置进行着色，次级 Glossy GI 也可以使用类似的方法完成
2. 使用 MIP map 模糊镜面反射结果的 buffer ，模糊时会使用特殊的双向滤波方法以保证其不会采样到无高光反射的黑色区域
3. 使用主表面的粗糙度和出入射光线的距离计算 MIP map 的层级，进而采样上述贴图

这种方法相较于进行随机光线采样可以较为简单地减少高光走样，但并不那么物理准确。事实上，上述问题是 Glossy GI 中会遇到的主要问题。TAA 对此而言是一把双刃剑，它虽然可以减少走样，但需要十分复杂的对于反射物体和反射表面的 motion vector 处理。一种处理方法是禁用视差和法线映射，并在图像上使用 FXAA 。这相当于在反射 buffer 上使用 TAA 。

在较近的物体上使用 SSR ，并对非常远的物体使用环境探针可以通过缩短光线长度以减少约一半的消耗。使用棋盘渲染方法而非减半垂直采样数可以提升图像质量，但也有着出现横向走样的风险。减半分辨率渲染和 DLSS 也可以被整合入整个流程中以减少走样。

# 旧 Diffuse GI

动态的高光 GI 已经在数年前被基本解决了，剩余的问题包括更高效地解决屏幕外的物体，以及减少噪声和走样。但从来没有一种稳定、动态且高效的方法能够解决 Diffuse GI 的问题。下面列举了一些可以在特定情况下提供优秀结果的方法：

- Light maps [Quake97](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-quake97) [Mitchell06](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-mitchell06)
- Irradiance probes/voxels [Greger98](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-greger98) [Tatarchuk05](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-tatarchuk05) [Ramamoorthi11](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-ramamoorthi11) [Gilabert12](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-gilabert12)
- Virtual point lights [Keller97](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-keller97) [Kaplanyan10](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-kaplanyan10) [Ding14](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-ding14) [Xu16](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-xu16) [Sassone19](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-sassone19) [White19](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-white19)
- Reflective shadow maps [Dachsbacher05](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-dachsbacher05)[Kaplanyan10](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-kaplanyan10)[Ding14](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-ding14)[Malmros17](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-malmros17)[Xu16](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-xu16)[White19](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-white19)
- Light propagation volumes [Kaplanyan09](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-kaplanyan09)[Kaplanyan10](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-kaplanyan10)
- Sparse voxel cone tracing [Crassin11](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-crassin11)[McLaren16](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-mclaren16)
- Denoised ray tracing [Mara17](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-mara17)[Schied17](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-schied17)[Metro19](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-metro19)[Archard19](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-archard19)

预计算的 Light maps 是 DX11 级别游戏的主流解决方案，并被大多数主流游戏引擎所支持。[Enlighten](https://www.siliconstudio.co.jp/) 中间件可以使用简化的模型在运行时动态地更新光照贴图和探针，它已被用于非常多的游戏，并为 DDGI 提供了灵感来源。

除了上述列举的方法以外，RTR4 中还列举了一个完整的 survey 以记录所有实时全局光照方法。

为了更好地理解 DDGI 首先需要理解传统的 irradiance 探针方法，因为这是 DDGI 主要加速并改进的全局光照方法。这种方法的限制也可以很好地代表上述方法的限制。

## 经典的 Irradiance 探针

源于 1998 年 [Greger98](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-greger98) 的在稀疏空间储存 irradiance 场的想法发展至今，已经被许多引擎所采用。引擎会使用细小的探针测量并储存空间中的 Diffuse GI 信息，这种使用 $\cos$ 加权积分入射光线的 radiance 得到的在数学上即为对应空间位置的 irradiance 。每个探针都表示了一整个单位球面的方向，它们可以使用任意的球面映射方式储存。这些值通常会使用离线方法被预计算出来，部分引擎会在运行时通过在非常低 LOD 的场景中采样光线来更新这个值。

irradiance 光照探针的质量可以非常高，对于在探针附近的表面通常可以得到非常完美的表现，但它也拥有包括离线烘焙耗时大且打断工作流、难以计算实时探针和在探针密度不足的区域漏光的问题。

## 漏光

当光照由于某些场景几何而发生大幅变化时，irradiance 探针会因为采样不足而产生漏光的问题。

<center style="margin-bottom: 10px"><img src="Untitled%201.png" style="max-height: 40vh; margin: 10px 0"/></center>

上图（来自 SIGGRAPH talk [Hooker16](https://morgan3d.github.io/articles/2019-04-01-ddgi/overview.html#citation-hooker16) ）屋外的亮处向屋内贡献了不正确的阳光光照，而门的阴影则贡献了不正确的暗部。当前最有效的解决方法是让艺术家人工地放置遮挡几何体以区分内外侧，上图的来源详细地讨论了这个问题，但这一方法也无法解决动态光照的问题。

这些问题在所有的实时 Diffuse GI 中都有出现，是所有算法中共同潜藏的缺陷。

# DDGI

## 升级探针

传统的 irradiance 探针由于其高效率、低噪声、适用广、无参数等特点广泛应用于各自引擎中。正如上文所描述的，这类算法的主要缺点就在于其漏光和难以动态更新的问题。DDGI 升级了传统的光照探针以解决此问题。

- 对于漏光问题，将可视性信息储存在探针中
- 对于动态问题，使用异步的 GPU 光线追踪以计算低分辨率的探针，并使用一个内存一致的收集混合策略以分摊消耗，避免闪烁

## 探针放置

下图展示了 DDGI 的探针放置情况。

<center style="margin-bottom: 10px"><img src="Untitled%202.png" style="max-height: 40vh; margin: 10px 0"/></center>

在制作中，可以首先从一个均匀网格开始，这一般就可以获得一个可用的结果。再针对高频的细节位置增加探针数量，或者减少部分位置的探针以提高运行效率。

另一方面，由于这种探针的整体效果随着放置位置的影响不大，它可以直接支持动态场景，有效地减少了流程中对光照和几何体的调整对最终效果的影响。除此之外，任意的光照探针都可以在场景网格中任意移动，从而提供精细的调整。

## 层级结构

光照探针的放置和更新策略可以使用层次结构有效地进行管理，对于远处的探针，在减小更新的频率的同时还可以通过禁用可视性检测以进一步加快运行效率。

## 数据结构

下图展示了在上述场景的探针中单一层次的材质内存储存方式，上一排展示了使用 `R11G11B10F` 方式储存的 irradiance 信息，下图展示了二维的 `RG16F` 的可见性信息。

<center style="margin-bottom: 10px"><img src="Untitled%203.png" style="max-height: 35vh; margin: 10px 0"/></center>

在这一场景中使用了 `32*32*4` 个探针，可以观察到每个材质包含了四个横向排列的矩形，这正对应了四个不同的纵向探针层级。黑色的部分表示了探针进入了墙壁中。

在每个大型矩形中有大量的小矩形以表示探针携带的值。球状的方向信息会被投影到八面体上，接着展开为一个 `6*6` 的 irradiance 矩形和一个 `16*16` 的可见性（储存了深度信息）矩形。

在这种储存方式下，每一层中的 irradiance 信息需要占用 590 KB的空间，可视性信息则占用了约 4MB 的空间。

## 算法

DDGI 和传统的 Glossy GI 算法类似，它主要包含了三个步骤：光线追踪、更新探针以及对可视位置进行着色。

<center style="margin-bottom: 10px"><img src="Untitled%204.png" style="max-height: 35vh; margin: 10px 0"/></center>

### 光线追踪
    
这一步中会为每个探针生成约 100 - 300 根光线，生成对应的 GBuffer ，并使用标准的 deferred 方式对这些光线进行着色。

由于 Glossy GI 只使用了一半的屏幕高度，光线追踪的信息可以被放入其屏幕缓冲区的下半部分以在单 Pass 内完成两部分的着色。由于着色时使用的 Diffuse GI 信息是来自上一帧的内容，DDGI 通过增加反射数量以让其结果可以在场景变化后的几帧之内快速收敛到新的值上。
    
### 更新探针
    
使用新的着色数据更新探针。这一步会遍历所有探针的纹素，对每一个纹素收集所有的光线相交位置并利用时序和可视性信息混合新旧数据，每一次混合中旧有的值可以占到 90% ~ 99.5% 的比例。
    
### 采样探针
    
这一步的效率极高，并且可以轻松结合入 forward 和 deferred 管线中，甚至可以应用于体积渲染和 Glossy 光线渲染中。这一步中的主要性能开销是 16 次纹理查询，但由于数据结构封装的高效性它们可以拥有很高的缓存命中率。
    
DDGI 主要的性能开销在前两部分，但它们可以独立于帧率和屏幕分辨率更新，这使得其开销可以简单地分摊到应用整体上。在 RTX2080 上，这种独立性会带来约 100ms 的间接光照更新延迟，这在大部分情况下都是难以察觉的；而对于低端的显卡，间接光照会存在更多的延迟，但对于静态场景而言其质量仍然十分突出。

在结合了 Glossy GI 后，一个使用 DDGI 技术的高端 GPU 可以得到和离线渲染相近的视觉效果，并且对于未来的 GPU 在层次结构、探针密度上均有有效的可拓展性。而对于低端的平台则可以通过禁用动态更新以获得和烘焙光照无异的优秀效果。