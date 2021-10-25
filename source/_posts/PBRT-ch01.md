---
title: PBRT 第一章笔记 | Notes for PBRT Chapter 01 - Introduction
date: 2021-10-22 01:42:44
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 1.1 Literate Programming

这部分与网页版的 PBRT 展示方式有关，此处跳过

# 1.2 Photorealistic Rendering and the Ray-Tracing Algorithm

真实感渲染的最终目的是得到足够接近真实世界的照片的渲染结果。大部分真实感渲染的系统均基于光线追踪的算法。光线追踪算法模拟了在场景中与物体交互、弹射的光线的路径，并以此计算摄像头最终接收到的颜色，

## 1.2.1 摄像机

摄像机的主要功能是根据像素在图像上的位置，生成对该位置有贡献的着色光线。

## 1.2.2 光线 - 物体相交

每当摄像机生成了光线后，渲染器的首要工作是确定该光线是否有击中任何物体，并找出第一个相交的物体和相交的位置。在找到相交的表面后，渲染器将提取交点处的法线、uv、微分等信息以供着色。

## 1.2.3 光照分布

在获得了着色位置的表面信息后，需要得到场景中的光照分布以计算该处的辐照度。由于在 PBRT 的假设中光照是线性的，对于多个光源可以简单地叠加其影响。

## 1.2.4 可见性

可见性补充了光照分布中阴影的缺失。光线追踪渲染器可以简单的使用光线相交技术确定在着色表面是否可被光源影响。

## 1.2.5 表面散射

在得到了光线的入射、出射方向和表面信息后，PBRT 会计算在此情况下光线在表面作用后出射的结果。PBRT 使用 BRDF / BTDF 表示能量的传递过程。

## 1.2.6 间接光照传输

Turner Whitted 最早的有关光线追踪的论文中就强调了其递归的性质，这使得它可以对间接光照进行建模，这给出了著名的光线传输方程：

$$L_{\mathrm{o}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)=L_{\mathrm{e}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)+\int_{\mathrm{S}^{2}} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L_{\mathrm{i}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}}$$

## 1.2.7 光线传输

前文所描述的内容均基于光线在真空中传播的假设，然而事实上光线经常需要经过参与介质。参与介质会产生消光或增强的效果。渲染器需要对这些现象进行建模和模拟，

# 1.3 PBRT: System Overview

PBRT 使用标准的面向对象编程技术编写。总共有 10 种关键的基类：

<center><img src="BaseClasses.png" style="max-height: 35vh; margin: 10px;"/></center>

## 1.3.1 运行阶段

PBRT 的运行可以被分为两段：首先它会解析场景描述文件，这个描述文件会产生一个 `Scene` 实例和一个 `Integrator` 实例，前者包含了场景的所有信息，后者包含了渲染这个场景的算法。第二个也是主要耗时的阶段是渲染阶段。PBRT 会调用 `Integrator::Render()` 方法渲染整个场景。

## 1.3.2 场景表示

```cpp
class Scene {
  public:
    // Scene Public Methods
    ...
    // Scene Public Data
    std::vector<std::shared_ptr<Light>> lights;
    // Store infinite light sources separately for cases where we only want
    // to loop over them.
    std::vector<std::shared_ptr<Light>> infiniteLights;

  private:
    // Scene Private Data
    std::shared_ptr<Primitive> aggregate;
    Bounds3f worldBound;
};
```

每个光源被表示为一个 `Light` 对象，其中储存了光源的形状和其能量的分布。`Scene` 类使用智能指针储存这些对象。（特别的，无穷远处的光源被单独储存以备他用）

场景中的每个几何体被表示为一个 `Primitive` 对象，其中保存了其性质 `Shape` 和材质 `Material` ，`Scene` 对象中保存的是一种特殊的几何体 `aggregate` ，它保存了所有的几何体，并实现了与普通 `Primitive` 一致的接口。

## 1.3.3 `Integrator` 接口和 `SamplerIntegrator`

图片的渲染由继承了 `Integrator` 接口的实例完成，其中定义了渲染用的 `void Render(const Scene &scene)` 函数：

```cpp
class Integrator {
  public:
    // Integrator Interface
    virtual ~Integrator();
    virtual void Render(const Scene &scene) = 0;
};
```

`Integrator` 的一个子类是 `SamplerIntegrator` ，它的渲染过程来自于一连串从 `Sampler` 获取的采样，每个采样均计算了图像上的某一点收到的光量。

```cpp
class SamplerIntegrator : public Integrator {
  public:
    SamplerIntegrator( ... ) : ... {}
    virtual void Preprocess( ... ) {}
    void Render(const Scene &scene);
    virtual Spectrum Li( ... ) const = 0;
    Spectrum SpecularReflect( ... ) const;
    Spectrum SpecularTransmit( ... ) const;

  protected:
    std::shared_ptr<const Camera> camera;

  private:
    std::shared_ptr<Sampler> sampler;
    const Bounds2i pixelBounds;
};
```

其中的 `sampler` 成员负责生成用于积分的采样，而 `camera` 成员则保存了相机相关的信息，其中包含了保存渲染结果的 `Film` 对象

## 1.3.4 主渲染循环

<center><img src="https://pbr-book.org/3ed-2018/Introduction/Class%20Relationships.svg" style="max-height: 25vh; margin: 10px;"/></center>

```cpp
void SamplerIntegrator::Render(const Scene &scene) {
		Preprocess(scene, *sampler);
		// <<Render image tiles in parallel>> 
		    // <<Compute number of tiles, nTiles, to use for parallel rendering>> 
		    ParallelFor2D([&](Point2i tile) {
		       // <<Render section of image corresponding to tile>> 
		         // <<Allocate MemoryArena for tile>> 
		         // <<Get sampler instance for tile>> 
		         // <<Compute sample bounds for tile>> 
		         // <<Get FilmTile for tile>> 
		         // <<Loop over pixels in tile to render them>> 
		         // <<Merge image tile into Film>> 
		      }, nTiles);
		// <<Save final image after rendering>> 
		camera->film->WriteImage();
}
```

## 1.3.5 Whitted Ray Tracing 的积分器

这是一种基础的光线追踪算法使用的积分器，其 `Li` 函数的运行流程如下


<center><img src="https://pbr-book.org/3ed-2018/Introduction/Surface%20Integration%20Class%20Relationships.svg" style="max-height: 25vh; margin: 10px;"/></center>

# 1.4 Parallelization of PBRT

本章节描述了 PBRT 中并行化的必要性、并行化的风险以及并行化中需要注意的问题。其中线程不安全的内容包括：

- 大部分的基础类型：如 `Point3f, Vector3f, Transform` 等使用频次过高的类型，对这些类型实现线程安全会导致极大的性能损失
- 工具类：如 `MemoryArena, RNG, Sampler` 等类型一般在每个线程中单独生成并使用，不做跨线程通信
- 预处理函数：由于 PBRT 的 Parsing 阶段一般是串行执行的，这类在初始化时调用的预处理函数一般也不是线程安全的

# 1.5 How to Proceed through This Book

教你怎么用这本书的一节，此处略

# 1.6 Using and Understanding the Code

教你怎么用这本书的代码的一节，此处略

# 1.7 A Brief History of Physically Based Rendering

本章的翻译链接：

[GitHub - kanition/pbrtbook: pbrt 中文整合翻译 基于物理的渲染：从理论到实现 Physically Based Rendering: From Theory To Implementation](https://github.com/kanition/pbrtbook)