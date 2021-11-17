---
title: PBRT 第七章笔记（基础篇） | Notes for PBRT Chapter 05 - 07 - Sampling and Reconstruction (basis)
date: 2021-11-15 21:53:39
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 7.1 Sampling Theory

一个数字图像一般使用一个矩形区域的离散位置的颜色值表示。由于这些离散的值是从一个连续的区域中采样重建得到的，因此一般需要额外注意走样（aliasing）的问题。

另一个需要注意的是，数字图像中的像素一般指的是图像连续函数在某一位置的值，而显示像素则表示了以某种分布发光的某种物理实体。

## 7.1.1 频域和傅里叶变换

为了评估重建的函数和原图像函数的区别，一般会使用傅里叶分析的方法。傅里叶变换将时空上的函数转换到频域上。傅里叶变换和反变换的公式分别如下：

$$
F(\omega) = \int_{-\infty}^\infty f(x)e^{-i2\pi\omega x}\mathrm{d}x\\
f(x) = \int_{-\infty}^\infty F(\omega)e^{i2\pi\omega \omega}\mathrm{d}\omega
$$

## 7.1.2 理想的采样与重建

首先定义冲击采样函数：

$${III}_T(x) = T\sum_{i=-\infty}^{+\infty}\delta(x - iT)$$

其中 $\delta(x)$ 是单位冲击函数。上述的函数实际上描述了在空间中等间距分布的一群冲激函数。将这个函数与图像函数 $f$ 相乘就可以得到理想的采样值。最后，使用一个重建的核函数 $r(x)$ 与前述采样值做卷积操作即可得到重建的结果：

$$\tilde{f} = (III_T(x)f(x)) \circledast r(x) = T\sum_{i=-\infty}^{+\infty}f(iT)r(x-iT)$$

下图展示了一个函数经过 $T=1$ 的采样和三角重建核函数重建后的结果：

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/func-to-sample.svg" style="max-height: 20vh; margin: 10px"/><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/shah-samples.svg" style="max-height: 20vh; margin: 10px"/><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/shah-sampled-function.svg" style="max-height: 20vh; margin: 10px"/></center>

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/func-tri-reconstruction.svg" style="max-height: 25vh; margin: 10px 0"/></center>

在理想状态下，如果原始图像函数 $f$ 是有可数的多个频率构成的，如一系列有最小宽度的矩形函数。在这种情况下，可以利用傅里叶正反变换的性质得到对应的采样结果函数的。

## 7.1.3 走样

对于由数量极多，甚至无限种频率构成的图像函数，我们常常不能得到完全准确的采样结果。在这种情况下，重建产生的偏差就形成了走样现象。

## 7.1.4 反走样技术

- 非均匀采样：用于采样的位置会在均匀采样的基础上做出大小不一的随机偏移。这种技术将走样现象转化为噪声，从而减少视觉上的怪异感。
- 适应性超采样：当某一区域的信号复杂度较高时，可以适应性地增加采样率，从而在较少的性能消耗换取更好的重建表现。
- 预滤波：在采样前先使用某种方式（如模糊滤波）去除目标函数中高频的部分

## 7.1.5 采样与图像合成

图像合成的过程实际上就是在一个二维的函数 $f(x,y) = L$ 上进行采样。然而，一般的渲染器比较难以直接对目标函数进行预滤波，所以实现上一般使用超采样和随机采样结合的方法减少走样现象。

## 7.1.6 渲染中的走样因素

几何体的边缘和阴影是走样的最重要的贡献因素，它在亮度空间上形成了一个阶跃函数——这是一类无法用有限的频率描述的无限维特征。

极小的物体也会造成走样：当一个物体体积小到比两次采样的间距还小时，它可能在屏幕上反复出现和消失。

物体的材质和贴图也会引入高频信息，从而造成走样。

# 7.2 **Sampling Interface**

所有的采样器均继承于同一个抽象接口。它的工作是从 $\left[0,1\right)^n$ 空间中生成一系列 n 维的采样，其中的每个采样向量被用于一次图像的采样，而具体采样的维度数量则取决于不同的需求。取得的采样向量通常有以下特征：

- 前五位会被相机使用，包括两位 subpixel jitter ，一位时间和两位透镜 uv 位置。
- 更加细致地设计的算法通常会生成更好的采样结果，在向量中，一般更好的采样结果会在更前面

## 7.2.1 评估采样结果 ⚠️

这部分内容描述了如何评价一组采样的好坏，恕我以后再看。

## 7.2.2 基础的 `Sampler` 接口

这个接口的定义如下：

```cpp
class Sampler {
  public:
    // Sampler Interface
    virtual ~Sampler();
    Sampler(int64_t samplesPerPixel);
    virtual void StartPixel(const Point2i &p);
    virtual Float Get1D() = 0;
    virtual Point2f Get2D() = 0;
    CameraSample GetCameraSample(const Point2i &pRaster);
    void Request1DArray(int n);
    void Request2DArray(int n);
    virtual int RoundCount(int n) const { return n; }
    const Float *Get1DArray(int n);
    const Point2f *Get2DArray(int n);
    virtual bool StartNextSample();
    virtual std::unique_ptr<Sampler> Clone(int seed) = 0;
    virtual bool SetSampleNumber(int64_t sampleNum);
    std::string StateString() const {
      return StringPrintf("(%d,%d), sample %" PRId64, currentPixel.x,
                          currentPixel.y, currentPixelSampleIndex);
    }
    int64_t CurrentSampleNumber() const { return currentPixelSampleIndex; }

    // Sampler Public Data
    const int64_t samplesPerPixel;

  protected:
    // Sampler Protected Data
    Point2i currentPixel;
    int64_t currentPixelSampleIndex;
    std::vector<int> samples1DArraySizes, samples2DArraySizes;
    std::vector<std::vector<Float>> sampleArray1D;
    std::vector<std::vector<Point2f>> sampleArray2D;

  private:
    // Sampler Private Data
    size_t array1DOffset, array2DOffset;
};
```

在构建采样器时，需要传入每个像素采样的最大次数 `samplesPerPixel` 。紧接着，当渲染器需要对某一像素进行评估时，它会将该像素位置传入本接口的 `StartPixel` 函数以初始化随机状态。紧接着就可以使用 `Get1D` 等方法获取接下来的一个或者数个不同维度的采样结果，需要注意的是，如果在渲染中需要一次性获取多个维度的，需要在渲染开始前就首先调用 `Request[12]DArray` 接口以告知该需求。最后，使用 `StartNextSample` 方法结束本轮采用，下一轮采样将会从头开始。

## 7.2.4 Pixel Sampler 接口

这个接口在 `Sampler` 接口的基础上增加了有关

```cpp
class PixelSampler : public Sampler {
  public:
    // PixelSampler Public Methods
    PixelSampler(int64_t samplesPerPixel, int nSampledDimensions);
    bool StartNextSample();
    bool SetSampleNumber(int64_t);
    Float Get1D();
    Point2f Get2D();

  protected:
    // PixelSampler Protected Data
    std::vector<std::vector<Float>> samples1D;
    std::vector<std::vector<Point2f>> samples2D;
    int current1DDimension = 0, current2DDimension = 0;
    RNG rng;
};
```

这种采样器一次性生成所有的样本，由于渲染过程中需要的采样次数并不能提前确定，它需要一个最大的采样数目。当需要的数目超过该值时，这类采样器只会返回 uniform 的采样结果。

对于每个预生成的维度，这种采样器会生成两组大小为  `nSampledDimensions * samplesPerPixel` 的一维、二维采样序列。在使用时，对于同一个 sample index ，从头开始读取预计算的值，并在改变 sample index 或开始新采样时重设维度偏移量。

## 7.2.4 Global Sampler 接口

在实际实现中，一些并不基于像素的采样器会连续地生成分布在整个空间中的采样结果，每一次采样结果都可能会代表不同的像素。对于多线程分片渲染而言，这种采样方法是有必要的。`GlobalSampler` 为这种需求抽象出了一个接口，它将 `Sampler` 接口封装为两个最主要的虚函数：

- `GetIndexForSample` ：实现从采样到实际像素位置的重映射
- `SampleDimension` ：采样给定序号下的给定维度

# 7.3 Stratified Sampling

分层采样的核心是将像素切分为不重叠的更小的子区域，并从每个子区域中获取一个样本。这种方法通常通过在每个子区域内做随机偏移以将走样转化为噪声，但它也提供了无偏的模式以供参考。

分层抽样的最大问题在于，当采样的维度增加时，需要的采样数量会以指数级别膨胀，这种情况被称为维数灾难。其中一种缓解方法在于独立地为各个维度生成采样，然后随机地将这些采样关联起来，如下例中从三个位置分别取了 4 个采样共 12 此采样，接着使用随机关联的方式将其联系成四组不同的采样，进而得到收敛上较为优秀的采样结果。

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/Sample%20padding.svg" style="max-height: 30vh; margin: 10px 0"/></center>

```cpp
class StratifiedSampler : public PixelSampler {
  public:
    // StratifiedSampler Public Methods
    StratifiedSampler(int xPixelSamples, int yPixelSamples, bool jitterSamples,
                      int nSampledDimensions)
        : PixelSampler(xPixelSamples * yPixelSamples, nSampledDimensions),
          xPixelSamples(xPixelSamples),
          yPixelSamples(yPixelSamples),
          jitterSamples(jitterSamples) {}
    void StartPixel(const Point2i &);
    std::unique_ptr<Sampler> Clone(int seed);

  private:
    // StratifiedSampler Private Data
    const int xPixelSamples, yPixelSamples;
    const bool jitterSamples;
};
```

由于对一个空间的采样次数有时可能并不能被分割到一组合理的阵列中，本类中使用了 Latin Hypercube Sampling (LHS) 技术从任意多的采样个数中获得均匀分布的样本。其做法是：将空间的每一个维度分割为等量维度（通常是是样本数）、接着随机交换不同维度中的顺序，从而可以生成优秀的采样结果。

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/LHS%20shuffle.svg" style="max-height: 20vh; margin: 20px 0"/></center>

LHS 在采样数目较小时可以生成较为优秀的分布，但当采样数量增加时，它的效果会越来越差，并不如原始的 stratified sampling 方法。

# 7.4 **The Halton Sampler** ⚠️

这部分说明了一种特别的采样方式，待我以后再看。

# 7.5 (0, 2)-Sequence Sampler ⚠️

这部分说明了一种特别的采样方式，待我以后再看。

# 7.6 Maximized Minimal Distance Sampler ⚠️

这部分说明了一种特别的采样方式，待我以后再看。

# 7.7 Sobol’ Sampler ⚠️

这部分说明了一种特别的采样方式，待我以后再看。

# 7.8 Image Reconstruction

给定一系列样本和它们的 radiance ，我们需要将它转换为像素值。这中间有三个流程：

1. 从样本重建一个连续的图像函数 $\tilde{L}$
2. 对该重建函数做一次滤波以去除任何超过了像素空间中纳什限制的频率
3. 在像素的位置采样 $\tilde{L}$ 以得到最终的像素值

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/2d%20image%20filtering.svg" style="max-height: 20vh; margin: 20px 0"/></center>

实现上，为了计算一个点 $(x, y)$ 处的像素值，通常会考虑和它在一定距离内的所有采样点，并使用一个二维滤波函数 $f(x-x_i,y-y_i)$ 加权平均。一个考虑了每个样本的相机权重 $w$ 的完整的公式如下：

$$I(x,y) = \frac{\sum_i\left[f(x-x_i,y-y_i)w(x_i,y_i)L(x_i,y_i)\right]}{\sum_if(x-x_i,y-y_i)}$$

## 7.8.1 滤波函数

```cpp
class Filter {
  public:
    // Filter Interface
    virtual ~Filter();
    Filter(const Vector2f &radius)
        : radius(radius), invRadius(Vector2f(1 / radius.x, 1 / radius.y)) {}
    virtual Float Evaluate(const Point2f &p) const = 0;

    // Filter Public Data
    const Vector2f radius, invRadius;
};
```

代表滤波函数的接口内容包括一个 `Evaluate` 虚函数和代表截止半径的一个值，任何超过截止半径的输入均会返回零。

### Box Filter

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/box-filter.svg" style="max-height: 20vh; margin: 10px 0"/></center>

简单的阶梯函数，代表在截止半径内的所有样本权重都一样

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/box-recon-b.svg" style="max-height: 25vh; margin: 10px 0"/></center>

### Triangle Filter

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/triangle-filter.svg" style="max-height: 20vh; margin: 10px 0"/></center>

比 BoxFilter 稍微复杂一点，用简单的直线表示越边缘的样本权值越低

### Gaussian Filter

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/gaussian-filter.svg" style="max-height: 20vh; margin: 10px 0"/></center>

使用高斯函数表示样本对中心的贡献值。这种滤波方式与其它相比会对图像带来少量的模糊，但这些模糊也可以帮助减少走样的视觉影响。

在实际实现中，高斯函数被建模为以下形式：

$$f(x,y) = \max(0, e^{-\alpha x^2} - e^{-\alpha r_x^2}) \cdot \max(0, e^{-\alpha y^2} - e^{-\alpha r_y^2})$$

### Mitchell Filter

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/mitchell-filter.svg" style="max-height: 20vh; margin: 10px 0"/></center>

为了平衡滤波中的模糊和幻影问题，Mitchell 和 Netravali 发明了一种参数化的滤波函数。这种函数的特别之处在于在接近截止位置边缘的地方它的权值是负的，这一特性使得它可以在图像中的边缘位置获得更加锐利的结果。

一维的 Mitchell 函数表达式如下，它由两个参数调控，并且需要保证 $C^0,C^1$ 连续性：

$$f(x) = \frac{1}{6} \times \begin{cases}(12-9 B-6 C)|x|^{3}+(-18+12 B+6 C)|x|^{2}+(6-2 B) & |x|<1 \\ (-B-6 C)|x|^{3}+(6 B+30 C)|x|^{2}+(-12 B-48 C)|x|+(8 B+24 C) & 1 \leq|x|<2 \\ 0 & \text { otherwise. }\end{cases}$$

最终的求值函数如下：

```cpp
Float MitchellFilter::Evaluate(const Point2f &p) const {
    return Mitchell1D(p.x * invRadius.x) * Mitchell1D(p.y * invRadius.y);
}
```

### Windowed Sinc Filter

Windowed Sinc 函数（下图蓝线）实际上是一个 $\sin$ 函数与一个窗口函数相乘的滤波函数，其中一种窗口函数 Lanczos Window 其实就是另一个缩放到区域上的 $\sin$  函数（如下图黄线），其表达式为：

$$w(x) = {\sin(\pi x / \tau) \over \pi x / \tau}$$

<center><img src="https://pbr-book.org/3ed-2018/Sampling_and_Reconstruction/sinc-and-window.svg" style="max-height: 20vh; margin: 10px 0"/></center>

# 7.9 Film and the Imaging Pipeline

在 PBRT 中，`Film` 类型建模了模拟相机的接收设备。在得到了每个光线样本的 radiance 后，它负责确定各个光线对成像平面上的各个像素的贡献。这个类同样在渲染流程结束后负责将最终图像储存到图片中。

## 7.9.1 Pixel 和 Film 类

一张图像是由数个像素构成的，一个 `Pixel` 结构体中包含了这一像素中的 XYZ 颜色值（加权和）、滤波权值之和、一个不加权的样本面元 XYZ 颜色值之和，以及一个用于将结构体大小对齐至整数个 cache line 大小的 pad 元素。

在构建 `Film` 实例时，需要传入的内容包括：

1. 图像分辨率与着色剪切区域（这二者会用于计算生成对应数量的 pixel 数组）
2. 滤波函数指针，感受器的物理大小
3. 储存的文件名与储存时数据的缩放比例

由于在对光线进行滤波的时候每次都计算对应位置的滤波函数值会带来很大的消耗，在本类的构造函数中同样会初始化一个储存了预计算的滤波值的表。

## 7.9.2 将像素值传入 Film 实例

`Film` 类型定义了一个用于给不同线程提供部分的区域进行渲染的方法。各个线程会将像素值首先写入对应的 `FilmTile` 代表的区块中，并在结束后统一合并至 `Film` 中。具体流程为：

1. 渲染前，每个线程会使用自身的渲染区域、结合滤波函数的截止宽度使用 `Film::GetFilmTile` 确定一块临时的图像区域
2. 渲染中，使用 `FilmTile::AddSample` 向这个线程中的图像子区域写入像素的 radiance 值，这个函数完成滤波的步骤
3. 渲染后，使用 `Film::MergeFilmTile` 将子区域合并入最终结果中。

一些光线传输方法（如 BDPT ）需要将一个样本的贡献不经加权地分摊到任意的像素点上。因此 Film 中也提供了可供多线程共享访问的 `Pixel::splatXYZ` 和 `Film::AddSplat` 方法以供这些算法操作。

## 7.9.3 输出图像

在主渲染流程结束后，`Integrator` 中的渲染函数就会调用 `Film::WriteImage` 方法。这个方法主要包括两个部分：

1. 将图像转换为 RGB 颜色空间并计算最终的颜色值，具体而言，对于每个像素有：
    1. 将 XYZ 颜色转换为 RGB 颜色
    2. 使用权值和归一化颜色值
    3. 将 splat 信息转换颜色空间并使用参数 splatScale 累积到 RGB 颜色中
    4. 使用 scale 值缩放颜色值
2. 将图像写入文件