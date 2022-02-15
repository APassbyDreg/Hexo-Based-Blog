---
title: PBRT 第十五章笔记 | Notes for PBRT Chapter 15 - Light Transport II > Volume Rendering
date: 2021-12-27 01:46:20
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 15.1 The Equation of Transfer

传输函数是光线在介质中吸收、散射、发光的基本描述方程。它描述了光线在空间一点处沿某一方向出射的辐照度和其它各项的关系。

首先考虑 11 章中提到的散射函数：

$$L_s(p,\omega) = L_e(p,\omega) + \sigma_s(p,\omega)\int_{S^2}p(p,\omega, \omega')L_i(p,\omega')\mathrm{d}\omega'$$

这一函数描述了光线在一点位置上的变化，结合光线在同一方向上的变化就有：

$$\frac{\partial}{\partial t} L_{\mathrm{o}}\left(\mathrm{p}^{\prime}, \omega\right)=-\sigma_{\mathrm{t}}\left(\mathrm{p}^{\prime}, \omega\right) L_{\mathrm{i}}\left(\mathrm{p}^{\prime},-\omega\right)+L_{\mathrm{s}}\left(\mathrm{p}^{\prime}, \omega\right)$$

积分并将散射系数转换为透照率：

$$L_i(p,\omega) = T_r(p_o \to p)L_o(p_o,-\omega) + \int_0^t T_r(p' \to p)L_s(p',-\omega)\mathrm{d}t'$$

## 15.1.1 推广至路径追踪方程

回顾路径追踪中单路径的贡献函数：

$$\begin{aligned}P\left(\overline{\mathrm{p}}_{n}\right)=& \underbrace{\int_{A} \int_{A} \cdots \int_{A}}_{n-1} L_{\mathrm{e}}\left(\mathrm{p}_{n} \rightarrow \mathrm{p}_{n-1}\right) T(\overline{p}_n)\mathrm{d} A\left(\mathrm{p}_{2}\right) \cdots \mathrm{d} A\left(\mathrm{p}_{n}\right) \end{aligned}$$

其中透照率的定义：

$$
T\left(\overline{\mathrm{p}}_{n}\right)=\prod_{i=1}^{n-1} f\left(\mathrm{p}_{i+1} \rightarrow \mathrm{p}_{i} \rightarrow \mathrm{p}_{i-1}\right) G\left(\mathrm{p}_{i+1} \leftrightarrow \mathrm{p}_{i}\right)$$

从前，路径追踪的路径节点只需要考虑在物体表面上的位置，但对于引入了参与介质的情况下，我们需要加入包含空间中任意的点的路径。我们在定义路径 $P_n$ 的同时定义另一个数组 $c$ 来记录该路径点是在空间中还是在物体表面上：

$$\begin{gathered}\mathrm{P}_{n}^{\mathrm{c}}=\underset{i=1}{\times} \begin{cases}A, & \text { if } \mathbf{c}_{i}=0 \\V, & \text { if } \mathbf{c}_{i}=1\end{cases} \\\mathrm{P}_{n}=\bigcup_{\mathbf{c} \in\{0,1\}^{n}} \mathrm{P}_{n}^{\mathbf{c}} \end{gathered}$$

我们接着改写透照率的定义，这首先需要将定义在表面的 BSDF 扩展到空间中：

$$\begin{gathered}\hat{f}\left(\mathrm{p}_{i+1} \rightarrow \mathrm{p}_{i} \rightarrow \mathrm{p}_{i-1}\right)= \begin{cases}\sigma_{\mathrm{s}} p\left(\mathrm{p}_{i+1} \rightarrow \mathrm{p}_{i} \rightarrow \mathrm{p}_{i-1}\right), & \text { if } \mathrm{p}_{\mathrm{i}} \in V \\f\left(\mathrm{p}_{i+1} \rightarrow \mathrm{p}_{i} \rightarrow \mathrm{p}_{i-1}\right), & \text { if } \mathrm{p}_{\mathrm{i}} \in A \end{cases} \end{gathered}$$

接着改写对应的 $G$ 项，同时引入透照度，并在空间位置不考虑由面积微分转换的影响有：

$$\\\hat{G}\left(\mathrm{p} \leftrightarrow \mathrm{p}^{\prime}\right)=V\left(\mathrm{p} \leftrightarrow \mathrm{p}^{\prime}\right) T_{r}\left(\mathrm{p} \rightarrow \mathrm{p}^{\prime}\right) \frac{C_{\mathrm{p}}\left(\mathrm{p}, \mathrm{p}^{\prime}\right) C_{\mathrm{p}^{\prime}}\left(\mathrm{p}^{\prime}, \mathrm{p}\right)}{\left\|\mathrm{p}-\mathrm{p}^{\prime}\right\|^{2}} \\C_{\mathrm{p}}\left(\mathrm{p}, \mathrm{p}^{\prime}\right)= \begin{cases}\left|\mathbf{n}_{\mathrm{p}} \cdot \frac{\mathrm{p}-\mathrm{p}^{\prime}}{\left\|\mathrm{p}-\mathrm{p}^{\prime}\right\|}\right|, & \text { if } \mathrm{p} \text { is a surface vertex } \\1, & \text { otherwise }\end{cases}$$

即可得到新的透照率和路径追逐的一般形式。

# 15.2 Sampling Volume Scattering

为了在体积中进行采样，我们首先需要定义 `Medium::Sample()` 接口。他的目标是采样上文中提到的包含介质的传输函数：

$$L_i(p,\omega) = T_r(p_o \to p)L_o(p_o,-\omega) + \int_0^{t_{\max}} T_r(p' \to p)L_s(p',-\omega)\mathrm{d}t'$$

其中 $p_0 = p + t_{\max}$ 是某个表面上的一点（因为无穷远处的光最终会被介质消光至无任何影响），这一采样行为有两种可能：其一，没有在路径上采样到任何 interaction ，则应该计算和表面有关的那一项 $T_r(p_o \to p)L_o(p_o, -\omega)$ ，否则则需要在空间中的一点处生成一个 `MediumInteraction` ，并继续采样新的光线。

假设 $p_t(t)$ 定义了在点 $p + t\omega$ nn 位置采样的概率，则在表面采样的概率有：

$$p_{surf} = 1 - \int_0^{t_{\max}}p_t(t)\mathrm{d}t$$

利用这一概率就可以定义对应的 $\beta$ 为：

$$\beta_{surf} = {T_r(p \to p+t\omega) \over p_{surf}}\\
\beta_{med} = {\sigma_s(p+t\omega)T_r(p \to p+t\omega) \over p_t(t)}$$

## 15.2.1 Homogeneous Medium

在一个各向同性的简单介质中，由于其各种性质在空间中没有变化，其中唯一的复杂度仅仅在于要处理不同波长下的消光值。

在 13.3.1 中，曾经介绍了一个简单的指数透射率分布，其 PDF 为：

$$p_t(t) = \sigma_te^{-\sigma_t t}$$

可以得到如下的采样方法：

$$t = -{\ln(1 - \xi) \over \sigma_t}$$

但由于 $\sigma_t$ 可能在不同波长上有所差异，但我们也不能在光线上同时采样多个位置，最后的解决方案就是首先均匀地采样一个通道 $i$ ，再在这个通道上采样 $t$ 。使用这种方法计算得到的 PDF 是在各个通道上的 PDF 均值。

$$\hat{p}_t(t) = {1 \over n}\sum_{i=1}^n{\sigma^i_t}^{-\sigma_t^i t}\\
p_{surf} = {1 \over n}\sum_{i=1}^n{\sigma^i_t}^{-\sigma_t^i t_{\max}}$$

最后返回采样到的对应 $\beta$ 即可。

## 15.2.2 Heterogeneous Medium

对于在空间上介质特性分布不均匀的情况，如 `GridDensityMedium` ，我们往往需要额外的一些开销以对它进行采样。在这一情况下，$p_t(t)$ 不是一个常数了。

要对变化的 $\sigma_t$ 进行采样，最直观的方法是 ray marching 。通过沿着光线采样 $\sigma_t$ ，进而构建出对应的 PDF 以进行采样。但这一方法会引入系统性的 bias ，总而降低渲染的质量。

为了解决这一问题，PBRT 使用了原本是为了计算中子在原子反应堆中的散射现象的 delta tracking 算法。这一方法使用空白粒子将介质填充至拥有均匀的 $\sigma_t$，并在发生 interaction 时增加一步 validation 以剔除空白粒子，从而实现可变的介质密度。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/Medium%20tracking.svg" style="max-height: 20vh; margin: 10px 0"/></center>

首先，在 `GridDensityMedium` 中会预计算一个最大密度。

接着，在 `Sample` 方法中，则会按照这个最大密度采样 $t$ 。当在 $t_{\max}$ 内如果发生了 interaction ，则会接着判断该 interaction 是否是由真实粒子贡献的（通过使用 $U[0,1]$ 的随机样本与当前密度除以最大密度比较得到），如果不是，则在当前位置继续向前寻找 interaction ，直到找到或达到边界为止。由于不采样介质 interaction 的概率等于光线的透照率，因此对于 $\beta_{surf}$  我们直接返回 1.0 即可。另一方面，对于一个介质的 interaction ，返回的 $\beta_{med}$ 则应该等于该位置的光线散射比例 $\sigma_s / \sigma_t$ 。

另一个需要实现的内容是该类的透照率函数 `Tr()` 。有了上面的采样函数，我们可以简单地用一组随机数去无偏地估计它，即我们可以进行多次采样，并取没有采样到 interaction 的情况比例作为透照率。在实际实现中，PBRT 还将 `Sample` 中二次 validate 的概率直接作为 $Tr$ 的估计值使用，从而加速收敛。

## 15.2.3 采样 Phase Functions

在 PBRT 中，由于 phase functions 是由公式显示定义并且经过了归一化的，所以它默认对这个函数的采样的 PDF 是和函数值完全一致的。

通过对 Henyey–Greenstein 函数的分析有：

$$\phi = 2\pi\xi_1\\
\cos \theta=-\frac{1}{2 g}\left(1+g^{2}-\left(\frac{1-g^{2}}{1+g-2 g \xi_2}\right)^{2}\right)$$

特别地，当 $g=0$ 时该函数退化为一个均匀的球面采样，即 $\cos\theta = 1 - 2\xi_2$

# **15.3 Volumetric Light Transport**

在拥有了以上采样技术后，我们就可以实现 `EstimateDirect` 中加入对空间中的介质点的采样支持了。这和采样 BSDF 的过程是极其相似的，只不过将 $f$ 项替换为了对 phase function 的采样，并将 `scatteringPdf` 设置为对应的采样值即可。

## 15.3.1 路径追踪

`VolPathIntegrator` 是将体积纳入考虑的路径追踪积分器。这一实现与之前描述的路径追踪方法的最大不同在于，当光线处在介质中时，会对该介质调用一次 `Medium::Sample` 从而确认是否存在 medium interaction ，同时记录光线的透照率到 $\beta$ 上。如果找到了一个 MI ，则接下来会使用 `UniformSampleOneLight` 采样一个直接光照，并使用 phase function 采样并生成新的光线。反之则按照原来的方法计算。

# **15.4 Sampling Subsurface Reflection Functions** ⚠️

另一种渲染介质的方法是利用 BSSRDF 和一个物体表面表示介质，并对 BSSRDF 进行采样从而获得最终的着色值，它大大减少了对介质中的每一条路径都进行采样所带来的极高的复杂度（特别是对于 albedo 较大的介质而言，它们中的光路通常十分复杂）。本节中介绍了对它的采样方法。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/bssrdf-motivation.svg" style="max-height: 15vh; margin: 10px 0"/></center>

和 BSDF 只用采样一个方向不同的是，BSSRDF 的采样还需要在物体表面采样另一个位置。`BSSRDF::Sample_S()` 函数负责这一采样过程，它接收三个随机数（由一个 `float` 和一个 `Point2f` 组成），以采样一个新的 surface interaction ，并返回传输率。

## 15.4.1 采样 `SeparableBSSRDF`

`SeparableBSSRDF` 将 BSSRDF 分为了菲涅尔项、简化为与距离有关的 $S_p$ 项和与入射光角度有关的 $S_\omega$ 项。
$$
S(p_o,\omega_o,p_i,\omega_i) \approx (1 - F_r(\cos\theta_o))S_p(p_o,p_i)S_{\omega}(\omega_i)$$

`SeparableBSSRDF::Sample_S` 的函数会首先转发一个子函数 `SeparableBSSRDF::Sample_Sp` ，如果采样出来的结果不是黑色，则会初始化对应的 surface interaction 中的 BSDF 和 wo 信息。

由于 BSSRDF 中的 $S_\omega$ 项实际上是一个归一化的菲涅尔反射项，它实际上和一个 BSDF_DIFFUSE 的表现极其一致，并且可以使用 `BxDF::Sample_f` 进行采样。利用 PBRT 中的设计，我们可以直接在出射 SI 上添加这样一个实际上相当于封装了对应 BSSRDF 的 `Sample_Sw` 的 `SeparableBSSRDFAdapter` 以实现对出射方向的估计和采样。

另一方面，为了实现对空间项 $S_p$ 的采样，我们需要一种方法将二维的随机数映射到三维的场景几何体表面的点上。一种直观的想法是使用测地线进行采样，但这一方法并不具有普遍性，并需要对各个模型附加极大难度的不同的测量实现。为此，PBRT 使用了更简单的基于光线追踪的映射方法。

<center style="margin-bottom: 10px"><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/Subsurface%20sample%20radius.svg" style="max-height: 25vh; margin: 10px 0"/></center>

上图展示了这一方法的基本概念：基于出点 SI 提供的 $p_o, N_o$ 可以构建一个球体的切面，我们会在这个以 $p_o$ 为球心，被以 $n_o$ 为法线的切面上采样一个角度 $\phi$ 和一个半径 $r$ ，接着在该位置沿着出点法线的逆方向发射一条光线与几何体求交，从而得到入点的 SI 信息。

这一方法仍然具有多个缺点：

1. 径向的衰减项 $S_r$ 并不一定在所有波长上都是一致的，事实上在很多情况下各个通道的平均自由程是不一样的
2. 当 $n_o \cdot n_i \to 0$ 时，探测射线会与平面相交于一个较远的位置，从而提供一个非常差的低概率样本，进而增加结果的方差
3. 探测光线可能与物体多次相交，而其中的每个交点都会以不同的方式贡献能量

对于前两个问题，一种结合了多投影方向和多光谱通道的 MIS 方法被用于同时解决这两个问题。在 PBRT 中，对投影方向的选择不止限于法线的逆向，而是以 $2:1:1$ 的权重分别选择法线或是两个切线的反方向之一（因为另两个方向的探测光线更容易 miss 掉物体本身）。接着，会随机地采样一个颜色通道。

`SeperableBSSRDF` 接口提供了以下两个函数以采样一个半径，以及从对应的采样恢复 PDF ：

```cpp
virtual Float Sample_Sr(int ch, Float u) const = 0;
virtual Float Pdf_Sr(int ch, Float r) const = 0;
```

在一般情况下，BSSRDF 值会随着半径快速降低，我们对大多数距离较远的位置并不感兴趣，因此采样的范围被缩小到了距离圆心有限的范围 $r_{\max}$ 中。这一值是由使用固定的数值调用 `Sample_Sr(ch, 0.99)` 生成的，以保证其中包含了 $99.9%$ 的散射能量。

<center style="margin-bottom: 10px"><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/bssrdf-radius-to-length.svg" style="max-height: 25vh; margin: 10px 0"/></center>

接着，PBRT 会在 $r_{\max}$ 的球范围内按照给定的投影轴和采样位置生成一段光线（如上图中长为 $l$ 的线段），这根光线会和场景进行多次求交，并在与本物体相交时将交点信息保存在一个相交位置的链表中，直到走完整个线段为止：

```cpp
struct IntersectionChain {
		SurfaceInteraction si;
		IntersectionChain *next = nullptr;
};
IntersectionChain *chain = ARENA_ALLOC(arena, IntersectionChain)();
```

> 我记得 PBRT 里面应该有光线和单个物体相交的函数来着？
> 

最后，PBRT 会均匀且随机地使用其中一个交点作为最终的采样结果。这一选择和之前的投影轴选择以及通道的选择均有传入的第一个随机数完成。每次使用后随机数会被进一步缩放至满足 $U[0,1]$ 分布，对于这些简单地选择而言单一随机数的精度在绝大多数情况下都绰绰有余。

第二个需要考虑的函数是 `SeparableBSSRDF::Pdf_Sp()` 它会评估输入的 SI 在采样过程中的 PDF 。它主要分为三个步骤：

1. 使用定义在 $p_o$ 的局部坐标系表示 $p_i - p_o$
2. 将这半径投影到另外两个坐标系上得到 `rProj`
3. 遍历所有的 `3 * Spectrum::nSamples` 种不同的采样组合并计算对应的 PDF ，最终取加权均值

## 15.4.2 采样 TabulatedBSSRDF ⚠️

上一节中，我们讨论了对 BSSRDF 的采样，其中没有设计的唯二函数是 `Sample_Sr, Pdf_Sr` 。本节中将说明它们在 `TabulatedBSSRDF` 中的实现。

和此前的 `FourierBSDF` 的采样类似，它的采样函数调用了同样的 `SampleCatmullRom2D` 以获得一个在单位圆上的半径采样，接着除以对应通道的 $\sigma_t$ 以得到最终的采样半径。

而 PDF 的计算则和之前一致，只需要将半径乘以对应通道的 $\sigma_t$ 就能获得一致的权重，接着调用前文中的接口计算 `sr` ，最后做一次 PDF 的重映射即可。

> 由于之前的章节中跳过了对傅里叶 BSDF 的采样部分，此处仅作简单描述
> 

## 15.4.3 路径追踪中的次表面散射

我们有：

$$L_{\mathrm{o}}\left(\mathrm{p}_{\mathrm{o}}, \omega_{\mathrm{o}}\right) \approx \frac{S\left(\mathrm{p}_{\mathrm{o}}, \omega_{\mathrm{o}}, \mathrm{p}_{\mathrm{i}}, \omega_{\mathrm{i}}\right)\left(L_{\mathrm{d}}\left(\mathrm{p}_{\mathrm{i}}, \omega_{\mathrm{i}}\right)+L_{\mathrm{i}}\left(\mathrm{p}_{\mathrm{i}}, \omega_{\mathrm{i}}\right)\right)\left|\cos \theta_{\mathrm{i}}\right|}{p\left(\mathrm{p}_{\mathrm{i}}\right) p\left(\omega_{\mathrm{i}}\right)}$$

特别地，对于一般的表面我们直接有：$p_i = p_o, p(p_i) = 1$

这意味着将 BSSRDF 整合入路径追踪之中非常简单，我们只需要在当生成的 interaction 中包含 BSSRDF 时改用对应的函数采样和求解光照即可。PBRT 中的 `PathIntegrator` 和 `VolPathIntegrator` 均支持次表面材质。

# 15.5 Subsurface Scattering Using the Diffusion Equation ⚠️

完成 BSSRDF 的采样和估计过程的最后一块拼图是 `SeperableBSSRDF::Sr(Float r)` 函数，它被广泛用于对 $S_p$ 的估计之中，贯穿整个采样和求值的过程。PBRT 中使用的方法基于 Habel 等人于 2013 年提出的光子束扩散技术 photon beam diffusion (PBD) 。为了高效地估计这一值，该技术做出了大量假设：

1. 光线在半透明物体中的分布是由扩散近似法建模的，描述了高度散射的材质中的光线稳态
2. 它需要散射属性在整个介质中是同质的
3. 它基于 Seperable BSSRDF 所做出的一切假设

在满足以上条件时，PBD 方法得到的结果和由路径追踪得到的结果可以做到非常相近。但大部分情况下，这些条件并不能都被满足，尤其是物体具有复杂的几何形状的时候。但即使一部分假设并不能得到满足，这一方法仍然可以生成视觉上过得去的结果。

零一方面，这一算法需要求与 $S_r$ 的 CDF 的逆，并完成其在球座标上的映射。但这通常并不能在 `TabulatedBSSRDF` 中得到一个解析解，因此 PBRT 会在构建场景时预计算包括 `radii, albedo` 等一系列数值并储存在表中。

## 15.5.1 相似性原理

为了将普适性的方程转换为扩散方程，接着用于估计求解次表面散射，需要使用到一系列的假设和转换。其中之一就是相似性原理（principal of similarity）。它认为，一个各向异性且具有高 albedo 的散射介质可以通过一个各向同性的 phase function 和适当修改的散射和吸光系数来表示。这主要是因为当散射次数逐渐增多时，散射光线的分布就会越来越趋向于均匀分布。Yanovitskij 定性了这一表示方法，他写出了 HG 分布函数在 n 次散射后的推广形式：

$$p(\omega \to \omega') = {1 - g^{2n} \over 4\pi(1 + g^{2n} - 2g|g^{n-1}|(-\omega \cdot \omega'))^{3 / 2}}$$

显然，当 $n \to \infty$ 时，只要 $g \neq \pm1$ ，这一方程会收敛到各向同性的 phase function $1 / 4\pi$ 上。这一原理将被应用于对 phase function 的简化上，同时会修改散射相关的系数：

$$\begin{aligned}
\sigma_s' &= (1 - g)\sigma_s\\ \sigma_t' &= \sigma_a + \sigma_s'\\ 
\rho' &= {\sigma_s' \over \sigma_t'}
\end{aligned}$$

对于这一近似的直观解释如下：当一个介质拥有 $g \to 1$ 时，这意味着在多次散射的过程中大部分情况下光线都会大致沿着原来的方向前进，造成一种类似低散射系数的效果。反之，当 $g \to -1$ 时，光线总是会在反射后射向相反的方向，造成的光路就如同进入了高散射系数的情况一样。下图展示了 $g = \pm0.9$ 的情况下的光路：

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/aniso-fwd-path.svg" style="max-height: 20vh; margin: 10px"/><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/aniso-backward-path.svg" style="max-height: 20vh; margin: 10px"/></center>

## 15.5.2 扩散理论

扩散理论提供了一种在同质且高散射的介质中简化传输方程的解决方法。它可以通过将上述修改后的系数带入传输方程中得到。我们首先考虑传输方程如下：

$$\begin{aligned}
\frac{\partial}{\partial t} L_{\mathrm{o}}\left(\mathrm{p}^{\prime}, \omega\right)=&-\sigma_{\mathrm{t}}\left(\mathrm{p}^{\prime}, \omega\right) L_{\mathrm{i}}\left(\mathrm{p}^{\prime},-\omega\right)\\
&+L_e(p,\omega) + \sigma_s(p,\omega)\int_{S^2}p(p,\omega, \omega')L_i(p,\omega')\mathrm{d}\omega'
\end{aligned}$$

将相似性原理应用于其上，同时简化 $L_o(p,\omega) = L_i(p,-\omega) = L(p, \omega)$ 之后有：

$$\frac{\partial}{\partial t} L(\mathrm{p}+t \omega, \omega)=-\sigma_{\mathrm{t}}^{\prime} L(\mathrm{p}, \omega)+\frac{\sigma_{\mathrm{s}}^{\prime}}{4 \pi} \int_{\mathrm{S}^{2}} L\left(\mathrm{p}, \omega^{\prime}\right) \mathrm{d} \omega^{\prime}+L_{\mathrm{e}}(\mathrm{p}, \omega)$$

扩散理论的核心假设在于，由于散射过程有效地对入射光进行了分散，其中来自入射位置沿角度分布的高频信息会迅速被混成一团。在高密度且同质的介质中，所有的光线方向性都会最终消解掉。因此，我们可以只使用在球面动量的二阶展开来表示辐照度方程。对于定义在球面上的函数 $f: S^2 \to R$ ，其 $n$ 阶动量的定义为：

$$\left(\mu_{n}[f]\right)_{i, j, k, \ldots}=\int_{S^{2}} \underbrace{\omega_{\mathrm{i}} \omega_{j} \omega_{k} \cdots}_{n \text { factors }} f(\omega) \mathrm{d} \omega .$$

也就是说，在三维的情况下，$k$ 阶动量就相当于一个大小为 $3^k$ 的常数 / 向量 / 矩阵 / 高维矩阵。其中 $i,j\cdots$ 位置上的元素就是由原始函数值和笛卡尔坐标系对应维度的值相乘而成的。特别地，零阶动量就相当于这一函数在球面上的均值，一阶动量则相当于一个表示质心的向量，二阶动量是一个正定的 $3 \times 3$ 矩阵。虽然更高阶的动量可以给我们提供更优秀的方向相关的特性重建结果，但此处我们只关心 $\leq 1$ 阶的动量以对原函数做一个展开操作。对于光照函数，我们有：

$$\begin{aligned}
\phi(p) &= \mu_0[L(p, \cdot)] &=& \int_{S^2}L(p,\omega)\mathrm{d}\omega\\
E(p) &= \mu_1[L(p, \cdot)] &=& \int_{S^2}\omega L(p,\omega)\mathrm{d}\omega&
\end{aligned}$$

展开式可以使用上述两阶动量展开为（以下 $L_d$ 表示了使用扩散方法计算的光照，而非直接光照）：

$$L(p,\omega) \approx L_{d}(p,\omega) = {1 \over 4\pi}\phi(p) + {3 \over 4\pi}\omega \cdot E(p)$$

在获得这一近似的下一步就是将这一近似应用于上述的传输方程之中。但很不幸的是，这一方程并不一定有解。但这一问题由一个小 trick 解决：在计算扩散方程时，我们只要求方程两边的各阶动量相等，即：

$$\begin{aligned}
\mu_i\left[\frac{\partial}{\partial t} L_{\mathrm{o}}\left(\mathrm{p}^{\prime}, \omega\right)\right]=\mu_i\bigg[&-\sigma_{\mathrm{t}}\left(\mathrm{p}^{\prime}, \omega\right) L_{\mathrm{i}}\left(\mathrm{p}^{\prime},-\omega\right)\\
&+L_e(p,\omega) + \sigma_s(p,\omega)\int_{S^2}p(p,\omega, \omega')L_i(p,\omega')\mathrm{d}\omega'\bigg]
\end{aligned}$$

通过化简上式，我们有：

$$\mathrm{div} E(p) = -\sigma_a\phi(p) + \mu_0[L_e(p, \cdot)]$$

其中 $\mathrm{div}E(p)$ 表示了 $E(p)$ 的散度：

$$\mathrm{div}E(p) = {\partial \over \partial x}E(p) + {\partial \over \partial y}E(p) + {\partial \over \partial z}E(p)$$

另一方面，从动量的定义我们又可得：

$${1 \over 3} \nabla \phi(p) = -\sigma'_tE(p) + \mu_1(L_e(p, \cdot))$$

我们接着做出另外一个假设：介质中的光源均匀地向所有方向发光，即 $\forall i \geq 1, \mu_i[L_e(p,\cdot)] = 0$ ，这又和之前的相似性假设类似。通过结合以上假设和方程，我们可得：

$$D\nabla^2\phi(p) - \sigma_a \phi(p) = - \mu_0[L_e]$$

其中 $\nabla^2 = \mathrm{div}\nabla$ 被称为拉普拉斯算子，常数 $D = 1 / (3\sigma_t)$ 项被称为 classical diffusion coefficient 。

有了上述的扩散方程，我们接着会从一个点光源和一块完全充满了无穷空间的介质开始分析，接着考虑多种方法以提高近似的精度。

## 15.5.3 Monopole Solution

我们首先由最简单的一种情况开始思考，考虑一块无限大的同质性介质，其空间的原点处有一个释放单位能量的点光源（monopole），即空间中的自发光项的分布为：

$$L_e(p,\omega) = {1 \over 4\pi}\delta(p)\\
\mu_0[L_e(p,\cdot)] = \delta(p)$$

将此式带入扩散方程可以得到一个简单的解析解：

$$\phi_M(r) = {1 \over 4\pi D}{e^{-\sigma_{tr}r} \over r}$$

其中的常数 $\sigma_{tr} = \sqrt{\sigma_a / D}$ 被称为 effective transport coefficient ，它贡献了主要的指数衰减项。这说明在扩散中光线衰减的速度和 $\sigma_t'$ 并不相同，$\sigma_{tr}$ 反而取决于介质的 albedo 。

接着，将这一公式带入动量的定义中，我们可以得到 $E$  项：

$$\begin{aligned}\mathbf{E}_{\mathrm{M}}(\mathrm{p}) &=-D \nabla \phi_{\mathrm{M}}(\mathrm{p}) \\&=\left[-D \frac{\partial}{\partial r} \phi_{\mathrm{M}}(r)\right] \widehat{\mathbf{r}} \\&=\frac{1+r \sigma_{\mathrm{tr}}}{4 \pi r^{2}} \mathrm{e}^{-\sigma_{\mathrm{tr}} r} \hat{\mathbf{r}}\end{aligned}$$

## 15.5.4 非典型扩散

虽然在上述 Monopole 的情况下我们可以得到一个有关通量的精确解，但它在底层的扩散性假设失效时相较于使用原始方法而言仍然具有较大的误差。特别地，有两个特殊情况：其一，对能量的吸收阻止了辐照度场达到稳态；其二则是在光源附近辐照度函数不能忽略介质的各向异性。

多年来，为了更加精确地在不同情况下进行近似，研究者们提出了多种改进方法。其中一个较为有效的是使用 Grosjean 在1956 年在中子传输领域提出的改进的 monopole 解：

$$\begin{gathered}
\phi_{\mathrm{G}}(r)=\frac{\mathrm{e}^{-\sigma_{\mathrm{t}}^{\prime} r}}{4 \pi r^{2}}+\tilde{\phi_{\mathrm{M}}}(r) \\
\tilde{\phi_{\mathrm{M}}}(r)=\rho^{\prime} \phi_{\mathrm{M}}(r)
\end{gathered}$$

第一部分是一个使用传统的辐射传输方法计算的一个消光系数以表示无散射情况下的通量（即单散射的部分），这有效地消除了不能被扩散理论解决的部分。而剩下的扩散（多散射）项表示了至少经过了一次散射的光线，它被一个 reduced albedo 项 $\rho'$ 缩放以体现额外的散射带来的能量损失。它还需要修改前文种使用的 diffusion coefficient $D$ 项如下：

$$D_G = {2\sigma_a + \sigma_s' \over 3(\sigma_a + \sigma_s')^2}$$

在低 albedo 的情况下，Grosjean 解（蓝线）相比上一节中的传统解（红线）更好地拟合了实际解（黑线）：

<center style="margin-bottom: 10px"><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/diffusion-classical.svg" style="max-height: 25vh; margin: 10px 0"/></center>

在接下来的内容中，我们将关注于 Grosjean 解中的后一个多次散射项，而前一半的消光项会在之后进行分离简化处理。

## 15.5.5 Dipole Solution

为了将扩散方程应用于次表面散射中，这一解必须考虑到分界面的存在。接下来，我们将使用一个分割平面拓展 monopole 的场景，以场景中的平面 $z = 0$ 为分界线，一面是介质，另一面则假定为真空。一个单位点光源被放置在介质中的 $z$ 轴上的点 $(0,0,z_r)$ 处。由于分界面的存在，一部分光线可以逃逸出介质，并不再进行散射，而另一部分光线则在分界面上发生镜面反射，重新回到介质中参与作用。

分界面的影响可以使用镜像法进行近似。我们假设在真空部分中的 $(0,0,z_v)$ 处有一个虚拟的光源，它与原始光源在分割面上的投影位置相同，但它在释放负的能量。这一假设让它吸收从原始光源出射的光线，剩余的光线则体现出反射的性质。由 $z_r$ 确认 $z_v$ 的值便是我们接下来需要探讨的内容。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/dipole-sources.svg" style="max-height: 10vh; margin: 10px 0"/></center>

由于扩散方程的线性性质，我们可以通过相加两个点光源的结果以得到它们的效果之和：

$$\phi_D(r) = \tilde{\phi_M}(d_r) - \tilde{\phi_M}(d_v)\\
E_D(r) = \tilde{E_M}(d_r) - \tilde{E_M}(d_v)\\
d_r = \sqrt{r^2+z_r^2}, \ d_v = \sqrt{r^2+z_v^2}$$

特别地，假设 $z_r>0,\ z_v<0$ 对于一阶动量 $E$ 在 $z$ 轴上的分量有：

$$(0,0,1) \cdot E_D(r) = \frac{1}{4 \pi}\left[\frac{z_{\mathrm{r}}\left(1+d_{\mathrm{r}} \sigma_{\mathrm{tr}}\right)}{d_{\mathrm{r}}^{3}} \mathrm{e}^{-\sigma_{\mathrm{tr}} d_{\mathrm{r}}}-\frac{z_{\mathrm{v}}\left(1+d_{\mathrm{v}} \sigma_{\mathrm{tr}}\right)}{d_{\mathrm{v}}^{3}} \mathrm{e}^{-\sigma_{\mathrm{tr}} d_{\mathrm{v}}}\right]$$

### 边界情况

在边界的真空一侧，由于没有任何散射现象的参与，散射过程会以平方反比的速度快速衰减。而当我们在边界面的位置使用扩散方程用到的一阶展开来近似这一过程的话，这个线性函数就会在某一位置 $z_e$ 变成零。这一位置对应到负光源的情况下就恰好是两个光源的连线中点：$z_v = 2 z_e - z_r$ 。一种对这一边界情况的近似 $z_e$ 如下：

$$z_e = -2D_G{1 + 3\bar{F}_{r,2}(\eta) \over 1 - 2\bar{F}_{r,1}(\eta)}$$

其中的 $\bar{F}_{r,1},\ \bar{F}_{r,2}$ 分别是 11 章中提到的菲涅尔动量。

### 出射辐照度

到了这一步，我们已经拥有了所有需要计算表面的出射光线的要素，通过将对应值写入扩散方程，我们可以得到介质内的光线值：

$$L_{d}(p,\omega) = {1 \over 4\pi}\phi(||p||) + {3 \over 4\pi}\omega \cdot E(||p||)$$

为了计算边界上的出射能量，我们需要使用菲涅尔项和 $\cos$ 项和这一函数在半球面上积分：

$$\begin{aligned}E_{\mathrm{d}}(\mathrm{p}) &=\int_{\mathrm{H}^{2}(\mathbf{n})}\left(1-F_{\mathrm{r}}\left(\eta^{-1}, \cos \theta\right)\right) L_{\mathrm{d}}(\mathrm{p}, \omega) \cos \theta \mathrm{d} \omega \\&=E_{\mathrm{d}, \phi_{\mathrm{D}}}(\|\mathrm{p}\|)+E_{\mathrm{d}, \mathrm{E}_{\mathrm{D}}}(\|\mathrm{p}\|) .\end{aligned}$$

通过线性性质将这两部分分别积分：

$$\begin{aligned}E_{\mathrm{d}, \phi_{\mathrm{D}}}(r) &=\int_{0}^{2 \pi} \int_{0}^{\frac{\pi}{2}}\left(1-F_{\mathrm{r}}\left(\eta^{-1}, \cos \theta\right)\right) \frac{1}{4 \pi} \phi_{\mathrm{D}}(r) \cos \theta \sin \theta \mathrm{d} \theta \mathrm{d} \phi \\&=\frac{1}{2} \phi_{\mathrm{D}}(r) \int_{0}^{\frac{\pi}{2}}\left(1-F_{\mathrm{r}}\left(\eta^{-1}, \cos \theta\right)\right) \cos \theta \sin \theta \mathrm{d} \theta \\&=\phi_{\mathrm{D}}(r)\left(\frac{1}{4}-\frac{1}{2} \bar{F}_{\mathrm{r}, 1}\right) \\E_{\mathrm{d}, \mathrm{E}_{\mathrm{D}}}(r) &=\int_{0}^{2 \pi} \int_{0}^{\frac{\pi}{2}}\left(1-F_{\mathrm{r}}\left(\eta^{-1}, \cos \theta\right)\right)\left(\frac{3}{4 \pi} \omega \cdot \mathbf{E}_{\mathrm{D}}(r)\right) \cos \theta \sin \theta \mathrm{d} \theta \mathrm{d} \phi \\&=\int_{0}^{\frac{\pi}{2}}\left(1-F_{\mathrm{r}}\left(\eta^{-1}, \cos \theta\right)\right)\left(\frac{3 \cos \theta}{2} \mathbf{n} \cdot \mathbf{E}_{\mathrm{D}}(r)\right) \cos \theta \sin \theta \mathrm{d} \theta \\&=\mathbf{n} \cdot \mathbf{E}_{\mathrm{D}}(r)\left(\frac{1}{2}-\frac{3}{2} \bar{F}_{\mathrm{r}, 2}\right) \cdot\end{aligned}$$

至此，我们就可以获得在单个位于 $(0,0,z_r)$ 位置的光源的情况下，在表面位置 $p$ 出射的总能量。我们会将这一值写作 $E(p, z_r)$ 以供接下来使用。

## 15.5.6 Beam Solution（多散射项）

对 dipole 模型而言，最后一个需要考虑的内容在于，那一个介质中的光源究竟应该被放在什么位置上。第一个在 CG 中应用的基于 dipole 的 BSSRDF 将光源放置在入射光的一倍平均自由程上 $z_r = 1 / \sigma'_t$ 。这一近似虽然有一点道理，但会在接近光源时产生极大的误差。

使用 PBD 方法，点光源的解需要在整个介质域内 $z_r\in[0,\infty)$ 积分，即：

$$E_d(p) = \int_0^{\infty}\sigma_s'e^{-\sigma_t'z_r}E_D(p, z_r)\mathrm{d}z_r$$

这一公式中的指数项代表了光线在介质中传播过程中的损失，它计算了一个垂直入射的单位辐照度的光线对于表面各点的影响。PBD 的一些其它变体还可以处理非垂直入射的情况，或者加快这一数值的估计，但此处我们不再深究。

在 PBRT 中，函数 `BeamDiffusionMS()` 使用介质参数 $\sigma_s,\sigma_a,g,\eta$ 和半径 $r$ ，根据我们上面的推导计算这一估计值，它会返回对指数项进行重要性采样 100 次的结果。

$$z_r = -{\ln(1 - \xi_i) \over \sigma_t'}$$

虽然对第二项进行分析可以提供更加准确的采样效果，但由于这一积分只会在预计算期间计算一次，我们并不特别关注这一估计值的性能表现。

```cpp
Float BeamDiffusionMS(Float sigma_s, Float sigma_a, Float g, Float eta,
                      Float r) {
    const int nSamples = 100;
    Float Ed = 0;
    // Precompute information for dipole integrand

    // Compute reduced scattering coefficients $\sigmaps, \sigmapt$ and albedo
    // $\rhop$
    Float sigmap_s = sigma_s * (1 - g);
    Float sigmap_t = sigma_a + sigmap_s;
    Float rhop = sigmap_s / sigmap_t;

    // Compute non-classical diffusion coefficient $D_\roman{G}$ using
    // Equation (15.24)
    Float D_g = (2 * sigma_a + sigmap_s) / (3 * sigmap_t * sigmap_t);

    // Compute effective transport coefficient $\sigmatr$ based on $D_\roman{G}$
    Float sigma_tr = std::sqrt(sigma_a / D_g);

    // Determine linear extrapolation distance $\depthextrapolation$ using
    // Equation (15.28)
    Float fm1 = FresnelMoment1(eta), fm2 = FresnelMoment2(eta);
    Float ze = -2 * D_g * (1 + 3 * fm2) / (1 - 2 * fm1);

    // Determine exitance scale factors using Equations (15.31) and (15.32)
    Float cPhi = .25f * (1 - 2 * fm1), cE = .5f * (1 - 3 * fm2);
    for (int i = 0; i < nSamples; ++i) {
        // Sample real point source depth $\depthreal$
        Float zr = -std::log(1 - (i + .5f) / nSamples) / sigmap_t;

        // Evaluate dipole integrand $E_{\roman{d}}$ at $\depthreal$ and add to
        // _Ed_
        Float zv = -zr + 2 * ze;
        Float dr = std::sqrt(r * r + zr * zr), dv = std::sqrt(r * r + zv * zv);

        // Compute dipole fluence rate $\dipole(r)$ using Equation (15.27)
        Float phiD = Inv4Pi / D_g * (std::exp(-sigma_tr * dr) / dr -
                                     std::exp(-sigma_tr * dv) / dv);

        // Compute dipole vector irradiance $-\N{}\cdot\dipoleE(r)$ using
        // Equation (15.27)
        Float EDn = Inv4Pi * (zr * (1 + sigma_tr * dr) *
                                  std::exp(-sigma_tr * dr) / (dr * dr * dr) -
                              zv * (1 + sigma_tr * dv) *
                                  std::exp(-sigma_tr * dv) / (dv * dv * dv));

        // Add contribution from dipole for depth $\depthreal$ to _Ed_
        Float E = phiD * cPhi + EDn * cE;
        Float kappa = 1 - std::exp(-2 * sigmap_t * (dr + zr));
        Ed += kappa * rhop * rhop * E;
    }
    return Ed / nSamples;
}
```

## 15.5.7 单散射项

我们曾在 15.5.4 中为了简化计算忽略了公式中第一项单散射部分的求解，这一节中我们就要对它进行补全。

单散射项在介质中接近光源的位置，即 $r \to 0$ 的地方贡献了不可忽略的影响，我们可以简单地通过对原始的传输方程计算这一参数。对于路径积分的情况有：

$$\begin{gathered}L_{\mathrm{ss}}\left(\mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right)=\widehat{P}\left(\overline{\mathrm{p}}_{2}\right)=\int_{\mathrm{P}_{1}} L_{\mathrm{e}}\left(\mathrm{p}_{2} \rightarrow \mathrm{p}_{1}\right) \hat{T}\left(\mathrm{p}_{0}, \mathrm{p}_{1}, \mathrm{p}_{2}\right) \mathrm{d} \mu_{1}\left(\mathrm{p}_{2}\right) \\L_{\mathrm{e}}(\mathrm{p}, \omega)=\delta\left(\mathrm{p}-\mathrm{p}_{\mathrm{i}}\right) \delta(\omega+\mathbf{n})\end{gathered}$$

它表示了从入点垂直入射进入介质的所有光线上的点形成的虚拟光源对出点经过一次散射的贡献值。我们接着对该项在所有体积内的点进行积分以得到出射的 irradiance ，有：

$$\begin{aligned}E_{\mathrm{SS}}\left(\mathrm{p}_{0}\right) &=\int_{\mathrm{P}_{1}} L_{\mathrm{SS}}\left(\mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right) \hat{G}\left(\mathrm{p}_{1} \leftrightarrow \mathrm{p}_{0}\right) \mathrm{d} \mu_{1}\left(\mathrm{p}_{1}\right) \\&=\int_{\mathrm{P}_{2}} L_{\mathrm{e}}\left(\mathrm{p}_{2} \rightarrow \mathrm{p}_{1}\right) \hat{T}\left(\mathrm{p}_{0}, \mathrm{p}_{1}, \mathrm{p}_{2}\right) \hat{G}\left(\mathrm{p}_{1} \leftrightarrow \mathrm{p}_{0}\right) \mathrm{d} \mu_{2}\left(\mathrm{p}_{1}, \mathrm{p}_{2}\right)\end{aligned}$$

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/Subsurface%20critical%20angle.svg" style="max-height: 15vh; margin: 10px 0"/></center>

如图所示，通过把 $L_e$ 带入式中，并展开传输率项 $\hat{T}$ 、和传输函数 $\hat{f}$ ，我们接着将它转换为关于入点距离 $t$ 的积分：

$$E_{ss}(p_o) = \int_0^\infty t^2\hat{G}(p_i \leftrightarrow (p_i-t\mathbf{n}))\sigma_sp(-\cos\theta_s)\hat{G}((p_i-t\mathbf{n})\leftrightarrow p_o)\mathrm{d}t$$

其中的 $t^2$ 项是由体积积分转换为对球坐标积分时引入的。

接着需要分析的是两个几何量 $\hat{G}$ ，基于对介质均匀性的假设，我们很容易的就有：

$$\begin{aligned}
\hat{G}(p_i \leftrightarrow (p_i-t\mathbf{n})) = & {e^{-\sigma_tt} \over t^2}\\
\hat{G}((p_i-t\mathbf{n}) \leftrightarrow p_o) = & {e^{-\sigma_td} \over d^2} |\cos\theta_o| = & {e^{-\sigma_td} \over d^2} |\cos\theta_s|
\end{aligned}$$

需要注意的是，在单散射项中我们使用的是原始的 $\sigma_t$ ，而非经过扩散假设变换后的那一个。这是因为在这一情况下各向异性的介质也是比较容易处理的。

最后，我们引入出射位置的菲涅尔项，既有：

$$E_{\mathrm{SS}, F_{\mathrm{r}}}\left(\mathrm{p}_{\mathrm{o}}\right)=\int_{0}^{\infty} \frac{\sigma_{\mathrm{s}} \mathrm{e}^{-\sigma_{\mathrm{t}}(t+d)}}{d^{2}} p\left(\cos \theta_{\mathrm{s}}\right)\left(1-F_{\mathrm{r}}\left(\eta, \cos \theta_{\mathrm{o}}\right)\right)\left|\cos \theta_{\mathrm{o}}\right| \mathrm{d} t$$

特别地，对于相对折射率 $\eta > 1$ 的情况下，我们还可以将全反射考虑在内，从而缩小需要积分的区域为 $(t_{crit}, \infty)$ ，使用几何光学方法易得：

$$t_{crit} = r \sqrt{\eta^2 - 1}$$

和多散项一样，我们同样只对指数衰减项做重要性采样，从而有：

$$t_i = t_{crit} - {\ln(1 - \xi_i) \over \sigma_t}\\
\mathrm{PDF}(t) = \sigma_te^{-\sigma_t(t-t_{crit})}$$

和计算多散项相似的，`BeamDiffusionSS` 负责返回对这一值的估计。

```cpp
Float BeamDiffusionSS(Float sigma_s, Float sigma_a, Float g, Float eta,
                      Float r) {
    // Compute material parameters and minimum $t$ below the critical angle
    Float sigma_t = sigma_a + sigma_s, rho = sigma_s / sigma_t;
    Float tCrit = r * std::sqrt(eta * eta - 1);
    Float Ess = 0;
    const int nSamples = 100;
    for (int i = 0; i < nSamples; ++i) {
        // Evaluate single scattering integrand and add to _Ess_
        Float ti = tCrit - std::log(1 - (i + .5f) / nSamples) / sigma_t;

        // Determine length $d$ of connecting segment and $\cos\theta_\roman{o}$
        Float d = std::sqrt(r * r + ti * ti);
        Float cosThetaO = ti / d;

        // Add contribution of single scattering at depth $t$
        Ess += rho * std::exp(-sigma_t * (d + tCrit)) / (d * d) *
               PhaseHG(cosThetaO, g) * (1 - FrDielectric(-cosThetaO, 1, eta)) *
               std::abs(cosThetaO);
    }
    return Ess / nSamples;
}
```

## 15.5.8 填充 BSSRDFTable

函数 `ComputeBeamDiffusionBSSRDF()` 负责根据给定的 $g, \eta$ 项生成一个预计算的 BSSRDFTable 。由于 $S_r$ 是根据半径的指数递减的，在储存表格时采样位置也会以指数储存：

```cpp
// Choose radius values of the diffusion profile discretization
t->radiusSamples[0] = 0;
t->radiusSamples[1] = 2.5e-3f;
for (int i = 2; i < t->nRadiusSamples; ++i)
    t->radiusSamples[i] = t->radiusSamples[i - 1] * 1.2f;
```

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_II_Volume_Rendering/effective-albedo.svg" style="max-height: 20vh; margin: 10px 0"/></center>

上图展示了散射定义的 albedo 和多次散射实际上产生的 albedo 之间的差异。这种非线性的关系对于艺术家调整参数时是非常不友好的，因此我们一般会使用 $\rho_{eff}$ 作为参数。而在 BSSRDFTable 的储存上我们则会以非线性的间隔放置 albedo ：

$$\rho_i = {1 - e^{-8i/(N-1)} \over 1 - e^{-8}}$$

接着上述的计算单次散射和多次散射的函数会被用于计算 `rho, rhoEff` 。

```cpp
// Compute scattering profile for chosen albedo $\rho$
for (int j = 0; j < t->nRadiusSamples; ++j) {
    Float rho = t->rhoSamples[i], r = t->radiusSamples[j];
    t->profile[i * t->nRadiusSamples + j] =
        2 * Pi * r * (BeamDiffusionSS(rho, 1 - rho, g, eta, r) +
                      BeamDiffusionMS(rho, 1 - rho, g, eta, r));
}
// Compute effective albedo $\rho_{\roman{eff}}$ and CDF for importance sampling
t->rhoEff[i] =
    IntegrateCatmullRom(t->nRadiusSamples, t->radiusSamples.get(),
                        &t->profile[i * t->nRadiusSamples],
                        &t->profileCDF[i * t->nRadiusSamples]);
```

其中 `IntegrateCatmullRom` 的细节此处暂且略过。

## 15.5.9 设置散射参数

在散射中，设置参数 $\sigma_s, \sigma_a$ 的过程是极其不直观的，调整这两个参数以获得期望的视觉表现的过程极其复杂且非线性。本节中定义了一个更加简单的工具函数 `SubsurfaceFromDiffuse` ，它被用于 `KdSubsurfaceMaterial` 中以解决参数不直观的问题。

它的输入只有两个参数：$\rho_{eff}$ 和平均自由程的长度。这一函数会根据这两个参数逆推出 $\rho,\ \sigma_s,\ \sigma_t$ 等参数。 

```cpp
void SubsurfaceFromDiffuse(const BSSRDFTable &t, const Spectrum &rhoEff,
        const Spectrum &mfp, Spectrum *sigma_a, Spectrum *sigma_s) {
    for (int c = 0; c < Spectrum::nSamples; ++c) {
        Float rho = InvertCatmullRom(t.nRhoSamples, t.rhoSamples.get(),
                                     t.rhoEff.get(), rhoEff[c]);
        (*sigma_s)[c] = rho / mfp[c];
        (*sigma_a)[c] = (1 - rho) / mfp[c];
    }
}
```