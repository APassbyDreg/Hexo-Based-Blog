---
title: PBRT 第十三章笔记 | Notes for PBRT Chapter 13 - Monte Carlo Integration
date: 2021-12-15 01:12:47
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 13.1 Background and Probability Review

本节简单介绍了概率论中的几个基础概念：

- cumulative distribution function (CDF): $P(x) = P\{X \leq x\}$
- probability density function (PDF): $p(x) = \mathrm{d}P(x) / \mathrm{d}x$
- expected value: $E[f(x)] = \int_Df(x)p(x)\mathrm{d}x$
- variance: $V[f(x)] = E[(f(x) - E[f(x)])^2] = E[f^2(x)] - E[f(x)]^2$

# 13.2 The Monte Carlo Estimator

蒙特卡洛估计器给了我们一种可以使用部分样本估计总体积分结果的方法：

$$F_N = {1 \over N} \sum_{i=1}^n{f(X_i) \over p(X_i)}$$

# 13.3 Sampling Random Variables

为了能够正确地使用蒙特卡洛方法求解积分，我们需要一种方法，以从任意的分布进行采样。

## 13.3.1 求逆法

求逆法通过对 CDF 进行求逆操作以得到从 $U[0,1]$ 样本到对应样本的映射，其采样一个样本流程为：

1. 根据给定的 PDF 通过积分计算 CDF
2. 计算 PDF 的逆函数 $P^{-1}(x)$
3. 从一个 $U[0,1]$ 中采样一个数 $\xi$
4. 计算 $X_i = P^{-1}(\xi)$ 作为采样值

特别地，对于离散的情况，PBRT 提供了 `Distribution1D` 类型作为一个工具类。它输入一组离散的样本值和离散点的个数，在构造函数中计算它的积分值（用于生成 PDF ），并生成归一化的 CDF 函数。

当需要采样一个值时，这个对象会首先使用二分查找找到采样随机数两侧的离散位置，接着根据采样的连续性要求与否生成采样值，最后返回对应的 PDF 。

## 13.3.2 拒绝法

对于无法使用数值方法积分以获得 PDF ，或无法求解 CDF 的逆函数的情况，拒绝法提供了一种解决方案。

拒绝法的核心是使用一个更加简单的采样方法指导对复杂分布的采样。对于复杂的分布 $p$ ，拒绝法采样的流程如下：

1. 找到一个简单的分布 $q(x)$ 和一个尽量小的常数 $c$ 使得 $p(x) \leq cq(x)$ 
2. 从简单分布中采样得到样本 $Y$ ，另从 $U[0,1]$ 中采样样本 $U$
3. 如果 $p(Y) \geq c q(Y) \times U$ 则接受这一样本，否则重复步骤 2 直至找到接受的样本

一个最简单的例子就是使用均匀分布作为参照分布，选取 $c$ 使得 $cq(x)$ 的顶端和 $p$ 的极值相等。几何上来说，任何在 $p$ 函数曲线下方的采样点均被接受，结合二维概率即可知拒绝后的采样概率密度和目标密度相等。

# 13.4 Metropolis Sampling ⚠️

这里讨论了另一种通用的对非负分布进行采样的方法。它不必对 CDF 求逆，而且每一次迭代均可以产生有效的样本。这一采样方式的缺点在于，生成的样本在统计学上存在着一定的先后关联性。需要在采样数量较大时才可以保证对整个采样域有良好的覆盖。

## 13.4.1 算法基础

Metropolis 算法可以生成一系列以映射 $f:\Omega = R^n \to R$ 为出现权重的高维样本 $X$ 。当第一个样本 $X_0$ 被选择后，以递推的方式，根据前一个样本的内容，使用随机变换（random mutation）的方式生成下一个样本。这个随机的变换可能被拒绝，当它被拒绝时，Metropolis 算法直接返回上一个样本值；当它被接受时，则返回变换后的样本值。这种算法挑选的接受方式需要保证这些随机变换在采样数量取极限时能让样本分布收敛到 $f$ 的概率密度上。

为了确定何时接受或拒绝给定的变换，我们首先需要计算在已有的变换生成方法下，从状态 $X$ 转移到状态 $X'$ 的转移概率密度 $T(X \to X')$ 。接下来还需要找到一个转移的接受概率 $a(X \to X')$ ，使得任意一个转移路径的概率都相等，即：

$$f(X)T(X \to X')a(X \to X') = f(X')T(X' \to X)a(X' \to X)$$

这一性质又被称为 detailed balance 。

由于 $T, f$ 均为已知量，我们可以由此计算 $a$ ：

$$a(X \to X') = \min\left(1, {f(X')T(X' \to X) \over f(X)T(X \to X')}\right)$$

将上述的内容结合起来，我们可以得到 Metropolis 算法的伪码：

```python
X = X0
for i in range(n):
    X_ = mutate(X)
    a = accept(X, X_)
    if (random() < a):
        X = X_
    record(X)
```

由于 Metropolis 算法常常会忽略 $f$ 值较小的样本，为了获取更多这些区域的信息，我们可以更改样本的储存方式，为每个样本赋予一定的权重，并在每次采样中同时以 $a$ 值为权重分割保存新旧两个样本，这一被称为 expected values 的策略伪码如下：

```python
X = X0
for i in range(n):
    X_ = mutate(X)
    a = accept(X, X_)
    record(X, 1 - a)
    record(X_, a)
    if (random() < a):
        X = X_
```

## 13.4.2 选择递推变换的方式

一般而言，只需要转移概率 $T$ 是可计算的，递推变换的选取方式是自由的。更进一步，只要这一转移概率是对称的，满足 $T(X \to X') = T(X' \to X)$，它甚至都不需要满足可计算性。

一般而言，这一变换一般都倾向于选择大幅偏离源样本的新采样，这有助于随机算法快速覆盖尽可能多的样本域。另一方面，如果当前位置的 $f$ 值很大的话，有很大的可能性会出现连续多个采样被拒绝的情况，这也是采样算法希望尽可能避免的。总的来说，我们需要保证随机算法的遍历性，使得对于任意的 $f(X), f(X') > 0$ 均有 $T(X \to X') > 0$ 。

一种可行的方式是对当前的样本 $X$ 进行随机扰动。对于一个向量 $X = (x_0, x_1, \cdots)$ ，我们可以随机对其中的一部分维度进行扰动，使得 $x_i' = (x_i\pm s\xi)\mathrm{mod}1$ ，其中的 $s$ 是一个扰动的缩放系数。更简单地，我们也也可以进一步直接使 $x_i' = \xi$ 完成采样。从结果而言，这两种采样方式均满足对称性，从而可以直接忽略 $T$ 的计算。

另一种方法是使用一个与目标函数相近的 PDF 去采样新的 $X' \sim p$ ，在这种情况下有 $T(X \to X') = p(X')$

## 13.4.3 初值 Bias

在之前的讨论中被忽视的一个重要组成部分是初值 $X_0$ 的选取。初值将指导后续的值的选择，虽然其后选择的样本分布与原始分布是无偏的，但初值的选择本身可能带来 Bias 。

一种简易的解决方法是首先使用 Metropolis 算法在随机初值上先运行几轮迭代，再抛弃之前的采样，将 $X_k$ 作为初值开始采样。但这种方法具有两方面的问题：其一是计算被抛弃的部分的代价比较高，再者我们并没有有效的方法来确定具体要迭代多少次才能使初值偏差的影响降低到可接受的范围内。

对于这一问题的终极解决方法是：使用另一种分布采样 $X_0 \sim p$ ，对于 $X_0$ ，我们会使用一个权值来平衡以这一初值开始的采样序列对总体的影响：$w = {f(X_0) / p(X_0)}$ 。

这一方法带来了一个问题：当采样的初值 $f(X_0) = 0$ 时，这一系列的采样的权值都会设为零。这时我们可以采样多个初值候选 $Y_1, \cdots, Y_n$ ，并定义各个初值的权重 $w_i$ ，接着根据权值从所有候选中选出 $X_0$ ，并以之前的权重平均值 $\overline w$ 作为这一轮采样的权值。

## 13.4.4 一维下的一个例子

考虑以下的简单分布：

$$f(x) = \begin{cases}
(x-0.5)^2 & ,0 \leq x \leq 1\\
0 & ,otherwise
\end{cases}$$

<center><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/metro-func.svg" style="max-height: 18vh; margin: 10px 0"/></center>

我们接着定义两种变换，对于一个随机数 $\xi \sim U[0, 1]$ 

- 第一种变换：将 $\xi$  直接赋值给新样本，因此得：
  
    $$\mathrm{mutate}_1(X)\to\xi\\
    T_1(X \to X') = 1$$
    
- 第二种变换：在原始样本附近 $\pm 0.05$ 的范围内随机采样，可得：

$$\mathrm{mutate}_2(X)\to X + 0.1(\xi - 0.5)\\
T_2(X \to X') = \begin{cases}
10 &, |X - X'| \leq 0.05\\
0 &, otherwise
\end{cases}$$

对于第一个样本，我们只需简单地从 $U[0,1]$ 中采样 $X_0$ ，并将本序列的权值设为 $w = f(X_0)$ 即可。

最后，我们使用一种基于桶的可视化重建方法：首先将 $[0,1]$ 区间等分为 大小相同的桶，并累加每个桶中的所有样本的权值之和。我们在每次试验中进行 10000 次迭代，并将结果用 50 个桶可视化如下：

<center><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/metro-onestrategy.svg" style="max-height: 18vh; margin: 10px 0"/></center>

在第一个实验中，我们全程使用第一种变换方式，可以发现这并不是一种非常有效的采样方法，会引入很大的噪声。这是因为它无法在发现高 $f$ 值的时候在该值附近进行更多的采样。但上图仍然可以说明这一方法可以收敛至正确的分布上。

<center><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/metro-bothstrategies.svg" style="max-height: 18vh; margin: 10px 0"/></center>

在第二个实验中，我们以 1 : 9 的概率随机选择采样方式 1 和 2 。很显然，这种采样方法可以让我们在相同的迭代次数内获得更小的方差，对比第一个实验收敛速度有明显增加。第二种方法提供的更小的步长使得迭代时得以在更加平缓的位置内对高权值部分附近的区域多次采样。

<center><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/metro-10k-1mutate.svg" style="max-height: 18vh; margin: 10px 0"/></center>

然而并非步长越小效果就一定越好，在第三个试验中仅使用了第二种变换方式，能够发现这一采样大幅度偏离了目标分布，较小的步长使得样本很难跨过中间的低函数值区域，从而可能导致在函数的另一侧根本得不到任何采样。Metropolis 算法的特性决定了它会尝试远离低 $f$ 值的区域，但这并不代表这一算法不能收敛到正确的位置——它只不过是慢的离谱罢了。

## 13.4.5 使用 Metropolis 算法估算积分

此时我们回到基础的带概率密度的积分计算 $\int f(x)g(x) \mathrm{d}\Omega$ ，如果我们将 $\int f(x)\mathrm{d}\Omega$ 归一化，并视作 PDF ，并在此分布上使用 Metripolis 算法采样 $X_1, \cdots, X_N$ 我们就能将积分转换为：

$$\begin{aligned}
\int_\Omega f(x)g(x) \mathrm{d}\Omega &\approx  {1 \over N}\sum_{i=1}^N{f(X_i)g(X_i) \over p(X_i)}\\
&\approx {1 \over N} \left[\sum_{i=1}^Ng(X_i)\right] \int_\Omega f(x)\mathrm{d}\Omega
\end{aligned}$$

# 13.5 Transforming between Distributions

在求逆法生成样本的过程中，我们引入了将 $U[0,1]$ 样本转换为特定样本的方法。在这一节中，我们将讨论一个更一般的情况：当我们使用一个函数将样本从一个分布转换为另一个分布式，得到的新分布与原分布之间有何联系。

假设我们从原始 PDF $p_x(x)$ 中采样了一系列样本 $X_i$ ，如果我们使用函数 $Y_i = y(X_i)$ 将这些样本转化为一组新的样本，通常我们会希望得到新样本组的分布。

解决这一问题的前提在于，用于变换的函数 $y$ 必须是可逆的（即导数必须是严格单调的）：如果多个不同的 $X$ 值被映射到了同一个 $Y$ 上，那么我们也没有办法准确地描述特定 $Y$ 值的概率分布了。这种一一对应的性质带来了以下等式：

$$P\{Y\leq y(x)\} = P\{X \leq x\}\\
P_y(y) = P_y(y(x)) = P_x(x)$$

对 CDF 复合函数的两侧对 $x$ 求导有：

$$p_y(y){\mathrm{d}y \over \mathrm{d}x} = p_x(x)\\
p_y(y) = \left({\mathrm{d}y \over \mathrm{d}x}\right)^{-1} p_x(x)\\
$$

由于严格单调的性质，有：

 

$$p_y(y) = \left|{\mathrm{d}y \over \mathrm{d}x}\right|^{-1} p_x(x)\\$$

如果我们希望从采集自分布 $p_x$ 的样本得到符合分布 $p_y$ 的样本，并希望 $P_y(y(x)) = P_x(x)$ 时，则有：

$$y(x) = P_y^{-1}(P_x(x))$$

## 13.5.1 高维情况下的变换

如果我们进一步将上述结论推广到更高维的样本空间 $X \in R^n$ ，并使用双射 $Y = T(X)$ 将原始样本转换到同维的另一个分布上，则有：

$$p_y(y) = p_y(T(x)) = {p_x(x) \over \mathrm{abs(}\det{J_T(x)})}$$

其中的 $J_T(x)$ 是转换在 $x$ 位置上的 Jacobian 矩阵：

$$\left(\begin{array}{ccc}\partial T_{1} / \partial x_{1} & \cdots & \partial T_{1} / \partial x_{n} \\\vdots & \ddots & \vdots \\\partial T_{n} / \partial x_{1} & \cdots & \partial T_{n} / \partial x_{n}\end{array}\right)$$

## 13.5.2 极坐标系

对于极坐标系：

$$x = r\cos\theta\\
y = r\sin\theta$$

其 Jacobian 矩阵为：

$$J_{T}=\left(\begin{array}{cc}\frac{\partial x}{\partial r} & \frac{\partial x}{\partial \theta} \\\frac{\partial y}{\partial r} & \frac{\partial y}{\partial \theta}\end{array}\right)=\left(\begin{array}{cc}\cos \theta & -r \sin \theta \\\sin \theta & r \cos \theta\end{array}\right)$$

因此：

$$p(r,\theta) = rp(x, y)$$

## 13.5.2 球坐标系

对于极坐标系：

$$x = r\sin\theta\cos\phi\\
y = r\sin\theta\sin\phi\\
z = r\cos\theta$$

将概率从 xyz 采样转换到球坐标系时的关系是：

$$p(r,\theta,\phi) = r^2\sin\theta p(x, y, z)$$

而利用立体角的导数公式 $\mathrm{d}\omega = \sin\theta\mathrm{d}\theta\mathrm{d}\phi$ 可得，将概率从立体角采样转换到球坐标系时的关系是：

$$\begin{aligned}
p(\theta,\phi)\mathrm{d}\theta\mathrm{d}\phi & = p(\omega)\mathrm{d}\omega\\
p(\theta,\phi) & = \sin\theta p(\omega)
\end{aligned}$$

# 13.6 2D Sampling with Multidimensional Transformations

假设我们需要从一个二维的分布函数 $p(x, y)$ 采样。

在一些情况下，二维的分布函数可被表示为两个一维的分布函数的乘积，即 $p(x,y) = p_x(x)p_y(y)$ 。此时我们可以独立地从两个分布中采样得到样本并拼接为一个二维样本。但在大部分情况下，这两个维度一般都是不可分的。

在此，我们需要定义边缘分布函数和条件分布函数：

$$\begin{aligned}
p_x(x) &= \int p(x,y)\mathrm{d}y\\ 
p_y(y) &= \int p(x,y)\mathrm{d}x\\
p(x|y) &= {p(x,y) \over p(x)}\\
p(y|x) &= {p(x,y) \over p(y)}
\end{aligned}$$

一种一般的采样二维分布的方法就是首先从其中一个维度的边缘分布入手采样一个变量，再以该位置的条件分布采样另一个变量。

## 13.6.1 Uniformly Sampling a Hemisphere

半球面上进行均匀采样是常见的采样方式，它在各个立体角上以均匀的概率 $p(\omega) = 1 / 2\pi$ 采样。由上一节中的转换有：$p(\theta,\phi) = \sin\theta / 2\pi$ 。

假设我们首先采样 $\theta$ ，计算其边缘分布可得 $p_\theta(\theta) = \sin\theta$ ，再计算对应的条件分布 $p(\phi|\theta) = 1 / 2\pi = p_\phi(\phi)$ 。注意到这两个概率中均不存在另一个变量，因此这是一个可分解的分布，我们只需要分别以对应的 PDF 采样两个变量即可。

$\phi$  由于是一个均匀分布，只需简单地将从 $U[0,1]$ 的样本乘以 $2\pi$ 即可得到，而对于 $\theta$ 则有：

$$P(\theta) = \int_0^\theta \sin\theta'\mathrm{d}\theta' = 1 - \cos\theta$$

结合 13.5 中讨论的采样域转换方法，可得将两个从 0-1 分布中采样的变量转换到在立体角上均匀分布的半球面的方法：

$$\theta = \cos^{-1}(1-\xi_1) \Leftrightarrow \cos^{-1}\xi_1\\
\phi = 2\pi\xi_2$$

## 13.6.2 Sampling a Unit Disk

对于平面上的单位圆内部进行采样的方式也是类似的：

$$p_r(r) = \int_0^{2\pi}p(r,\theta)\mathrm{d}\theta = 2r\\
p(\theta|r) = {p(r,\theta) \over p_r(r)} = {1\over2\pi} $$

因此：

$$r = \sqrt{\xi_1}\\
\theta = 2\pi\xi_2$$

虽然这一方法能够解决均匀采样的问题，但这种映射方法也使得在原分布下的方形被扭曲压缩得比较严重。

<center style="margin-bottom: 10px"><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/uniform-disk-distortion.svg" style="max-height: 25vh; margin: 10px 0"/></center>

另一种更佳的映射方式会同轴地从 $[-1,1]^2$ 映射到单位圆上，从而生成扭曲更少的结果：

<center style="margin-bottom: 10px"><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/concentric-disk-mapping.svg" style="max-height: 25vh; margin: 10px 0"/></center>

它将一个范围在 $[-1,1]^2$ 的方形经过压缩变形后映射到单位圆上，转换的方式如下：

$$r = x\\
\theta = {y \over x}{\pi \over 4}$$

对于方形中的每个八分之一三角的区域均会被映射为圆上的一个扇形区域

<center><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/wedge-concentric.svg" style="max-height: 7.5vh; margin: 10px 0"/></center>

## 13.6.3 Cosine-Weighted Hemisphere Sampling

由于 BSDF 中 $\cos$ 项的存在，我们经常会希望更多地在法线附近采样，即有 $p(\omega) \propto \cos\theta$ 。通过归一化可得：

$$\int_{H^2}p(\omega)\mathrm{d}\omega = \int_{H^2}c\cos\theta\mathrm{d}\omega = \int_{H^2}c\cos\theta\sin\theta\mathrm{d}\theta\mathrm{d}\phi = 1\\
p(\theta, \phi) = {\cos\theta\sin\theta\over\pi}$$

虽然我们也可以像之前一样计算边缘概率分布和条件概率分布，但在此处我们可以使用一种称为 Malley's method 的方法来解决这一问题，它背后的原理在于：如果我们在一个圆上均匀地采样，再将采样点投影到对应的单位球上，那么得到的结果天然地具有 $\cos$ 加权地性质。

<center><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/Malleys%20method.svg" style="max-height: 12vh; margin: 10px 0"/></center>

为了证明这一点，我们需要从等式 $(r,\phi) = (\sin\theta,\phi)$ 完成从圆采样到半球面采样的转换，首先计算 Jacobian 矩阵的行列式：

$$|J_T| = \left|\begin{matrix}
\cos\theta & 0\\
0 & 1
\end{matrix}\right| = \cos\theta$$

即：

$$p(\theta,\phi) = |J_T|p(r,\phi) = \cos\theta\times{r\over\pi} = {\cos\theta\sin\theta\over\pi}$$

## 13.6.4 Sampling a Cone

对于基于球体的范围光源和聚光灯光源的采样而言，在一个圆锥体内均匀地采样一个方向也是有必要的。在这一情况中，$\phi, \theta$ 依旧是可分离的。其中有 $\phi = 1 / 2\pi$ ，由于：

$$p(\omega) = {p(\theta,\phi) \over \sin\theta} = p(\phi)\times{p(\theta) \over \sin\theta} = c$$

$$\begin{aligned}
1 & = \int_\Omega p(\phi,\theta)\mathrm{d}\phi\mathrm{d}\theta\\
  & = \int_0^{\theta_{\max}} c\sin\theta\mathrm{d}\theta\\
p(\theta) & = {\sin\theta \over {1-\cos\theta_{\max}}}\\
p(\omega) & = {1 \over 2\pi(1 - \cos\theta_{\max})}
\end{aligned}$$

由此我们可以得到从 $U[0,1]$ 转换到角度的方法：

$$\phi = 2\pi \xi_1\\
\cos\theta = (1-\xi_2) + \xi\cos\theta_{max}$$

## 13.6.5 Sampling a Triangle

在三角形中均匀采样实际上比大部分人想象得复杂很多。为了简化问题，我们首先在一个腰长为 1 的等腰直角三角形中进行均匀采样。

<center><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/Sample%20right%20triangle.svg" style="max-height: 10vh; margin: 10px 0"/></center>

还是老步骤，计算 PDF 、边缘分布和 CDF ，最后可得：

$$u = 1 - \sqrt{\xi_1}\\
v = \xi_2\sqrt{\xi_1}$$

## 13.6.6 Sampling Cameras ⚠️

这一部分和 6.4 的内容有关，描述了如何采样一段时间内摄像机的某个像素收到的总能量公式：

$$J=\frac{1}{z^{2}} \int_{A_{\mathrm{p}}} \int_{t_{0}}^{t_{1}} \int_{A_{\mathrm{e}}} L_{\mathrm{i}}\left(\mathrm{p}, \mathrm{p}^{\prime}, t^{\prime}\right)\left|\cos ^{4} \theta\right| \mathrm{d} A_{\mathrm{e}} \mathrm{d} t^{\prime} \mathrm{d} A_{\mathrm{p}}$$

## 13.6.7 Piecewise-Constant 2D Distributions

从有限的离散 PDF 上采样也是一个非常有必要讨论的内容，考虑一个 $n_u \times n_v$ 的二维离散常值分布函数 $f[u,v]$ ，我们可以使用累加替代积分进行计算：

$$\begin{aligned}
p(u, v)&=\frac{f(u, v)}{\iint f(u, v) \mathrm{d} u \mathrm{~d} v}=\frac{f[\tilde{u}, \tilde{v}]}{1 /\left(n_{u} n_{v}\right) \sum_{i} \sum_{j} f\left[u_{i}, v_{j}\right]}\\
p(v)&=\int p(u, v) \mathrm{d} u=\frac{\left(1 / n_{u}\right) \sum_{i} f\left[u_{i}, \tilde{v}\right]}{I_{f}}\\
p(u \mid v)&=\frac{p(u, v)}{p(v)}=\frac{f[\tilde{u}, \tilde{v}] / I_{f}}{p[\tilde{v}]}
\end{aligned}$$

`Distribution2D` 类型负责了这类内容的采样。

# 13.7 Russian Roulette and Splitting

我们首先使用耗时 $T$ 和方差 $V$ 定义一个估计器的效能：

$$\epsilon[F] = {1 \over V[F]T[F]}$$

Russian Roulette 和 Splitting 是两种可以提高效能的算法，它们通过提高高贡献值的样本比例而在相同的时间内获得更小的方差。

## 13.7.1 Russian Roulette

考虑直接光照的积分：

$$L_{\mathrm{o}}\left(\mathrm{p}, \omega_{\mathrm{o}}\right)=\int_{\mathrm{S}^{2}} f_{\mathrm{r}}\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L_{\mathrm{d}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{i}\right| \mathrm{d} \omega_{\mathrm{i}}$$

假设我们决定使用两个样本来估计这个积分，则有：

$$L_o(p, \omega_o) \approx {1 \over 2}\sum_{i=1}^2 {f_{\mathrm{r}}\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) L_{\mathrm{d}}\left(\mathrm{p}, \omega_{\mathrm{i}}\right)\left|\cos \theta_{i}\right|  \over p(\omega_i)}$$

在这一行为中，大部分的计算量来自于跟踪 shadow ray 以测试光源和着色点之间是否有遮挡物的操作。显而易见的，对于任何使得 $f_{\mathrm{r}}\left(\mathrm{p}, \omega_{\mathrm{o}}, \omega_{\mathrm{i}}\right) = 0$ 的方向，我们都应该跳过 shadow ray 的跟踪工作。Russian Roulette 让我们有机会掠过其它 $f_r$ 值较低的部分，从而将更多的计算量集中在贡献值更大的位置。

实现这一算法的步骤如下：

1. 首先根据先验知识和经验设定一个截止概率 $q$ 
2. 直接跳过任何在该概率以下权重的采样，并返回一个预设常数 $c$（通常是零）
3. 对于其它样本，以 $(F - qc) / (1-q)$ 为权重加权得到结果

统计学上，这是一种无偏的方法，但它仅仅能够在统计学上增加估计器的效能，甚至在选择了不正确的截止概率时，这一方法还会大大增加方差，从而反而降低效能。

## 13.7.2 Splitting

相对于减少不重要样本的采样的 Russian Roulette 方法，Splitting 方法通过更好地分配采样数以增加效能。

考虑对某个无 pixel filter 的像素的求解：

$$P(x,y)=\int_{A_{\mathrm{p}}}  \int_{S^2} L_{\mathrm{d}}\left(x,y,\omega\right) \mathrm{d} A_{\mathrm{p}} \mathrm{d} \omega$$

一种很自然的方法是生成数组不同的光线采样，每组有各自的像素位置和方向。假设场景中有且只有一个面光源，且所有光线均击中了物体，那么如果我们发射100 条这样的光线，就需要 100 次 shadow ray 运算和 100 次 camera ray 运算。然而事实上我们可能并不需要如此多的 camera ray 以生成优质的图像，事实上，如果我们仅仅发射 5 根从相机出发的光线，并在其中每根光线上采样 20 次光源，这样我们就可以在光线追踪数量大幅减少的同时获得近似的效果。

# 13.8 Careful Sample Placement

为了减少采样的方差，一种方式是使用更加精心挑选的采样位置以使得样本能获取更多积分的特征。

## 13.8.1 Stratified Sampling

Stratified Sampling 通过将积分域 $\Lambda$ 划分为 $n$ 个不重复的区域，然后根据各区域内的密度函数分别取 $n_i$ 个样本，则有：

$$F = \sum_{i=1}^nv_i\left( {1 \over n_i}\sum_{j=1}^{n_i}{f(X_{i,j}) \over p_i(X_{i,j})}  \right)$$

其中 $\sum v_i = 1$ 是各个区域的大小占总大小的比重。根据每一部分内的均值 $\mu_i$ 和方差 $\sigma_i^2$ 我们可以得到总方差：

$$\begin{aligned}V[F] &=V\left[\sum v_{i} F_{i}\right] \\&=\sum V\left[v_{i} F_{i}\right] \\&=\sum v_{i}^{2} V\left[F_{i}\right] \\&=\sum \frac{v_{i}^{2} \sigma_{i}^{2}}{n_{i}}\end{aligned}$$

特别地，当我们以区域大小作为区域采样数量的基准，即 $n_i = v_iN$ 时：

$$V[F_N] = {1 \over N}\sum v_i\sigma_i^2$$

为了将其与随机采样进行对比，我们设随机采样的采样方式实际上是首先随机选择一个区域 $I$ ，再在区域中进行采样 $X$，通过条件概率的方差公式可得：

$$\begin{aligned}
V[F] &= \sum_i v_i\left({1\over v_i}\int_{\Lambda_i}(f(x)-\mu)^2\mathrm{d}x\right)\\
&= \sum_i v_i\left({1\over v_i}\left(\int_{\Lambda_i}(f(x)-\mu_i)^2\mathrm{d}x + \int_{\Lambda_i}(\mu_i - \mu)^2\mathrm{d}x\right)\right)\\&=\frac{1}{N}\left[\sum v_{i} \sigma_{i}^{2}+\sum v_{i}\left(\mu_{i}-\mu\right)^2\right]\\
 &=E_{x} V_{i} F+V_{x} E_{i} F 
\end{aligned}$$

这一公式向我们展示了 Stratified Sampling 的一个优势之处：它只会减少方差，无论再差的设置也顶多是和完全随机一样差罢了。但它也并非毫无缺点，最大的一点就在于它和其它采样方法一样会受到维度灾难的影响，随着维度数量增加、复杂度以指数上升。另一方面，Stratified Sampling 的效果也和用于 stratify 的维度有关。我们一般会 stratify 具有较强的相关性的维度，如之前的那个直接光照的例子中，stratify $(x, y)$ 和 $(\theta, \phi)$ 维度的效果就远远好于 stratify $(x,\phi)$ 一类的组合。

## 13.8.2 Quasi Monte Carlo

在第七章中提到的一些其它采样方法组成了 quasi Monte Carlo 算法的基础。与标准的蒙特卡洛算法使用随机数生成采样这一点不同的是，这一类算法会使用确定性算法事先确定采样的样本位置，从而在一些情况下获得更高的收敛速率。而且蒙特卡洛算法中的一些技术（如重要性采样）也可以直接使用这种方法生成的采样。

## 13.8.3 Warping Samples and Distortion

大部分采样算法均事先生成 $U[0,1]^n$ 的样本，再通过算法转换到真正的采样域中，虽然在概率上分布依旧是均匀的，但由于空间的变换，原有 stratified 的样本的规律性会在这个过程中受损，如下图第一种方式是从一个 $4 \times 4$ 的来自 $U[0,1]^2$ 的 stratified sampling 采样映射到长方形的结果，它明显得要比使用确定性算法生成的低偏差采样来的差。

<center><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/longquad-jittered.svg" style="max-height: 10vh; margin: 10px 0"/></center>

<center><img src="https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/longquad-02.svg" style="max-height: 10vh; margin: 10px 0"/></center>

和 13.6.2 中提出的另一种映射到圆上的方式类似，这些例子体现了选择更好的映射方式和采样方式对最终采样图案的影响。

# 13.9 Bias

另一种降低方差的方法是人为地引入偏差（bias）。一个估计器的偏差定义为：$\beta = E[F]-\int f(x)\mathrm{d}x$

一个引入偏差降低方差的例子是，考虑两个估计器 $F_1 = {1\over N}\sum_{i=1}^N X_i,\ F_2 = {1\over 2}\max(X_1, X_2, \cdots, X_n)$ ，虽然第一个估计器是无偏的，但它的方差是 $O(N^{-1})$ ，而后者的方差却只有 $O(N^{-2})$

另一个例子是 7.8 中讨论的图像重建方法，我们的目标是使用一个 滤波函数 $f$ 在像素面积上对辐照度分布进行卷积：

$$I(x, y)=\iint f\left(x-x^{\prime}, y-y^{\prime}\right) L\left(x^{\prime}, y^{\prime}\right) \mathrm{d} x^{\prime} \mathrm{d} y^{\prime}$$

一种无偏的方法是使用传统的蒙特卡洛方法：

$$I(x, y) \approx {1 \over N} \sum_{i=1}^{N} {f\left(x-x_{i}, y-y_{i}\right) L\left(x_{i}, y_{i}\right) \over p(x_i, y_i)}$$

但 PBRT 中实际使用的则是一个有偏的方法，其公式如下：

$$I(x, y) \approx {\sum_{i=1}^{N} f\left(x-x_{i}, y-y_{i}\right) L\left(x_{i}, y_{i}\right) \over \sum_{i=1}^{N} f\left(x-x_{i}, y-y_{i}\right)}$$

考虑一种情况：整个图像上的亮度值均为常数 1 ，那么第二种方法会毫无疑问地给出常数 1 ，而前者则由于采样的随机性而不一定能够给出稳定的准确结果。

## 13.10 Importance Sampling

重要性采样是应用中最重要的减少方差的手段之一，如下的蒙特卡洛积分器：

$$F_N = {1 \over N} \sum_{i=1}^N {f(X_i) \over p(X_i)}$$

当我们选择样本的概率 $p$ 接近被积函数 $f$ 时，其收敛的速度可以明显地加快。考虑一个简单的 diffuse 平面的采样，当我们完全随机地采样，即 $p(x) = 1$ 时，在很多情况下我们可能采样到接近于法线平行的掠射角，从而生成一根对最终结果贡献甚微的光线。另一个例子是如果我们极端地使用完全正比于被积函数的概率采样样本，使用这种方式得到的结果就能拥有零方差，是完全准确的估计值。

$$\begin{aligned}
p &= {f(x) \over \int f(x)\mathrm{d}x}\\
{f(X_i) \over p(X_i)} &=  \int f(x)\mathrm{d}x
\end{aligned}$$

然而在另一方面，选择错误的采样概率，如和被积函数形状相差甚远的概率时反而会降低算法的收敛速度。

## 13.10.1 Multiple Importance Sampling

单纯的重要性采样给出了对单一函数 $f(x)$ 进行快速积分估计的方法，但实际上我们可能还会遇到由多个函数的乘积组成的积分 $\int f(x)g(x) \mathrm{d}x$ ，如光线传输方程中的多项乘积、微表面模型中的各项乘积等。

多重重要性采样的方法提供了使用多个分布结合采样的指导思想。其基本思想在于，对于所有的分布同时进行采样，再接着从这些样本中挑选出真正使用的采样。MIS 提供了在多个采样之中选择所需采样的加权方法，这同时可以有效地降低估计器的方差。

在从两个分布的乘积中采样的 MIS 算法给出的估计公式如下：

$$\frac{1}{n_{f}} \sum_{i=1}^{n_{f}} \frac{f\left(X_{i}\right) g\left(X_{i}\right) w_{f}\left(X_{i}\right)}{p_{f}\left(X_{i}\right)}+\frac{1}{n_{g}} \sum_{j=1}^{n_{g}} \frac{f\left(Y_{j}\right) g\left(Y_{j}\right) w_{g}\left(Y_{j}\right)}{p_{g}\left(Y_{j}\right)}$$

其中 $n_f,n_g$ 是在各个分布中选择的样本数量，$X,Y$ 分别是从不同分布中采样的样本，$w_f,w_g$ 是选用的特殊的加权函数以将估计值调整至无偏。这一加权函数需要考虑到所有可能的样本生成方式，而不只是关注于当前的样本。一种简单好用的是平衡地启发式加权函数：

$$w_s(X) = {n_sp_s(X) \over \sum_i n_ip_i(X)}$$

它代表了当前样本在当前分布下的概率密度占所有分布下得到概率密度之和的比例。在实际使用中，有时还会对这一权重做一次幂乘处理，变为：

$$w_s(X) = {(n_sp_s(X))^\beta \over \sum_i (n_ip_i(X))^\beta}$$

这种处理可以增加对于某一分布而言优秀的样本的权重，在经验上选择 $\beta = 2$ 是一种可行的方法。

推广到 $n$ 个分布的更一般的表述如下：

$$F=\sum_{i=1}^{n} \frac{1}{n_{i}} \sum_{j=1}^{n_{i}} w_{i}\left(X_{i, j}\right) \times \frac{f\left(X_{i, j}\right)}{p_{i}\left(X_{i, j}\right)}$$
