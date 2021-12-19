---
title: PBRT 第十四章笔记 | Notes for PBRT Chapter 14 - Light Transport I > Surface Reflection (basis)
date: 2021-12-18 22:42:10
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 14.1 Sampling Reflection Functions

`BxDF::Sample_f()` 函数会根据底层的散射函数采样一个出射光线，它接收来自 $U[0,1]^2$ 的随机数，输出采样方向、经散射出射的光线 BSDF 值以及本次采样对应的固体角上的 PDF 。

在默认情况下，这一函数会使用 $\cos$  加权的半球采样方法采样光线，这对于大部分非 delta 分布的 BRDF 都是一种可接受的采样方法（但这意味着需要在整个球面内采样的 BTDF 必须重载这一方法）。默认的实现同样会在光线从法线另一侧入射时翻转出射光线，从而保证出射与入射方向在法线的同一侧。（即物体的两面都已相同的材质参数反射光线）

## 14.1.1 Microfacet BxDFs

在 8.4 节中提到的微表面模型使用 $D(\omega_h)$ 描述了镜面反射 / 折射的表面法线分布。由于这一函数对 Torrance–Sparrow 模型的最终 BSDF 曲线影响极大，对于这一分布进行采样，再使用微表面法线计算反射或折射的光线就成为了一种非常有效的方法。为此 `MicrofacetDistribution` 中实现了从出射光线和宏观法线采样微表面法线的方法 `Sample_wh` 。

### 直接采样法线分布

一种采样方法是直接对 $D$ 进行采样，以各向同性的 Beckmann 分布为例，其两个参数是可分的，即 $p(\theta,\phi) = p(\theta)p(\phi)$ ，由于其各项同性，易有：

$$p(\phi) = {1 \over 2\pi},\ \phi = 2\pi\xi$$

而由于：

$$\int_{H^2(n)} p(\theta, \phi)\mathrm{d}\theta\mathrm{d}\phi = 1\\
\int_{H^2(n)} D(\omega_h)\cos\theta_h\mathrm{d}\omega_h = \int_{H^2(n)} D(\omega_h)\cos\theta_h\sin\theta_h\mathrm{d}\theta\mathrm{d}\phi  = 1
$$

有：

$$p(\theta) = {D(\theta,\phi)\sin\theta\cos\theta \over p(\phi)} = {2e^{-\tan^2\theta / \alpha^2}\sin\theta \over \alpha^2\cos^3\theta}$$

对其积分得到 CDF ：

$$\begin{aligned}P_{\mathrm{h}}\left(\theta^{\prime}\right) &=\int_{0}^{\theta^{\prime}} \frac{2 \mathrm{e}^{-\tan ^{2} \theta / \alpha^{2}} \sin \theta}{\alpha^{2} \cos ^{3} \theta} \mathrm{d} \theta \\&=1-\mathrm{e}^{-\tan ^{2} \theta^{\prime} / \alpha^{2}}\end{aligned}$$

则从 $\xi \sim U[0,1]$ 转换到该分布的转换方式如下：

$$\begin{aligned}
\xi &= 1 - e^{-tan^2\theta / \alpha^2}\\
\tan^2\theta &= -\alpha^2\log(1-\xi)
\end{aligned}$$

而有了 $\tan^2\theta$ 后，我们就可以轻易得到 $\cos^2\theta, \cos\theta$ 等用于计算位置值的参数了。

### 只采样可视法线

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/visible-normals.svg" style="max-height: 16vh; margin: 10px 0"/></center>

在给定入射光的方向时，并不是所有微表面模型的法线都是可见的，采样不可见的法线会造成计算的浪费。在给定出射方向 $\omega$ 时，可见法线的分布实际上应该如下：

$$D_\omega(\omega_h) = {D(\omega_h)G_1(\omega,\omega_h)\max(0, \omega\cdot\omega_h) \over \cos\theta}$$

这一公式的正确性可以由对 $G_1$ 的定义侧面佐证：

$$\cos \theta=\int_{\mathrm{H}^{2}(\mathbf{n})} G_{1}\left(\omega, \omega_{\mathrm{h}}\right) \max \left(0, \omega \cdot \omega_{\mathrm{h}}\right) D\left(\omega_{\mathrm{h}}\right) \mathrm{d} \omega_{\mathrm{h}} .$$

它首先通过一个 $G_1$ 项代表自阴影，进而用 $\max(0, \omega\cdot\omega_h) / \cos\theta$ 将面积投影回单位表面积上。下图展示了 $\cos\theta_o = 0.1, \alpha = 0.3$ 时两种采样方式产生的概率 lobe 的区别：

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/beckmann-d-full.svg" style="max-height: 30vh; margin: 10px"/><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/beckmann-d-visible.svg" style="max-height: 30vh; margin: 10px"/></center>

最后，我们需要对这种采样的 PDF 进行转换，因为我们进行采样时采样的是微表面的法线，但实际上我们需要的是入射光线的分布。为了进行 PDF 的转换，我们需要计算采样的微分。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/ndf-change-of-variables.svg" style="max-height: 16vh; margin: 10px 0"/></center>

由上图可得：$\mathrm{d}\phi_i = \mathrm{d}\phi_h, \mathrm{d}\theta_i = 2\mathrm{d}\theta_h$ ，则有：

$$\begin{aligned}\frac{\mathrm{d} \omega_{\mathrm{h}}}{\mathrm{d} \omega_{\mathrm{i}}} &=\frac{\sin\theta_h\mathrm{d}\theta_h\mathrm{d}\phi_h}{\sin\theta_i\mathrm{d}\theta_i\mathrm{r}\phi_i} \\&=\frac{\sin \theta_{\mathrm{h}} \mathrm{d} \theta_{\mathrm{h}} \mathrm{d} \phi_{\mathrm{h}}}{\sin 2 \theta_{\mathrm{h}} 2 \mathrm{~d} \theta_{\mathrm{h}} \mathrm{d} \phi_{\mathrm{h}}} \\&=\frac{\sin \theta_{\mathrm{h}}}{4 \cos \theta_{\mathrm{h}} \sin \theta_{\mathrm{h}}} \\&=\frac{1}{4 \cos \theta_{\mathrm{h}}} \\&=\frac{1}{4\left(\omega_{\mathrm{i}} \cdot \omega_{\mathrm{h}}\right)}=\frac{1}{4\left(\omega_{\mathrm{o}} \cdot \omega_{\mathrm{h}}\right)}\end{aligned}$$

因此对于任何采样 $\omega_h$ 的微表面分布，其 PDF 需要在微表面的 PDF 之上额外除以 $4\cos\theta_h$，即：

$$p(\theta) = {p_h(\theta_h)\over 4(\omega_o \cdot \omega_h)}$$

## 14.1.2 FresnelBlend

回顾 `FresnelBlend` 的定义，它混合了一个微表面材质和一个 Lambertian 材质。在采样时，我们假设这两个材质对出射的贡献是相当的，因此我们会以各 50% 的概率采样两个分布。而其 PDF 也就相当于两个分布 PDF 的算术均值。

在实现中，我们利用 $\xi_1$ 判断用于采样的分布，接着将 $\xi_1$ 重新映射到 $U[0,1]$  上。

## 14.1.3 Specular Reflection and Transmission

正如之前讨论过的，镜面反射和折射的光路是唯一确定的，因而其 PDF 是一个无法被采样的 delta 分布，任何该方向外的光线的 PDF 均为零，而对应方向上的 PDF 则是无穷大。

PBRT 中 `FresnelSpecular` 类型同时包装了反射和折射两种情况。它使用菲涅尔项作为选择传输模式的概率，从二者选出一种传输方式采样。

## 14.1.4 Fourier BSDF ⚠️

这一部分讲解了傅里叶 BSDF 的采样方法，此处暂且跳过

## 14.1.5 Application: Estimating Reflectance

回顾反射率的定义：

$$\begin{aligned}\rho_{\mathrm{hd}}\left(\omega_{\mathrm{o}}\right) &=\int_{\mathrm{H}^{2}(\mathbf{n})} f_{\mathrm{r}}\left(\omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}} . \\\rho_{\mathrm{hh}} &=\frac{1}{\pi} \int_{\mathrm{H}^{2}(\mathbf{n})} \int_{\mathrm{H}^{2}(\mathbf{n})} f_{\mathrm{r}}\left(\omega^{\prime}, \omega^{\prime \prime}\right)\left|\cos \theta^{\prime} \cos \theta^{\prime \prime}\right| \mathrm{d} \omega^{\prime} \mathrm{d} \omega^{\prime \prime},\end{aligned}$$

使用蒙特卡洛方法估计这两个值：

$$\begin{aligned}
\rho_{hd} &\approx\frac{1}{N} \sum_{j}^{N} \frac{f_{\mathrm{r}}\left(\omega, \omega_{j}\right)\left|\cos \theta_{j}\right|}{p\left(\omega_{j}\right)} \\
\rho_{hh} &\approx\frac{1}{\pi N} \sum_{j}^{N} \frac{f_{\mathrm{r}}\left(\omega_{j}^{\prime}, \omega_{j}^{\prime \prime}\right)\left|\cos \theta_{j}^{\prime} \cos \theta_{j}^{\prime \prime}\right|}{p\left(\omega_{j}^{\prime}\right) p\left(\omega_{j}^{\prime \prime}\right)}\end{aligned}$$

## 14.1.6 Sampling BSDFs

由于 `BSDF` 中储存了一个或多个 `BxDF` ，在 PBRT 中实际采样的则是它们的 PDF 均值：

$$p(\omega) = {1 \over N}\sum_{i=1}^N p_i(\omega)$$

`BSDF::Sample_f()` 函数接收两个随机数和入射光线的世界坐标为输入值，接着使用第一个随机数选择采样的分布编号（随后会将改随机数缩放回对应 $U[0,1]$ 分布）。紧接着调用对应的 `BxDF::Sample_f` 生成光线、在该采样上调用所有 `BxDF::Pdf` 计算 PDF 的均值，最后调用 `BxDF::f` 计算传输率的均值并返回。

# 14.2 Sampling Light Sources

对表面采样有可能有入射光线的位置是另一种重要性采样的重要方式。考虑一个 diffuse 的球体被一个相对较小的光源照亮的场景，当在表面上随机采样时，只有很小一部分的光源会采样到光源本身上，从而大大降低了收敛的速度。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/effective-light-sampling.svg" style="max-height: 30vh; margin: 10px 0"/></center>

对于光源而言，有两个必须实现的采样函数：`Sample_Li` 负责采样一条可能有来自该光源的入射方向，并返回对应的 radiance ；以及会在 16 章描述的 `Sample_Le` 。

光源的 `Light::Pdf_Li` 函数则与表面模型的 PDF 函数类似，它通过给定着色点和入射方向返回对于该光源和采样模式下的 PDF 。

## **14.2.1 采样 delta 分布的光源**

正如镜面反射和折射的表面，使用 delta 分布定义的光源的光路是唯一确定的，这导致它的 PDF 在除了光源方向外的所有位置均为 0 。另外将 `Sample_Li` 的 PDF 设为 1 以表示这条光路上的 radiance 就是光源的全部贡献。详细的实现方法在 12 章中已有描述。

## 14.2.2 采样面光源

在 PBRT 中，面光源是通过在一个 Shape 上附加发光特性而定义的。因此，为了采样这类光源，我们需要在物体表面均匀地生成样本。为了满足这一要求，在 `Shape` 类中设计了接口 `Shape::Sample` 。

PBRT 中实现了两种采样方法和他们对应的 PDF 计算函数：

第一种方法使用和表面积相关的采样分布在物体表面采样一点，并返回对应的 `SurfaceInteraction` 。除此之外它还需要初始化浮点误差 `pError` 以计算高鲁棒性的采样位置。对于这种方式而言，由于其样本是按表面积均匀地分布的，因此其 PDF 就相当与总表面积的倒数：$p = 1 / A$ 。

第二种方法则同时输入了需要着色的目标点 `SurfaceInteraction` 信息，以辅助在采样的过程中尽量不生成对该位置没有贡献的样本。与第一种方法返回基于表面积的概率不同的是，这一方法下的 PDF 函数返回的概率密度是基于着色点的立体角的一个值。

为了计算这一 PDF ，首先需要测试光线是否和光源几何体相交，如果不相交则 PDF 会被设为零（由于这样的样本不应该被生成或计算，这种方法是有效的，而且仅仅与一个物体求交的速度也是相对较快的）。下一步，为了将定义在光源面积上的 PDF 转换为定义在固体角上的 PDF ，我们有：

$${\mathrm{d}\omega_i \over \mathrm{d}A} = {\cos\theta_o\over r^2}$$

### 采样圆盘

对圆盘的采样并不复杂，在上一章中我们讨论了如何在单位圆中进行采样，在这里也只需要首先从单位圆上采样、接着将样本转换到对应的位置上即可。

PBRT 中的采样方法并没有考虑圆盘的 `innerRadius` 和 `pMax` ，为了支持这两个特征还需要对采样方式进行更进一步的修改。

### 采样圆柱

对柱面的采样并没有什么特别的：对高度和 $\phi$ 值的采样均是均匀地，只要简单地把来自 $U[0,1]^2$ 的样本映射到圆柱坐标上即可

### 采样三角形

从上一节中我们得到了返回均匀采样的重心坐标的函数 `UniformSampleTriangle` 。我们会使用它返回的重心坐标经过顶点位置插值得到最后的采样点。

### 采样球体

采样球体的方式同样由之前的 `UniformSampleSphere` 可得。和 `Disk` 一样 PBRT 中也没有支持对被切割过的球的采样。

虽然在整个球体上采样毫无疑问是正确的，但更加有效的方法是只采样对于着色点可见的那一部分。PBRT 在由球和着色点确定的立体角中进行采样。

为了实现这一点，首先会计算一个着色点上的局部坐标系，其中 $z$ 轴指向由着色点指向圆心的方向。接着根据相切的正交性可以判断采样的锥体内的 $\theta_{\max}$。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/spherical-light-source-cone.svg" style="max-height: 25vh; margin: 10px 0"/></center>

在得到了采样的锥体后，我们就可以在这上面做固体角上的均匀采样了。将均匀采样转换为球体表面坐标的方法如下三步：

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/sphere-angle-to-point.svg" style="max-height: 50vh; margin: 10px 0"/></center>

通过如上方法获得的 $\alpha$ 结合之前采样的 $\phi$ 就可以确定球上一点了。

> 个人感觉把局部坐标系的原点放在球体中心会不会好一些，但这要考虑 PDF 的转换
> 

对于样本对应的 PDF ，PBRT 会考虑采样方法的性质：对于在球体内部的点，会将 PDF 直接交由底层的 `Shape::Pdf` 处理以转换成固体角的 PDF ，而对于更一般的情况，则会返回之前所言的锥体上的 PDF 。

## 14.2.3 范围光源

有了之前的采样 Shape 的方法，漫反射面光源的 `Sample_Li()` 方法的实现就显而易见了：

```cpp
Spectrum DiffuseAreaLight::Sample_Li(const Interaction &ref,
        const Point2f &u, Vector3f *wi, Float *pdf,
        VisibilityTester *vis) const {
    Interaction pShape = shape->Sample(ref, u);
    pShape.mediumInterface = mediumInterface;
    *wi = Normalize(pShape.p - ref.p);
    *pdf = shape->Pdf(ref, *wi);
    *vis = VisibilityTester(ref, pShape);
    return L(pShape, -*wi);
}
```

而 `Pdf_Li()` 方法也仅是简单地转发 `Shape::Pdf` 接口即可。

## 14.2.4 无穷范围光源

在无穷远面光源的使用中，采用的光照贴图常常并不是均匀的，对于这类光源进行重要性采样就成了一个重要的减少方差的手段。采样这类光源的方法如下：

1. 使用贴图的辐照度初始化一个离散的二维 PDF
2. 在该 PDF 上进行重要性采样
3. 将 PDF 从 $(u,v)$ 坐标转换到球坐标上

需要注意的是，虽然 PDF 和贴图的大小是相同的，但其中的值已经被双线性插值平滑过了。这是为了尽可能地避免出现零值的 PDF：

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/const-approx-linear.svg" style="max-height: 16vh; margin: 10px 0"/></center>

由于 PBRT 中使用的映射是 $\theta = \pi u, \ 
\phi = 2\pi v$ ，在转换时就需要将每一行的 PDF 均除以 $\sin\theta$ 以调整这种映射带来的畸变。（没搞明白这里为啥不用 $\sin\theta$ 分布映射）

# 14.3 Direct Lighting

在引入一般的光线传输方程之前，首先介绍的是 `DirectLightingIntegrator` 。它只对场景中的直接光照进行着色，忽略所有间接光照。

PBRT 中提供了两种策略来计算直接光照，它们均是无偏的。第一种策略 `UniformSampleAll` 会遍历所有光源并根据对应光源的 `Light::nSamples` 采样，最后将各个光源的贡献相加。而第二种策略 `UniformSampleOne` 则随机地选择一个光源采样一次。一般而言，当图片的 SPP 较高时，可以选择后者以节省运行消耗，反之则需要使用前者降低噪声。

为了构建一个 `DirectLightingIntegrator` 对象，我们需要传入一个 `Camera` 和一个 `Sampler`

对象到 `SamplerIntergrator` 基类的构造函数中，并初始化计算策略和对于镜面反射和折射能使用的最大递归层数。

```cpp
// LightStrategy Declarations
enum class LightStrategy { UniformSampleAll, UniformSampleOne };

// DirectLightingIntegrator Declarations
class DirectLightingIntegrator : public SamplerIntegrator {
  public:
    // DirectLightingIntegrator Public Methods
    DirectLightingIntegrator(LightStrategy strategy, int maxDepth,
                             std::shared_ptr<const Camera> camera,
                             std::shared_ptr<Sampler> sampler,
                             const Bounds2i &pixelBounds)
        : SamplerIntegrator(camera, sampler, pixelBounds),
          strategy(strategy),
          maxDepth(maxDepth) {}
    Spectrum Li(const RayDifferential &ray, const Scene &scene,
                Sampler &sampler, MemoryArena &arena, int depth) const;
    void Preprocess(const Scene &scene, Sampler &sampler);

  private:
    // DirectLightingIntegrator Private Data
    const LightStrategy strategy;
    const int maxDepth;
    std::vector<int> nLightSamples;
};
```

在这一 `Integrator` 的预处理阶段，会根据计算策略决定是否需要预先生成一组随机样本。

作为一个基于采样的 `Integrator` ，这一类型中最重要的函数就是 `Li()` ，它的实现方法和 `WhittedIntegrator::Li()` 相似，其中最重要的函数调用是 `UniformSampleAllLights` 和 `UniformSampleOneLight` 。

由于 PBRT 中假定了光的线性特性，对于光照函数有：

$$\begin{gathered}L_{\mathrm{o}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)=\int_{\mathrm{S}^{2}} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L_{\mathrm{d}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}} \\\sum_{j=1}^{n} \int_{\mathrm{S}^{2}} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L_{\mathrm{d}(j)}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}}, \\L_{\mathrm{d}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)=\sum_{j} L_{\mathrm{d}(j)}\left(\mathrm{p}, \omega_{\mathrm{i}}\right) .\end{gathered}$$

因此，对所有光源进行采样并累加在一起就成为了最基础的采样方法，它被实现在 `UniformSampleAllLights` 中。

另一方面，当场景中存在着大量的光源时，逐个遍历他们以得到总的直接光照变得开销极大。此时，通过随机采样任意一个光源，并将结果乘以总光源数量，就能得到无偏的估计值了。

更进一步地，我们还可以使用 MIS 的思想使用一个非均匀的概率采样各个光源，从而加速它的收敛。

## 14.3.1 估计直接光照

在选择完确定的光源进行采样之后，我们就需要计算该光源经着色点散射，最终出射的光线辐照度了，这个辐照度被定义为：

$$\int_{\mathrm{S}^{2}} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L_{\mathrm{d}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}}$$

在采样时，对 BRDF 和对光源采样各有优势。对于 BRDF lobe 较为窄小的情况，对 BRDF 采样的效果较优，而对于光源的投影立体角较小的情况下，则对光源采样较优。PBRT 因此选用了结合二者的 MIS 采样方式来解决这一问题。

`EstimateDirect` 方法实现了对于直接光照的采样，它首先对光源进行一次采样，再接着对 BSDF 进行一次采样，最后使用 MIS 将二者结合起来。需要注意的是，对于以 delta 分布定义的光源，由于其 PDF 是恒值零，所以我们并不需要对它再额外进行 BSDF 采样。

# 14.4 The Light Transport Equation

光线传输方程（LTE）是描述场景中的辐照度平衡值的方程。它给出了从一点处出射的辐照度与其自发光、BSDF 以及入射的光线的关系。

## 14.4.1 基础概念

LTE 的成立依赖于我们在很早之前就对光线的性质做出的一系列假设，而其中最重要的一点就是能量守恒，这一概念在各个尺度上都有其体现。宏观上，我们有：

$$\Phi_o - \Phi_i = \Phi_e - \Phi_a$$

即从物体出射的能量与入射的能量的差值等于自释放的和吸收的能量之差。通过在某一位置的立体角上积分就有：

$L_o(p,\omega) = L_e(p,\omega) + \int_{S^2} f(p,\omega_o,\omega_i)L_i(p,\omega_i)|\cos\theta_i|\mathrm{d}\omega_i$

在真空中，对于入射光线又有：

$$L_i(p,\omega) = L_o(t(p,\omega), -\omega)$$

其中 $t(p,\omega)$ 是反方向的光线与物体的物理交点（也可能是无穷远表示无穷远的环境光）。通过这一公式我们就能写出一个递归的关系式：

$$L(p,\omega) = L_e(p,\omega) + \int_{S^2} f(p,\omega_o,\omega_i)L(t(p, \omega_i),-\omega_i)|\cos\theta_i|\mathrm{d}\omega_i$$

## 14.4.2 LTE 的解析解

虽然光线传输方程看似简单，但其在大部分情况下是无法得到解析解的。着是由于大部分场景中均包含了复杂的基于物理的材质、以及各个不同物体之间的各类关系。

当然，在一些极简的情况下我们确实可能为它找到解析解，虽然它对求解一般情况下的光线传输方程基本没有帮助，但它提供了 debug 部分 `Integrator` 的方法。如果一个 `Integrator` 给出的结果和解析解不同，这就能说明它绝对存在问题。

考虑一个带有自发光的 Lambertian 材质的球体的内侧，由于其对称性，我们可知从任意角度观察任意点得到的结果应该均是同一个常数 $L$ ，我们就有：

$$L(p,\omega_o) = L_e + \int_{H^2(n)}cL(t(p,\omega_i), -\omega_i)|\cos\theta_i|\mathrm{d}\omega_i\\
L = L_e + \rho_{hh}(L_e + \rho_{hh}(L_e + \cdots)) = {L_e \over 1 - \rho_{hh}}\\
L = L_e + c \pi L\\$$

由于 $\rho_{hh} < 1$ ，这一方程会严格收敛到有限的值上。

## 14.4.3 LTE 的表面形式

之所以 LTE 会写作由 $L_i$ 代表入射光的形式，主要是为了隐去场景中几何体对公式的影响。在这里我们会考虑该方程的另一种替代：

$$L(p' \to p) = L(p', \omega)\\
f(p'' \to p' \to p) = f(p', \omega_o, \omega_i)$$

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/three-point-form.svg" style="max-height: 30vh; margin: 10px 0"/></center>

除此之外，我们还需要将积分域从立体角转换到面积上。这将乘以一个 Jacobian 项 $|\cos\theta'|/r^2$ 。我们将这一个积分转换项和原来的 $|\cos\theta|$ 项，以及可见性测试的函数 $V(p \leftrightarrow p')$ 结合为一项 $G$ ：

$$G(p \leftrightarrow p') = V(p \leftrightarrow p') {|\cos\theta'||\cos\theta| \over ||p-p'||^2}$$

利用这一代换，我们就能写出 LTE 关于几何体面积的表达形式：

$$L\left(\mathrm{p}^{\prime} \rightarrow \mathrm{p}\right)=L_{\mathrm{e}}\left(\mathrm{p}^{\prime} \rightarrow \mathrm{p}\right)+\int_{A} f\left(\mathrm{p}^{\prime \prime} \rightarrow \mathrm{p}^{\prime} \rightarrow \mathrm{p}\right) L\left(\mathrm{p}^{\prime \prime} \rightarrow \mathrm{p}^{\prime}\right) G\left(\mathrm{p}^{\prime \prime} \leftrightarrow \mathrm{p}^{\prime}\right) \mathrm{d} A\left(\mathrm{p}^{\prime \prime}\right)$$

虽然这一公式与基于立体角的 LTE 是一致的，但它却体现了不同的采样思路：对于原始 LTE ，我们通常在点周围采样方向，追逐生成的光线以检测辐照度，而对于本公式则通常在物体表面采样对应数量的点，追踪其连线以确定实际贡献的辐照度。

## 14.4.4 在路径上积分

通过展开上述与面积有关的积分，我们可以得到一个有关路径的积分：

$$\begin{aligned}L\left(\mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right)=& L_{\mathrm{e}}\left(\mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right) \\&+\int_{A} L_{\mathrm{e}}\left(\mathrm{p}_{2} \rightarrow \mathrm{p}_{1}\right) f\left(\mathrm{p}_{2} \rightarrow \mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right) G\left(\mathrm{p}_{2} \leftrightarrow \mathrm{p}_{1}\right) \mathrm{d} A\left(\mathrm{p}_{2}\right) \\&+\int_{A} \int_{A} L_{\mathrm{e}}\left(\mathrm{p}_{3} \rightarrow \mathrm{p}_{2}\right) f\left(\mathrm{p}_{3} \rightarrow \mathrm{p}_{2} \rightarrow \mathrm{p}_{1}\right) G\left(\mathrm{p}_{3} \leftrightarrow \mathrm{p}_{2}\right) \times f\left(\mathrm{p}_{2} \rightarrow \mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right) G\left(\mathrm{p}_{2} \leftrightarrow \mathrm{p}_{1}\right) \mathrm{d} A\left(\mathrm{p}_{3}\right) \mathrm{d} A\left(\mathrm{p}_{2}\right)\\
& +\cdots\end{aligned}$$

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/path-annotated-1.svg" style="max-height: 40vh; margin: 10px 0"/></center>

公式右端的每一项都代表了一条长度递增的路径，这说明只要我们使用一种随机采样路径的算法，在足够的采样数下总能得到收敛的结果。

我们将右侧的累加式写为一个新的公式：

$$L(p_1 \to p_0) = \sum_{n=1}^\infty P(\bar{p}_n)$$

其中 $P(\bar{p}_n)$ 代表了从所有路径长度为 $n$ 的路径接收的 radiance 值。

$$\begin{aligned}P\left(\overline{\mathrm{p}}_{n}\right)=& \underbrace{\int_{A} \int_{A} \cdots \int_{A}}_{n-1} L_{\mathrm{e}}\left(\mathrm{p}_{n} \rightarrow \mathrm{p}_{n-1}\right) \left(\prod_{i=1}^{n-1} f\left(\mathrm{p}_{i+1} \rightarrow \mathrm{p}_{i} \rightarrow \mathrm{p}_{i-1}\right) G\left(\mathrm{p}_{i+1} \leftrightarrow \mathrm{p}_{i}\right)\right) \mathrm{d} A\left(\mathrm{p}_{2}\right) \cdots \mathrm{d} A\left(\mathrm{p}_{n}\right) .\end{aligned}$$

更进一步的，我们定义单条路径上的 throughput 为：

$$T\left(\overline{\mathrm{p}}_{n}\right)=\prod_{i=1}^{n-1} f\left(\mathrm{p}_{i+1} \rightarrow \mathrm{p}_{i} \rightarrow \mathrm{p}_{i-1}\right) G\left(\mathrm{p}_{i+1} \leftrightarrow \mathrm{p}_{i}\right)$$

因此：

$$\begin{aligned}P\left(\overline{\mathrm{p}}_{n}\right)=& \underbrace{\int_{A} \int_{A} \cdots \int_{A}}_{n-1} L_{\mathrm{e}}\left(\mathrm{p}_{n} \rightarrow \mathrm{p}_{n-1}\right) T(\overline{p}_n)\mathrm{d} A\left(\mathrm{p}_{2}\right) \cdots \mathrm{d} A\left(\mathrm{p}_{n}\right) .\end{aligned}$$

使用上述公式，在给定路径长度 $n$ 的情况下，我们就可以使用蒙特卡洛方法估计经由这一长度的路径到达着色点的 radiance 。而这一路径的选择可以是非常自由的。我们可以自由地从相机、光源、甚至场景中选取点来构建路径。

## 14.4.5 积分中的 delta 分布

由于特殊的 BSDF 和光源的形式，在 $P(\bar{p}_n)$ 中时常会出现 delta 函数。这类分布需要被光照传输算法显式地处理，使用随机算法是无法采样到这些分布的。

考虑一个只包含了单一点光源的直接光照场景 $P(\bar{p}_2)$ ：

$$\begin{aligned}P\left(\overline{\mathrm{p}}_{2}\right) &=\int_{A} L_{\mathrm{e}}\left(\mathrm{p}_{2} \rightarrow \mathrm{p}_{1}\right) f\left(\mathrm{p}_{2} \rightarrow \mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right) G\left(\mathrm{p}_{2} \leftrightarrow \mathrm{p}_{1}\right) \mathrm{d} A\left(\mathrm{p}_{2}\right) \\&=\frac{\delta\left(\mathrm{p}_{\text {light }}-\mathrm{p}_{2}\right) L_{\mathrm{e}}\left(\mathrm{p}_{\text {light }} \rightarrow \mathrm{p}_{1}\right)}{p\left(\mathrm{p}_{\text {light }}\right)} f\left(\mathrm{p}_{2} \rightarrow \mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right) G\left(\mathrm{p}_{2} \leftrightarrow \mathrm{p}_{1}\right) .\end{aligned}$$

点光源把对面积的积分简化为了单一的对光源位置的采样，这一简化得以发生主要是因为在任何其它采样点，delta 分布均会给出零值。

## 14.4.6 LTE 的分解

在诸多光线传输算法中，不同的算法常常会在不同的情况下表现优秀，而在其它情况下表现平平甚至非常差。如 whitted 光线追踪可以优秀地处理镜面反射和折射，但对漫反射表面无能为力，而基于密度估计的光子映射（stochastic progressive photon mapping, SPPM）算法则在粗糙表面能获得优秀的结果，但对光滑表面无能为力。

为了设计可以适应不同散射情况的光线传输算法，我们常常会将 LTE 进行分解。常用的一种分解方法如下：

$$L(p_1 \to p_0) = P(\bar{p}_1) + P(\bar{p}_2) + \sum_{i=3}^\infty P(\bar{p}_i)$$

其中第一项代表自发光、第二项代表直接光照，这二者均可以通过相对准确的算法得到，而第三项则会通常使用更高效但准确度略低的方案。

另一种分解的方法则可以从光源的贡献入手，如将光源分为来自小光源和大光源两类分别计算结果：

$$\begin{aligned}P\left(\overline{\mathrm{p}}_{n}\right)=& \int_{A^{n-1}}\left(L_{\mathrm{e}, \mathrm{s}}\left(\mathrm{p}_{n} \rightarrow \mathrm{p}_{n-1}\right)+L_{\mathrm{e}, 1}\left(\mathrm{p}_{\mathrm{n}} \rightarrow \mathrm{p}_{\mathrm{n}-1}\right)\right) T\left(\overline{\mathrm{p}}_{n}\right) \mathrm{d} A\left(\mathrm{p}_{2}\right) \cdots \mathrm{d} A\left(\mathrm{p}_{n}\right) \\=& \int_{A^{n-1}} L_{\mathrm{e}, \mathrm{s}}\left(\mathrm{p}_{\mathrm{n}} \rightarrow \mathrm{p}_{\mathrm{n}-1}\right) T\left(\overline{\mathrm{p}}_{n}\right) \mathrm{d} A\left(\mathrm{p}_{2}\right) \cdots \mathrm{d} A\left(\mathrm{p}_{n}\right) \\&+\int_{A^{n-1}} L_{\mathrm{e}, \mathrm{l}}\left(\mathrm{p}_{\mathrm{n}} \rightarrow \mathrm{p}_{\mathrm{n}-1}\right) T\left(\overline{\mathrm{p}}_{n}\right) \mathrm{d} A\left(\mathrm{p}_{2}\right) \cdots \mathrm{d} A\left(\mathrm{p}_{n}\right) .\end{aligned}$$

根据不同大小的光源特性，上述两个积分可以使用完全不同的算法进行计算——只要它们互相忽略对方的贡献即可。

除此之外，还可以根据 BSDF 的特性进行分解，一种常见的算法就是将 BSDF 分为 delta 和非 delta 两类：

$$\begin{aligned}P\left(\overline{\mathrm{p}}_{n}\right)=\int_{A^{n-1}} L_{\mathrm{e}} &\left(\mathrm{p}_{\mathrm{n}} \rightarrow \mathrm{p}_{\mathrm{n}-1}\right) \\\times & \prod_{i=1}^{n-1}\left(f_{\Delta}\left(\mathrm{p}_{\mathrm{i}+1} \rightarrow \mathrm{p}_{\mathrm{i}} \rightarrow \mathrm{p}_{\mathrm{i}-1}\right)+f_{\neg \Delta}\left(\mathrm{p}_{\mathrm{i}+1} \rightarrow \mathrm{p}_{\mathrm{i}} \rightarrow \mathrm{p}_{\mathrm{i}-1}\right)\right) \\ & \times G\left(\mathrm{p}_{\mathrm{i}+1} \leftrightarrow \mathrm{p}_{\mathrm{i}}\right) \mathrm{d} A\left(\mathrm{p}_{2}\right) \cdots \mathrm{d} A\left(\mathrm{p}_{\mathrm{n}}\right)\end{aligned}$$

# 14.5 Path Tracing

路径追踪是图形学中的首个通用且无偏的蒙特卡洛光线传输算法。它通过不断地在散射发生的位置生成并跟踪光线，最终终止于光源，从而计算采样值。

## 14.5.1 总览

给定路径形式的 LTE ，我们的目标就是估计相机光线与场景几何体相交位置 $p_1$ 的出射辐照度。

$$L(p_1 \to p_0) = \sum_{i = 1}^\infty P(\bar{p}_i)$$

对于路径追踪而言，由于场景的物理性质，我们常常能够假定路径较短的光线往往会传输更多的能量。因此在路径追踪中我们通常会计算前数项，接着使用 Russian Roulette 方法粗略地估计后面的数项：

$$L \approx \sum_{i=1}^\infty \left(\left(\prod_{j=1}^i {1 \over 1 - q_j} \right)P(\bar{p}_i)\right)\\
q_1 = q_2 = q_3 = 0$$

## 14.5.2 采样路径

在有了以上指导的情况下，我们接下来需要考虑的是如何采样 $P(\bar{p}_i)$ ，在给定光线终点的情况下，我们需要在场景中采样 $i$ 个位置以形成完整的光路。这一节中描述了一种非常低效的采样算法，它在整个场景中按表面积等概率地均匀采样。因为过于低效，此处就不再详述。

## 14.5.3 增量地构建路径

最经典的路径追踪采样方法增量地构建光路。在每一个光路的顶点位置，使用 BSDF 采样生成新的光线方向以寻找下一个顶点。

由于 BSDF 的采样是定义在立体角上的，我们需要将基于面积积分的路径追踪公式改写为对立体角积分，而由于这一变化基本上就相当于前述的 $G$ 项的逆过程，因此过程中的绝大多数内容都被抵消掉了。对于单根光线，使用蒙特卡洛方法估计的贡献值如下：

$$\begin{gathered}\frac{L_{\mathrm{e}}\left(\mathrm{p}_{\mathrm{i}} \rightarrow \mathrm{p}_{\mathrm{i}-1}\right) f\left(\mathrm{p}_{\mathrm{i}} \rightarrow \mathrm{p}_{\mathrm{i}-1} \rightarrow \mathrm{p}_{\mathrm{i}-2}\right) G\left(\mathrm{p}_{\mathrm{i}} \leftrightarrow \mathrm{p}_{\mathrm{i}-1}\right)}{p_{A}\left(\mathrm{p}_{\mathrm{i}}\right)} \\\quad \times\left(\prod_{j=1}^{i-2} \frac{f\left(\mathrm{p}_{\mathrm{j}+1} \rightarrow \mathrm{p}_{\mathrm{j}} \rightarrow \mathrm{p}_{\mathrm{j}-1}\right)\left|\cos \theta_{j}\right|}{p_{\omega}\left(\mathrm{p}_{\mathrm{j}+1}-\mathrm{p}_{\mathrm{j}}\right)}\right)\end{gathered}$$

## 14.5.4 算法实现

在 `PathIntegrator` 中，从摄像机光线的交点开始，程序使用对应 `SurfaceInteraction` 的信息采样新的方向，生成并跟踪下一条光线。当由 Russian Roulette 或最大深度结束了本轮采样时，为了保证这条光路尽可能有效，会使用之前采样直接光照的方法采样从最后一个顶点接收到的辐照度。

为了记录光线追踪过程中的一系列状态，PBRT 记录了一系列参数。其中一个参数 `beta` 代表了当前光路下的 throughput 权重，将它与本位置接收到的辐照度估计值相乘即可得到摄像机处最终会获得的辐照度：

$$\beta=\prod_{j=1}^{i-2} \frac{f\left(\mathrm{p}_{\mathrm{j}+1} \rightarrow \mathrm{p}_{\mathrm{j}} \rightarrow \mathrm{p}_{\mathrm{j}-1}\right)\left|\cos \theta_{j}\right|}{p_{\omega}\left(\mathrm{p}_{\mathrm{j}+1}-\mathrm{p}_{\mathrm{j}}\right)}$$

`L` 则记录了在本条光路上所有长度辐照度累计值 $\sum P(\bar{p}_i)$ ，`ray` 保存了下一个需要追踪的光线信息，`specularBounce` 保存了当前位置的物体是否为镜面材质。

> 在 PBRT 出版后，源码中加入了另一个状态变量 `etaScale` 以调控在发生镜面折射的时候的 RR 概率值的变量，它在光线进入介质时减小为 $\eta^2$ ，出射时回到初始值 $1$ 。
> 

函数的主要工作均在一个循环中完成，在循环中，会完成以下内容：

1. 光线和场景求交以得到 `SurfaceInteraction` 
2. 在光线恰好击中光源时记入自发光项，或者在 miss 的情况下记入背景光
3. 如果没有找到相交或者已经达到最大深度则终止光线
4. 调用 `SurfaceInteraction::ComputeScatteringFunctions` 计算 BSDF 和 BSSRDF，并跳过空的介质分界面。
5. 使用上一节中的 `UniformSampleOneLight` 采样直接光源，并与当前的 `beta` 相乘累加到总辐照度中，代表了本长度的贡献值
6. 采样 BSDF 以得到新的光线
7. 如果有 BSSRDF 则需要计算次表面散射的情况，这将在下一节中讲解
8. 使用由 `beta` 和 `etaScale` 相乘得到的向量的最大值来确认 RR 的概率 `q = std::max((Float).05, 1 - (beta * etaScale).MaxComponentValue());` ，并应用 RR 方法