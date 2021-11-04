---
title: PBRT 第六章笔记 | Notes for PBRT Chapter 06 - Camera Models (basis)
date: 2021-11-04 22:21:01
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 6.1 Camera Model

```cpp
class Camera {
  public:
    // Camera Interface
    Camera(const AnimatedTransform &CameraToWorld, Float shutterOpen,
           Float shutterClose, Film *film, const Medium *medium);
    virtual ~Camera();
    virtual Float GenerateRay(const CameraSample &sample, Ray *ray) const = 0;
    virtual Float GenerateRayDifferential(const CameraSample &sample,
                                          RayDifferential *rd) const;
    virtual Spectrum We(const Ray &ray, Point2f *pRaster2 = nullptr) const;
    virtual void Pdf_We(const Ray &ray, Float *pdfPos, Float *pdfDir) const;
    virtual Spectrum Sample_Wi(const Interaction &ref, const Point2f &u,
                               Vector3f *wi, Float *pdf, Point2f *pRaster,
                               VisibilityTester *vis) const;

    // Camera Public Data
    AnimatedTransform CameraToWorld;
    const Float shutterOpen, shutterClose;
    Film *film;
    const Medium *medium;
};
```

相机的抽象基类如上图所示。为了构建一个 `Camera` 衍生类的对象，你需要向父类传入的参数包括：

- 摄像机坐标到世界坐标的变换，表示相机在世界坐标内的位置
- 记录了快门开启和关闭时间的两个浮点数，可以用于生成带有运动模糊的场景
- 用于记录图像的 `Film` 指针
- 表示了摄像机周围介质的 `Medium` 指针

Camera 类型中最重要的函数即是 `GenerateRay, GenerateRayDifferential` 两个生成光线的函数。它们均会利用 `CameraSample` 中的信息生成对应的光线（或微分光线），并返回该光线对应的权值。一般而言，这个权值会是 1 ，但对于另外一些考虑了镜头特性的系统，它可能会发生变化。

## 6.1.1 摄像机坐标系

在这个坐标系中，相机的 $z$ 轴被作为相机镜头指向的方向， $y$ 轴作为镜头的上方。使用这种方法可以简单地比较物体在摄像机坐标系中的 $z$ 值来判断它是否被其他物体遮挡。

# 6.2 Projective Camera Models

<center><img src="https://pbr-book.org/3ed-2018/Camera_Models/Camera%20coordinate%20spaces.svg" style="max-height: 30vh; margin: 10px"/></center>

CG 中最基础的问题之一就是如何将三维空间投影到二维平面表示。在大部分解决方案中，均使用了一个 $4 \times 4$ 的投影变换矩阵将三维空间的一部分区域投影到 Normalized device coordinate (NDC) 空间中。这个空间是一个单位立方体（虽然有的地方可能使用的坐标范围是 $[-1, 1]$ ），通过这个空间的 xy 轴信息即对应了屏幕上的 uv 坐标。

作为这类摄像机的基类的 `ProjectiveCamera` 类，在 `Camera` 类的基础上增加了镜头半径、视窗大小、对焦距离等参数。在这个类型中有三种空间：

- 相机空间：以相机为中心的坐标系
- 屏幕空间：由输入的 `screenWindow` 决定的屏幕 xy 坐标范围包围盒空间，一般指的是 NDC 空间
- 光栅（Raster）空间：由像素的实际索引坐标组成的包围盒空间

```cpp
class ProjectiveCamera : public Camera {
  public:
    // ProjectiveCamera Public Methods
    ProjectiveCamera(const AnimatedTransform &CameraToWorld,
                     const Transform &CameraToScreen,
                     const Bounds2f &screenWindow, Float shutterOpen,
                     Float shutterClose, Float lensr, Float focald, Film *film,
                     const Medium *medium)
        : Camera(CameraToWorld, shutterOpen, shutterClose, film, medium),
          CameraToScreen(CameraToScreen);

  protected:
    // ProjectiveCamera Protected Data
    Transform CameraToScreen, RasterToCamera;
    Transform ScreenToRaster, RasterToScreen;
    Float lensRadius, focalDistance;
};
```

## 6.2.1 正交投影相机

正交投影相机相机的视锥是一个长方体：

<center><img src="https://pbr-book.org/3ed-2018/Camera_Models/Ortho%20generate%20ray.svg" style="max-height: 25vh; margin: 10px"/></center>

这种摄像机的屏幕空间到光栅空间的换算异常简单，只需要将边长匹配并忽略 $z$ 轴即可。生成光线时，则会从近平面上对应的位置生成一个平行于 $z$ 轴的光线。

## 6.2.2 透视投影相机

透视投影相机的视锥是一个锥体，它由一个表示视口左右端向量夹角的浮点数 FOV 和远近平面的距离组成（见本章图一）。由于对于任意位置的不同平面，其相似比率均和其 $z$ 值正比。为了将视锥空间映射到 NDC 空间中，不同轴向的比例和对应的矩阵为：

$$\begin{matrix}
  x' = & x / z\\
  y' = & y / z\\
  z' = & {far(z - near) \over z(far - near)}
\end{matrix} , \ \left(\begin{matrix}
  1 & 0 & 0 & 0\\
  0 & 1 & 0 & 0\\
  0 & 0 & {f \over f-n} & -{fn \over f-n}\\
  0 & 0 & 1 & 0
\end{matrix}\right)$$

注意 $z$ 轴的变换并非是一个线性的变换，为了构造对应矩阵 $z'(z_0) + z'(z_1) \neq z'(z_0 + z_1)$ 。注意上述变换，在一个四维的齐次坐标乘以该变换后不难发现新的坐标的第四个参量就代表了原始的 $z$ 值，这在很多渲染中都是十分常用的保存线性的深度坐标的方法。

这种小孔成像的透视相机模型生成光线的方式也很简单，只要找到对应像素在任意一个平行于远、近平面的平面上的位置，构建一个从原点指向该方向的光线即可。

## 6.2.3 透镜模型和景深

以上的透视相机模型模拟的是一个小孔成像的相机类型。在实际生活中，光线一般会通过一个有限大小的光圈进入相机，并经过透镜折射到胶片上。这种相机模型将会在 6.4 节中详细讨论。

另一种建模这种现象的古典方法是 thin lens approximation 。这种方法假设相机的镜头是一个厚度相对于其曲率可以忽略的球形镜面。在这种模型中，平行的入射光将会汇聚在同一点上。特别地，平行于对称轴的光线将会汇聚在焦点上。

<center><img src="https://pbr-book.org/3ed-2018/Camera_Models/Thin%20lens.svg" style="max-height: 20vh; margin: 10px"/></center>

在更一般的情况下，对于在对称轴上的点，Gaussian Lens Equation 给出了其发出的光线经过透镜聚焦的位置：

$${1 \over z'} - {1 \over z} = {1 \over f}, \ z' = {zf \over z + f}$$

<center><img src="https://pbr-book.org/3ed-2018/Camera_Models/Circle%20of%20confusion%20diameter.svg" style="max-height: 32vh; margin: 10px"/></center>

失焦的点将在成像平面上投影出一个圆盘状的光斑（或取决于镜头特性的别的东西）。这个光斑的大小取决于相机的光圈、焦距和对焦平面的位置。在现实中，物体不一定需要准确的位于焦平面上才能拥有锋利的成像——只要上述提到的光斑的大小小于成像平面上一个像素的大小即可。这一整段在焦内的区域被称为景深。在几何上，我们很容易就能得到光斑的大小：

$$d_c = \frac{a}{z'} \times |z_f - z'| $$

在这种模型下采样光线需要增加额外的随机性。对于感受器上的某一点，会首先随机采样镜头上的任意位置，接着构建经过镜头该位置折射向感受器相应位置的光线。构建方式如下图所示：

<center><img src="https://pbr-book.org/3ed-2018/Camera_Models/Thin%20lens%20choose%20ray.svg" style="max-height: 40vh; margin: 10px"/></center>

中间的原点即代表 pinhole 相机的小孔成像位置，通过在对焦平面上找到对应位置，可知该位置发出的光线经过透镜的任意一点均会最终汇聚到感受器的那一点上，连接该点和之前的随机采样点即可得到光线方向。

# 6.3 **Environment Camera**

相当于一个全景相机，利用球坐标系的角度采样光线。二维的角度恰好可以映射到记录媒介的 uv 上：

```cpp
Float EnvironmentCamera::GenerateRay(const CameraSample &sample,
                                     Ray *ray) const {
    ProfilePhase prof(Prof::GenerateCameraRay);
    // Compute environment camera ray direction
    Float theta = Pi * sample.pFilm.y / film->fullResolution.y;
    Float phi = 2 * Pi * sample.pFilm.x / film->fullResolution.x;
    Vector3f dir(std::sin(theta) * std::cos(phi), std::cos(theta),
                 std::sin(theta) * std::sin(phi));
    *ray = Ray(Point3f(0, 0, 0), dir, Infinity,
               Lerp(sample.time, shutterOpen, shutterClose));
    ray->medium = medium;
    *ray = CameraToWorld(*ray);
    return 1;
}
```

# 6.4 真实相机 ⚠️

<center><img src="https://pbr-book.org/3ed-2018/Camera_Models/wide22-cross-section.svg" style="max-height: 30vh; margin: 10px"/></center>

这部分描述了真实世界中由多个镜片组构成的真实相机系统，容我以后再看。W