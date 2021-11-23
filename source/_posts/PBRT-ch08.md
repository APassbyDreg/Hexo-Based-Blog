---
title: PBRT 第八章笔记 | Notes for PBRT Chapter 08 - Reflection Models
date: 2021-11-21 17:02:43
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 8.1 Basic Interface

在 PBRT 中，BRDF 和 BTDF 这两种反射模型均使用 BxDF 接口：

```cpp
class BxDF {
  public:
    // BxDF Interface
    virtual ~BxDF() {}
    BxDF(BxDFType type) : type(type) {}
    bool MatchesFlags(BxDFType t) const { return (type & t) == type; }
    virtual Spectrum f(const Vector3f &wo, const Vector3f &wi) const = 0;
    virtual Spectrum Sample_f(const Vector3f &wo, Vector3f *wi,
                              const Point2f &sample, Float *pdf,
                              BxDFType *sampledType = nullptr) const;
    virtual Spectrum rho(const Vector3f &wo, int nSamples,
                         const Point2f *samples) const;
    virtual Spectrum rho(int nSamples, const Point2f *samples1,
                         const Point2f *samples2) const;
    virtual Float Pdf(const Vector3f &wo, const Vector3f &wi) const;
    virtual std::string ToString() const = 0;

    // BxDF Public Data
    const BxDFType type;
};
```

PBRT 支持的 BxDF 类型如下：

```cpp
enum BxDFType {
    BSDF_REFLECTION = 1 << 0,
    BSDF_TRANSMISSION = 1 << 1,
    BSDF_DIFFUSE = 1 << 2,
    BSDF_GLOSSY = 1 << 3,
    BSDF_SPECULAR = 1 << 4,
    BSDF_ALL = BSDF_DIFFUSE | BSDF_GLOSSY | BSDF_SPECULAR | BSDF_REFLECTION |
               BSDF_TRANSMISSION,
};
```

BxDF 接口中最重要的函数是 `Spectrum BxDF::f(const Vector3f &wo, const Vector3f &wi)` 它返回从 $w_i$ 方向入射的光线能量转移到 $w_o$ 方向上的比例。它假设不同波长的光线互相独立，因此返回值是一个 `Spectrum` 值，通过直接将它于入射光的 radiance 相乘即可得到出射光的 radiance 。

不是所有的 BxDF 都可以使用上面的函数采样。例如：完美光滑的镜面反射对于任意出射角度只有一个角度会贡献能量，这造成了对于几乎所有的入射光线调用 `BxDF::f` 的返回结果都是零。因此在这个接口中还提供了结合了采样和评估影响的结合函数 `BxDF::Sample_f` 。这个函数通过给定的出射角度、采样类型和生成的随机样本采样入射角，并将本次采样的 pdf 和对应的 f 值返回。

## 8.1.1 反射率

根据与出射方向是否相关这一点，可以引申出两种反射率的定义：与出射方向无关的 hemispherical-hemispherical reflectance 和与出射方向有关的 hemispherical-directional reflectance 。它们表示了所有的入射能量经过表面反射后出射的比例。

$$\rho_{\mathrm{hd}}(w_o)=\int_{\mathrm{H}^{2}(\mathbf{n})} f_{\mathrm{r}}\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}}\\

\rho_{\mathrm{hh}}=\frac{1}{\pi} \int_{\mathrm{H}^{2}(\mathbf{n})} \int_{\mathrm{H}^{2}(\mathbf{n})} f_{\mathrm{r}}\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{o}} \cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{o}} \mathrm{d} \omega_{\mathrm{i}}$$

`BxDF::rho` 函数提供了计算这两个反射率的方法，如果其中提供了一个出射方向则计算 $\rho_{hd}$ ，反之则计算 $\rho_{hh}$ 。

## 8.1.2 BxDF Scaling Adapter

在实际应用中，可能会出现将不同的 BxDF 以不同的比例结合到一起的情况。此时需要将 BxDF 的 f 值进行一定程度的缩放。`ScaledBxDF` 类完成了这一操作，它在 BxDF 的基础上额外加入了一个缩放参数，并在计算 `f, Sample_f` 的基础上在返回前将贡献值乘以该参数。

# 8.2 Specular Reflection and Transmission

本章中讨论了镜面反射和折射，它们的入射、出射关系分别为：

$$\theta_i = \theta_o \\
\eta_i\sin\theta_i = \eta_t\sin\theta_t$$

## 8.2.1 菲涅尔反射系数 (Fresnel Reflectance)

菲涅尔方程定义了光线在物体表面反射和折射（对于不透明物体即吸收）的能量比例，它是麦克斯韦方程在光滑表面上的解。给定入射光线与法线的夹角以及两种介质的折射率，菲涅尔方程可以得到光线在两个不同的极化方向上入射光的折射率。PBRT 中并不考虑光的极性，因此将最终的折射率简单地认为是两个方向上的折射率的平方均值。

在现实中计算菲涅尔方程时，通常需要考虑三种材质：

- dielectrics（电介质）：它们不导电，折射率通常是一个 1 - 3 之间的实数
- conductors（导体）：这类材质中具有自由电子，它们通常是不透明的且会将大部分能量反射出去。没有被反射的光子通常在进入导体后的 $0.1\mathrm{\mu m}$ 就被迅速吸收，因此在 PBRT 中并不考虑这种材质的折射情况。与 dielectrics 不同的是，这类材质的折射率是一个复数 $\bar{\eta} = \eta + ik$
- 除了上述两种材质外还有半导体材质，PBRT 并不考虑这一类物体

### dielectrics 的折射系数

电介质的菲涅尔公式的计算方法如下：

$$\begin{aligned}r_{\|} &=\frac{\eta_{\mathrm{t}} \cos \theta_{\mathrm{i}}-\eta_{\mathrm{i}} \cos \theta_{\mathrm{t}}}{\eta_{\mathrm{t}} \cos \theta_{\mathrm{i}}+\eta_{\mathrm{i}} \cos \theta_{\mathrm{t}}} \\r_{\perp} &=\frac{\eta_{\mathrm{i}} \cos \theta_{\mathrm{i}}-\eta_{\mathrm{t}} \cos \theta_{\mathrm{t}}}{\eta_{\mathrm{i}} \cos \theta_{\mathrm{i}}+\eta_{\mathrm{t}} \cos \theta_{\mathrm{t}}}\end{aligned}$$

对于非极性的光线，最终的菲涅尔反射系数计算方法为：

$$F_r = {1 \over 2}(r_{\parallel}^2 + r_{\perp}^2)$$

考虑能量守恒，折射系数即为 $F_t = 1 - F_r$

PBRT 中计算菲涅尔反射系数的函数是 `Float FrDielectric(Float cosThetaI, Float etaI, Float etaT)` ，它使用预先计算的入射光线与法线的夹角的余弦值和两种介质的折射率得到反射系数。需要注意的是，当光线与法线夹角为负数时，说明是从反向入射，此时需要做一点转换，交换介质的折射系数并将夹角取绝对值。

在准备工作完成后，整个计算工作分为两步：

1. 计算折射角度：这一步中不需要计算角度值，只需要计算 $\sin\theta_t$ 即可。当由折射公式得到的 $\sin\theta_t$ 大于 1 时，说明发生了全反射，此时直接让函数返回 1 即可
2. 使用 $\sin\theta_t$ 得到 $\cos\theta_t$ 并计算反射系数。

### conductor 的折射系数

导体的折射系数分为实部和虚部两个部分，其中的虚部 $k$ 一般称为吸收系数，因为部分入射光的能量会在导体中被吸收并最终转换为热能。下图展示了金的折射系数随波长变化的曲线（实线表示虚部、虚线表示实部）：

<center><img src="https://pbr-book.org/3ed-2018/Reflection_Models/au-k-eta.svg" style="max-height: 25vh; margin: 10px 0"/></center>

由于一般不考虑导体的折射情况，因此只需要考虑电介质与导体的交界处的反射情况，因此只需要输入一个虚部。导体的菲涅尔反射系数的计算方法如下：

$$\begin{aligned}r_{\perp}&=& \frac{a^{2}+b^{2}-2 a \cos \theta+\cos ^{2} \theta}{a^{2}+b^{2}+2 a \cos \theta+\cos ^{2} \theta} \\
r_{\|}&=& r_{\perp} \frac{\cos ^{2} \theta\left(a^{2}+b^{2}\right)-2 a \cos \theta \sin ^{2} \theta+\sin ^{4} \theta}{\cos ^{2} \theta\left(a^{2}+b^{2}\right)+2 a \cos \theta \sin ^{2} \theta+\sin ^{4} \theta} \\
a^{2}+b^{2}&=&\sqrt{\left(\eta^{2}-k^{2}-\sin ^{2} \theta\right)^{2}+4 \eta^{2} k^{2}}\\
a^2&=& b^2 + (\eta^{2}-k^{2}-\sin ^{2} \theta) \end{aligned}$$

### 代码实现

所有菲涅尔项的计算均继承于基类 `Fresnel` ：

```cpp
class Fresnel {
  public:
    // Fresnel Interface
    virtual ~Fresnel();
    virtual Spectrum Evaluate(Float cosI) const = 0;
    virtual std::string ToString() const = 0;
};
```

运行时，首先会通过构造函数构造一个界面，它接收 `Spectrum` 类型的 $\eta, k$ 值（如果需要的话），最后使用 `Evaluate` 函数从入射光的余弦得到反射系数。

`FresnelConductor, FresnelDielectric` 类实现了上述的两个公式，另外有无论从任何地方的入射光均会返回 1 的一种特殊情况由 `FresnelNoOp` 实现。

## 8.2.2 镜面反射

由于镜面反射只在一个角度上发生，积分可以使用一个 $\delta$ 冲激函数简化为一个位置处的值，即：

$$L_o(\omega_o)=F_r(\omega_r)L_i(\omega_r)$$

转换为 BRDF 的表达方式为（注意要抵消掉渲染公式中额外的 $\cos\theta$ 项）：

$$f_r(\omega_i, \omega_o) = {F_r(\omega_r)\delta(\omega_i-\omega_r) \over |\cos\theta_r|}$$

这种冲激函数的性质导致了在实际采样中恰好采样到 0 位置的可能性为零，因此它不适用 `BxDF::f` 接口（在本类中这个函数返回常值 0 ），而需要使用 `BxDF::Sample_f` 接口，首先用反射方法采样一个 pdf 为 1 的入射角度，再返回上述 BRDF 的值。

除此之外，本类中还含有一个 `Spectrum R` 成员变量，用来表示在反射的过程中有多少能量没有被吸收。

## 8.2.3 镜面折射

<center><img src="https://pbr-book.org/3ed-2018/Reflection_Models/Radiance%20change%20at%20refraction.svg" style="max-height: 30vh; margin: 10px 0"/></center>

与反射不同，在不同介质之间折射的情况下 radiance 的变化还需要乘以一个折射率的平方比例，直觉上可以将这一项缩放系数理解为光线在折射后固体角发生了拉伸或者压缩。详细的推导过程如下：

首先从能量角度看：

$$\mathrm{d\Phi_o} = (1 - F_r(\omega_i))\mathrm{d\Phi_i}$$

展开为 radiance 的样式

$$L_o\cos\theta_o\mathrm{dA}\mathrm{d\omega_o} = (1 - F_r(\omega_i))L_i\cos\theta_i\mathrm{dA}\mathrm{d\omega_i}$$

再将固体角展开

$$L_o\cos\theta_o\mathrm{dA}(\sin\theta_o\mathrm{d\theta_o}\mathrm{d\phi_o}) = (1 - F_r(\omega_i))L_i\cos\theta_i\mathrm{dA}(\sin\theta_i\mathrm{d\theta_i}\mathrm{d\phi_i})$$

注意到折射的公式：

$$\eta_o\sin\theta_o = \eta_i\sin\theta_i\\
\eta_o\cos\theta_o\mathrm{d\theta_o} = \eta_i\cos\theta_i\mathrm{d\theta_i}$$

将上述公式带入固体角展开的公式，并注意到 $\phi_i= \phi_o + \pi$

$$L_o = (1 - F_r(\omega_i)){\eta_o^2 \over \eta_i^2}$$

由此得到 BRDF 的表达式：

$$f_r(\omega_o, \omega_i) = (1 - F_r(\omega_i)){\eta_o^2 \over \eta_i^2}\cdot{\delta(\omega_i-\omega_t) \over |\cos\theta_r|}$$

折射情况下和反射的代码实现大同小异，其中含的成员变量 `Spectrum T` 和上一节中的 `Spectrum R` 类似，用来表示在折射的过程中有多少能量没有被吸收。

## 8.2.4 Fresnel-Modulated Specular Reflection and Transmission

`FresnelSpecular` 类型统一了 dielectrics 的折射和反射（因为我们不考虑导体的折射）。

该接口在调用 `Sample_f` 时会首先计算菲涅尔反射系数，并将这个系数和随机数的第一个元素比对。当随机数在反射系数范围内时，采样反射光线、否则采样折射光线。它会将采样的类型写入 `BxDFType *sampledType` 中。

# 8.3 Lambertian Reflection

与镜面反射相对的另一种理想材质是 Lambertian Reflection，它代表了完美的漫反射。从任意角度入射的光线能量会被均匀地分布到整个半球面上。其 BRDF 可以简单地表示为：

$$f_r(\omega_i, \omega_o) = C = {1 \over \pi}$$

这个公式可以通过将常数值带入 $\rho_{hd}$ 的公式求解。需要注意的是将固体角积分转换为极坐标积分时有另一个额外的 $\cos$ 项。它在实现上异常简单：

```cpp
class LambertianReflection : public BxDF {
  public:
    // LambertianReflection Public Methods
    LambertianReflection(const Spectrum &R)
        : BxDF(BxDFType(BSDF_REFLECTION | BSDF_DIFFUSE)), R(R) {}
    Spectrum f(const Vector3f &wo, const Vector3f &wi) const { return R * InvPi; }
    Spectrum rho(const Vector3f &, int, const Point2f *) const { return R; }
    Spectrum rho(int, const Point2f *, const Point2f *) const { return R; }
    std::string ToString() const;

  private:
    // LambertianReflection Private Data
    const Spectrum R;
};
```

与反射项相似的可以定义 Lambertian Transmission ，其实现过程和本类相似。

# 8.4 Microfacet Models

<center><img src="https://pbr-book.org/3ed-2018/Reflection_Models/Rough%20smooth%20microfacets.svg" style="max-height: 20vh; margin: 10px 0"/></center>

微表面模型将一个粗糙表面视作由大量凹凸不平的微小平滑表面组成，光线在这些以某种统计学分布的微表面上以某种方式被散射出去。这种模型的对光的影响分为以下几类：

<center><img src="https://pbr-book.org/3ed-2018/Reflection_Models/Masking%20shadowing%20interreflection.svg" style="max-height: 20vh; margin: 10px 0"/></center>

1. Masking：反射的光线被微表面遮挡
2. Shadowing：入射的光线被微表面遮挡
3. Interreflection：光线经过多个微表面的反射到达观察者

## 8.4.1 Oren-Nayar Diffuse Reflection

Oren 和 Nayar 从对现实世界的观测中发现：现实世界中粗糙表面通常会在当入射角度接近观测角度时变得更明亮。他们由此提出了一种简单的「V」形微表面模型，其中的微表面发现分布由单一参数 $\sigma$ 的球形高斯分布控制。这种模型最终的 BRDF 计算方式如下：

$$f_{\mathrm{r}}\left(\omega_{\mathrm{i}}, \omega_{\mathrm{o}}\right)=\frac{R}{\pi}\left(A+B \max \left(0, \cos \left(\phi_{\mathrm{i}}-\phi_{\mathrm{o}}\right)\right) \sin \alpha \tan \beta\right)
$$

其中 $\sigma$ 由弧度表示，$A,B,\alpha,\beta$ 的定义如下：

$$\begin{aligned}A &=1-\frac{\sigma^{2}}{2\left(\sigma^{2}+0.33\right)} \\B &=\frac{0.45 \sigma^{2}}{\sigma^{2}+0.09} \\\alpha &=\max \left(\theta_{\mathrm{i}}, \theta_{\mathrm{o}}\right) \\\beta &=\min \left(\theta_{\mathrm{i}}, \theta_{\mathrm{o}}\right)\end{aligned}$$

这类模型的实现见 `OrenNayar`

## 8.4.2 微表面分布函数

<center><img src="https://pbr-book.org/3ed-2018/Reflection_Models/Microfacet%20normalize%20dA.svg" style="max-height: 15vh; margin: 10px 0"/></center>

为了表示微表面模型的几何特征，我们需要一种方法表示它的分布函数。`MicrofacetDistribution` 类提供了这一层抽象。这个类型的主要目的是计算微表面的分布函数 $D(\omega_h)$ ，它表示表面法线在各个方向上的微分面积的比例。它必须被归一化：

$$\int_{H^2(n)}D(\omega_h)\cos\theta_h\mathrm{d\omega_h} = 1$$

如上图，微表面分布函数体现了不同角度的面的面积，这些面积在乘以 $\cos\theta_h$ 后需要可以被归一化到于直射相同的微分面积上。

### Beckmann–Spizzichino 微表面模型

Beckmann–Spizzichino 模型的函数分布如下：

$$D\left(\omega_{\mathrm{h}}\right)=\frac{\mathrm{e}^{-\tan ^{2} \theta_{\mathrm{h}}\left(\cos ^{2} \phi_{\mathrm{h}} / \alpha_{x}^{2}+\sin ^{2} \phi_{\mathrm{h}} / \alpha_{y}^{2}\right)}}{\pi \alpha_{x} \alpha_{y} \cos ^{4} \theta_{\mathrm{h}}}$$

其中 $\alpha_x, \alpha_y$ 分别是 uv 方向上的各向异性参数，对于各向同性的材质，$\alpha_x = \alpha_y = \sqrt{2}\sigma$ ，其中 $\sigma$ 是微表面的 RMS slope。

### Trowbridge–Reitz (GGX) 微表面模型

Trowbridge–Reitz 模型的函数分布如下：

$$D\left(\omega_{\mathrm{h}}\right)=\frac{1}{\pi \alpha_{x} \alpha_{y} \cos ^{4} \theta_{\mathrm{h}}\left(1+\tan ^{2} \theta_{\mathrm{h}}\left(\cos ^{2} \phi_{\mathrm{h}} / \alpha_{x}^{2}+\sin ^{2} \phi_{\mathrm{h}} / \alpha_{y}^{2}\right)\right)^{2}}$$

<center><img src="https://pbr-book.org/3ed-2018/Reflection_Models/beckmann-vs-tr-tails.svg" style="max-height: 25vh; margin: 10px 0"/></center>

这种模型具有更加平滑的尾部（又称长尾效应），且可以提供一种快速地从范围在 $[0,1]$ 的粗糙度参数转换为各向异性参数 $\alpha$ 的方法 `TrowbridgeReitzDistribution::RoughnessToAlpha(Float roughness)` 。PBRT 中使用了四阶泰勒展开计算这一值。

## 8.4.3 Masking and Shadowing

本节中讨论了之前提到的 Masking 和 Shadowing 两种影响。这主要是由于从视点或光线方向看到物体的表面上会有一部分的微表面因为被别的微表面遮挡二不可见的情况，它们通常使用 masking-shadowing 函数 $G_1(\omega, \omega_h)$ 表示，即给定观测方向和一种微表面的法线方向，这类微表面对于该观测方向的可见度。在更理想的情况下，这个值通常对于所有的微表面法线均相同，可以将这个函数简化为 $G(\omega)$ 。

<center><img src="https://pbr-book.org/3ed-2018/Reflection_Models/Microfacet%20visible%20area.svg" style="max-height: 20vh; margin: 10px 0"/></center>

使用投影定理不难得到这个函数的一个限制条件：

$$\cos \theta=\int_{\mathrm{H}^{2}(\mathbf{n})} G_{1}\left(\omega, \omega_{\mathrm{h}}\right) \max \left(0, \omega \cdot \omega_{\mathrm{h}}\right) D\left(\omega_{\mathrm{h}}\right) \mathrm{d} \omega_{\mathrm{h}}$$

由于微表面形成了一个高度场，每一个背光的表面均会在面向光源的一面上投影相同的阴影，我们用 $A^{\pm}(\omega)$ 分别表示面光的投影面积和逆光的投影面积，有：

$$\begin{aligned}
\cos\theta &= A^+(\omega) - A^-(\omega)\\
G_1(\omega) &= {A^+(\omega) - A^-(\omega) \over A^+(\omega)}
\end{aligned}$$

masking-shadowing 函数通常会使用一个中间量 $\Lambda$ 书写，它表示了背光的投影面积占总体投影面积的比例，由 `MicrofacetDistribution::Lambda(const Vector3f &w)` 接口实现：

$$\Lambda(\omega) = {A^-(\omega) \over A^+(\omega) - A^-(\omega)} = {A^-(\omega) \over \cos\theta}\\
G_1(\omega) = {1 \over 1 + \Lambda(\omega)}$$

最后，要计算入射和出射方向上的 masking-shadowing 函数时，一种简单的方法是直接将两个方向上的函数值相乘，即 $G(\omega_i, \omega_o) = G_1(\omega_i)G_1(\omega_o)$

但这要求各个方向上的可见性是完全独立的，但这并不现实，如当 $\omega_i = \omega_o$ 时显然应该有 $G(\omega_i, \omega_o) = G_1(\omega_i) = G_1(\omega_o)$ ，因此人们通常会使用另一种计算双向 masking-shadowing 函数的方法：

$$G(\omega_i, \omega_o) = {1 \over 1 + \Lambda(\omega_i) + \Lambda(\omega_i)}$$

### Beckmann–Spizzichino 分布下的 $\Lambda$ 函数

$$\Lambda(\omega)=\frac{1}{2}\left(\operatorname{erf}(a)-1+\frac{\mathrm{e}^{-a^{2}}}{a \sqrt{\pi}}\right)$$

其中：

$$\begin{aligned}
a & ={1 \over \tan\theta\sqrt{\alpha_x^2\cos^2\phi + \alpha_y^2\sin^2\phi}}\\
\mathrm{erf}(x) & = {2 \over \sqrt{\pi}}\int_0^xe^{-t^2}\mathrm{dt}
\end{aligned}$$

### Trowbridge–Reitz 分布下的 $\Lambda$ 函数

$$\Lambda(\omega)=\frac{-1 + \sqrt{1 + \alpha^2\tan^2\theta}}{2}$$

其中：

$$\alpha^2 = \alpha_x^2\cos^2\phi + \alpha_y^2\sin^2\phi$$

## 8.4.4 Torrance–Sparrow 模型

<center><img src="https://pbr-book.org/3ed-2018/Reflection_Models/Torrance%20sparrow%20setting.svg" style="max-height: 15vh; margin: 10px 0"/></center>

Torrance–Sparrow 模型认为每一个微表面都是光滑的镜面，光线在这些微表面上满足菲涅尔反射定理。即有：

$$\omega_h = \widehat{\omega_i + \omega_o}$$

从某个方向入射的能量可以表示为：

$$\mathrm{d} \Phi_{\mathrm{h}}=L_{\mathrm{i}}\left(\omega_{\mathrm{i}}\right) \mathrm{d} \omega \mathrm{d} A^{\perp}\left(\omega_{\mathrm{h}}\right)=L_{\mathrm{i}}\left(\omega_{\mathrm{i}}\right) \mathrm{d} \omega \cos \theta_{\mathrm{h}} \mathrm{d} A\left(\omega_{\mathrm{h}}\right)$$

其中对该方向的投影面积微分可以转化到对微表面的面积微分：

$$\mathrm{dA(\omega_h)} = D(\omega_h)\mathrm{d\omega_hdA}$$

另一方面，认为每个微表面分布独立地以菲涅尔系数反射光线：

$$\mathrm{d\Phi_o} = F_r(\omega_o)\mathrm{d\Phi_h}$$

再利用出射 radiance 的定义有：

$$L\left(\omega_{\mathrm{o}}\right)=\frac{F_{\mathrm{r}}\left(\omega_{\mathrm{o}}\right) L_{\mathrm{i}}\left(\omega_{\mathrm{i}}\right) \mathrm{d} \omega_{\mathrm{i}} D\left(\omega_{\mathrm{h}}\right) \mathrm{d} \omega_{\mathrm{h}} \mathrm{d} A \cos \theta_{\mathrm{h}}}{\mathrm{d} \omega_{\mathrm{o}} \mathrm{d} A \cos \theta_{\mathrm{o}}}$$

此时需要用到一个在 14.1.1 中会证明的先验知识：

$$\mathrm{d\omega_h} = {\mathrm{d\omega_o} \over 4\cos\theta_h}$$

通过消元，我们最终得到了这一微表面模型下的 BRDF ：

$$f_r(\omega_i, \omega_o) = {F_r(\omega_o)G(\omega_o,\omega_i)D(\omega_h) \over 4\cos\theta_o\cos\theta_i}$$

这正是 PBRT 的 `MicrofacetReflection` 类使用的 BRDF。该类通过一个反射系数 `R` 、一个微表面分布（通过多态实现）和一个菲涅尔项作为输入，计算最终的 BRDF：

```cpp
class MicrofacetReflection : public BxDF {
  public:
    // MicrofacetReflection Public Methods
    MicrofacetReflection(const Spectrum &R,
                         MicrofacetDistribution *distribution, Fresnel *fresnel)
        : BxDF(BxDFType(BSDF_REFLECTION | BSDF_GLOSSY)),
          R(R),
          distribution(distribution),
          fresnel(fresnel) {}
    Spectrum f(const Vector3f &wo, const Vector3f &wi) const;
    Spectrum Sample_f(const Vector3f &wo, Vector3f *wi, const Point2f &u,
                      Float *pdf, BxDFType *sampledType) const;
    Float Pdf(const Vector3f &wo, const Vector3f &wi) const;
    std::string ToString() const;

  private:
    // MicrofacetReflection Private Data
    const Spectrum R;
    const MicrofacetDistribution *distribution;
    const Fresnel *fresnel;
};
```

同样的，`MicrofacetTransmission` 也以相似的方式实现。

# 8.5 Fresnel Incidence Effects

之前讨论的问题均没有考虑多层结构，以及由于菲涅尔效应导致的下层受光减少的现象。本节中讨论的 `FresnelBlend` 类型描述了一个多层结构：一个薄光滑表面下方加上另一个漫反射表面的反射表现。它利用入射角度混合两个表面的颜色。 

<center><img src="https://pbr-book.org/3ed-2018/Reflection_Models/Fresnel%20incidence.svg" style="max-height: 10vh; margin: 10px 0"/></center>

这个模型中使用了一种著名的 Fresnel 函数近似方法：

$$F_r(\cos\theta) = R + (1 - R)(1 - \cos\theta)^5$$

其中 $R$ 表示竖直入射时的反射率。

本例中的镜面反射项和漫反射项的计算公式分别如下：

$$f_{specular}\left(\mathrm{p}, \omega_{\mathrm{i}}, \omega_{\mathrm{o}}\right)=\frac{D\left(\omega_{\mathrm{h}}\right) F\left(\omega_{\mathrm{o}}\right)}{4\left(\omega_{\mathrm{h}} \cdot \omega_{\mathrm{i}}\right)\left(\max \left(\left(\mathbf{n} \cdot \omega_{\mathrm{o}}\right),\left(\mathbf{n} \cdot \omega_{\mathrm{i}}\right)\right)\right)}\\
f_{diffuse}\left(\mathrm{p}, \omega_{\mathrm{i}}, \omega_{\mathrm{o}}\right)=\frac{28 R_{\mathrm{d}}}{23 \pi}\left(1-R_{\mathrm{s}}\right)\left(1-\left(1-\frac{\left(\mathbf{n} \cdot \omega_{\mathrm{i}}\right)}{2}\right)^{5}\right)\left(1-\left(1-\frac{\left(\mathbf{n} \cdot \omega_{\mathrm{o}}\right)}{2}\right)^{5}\right)$$

将这两项的结果相加即可得到最终的 BRDF 。

# Fourier Basis BSDFs

虽然之前描述的模型能满足大部分需求，但仍让有很大部分的 BRDF 形状无法用此前的模型描述（如更复杂的多层结构）。对于这些 BRDF ，最简单的解决方案就是直接使用特殊设备在现实中进行采样，并使用一个 3D 或 4D 的查找表储存 BRDF 在不同的情况下的取值，但这无疑会占用大量空间。为了解决这种问题，本节引入了傅里叶变换来压缩 BRDF 的表示。

Fourier Basis BSDFs 使用了一系列三角函数作为基函数，只需储存基函数的参数，在使用时将各个基函数累加起来即可得到重建的 BRDF 值。本节中不会讨论 BRDF 是如何从原始数据转化为基函数的参数，而只关注其应用方法。

首先，BRDF 可以表示为一个四维的函数：

$$f(\omega_i, \omega_o) = f(\mu_i,\phi_i,\mu_o,\phi_o)$$

特别地，如果我们认为表面表现为各项同性，有：

$$f(\omega_i, \omega_o) = f(\mu_i,\mu_o,\phi_i-\phi_o) = f(\mu_i,\mu_o,\phi_o-\phi_i)$$

这样就可以将参数维度降低到 3 维。接下来我们在含有 $\cos$ 项的 BRDF 在第三个维度 $\phi$ 处使用傅里叶展开：

$$f(\mu_i,\mu_o,\phi_o-\phi_i)|\mu_i| = \sum_{k}a_k(\mu_i,\mu_o)\cos(k(\phi_o-\phi_i))$$

其中 $a_k(\mu_i,\mu_o)$ 的值会使用一个 $n \times n$ 的表格储存（其中的 $\mu$ 取值并不一定需要是等间距的，使用时会通过二分查找定位对应位置），如果使用 $m$  个 $\cos$ 项，整个傅里叶表示法需要 $m \times n \times n$ 的空间储存参数。

`FourierBSDFTable` 储存了一个这样的表：

```cpp
		struct FourierBSDFTable {
		Float eta; // 介质的 roi
		int mMax;  // 傅里叶阶数
		int nChannels; // 光谱通道数
		int nMu;       // μ 的采样数量
		Float *mu;     // μ 的取值位置
		int *m;        // 用于储存不同 μ 对使用的傅里叶阶数
		int *aOffset;  // 用于从两个 μ 的位置查询 a 的偏移量
		Float *a;      // a[aOffset[offset]] 以及后 m*ch 个位置储存了傅里叶系数
		Float *a0;
		Float *cdf;
		Float *recip;
		
		static bool Read(const std::string &filename, FourierBSDFTable *table);
		const Float *GetAk(int offsetI, int offsetO, int *mptr) const;
		bool GetWeightsAndOffset(Float cosTheta, int *offset, Float weights[4]) const;
};
```

`FourierBSDF` 类型封装了这种类型的 BRDF ，它保存了一个指向数据表的常引用。由于 $a_k$  是使用离散的值储存的，因此有必要对他进行插值。在实践中，会从角度周围的 4 * 4 个采样点处加权插值得到 $a_k$ ：

$$a_k = \sum_{a=0}^3\sum_{b=0}^3 a_k(offset_i+a,offset_o+b)w_i(a)w_o(b)$$

> 这里不使用 offset 左侧的样本加权主要是因为在 `GetWeightsAndOffset` 中就已经把 offset 设置在最左侧的样本位置上了
> 

最后使用三角基函数计算拟合的 BSDF 值：

$$f(\phi) = \sum_{k=0}^{m-1}a_k\cos(k\phi)$$

当阶数较多时，反复计算三角函数可能带来较多的开销，此时可以使用三角函数中的倍角公式利用递推的方法减少计算量：

$$\cos(k\phi) + \cos((k-2)\phi)\\
=\cos((k-1)\phi + \phi) + \cos((k-1)\phi-\phi)\\
=2\cos\phi\cos((k-1)\phi)$$

## 8.6.1 Spline Interpolation

这部分介绍了如何使用基于样条线（spline-based）的插值算法重建 $a_k$ 参数。PBRT 中使用了 Catmull–Rom 样条线，它含有四个控制点，通过加权和计算曲线的值。这条曲线会经过所有控制点，并提供一段较为平缓的插值。

想要理解这一插值的方法，我们首先需要由一个三次多项式的角度逼近。

我们希望利用 $p(x) = ax^3 + bx^2 + cx + d$ 去拟合某一段曲线在 $x_0, x_1$ 的范围内的片段，即有 ：

$$p(x_0) = f(x_0),\ p(x_1) = f(x_1)\\
p'(x_0) = f'(x_0),\ p'(x_1) = f'(x_1)$$

把这几个有关原函数的值作为未知数，并将整个区间归一化到 $[0,1]$ 范围内，我们就能解得上述多项式的系数：

$$\begin{aligned}
a &=f^{\prime}\left(x_{0}\right)+f^{\prime}\left(x_{1}\right)+2 f\left(x_{0}\right)-2 f\left(x_{1}\right) \\
b &=3 f\left(x_{1}\right)-3 f\left(x_{0}\right)-2 f^{\prime}\left(x_{0}\right)-f^{\prime}\left(x_{1}\right) \\
c &=f^{\prime}\left(x_{0}\right) \\
d &=f\left(x_{0}\right)
\end{aligned}$$

然而一个不可忽视的现实情况是：BSDF 的导数常常不能直接得到，因此我们需要用离散的差值去逼近它：

$$f'(x_i) \approx {f(x_{i+1}) - f(x_{i-1}) \over x_{i+1} - x_{i-1}}$$

使用此不等式带入之前的方程即可得到：

$$\quad p(x)=w_{0} f\left(x_{-1}\right)+w_{1} f\left(x_{0}\right)+w_{2} f\left(x_{1}\right)+w_{3} f\left(x_{2}\right)$$

其中：

$$\begin{aligned}
&w_{0}=\frac{x^{3}-2 x^{2}+x}{x_{-1}-1} \\
&w_{1}=2 x^{3}-3 x^{2}+1-\frac{x^{3}-x^{2}}{x_{2}}=\left(2 x^{3}-3 x^{2}+1\right)-w_{3} \\
&w_{2}=-2 x^{3}+3 x^{2}+\frac{x^{3}-2 x^{2}+x}{1-x_{-1}}=\left(-2 x^{3}+3 x^{2}\right)-w_{0} \\
&w_{3}=\frac{x^{3}-x^{2}}{x_{2}}
\end{aligned}$$

需要注意的是，当使用这种方法进行插值的时候，可能会在边界情况下出现超出范围的参数访问，此时 PBRT 会将对应的权重设为 0 以避免这些影响。