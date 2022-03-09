---
title: 基于 Optix 的渲染器框架 v0.3 | Optix Based Renderer Framework v0.3
date: 2022-01-07 17:19:17
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

> 本页面仅为 v0.2 版本的介绍，旧版本介绍见[此页面](/2021/10/OptixBasedRendererFramework-v0-2/)

## 渲染样例

<center>
    <img src="result-v0_3-1000spp-5min.png" style="max-height: 40vh">
    <br />
    <span>包含了 GGX 材质、玻璃材质、以及参与介质的渲染结果</span>
    <br />
    <span>1000spp，512 * 512，耗时约 5 分钟</span>
</center>

## 更新日志

- 增加了参与介质的渲染
- 从利用类似虚拟机制实现的 `Mesh, Light, Material` 等类型抽象出 `VirtualProgramEntry` 用于管理对象
- 重构了渲染管线，从递归式改为循环式
- 框架结构调整