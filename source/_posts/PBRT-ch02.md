---
title: PBRT 第二章笔记（基础篇） | Notes for PBRT Chapter 02 - Geometry and Transforms (basis)
date: 2021-10-22 02:07:09
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

# 2.1 Coordinate Systems

PBRT 使用常规的三维坐标系

## 2.1.1 坐标系的手性

将手的大拇指、食指和中指比作一个三维直角坐标系，并以大拇指作为 x 轴，食指作为 y 轴，中指作为 z 轴即可确定坐标系的手性

PBRT 使用的坐标系类型为左手系。

# 2.2 Vectors

PBRT 拥有两种基础的向量模板类，虽然可以利用含维度参数的单一模板来定义向量，但为了可以使用常用的 `v.x, v.y` 取值方式， PBRT 选择为不同维度的向量使用不同的模板

```cpp
template <typename T> class Vector2 { ... };
template <typename T> class Vector3 { ... };
```

一些常用的向量类型定义如下：

```cpp
typedef Vector2<Float> Vector2f;
typedef Vector2<int>   Vector2i;
typedef Vector3<Float> Vector3f;
typedef Vector3<int>   Vector3i;
```

向量模板类包括了常用的操作：

- 加减乘除
- 点乘、叉乘（注意叉乘结果与坐标系类型无关）
- 正则化
- 获取最小、最大值和最小、最大值的位置
- 从一个向量构建局部坐标系

# 2.3 Points

与向量相似，PBRT 提供了两种基础的点模板类和若干

```cpp
template <typename T> class Point2 { ... };
template <typename T> class Point3 { ... };
typedef Point2<Float> Point2f;
typedef Point2<int>   Point2i;
typedef Point3<Float> Point3f;
typedef Point3<int>   Point3i;
```

PBRT 将点和向量严格区分，并为点类提供了以下函数：

- 与向量的互相转化（但需要 explicit cast ）
- 加减某向量得到新的点
- 通过减法得到向量
- 数乘和点之间的相加（用于加权等数学操作）
- 求两向量的距离和距离的平方
- 对两个向量进行线性插值
- component-wise 最大最小值和取整
- 对元素进行重排列（`Permute()` 函数）

# 2.4 Normals

由于法线是被定义在平面上的，其表现可能与普通的向量有所不同，因此 PBRT 使用了独立的法线模板类

```cpp
template <typename T> class Normal3 { ... }；
typedef Normal3<Float> Normal3f;
```

一部分法线的操作和向量相同，并且提供了法线、向量混合类型的支持，包括了：

- `Dot(), AbsDot()`
- 数学运算
- 与向量互相转换的构造函数

法线也有一些特殊的函数：

- 计算在给定向量方向半球内的法线的 `Faceforward()`

# 2.5 Rays

```cpp
class Ray {
public:
    // Ray Public Methods
    ...
    // Ray Public Data
	Point3f o;
    Vector3f d;
    mutable Float tMax;
    Float time;
    const Medium *medium;
};
```

光线主要由一个起始点 `o` 和一个方向向量 `d` 构成，并且含有一个表示最大长度的可变变量 `tMax` 

除此之外，光线本身还被附加了和场景动画关联的时间 `time` 和与当前所在介质关联的介质指针 `medium`

光线类同时给出了包括计算光线位置、打印光线等功能的函数

## 2.5.1 光线微分（Ray Differentials）

为了赋予光线更多的信息，PBRT 基于 `Ray` 类和两条额外的辅助光线实现了光线的微分

```cpp
class RayDifferential : public Ray {
public:
    // RayDifferential Public Methods
    ...
    // RayDifferential Public Data
    bool hasDifferentials;
    Point3f rxOrigin, ryOrigin;
    Vector3f rxDirection, ryDirection;
};
```

由于 `RayDifferential` 类继承自 `Ray` 类，大部分接口可以直接使用该类，并且只在需要微分信息的地方调用它

# 2.6 Bounding Boxes

PBRT 使用 Axis Aligned Bounding Box (AABB) 加速运行，为此设计了储存包围盒的类型：

```cpp
template <typename T> class Bounds2 { ... };
template <typename T> class Bounds3 { ... };
typedef Bounds2<Float> Bounds2f;
typedef Bounds2<int>   Bounds2i;
typedef Bounds3<Float> Bounds3f;
typedef Bounds3<int>   Bounds3i;
```

一个包围盒可以由对角线上的两个点 `pMix, pMax` 表示如图

<center><img src="https://pbr-book.org/3ed-2018/Geometry_and_Transformations/AABB.svg" style="max-height: 25vh; margin: 10px;"/></center>

包围盒可以初始化为整个空间、一个点或根据给定的两点确定包围盒，它同样重载了中括号函数以方便访问八个角点的位置

其它工具函数包括：

- 合并或相交两个包围盒
- 判断点是否在包围盒内（含边界或仅含下边界）
- 使用一个固定的 margin 拓展包围盒
- 获取从极小点指向极大点的对角线向量
- 获取表面积与体积
- 获取最长边的序号
- 在盒内的单位坐标中进行线性插值得到点坐标，或给定点求盒内坐标
- 获取包围盒的包围球的中心和半径

# 2.7 Transformations

在 PBRT 中的变换是一种点到点、向量到向量的映射，PBRT 主要使用的变换需要满足以下条件：

1. 线性 $T(sv) = sT(v), T(v_1+v_2) = T(v_1)+T(v_2)$
2. 连续性
3. 可逆性

PBRT 使用了 $4 \times 4$ 的矩阵表示线性变换

## 2.7.1 齐次坐标

PBRT 中的三维齐次坐标使用一个包括参考点、三轴方向的四元组 $(v_x,v_y,v_z,p_0)$ 表示一个参考系，在参考系中的向量和点均被表示为一个四维向量，表示方式为：

- 向量：$v = [s_x, s_y, s_z, 0]$
- 点：$p = [s_x, s_y, s_z, w]$

将点从齐次坐标转换为常规三维坐标只需用前三个分量除以第四个分量

$$(x,y,z,w) \to ({x \over w}, {y \over w}, {z \over w})$$

在此表示方式下，一个齐次坐标系可以表示为一个 $4 \times 4$ 的矩阵，将坐标系中的向量或点转换到原始坐标系均可以通过将此向量和该矩阵相乘得到

使用矩阵的良好性质定义的变换类如下：

```cpp
class Transform {
public:
    // Transform Public Methods
    ...
private:
    // Transform Private Data
    Matrix4x4 m, mInv;
    friend class AnimatedTransform;
    friend struct Quaternion;
};
```

为了避免在多个 Shape 中储存同样的 Transform 实例，物体的变换由一个指向需要的 Transform 的指针表示，而所有的 Transform 实例均储存在在 TransformCache 中

## 2.7.2 基本操作

- 构造函数
    - 初始化为单位矩阵（等价变换）
    - 用数组和矩阵初始化
- 构造逆变换（通过交换两个矩阵）
- 构造转置变换

## 2.7.3 平移变换

平移变换矩阵如下所示：

$$\left(
\begin{matrix}
    1 & 0 & 0 & \Delta x\\ 
    0 & 1 & 0 & \Delta y\\
    0 & 0 & 1 & \Delta z\\
    0 & 0 & 0 & 1
\end{matrix}
\right)$$

## 2.7.4 伸缩变化

伸缩变换矩阵如下所示：

$$\left(
\begin{matrix}
    x & 0 & 0 & 0\\ 
    0 & y & 0 & 0\\
    0 & 0 & z & 0\\
    0 & 0 & 0 & 1
\end{matrix}
\right)$$

## 2.7.5 基于坐标轴的旋转

$$R_x(\theta) = 
\left(
\begin{matrix}
    1 & 0 & 0 & 0\\ 
    0 & \cos\theta & -\sin\theta & 0\\
    0 & \sin\theta & \cos\theta & 0\\
    0 & 0 & 0 & 1
\end{matrix}
\right)$$

$$R_y(\theta) = 
\left(
\begin{matrix}
    \cos\theta & 0 & \sin\theta & 0\\
    0 & 1 & 0 & 0\\
    -\sin\theta & 0 & \cos\theta & 0\\
    0 & 0 & 0 & 1
\end{matrix}
\right)$$

$$R_z(\theta) = 
\left(
\begin{matrix}
    \cos\theta & -\sin\theta & 0 & 0\\
    \sin\theta & \cos\theta & 0 & 0\\
    0 & 0 & 1 & 0\\
    0 & 0 & 0 & 1
\end{matrix}
\right)$$

## 2.7.6 基于任意旋转轴的旋转

<center><img src="https://pbr-book.org/3ed-2018/Geometry_and_Transformations/Rotate%20arbitrary%20axis.svg" style="max-height: 25vh; margin: 10px;"/></center>

由给定向量和旋转轴可以构成一个局部坐标系，在局部坐标系下旋转并利用变换的合成方法结合即可，PBRT 中的代码如下

```cpp
Transform Rotate(Float theta, const Vector3f &axis) {
    Vector3f a = Normalize(axis);
    Float sinTheta = std::sin(Radians(theta));
    Float cosTheta = std::cos(Radians(theta));
    Matrix4x4 m;
    // Compute rotation of first basis vector
    m.m[0][0] = a.x * a.x + (1 - a.x * a.x) * cosTheta;
    m.m[0][1] = a.x * a.y * (1 - cosTheta) - a.z * sinTheta;
    m.m[0][2] = a.x * a.z * (1 - cosTheta) + a.y * sinTheta;
    m.m[0][3] = 0;

    // Compute rotations of second and third basis vectors
    m.m[1][0] = a.x * a.y * (1 - cosTheta) + a.z * sinTheta;
    m.m[1][1] = a.y * a.y + (1 - a.y * a.y) * cosTheta;
    m.m[1][2] = a.y * a.z * (1 - cosTheta) - a.x * sinTheta;
    m.m[1][3] = 0;
    m.m[2][0] = a.x * a.z * (1 - cosTheta) - a.y * sinTheta;
    m.m[2][1] = a.y * a.z * (1 - cosTheta) + a.x * sinTheta;
    m.m[2][2] = a.z * a.z + (1 - a.z * a.z) * cosTheta;
    m.m[2][3] = 0;

    return Transform(m, Transpose(m));
}
```

## 2.7.7 Look-At 变换

这是一个常用的摄像机相关变换，给定摄像机的位置、观察方向和摄像机的上侧向量即可得到一个从世界坐标到以相机为中心的坐标系变换矩阵。

<center><img src="https://pbr-book.org/3ed-2018/Geometry_and_Transformations/Viewing%20transform.svg" style="max-height: 25vh; margin: 10px;"/></center>

由图中以及变换矩阵的定义可知：

- 矩阵的第 1 - 3 列分别是摄像机坐标系的三个方向
- 矩阵的第四列是摄像机中心坐标

# 2.8 Applying Transformations

## 2.8.1 - 2.8.2 点和向量的变换

这两种情况在上一部分中已经有过描述，直接进行矩阵 - 向量乘法即可

## 2.8.3 法线的变换

由于变换均作用于物体，即切线上，不能直接变换法线。假设作用于法线的变换矩阵为 $S$ ，根据法线和切线 $t$ 在变换 $M$ 下的关系，有：

$$\begin{align}
    0 & = (n')^Tt'\\
      & = (Sn)^T(Mt)\\
      & = n^T(S^TM)t\\
      & = n^Tt
\end{align}$$

因此对法线的变化相当于变换矩阵的逆的转置 $S = (M^{-1})^T$

## 2.8.4 光线的变换

相当于同时变换光线的起点和方向，而其它保持不变

## 2.8.5 包围盒的变换

在 PBRT 中，包围盒的变换相当于将包围盒的八个顶点处的极小包围盒合并起来：

```cpp
Bounds3f Transform::operator()(const Bounds3f &b) const {
    const Transform &M = *this;
    Bounds3f ret(M(Point3f(b.pMin.x, b.pMin.y, b.pMin.z)));    
    ret = Union(ret, M(Point3f(b.pMax.x, b.pMin.y, b.pMin.z)));
    ret = Union(ret, M(Point3f(b.pMin.x, b.pMax.y, b.pMin.z)));
    ret = Union(ret, M(Point3f(b.pMin.x, b.pMin.y, b.pMax.z)));
    ret = Union(ret, M(Point3f(b.pMin.x, b.pMax.y, b.pMax.z)));
    ret = Union(ret, M(Point3f(b.pMax.x, b.pMax.y, b.pMin.z)));
    ret = Union(ret, M(Point3f(b.pMax.x, b.pMin.y, b.pMax.z)));
    ret = Union(ret, M(Point3f(b.pMax.x, b.pMax.y, b.pMax.z)));
    return ret;
}
```

### EXERSICE：

通过将包围盒视为一个中心和一个偏移量的组合：

$$B = \left[
\begin{matrix}
    \min\left(
    \begin{matrix}
         c_x \pm r_x\\
         c_y \pm r_y\\
         c_z \pm r_z\\   
    \end{matrix}
    \right)
    &
    \max\left(
    \begin{matrix}
         c_x \pm r_x\\
         c_y \pm r_y\\
         c_z \pm r_z\\   
    \end{matrix}
    \right)
\end{matrix}
\right]$$

$$B' = \left[
\begin{matrix}
    \min\left(M\left(
    \begin{matrix}
         c_x \pm r_x\\
         c_y \pm r_y\\
         c_z \pm r_z\\
         1
    \end{matrix}
    \right)\right)
    &
    \max\left(M\left(
    \begin{matrix}
         c_x \pm r_x\\
         c_y \pm r_y\\
         c_z \pm r_z\\ 
         1
    \end{matrix}
    \right)\right)
\end{matrix}
\right]$$

其中可以通过矩阵分解与移动取最值的次序可得：

$$\min\left(M\left(
\begin{matrix}
    c_1 \pm r_1\\
    c_2 \pm r_2\\
    c_3 \pm r_3\\
    1
\end{matrix}
\right)\right)\\

=\min\left( 
\left(\sum_{i=1}^3M_{coli}(c_i \pm r_i)\right) + M_{col4} 
\right)\\

=\left(\sum_{i=1}^3\min(M_{coli}(c_i \pm r_i))\right) + M_{col4}$$


通过这种方法可以将计算的复杂度大大降低。

> Arvo, J. 1990. Transforming axis-aligned bounding boxes. In A. S. Glassner (Ed.), Graphics Gems I, 548–50. San Diego: Academic Press.
> 

## 2.8.6 变换的合成

变换的合成和矩阵的合成无异，遵从矩阵乘法的相关规律

```cpp
Transform Transform::operator*(const Transform &t2) const {
    return Transform(Matrix4x4::Mul(m, t2.m),
                     Matrix4x4::Mul(t2.mInv, mInv));
}
```

## 2.8.7 变换与坐标系的手性

部分变换会改变坐标系的手性（比如将单位变换中某一维度取反），这种特性可以通过对变换矩阵的非齐次部分（即新坐标系在老坐标系下的三个基）的行列式的符号得到。

当行列式值为负时，表示手性发生了转换

# 2.9 Animating Transformations

这部分包括了变换动画、四元数、变化的包围盒等内容，容我以后再看

# 2.10 Interactions

`SurfaceInteraction` 类表示了物体表面上的一点处的局部信息，如光线-物体的相交，而 `MediumInteraction` 类则用于表示光线在空间参与介质中某点处的散射情况，它们均从一个更一般的 `Interaction` 类中派生出来。

```cpp
struct Interaction {
    // Interaction Public Methods
    ...	
    // Interaction Public Data
    Point3f p;
    Float time;
    Vector3f pError;
    Vector3f wo;
    Normal3f n;
    MediumInterface mediumInterface;
};
```

其成员变量的含义如下：

- 一个作用的发生需要空间中的一个位置 `p` 和时间 `t` ，`pError` 则给出了一个保守的有关该作用发生位置的错误边界以解决浮点误差问题（见 3.9 部分）
- 在光线上发生的作用则使用 `wo` 表示光线来源的方向
- 在表面的作用使用 `n` 表示法线
- 在介质中的作用使用 `mediumInterface` 表示介质

## 2.10.1 表面作用（Surface Interaction）

物体表面某点处的几何信息使用 `SurfaceInteraction` 类表示，这层抽象让大部分系统在处理表面时可以不关注不同几何体类型的实现。这一类型的定义如下：

```cpp
class SurfaceInteraction : public Interaction {
public:
    // SurfaceInteraction Public Methods
	...
    // SurfaceInteraction Public Data
    Point2f uv;
    Vector3f dpdu, dpdv;
    Normal3f dndu, dndv;
    const Shape *shape = nullptr;
    struct {
        Normal3f n;
        Vector3f dpdu, dpdv;
        Normal3f dndu, dndv;
    } shading;
    const Primitive *primitive = nullptr;
    BSDF *bsdf = nullptr;
    BSSRDF *bssrdf = nullptr;
    mutable Vector3f dpdx, dpdy;
    mutable Float dudx = 0, dvdx = 0, dudy = 0, dvdy = 0;
};
```

<center><img src="https://pbr-book.org/3ed-2018/Geometry_and_Transformations/Local%20differential%20geometry.svg" style="max-height: 25vh; margin: 10px;"/></center>

除了交点和交点法线外，这一类型中还储存了贴图位置 uv 、交点和法线关于 uv 的偏导数。

由于一个表面的几何信息可能由 bump-mapping 或顶点插值等方法生成，PBRT 在 `SurfaceInteraction` 类中储存了第二套表面信息，并用一个结构体给出。这里的信息可能在后续过程中被修改或设置，如下面这个函数：

```cpp
void SurfaceInteraction::SetShadingGeometry(
    	const Vector3f &dpdus,
        const Vector3f &dpdvs, 
    	const Normal3f &dndus,
        const Normal3f &dndvs, 
    	bool orientationIsAuthoritative) {
    // Compute shading.n for SurfaceInteraction 
    shading.n = Normalize((Normal3f)Cross(dpdus, dpdvs));
    if (shape && (shape->reverseOrientation ^
                  shape->transformSwapsHandedness))
        shading.n = -shading.n;
    if (orientationIsAuthoritative)
        n = Faceforward(n, shading.n);
    else
        shading.n = Faceforward(shading.n, n);
    // Initialize shading partial derivative values
    shading.dpdu = dpdus;
    shading.dpdv = dpdvs;
    shading.dndu = dndus;
    shading.dndv = dndvs;
}
```

之所以对于法线的方向进行了一系列的调整，是因为在 PBRT 中物体法线的方向表示了物体的正向 / 外侧，而会改变坐标系手性的变换在作用于物体上会将其外侧的方向一起改变，因此需要通过传入的一个布尔值标识判断最终的法线方向取决于原始信息还是通过传入值计算的着色用法线。