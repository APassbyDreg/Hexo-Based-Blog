---
title: 《深度探索C++对象模型》第一章笔记 | Notes for Inside Cpp Object Model Chapter 01
date: 2021-10-22 13:11:58
categories:
- programming
- reading notes
tags:
- C++
- OOP
- 深度探索C++对象模型
toc: true
---

[[toc]]

# 第一章 - 关于对象 | Object Lessons

## 1.1 C++ 对象模式

1. non-static data members 被存放在每个 class object 中
2. static members 和 non-static function members 被存放在所有 class object 外
3. 每个 class 产出一个指向 virtual functions 的虚函数表（vtbl），并为每个 class object 添加一个指向相关的虚函数表的指针（vptr）
   
    > class 相关的 type_info 信息也在 vtbl 中，通常存放于第一个 slot
    > 
4. 每个 class object 包含一个指向 base classes 表的指针（bptr）

<center><img src="example.png" style="max-height: 40vh"/></center>


## 1.2 关键词差异

一般来说不去碰 struct ，除非是纯数据结构或涉及传入已编译的 C 函数的情况

## 1.3 对象的差异

### 1.3.1 编程模式

1. 程序模型（procedural model）：C 风格编程，使用单独的数据结构和函数集
2. 抽象数据类型模型（ADT model）：通过提供公共的表达式接口实现抽象的功能，如重载的运算符等，数据的类型可以在编译器获得
3. 面向对象模型（OO model）：涉及多态等内容，通常通过引用和指针处理，有时需要支持运行时类型判断

### 1.3.2 指针的类型

在每个执行点，指针所指的 object 类型决定了函数调用的实体，这种类型信息并不维护于指针中，而是维护于 vptr 与所指的 vtbl 的链接中

### 1.3.3 指针操作与对象操作的区别

在面向对象模式中，当对指针或引用进行操作时，实际上改变的是它们对指向的内存的解释方式，而操作对象则会引起与类型相关的内存操作

> 因此，在 OO 模式下将一个 derived 类对象赋值给一个 base 类对象时会引起裁切，vptr 将会指向基类，而派生类相关的信息均会消除
> 
> 从另一种角度说，在 OO 模式的指导下编译器会根据类型自动指定 vptr ，而不随程序员的类赋值操作改变（除非重载了赋值函数或采用 C 风格的直接内存操作）