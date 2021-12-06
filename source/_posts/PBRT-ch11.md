---
title: PBRT 第十一章笔记 | Notes for PBRT Chapter 11 - Volume Scattering
date: 2021-12-04 11:00:46
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 11.1 Volume Scattering Processes

体积散射中有三个主要的过程：

- 吸收 Absorption
- 发光 Emission
- 散射 Scattering

而这其中的各个过程均有可能是同质（homogeneous）或异质（inhomogeneous）的。同质的体积中各个位置处的各个参数相等，而异质的体积中各个参数则可能不同。

## 11.1.1 Absorption

<center><img src="https://pbr-book.org/3ed-2018/Volume_Scattering/Volume%20absorption.svg" style="max-height: 9vh; margin: 10px 0"/></center>

散射中的吸收代表了入射的 radiance 在经过单位体积后在相同方向上出射的能量减少的比例，既有：（注意 PBRT 中入射光的方向和光线的传播方向是相反的）

$$\mathrm{d}L_o(p, \omega) = -\sigma_a(p, \omega)L_i(p, -\omega)\mathrm{d}t\\
L_o(p+d\omega, \omega) = L_i(p, -\omega)e^{-\int_0^d\sigma_a(p+t\omega, \omega)\mathrm{d}t}$$

## 11.1.2 Emission

<center><img src="https://pbr-book.org/3ed-2018/Volume_Scattering/Volume%20emission.svg" style="max-height: 9vh; margin: 10px 0"/></center>

这一过程代表了单位体积中发光的粒子对输出 radiance 的贡献，即：

$$\mathrm{d}L_o(p, \omega) = L_e(p, \omega)\mathrm{d}t\\
L_o(p+d\omega, \omega) = \int_0^d L_e(p+t\omega,-\omega)\mathrm{d}t$$

## 11.1.3 Out-Scattering and Attenuation

<center><img src="https://pbr-book.org/3ed-2018/Volume_Scattering/Volume%20out%20scattering.svg" style="max-height: 12vh; margin: 10px 0"/></center>

向外的散射表示单位体积中光线击中粒子后向其他方向散射的能量比例。

$$\mathrm{d}L_o(p, \omega) = -\sigma_s(p, \omega)L_i(p, -\omega)\mathrm{d}t\\
L_o(p+d\omega, \omega) = L_i(p, -\omega)e^{-\int_0^d\sigma_s(p+t\omega, \omega)\mathrm{d}t}$$

在对出射 radiance 的贡献上，他实际上和吸收系数是相同的，也因此会将他和吸收系数合成一个系数表示光线在单位体积内传输的衰减率：

$$\sigma_t = \sigma_a + \sigma_s\\
T_r(p \to p') = e^{-\int_0^d\sigma_t(p+t\omega,\omega)\mathrm{d}t}$$

利用 $\sigma_t$ 我们还可以定义很多其它有用的物理量，包括了：

- 体积的散射率 albedo $\rho = {\sigma_s / \sigma_t}$ ，它表示了散射占所有衰减的比例
- 平均自由路径长度（mean free path）$1 / \sigma_t$ ，它表示了光线在体积中与粒子的两次交互之间（或从入射到与粒子交互之间）的平均距离
- 光学厚度 $\tau = \int_0^d\sigma_t(p+t\omega,\omega)\mathrm{d}t$

## 11.1.4 In-scattering

<center><img src="https://pbr-book.org/3ed-2018/Volume_Scattering/Volume%20in%20scattering.svg" style="max-height: 12vh; margin: 10px 0"/></center>

与外散射相对的内散射表示了单位体积中从其它方向经由散射进入光线出射方向的能量比例。为了描述散射的过程，我们一般会使用一个 phase function 来表示空间某点处从一个给定方向上入射的能量在各个方向上的散射的能量分布，它遵从能量守恒定律：

$$\int_{s^2}p(p, \omega,\omega')\mathrm{d}\omega' = 1$$

内散和发光结合在一起组成了体积散射中的 source 项：

$$L_s(p, \omega) = L_e(p,\omega) + \sigma_s(p,\omega)\int_{s^2}p(p,\omega_i, \omega)L_i(p, \omega_i)\mathrm{d}\omega_i$$

这一项和表面散射函数非常类似，唯一的区别在于它并不需要一个 $\cos$ 项，因为这里操作的量纲均为 radiance 。

# 11.2 Phase Functions

体积散射中的 Phase Functions 就相当于表面散射中的 BSDF 。`PhaseFunction` 定义了这样的一个抽象接口。

```cpp
class PhaseFunction {
public:
    virtual Float p(const Vector3f &wo, const Vector3f &wi) const = 0;
    virtual Float Sample_p(const Vector3f &wo, Vector3f *wi,
                            const Point2f &u) const = 0;
};
```

在大部分的介质中，phase function 均可以简化为一个一维函数 $p(\cos\theta)$ ，其中 $\theta$ 表示的入射和出射方向的夹角。这样的 phase function 计算简单且具有可逆性，交换入射和出射方向不会改变函数值。具有这种特性的介质称为各项同性的介质。

在 PBRT 中最常用的一种 phase function 是 HG phase function。它因为是由 Henyey 和 Greenstein 设计而得名，其方程为：

$$p_{HG}(\cos\theta) = {1\over4\pi}{1 - g^2 \over (1 + g^2 + 2g\cos\theta)^{3/2}}$$

其中的 g 项是一个在 $(-1, 1)$ 范围内的可调整的参数。负数的 g 值表示了光线总会向来源方向散射（如下图蓝线），而正的 g 值则表示了光线会更多地向光线前进方向散射（如下图黄线）。下图展示了两种 g 值在沿横轴正向入射的光线的散射能量分布剖面图：

<center><img src="https://pbr-book.org/3ed-2018/Volume_Scattering/hg-plot.svg" style="max-height: 10vh; margin: 10px 0"/></center>

g 值的取值实际上源自于 phase function 在入射方向上的投影均值：

$$g=\int_{\mathrm{S}^{2}} p\left(-\omega, \omega^{\prime}\right)\left(\omega \cdot \omega^{\prime}\right) \mathrm{d} \omega^{\prime}=2 \pi \int_{0}^{\pi} p(-\cos \theta) \cos \theta \sin \theta \mathrm{d} \theta .$$

当然，这种 phase function 并不能描述各种类型的介质。因此在 PBRT 中常常使用一系列参数不同的 phase fuction 加权组合为一个新的 phase function 以表示复杂的介质：

$$p(\omega,\omega') = \sum_{i=1}^nw_ip_i(\omega \to \omega')$$

# 11.3 Media

```cpp
class Medium {
public:
    // Medium Interface
    virtual ~Medium() {}
    virtual Spectrum Tr(const Ray &ray, Sampler &sampler) const = 0;
    virtual Spectrum Sample(const Ray &ray, Sampler &sampler,
                            MemoryArena &arena,
                            MediumInteraction *mi) const = 0;
};
```

`Medium` 基类提供了体积散射在空间区域中的表示方法。其中最重要的函数之一是计算透射率的 `Tr` 。它接收一根光线作为输入，计算该光线从原点到 tMax 位置的透射率（我们一般会假设这条光线没有受到任何遮挡，并且全段均在介质中）。

在 PBRT 中，两种不同散射介质之间的分界均会由一个 `GeometricPrimitive` 的表面表示，因此每个几何体中均会储存一个 `MediumInterface` 储存表面两侧的介质。

```cpp
struct MediumInterface {
    MediumInterface(const Medium *medium)
        : inside(medium), outside(medium) { }
    MediumInterface(const Medium *inside, const Medium *outside)
        : inside(inside), outside(outside) { }
    bool IsMediumTransition() const { return inside != outside; }
    const Medium *inside, *outside;
};
```

对大部分场景而言，通常并不需要对场景中的每个物体均设置其两侧的介质。为了降低复杂度，只有在摄像机、光源，以及透明且两侧介质不同的物体才会被设置介质值。`GeometricPrimitive::Intersect()` 方法会在光线和几何体相交时根据需求设置 `SurfaceInteraction::mediumInterface` 。

另外，为了不在透明的介质边界处（比如使用 box 表示的云）产生额外的 BSDF 计算负担，PBRT 允许一个表面拥有值为 `nullptr` 的 `Material` 和 `bsdf` 指针

根据以上内容设计的新相交方法 `Scene::IntersectTr` 会忽略空白的边界，并在第一个具有材质处汇报相交、返回 si 和对应的透射率 $T_r$ 。

## 11.3.1 Medium Interactions

`MediumInteraction` 类包装了一个 `Interaction` 类和一个指向 `PhaseFunction` 的指针。和 `SurfaceInteraction` 对应的，这个类表示了介质内一点处发生的散射作用。

```cpp
class MediumInteraction : public Interaction {
public:
    // MediumInteraction Public Methods
    MediumInteraction() : phase(nullptr) {}
    MediumInteraction(const Point3f &p, const Vector3f &wo, Float time,
                      const Medium *medium, const PhaseFunction *phase)
        : Interaction(p, wo, time, medium), phase(phase) {}
    bool IsValid() const { return phase != nullptr; }

    // MediumInteraction Public Data
    const PhaseFunction *phase;
};
```

## 11.3.2 Homogeneous Medium

`HomogeneousMedium` 是最简单的介质，它表示了空间中一处拥有恒定的散射和吸收系数的介质，并使用 HG phase function 处理散射现象。

```cpp
class HomogeneousMedium : public Medium {
public:
    HomogeneousMedium(const Spectrum &sigma_a, const Spectrum &sigma_s, Float g)
        : sigma_a(sigma_a), sigma_s(sigma_s), sigma_t(sigma_s + sigma_a), g(g) {}
    Spectrum Tr(const Ray &ray, Sampler &sampler) const;
    Spectrum Sample(const Ray &ray, Sampler &sampler, MemoryArena &arena,
                    MediumInteraction *mi) const;
private:
    const Spectrum sigma_a, sigma_s, sigma_t;
    const Float g;
};
```

由于其内部的投射系数 $\sigma_t$ 是一个常值，对于其透射率的求值就变得异常简单了。我们只需要简单地利用光学密度 $\tau=\sigma_t \times d$ 即可得到投射后剩余的能量。

## 11.3.3 3D Grids

除此之外，我们还可以使用一个 3D 的浮点数表来储存来自 3D 扫描、物理模拟等方式得到的数据。表格的每一项代表了一个体素位置上的介质的浓度（光学密度）。PBRT 中的 `GridDensityMedium` 实现了这一表示方法。它的构造函数输入作为基准值的 $\sigma_s, \sigma_a$ 、从世界坐标到单位网格 NDC 坐标的变换矩阵以及一个浮点数密度网格。每个体素位置的 $\sigma_s, \sigma_a$ 即可以通过使用基准值乘以该位置处由三线性插值获得的密度值得到。

# 11.4 The BSSRDF

在之前引入的 BSSRDF 可以表示从物体的任意入射位置和方向入射的射线，从另一个任意位置以任意方向出射的 radiance 变化比例。

$$L_o(p, \omega_o) = \int_A{\rm dA}\int_{H^2(n)}f(p_i,p_o,\omega_o,\omega_i)L_i(p_i,\omega_i)|\cos\theta_i|{\rm d}\omega_i$$

这一方程的求解与特性将使用 `BSSRDF` 类型表示：

```cpp
class BSSRDF {
public:
    BSSRDF(const SurfaceInteraction &po, Float eta) : po(po), eta(eta) {}
    virtual ~BSSRDF() {}
    virtual Spectrum S(const SurfaceInteraction &pi, const Vector3f &wi) = 0;
    virtual Spectrum Sample_S(const Scene &scene, Float u1, const Point2f &u2,
                              MemoryArena &arena, SurfaceInteraction *si,
                              Float *pdf) const = 0;
protected:
    const SurfaceInteraction &po;
    Float eta;
};
```

它储存了一个出射位置和方向的 `SurfaceInteraction po` ，并根据入射光线的位置 `pi` 和入射角度 `wi` 计算这一情况下的 BSSRDF 函数值。和 BSDF 一样，它也提供了采样 - 求值函数以应对极端情况。

## 11.4.1 Separable BSSRDFs

BSSRDF 的最大问题在于，出于它的一般性，它所需要存储数据的维度实在是太高了。即使在一个简单的平面上寻找 BSSRDF 就已经是十分困难的过程了，而 BSSRDF 需要能应用于任意复杂的模型之上。为了简化支持更一般的模型的能力，PBRT 中引入了 `SeparableBSSRDF`。这个接口将 BSSRDF 分为了三个互相独立的部分的乘积：

$$S(p_o,\omega_o,p_i,\omega_i) \approx (1 - F_r(\cos\theta_o))S_p(p_o,p_i)S_{\omega}(\omega_i)$$

这三个部分的含义如下：

- 第一项使用出射位置的菲涅尔项表示从出射点通过折射透过出射点的能量比例
- 第二项将两点的空间差异带来的影响单独建模提出为一项
- 第三项表示了从入射位置进入介质的能量比例，其中内含了另一个菲涅尔项

对于高 albedo 的介质，它们往往表现出更强的各向同性，此时菲涅尔传输项对最终的方向相关的结果影响更大，因此这一近似方法较为准确。而对于低 albedo 的介质则可能出现偏差。

该式的第三项在实际使用中往往会使用一个在半球面上归一化后的菲涅尔项：

$$1 = \int_{H^2}S_{\omega}(\omega)\cos\theta\mathrm{d}\omega 
= \int_{H^2}{1 - F_r(\eta, \cos\theta) \over c\pi}\cos\theta\mathrm{d}\omega$$

将立体角积分展开有：

$$\begin{aligned}
c & = \int_0^{2\pi}\mathrm{d}\phi\int_0^{\pi/2}{1 - F_r(\eta, \cos\theta) \over \pi}\sin\theta\cos\theta\mathrm{d}\theta\\
 & = 1 - 2\int_0^{\pi/2}F_r(\eta, \cos\theta)\sin\theta\cos\theta\mathrm{d}\theta
\end{aligned}$$

其中后一项的积分被称为菲涅尔反射函数的一阶动量（first moment），其广义上的定义如下：

$$\bar{F}_{r, i}(\eta) = \int_0^{\pi/2}F_r(\eta, \cos\theta)\sin\theta\cos^i\theta\mathrm{d}\theta$$

而代表空间差异带来的传输衰减的中间项而言，我们通常会使用更加简单的、仅与两点的几何距离有关的一维函数代替：

$$S_p(p_o, p_i) \approx S_r(||p_o - p_i||)$$

这一近似需要要求整个介质都是相对均匀且表面较为平缓——任何的重大变化的距离均需要大于平均自由距离。

## 11.4.2 Tabulated BSSRDF

和 BSDF 一样，BSSRDF 也可以通过一个简单的表格表示出来。这也是 `SeperableBSSRDF` 接口的唯一实现。和 `FourierBSDF` 相似的，这一类型使用适应性的基于样条线的插值算法对距离相关的 $S_r$ 项进行插值，并将插值参数与其它四个角度一并作为表格储存。

虽然上述的说明中 $S_r$ 只是一个一维的函数，但实际上它会受到多个值的影响，完整的映射关系可以写成 $S_r(\eta,g,\rho,\sigma_t,r)$ 。为了降维，我们需要固定或合并其中的部分变量。

我们首先固定折射率参数 $\eta$ 和散射各向异性参数 $g$ ，这两个参数一般不会随着介质空间变化。其次，注意到这个公式中有物理量纲的值只有两个：$\sigma_t, r$ ，我们会将第一个透射率设为恒值 1 并将距离参数 $r$ 转换为无量纲的光学距离 $r_{optical}$ 。经此改变，整个 $S_r$ 函数就变成了仅与 albedo 和光学距离有关的二维函数了（需要注意转换单位时带来的额外系数项）。`BSSRDFTable` 储存了这样的一个表。

$$S_r(\eta,g,\rho,\sigma_t,r) = \sigma_t^2 S_r(\eta,g,\rho,1,r_{optical})$$

```cpp
struct BSSRDFTable {
    // BSSRDFTable Public Data
    const int nRhoSamples, nRadiusSamples;
    std::unique_ptr<Float[]> rhoSamples, radiusSamples;
    std::unique_ptr<Float[]> profile;
    std::unique_ptr<Float[]> rhoEff;
    std::unique_ptr<Float[]> profileCDF;

    // BSSRDFTable Public Methods
    BSSRDFTable(int nRhoSamples, int nRadiusSamples);
    inline Float EvalProfile(int rhoIndex, int radiusIndex) const {
        return profile[rhoIndex * nRadiusSamples + radiusIndex];
    }
};
```

表格将一系列（大概率也是不均匀的）采样位置储存在 `rhoSamples, radiusSamples` 中，而采样值则储存在 `profile` 中。

从 `BSSRDFTable` 中获取 $S_r$ 值的方式和 `BSDFTable` 大同小异，大致流程如下：

1. 对于每一个通道，首先将距离使用 $\sigma_t$ 转换为光学距离
2. 计算四个采样点的 Catmull Rom 权重
3. 加权平均各个采样点以得到最终结果，并乘以缩放系数
4. 在输出前 Clamp 掉插值中可能产生的负值错误

---

注意到 `TabulatedBSSRDF::rho` 实际储存的是单次散射后能量的衰减比例，这个值和表面材质的 albedo 还有一定的区别：表面的 albedo 会将多次散射也计入考量。后者被记为 effective albedo $\rho_\mathrm{eff}$。由于这一值会在其它计算中频繁使用，它被预计算并储存于 `BSSRDFTable::rhoEff` 中。

$$\rho_\mathrm{eff} = \int_0^{2\pi}\mathrm{d}\phi\int_0^{\infty}rS_r(r)\mathrm{d}r = 2\pi\int_0^{\infty}rS_r(r)\mathrm{d}r$$

这一值的具体计算过程将在 15 章中讨论，目前我们只需要知道它是一个关于 albedo 的严格单调递增函数即可。

## 11.4.3 Subsurface Scattering Materials

对于这些半透明的物体，PBRT 中有两种材质可以描述它们，分别是 `SubsurfaceMaterial, KdSubsurfaceMaterial` 它们的区别仅在于它们指定散射参数的方式有所不同。

`SubsurfaceMaterial` 将散射的参数作为纹理储存，使得表面的各个位置的参数值可能变化（这其实是一种对内部参数值变化的一种近似）。需要注意的是：这种变化会导致 BSSRDF 的可逆性被摧毁，因为材质总是在其中一个点上被采样，而交换出射入射点则会改变采样的位置，从而引起参数的改变。

`SubsurfaceMaterial` 会在出射点位置采样 $\sigma_t, \sigma_s$ ，进而使用这些信息初始化 `TabulatedBSSRDF` 。但这两个值的设置并不是艺术家友好的。它们对材质最后的表现的影响是非线性且非直觉的。为了解决这一问题，`KdSubsurfaceMaterial` 使用平均自由距离和漫反射率定义次表面散射的性质，通过 `SubsurfaceFromDiffuse()` 工具函数转换为标准的 BSSRDF 以供使用。这一部分将在 15 章中进一步讨论。