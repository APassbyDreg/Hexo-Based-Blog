---
title: PBRT 第五章笔记 | Notes for PBRT Chapter 05 - Color and Radiometry
date: 2021-11-02 21:44:45
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 5.1 Spectral Representation

真实世界中的物体的 SPD（spectral power distribution，光谱能量分布）可能十分复杂。在渲染器中需要一种紧凑、高效和准确的方式来表现这些函数。在实践中却不得不做出一些牺牲。

研究这些问题的一般框架在于找到一些优质的基函数来表示 SPD 。基函数可以将无穷维的 SPD 映射到离散的包含数个参数的表示方法上。

## 5.1.1 Spectrum 类型

在 pbrt 中，光照信息被保存在 `Spectrum` 类型中。这个类并没有被定义，而是用一个 `typedef` 重定向至了最常用了 `RGBSpectrum` 类上。

## 5.1.2 CoefficientSpectrum 类型

```cpp
template <int nSpectrumSamples> class CoefficientSpectrum {
public:
    /* ... */
		static const int nSamples = nSpectrumSamples;
protected:
    Float c[nSpectrumSamples];
};
```

这个类型展示了一种使用参数定义的光谱类型。它是一个模板类，可以适配各种不同的参数的数量。其 public 的函数中除了重载了各类运算符外，还包括了一些常用的数学和测试函数如 `Clamp, Lerp, HasNaNs` 等等。这个类型基于以下假设：

- 采样的基函数对于不同的输入的响应是线性的
- 各个基函数之间不会互相影响

# 5.2 The SampledSpectrum Class

`SampledSpectrum` 类型继承自 `CoefficientSpectrum` 类，它将一段波长范围之内分割为等长的不同区间，使用一个采样值代替各个区间之内的能量分布。默认情况下，该类会在 $[400 {\rm nm}, 700 {\rm nm})$ 的范围内采样 60 个区间来表示光谱上的能量分布。

这个类型除了继承自基类的函数外，还提供了包括初始化为统一值、使用已有样本初始化等方法。

## 5.2.1 XYZ Color

基于人眼感受颜色的生物学机理，可以只用三个浮点数来表示光谱从而得到与人眼收到的信息相似的结果。设三个分量上对于不同波长的权重函数为 $X(\lambda), Y(\lambda), Z(\lambda)$ ，则从 SPD 中计算出三个对应的浮点数的公式为：

$$\begin{aligned}&x_{\lambda}=\int_{\lambda} S(\lambda) X(\lambda) \mathrm{d} \lambda \\&y_{\lambda}=\int_{\lambda} S(\lambda) Y(\lambda) \mathrm{d} \lambda \\&z_{\lambda}=\int_{\lambda} S(\lambda) Z(\lambda) \mathrm{d} \lambda\end{aligned}$$

CIE 标准中定义的这三条权重函数如下所示，它们是从一系列基于人类视觉的试验中总结出的经验值，代表了人眼中不同的细胞对于不同颜色的敏感度。

<center><img src="https://pbr-book.org/3ed-2018/Color_and_Radiometry/matching-xyz.svg" style="max-height: 30vh; margin: 10px 0"/></center>

`SampledSpectrum` 类同样提供了将 SPD 转化为这三个参数的方法

## 5.2.2 RGB Color

上文中的 XYZ 颜色的表示方式是基于人眼感受功能的，而 RGB 颜色则是基于显示器显示方式的角度构建出来的。下图展示了 LCD 和 LED 显示器的三种显示颜色的实际 SPD 分布：

<center><img src="https://pbr-book.org/3ed-2018/Color_and_Radiometry/lcd-display-spd.svg" style="max-height: 30vh; margin: 10px 0"/></center>

将人眼感受值 XYZ 颜色转化为用于显示的 RGB 颜色需要乘以一个预计算的矩阵：

$$\left[\begin{array}{l}r \\g \\b\end{array}\right]=\left(\begin{array}{ccc}\int R(\lambda) X(\lambda) \mathrm{d} \lambda & \int R(\lambda) Y(\lambda) \mathrm{d} \lambda & \int R(\lambda) Z(\lambda) \mathrm{d} \lambda \\\int G(\lambda) X(\lambda) \mathrm{d} \lambda & \int G(\lambda) Y(\lambda) \mathrm{d} \lambda & \int G(\lambda) Z(\lambda) \mathrm{d} \lambda \\\int B(\lambda) X(\lambda) \mathrm{d} \lambda & \int B(\lambda) Y(\lambda) \mathrm{d} \lambda & \int B(\lambda) Z(\lambda) \mathrm{d} \lambda\end{array}\right)\left[\begin{array}{c}x_{\lambda} \\y_{\lambda} \\z_{\lambda}\end{array}\right]$$

PBRT 中也提供了不同表示方法之间互相转化的方法。特别地，从 RGB 颜色转换为 `SampledSpectrum` 的采样值时，由于维度的减少使得大量光谱信息在转换的过程中丢失，而原始的 RGB 颜色的 SPD 函数的变化又实在太大，直接重建会导致 `SampledSpectrum` 中的采样值函数十分不光滑。PBRT 使用了 Smits 等人在 1999 年发表的文章 An RGB-to-spectrum conversion for reflectances 中使用的方法从而得到一个较为平滑的转换。

# 5.3 RGBSpectrum Implementation

有关 RGB 颜色的讨论在上一节中已经提到，`RGBSpectrum` 类继承于只有三个采样值的 `SampledSpectrum` 基类，它实现了上节中的部分于 RGB 颜色互相转换的函数。

RGB 表示法是目前最常用的表示方式，默认情况下，PBRT 使用它作为 SPD 的表示法。

# 5.4 Radiometry

光也是电磁波的一种。在 PBRT 中，为了简化计算的复杂度，对光线的行为做出了以下假设：

1. 线性响应：系统对两个不同的输入之和产生的输出等于它们分开的输出之和
2. 能量守恒：在散射过程中，出射的能量永远小于等于入射值
3. 无极性、无散射或衍射、不同波长之间互相独立
4. 稳定状态：在环境中的光场被认为是已经达到了平衡状态

## 5.4.1 基础量纲

### Energy

单位是 $J$ ，单个光子的能量表示为：

$$Q = {hc \over \lambda}$$

### Flux

单位时间内接收或释放的光能，单位是 $J/s$ 或 $W$，定义为：

$$\Phi = \lim_{\Delta t \to 0} {\Delta Q \over \Delta t} = {\mathrm{d} Q \over \mathrm{d} t}$$

在时间上积分这个量即可得到总能量。

### Irradiance

单位面积上单位时间接收或释放的光能，单位是 $W/m^2$ ，定义为：

<center><img src="https://pbr-book.org/3ed-2018/Color_and_Radiometry/Lamberts%20Law.svg" style="max-height: 25vh; margin: 10px 0"/></center>

需要注意的是，这里的面积指的是垂直于光照方向的面积，当光线并非垂直于表面入射时接收到光线的面积时，由于实际受光范围变大，计算 irradiance 时需要乘以一个 $\cos(\theta)$ 项，其中 $\theta$ 代表了入射方向于法线的夹角。

### Intensity

intensity 描述了固定功率的光源在单位立体角上的能量分布情况。其中立体角可以通过将物体投影到单位圆上求面积得到。intensity 的单位是 $W/sr$ ，定义为：

$$E(p) = \lim_{\Delta A \to 0} {\Delta \Phi \over \Delta A^{\perp}} = {\mathrm{d} \Phi \over \mathrm{d}A^{\perp}}$$

### Radiance

radiance 描述了在单位立体角单位面积上单位时间内接收到的能量。你可以理解为：光源相对受光位置的方向为 $\omega$ 立体角为 $\mathrm{d}\omega$ ，受光位置的面积为 $\mathrm{d}A$ ，在单位时间内的能量传输。其计算方式为：

$$L(p, \omega) = \lim_{\Delta \omega \to 0} {\Delta E_\omega(p) \over \Delta \omega} = {\mathrm{d} E_\omega(p) \over \mathrm{d}\omega} = {\mathrm{d}\Phi \over \mathrm{d}\omega\mathrm{d}A^{\perp}}$$

<center><img src="https://pbr-book.org/3ed-2018/Color_and_Radiometry/Radiance.svg" style="max-height: 25vh; margin: 10px 0"/></center>

radiance 是全书中最常用的物理量，因为它在一条光线的传输过程中是一个不变的量，不会随着距离等因素变化。

## 5.4.2 出射和入射光线函数

由于在 PBRT 中法线规定了一个表面的正向和反向，因此需要对表面上的某处的受光也做出正向反向的区分。

$$\begin{aligned}&L^{+}(\mathrm{p}, \omega)=\lim _{t \rightarrow 0^{+}} L\left(\mathrm{p}+t \mathbf{n}_{\mathrm{p}}, \omega\right) \\&L^{-}(\mathrm{p}, \omega)=\lim _{t \rightarrow 0^{-}} L\left(\mathrm{p}+t \mathbf{n}_{\mathrm{p}}, \omega\right)\end{aligned}$$

另一个需要注意的内容是，无论是出射还是入射光线的表示中，光线的起点均为该表面上的点。也就是说，对于入射光线的方向，实际上是从受光点指向光源的向量，而不是一般而言认为的从光源发出的向量。

<center><img src="https://pbr-book.org/3ed-2018/Color_and_Radiometry/Incident%20outgoing%20radiance.svg" style="max-height: 30vh; margin: 10px 0"/></center>

结合两种定义即可得到出射光和入射光的表达：

$$\begin{aligned}&L_{\mathrm{i}}(\mathrm{p}, \omega)= \begin{cases}L^{+}(\mathrm{p},-\omega), & \omega \cdot \mathbf{n}_{\mathrm{p}}>0 \\L^{-}(\mathrm{p},-\omega), & \omega \cdot \mathbf{n}_{\mathrm{p}}<0\end{cases} \\&L_{\mathrm{o}}(\mathrm{p}, \omega)= \begin{cases}L^{+}(\mathrm{p}, \omega), & \omega \cdot \mathbf{n}_{\mathrm{p}}>0 \\L^{-}(\mathrm{p}, \omega), & \omega \cdot \mathbf{n}_{\mathrm{p}}<0\end{cases}\end{aligned}$$

特别的，对于空间中没有介质的位置有：

$$L_{o}(\mathrm{p}, \omega)=L_{\mathrm{i}}(\mathrm{p},-\omega)=L(\mathrm{p}, \omega)$$

# 5.5 Working with Radiometric Integrals

本节中提供了一些计算某一点处的 Irradiance 的积分公式与技巧。原始的定义如下：

$$E(p, n) = \int_{\Omega}L_i(p, \omega)|\cos\theta|\mathrm{d}\omega$$

## 5.5.1 使用投影固体角积分

使用固体角积分可以简化掉上面公式中的 $\cos$ 项，由于有 $\mathrm{d}\omega^{\perp} = |\cos\theta|\mathrm{d}\omega$ ，上述公式可以转换为：

$$E(p, n) = \int_{H^2(n)}L_i(p, \omega)\mathrm{d}\omega^{\perp}$$

## 5.5.2 在球坐标系下积分

<center><img src="https://pbr-book.org/3ed-2018/Color_and_Radiometry/Sin%20dtheta%20dphi.svg" style="max-height: 30vh; margin: 10px 0"/></center>

为了将固体角转换为在坐标系上的积分，需要进行以下映射：

$$\mathrm{d}\omega = \sin\theta\mathrm{d}\theta\mathrm{d}\phi$$

因此在法线半球的积分就可以写成：

$$E(p, n) = \int_0^{2\pi}\mathrm{d}\phi\int_0^{\pi/2} L_i(p, \theta,\phi)\cos\theta\sin\theta\mathrm{d}\theta$$

## 5.5.3 在面积上积分

<center><img src="https://pbr-book.org/3ed-2018/Color_and_Radiometry/Differential%20solid%20angle%20of%20dA.svg" style="max-height: 30vh; margin: 10px 0"/></center>

在现实情况中，常常需要对一个面光源积分。想从使用几何体表示的面光源获得面积容易、获得前述的立体角之类的信息却十分困难。此时在面积上积分会带来更多好处。转换面积积分和立体角积分的方式是：

$$\mathrm{d}\omega = {\rm{d}A\cos\theta \over r^2}$$

其中 $\theta$ 是该面的法线与光线的夹角。转换后的积分变成了：

$$E(p, n) = \int_A L\cos\theta_i {\rm{d}A\cos\theta_o \over r^2}$$

# 5.6 Surface Reflection

当一束光射入某个表面并发生反射，我们通常使用反射光在不同方向上的光谱分布来描述它的光线传输过程。对于半透明材质而言，描述光线的传输将变得更为复杂。对于皮肤、牛奶等材质，一束光可能从一点进入并从另一个位置射出。

在 PBRT 中使用了 BRDF 和 BSSRDF 来描述这些过程。

## 5.6.1 Bidirectional Scattering Distribution Function *(*BSDF)

<center><img src="https://pbr-book.org/3ed-2018/Color_and_Radiometry/BRDF.svg" style="max-height: 30vh; margin: 10px 0"/></center>

给定光线的入射方向、出射方向和表面上的一点，BSDF 描述了入射方向的光线 irradiance 传输到出射方向上的 radiance 比例。

$$f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right)=\frac{\mathrm{d} L_{\mathrm{o}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)}{\mathrm{d} E\left(\mathrm{p}, \omega_{\mathrm{i}}\right)}=\frac{\mathrm{d} L_{\mathrm{o}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)}{L_{\mathrm{i}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right) \cos \theta_{\mathrm{i}} \mathrm{d} \omega_{\mathrm{i}}}$$

基于物理的 BSDF 满足了两大性质：

1. 对称：交换入射和出射方向得到的值不变 $f(p, \omega_o, \omega_i) = f(p, \omega_i, \omega_o)$
2. 能量守恒：入射的能量永远大于等于出射的能量，即对入射方向积分有
   
    $$\int_{S^2}f(p, \omega_o, \omega')\cos\theta'\mathrm{d}\omega' \leq 1$$
    

BSDF 实际上由 BRDF 和 BTDF 组成，BRDF 描述了在法线半球上的传输信息，常用于各种不透明表面；而 BSDF 则描述了另一个半球上的传输信息，常用于各种折射表面。

使用 BSDF 计算出射 radiance 的方法只需在立体角上积分即可：

$$L_o(p, \omega_o) = \int_{S^2}f(p, \omega_o,\omega_i)L_i(p,\omega_i)|\cos\theta_i|\mathrm{d}\omega_i$$

## 5.6.2 Bidirectional Scattering Surface Reflectance Distribution Function (BSSRDF)

<center><img src="https://pbr-book.org/3ed-2018/Color_and_Radiometry/BSSRDF.svg" style="max-height: 30vh; margin: 10px 0"/></center>

由于 BSSRDF 在法线方向的另一侧通常在发生复杂的光线传输过程，我们假定在那些位置不会贡献额外的入射光，也就只需考虑法线侧的入射光。通过分离 BRDF 中的入射点和出射点可以得到 BSSRDF ，它并不考虑光线在介质内部经过的路径，只考虑最终的结论。使用 BSSRDF 得到出射 radiance 的方法需要在 BRDF 的基础上增加对面积的积分：

$$L_o(p, \omega_o) = \int_A{\rm dA}\int_{H^2(n)}f(p_i,p_o,\omega_o,\omega_i)L_i(p_i,\omega_i)|\cos\theta_i|\mathrm{d}\omega_i$$
