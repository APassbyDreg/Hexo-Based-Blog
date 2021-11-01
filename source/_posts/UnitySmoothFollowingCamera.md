---
title: Unity 里的平滑跟踪相机 | Smooth Following Camera in Unity
date: 2021-11-01 00:44:03
categories:
- programming
- gamedev
tags:
- Unity
- GameLogic
toc: true
---

[[toc]]

<center style="padding: 10px 0" ><video src="sample.mp4" muted loop autoplay/></center>

在游戏中，你可能会经常碰到需要让摄像机跟踪某一角色的需求。当然，简单地将摄像机绑定到角色身上自然是最容易不过了，可这是否有一点不够 **COOOOOOL** ？本文描述了一种简单的平滑跟踪策略，让摄像机能够实现类似缓入缓出、刹车过冲的效果。这种方法同样可以运用于自动生成补间摄像机动画上。你可以在 [这里](SmoothCameraFollower.cs) 查看完整的源码。

# 一个公式说明原理

一个运动平滑的跟踪相机的本质是什么？我认为主要有三点：

1. 相机的速度函数应该是连续的，不能有急刹车的存在
2. 相机的视点（LookAt）需要尽可能贴近被摄物体
3. 相机的速度需要尽可能与被摄物体匹配

这三点每一条都看上去很直观，但实际上想要得出一个解析解难度并不算小（至少以我薄弱的数学基础真解不出来）。那么，当无法直接得到直观的有关位置的函数时，我们应该怎么办呢？在此我选择将上面的三个条件做一点转换：

1. 相机的速度函数处处可导（连续一定可导）
2. 相机的加速度应该与视点和被摄物体的距离成反比
3. 相机的加速度应该与摄像机速度和被摄物体速度之差成反比

有了以上三条公式，再将反比关系替换成一个简单的线性映射，加入两个参数，我们就得到了一个核心公式：

$$a = -w_0(p_o - p) - w_1(v_o - v)$$

或者，写成微分方程的形式：

$${\partial^2 p \over \partial t^2} = -w_0(p_o - p) - w_1(v_o - {\partial p \over \partial t})$$

当然，这个公式不止可以描述位置，它实际上可以在任意的地方生成较为平滑的插值。另一方面，将它应用在角度、偏移量等参数上也可以得到平滑的补间动画。使用这种系统的一个优势在于：不同于基于预设关键帧的动画，这种方式生成的动画可以在任意时间点从一个目标无缝转换到另一个目标上。

# 实现

有了这么简单的一个公式，实现起来自然并不困难。在每一次 `Update()` 调用中，你需要保存少数几个有关当前位置、速度的变量就能通过计算加速度、更新速度、从而得到更新后的摄像机参数。以下代码负责更新摄像机的目标点和速度，其他参数也是类似的过程。

```c#
{
    Vector3 accel = L0Weight * (objPos - camLookAt) + L1Weight * (objSpeed - camSpeed);
    Vector3 dv = accel * Time.deltaTime;
    camLookAt += (camSpeed + dv * 0.5f) * Time.deltaTime;
    camSpeed += dv;
}
```

在这个控制器中，主要的控制方式使用的是在以物体为中心的球坐标系上确定位置的方法，而非之前常用的欧拉角。默认情况下，摄像机的上方向是沿着 y 轴正方向的，但本控制器也提供了一个侧滚参数来表示摄像机以拍摄方向为轴上的滚动。这个特性不仅仅可以表示滚动，还可以在特殊情况下帮助摄像机进行正确的插值。

特例的一种在于，当摄像机垂直地从上向下拍摄时，默认的 y 轴正方向已经不能作为摄像机的上方了。在转过这一点时，随着朝向的改变，摄像机会瞬间旋转 180° 的朝向。这种突变是大部分情况下都不愿意看到的。本系统利用这个沿着拍摄方向的翻滚特性设计了一个在转过这一点处的平滑插值方案。你可以通过控制平滑过渡开始的位置以更好地规划摄像机动画。当然，如果你并不需要这一特性的话，也可以将 `interpThres` 设置为 0 以关闭它。

```c#
private Vector3 realRotationsToWorldUp(Vector3 dir)
{
    float a0 = Mathf.Deg2Rad * realDirections[0], a1 = Mathf.Deg2Rad * realDirections[1];
    float rot = Mathf.Deg2Rad * realRotation;

    Vector3 rotZ = dir.normalized;
    Vector3 rotX, rotY;
    if (Mathf.Abs(rotZ.y) < 1.0f)
    {
        Vector3 z = new Vector3(0, 1, 0);
        rotY = Vector3.Cross(z, rotZ).normalized;
        rotX = Vector3.Cross(rotZ, rotY);
    }
    else
    {
        rotX = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * -1;
        rotY = new Vector3(Mathf.Cos(a0), 0, -Mathf.Sin(a0)) * -1;
    }

    // smooth transition
    float diff = Mathf.Abs(realDirections[1] - 180 * Mathf.Floor(realDirections[1] / 180) - 90) / 90;
    if (diff < interpThres)
    {
        float interp = Mathf.Pow(1 - diff / interpThres, 2.5f);
        rot += Mathf.PI * interp * 0.5f * Mathf.Sign(Mathf.Cos(a1));
    }

    return rotX * Mathf.Cos(rot) + rotY * Mathf.Sin(rot);
}
```

# 微分方程的稳定性分析

受 3B1B 的 [这个关于微分方程可视化分析的视频](https://www.bilibili.com/video/BV1tb411G72z) 的启发，我尝试着在相空间中可视化了这个微分方程。

有关上述的微分方程，当被摄物体静止于某一点时，可以写成：

$${\partial^2 dp \over \partial t^2} = -w_0dp - w_1(v_o - {\partial dp \over \partial t}), \ dp = (p - p_0)$$

将整个空间的中心设为零点，以横坐标表示物体的位置、纵坐标代表速度构建相空间。下面这个工程可视化了在被摄物体静止不动的情况下，相机在不同的初始条件下，相空间中的向量场分布，以及其对应的运动轨迹。你可以通过它直观地体会在不同初始条件和参数下物体的位置和速度将会如何变化。

<center style="margin: 10px 0" ><video src="differetial-simulation.mp4" muted loop autoplay /></center>

# 一些问题

这个系统只是一个非常简单的微分系统，其中包含着不少问题。有的问题可以通过简单地修改代码得到改善，而有的问题则较难解决。

## 可能的优化

我目前有头绪的优化包括：

1. 对不同的参数设置不同的微分方程的权重，从而体现不同的敏感度
2. 通过修改插值的公式形式，可能获得更加平滑的过渡

## 潜在的不足

1. 参数较难调整：虽然每个参数的意义都颇为直观，但当你想要获得一个「能在固定时间由某初始情况收敛」的参数组是十分困难的
2. 对于获取的数值稳定性有一定要求：当输入的数值含有抖动的情况下，可能会出现一些鬼畜的情况（比如我在之前的一版测试中简单地在每次更新中给被摄物体加入一个固定的位移量，但由于更新的时间间隔并不统一，造成了速度计算中的反复横跳，结果非常鬼畜）



