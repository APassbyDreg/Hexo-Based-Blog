---
title: 《游戏编程模式》第二章笔记 | Notes for Game Programming Patterns Chapter 02
date: 2022-03-15 20:46:55
categories: 
- programming
- reading notes
tags:
- OOP
- Game Programming Patterns
toc: true
---

# 第二章：序列模式

大多数游戏世界都有的特性是时间——虚构世界以其特定的节奏运行。 作为世界的架构师，我们必须发明时间，制造推动游戏时间运作的齿轮。

本篇的模式是建构这些的工具。 游戏循环是时钟的中心轴。 对象通过更新方法来聆听时钟的滴答声。 我们可以用双缓冲模式存储快照来隐藏计算机的顺序执行，这样看起来世界可以进行同步更新。

## 双缓冲模式

<embed src="./双缓冲模式 · Sequencing Patterns · 游戏设计模式.pdf" type="application/pdf" width="100%" height="600px">

## 游戏循环

<embed src="./游戏循环 · Sequencing Patterns · 游戏设计模式.pdf" type="application/pdf" width="100%" height="600px">

## 更新方法

<embed src="./更新方法 · Sequencing Patterns · 游戏设计模式.pdf" type="application/pdf" width="100%" height="600px">