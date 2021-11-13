---
title: 《深度探索C++对象模型》第五章笔记 | Notes for Inside Cpp Object Model Chapter 05
date: 2021-11-08 17:44:35
categories:
- programming
- reading notes
tags:
- C++
- OOP
- 深度探索C++对象模型
toc: true
---

# 5.1 无继承情况下的对象构造

## Plain Old Data (POD)

> A Plain Old Data Structure in C++ is an aggregate class that contains only PODS as members, has no user-defined destructor, no user-defined copy assignment operator, and no nonstatic members of pointer-to-member type.
> 

这类数据结构是 C 兼容的，表现和 C 程序一致。

## Abstract Data Type (ADT)

加入 ADT 结构之后，程序会在特定情况下调用给定的构造、析构函数。虽然这带来了微小的性能损失，但在工程上带来的好处常常更大。

## 为继承做准备

在加入虚函数后，编译器必须引入虚表，并提供一系列底层的对 vptr 的设置和转换操作，包括：

- 向构造函数中添加初始化 vptr 的代码
- 合成有意义的 copy construcor、copy assignment constructor

这些内容的引入让对象不再满足从前的 bitwise 语义，因此在函数传入、返回、拷贝赋值的过程中均会引入额外的消耗。一般而言，当设计中有大量函数需要以传值的方式返回一个 local class object ，那么提供一个 copy constructor 就比较合理。这样的 copy constructor 可以触发 NRV 优化，避免额外对象的构建。

# 5.2 继承体系下的对象构造

在一个 class object 被定义时，其 constructor 中可能含有大量的隐藏码，可能包括了：

- 所有 virtual base class constructors 必须被按声明和层次顺序调用
    - 如果在 member initialization list 中，则使用明确指定的参数初始化 base class
    - 如果不在 member initialization list 中，则寻找 default constructor 初始化 base class
    - 每一个 subobject 的偏移量需要在执行期可存取
- 所有 base class constructor 按声明顺序调用
    - 如果在 member initialization list 中，则使用明确指定的参数初始化 base class
    - 如果不在 member initialization list 中，则寻找 default constructor 初始化 base class
    - 在 base class 是
- vptr 的初始化
- 记录在 member initialization list 中的变量初始化
- 含有 default constructor 且未在 member initialization list 中的变量初始化

## 虚拟继承

当引入虚拟继承后，constructor 的扩充就不同于传统的只需要考虑 data members 和 vptr 的初始化问题了。在串式的 constructor 调用过程中 virtual base class 的 constructor 可能被重复调用，这要求编译器只在当一个完整的 class object 被构建时调用 virtual base class 的 constructor ，而在处理 subobjects 时压抑它们的调用。

## vptr 初始化

C++ 语言规则要求，在一个 class object 的 constructor（包括其 subobject 的 constructor ）中，经由构造中的对象调用 virtual function ，其函数实体应该是在当前 constructor 属于的 class 中定义的那个。这要求虚拟机制必须知道自己是否处于一个 constructor 中。

为了控制决定对应位置的 virtual functions 的名单，编译系统需要通过控制 vptr 的初始化。事实上这一操作是在 base class constructor 调用之后、程序员代码或 member initialization list 对应的代码之前完成的。

# 5.3 对象复制语义学

当使用一个 class object 指定给另一个 class object 时，有三种可能的选择：

1. 不做任何事，使用默认拷贝行为
2. 提供一个 copy assignment operator
3. 明确拒绝这个行为（通过提供一个 private 的 copy assignment operator）

在以下的情况下，一个 object 不符合 bitwise semantics ，而需要用户提供明确的 copy assignment operator ：

1. 其中含有一个具有用户定义的 copy assignment operator 的 member object
2. 其 base class 中存在 copy assignment operator 定义
3. 其声明了任何 virtual function（vptr 总是不能直接拷贝的）
4. 其继承自一个 virtual base class

但这种用户声明的内容总比默认情况要慢，还会出现因为不像 constructor 拥有 members initialization list 从而无法压抑 virtual base class 的 copy operators 的调用的情况。

解决这一情况的方法还是，如之前所说，不要允许 virtual base class 的拷贝操作、甚至不要在其中存放任何数据。

# 5.4 对象的效率

在对象符合 bitwise copy semantics 的时候效率最高，无论是 POD 、单继承或者是多继承效率均没有多少差别。为数不多的差距在于使用用户代码初始化成员变量和使用 members initialization list 初始化成员变量所带来的细微区别。

一旦引入 virtual function 或 virtual base class ，由于程序需要合成新的处理虚继承、虚函数的 copy assignment / constructor 内容，运行效率将大大降低。其中又以虚拟继承的情况尤甚。

# 5.5 解构语义学

如果类本身没有定义 destructor ，那么只在其内带的、或基类中的成员变量拥有destructor 的时候，编译器才会合成一个destructor ——即使其中使用了虚函数或者虚拟派生。

一个由程序员定义的 destructor 被扩展的方式类似 constructor 被扩展的方式，但是会反过来，其顺序如下：

1. 如果 object 内存在一个 vptr，会首先重设相关的 vtbl
2. destructor 的函数本身被执行
3. 如果其任何 member class object（不含基类中的）有 destructor ，它们会被执行
4. 如果有任何上一层的 nonvirtual base class 拥有 destructor ，它们会被执行
5. 如果有任何 virtual base class 拥有 destructor ，它们会被执行

一个 object 的生命周期结束于 destructor 被执行的时候。在执行期间，它会依次变成它的各个层级的基类被最终消亡。

> 这里的顺序可能有些问题，第一步实际上可能应该放在第三步后执行