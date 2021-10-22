---
title: 《深度探索C++对象模型》第二章笔记 | Notes for Inside Cpp Object Model Chapter 02
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

# 第二章 - 构造函数语义学 | The Semantics of Constructors

## 2.1 Default Constructor

C++ 标准称，对于一个没有任何由用户指定的构造函数的类，会有一个 default constructor 在编译器需要时被隐式地声明出来，这个 constructor 常常是没有任何作用的，例外是以下四种情况：

1. 当这个类拥有带有 default constructor 的 member class object 时
2. 当这个类的父类带有 default constructor 时
3. 当这个类声明或继承了一个虚函数时（自动生成 vtbl 和 vptr 并嵌入类中）
4. 当这个类派生自一个具有 virtual base class 的继承链时（类似虚指针的处理方式）

## 2.2 Copy Constructor

在以下三种情况下，会以一个 object 的内容作为另一个 class object 的初值：

1. 使用一个 object 显式的给一个类变量初始化
2. 函数的传入值是一个类
3. 函数的返回值是一个类

### 2.2.1 Default Memberwise Initialization

当一个类不存在显式的 copy constructor 时，当 class object 以相同类型的另一个 object 作为初始值时，内部会使用 default memberwise initialization 初始化

其内部的 data member 会从原始 object 中拷贝除了 member class object 外的所有内容，并使用递归的方法处理 member class object

### 2.2.2 Bitwise Copy Semantics

一个类是否具有 bitwise copy semantics 决定了一个由编译器生成的 copy constructor 是否是有效用的。

在以下情况下，一个类不具备 bitwise copy semantics：

1. 它内含一个存在 copy constructor 的 member class object 
2. 它继承于一个存在 copy constructor 的基类
3. 它声明了一个或多个 virtual functions
4. 它继承自一个含有 virtual base class 的继承链

### 2.2.3 vptr 的重新设定

当一个类被相同类型的 object 初始化时，vptr 的初始化可以直接拷贝，而当其由一个派生类或父类的 object 初始化时，则需要将 vptr 重定向会本类对应的 vtbl

### 2.2.4 Virtual Base Class Subobject 的处理

当一个类以其派生类的 object 初始化时会引起虚继承的问题，其 virtual base class 的位置需要重新定位，因此不能直接使用 bitwise copy 的操作

## 2.3 程序转化语义学

### 2.3.1 明确的初始化操作

这一部分的程序转换会将定义和初始化操作（调用 copy constructor）将被分离

### 2.3.2 参数的初始化

将一个 class object 作为参数传入函数时，相当于一个初始化操作，一般而言会通过以下两步实现：

1. 构建一个临时变量，调用 copy constructor 初始化它
2. 改写函数声明，将临时变量的引用传入函数

### 2.3.3 返回值的初始化

这一步在一开始是通过一个双阶段转化形成的：

1. 将返回对象的 reference 加入函数的传入值
2. 在返回前使用函数的返回值调用该 reference 的 copy constructor

这一点上有两种优化方式

1. 程序员优化：程序员使用一个 constructor 调用作为返回值，编译器会直接将这个 constructor 调用应用于传入的返回值引用上
2. 编译器优化（NRV named retrun value）：对返回的具名变量实施编译器层面的优化，将该函数中的该变量直接替换成返回值的引用

## 2.4 成员初始化列表（member initialization list）

在以下情况下，必须使用成员初始化列表：

1. 初始化一个 reference member
2. 初始化一个 const member
3. 调用 base class 或 member class 的含参 constructor 时

编译器会一一操作 initialization list，以适当的次序（一般是变量的声明次序）在 constructor 的显式用户代码前插入初始化代码