---
title: 基于 Optix 的渲染器框架 | Optix Based Renderer Framework
date: 2021-10-20 13:49:29
categories: 
- programming
- projects
tags: 
- CG
- render
- OBR
toc: true
---

## 简介

本项目是一个基于 Optix 7.3 的 GPU 光线追踪渲染框架，在保障了一定的可拓展性的同时，利用 NVIDIA 光线追踪技术提高了渲染的效率。

> 新版本已发布，详见[新版本介绍页面](/2021/12/OptixBasedRendererFramework-v0-2/)

## 渲染样例

<style>
summary {
    cursor: pointer;
    transition: all ease 0.2s;
}
summary:hover {
    font-weight: bold;
    background-color: rgba(0, 0, 0, 0.05);
}
</style>

<details>
<summary>> Simple Mirror</summary>
    <center><img src="simple-mirror.png"></center>
</details>

<details>
<summary>> Cornell Box</summary>
    <center><img src="cornell-box.png"></center>
</details>

<details>
<summary>> GGX Grid</summary>
    <center><img src="ggx-grid.png"></center>
</details>

## 相关链接

代码仓库：[https://gitee.com/martin_z_he/optix-based-renderer](https://gitee.com/martin_z_he/optix-based-renderer)

介绍文档：[https://gitee.com/martin_z_he/optix-based-renderer/raw/master/doc/presentations/presentation-20210830.pdf](https://gitee.com/martin_z_he/optix-based-renderer/raw/master/doc/presentations/presentation-20210830.pdf)

## 附录

### 框架结构总览

![](overall.svg)

### 框架渲染流程

![](render_pipeline.svg)