---
title: PBRT 第十二章笔记 | Notes for PBRT Chapter 12 - Light Sources
date: 2021-12-07 19:54:28
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

若要使场景中的物体可见，场景中必定需要有向外辐射光子的光源。本节将首先讨论不同的发光物理过程，并引入基础的 `Light` 抽象类。紧接着讲解几种常用的光源类型。

# 12.1 Light Emission

所有非处在绝对零度的物体中均有运动的原子。而根据麦克斯韦定理，运动的电荷会向四周辐射电磁波。大部分室温下的物体辐射的波长均在红外波段，而辐射可见光波段的物体则需要具有更高的温度。当前市面上常见的物理光源有以下几种：

- 白炽灯：它使用一根钨丝通过大量电流产生热量从而发光。其发光的颜色（即能量在各个波长上的分布）取决于钨丝的温度。钨丝的外侧通常被一层磨砂玻璃包裹以吸收特定波长的光线，从而获得需要的 SPD 。白炽灯的大部分能量均集中在红外波段，也就是说输入它的能量大部分转换为了热量而非光能。
- 卤素灯：这种灯相当于在白炽灯的真空中填充了卤素气体。在普通白炽灯中，钨丝会在加热过程中逐渐升华、进而沉积在灯泡内壁上使得灯泡变暗。卤素灯中的卤素气体能通过将大部分气化的钨重新附着回灯丝上而延长灯泡的寿命。
- 气体放电灯：这种灯在气体中直接施加电流，使得这些气体根据原子的性质释放出特定波长的光线。在灯管的内侧常常涂有荧光材料以吸收有害的紫外线，并将能量转化到更多的波长范围上。
- LED 灯：这一类灯基于电激发光的原理制造，它使用在通电时释放光子的特殊材料制作。

对于气体放电灯和 LED 灯具，其相通的底层物理原理均是电子与原子撞击时产生的外层电子层级跃迁现象。当外层电子回到低能级时就会将能量以光能释放。

对于物理光源，我们可以定义它的光转换效率：

$$\int\Phi_e(\lambda)V(\lambda)\mathrm{d}\lambda \over \int\Phi_i(\lambda)\mathrm{d}\lambda$$

式中的 $V$ 代表 5.4.3 中提到的观测者的光谱响应曲线，表示接收者对不同波段的光的敏感程度。另一方面，该式的分子部分也可以使用光具使用的总能量代替。它的量纲是 $\mathrm{lm / W}$ ，以白炽灯为例，它的转换率大约是 $15 \ \mathrm{lm / W}$ 。

## 12.1.1 黑体辐射

黑体是一种完美的辐射体，它会吸收所有的能量，并只根据自身的温度发射电磁波。黑体发出的辐射 SPD 可以用一个关于波长和温度的公式描述，根据普朗克定律：

$$L_e(\lambda,T) = {2hc^2 \over \lambda^5(e^{hc/\lambda k_b T} - 1)}$$

其中 $h$ 是普朗克常数，$c$ 是光速，$k_b$ 是玻尔兹曼常数 $1.3806488 \times 10^{-23} \mathrm{J/K}$

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/blackbody-L-vs-lambda.svg" style="max-height: 18vh; margin: 10px 0"/></center>

玻尔兹曼公式给出了黑体上任意一点处的全频段辐出功率（单位是 $\mathrm{W/m^2}$）：

$$M(p) = \sigma T^4, \ \sigma = 5.67032 \times 10^{-8} Wm^{-2}K^{-4}$$

从黑体辐射公式我们也能很容易地知道能量最高的频率：

$$\lambda_{max} = {b \over T}, \ b = 2.897721 \times 10^{-3} \mathrm{mK}$$

而对于非黑体而言，根据基尔荷夫公式可得：对于任何达到了热平衡的物体，给定其出射的方向和温度，其 SPD 应该正比于黑体辐射，且比例与该方向上的半球反照率有关。表示的公式如下：

$$L_e'(T,\lambda,\omega) = L_e(T,\lambda)(1 - \rho_{hx}(\omega))$$

黑体辐射可以定义发光体的色温。如果某一发光体的 SPD 与某一温度 $T$ 下的黑体辐射相似 ，我们可以称其色温为 $T$ 。一般而言，卤素灯和白炽灯的色温一般在 $2700 \sim 3000 \mathrm{K}$ 之间，而荧光灯的色温可以达到 $3000 \sim 6000 \mathrm{K}$ 。人们可以通过色温高低区分颜色的冷暖。

## 12.1.2 Standard Illuminants

另一种用于区分发光能量分布的方式是将 SPD 与 CIE 标准光源的 SPD 作为代表，并归类到 $A$ 至 $F$  的其中一类的标准发光体上。

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/cie-a.svg" style="max-height: 18vh; margin: 10px 0"/></center>

Standard Illuminant A 于 1931 年引入，它最初的目的是为了体现大部分白炽灯的发光性能，因此设为了当时 $2850\mathrm{K}$ 的黑体辐射 SPD ，随着技术进步，数值精度的提高，现代的 Standard Illuminant A 相当于 $2856\mathrm{K}$ 的黑体辐射。

Standard Illuminant B 和 C 原本用于建模一天中的两个时段的天光，但目前已经不再使用。

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/cie-d6500.svg" style="max-height: 18vh; margin: 10px 0"/></center>

Standard Illuminant D 则描述了天光的不同形态。它由一系列有关天光 SPD 的向量分析得出，具有一个固定部分和两个可变加权部分。两个可变部分分别代表了一个在黄蓝色域上的云量变量以及一个在粉绿色域上的大气含水量变量。特别地，D65 标准是一个类 6504K 的黑体辐射 SPD ，它表示了欧洲正午十分的光线。

Standard Illuminant E 表示了一个常数 SPD ，一般仅被用于和其它组分比较。

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/cie-f4-f9.svg" style="max-height: 18vh; margin: 10px 0"/></center>

Standard Illuminant F 则表示了一系列不同的荧光 SPD ，它们的定义来自于对一系列常用荧光灯的 SPD 测量值。

# 12.2 Light Interface

```cpp
class Light {
  public:
    // Light Interface
    virtual ~Light();
    Light(int flags, const Transform &LightToWorld,
          const MediumInterface &mediumInterface, int nSamples = 1);
    virtual Spectrum Sample_Li(const Interaction &ref, const Point2f &u,
                               Vector3f *wi, Float *pdf,
                               VisibilityTester *vis) const = 0;
    virtual Spectrum Power() const = 0;
    virtual void Preprocess(const Scene &scene) {}
    virtual Spectrum Le(const RayDifferential &r) const;
    virtual Float Pdf_Li(const Interaction &ref, const Vector3f &wi) const = 0;
    virtual Spectrum Sample_Le(const Point2f &u1, const Point2f &u2, Float time,
                               Ray *ray, Normal3f *nLight, Float *pdfPos,
                               Float *pdfDir) const = 0;
    virtual void Pdf_Le(const Ray &ray, const Normal3f &nLight, Float *pdfPos,
                        Float *pdfDir) const = 0;

    // Light Public Data
    const int flags;
    const int nSamples;
    const MediumInterface mediumInterface;

  protected:
    // Light Protected Data
    const Transform LightToWorld, WorldToLight;
};
```

所有的 `Light` 派生类均含有以下四个变量：

1. `flags` 变量表示了基础的光源类型，其中包括了面光源、点光源、方向光源等。蒙特卡洛算法需要根据光源的不同类型做出不同的计算
2. 一组表示了光源与世界坐标互相转化的变换，光源一般都定义在光源坐标的原点，并使用统一的方向
3. 一个表示了光源所在介质的 `MediumInterface` 
4. `nSamples` 变量表示了面光源使用的采样数量

对光源而言，最重要的接口是 `Sample_Li()` 函数。它接受一个代表了目标点的世界坐标的 `Interaction` 参数，并返回在没有遮挡的情况下、从光源到达该位置的 radiance 量。值得注意的是，PBRT 中的光源并不支持动画，所有光源必须是静止的。

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/Light%20Sample%20Li.svg" style="max-height: 20vh; margin: 10px 0"/></center>

`Sample_Li()` 还负责初始化光源到着色点位置的射线 $\omega_i$ 、以及使用阴影光线测试光源是否可见的 `VisibilityTester` 对象（虽然当返回值为黑色时这一对象不会被使用）。

另一个所有光源类需要实现的接口是返回其释放的所有能量之和的 `Power()` 函数。这一值对于一些光线传输算法（如找到贡献度最高的光源并计算阴影）而言具有一定意义，但它并不需要返回一个完全精确的值。

最后，所有光源还具有一个预处理接口，用于在渲染前使用场景信息事先生成一部分数据以供其后使用。

## 12.2.1 可见性测试

`VisibilityTester` 是一个闭包——它包裹了一部分数据和一部分需要完成的计算内容。将这一部分单独抽象出来使得光源类可以在无遮挡的假设下返回信息，而让 `Integrator` 决定该信息是否被使用。

这个类中储存了两个 `Interaction` 数据，代表了一条 shadow ray 的两端。`Integrator` 接下来可以使用任意一个函数 `Unoccluded()` 或 `Tr()` 来获得这条 shadow ray 上面的光线传输量。

第一个接口仅返回一个布尔值代表光源和着色点之间是否没有遮挡，而第二个接口则会沿途调用所有介质的 `Tr()` 接口，返回最终的传输率。

# 12.3 Point Lights

## 12.3.1 点光源

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/dragon-point.png" style="max-height: 40vh; margin: 10px 0"/></center>

最基础的点光源向各个方向均匀地释放能量，它的位置固定在灯光坐标系的原点位置。描述点光源的物理量是一个常量 `intensity` ，表示单位立体角上从这个光源释放的能量。它的光源类型被设置为 `LightFlags::DeltaPosition` 。为了减少重复的变换计算，它储存了一个预计算量 `pLight` 表示点光源在世界坐标下的位置。

对于其 `Sample_Li` 接口，通过简单地将 intensity 除以距离的平方即可得到对应的 radiance ，而 `Power` 接口则可以由立体角积分得到解析解 $\Phi = \int_S^2I\mathrm{d}\omega = 4\pi I$

## 12.3.2 聚光灯

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/dragon-spot.png" style="max-height: 40vh; margin: 10px 0"/></center>

与点光源向所有方向发射能量不同，聚光灯指向光线坐标的 $+z$ 方向的一个锥体内发射光线。这个锥体由两个角度 `totalWidth` 和 `falloffStart` 定义如下：

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/Spotlight%20angles.svg" style="max-height: 15vh; margin: 10px 0"/></center>

聚光灯 `Sample_Li` 的接口实际上就相当于将点光源乘以一个衰减项：光线从 `falloffStart` 角度开始衰减，直到 `totalWidth` 位置最终变为零。`SpotLight` 类型中负责计算这一衰减值的函数是 `Falloff()` 。

衰减函数的计算流程也并不困难：首先计算光线方向与聚光灯方向的 $\cos$ 夹角，再将这个夹角与 `cosTotalWidth, cosFalloffStart` 比较。若光线在衰减范围内，则使用一个四次方 $\cos$ 的插值作为衰减值，这部分代码如下：

```cpp
// Compute falloff inside spotlight cone
Float delta = (cosTheta - cosTotalWidth) /
              (cosFalloffStart - cosTotalWidth);
return (delta * delta) * (delta * delta);
```

给定锥体角度，其张成的立体角度为 $2\pi(1 - \cos\theta)$ ，由于 `Power` 并不要求对衰减区域内的能量求精确解，我们可以直接使用一个无衰减的、立体角度 $\cos$ 为 `cosTotalWidth, cosFalloffStart` 的中值的圆锥体替代求解，即：

```cpp
Spectrum SpotLight::Power() const {
    return I * 2 * Pi * (1 - .5f * (cosFalloffStart + cosTotalWidth));
}
```

## 12.3.3 材质投影光源

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/dragon-projection.png" style="max-height: 40vh; margin: 10px 0"/></center>

这类材质接收一个二维的图像作为映射，从一点按给定的 fov 投影到一个锥体上。

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/Projection%20light%20setting.svg" style="max-height: 16vh; margin: 10px 0"/></center>

与透视相机类似地，材质投影光源也会计算一个投影矩阵。除此之外，他还会计算从中点到角点这一夹角的余弦值以确定包裹了这一投影的最小圆锥体。

在 `Sample_Li()` 的过程中，首先会调用投影矩阵获得着色点在材质上的 uv 位置，接着从材质上采样对应位置（PBRT 在这里并没有使用到偏导数纠正采样）。最后将颜色值乘以 intensity 并除以距离的平方即可得到 radiance 。

对于光线释放的总能量，由于 mipmap 的最高层级以将计算过整体材质颜色值的均值，我们直接通过对最高层查询即可得到它。接着会使用之前的最小包裹锥体的立体角替代方形锥体的立体角计算总能量。需要注意的是，这一近似在投影长宽比接近方形时才比较准确，否则可能带来较大的误差。

## 12.3.4 Goniophotometric Diagram Lights

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/dragon-gonio.png" style="max-height: 40vh; margin: 10px 0"/></center>

这类光源使用一张球形贴图包裹点光源，使用对应方向上采样的亮度值模拟点光源发光的方向性，在采样光线时，根据光线的方向计算出球坐标的 uv ，将贴图采样值与 intensity 相乘、最后除以距离的平方即可得到 radiance 。在计算能量时，也只需要简单地将贴图的均值乘以点光源的能量即可。

# 12.4 Distant Lights

这种光源放置在极其遥远的位置，因此光线的方向近乎平行，又被称为方向光源。由于其距离极其遥远，我们通常认为它必定是在真空中，而不经过介质计算。但当采样光线时，这类光源需要知道场景的边界，以从场景的边界外的某一点出发采样光线，从而可以对场景内的介质做出反应。因此，`DistantLight::Preprocess` 接口会记录场景的包围球，并写入自身的成员变量中。

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/DistantLight%20shadow%20ray.svg" style="max-height: 21vh; margin: 10px 0"/></center>

对光源的采样而言，我们只需要初始化一个从着色点开始，长度为场景包围球直径的 shadow ray ，并直接返回本光源对应的 radiance 即可。

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/DistantLight%20approx%20power.svg" style="max-height: 12vh; margin: 10px 0"/></center>

当我们需要计算这类光源释放的总能量时，我们会将场景的包围球投影到与光线垂直的平面上，通过对 radiance 的积分得到总能量 $2\pi r^2 \times L$

# 12.5 Area Lights

面光源是使用至少一个几何体定义的发光表面。表面上的每一点又可能拥有各自的方向性辐射能量分布。总的来说，对面光源求解到达着色点的能量需要在整个光源上进行积分，但这实际上是几乎不可能做到的。高复杂度带来的是柔和的、更加贴近现实表现的软阴影，而不是像之前的光源一样的硬边界。

<center><img src="https://pbr-book.org/3ed-2018/Light_Sources/area-scene-wide.png" style="max-height: 40vh; margin: 10px 0"/></center>

`AreaLight` 是继承于 `Light` 类的一个抽象类，它提供了一个新的接口 `AreaLight::L()` ，给定表面上的一点和出射方向，本接口会返回该条光线上的 radiance 。`SurfaceInteraction::Le()` 便调用了这一方法计算自发光的 radiance 。

`DiffuseAreaLight` 实现了一种面光源的一般形态，它向四周均匀地发射光线。它使用一个 `Shape` 指针指向发光的表面，并在法线正半球均匀发出光线。由于其发光的均匀性，在实现上述 `L()` 接口时只需要简单地测试出射光线是否在出射位置的法线正半球内即可。

计算面光源的 `Sample_Li()` 较为困难，这一部分将在 14 章中详细描述。

而 `DiffuseAreaLight` 的总能量则可以通过将表面的每个点视为一个点光源，并在表面上积分得到：$\Phi = L \times S\pi$

# 12.6 Infinite Area Lights

在一些情况下，我们需要一些来自无穷远的，包围整个场景的面积无穷大的光源（如天光等环境光照）。一种常用的表现方法时使用一个经纬度 radiance （又称 equirectangular projection ）贴图表示从各个方位而来的光线 radiance 。

与 `DistantLight` 类似，`InfiniteAreaLight` 也需要在预计算期间获得场景的包围球。由于其需要在一整块区域上采样光源，采样期间也需要蒙特卡洛方法的帮助。接下来会在 14 章中详细描述 PDF 的预计算以及光线采样的内容。

计算这类光源的总能量的方法和平行光类似，它同样使用场景的球面近似，近似方法为：$\Phi = \bar{L} \times \pi r_{scene}^2$

由于这种光源同样会对没有击中物体的光线产生影响，在 `Light` 抽象类中还有另外一个接口 `Le()` 用于处理没有发生相交的光线。