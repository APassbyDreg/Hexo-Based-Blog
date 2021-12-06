---
title: 基于 Optix 的渲染器框架 v0.2 | Optix Based Renderer Framework v0.2
date: 2021-12-06 21:46:03
categories: 
- programming
- projects
tags: 
- CG
- render
toc: true
---

## 简介

本项目是一个基于 Optix 7.3 的 GPU 光线追踪渲染框架，在保障了一定的可拓展性的同时，利用 NVIDIA 光线追踪技术提高了渲染的效率。

> 本页面仅为 v0.2 版本的介绍，旧版本介绍见[此页面](/2021/10/OptixBasedRendererFramework/)

## 渲染样例

<center><img src="cornell-box-new.png" style="max-height: 40vh"></center>

## 更新日志

### 架构更新

1. 修复了部分内存管理问题
2. 使用智能指针代替裸指针
3. 更改了 `derived instances` 储存的位置，现在由各个类管理它们各自的 `instances` 和 `derived classes`
4. 增加了 instancing 机制，您现在可以复用同一模型，描述文件的语法也因此发生了改变
5. 配合 instancing 机制更改了渲染流程，现在 `Hit Program` 由单独的 `Integrator` 类管理，原来的 `Mesh` 则抽象出从一个 `HitData` 生成 `SurfaceData` 的接口，转移到 `Direct Callable` 中

### 内容更新

1. 修复了数个渲染问题
2. 将部分材质的常数参数接口改为贴图接口
3. 增加了玻璃材质，使用非导体的菲涅尔函数计算反射与折射

## 附录

### 新的场景结构与渲染流程

![scene_structure.svg](scene_structure.svg)

> *注：其中的 MatrixMotionTransform 暂时还没有实现，目前可以通过指定 instance 的 transform 来实现不同 instance 的分离*

## 相关链接

代码仓库：[https://gitee.com/martin_z_he/optix-based-renderer](https://gitee.com/martin_z_he/optix-based-renderer)

介绍文档：[https://gitee.com/martin_z_he/optix-based-renderer/raw/master/doc/presentations/presentation-20210830.pdf](https://gitee.com/martin_z_he/optix-based-renderer/raw/master/doc/presentations/presentation-20210830.pdf)

