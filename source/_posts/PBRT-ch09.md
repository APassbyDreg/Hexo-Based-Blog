---
title: PBRT 第九章笔记 | Notes for PBRT Chapter 09 - Materials
date: 2021-11-23 13:58:38
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 9.1 BSDFs

`BSDF` 类型统一了 BRDF 和 BTDF ，并提供了一个供其它系统使用的统一接口。为了生成一个这样的实例，你需要提供以下信息：

- 一个代表了几何体表面信息的 `SurfaceInteraction si`
- 一个代表了交界面位置反射率的差值的浮点数 `eta`

一个 BSDF 类型中会储存至多 8 个 BxDF 的指针（当然，你可以通过修改源码中的一个常量改变这一值）以表示不同的表面模型，在使用的时候，`BSDF::f` 函数会调用每一个 BxDF 对象，并返回它们的 `BxDF::f` 给出的值的和。由于 BxDF 在着色的时候使用的是局部坐标系，BSDF 中也需要提供一个在两个坐标系之间转换方法，即使用下面这个正交矩阵完成转换。

$$\mathbf{M}=\left(\begin{array}{ccc}\mathbf{s}_{x} & \mathbf{s}_{y} & \mathbf{s}_{z} \\\mathbf{t}_{x} & \mathbf{t}_{y} & \mathbf{t}_{z} \\\mathbf{n}_{x} & \mathbf{n}_{y} & \mathbf{n}_{z}\end{array}\right)=\left(\begin{array}{c}\mathbf{s} \\\mathbf{t} \\\mathbf{n}\end{array}\right)$$

在很多情况下，渲染的时候会使用与几何法线 $n_g$ 不同的表面法线 $n_s$ 来提高渲染质量，这些法线的值可能来自 bump map 、三角形插值等情况，但这也带来了一定的物理不正确。光线会因为不同法线方向带来的几何不一致问题发生漏光等问题。

<center><img src="https://pbr-book.org/3ed-2018/Materials/Shading%20normal%20errors.svg" style="max-height: 32vh; margin: 10px 0"/></center>

## 9.1.1 BSDF 的内存管理

每当光线和场景中的几何体相交时，至少都会有一个 BSDF 对象被产生出来。而每个 BSDF 对象中又需要储存至少一个 BxDF 对象。如果每次都使用 new 和 delete 去动态地生成和管理这些对象的内存，整个系统会变得异常低效。PBRT 中会使用 `MemoryArena` 来管理这些小块的内存，其写法如下：

```cpp
BSDF *b = ARENA_ALLOC(arena, BSDF);
BxDF *lam = ARENA_ALLOC(arena, LambertianReflection)(Spectrum(0.5f));
```

`ARENA_ALLOC` 宏使用了 C++ 中的 placement new 特性以在自定义的内存位置上构建对象。

```cpp
#define ARENA_ALLOC(arena, Type) new (arena.Alloc(sizeof(Type))) Type
```

 为了避免在自定义的内存上删除由自定义方法申请的对象，BSDF 的析构函数被设置为 `private` ，这保证了任何对他的 delete 操作均会在编译期间报错。

# 9.2 Material Interface and Implementations

```cpp
// TransportMode Declarations
enum class TransportMode { Radiance, Importance };

// Material Declarations
class Material {
  public:
    // Material Interface
    virtual void ComputeScatteringFunctions(SurfaceInteraction *si,
                                            MemoryArena &arena,
                                            TransportMode mode,
                                            bool allowMultipleLobes) const = 0;
    virtual ~Material();
    static void Bump(const std::shared_ptr<Texture<Float>> &d,
                     SurfaceInteraction *si);
};
```

抽象类 `Material` 定义了各种材质的接口，这里面最重要的接口函数是 `ComputeScatteringFunctions()` 。它负责了初始化 `SurfaceInteraction::bsdf/bssrdf` 成员以供评估光线的传播。其中部分参数的意义在于：

- `TransportMode mode` ：体现了这一条光线的来源是光源还是摄像机（会在 16.1 节中详细讨论）
- `bool allowMultipleLobes` ：在不同的 Integrator 中提供的值不同，将它设置为 `true` 允许了随机采样的方法得到更加优秀的结果，但会在不使用随机采样的积分器中引入噪声。

计算散射函数的过程开始于 `SurfaceInteraction::ComputeScatteringFunctions` ，它会首先计算光线的微分，再调用 `GeometricPrimitive::ComputeScatteringFunctions()` 寻找几何体对应的材质，最后对相应的材质调用 `Material::ComputeScatteringFunctions()` 。

## 9.2.1 Matte Material

这是一种最简单的材质，描述了一种存粹的漫反射表面，它需要使用三个材质：

- 表示颜色的 `Kd`
- 表示粗糙度的 `sigma` ：当粗糙度为零时材质会使用 Lambertian 表面模型，其他情况下则会使用 OrenNayar 表面模型
- 提供表面细节的 `bumpMap` ，这是一个可选项

它的 `ComputeScatteringFunctions` 方法将上述内容结合到一起：

```cpp
void MatteMaterial::ComputeScatteringFunctions(SurfaceInteraction *si,
                                               MemoryArena &arena,
                                               TransportMode mode,
                                               bool allowMultipleLobes) const {
    // Perform bump mapping with _bumpMap_, if present
    if (bumpMap) Bump(bumpMap, si);

    // Evaluate textures for _MatteMaterial_ material and allocate BRDF
    si->bsdf = ARENA_ALLOC(arena, BSDF)(*si);
    Spectrum r = Kd->Evaluate(*si).Clamp();
    Float sig = Clamp(sigma->Evaluate(*si), 0, 90);
    if (!r.IsBlack()) {
        if (sig == 0)
            si->bsdf->Add(ARENA_ALLOC(arena, LambertianReflection)(r));
        else
            si->bsdf->Add(ARENA_ALLOC(arena, OrenNayar)(r, sig));
    }
}
```

## 9.2.2 Plastic Material

塑料材质可以被描述为 diffuse 和 glossy 散射函数的一种组合。它利用 `Kd, Ks` 描述漫反射的颜色和高光的颜色，并使用 `roughness` 描述表面的粗糙度，它将影响高光的范围。

在本材质中，漫反射项由 Lambertian 材质提供，高光项则由粗糙度定义的微表面材质提供。

## 9.2.3 Mix Material

这种模型可以使用给定的权重混合任意两种材质，它会首先复制一份 SurfaceInteraction ，接着在两个 SI 上使用两种材质生成原始的 BSDF ，最后使用权重在原始的 SI 上重新生成 ScaledBSDF。

```cpp
void MixMaterial::ComputeScatteringFunctions(SurfaceInteraction *si,
                                             MemoryArena &arena,
                                             TransportMode mode,
                                             bool allowMultipleLobes) const {
    // Compute weights and original _BxDF_s for mix material
    Spectrum s1 = scale->Evaluate(*si).Clamp();
    Spectrum s2 = (Spectrum(1.f) - s1).Clamp();
    SurfaceInteraction si2 = *si;
    m1->ComputeScatteringFunctions(si, arena, mode, allowMultipleLobes);
    m2->ComputeScatteringFunctions(&si2, arena, mode, allowMultipleLobes);

    // Initialize _si->bsdf_ with weighted mixture of _BxDF_s
    int n1 = si->bsdf->NumComponents(), n2 = si2.bsdf->NumComponents();
    for (int i = 0; i < n1; ++i)
        si->bsdf->bxdfs[i] =
            ARENA_ALLOC(arena, ScaledBxDF)(si->bsdf->bxdfs[i], s1);
    for (int i = 0; i < n2; ++i)
        si->bsdf->Add(ARENA_ALLOC(arena, ScaledBxDF)(si2.bsdf->bxdfs[i], s2));
}
```

## 9.2.4 Fourier Material

这种材质根据提供的数据源文件名生成 FourierBSDF 并应用在 SI 上。

## 9.2.5 其它材质

除此之外，PBRT中还实现了大量如金属材质、次表面材质、Disney BSDF 材质等内容，此处就不一一详述。

# 9.3 Bump Mapping

<center><img src="https://pbr-book.org/3ed-2018/Materials/Displacement%20function.svg" style="max-height: 20vh; margin: 10px 0"/></center>

Bump Mapping 实际上在每一个位置上从对应材质上采样了一个浮点数 $d(p)$ ，并将该位置沿着法线方向向外扩张 $d(p)$ 单位，以此来计算用于着色的法线。这一技术可以以很低的成本增加场景中的细节，因此被广泛地应用于各种渲染流程中。

`Material::Bump()` 方法提供了一种通用的 bump mapping 计算方法，它输入一个 Bump 材质和一个 SurfaceInteraction 指针，利用 SI 中的信息采样材质，并将对应的表面信息写入用于着色的 `ShadingGeometry` 之中。它通过 SI 中的微分信息在着色点和 `du,dv` 位置采样三次 bump map，并将得到的表面信息写入 SI 。

```cpp
void Material::Bump(const std::shared_ptr<Texture<Float>> &d,
                    SurfaceInteraction *si) {
    // Compute offset positions and evaluate displacement texture
    SurfaceInteraction siEval = *si;

    // Shift _siEval_ _du_ in the $u$ direction
    Float du = .5f * (std::abs(si->dudx) + std::abs(si->dudy));
    // The most common reason for du to be zero is for ray that start from
    // light sources, where no differentials are available. In this case,
    // we try to choose a small enough du so that we still get a decently
    // accurate bump value.
    if (du == 0) du = .0005f;
    siEval.p = si->p + du * si->shading.dpdu;
    siEval.uv = si->uv + Vector2f(du, 0.f);
    siEval.n = Normalize((Normal3f)Cross(si->shading.dpdu, si->shading.dpdv) +
                         du * si->dndu);
    Float uDisplace = d->Evaluate(siEval);

    // Shift _siEval_ _dv_ in the $v$ direction
    Float dv = .5f * (std::abs(si->dvdx) + std::abs(si->dvdy));
    if (dv == 0) dv = .0005f;
    siEval.p = si->p + dv * si->shading.dpdv;
    siEval.uv = si->uv + Vector2f(0.f, dv);
    siEval.n = Normalize((Normal3f)Cross(si->shading.dpdu, si->shading.dpdv) +
                         dv * si->dndv);
    Float vDisplace = d->Evaluate(siEval);
    Float displace = d->Evaluate(*si);

    // Compute bump-mapped differential geometry
    Vector3f dpdu = si->shading.dpdu +
                    (uDisplace - displace) / du * Vector3f(si->shading.n) +
                    displace * Vector3f(si->shading.dndu);
    Vector3f dpdv = si->shading.dpdv +
                    (vDisplace - displace) / dv * Vector3f(si->shading.n) +
                    displace * Vector3f(si->shading.dndv);
    si->SetShadingGeometry(dpdu, dpdv, si->shading.dndu, si->shading.dndv,
                           false);
}
```