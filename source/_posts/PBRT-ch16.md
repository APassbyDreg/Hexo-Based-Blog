---
title: PBRT 第十六章笔记 | Notes for PBRT Chapter 16 - Light Transport III > Bidirectional Methods
date: 2022-02-15 17:29:46
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

在之前所提到的渲染方法中，均只考虑了从摄像机出发的光线，并仅在路径的终点处尝试寻找光源。本章则要讨论从光源和摄像机双向同时采样路径的方法。这些采样方法可以比单向的算法更高效地收敛。

从光源和从摄像机出发的光线，基于光路的可逆性，在散射过程中的物理和数学表示有极高的一致性。另一方面，他们之间又存在微妙但重要的不同。本章会首先探讨这一基础，接着介绍光子映射算法、双向路径追踪算法，并最终利用 Metropolis 采样方法进一步加速采样。

# 16.1 The Path-Space Measurement Equation

measurement equation（测量方程）描述了一种由积分一系列带有辐照度的光线得到的抽象的度量。例如在最简单的，最简化的成像模型中计算图像中的像素 $j$ 的情况下，我们可以把该像素的值写成在图像平面上积分的如下形式：

$$
\begin{aligned}I_{j} &=\int_{A_{\text {film }}} \int_{\mathrm{S}^{2}} W_{\mathrm{e}}^{(j)}\left(\mathrm{p}_{\text {film }}, \omega\right) L_{\mathrm{i}}\left(\mathrm{p}_{\text {film }}, \omega\right)|\cos \theta| \mathrm{d} \omega \mathrm{d} A\left(\mathrm{p}_{\text {film }}\right) \\&=\int_{A_{\text {film }}} \int_{A} W_{\mathrm{e}}^{(j)}\left(\mathrm{p}_{0} \rightarrow \mathrm{p}_{1}\right) L\left(\mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right) G\left(\mathrm{p}_{0} \leftrightarrow \mathrm{p}_{1}\right) \mathrm{d} A\left(\mathrm{p}_{1}\right) \mathrm{d} A\left(\mathrm{p}_{0}\right)\end{aligned}
$$

其中 $p_0$ 是胶片上的一点，$W_e^{(j)}(p_0 \to p_1)$ 项则表示了像素的滤波函数和用于选择出射位置的函数的乘积（在此情况下，出射位置遵循 delta 分布）：

$$
W_{\mathrm{e}}^{(j)}\left(\mathrm{p}_{0} \rightarrow \mathrm{p}_{1}\right)=f_{j}\left(\mathrm{p}_{0}\right) \delta\left(t\left(\mathrm{p}_{0}, \omega_{\text {camera }}\left(\mathrm{p}_{1}\right)\right)-\mathrm{p}_{1}\right)
$$

接着展开光线传输的 $L$ 项，有：

$$
\begin{aligned}I_{j} &=\int_{A_{\mathrm{film}}} &\int_{A} W_{\mathrm{e}}^{(j)}\left(\mathrm{p}_{0} \rightarrow \mathrm{p}_{1}\right) L\left(\mathrm{p}_{1} \rightarrow \mathrm{p}_{0}\right) G\left(\mathrm{p}_{0} \leftrightarrow \mathrm{p}_{1}\right) \mathrm{d} A\left(\mathrm{p}_{1}\right) \mathrm{d} A\left(\mathrm{p}_{0}\right) \\&=\sum_{i} &\int_{A} \int_{A} W_{\mathrm{e}}^{(j)}\left(\mathrm{p}_{0} \rightarrow \mathrm{p}_{1}\right) P\left(\overline{\mathrm{p}}_{i}\right) G\left(\mathrm{p}_{0} \leftrightarrow \mathrm{p}_{1}\right) \mathrm{d} A\left(\mathrm{p}_{1}\right) \mathrm{d} A\left(\mathrm{p}_{0}\right) \\&=\sum_{i} &\underbrace{\int_{A} \cdots \int_{A}}_{i+1 \text { times }} W_{\mathrm{e}}^{(j)}\left(\mathrm{p}_{0} \rightarrow \mathrm{p}_{1}\right) T\left(\overline{\mathrm{p}}_{i}\right) L_{\mathrm{e}}\left(\mathrm{p}_{\mathrm{i}+1} \rightarrow \mathrm{p}_{\mathrm{i}}\right) G\left(\mathrm{p}_{0} \leftrightarrow \mathrm{p}_{1}\right) \\&&\mathrm{d} A\left(\mathrm{p}_{\mathrm{i}+1}\right) \cdots \mathrm{d} A\left(\mathrm{p}_{0}\right) \end{aligned}
$$

注意到此处的光线出射函数 $L_e$ 和相机权重函数 $W_e$ 同时出现在了同一个方程的乘积中，它们均不需要任何的特殊处理，也因此我们意识到这两者在数学上是可交换的。这意味着我们可以从两种不同的视角看待渲染过程：光线可以从光源出发，经过散射最终到底传感器，并使用 $W_e$ 描述其贡献；或者也可以从摄像机出发，经过散射后到达光源位置，使用 $L_e$ 描述其贡献。

简单地交换相机和光源的位置，我们就能得到一种称为 particle tracing （粒子跟踪）的方法。它从光源发射粒子，并递归地在场景中散射，计算到达传感器的量。虽然作为一种渲染方法而言它并不实用，但却是各类双向方法的重要组成部分。

式中的 $W_e$ 描述了末段是 $p_1 \to p_0$  的光线对测量方程的重要性。包括图像生成在内的大部分测量方程都可以通过调整这一项得到对应的公式。

## 16.1.1 采样相机光线

双向光线传输算法需要可以在环境中任意点进行评估的重要性函数 $W_e$ ，函数 `Spectrum We(const Ray &ray, Point2f *pRaster2)` 函数通过传入一个光线以计算这一函数。第二个参数（如果提供了）则用于返回光栅位置。这一函数的默认实现会抛出一个错误，它仅在 `PerspectiveCamera` 类中有一个有效的重载。在这一函数中，会依次完成以下任务：

1. 计算光线和摄像机朝向的夹角，检查光线是否和摄像机朝向一致
2. 计算光栅位置，并按需写入参数指定的地址
3. 对于采样区域外的位置，返回零值
4. 计算重要性的函数值

`PerspectiveCamera` 类在定义上为了防止出现物理中的小孔成像的暗角问题，在 PDF 设计上加入了光线方向的影响。对于面积为 $A$ 的成像平面，其 PDF 定义为：

$$
p(\omega) = {d^2 \over A\cos^3\theta} ,\ \mathrm{if \ \omega \ is \ within \ frustum, \ else \ 0}
$$

更一般地，如果使用了非小孔的光圈，在 PDF 上还需要加上光圈上的采样位置的分布：

$$
p(\omega) = {d^2 \over A\pi r^2\cos^4\theta} ,\ \mathrm{if \ \omega \ is \ within \ frustum, \ else \ 0}
$$

当我们得到了这一分布函数后，对于 `Camera::GenerateRay` 函数就可以被改写为对应的重要性采样了。为此，一个计算方向和镜头光圈位置 PDF 的函数 `Pdf_We` 需要单独抽象出来。其计算过程和之前的重要性计算大同小异。最后一个统一的采样函数 `Sample_Wi` 和光源的 `Sample_Li` 相对应，它会根据给定的起始点，从镜头上采样一点以形成新的，并返回对应的 PDF 。

## 16.1.2 采样光源光线

对于双向光线传输而言，和 `Camera::GenerateRay` 类似地，还需要实现一个 `Sample_Le` 方法以完成对光源出射的射线的采样。该函数接收四个随机数，并返回采样光线、采样点的法线以及固体角和位置 PDF 。我们可以使用多重重要性采样的方法对它进行采样。除此之外，也有对应的返回两种 PDF 的 `Pdf_Le` 函数。

### 点光源

对点光源而言，直接均匀采样单位球面即可，其位置 PDF 为恒值 1 ，方向 PDF 同球面 PDF 。而给定光线计算 PDF 时，由于其 Delta 分布的特性，位置 PDF 为恒值 0。

### 聚光灯

和点光源类似，对于聚光灯而言只需要将均匀的球面采样替换为锥采样，并增加光线是否在锥内的判断即可。

### 面光源

由于 PBRT 中只考虑了漫反射的面光源，对这类光源进行采样的过程也相对简单。只需要先调用底层 `Shape` 的采样函数，接着对采样点的法线半球进行均匀采样即可。

### 方向光

回顾计算方向光的功率时的近似方法，对于方向光的采样，我们可以在场景的包围球的切面上均匀采样一点，并按光源方向生成光线。

<center style="margin-bottom: 10px"><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/envmap-sampling.svg" style="max-height: 25vh; margin: 10px 0"/></center>

## 环境光（Infinite Area Lights）

和方向光类似的，我们可以在场景的包围球上均匀（或重要性）采样一个方向，并发出光线。

## 16.1.3 非对称散射

在一些特殊的场景设置下，光线在部分材料上的散射行为是非对称的，如果不对这些情况进行特殊处理，双向光线传输算法就无法得到一致的结果。本节将简单地分析一系列可能导致非对称性的情况，并提出一些解决方案。

回顾 LTE 中的传输率项：

$$
T\left(\overline{\mathrm{p}}_{n}\right)=\prod_{i=1}^{n-1} f\left(\mathrm{p}_{i+1} \rightarrow \mathrm{p}_{i} \rightarrow \mathrm{p}_{i-1}\right) G\left(\mathrm{p}_{i+1} \leftrightarrow \mathrm{p}_{i}\right)
$$

此时我们引入伴随 BSDF ：$f^*(p, \omega_o, \omega_i) = f(p, \omega_i, \omega_o)$在

在 PBRT 中，大部分的 BSDF 均是对称的，即：$f = f^*$ 。但有关着色法线以及折射的过程需要额外的注意。枚举类 `TransportMode` 记录了当前的光线属性，它为表面提供了有关当前光线源头的信息，以便表面可以处理不同的情况。`Importance` 表示当前传输的是从光源发射的光线（接收 importance），`Radiance` 表示传输的是从相机发射的光线（接收 radiance ）

### 折射带来的非对称性

当光线从低 IOR 介质进入高 IOR 介质时，其能量会被压缩至更小的角度内。为此，光线的 radiance 会按比例增加：

$$
L_i = \frac{\eta_i^2}{\eta_t^2}L_t
$$

我们因此有：

$$
f^{*}\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right)=f\left(\mathrm{p}, \omega_{\mathrm{i}}, \omega_{\mathrm{o}}\right)=\frac{\eta_{\mathrm{t}}^{2}}{\eta_{\mathrm{i}}^{2}} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right)
$$

因此，当我们追踪来自光源的光线时，需要额外地乘以对应系数。这一点在 `SpecularTransmission` 、`FourierBSDF` 和 `SeperableBSSRFD` 中均需要额外处理。

### 着色法线带来的非对称性

着色法线用另一个法线覆盖了几何体本身的法线，以提供更加平滑、细节的表面效果。这种改变会导致底层着色模型的改变，常常在会产生一个非对称的 BSDF ，最终造成一系列包括不连续的表面着色等问题。考虑原始的 LTE ：

$$
L_{\mathrm{o}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)=L_{\mathrm{e}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)+\int_{\mathrm{S}^{2}} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L_{\mathrm{i}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\mathbf{n}_{\mathrm{g}} \cdot \omega_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}}
$$

其中的 $\cos$ 项以点乘的形式给出。为了加入表面法线的影响，我们将该项单独取出并入一个新的 BSDF ：

$$
f_{shade}(p, \omega_o, \omega_i) = {|\mathbf{n}_s \cdot \omega_i| \over |\mathbf{n}_g \cdot \omega_i|} f(p, \omega_o, \omega_i)\\
f_{shade}^*(p, \omega_o, \omega_i) = {|\mathbf{n}_s \cdot \omega_o| \over |\mathbf{n}_g \cdot \omega_o|} f^*(p, \omega_o, \omega_i)
$$

结合二者的差距有：

$$
f^*(p, \omega_o, \omega_i) = {|\mathbf{n}_s \cdot \omega_o||\mathbf{n}_g \cdot \omega_i| \over |\mathbf{n}_g \cdot \omega_o||\mathbf{n}_s \cdot \omega_i|}f(p, \omega_o, \omega_i), \mathrm{while \ transporting \ importance}
$$

# 16.2 Stochastic Progressive Photon Mapping (SPPM)

光子映射是 particle tracing 算法中的一种，它从光源开始构建光路，并最终连接相机和路径中的顶点以计算最终的 radiance 。

## 16.2.1 理论基础

一个 particle tracing 算法会在场景中生成一组 $N$ 个光照样本 $(p_j, \omega_j, \beta_j)$ 每个样本记录了来自方向 $\omega_j$ 入射 $p_j$ 的光线的传输率 $\beta_j$ 。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/photon-history.svg" style="max-height: 30vh; margin: 10px 0"/></center>

给定任意一个重要性函数 $W_e(p, \omega)$ ，我们希望上述样本的传输量参数 $\beta$ 满足：

$$
E\left[\frac{1}{N} \sum_{j=1}^{N} \beta_{j} W_{\mathrm{e}}\left(\mathrm{p}_{\mathrm{j}}, \omega_{\mathrm{j}}\right)\right]=\int_{A} \int_{\mathrm{S}^{2}} W_{\mathrm{e}}(\mathrm{p}, \omega) L_{\mathrm{i}}(\mathrm{p}, \omega)|\cos \theta| \mathrm{d} A \mathrm{~d} \omega
$$

使用 $p(p_{i,j})$ 表示选取某点在面积上的概率，使用蒙特卡洛方法展开右式如下：

$$
E\left[\frac{1}{N} \sum_{i=1}^{N} W_{\mathrm{e}}\left(\mathrm{p}_{\mathrm{i}, 0} \rightarrow \mathrm{p}_{\mathrm{i}, 1}\right)\left\{\frac{L\left(\mathrm{p}_{\mathrm{i}, 1} \rightarrow \mathrm{p}_{\mathrm{i}, 0}\right) G\left(\mathrm{p}_{\mathrm{i}, 0} \leftrightarrow \mathrm{p}_{\mathrm{i}, 1}\right)}{p\left(\mathrm{p}_{\mathrm{i}, 0}\right) p\left(\mathrm{p}_{\mathrm{i}, 1}\right)}\right\}\right]
$$

又因为 $E[ab] = E[aE[b]]$ ，我们继续将右侧部分展开为长度为 $n_i$ 的路径，并计入 Russian Roulette 终止概率 $q_{i,j}$ 的影响，最后有：

$$
\beta_{i, j}=\frac{L_{\mathrm{e}}\left(\mathrm{p}_{\mathrm{i}, \mathrm{n}_{\mathrm{i}}} \rightarrow \mathrm{p}_{\mathrm{i}, \mathrm{n}_{\mathrm{i}}-1}\right)}{p\left(\mathrm{p}_{\mathrm{i}, \mathrm{n}_{\mathrm{i}}}\right)} \prod_{j=1}^{n_{i}-1} \frac{1}{1-q_{i, j}} \frac{f\left(\mathrm{p}_{\mathrm{i}, \mathrm{j}+1} \rightarrow \mathrm{p}_{\mathrm{i}, \mathrm{j}} \rightarrow \mathrm{p}_{\mathrm{i}, \mathrm{j}-1}\right) G\left(\mathrm{p}_{\mathrm{i}, \mathrm{j}+1} \leftrightarrow \mathrm{p}_{\mathrm{i}, \mathrm{j}}\right)}{p\left(\mathrm{p}_{\mathrm{i}, \mathrm{j}}\right)}
$$

虽然 particle tracing 允许我们以任何方法生成一系列的采样，但最常用的方式还是从光源出发跟随表面散射的规律跟踪并记录沿途的状态。特别地，如果我们只需要评估一种已知的重要性测量函数，我们完全可以更加智能地进行采样，这是因为在默认情况下我们常常会生成大量无效的样本。另一方面，如果我们需要同时计算多种测量函数，那 particle tracing 的优势就在于可以只生成一次样本并在各种情况下复用即可。

## 16.2.2 光子映射（Photon Mapping）

光子映射算法的基础在于追踪进入场景的粒子，并使用模糊后的贡献值以近似它们在着色点的光照。我们一般称这种算法出射的粒子是光子。

为了计算沿某个位置出射的 radiance ，我们有：

$$
\begin{aligned}\int_{\mathrm{S}^{2}} & L_{\mathrm{i}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right) f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}} \\&=\int_{A} \int_{\mathrm{S}^{2}} \delta\left(\mathrm{p}-\mathrm{p}^{\prime}\right) L_{\mathrm{i}}\left(\mathrm{p}^{\prime}, \omega_{\mathrm{i}}\right) f\left(\mathrm{p}^{\prime}, \omega_{0}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}} \mathrm{d} A\left(\mathrm{p}^{\prime}\right)\end{aligned}
$$

即：

$$
W_e(p', \omega) = \delta(p' - p)f(p, \omega_o, \omega)
$$

由于其中含有一个 delta 分布函数，在概率上任何记录的粒子位置对该方程的贡献应该均为零。这时我们需要引入一点的 bias 以得到一个有用的关于着色点光照情况的近似。由于临近位置的光照和着色点处的光照常常具有相似性，光子映射方法会使用周围最近的光子，使用滤波函数进行插值以近似着色点的光照信息。这种方法允许同一个光子在不同位置贡献信息，这也是光子映射方法效率的来源。这种方法同样可以用于得到一些难以通过增量的路径重建算法得到的光路。一个无法使用路径追踪 / 双向路径追踪方法采样的例子如下：

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/impossible-path.svg" style="max-height: 20vh; margin: 10px 0"/></center>

考虑使用小孔相机拍摄使用点光源照亮的由一层折射介质和一层漫反射材质组成的场景。由于介质的存在，路径追踪无法从该场景中得到任何关于漫反射表面的直接光照，而因为光源和玻璃的折射都是 delta 分布的关系，任何玻璃上的出射方向也无法得到光照。即使使用了面光源，该场景的 variance 仍然会十分高。

有了光子映射，我们可以从光源发出光子，让它们穿过介质落在漫反射表面上，接着从摄像机发出光线，在击中表面时收集光子以得到良好的结果。

统计学中的密度估计课题为光子映射提供了评估光子贡献的方法。使用一个核函数 $\int_{-\infty}^{\infty}k(x)\mathrm{d}x = 1$ ，我们可以估计采样点 $x_i$ 周围的 $N$ 个样本：

$$
\hat{p}(x) = {1 \over Nh}\sum_{i=1}^N k({x - x_i \over h})
$$

其中的 h 称为窗口宽度，它是一个可调参数，用于控制分布估计的平滑程度。一个密度估计的例子如下：蓝点为采样点，经过虚线表示的核函数 $k(t) = \max(0, 0.75 * (1-0.25t^2) / \sqrt{5})$ 卷积后得到如图所示的估计。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/density-estimation-1d.svg" style="max-height: 20vh; margin: 10px 0"/></center>

这里的一个关键问题是 $h$ 的取值。过大或过小的 $h$ 都可能造成不同的问题。一种常用的方法是使用第 $N$ 个最近的相邻样本的距离适应性地选择 $h$ 的值，这样可以保证核函数的归一范围内永远有 $N$ 个样本。即：

$$
\hat{p}(x) = {1 \over Nd_N(x)}\sum_{i=1}^N k({x - x_i \over d_N(x)})
$$

扩展到高维情况下：

$$
\hat{p}(x) = {1 \over N(d_N(x))^d}\sum_{i=1}^N k({x - x_i \over d_N(x)})
$$

使用这一近似方法带入出射辐照度的公式，既有：

$$
L_{\mathrm{o}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right) \approx \frac{1}{N_{\mathrm{p}} d_{N}(\mathrm{p})^{2}} \sum_{j}^{N_{\mathrm{p}}} k\left(\frac{\mathrm{p}-\mathrm{p}_{j}}{d_{N}(\mathrm{p})}\right) \beta_{j} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{j}\right)
$$

这一方法引入的误差非常难以计算。一般而言，增加光子的数量通常都可以降低误差。具体到固定的位置，其误差往往和它附近光照变换的剧烈程度正相关。如果光子映射方法只用于间接光照，其带来的误差通常是可以接受的，因为间接光照大多都是低频的。

原始的光子映射算法基于两个步骤。光子首先被发射、跟踪、并记录在场景的表面上，储存在一个空间结构中（通常是 kd-tree ）；接着的第二个流程跟踪来自相机的光线，在路径上的每个位置统计周围的光子以得到简介光照。虽然这一方法是有效的，但它所能使用的光子数量受限于可用的内存。这是因为在第一个流程中的每一个光子都需要被储存下来，而与之相对的路径追踪在增加采样数时并不会增加任何空间开销。

### **Progressive Photon Mapping**

PPM 算法重构了光子映射的流程以解决这一问题：首先进行的相机 pass 向场景中追踪相机射线，在每个像素中储存所有非镜面的表面信息，这些信息被称为可视点（visible points）。接着的第二个 pass 则负责从光源跟踪光子，在每个光子与表面的作用位置向周围的可视点贡献照明。观察展开后的 LTE ：

$$
\begin{aligned}L\left(\mathrm{p}, \omega_{\mathrm{o}}\right) &=L_{\mathrm{e}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)+\int_{\mathrm{S}^{2}} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}} \\&=L_{\mathrm{e}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)+\int_{\mathrm{S}^{2}} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L_{\mathrm{d}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}} \\&+\int_{\mathrm{S}^{2}} f\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L_{\mathrm{i}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{\mathrm{i}}\right| \mathrm{d} \omega_{\mathrm{i}}\end{aligned}
$$

其中的 $L_e, L_d$ 部分都比较任意获得，而简介光照部分则有两种不同的处理方法：要么和路径追踪一样发射一条新的光线递归地计算，要么利用周围的光子信息近似简介光照。对于完美镜面和高度光滑的表面，一般会选择继续进行光线追踪，而对于漫反射表面而言则一般会使用光子信息。但也有算法会选择额外进行一次漫反射弹射，这样虽然会需要更多的摄像机光线，但可以有效消除光子映射带来的视觉影响。这些方法一般被称为 final gathering 。

使用 PPM 可以消除光子的存储消耗，将储存负担放到了可视点的存储上，而可以不设限制地发射光子。但在图像分辨率较高或者需要引入运动模糊的情况下仍然可能受到内存的限制。

### ****Stochastic Progressive Photon Mapping****

SPPM 在 PPM 的基础上作出了改进，使得光子映射算法不必受到相机光线采样的内存性能影响。和 PPM 一样，它会生成一系列的可视点记录，但不同的是每次生成的记录采样率都比较低，接着发射光子，记录数值，接着丢弃所有的现有采样点，重复这一步骤。

SPPM 对光子近似函数进行了两个修改，首先，它使用了一个固定的圆盘状核函数：

$$
L_o(p,\omega_o) \approx {1 \over N_p\pi r^2}\sum_j^{N_p}\beta_j f(p,\omega_o,\omega_j)
$$

第二个改动会在各个迭代之间通过计算得到的光子数量调整每个像素对应的可视点的核函数的半径。当一个可视点接收到的光子数量足够多时，我们有理由相信该位置可以接收到足够的光子以支持更加细节的采样。更改核函数的半径需要修改 radiance 的计算过程，对每个像素维护一系列状态量，其更新的方式如下：

$$
\begin{aligned}N_{i+1} &=N_{i}+\gamma M_{i} \\r_{i+1} &=r_{i} \sqrt{\frac{N_{i+1}}{N_{i}+M_{i}}} \\\tau_{i+1} &=\left(\tau_{i}+\Phi_{i}\right) \frac{r_{i+1}^{2}}{r_{i}^{2}}\end{aligned}
$$

其中 $N_i$ 是 $i$ 轮次后总的接收到的光子数量，$M_i$ 是 $i$ 轮次中着色点接收到的光子数量，$r_i$ 是 $i$ 轮次中使用的半径，$\tau_i$ 保存了光子的贡献值之和，并使用半径的平方缩放，$\Phi_i = \sum_j^{M_i}\beta_jf(p,\omega_o,\omega_j)$ 保存了该轮中的所有光子信息和 BSDF 的乘积之和。$\gamma$ 用于调整半径响应的速度，通常取 $2/3$ 左右。

## 16.2.3 SPPMIntegrator

`SPPMIntegrator` 不是一个 `SamplerIntegrator` ，因此它实现了自己的 `Render()` 方法。它会在初始化一系列相关变量后运行多次 SPPM 迭代。

SPPM 的像素数据储存在结构体 SPPMPixel 中，其中除了估计结果外还包含了一系列运行时变量和可视点的信息：

```cpp
struct SPPMPixel {
    Float radius = 0;
    Spectrum Ld;
    struct VisiblePoint {
        // VisiblePoint Public Methods
        VisiblePoint() {}
        VisiblePoint(const Point3f &p, const Vector3f &wo, const BSDF *bsdf,
                     const Spectrum &beta)
            : p(p), wo(wo), bsdf(bsdf), beta(beta) {}
        Point3f p;
        Vector3f wo;
        const BSDF *bsdf = nullptr;
        Spectrum beta;
    } vp;
    AtomicFloat Phi[Spectrum::nSamples];
    std::atomic<int> M;
    Float N = 0;
    Spectrum tau;
};
```

虽然我们可以通过生成带有不同权重的光子以加速收敛，但这在实现上较为复杂，且可能带来较为严重的视觉错误，因此一般的实现方法是生成空间上有一定分布的，权重相同的光子。我们一般会从更亮的光源发出更多的光子，更具体一点，会根据光源的功率按比例发射光子。

接着，PBRT 使用一个 HaltonSampler 生成相机光线，接着运行 SPPM 方法数轮以收集辐照度值。迭代的次数是由用户控制的。与路径追踪带来的噪声不同，采样不足的光子映射会产生点状的噪声。

和 `SamplerIntegrator` 相似，`SPPMIntegrator` 也将图像分割为 16 * 16 像素的子块并行地渲染。在完成一系列采样器设置、内存管理初始化后，会进入迭代环节，并按顺序完成以下环节：

1. 生成 SPPM 可视点
2. 跟踪光子并累积贡献度
3. 更新像素值，并周期性地将中间结果保存下来

## 16.2.4 生成可视点

和路径追踪的过程相似，这一步中会记录直接光照 $L_d$ ，并在第一个 diffuse 表面或最大深度位置的 glossy 表面生成可视点。可视点结构体记录了该位置的出射方向、BSDF、beta 值等一系列信息。

```cpp
struct VisiblePoint {
    <<VisiblePoint Public Methods>> 
    Point3f p;
    Vector3f wo;
    const BSDF *bsdf = nullptr;
    Spectrum beta;
} vp;
```

## 16.2.5 构建可视点网格结构

在生成了所有可视点后，为了让接下来的光子可以迅速找到临近的可视点，需要构建一个空间网格加速结构以加速这一过程。PBRT 的默认实现是使用一个均匀的网格覆盖所有可视点的 AABB ，每个网格内保存一个 SPPMPixel 的链表以表示可能产生影响的可视点（这是由于一个可视点可能覆盖多个网格，因此只储存指针即可）。由于整个网格中可视点的分布是十分稀疏的，可以使用一个最大 hashsize 为屏幕分辨率大小的哈希表来储存这些位置。

如果一个像素在当前迭代中没有生成可视点，那它的路径透过率 $\beta = 0$ ，这可能是由于路径射出了场景，或者是因为它被各种原因终止了。这样的可视点将不会参与到网格的构建中。而对于其他像素，则会构建一个宽度为 $r_i$ 的包围盒，并储存在与包围盒相交的网格体素中。因为每个可视点在不同轮次都可能有不同的搜索半径，如果我们只储存裸可视点而不将搜索半径计入考量，在计算时就会平添许多难度。

<center style="margin-bottom: 10px"><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/ppm-grid.svg" style="max-height: 25vh; margin: 10px 0"/></center>

计算网格的顺序如下：

1. 顺序遍历所有有效可视点，生成最小全覆盖网格的 AABB ，并记录最大搜索半径
2. 生成边长约为最大搜索半径的方形网格
3. 并行遍历所有有效可视点，将每个可视点按影响范围写入网格

由于对网格的操作是并行的，网格中的链表结构的操作需要是原子的。网格使用了一个基础的哈希函数，并且并不会处理冲突（两个冲突单元格的所有影响可视点会被合并为一个链表）

## 16.2.6 累积光子贡献

接下来的步骤是发射并跟踪光子的路程，这一步骤由多个进程并行运行，并使用一组和可视点生成不同的内存池。每次迭代中的光子数量的选择也是一个需要平衡的点，如果光子数量太多，那么一个像素的搜索半径无法有效地缩小，会在前期使用大量的过远的样本；反之则无法有效分摊前面步骤产生的额外开销，降低整体的效率。

为了生成分布更加均匀的采样，SPPM 使用 Halton 序列来生成光源的采样，并使用 `Sample_Le` 生成出射光线，其影响记为：

$$
\beta = {|\cos\omega_0|L_e(p_0,\omega_0) \over p(\mathrm{light})p(p_0,\omega_0)}
$$

接下来的过程就和路径追踪类似，跟踪光子进入场景进行弹射，每击中一点就通过查询网格找到对应的受影响可视点，并根据不断更新的 $\beta$ 计算贡献度。需要注意的是，光子的第一次弹射位置不会记录贡献，这是因为它代表的直接光照已经在前面的过程中计入考虑了。

为了提高效率，PBRT 使用了一种新的 RR 光线终止方案，它使用新 beta 与旧 beta 的亮度值之比作为终止概率，这样做会在反射率低的表面以更低的概率发射出权重更高的光线。

最后，在该轮中所有光子均完成追踪后，各个像素的 radiance 估计可以使用上述公式更新。同时更新的还有 `SPPMPixel` 中的若干状态量，最终的亮度值计算方法为：

$$
L = L_d + {\tau \over N_p\pi r^2}
$$

# 16.3 Bidirectional Path Tracing

在前文中提到的路径追踪方法是一个完全通用的光线追踪算法，它可以处理大量种类的几何体、光源和表面模型。然而在特别的场景设置下，这种算法的收敛速度可能会十分缓慢。例如下图中光源被几何体包裹而不可见的情况下，大多数光线都会浪费在无法找到光源之上。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/difficult-path.svg" style="max-height: 20vh; margin: 10px 0"/></center>

复杂的光源可以通过从光路的两端同时发出光线而提高效率，这就是 BDPT 的启发。这一算法是传统路径追踪的推广，而且和 SPPM 不同的是，这是一种无偏的算法。

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/bdpt-connection.svg" style="max-height: 20vh; margin: 10px 0"/></center>

BDPT 基于从摄像机和光源两个位置生成两个子路径，接着连接路径的端点从而生成一个完整的路径。从表面上看，这一过程和光子映射中的两个步骤相似度极高，但这两种方法实际上在构建和处理光路上有着巨大的差别。

为了提高 BDPT 的效率，一系列的优化方法包括了：

1. 重用子路径：给定一条光路 $q_0, q_1, \cdots, q_{s-1}, p_{t-1}, \cdots, p_1, p_0$ ，其中任意的子序列均可能构成新的有效光路。
2. 抛弃可以使用直接光照或直接重要性传输快速求解的路径第一个节点带来的光照
3. 使用不同的权重对使用不同策略生成的光路进行加权，对于同一条包含 $n$ 次散射的光路而言，一共有 $n+3$ 种不同的链接方法，其中部分策略可能比别的策略更加有效，这意味着 MIS 也可以被引入路径的构建中

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/bdpt-direct-connections.svg" style="max-height: 40vh; margin: 10px 0"/></center>

和 `SPPM`Integrator 一样，`BDPTIntegrator` 也直接继承自 `Integrator` 类。所有子路径生成均在 `BDPTIntegrator::Render()` 中经由一个嵌套循环完成，且过程也十分相似：

1. 细分图像为 $16 \times 16$ 的子块
2. 对每个子块生成内存管理模块和采样器
3. 对于每个像素，重复采样直到 `Sampler::StartNextSample()` 返回 `false`

对于 BDPT 而言，最重要的一步就是生成一系列的路径，并在其中试图找到光源和相机之间的一条通路。`Vertex` 类型储存了一个路径上的顶点信息，包括了光源和摄像机本身。在渲染的过程中会申请两个 `Vertex` 数组以分布储存从摄像机出发和从光源出发的不同子路径。接着 `GenerateCameraSubpath` 和 `GenerateLightSubpath` 方法负责生成这两条子路径。在这之后，一个嵌套的双层循环会尝试连接这两条路径上的所有顶点对。

```cpp
Spectrum L(0.f);
for (int t = 1; t <= nCamera; ++t) {
    for (int s = 0; s <= nLight; ++s) {
        int depth = t + s - 2;
        if ((s == 1 && t == 1) || depth < 0 ||
            depth > maxDepth)
            continue;
        // Execute the $(s, t)$ connection strategy and
        // update _L_
        Point2f pFilmNew = pFilm;
        Float misWeight = 0.f;
        Spectrum Lpath = ConnectBDPT(
            scene, lightVertices, cameraVertices, s, t,
            *lightDistr, lightToIndex, *camera, *tileSampler,
            &pFilmNew, &misWeight);
        if (t != 1)
            L += Lpath;
        else
            film->AddSplat(pFilmNew, Lpath);
    }
}
```

特别地，当 $t = 1$ 时，由于光线直接入射摄像机，其像素位置很大可能会与当前像素不同，此时会使用 `film->AddSplat(pFilmNew, Lpath);` 解决这一问题。

## **16.3.1 `Vertex` 抽象层**

路径空间的渲染算法的其中一个优势在于：路径生成的方式可以是十分自由的。但这一特点常常导致复杂且难以 debug 的实际实现。为了避免在核心代码中出现大量的条件判断语句，PBRT 定义了 `Vertex` 类型以表现路径上的任意一种顶点。

PBRT 中总共有 4 种路径顶点类型：`enum class VertexType { Camera, Light, Surface, Medium };`

其中的 `beta` 成员与之前的 $\beta$ 相似，包含了 BSDF 、phase function 、透射率、$\cos$ 项等内容在路径上的乘积。

在原有的表面和介质 `Interaction` 之外，BDPT 还拓展了 `EndpointInteraction` 以表示摄像机或灯光的一个位置。它在 `Interaction` 基础上增加了一个 union 以储存指向摄像机或光源的指针。而这个类型也和其它两种类型一并以一个 union 的形式储存在 `Vertex` 内。

此外，在 `Vertex` 中还保存了 `delta` 布尔值表示当前节点是否采样自一个 Dirac delta 分布。

该抽象层中保存的最后两个变量是用于 MIS 的概率密度 `pdfFwd, pdfRev` 。为了从多种采样方式中得到的路径获取其概率密度 ，PBRT 使用了路径上所有节点的面积概率密度的乘积作为路径的概率密度。上述的两个变量分布代表了从采样算法得到的概率密度和一个假设的，在逆向传输（即假设所属子路径的类型互换）时对应的概率密度。

除此之外，本类中还包含了一系列的工具函数，包括但不限于：

- 统一 BSDF 和 phase function 的传输函数 `f`
- 判断表面类型、是否可连接的一系列函数 `IsOnSurface, IsLight, IsConnectable...`
- 根据给定前后节点计算该次采样 PDF 的 `Pdf`

由于一个光源上的节点可能由直接采样或光线追踪生成，这种节点的 PDF 需要单独的方法计算（需要注意的是，只有从光源子路径的第一个节点才有 `type == VertexType::Light` ，这也是判断节点来源的根据）。特别地，对于光源的采样，我们还需要加入场景种所有的光源信息的考量。

## 16.3.2 生成子路径

子路径的生成由一对对称的函数 `GenerateCameraSubpath()` 和 `GenerateLightSubpath()` 完成。二者均会在初始化路径的第一个顶点后调用一个统一的次级函数 `RandomWalk()` 以采样接下来的路径顶点，最后返回子路径中实际的顶点数。

生成两对子路径时，首先由上级的函数调用 `Camera::Sample_We, Light::Sample_Le` 生成第一个采样，并初始化路径的端点。紧接着它们会将当前的光线方向、$\beta$ 、方向立体角 PDF 和最大深度传入 `RandomWalk` 函数。

在 `RandomWalk` 函数中，`pdfFwd, pdfRev` 两个局部变量在每个光线追踪的迭代之间更新，它们满足以下情况：

1. 在迭代开始时，`pdfFwd` 记录了当前的采样射线 `ray.d` 在立体角上的概率
2. `pdfFwd` 会在创建 `Vertex` 时被转换为单位平面上的概率储存在当前 `Vertex::pdfFwd` 中
3. 在迭代结束时，`pdfRev` 记录了在已知新的出射方向时，将旧射线端点交换后得到的反向射线的立体角概率
4. `pdfRev` 会被转换并写入前一个节点的 `Vertex::pdfRev` 

特别地，由于使用的 phase function 均是对称的，在计算 medium 散射时会有 `pdfFwd = pdfRev` ；另一方面，当遇到完全光滑的表面时，会有 `pdfFwd = pdfRev = 0` 。

## 16.3.3 连接子路径

`ConnectBDPT` 函数的作用是，输入两段子路径和 `s, t` 两个参数表示两侧分别选取的节点数，返回对应连接策略的贡献值。特别地，当 `t = 1` 即只使用一个摄像机节点时，对应的胶片位置会使用 `pRaster` 返回。

函数的主体包含了对各种情况的分类处理：

- 对于非法连接（连接灯光路径和摄像机路径中的灯光）直接返回零值
- `t == 1` 时：忽略灯光路径信息，而使用和 PT 相同的直接光照估计方法计算
- `s == 0` 时：将相机路径视为一个完整路径处理，返回相机路径最后一点的出射照度对整个路径的贡献
- `s == 1` 时：在返回贡献的同时返回像素位置
- 在其它情况下，在测试连接性后，返回贡献度，其中使用 `G(scene, sampler, qs, pt)` 函数返回两点之间的几何项，公式如下：
  
    $$
    \begin{array}{r}\hat{P}\left(\overline{\mathrm{q}}_{s} \overline{\mathrm{p}}_{t}\right)=L_{\mathrm{e}} \hat{T}\left(\overline{\mathrm{q}}_{s}\right)\left[\hat{f}\left(\mathrm{q}_{s-2} \rightarrow \mathrm{q}_{s-1} \rightarrow \mathrm{p}_{t-1}\right) \hat{G}\left(\mathrm{q}_{s-1} \leftrightarrow \mathrm{p}_{\mathrm{t}-1}\right)\right. \\\left.\hat{f}\left(\mathrm{q}_{s-1} \rightarrow \mathrm{p}_{t-1} \rightarrow \mathrm{p}_{t-2}\right)\right] \hat{T}\left(\overline{\mathrm{p}}_{t}\right) W_{\mathrm{e}}\end{array}
    $$
    

## 16.3.4 MIS

MIS 在 BDPT 中发挥了重要的作用，通过赋予不同的连接方式以不同的权重，这一方法可以大幅降低结果的方差。为了实现在路径上的 MIS ，我们需要一种方法以获知每一种路径各自的概率。这一步发生在 `ConnectBDPT` 函数的最后，它调用 `MISWeight` ，利用路径信息，特别是之前缓存的 `pdfFwd, pdfRev` ，以生成对应的概率。

下图对比了在不使用 MIS 的情况下，各个路径生成方式的结果差异：

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/bdpt-strategies.png" style="max-height: 45vh; margin: 10px 0"/></center>

下图展示了 MIS 的权重在不同路径设置下对画面内容的选择倾向，可以注意到这种方法有效地降低了低质量的路径的权重：

<center><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/bdpt-weighted.png" style="max-height: 45vh; margin: 10px 0"/></center>

我们可以使用元组 $(s, t)$ 表示当前的一种连接策略，它代表了长度为 $n = s + t$ 的一条路径 $\bar{x}$ ：

$$
\overline{\mathrm{x}}=\left(\mathrm{x}_{0}, \ldots, \mathrm{x}_{\mathrm{n}-1}\right)=\left(\mathrm{q}_{0}, \ldots, \mathrm{q}_{s-1}, \mathrm{p}_{t-1}, \ldots, \mathrm{p}_{0}\right)
$$

记每个节点上的两个方向概率密度为 $p^{\rightarrow}(x_i), p^{\leftarrow}(x_i)$ ，则某一条路径的概率密度为：

$$
p_{s}(\overline{\mathrm{x}})=p^{\rightarrow}\left(\mathrm{x}_{0}\right) \cdots p^{\rightarrow}\left(\mathrm{x}_{\mathrm{S}-1}\right) \cdot p^{\leftarrow}\left(\mathrm{x}_{\mathrm{S}}\right) \cdots p^{\leftarrow}\left(\mathrm{x}_{\mathrm{n}-1}\right)
$$

上式中的概率值均储存在对应子路径节点中的 `pdfFwd` 中。

更一般地，我们同样关心该路径中不同的传输方式的概率值，我们枚举 $s$ 的值 $i$ ，有：

$$
p_{i}(\overline{\mathrm{x}})=p^{\rightarrow}\left(\mathrm{x}_{0}\right) \cdots p^{\rightarrow}\left(\mathrm{x}_{i-1}\right) \cdot p^{\leftarrow}\left(\mathrm{x}_{i}\right) \cdots p^{\leftarrow}\left(\mathrm{x}_{\mathrm{n}-1}\right), \ 0 \leq i \leq n
$$

最后，我们使用 MIS 中的平衡启发式权重（balance heuristic weight）为单一路径赋值，即：

$$
w_{s}(\overline{\mathrm{x}})=\frac{p_{s}(\overline{\mathrm{x}})}{\sum_{i} p_{i}(\overline{\mathrm{x}})}
$$

上述的基本思想就是 `MISWeight` 函数中将要进行的主要计算，但在实现上这带来了两个问题：

1. 路径密度函数很容易溢出浮点数的精度表示，它的精度要求会随着路径长度和场景复杂度指数增长
2. 原始的 MIS 算法实现会花费相对最大路径长度而言 $O(n^4)$ 的时间复杂度（计算单一概率、总概率和枚举两个路径参数分别会贡献 $O(n)$ 的复杂度）

为了避免上述两个问题，实现中会采用一种增量式的方法。首先将分子直接除到分母上可得：

$$
w_{s}(\overline{\mathrm{x}})=\frac{1}{\sum_{i} \frac{p_{i}(\overline{\mathrm{x}})}{p_{s}(\overline{\mathrm{x}})}}=\left(\sum_{i=0}^{s-1} \frac{p_{i}(\overline{\mathrm{x}})}{p_{s}(\overline{\mathrm{x}})}+1+\sum_{i=s+1}^{n} \frac{p_{i}(\overline{\mathrm{x}})}{p_{s}(\overline{\mathrm{x}})}\right)^{-1}
$$

接着引入一个新变量 $r_i(\bar{x})$ 表示上式中出现的比例式，我们可以得到一系列的递归定义：

$$
r_{i}(\overline{\mathrm{x}})=\frac{p_{i}(\overline{\mathrm{x}})}{p_{s}(\overline{\mathrm{x}})}
$$

$$
\begin{aligned}&r_{i}(\overline{\mathrm{x}})=\frac{p_{i}(\overline{\mathrm{x}})}{p_{i+1}(\overline{\mathrm{x}})} \frac{p_{i+1}(\overline{\mathrm{x}})}{p_{s}(\overline{\mathrm{x}})}=\frac{p_{i}(\overline{\mathrm{x}})}{p_{i+1}(\overline{\mathrm{x}})} r_{i+1}(\overline{\mathrm{x}}) \quad(i<s) \\&r_{i}(\overline{\mathrm{x}})=\frac{p_{i}(\overline{\mathrm{x}})}{p_{i-1}(\overline{\mathrm{x}})} \frac{p_{i-1}(\overline{\mathrm{x}})}{p_{s}(\overline{\mathrm{x}})}=\frac{p_{i}(\overline{\mathrm{x}})}{p_{i-1}(\overline{\mathrm{x}})} r_{i-1}(\overline{\mathrm{x}}) \quad(i>s)\end{aligned}
$$

将递归定义的递归系数展开，易得：

$$
\begin{aligned}\frac{p_{i}(\overline{\mathrm{x}})}{p_{i+1}(\overline{\mathrm{x}})} &=\frac{p^{\rightarrow}\left(\mathrm{x}_{0}\right) \cdots p \rightarrow\left(\mathrm{x}_{\mathrm{i}-1}\right) \cdot p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}}\right) \cdot p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}+1}\right) \cdots p^{\leftarrow}\left(\mathrm{x}_{\mathrm{n}-1}\right)}{p \rightarrow\left(\mathrm{x}_{0}\right) \cdots p \rightarrow\left(\mathrm{x}_{\mathrm{i}-1}\right) \cdot p \rightarrow\left(\mathrm{x}_{\mathrm{i}}\right) \cdot p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}+1}\right) \cdots p^{\leftarrow}\left(\mathrm{x}_{\mathrm{n}-1}\right)}=\frac{p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}}\right)}{p^{\rightarrow}\left(\mathrm{x}_{\mathrm{i}}\right)} \\\frac{p_{i}(\overline{\mathrm{x}})}{p_{\mathrm{i}-1}(\overline{\mathrm{x}})} &=\frac{p^{\rightarrow}\left(\mathrm{x}_{0}\right) \cdots p \rightarrow\left(\mathrm{x}_{\mathrm{i}-2}\right) \cdot p^{\rightarrow}\left(\mathrm{x}_{\mathrm{i}-1}\right) \cdot p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}}\right) \cdots p^{\leftarrow}\left(\mathrm{x}_{\mathrm{n}-1}\right)}{p \rightarrow\left(\mathrm{x}_{0}\right) \cdots p \rightarrow\left(\mathrm{x}_{\mathrm{i}-2}\right) \cdot p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}-1}\right) \cdot p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}}\right) \cdots p^{\leftarrow}\left(\mathrm{x}_{\mathrm{n}-1}\right)}=\frac{p^{\rightarrow}\left(\mathrm{x}_{\mathrm{i}-1}\right)}{p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}-1}\right)}\end{aligned}
$$

即：

$$
r_{i}(\overline{\mathrm{x}})= \begin{dcases}1, & \text { if } i=s \\ \frac{p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}}\right)}{p \rightarrow\left(\mathrm{x}_{\mathrm{i}}\right)} r_{i+1}(\overline{\mathrm{x}}), & \text { if } i<s \\ \frac{p \rightarrow\left(\mathrm{x}_{\mathrm{i}-1}\right)}{p^{\leftarrow}\left(\mathrm{x}_{\mathrm{i}-1}\right)} & r_{i-1}(\overline{\mathrm{x}}), & \text { if } i>s\end{dcases}
$$

有了这个式子，我们就可以在 $O(n)$ 的时间内完成单个路径的权值计算，从而将总复杂度降低至 $O(n^3)$ ，并避免精度问题了。

需要特别注意的是，对于 Dirac delta 分布会出现的 $0 / 0$ 的情况需要特殊处理为 $1$ 。

对于路径端点的数据需要特殊处理，由于连接路径的变化，我们需要重新计算两条子路径的端点处的 PDF ，PBRT 引入了一个模板类 `ScopedAssignment` 在当前函数中暂时地修改路径端点的各个性质。这个类输入一个目标对象作为构造函数，在本类中保存一份快照，并在析构时将快照恢复。

> 总感觉这个类的设计充斥着各种潜在的坑……
> 

## 16.3.5 背景光源和 BDPT

背景光源在 PBRT 中作为从一个无限远、无限大的光源发出的光线，对基于面积概率的 BDPT 而言是一个异类。虽然我们可以将这类光源实现为一个包裹场景的球体发出的光线，但它在增大整体复杂度之外对于解决系统的其它部分没有任何作用。因此 PBRT 中选择了向 `Vertex` 抽象层中添加固体角概率的支持以解决这一问题。

回顾 `RandomWalk` 函数，在射线离开场景时，我们还需要额外记录一个光源节点以表示环境光。这一节点的 `pdfFwd` 中储存的是固体角概率。

这一光源节点的出现与后文中的连接策略结合，防止了向出射方向连接出不存在的路径。

除了出射场景的情况以外，我们还可能在采样光源的时候采样出一个背景光源，这一特殊情况将在 `GenerateLightSubpath()` 的末端特殊处理：

1. 将路径后的第一个端点的 `pdfFwd` 使用背景光在场景包围球上的面积概率替换
2. 重新计算光源端点的 `pdfFwd` 为所有背景光源的 pdf 之和

# 16.4 Metropolis Light Transport

1997 年，Veach 和 Guibas 提出了一种非常规的渲染方法，他们称之为 Metropolis Light Transport (MLT)，这种方法将 Metropolis-Hastings 采样算法应用于路径空间的采样之中，使得采样的样本在统计学上具有相关性。MLT 会在场景中生成一系列携带光照的路径，每个路径均由上一个路径衍生而来，并在统计上和各个路径的贡献度具有相同的概率。这种算法拥有极高的灵活性，由于其对演变方法的限制较少，它可以使用一些高度专一化的演变方法以采样对于传统 MC 方法而言难以采样的场景。

原始的 MLT 算法直接在路径空间的光线传输理论上进行构建，这带来了额外的实现挑战。这是因为路径空间不是完整的欧氏空间，一方面，路径的顶点会落在一系列 3D 空间的 2D 子集中，另一方面，平滑高光反射和折射也会进一步压缩空间自由度。原始的 MLT 构建了五种不同的演变策略以应对不同种类的光线路径类型。其中的三种表示了局部的探索以采样焦散或一系列复杂的平滑-漫反射-平滑表面序列；而另外两种则表现出更大的步长和更小的接受率。实现完整的 MLT 变换是巨大的工程量，其主要原因之一是它的变换没有一个是对称的。任意一个出现在系统中的小错误都可能导致微妙的收敛视觉错误，而且这些错误是臭名昭著地难以找出。

## 16.4.1 Primary Sample Space MLT（PSSMLT）

2002 年，Kelemen 等人提出了另一种名为 Primary Sample Space MLT (PSSMLT) 的同样基于 Metropolis-Hastings 采样算法的渲染技术。和原始的 MLT 不同，PSSMLT 并不直接使用路径空间，而是通过附加在 PT 或 BDPT 算法上间接地采样光线。这一做法主要的优势在于 PSSMLT 可以在欧氏空间中使用对称的转移策略，进而更容易被实现，但缺点在于这种算法缺乏对以构建光路的细节信息，从而难以构建和原始 MLT 相似的那种复杂的转移策略。

回顾此前 PT 的实现方法，我们使用程序生成的随机变量在各个路径位置采样光线。但倘若我们将随机变量作为一个额外的输入值传入 `PathIntegrator::L()` 中，我们就能得到一个确定性函数，其除了给定场景以外的另一组参数就是输入的随机值 $[0,1)^{\infty}$ 。

有了这种让 $L$ 作为一个在所有可能的采样序列上定义的 radiance 估计函数的解释，我们就可以在这个采样序列空间中使用 Metropolis-Hastings 算法，以返回的辐照度为对应的重要性权重进行采样。这种采样方法就被称为主采样空间（Primary Sample Space）。为了简洁，我们记采样序列 $X = (X_1, X_2, \cdots) \in [0,1)^\infty$

<center style="margin-bottom: 10px"><img src="https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/pssmlt-idea.svg" style="max-height: 25vh; margin: 10px 0"/></center>

PSSMLT 搜索 PSS 的主要转移策略有两种：

1. 直接替代所有 $X$ 的分量为新的随机生成的数值（大步长）
2. 在各个采样值 $X_i$ 上添加微小的扰动，一般是从以当前位置为中心的正态分布采样（小步长）

这两种策略均是对称的，因此它们的转移概率在计算时可以互相抵消。

由于 Metropolis-Hastings 采样和 `Integrator` 的接口只包含了对虚拟采样向量的改变，这使得它成为了一个非常一般性的方法。理论上 PSSMLT 可以增强任意一种基于蒙特卡洛采样的渲染方法。事实上，它甚至可以增强和渲染无关的蒙特卡洛方法。

在实践中，PSSMLT 一般实现于已有的 BDPT 之上，新的方法在每次迭代之间生成新的采样空间状态并传给 BDPT ，让它使用已有的方法连接路径并在路径之间进行 MIS 。但这也并非没有缺点，通常情况下只有一小部分连接方法是较为有效的，渲染器仍然会花费大量时间生成并评估低权重的光路。

## 16.4.2 Multiplexed MLT（MMLT）

2014 年 Hachisuka 等人提出了一种 PSSMLT 的改进算法 Multiplexed Metropolis Light Transport (MMLT) 以解决上述低权重光路的评估问题，MMLT 没有改变每次迭代外的 Metropolis-Hastings 算法过程，而是在 BDPT 的内部增加了细微但有效的改动。和原始 BDPT 中尝试创建所有的路径不同，MMLT 增加了一个随机变量用于选择需要连接的光路，并将该唯一光路的贡献除以其概率并返回。

为了避免不必要的结构性路径变化，Hachisuka 等人更改了马尔科夫链，从而使得该随机值只会在固定长度的光路中进行挑选，更一般性的光照传输则由多个独立的马尔科夫链共同完成。这种实践的结果使得采样器可以在有效的光路上花费更多时间，从而产生更大的 MIS 权重的贡献值。而每个独立的迭代也因此变得更快了。

下图从左到右展示了 PT ，BDPT 和 MMLT 在 San Miguel 场景，近似计算量下的视觉表现：

<center style="margin-bottom: 10px">
    <img src="sm-path.png" style="max-height: 18vh; margin: 5px"/>
    <img src="sm-bdpt.png" style="max-height: 18vh; margin: 5px"/>
    <img src="sm-mlt.png" style="max-height: 18vh; margin: 5px"/>
</center>

下表则对比了不同场景中三种不同的方法产生零 radiance 光路的比例：

|              | PT    | BDPT  | MMLT  |
| ------------ | ----- | ----- | ----- |
| Modern House | 98.0% | 97.6% | 51.9% |
| San Miguel   | 95.9% | 97.0% | 62.0% |

## 16.4.3 渲染中的应用

Metropolis 采样会从给定的标量函数中生成样本，为了将它应用于渲染中，需要解决两个问题：

1. 我们需要对每个像素计算不同的积分以生成整个图片
2. 我们需要处理的函数 $L$ 是一个在光谱上有多个维度的函数而非标量函数

为了解决这一问题，我们首先需要定义一个图像贡献函数，它表示了一个具有 $j$ 个像素的图像，每个像素都有一个值 $I_j$ 以表示其在滤波函数 $h_j$ 下重建的像素值：

$$
I_j= \int_\Omega h_j(X)L(X)\mathrm{d}\Omega
$$

当有 $N$ 个样本从某一分布中取出时，给出的蒙特卡洛估计为：

$$
I_{j} \approx \frac{1}{N} \sum_{i=1}^{N} \frac{h_{j}\left(X_{i}\right) L\left(X_{i}\right)}{p\left(X_{i}\right)}
$$

由于函数 $L$ 的光谱性质，我们需要一个标量贡献函数以应用 Metropolis 采样。一般而言，直接使用光谱的亮度值就是一个不错的选择。实际上使用任何当 $L$ 非零时也非零的函数都可以生成正确的结果，只不过其有效性会有所区别。

给定一个标量贡献函数 $C(X)$ ，Metropolis 采样会从其标准化分布中采样一系列样本：

$$
X_i \sim p(X) = {C(X) \over \int_\Omega C(X)\mathrm{d}\Omega}
$$

其中的分母，归一化常数 $b = \int_\Omega C(X)\mathrm{d}\Omega$ ，可以实现使用 BDPT 等方法预计算出来，最终每个采样对像素的贡献为：

$$
\frac{b}{N} \frac{h_{j}\left(\mathbf{X}_{i}\right) L\left(\mathbf{X}_{i}\right)}{C\left(\mathbf{X}_{i}\right)}
$$

## 16.4.4 Primary Sample Space Sampler

`MLTIntegrator` 基于 BDPT 实现了 Metropolis 采样和 MMLT 方法以渲染图片。在介绍该类之前，我们首先需要引入 `MLTSampler` ，它负责了管理 PSS 的状态变量、变换以及接受 / 拒绝的步骤。

在采样时，函数 `MLTSampler::StartIteration()` 首先被调用以决定当前迭代中使用的是哪一类步长。`MTLSampler::currentIteration` 成员变量记录了当前的迭代轮数（被拒绝的采样不会计入），而 `MLTSampler::lastLargeStepIteration` 则记录了上一次大步长发生的位置。 `MLTSampler::Accept()` 则会在任意采样提议被接受时调用。

`MLTSampler::EnsureReady()` 实现了一种懒更新的机制，它会在每次获取值时调用，保证当前调取的值已经被更新。所有的采样值使用 `PrimarySample` 保存，它除了记录采样值外还额外记录了上一次采样值更新的时间以及一份额外的备份值和备份更新时间，当一个样本被拒绝时，`MLTSampler::Reject()` 会被调用以恢复所有当前轮次生成的备份值。。特别地，由于正态分布采样有可能采样到超出 $[0,1)$ 范围的数值，在最后还需要将采样值 warp 回规定的范围内：`Xi.value -= std::floor(Xi.value)` 。

为了分割不同目的的采样，`MLTSampler` 没有将所有采样顺序储存，而是根据初始化时传入的分隔流的数量按交叉顺序储存各个目的的采样，`MLTSampler::StartStream(), MLTSampler::GetNextIndex()` 方法负责开始对特定流的采样和转换流位置和实际位置。

## 16.4.5 MLT Integrator

在拥有了以上内容后，我们就可以定义 `MLTIntgrator` 了。

### `MLTIntegrator::L()`

首先定义的函数是 `MLTIntegrator::L()` ，它负责根据所给的采样样本生成对应的 radiance 。其中的参数 `depth` 表示了对应的路径长度，`pRaster` 则返回了对应的像素位置。

PBRT 的实现使用了三个 `MLTSampler` 中的样本流，前两个流用于相机和灯光路径采样，而最后一个则用于连接路径中可能出现的采样。

接着，本函数会首先利用深度和相机采样的第一个元素确定采样的路径长度，接着调用 BDPT 中的子路径生成函数，用对应的采样流生成子路径，最后使用连接函数返回估计的 radiance 。

### `MLTIntegrator::Render()`

在主渲染循环中，有两个重要的阶段：

1. 首先会生成一系列的初始采样来作为后续马尔科夫链的初始状态，这里的采样同时会被用于计算前文中的归一化常数 $b$ 。
2. 接下来则会从此前的初始状态中挑选并运行一系列马尔科夫链以应用 Metropolis 采样。

在第一个阶段中，MTLIntegrator 创建并采样了一个 `nBootstrapSamples = nBootstrap * (maxDepth + 1)` 长的一维分布，每个位置的概率密度和对应深度和初始状态所对应的亮度值成正比。接着在主渲染循环中，会从这一分布中采样出对应的初始状态，按照每个马尔科夫链平均可使用的变换次数进行数次变换。为了提高效率，此处会使用此前讨论过的将两个样本按接受率同时写入图像的优化方法。最后，所有数据会使用前述的常数缩放参数 $b / N$ 缩放后写入图像。

与其它 `Integrator` 按图像区块划分并行不同的是，`MTLIntegrator` 会按样本数并行，每个线程执行固定数量的马尔科夫链。

> 有个小问题，在图片全域进行 MH 采样会导致较暗的像素欠采样吧……？